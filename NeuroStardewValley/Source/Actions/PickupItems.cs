using NeuroSDKCsharp.Actions;
using NeuroSDKCsharp.Json;
using NeuroSDKCsharp.Websocket;
using NeuroStardewValley.Debug;
using NeuroStardewValley.Source.RegisterActions;
using NeuroStardewValley.Source.Utilities;
using StardewValley;
using StardewValley.Extensions;

namespace NeuroStardewValley.Source.Actions;

public class PickupItems : NeuroAction<KeyValuePair<List<Item>,List<int>>>
{
	public override string Name => "pick_up_items";
	protected override string Description => "Pick up items that are on the ground.";
	protected override JsonSchema Schema => new()
	{
		Type = JsonSchemaType.Object,
		Required = new List<string> { "items" },
		Properties = new Dictionary<string, JsonSchema>
		{
			["items"] = new()
			{
				Type = JsonSchemaType.Array,
				Items = new JsonSchema
				{
					Type = JsonSchemaType.Object,
					Required = { "item", "quantity" },
					Properties =
					{
						["item"] = new()
						{
							Type = JsonSchemaType.String,
							Enum = GetItemSchema()
						},
						["quantity"] = new()
						{
							Type = JsonSchemaType.Integer,
						}
					}
				}
			}
		}
	};
	protected override ExecutionResult Validate(ActionData actionData, out KeyValuePair<List<Item>,List<int>> resultData)
	{
		object? items = actionData.Data?.Value<object>("items");

		resultData = new();
		if (items is null)
		{
			return ExecutionResult.Failure($"");
		}
		
		if (!SchemaUtilities.SchemaToItemJson(items, out var jsons, out var e) || !jsons.Any())
		{
			Logger.Error($"Error in pick up validation {e}");
			return ExecutionResult.Failure($"There was an issue with parsing the json you provided: {e}");
		}

		var enumItems = SchemaUtilities.EnumToItem(Main.Bot.Debris.Debris.Select(Item? (debris) =>
			debris.item).ToList(), jsons.Select(json => json.Item).ToList(), false, false);
		resultData = SchemaUtilities.ItemJsonToItem(jsons,
			Main.Bot.Debris.Debris.Select(Item? (debris) => debris.item).ToList(), enumItems);
		
		if (resultData.Key.Count != resultData.Value.Count)
			return ExecutionResult.Failure($"");
		
		return ExecutionResult.Success($"Picking up items");
	}

	protected override async void Execute(KeyValuePair<List<Item>,List<int>> resultData)
	{
		try
		{
			Dictionary<Item, List<Debris>> nearestDebris = new();
			foreach (var debris in Main.Bot.Debris.Debris)
			{
				foreach (var item in resultData.Key)
				{
					// we check this as we could pick up by being in range
					if (debris is null || debris.item.ItemId != item.ItemId) continue;
					
					if (nearestDebris.TryAdd(item, new() {debris})) continue;
					
					nearestDebris[item].Add(debris);
				}
			}
			
			foreach (var kvp in nearestDebris)
			{
				kvp.Value.Sort(SortDebris);
			}
			
			for (int i = 0; i < resultData.Key.Count; i++)
			{
				Item item = resultData.Key[i];
				int quantity = resultData.Value[i];
				int? newAmount = null;
				List<Item> items = Main.Bot.Inventory.Inventory.Where(it => it is not null && it.ItemId == item.ItemId).ToList();
				var startingAmount = items.Aggregate<Item, int?>(null, (current, it) => current + it.Stack);
				
				foreach (var debris in nearestDebris[item])
				{
					// we check this as we could pick up debris by being in range while walking or just generally
					if (!Main.Bot.Debris.Debris.Contains(debris) || debris.item.ItemId != item.ItemId) continue;
					Logger.Warning($"item {item.DisplayName}   quantity: {quantity}   item stack: {debris.item.Stack}");
					Logger.Info($"starting amount: {startingAmount}");
				
					await Main.Bot.Debris.PickUpDebris(debris);
					// we wait so it has time to move to player and enter the inventory in case the approximate position isn't amazing.
					await Util.WaitForSeconds(0.5);
					if (startingAmount is not null || newAmount is not null)
					{
						Logger.Info($"quantity is greater");
						items = Main.Bot.Inventory.Inventory.Where(it => it is not null && it.ItemId == item.ItemId).ToList();
						Logger.Info($"items: {items.Count}");
						newAmount = items.Aggregate<Item, int?>(null, (current, it) => current + it.Stack);
					}
					// we should only run this if we know the next debris is going satisfy the wanted quantity
					if (startingAmount is null || newAmount is null || newAmount > startingAmount + quantity)
					{
						await Util.WaitForSeconds(0.25);
						break;
					}
				
					if (newAmount >= startingAmount + quantity)
					{
						resultData.Key.RemoveWhere(it => it.ItemId == item.ItemId);
						nearestDebris.Remove(item);
						break;
					}
					await Util.WaitForSeconds(0.25);
				}

				RegisterMainActions.RegisterPostAction();
			}
		}
		catch (Exception e)
		{
			await TaskDispatcher.SwitchToMainThread();
			Logger.Error($"Issue when picking up items {e}");
			RegisterMainActions.RegisterPostAction();
		}
	}

	private static List<object> GetItemSchema()
	{
		List<object> names = new();
		
		foreach (var debris in Main.Bot.Debris.Debris)
		{
			if (names.Contains(debris.item.DisplayName)) continue;
			
			names.Add(debris.item.DisplayName);
		}

		return names;
	}

	private static int SortDebris(Debris debris1, Debris debris2)
	{
		return Util.SortObjectsByDistance(Main.Bot.Debris.DebrisPosition(debris1.Chunks).ToPoint(),
			Main.Bot.Debris.DebrisPosition(debris2.Chunks).ToPoint());
	}
}