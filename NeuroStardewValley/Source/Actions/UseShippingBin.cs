using Microsoft.Xna.Framework;
using NeuroSDKCsharp.Actions;
using NeuroSDKCsharp.Json;
using NeuroSDKCsharp.Websocket;
using NeuroStardewValley.Debug;
using NeuroStardewValley.Source.RegisterActions;
using NeuroStardewValley.Source.Utilities;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.Menus;
using static NeuroStardewValley.Source.Utilities.SchemaUtilities;

namespace NeuroStardewValley.Source.Actions;

public class UseShippingBin : NeuroAction<KeyValuePair<List<Item>,List<int>>>
{
	public override string Name => "use_shipping_bin";
	protected override string Description => "Use the nearest shipping bin and sell the specified items.";

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
						["item"] = new() { Type = JsonSchemaType.String, Enum = ItemEnum(Main.Bot.Inventory.Inventory, item => item.canBeShipped())},
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
		object? itemNames = actionData.Data?.Value<object>("items");

		resultData = new();
		if (itemNames is null) return ExecutionResult.Failure($"You cannot provide a null value");
		
		if (!SchemaToItemJson(itemNames, out List<ItemJson> items, out var e))
		{
			return ExecutionResult.Failure(
				$"You provided invalid json, look at this error message and think about the many mistakes" +
				$" you have made in your life to get to this point. {e}");
		}
		
		resultData = new(new(), new());
		var jsonItems = EnumToItem(Main.Bot.Inventory.Inventory, items.Select(json => json.Item).ToList());
		foreach (var item in Main.Bot.Inventory.Inventory)
		{
			if (item is null) continue;
			for (int i = 0; i < jsonItems.Count; i++)
			{
				var json = jsonItems[i];
				Logger.Info($"json item: {json.DisplayName}   count: {json.Stack}   item: {item.DisplayName}   {item.Stack}");
				if (json.DisplayName != item.DisplayName || json.Stack > item.Stack) continue;

				resultData.Key.Add(item);
				resultData.Value.Add(items[i].Quantity);
			}
		}

		if (resultData.Key.Count == items.Count && resultData.Value.Count == items.Count)
			return ExecutionResult.Success($"Adding items to the shipping bin.");
		
		Logger.Error($"key count: {resultData.Key.Count}  value count: {resultData.Value.Count}   item count: {items.Count}");
		return ExecutionResult.Failure($"You are either missing certain items or you have provided a higher quantity than the item actually has.");
	}

	protected override async void Execute(KeyValuePair<List<Item>,List<int>> resultData)
	{
		try
		{
			var dictionary = ClosestShippingBin(Main.Bot._farmer.TilePoint,
				Main.Bot.ShippingBinInteraction.GetShippingBinsInLocation(Main.Bot._currentLocation));
			int lowestIndex = 0;
			foreach (var kvp in dictionary)
			{
				if (kvp.Key < lowestIndex || lowestIndex == 0)
				{
					lowestIndex = kvp.Key;
				}
			}
			ShippingBin shippingBin = dictionary[lowestIndex].Dequeue();
		
			await TileUtilities.PathfindToObject(new Point(shippingBin.tileX.Value,shippingBin.tileY.Value));
			await TaskDispatcher.SwitchToMainThread();
			Main.Bot.ShippingBinInteraction.OpenBin(shippingBin);
			// if the farmer is not facing will not open so double check if the menu appears
			await Util.WaitForSeconds(1);
			if (Game1.activeClickableMenu is not ItemGrabMenu)
			{
				// This could cause an issue if it opens a menu that does not register after it is closed. I don't think that can happen here though.
				if (Game1.activeClickableMenu is not null)
				{
					Game1.activeClickableMenu.exitThisMenu(false);
					return;
				}

				RegisterMainActions.RegisterPostAction();
				return;
			}

			for (int i = 0; i < resultData.Key.Count; i++)
			{
				Main.Bot.ShippingBinInteraction.AddItemAmount(resultData.Key[i],resultData.Value[i]);
				await Util.WaitForSeconds(0.3);
			}
			
			await Util.WaitForSeconds(2);
			// actions get registered when exiting menu.
			Main.Bot.ShippingBinInteraction.RemoveMenu();
		}
		catch (Exception e)
		{
			Logger.Error($"{e}");
			await TaskDispatcher.SwitchToMainThread();
			if (Game1.activeClickableMenu is ItemGrabMenu)
			{
				Game1.activeClickableMenu.exitThisMenuNoSound();
				return;
			}
			RegisterMainActions.RegisterPostAction();
		}
	}
	
	private static Dictionary<int, Queue<ShippingBin>> ClosestShippingBin(Point place, List<ShippingBin> bins)
	{
		Dictionary<int, Queue<ShippingBin>> points = new();
		foreach (var bin in bins)
		{
			int point = Math.Abs(place.X - bin.tileX.Value + place.Y - bin.tileY.Value);
			if (points.ContainsKey(point))
			{
				points[point].Enqueue(bin);
			}
			else
			{
				Queue<ShippingBin> queue = new();
				queue.Enqueue(bin);
				points.Add(point, queue);
			}
		}

		return points;
	}
}