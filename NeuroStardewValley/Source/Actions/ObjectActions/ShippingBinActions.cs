using Microsoft.Xna.Framework;
using NeuroSDKCsharp.Actions;
using NeuroSDKCsharp.Json;
using NeuroSDKCsharp.Websocket;
using NeuroStardewValley.Debug;
using NeuroStardewValley.Source.ContextStrings;
using NeuroStardewValley.Source.RegisterActions;
using NeuroStardewValley.Source.Utilities;
using Newtonsoft.Json.Linq;
using StardewBotFramework.Source.Modules.Pathfinding.Base;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.Menus;

namespace NeuroStardewValley.Source.Actions.ObjectActions;

public static class ShippingBinActions
{
	public static void RegisterBinActions()
	{
		ActionWindow window = ActionWindow.Create(Main.GameInstance);
		window.AddAction(new ExitBin()).AddAction(new SellItems());
		string state =
			$"These are the items you can ship: {InventoryContext.GetShippableString(Main.Bot.Inventory.Inventory)}"; 
		if (Game1.getFarm().lastItemShipped is not null)
		{
			window.AddAction(new GrabLastInsertedItem());
			Item lastItem = Game1.getFarm().lastItemShipped;
			state += $"\nYou last shipped {lastItem.Stack} {lastItem.DisplayName}s";
		}
		window.SetForce(0,"You have opened the nearest shipping bin, you can use this to sell items." + 
		            " This should be your main source of making money", state,true).Register();
	}
	
	public class GoToNearestShippingBin : NeuroAction<ShippingBin>
	{
		private static ShippingBin _bin = new();
		public override string Name => "go_to_nearest_shipping";
		protected override string Description => "Walk and open nearest shipping bin";
		protected override JsonSchema Schema => new();

		protected override ExecutionResult Validate(ActionData actionData, out ShippingBin resultData)
		{
			if (Main.Bot.ShippingBinInteraction.GetShippingBinLocations(Game1.currentLocation).Count == 0)
			{
				resultData = new ShippingBin();
				return ExecutionResult.Failure($"There are no shipping bins in this location");
			}

			List<ShippingBin> bins = Main.Bot.ShippingBinInteraction.GetShippingBinsInLocation(Game1.currentLocation);
			Dictionary<int, Queue<ShippingBin>> dictionary = ClosestShippingBin(Game1.player.TilePoint, bins);
			int lowestIndex = 0;
			foreach (var kvp in dictionary)
			{
				if (kvp.Key < lowestIndex || lowestIndex == 0)
				{
					lowestIndex = kvp.Key;
				}
			}

			resultData = dictionary[lowestIndex].Dequeue();
			return ExecutionResult.Success("Walking to shipping bin now.");
		}

		protected override async void Execute(ShippingBin? resultData)
		{
			try
			{
				if (resultData is null) return;
		
				await Main.Bot.Pathfinding.Goto(new Goal.GoalNearby(resultData.tileX.Value, resultData.tileY.Value, 1));
				await TaskDispatcher.SwitchToMainThread();
				_bin = resultData;
				Main.Bot.ShippingBinInteraction.OpenBin(_bin);
				// if the farmer is not facing will not open so double check
				await Util.WaitForSeconds(1);
				if (Game1.activeClickableMenu is not null) return;
				
				RegisterMainActions.RegisterPostAction();
			}
			catch (Exception e)
			{
				Logger.Error($"{e}");
				await TaskDispatcher.SwitchToMainThread();
				if (Game1.activeClickableMenu is ItemGrabMenu)
				{
					RegisterBinActions();
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

	public class SellItems : NeuroAction<List<int>>
	{
		public override string Name => "sell_items";
		protected override string Description => "Add items from your inventory to be sold, If there is already an item " +
		                                         "in the shipping bin it will replace your ability to grab it with the new item.";
		protected override JsonSchema Schema => new()
		{
			Type = JsonSchemaType.Object,
			Required = new List<string> { "item_index" },
			Properties = new Dictionary<string, JsonSchema>
			{
				["item_index"] = new()
				{
					Type = JsonSchemaType.Array,
					Items = new JsonSchema { Type = JsonSchemaType.Integer },
				}
			}
		};

		protected override ExecutionResult Validate(ActionData actionData, out List<int>? resultData)
		{
			var objarray = actionData.Data?.Value<object>("item_index");

			resultData = new();
			if (objarray is null)
			{
				return ExecutionResult.Failure($"item index is null");
			}

			List<int> array = new();
			foreach (var token in (JArray)objarray)
			{
				if (token.Value<int?>() is null) continue;
				
				array.Add(token.Value<int>());
			}
			
			foreach (var item in array)
			{
				if (item > Main.Bot.Inventory.MaxInventory - 1)
				{
					return ExecutionResult.Failure($"{item} is a larger index than there are slots in your inventory. You can only go up to {Main.Bot.Inventory.MaxInventory - 1}");
				}
				if (item < 0)
				{
					return ExecutionResult.Failure($"{item} is less than 0, the lowest possible index is 0.");
				}
				
				resultData.Add(item);
			}
			
			List<Item> items = new();
			foreach (var index in resultData)
			{
				items.Add(Main.Bot.Inventory.Inventory[index]);	
			}
			return ExecutionResult.Success($"You have sold: {string.Concat(items.Select(item => $"\n{item.stack} {InventoryContext.QualityStrings[item.Quality]} {item.DisplayName}").ToList())}.");
		}

		protected override void Execute(List<int>? resultData)
		{
			List<Item> items = new();
			if (resultData is null) return;
			foreach (var index in resultData)
			{
				items.Add(Main.Bot.Inventory.Inventory[index]);
			}
			Main.Bot.ShippingBinInteraction.ShipMultipleItems(items.ToArray());
			RegisterBinActions();
		}
	}

	public class GrabLastInsertedItem : NeuroAction
	{
		public override string Name => "grab_last_item";
		protected override string Description => "Grab the last item inserted into this shipping bin, it will be placed" +
		                                         " in the first empty slot in your inventory.";
		protected override JsonSchema Schema => new();
		protected override ExecutionResult Validate(ActionData actionData)
		{
			Item? lastInsertedItem = Main.Bot.ShippingBinInteraction.GetLastItem(); 
			if (lastInsertedItem is null)
			{
				return ExecutionResult.Failure($"No item has been inserted into the bin yet");
			}

			return ExecutionResult.Success($"Getting the {lastInsertedItem.DisplayName}");
		}

		protected override void Execute()
		{
			Main.Bot.ShippingBinInteraction.GrabLastItem();
			RegisterBinActions();
		}
	}

	public class ExitBin : NeuroAction
	{
		public override string Name => "exit_bin";
		protected override string Description => "Close the bin and go back to the game";
		protected override JsonSchema Schema => new();
		protected override ExecutionResult Validate(ActionData actionData)
		{
			return ExecutionResult.Success($"You have left the shipping bin.");
		}
		protected override void Execute()
		{
			Main.Bot.ShippingBinInteraction.RemoveMenu();
		}
	}
}