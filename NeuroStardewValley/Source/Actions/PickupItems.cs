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
							Enum = SchemaUtilities.ItemEnum(Main.Bot.Debris.Debris.Select(Item? (debris) => debris.item).ToList(), null , true)
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
		
		if (!SchemaUtilities.SchemaToItemJson(items, out var jsons, out var e))
		{
			Logger.Error($"Error in pick up validation {e}");
			return ExecutionResult.Failure($"There was an issue with parsing the json you provided: {e}");
		}

		var enumItems = SchemaUtilities.EnumToItem(Main.Bot.Debris.Debris.Select(Item? (debris) =>
			debris.item).ToList(), jsons.Select(json => json.Item).ToList(), true);
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
			foreach (var debris in Main.Bot.Debris.Debris)
			{
				for (int i = 0; i < resultData.Key.Count; i++)
				{
					Item item = resultData.Key[i];
					int quantity = resultData.Value[i];
					// we check this as we could pick up by being in range
					if (debris is null || debris.item.ItemId != item.ItemId) continue;
					List<Item> items;
					int? startingAmount = null;
					if (quantity > debris.item.Stack)
					{
						items = Main.Bot.Inventory.Inventory.Where(it => it is not null && it.ItemId == item.ItemId).ToList();
						startingAmount = items.Aggregate<Item, int?>(null, (current, it) => current + it.Stack) ?? null;
					}
					
					await Main.Bot.Debris.PickUpDebris(debris);
					if (startingAmount is null)
					{
						await Util.WaitForSeconds(0.25);
						continue;
					}
					
					items = Main.Bot.Inventory.Inventory.Where(it => it is not null && it.ItemId == item.ItemId).ToList();
					int? newAmount = items.Aggregate<Item, int?>(null, (current, it) => current + it.Stack) ?? null;
					if (newAmount is not null && newAmount >= startingAmount + quantity)
					{
						resultData.Key.RemoveWhere(it => it.ItemId == item.ItemId);
					}
					await Util.WaitForSeconds(0.25);
				}
			}
		}
		catch (Exception e)
		{
			await TaskDispatcher.SwitchToMainThread();
			Logger.Error($"Issue when picking up items {e}");
			RegisterMainActions.RegisterPostAction();
		}
	}
}