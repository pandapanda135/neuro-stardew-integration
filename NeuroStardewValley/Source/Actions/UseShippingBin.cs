using Microsoft.Xna.Framework;
using NeuroSDKCsharp.Actions;
using NeuroSDKCsharp.Json;
using NeuroSDKCsharp.Websocket;
using NeuroStardewValley.Debug;
using NeuroStardewValley.Source.RegisterActions;
using NeuroStardewValley.Source.Utilities;
using Newtonsoft.Json.Linq;
using StardewBotFramework.Source.Modules.Pathfinding.Base;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.Menus;

namespace NeuroStardewValley.Source.Actions;

public class UseShippingBin : NeuroAction<List<Item>>
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
				Items = new JsonSchema { Enum = Main.Bot.Inventory.Inventory.Where(item => item is not null && item.canBeShipped()).Select(object (item) => item.DisplayName).ToList()},
			}
		}
	};
	protected override ExecutionResult Validate(ActionData actionData, out List<Item>? resultData)
	{
		JArray? itemNames = actionData.Data?.Value<JArray>("items");

		resultData = new();
		if (itemNames is null) return ExecutionResult.Failure($"You cannot provide a null value");
		List<string> names = new();
		foreach (var itemName in itemNames)
		{
			string? s = itemName.Value<string>();
			if (s is null) return ExecutionResult.Failure($"You provided an invalid value.");
			names.Add(s);
		}
		
		List<Item> items = new();
		foreach (var item in Main.Bot.Inventory.Inventory.Where(item => item is not null && names.Contains(item.DisplayName)))
		{
			if (items.Contains(item)) continue;
			items.Add(item);
		}
		if (!items.Any()) return ExecutionResult.Failure($"You have not provided any items");

		Logger.Info($"items count: {items.Count}");
		resultData = items;
		return ExecutionResult.Success();
	}

	protected override async void Execute(List<Item>? resultData)
	{
		try
		{
			if (resultData is null || !resultData.Any()) return;
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
		
			await Main.Bot.Pathfinding.Goto(new Goal.GoalNearby(shippingBin.tileX.Value, shippingBin.tileY.Value, 1));
			await TaskDispatcher.SwitchToMainThread();
			Main.Bot.ShippingBinInteraction.OpenBin(shippingBin);
			// if the farmer is not facing will not open so double check if the menu appears
			await Utils.WaitForSeconds(1);
			await TaskDispatcher.SwitchToMainThread();
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
			
			Main.Bot.ShippingBinInteraction.ShipMultipleItems(resultData.ToArray());
			await Utils.WaitForSeconds(5);
			await TaskDispatcher.SwitchToMainThread();
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