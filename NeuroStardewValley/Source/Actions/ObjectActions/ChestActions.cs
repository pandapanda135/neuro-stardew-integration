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
using StardewValley.Inventories;
using StardewValley.Menus;
using StardewValley.Objects;
using Object = StardewValley.Object;

namespace NeuroStardewValley.Source.Actions.ObjectActions;

public static class ChestActions
{
	public static Chest? Chest { get; set; }

	public class OpenChest : NeuroAction<Chest>
	{
		public override string Name => "open_chest";
		protected override string Description => "Open a chest that is in the current location";

		protected override JsonSchema Schema => new()
		{
			Type = JsonSchemaType.Object,
			Required = new List<string> { "chest_position" },
			Properties = new Dictionary<string, JsonSchema>
			{
				["chest_position"] = QJS.Enum(GetChestsLocations(out _))
			}
		};
		protected override ExecutionResult Validate(ActionData actionData, out Chest? resultData)
		{
			string? providedPos = actionData.Data?.Value<string>("chest_position");

			if (providedPos is null)
			{
				resultData = null;
				return ExecutionResult.Failure($"chest_position was null");
			}

			int index = GetChestsLocations(out var list).IndexOf(providedPos);
			Chest chest = list[index];
			// Point point = new Point((int)providedPos, (int)y);
			if (!TileUtilities.IsValidTile(chest.TileLocation.ToPoint(), out var reason, false,false))
			{
				resultData = null;
				return ExecutionResult.Failure(reason);
			}
			
			var objects = StringUtilities.GetObjectsInLocation(new Chest());
			if (!objects.TryGetValue(chest.TileLocation.ToPoint(), out var _))
			{
				resultData = null;
				return ExecutionResult.Failure($"There is not a chest at the provided location");
			}

			resultData = chest;
			return ExecutionResult.Success();
		}

		protected override async void Execute(Chest? resultData)
		{
			try
			{
				if (resultData is null) return;
			
				foreach (var kvp in Main.Bot._currentLocation.Objects.Pairs.Where(kvp => kvp.Value == resultData))
				{
					await Main.Bot.Pathfinding.Goto(new Goal.GetToTile(kvp.Key.ToPoint().X,kvp.Key.ToPoint().Y));
					await TaskDispatcher.SwitchToMainThread();
					Open(resultData);
				}
			}
			catch (Exception e)
			{
				Logger.Error($"{e}");
				await TaskDispatcher.SwitchToMainThread();
				if (Game1.activeClickableMenu is ItemGrabMenu)
				{
					RegisterChestActions();
					return;
				}
				RegisterMainActions.RegisterPostAction();
			}
		}
		
		private static void Open(Chest chest)
		{
			Chest = chest;
			Dictionary<Point, Object> objects = StringUtilities.GetObjectsInLocation(Chest);

			if (!objects.ContainsValue(Chest))
			{
				return;
			}

			Main.Bot.Chest.OpenChest(Chest);
		}

		private static List<string> GetChestsLocations(out List<Chest> chests)
		{
			List<string> chestPoints = new();
			chests = new();
			foreach (var kvp in Game1.currentLocation.Objects.Pairs)
			{
				if (kvp.Value is not Chest chest) continue;
				
				chests.Add(chest);
				chestPoints.Add(kvp.Key.ToPoint().ToString());
			}

			return chestPoints;
		}
	}
	
	private class CloseChest : NeuroAction
	{
		public override string Name => "close_chest";
		protected override string Description => "Close the currently opened chest.";
		protected override JsonSchema Schema => new ();
		protected override ExecutionResult Validate(ActionData actionData)
		{
			if (Chest is null) return ExecutionResult.ModFailure($"A chest is not currently opened, that means this action should not have been registered. Sorry.");
			return ExecutionResult.Success($"Closed the chest at {Chest.TileLocation}");
		}

		protected override void Execute()
		{
			if (Chest is null) return;
			Main.Bot.Chest.CloseChest();
			Main.Bot.ItemGrabMenu.RemoveMenu(); // do this as colour changing is in here
		}
	}
	
	private class AddItemsToChest : NeuroAction<KeyValuePair<List<Item>,List<int>>>
	{
		public override string Name => "insert_items";
		protected override string Description => $"Insert items in this chest, you should make sure the amount of items" +
		                                         $" you send can be fit in this chest. The max capacity of this chest" +
		                                         $" assuming no items is {Chest?.GetActualCapacity() - 1}";
		protected override JsonSchema Schema => new ()
		{
			Type = JsonSchemaType.Object,
			Required = new List<string> { "item_index" },
			Properties = new Dictionary<string, JsonSchema>
			{
				["item_index"] = new()
				{
					Type = JsonSchemaType.Array,
					Items = new JsonSchema { Enum = ItemEnum(Main.Bot.Inventory.Inventory)},
				},
				["amount"] = new()
				{
					Type = JsonSchemaType.Array,
					Items = new JsonSchema { Type = JsonSchemaType.Integer },
				}
			}
		};
		protected override ExecutionResult Validate(ActionData actionData,out KeyValuePair<List<Item>,List<int>> resultData)
		{
			if (Chest is null)
			{
				resultData = new();
				return ExecutionResult.ModFailure($"A chest is not currently opened, that means this action should not have been registered. Sorry.");
			}
			var itemArray = actionData.Data?.Value<object>("item_index");
			var amountArray = actionData.Data?.Value<object>("amount");

			resultData = new();
			if (itemArray is null || amountArray is null)
			{
				return ExecutionResult.Failure($"item index is null");
			}

			List<int> amount = (from token in (JArray)amountArray select token.Value<int>()).Select(i => i).ToList();

			List<string> itemStrings = new();
			foreach (var token in (JArray)itemArray)
			{
				if (token.Value<string?>() is null) continue;
				
				// if (token.Value<string>() < 0 || token.Value<string>() > Main.Bot.Inventory.Inventory.Count - 1 || Main.Bot.Inventory.Inventory[token.Value<string>()] is null)
					// return ExecutionResult.Failure($"{token.Value<int>()} cannot be accessed.");
				
				itemStrings.Add(token.Value<string>() ?? string.Empty);
			}

			List<Item> items = EnumToItem(Main.Bot.Inventory.Inventory,itemStrings);
			
			if (items.Count != amount.Count) return ExecutionResult.Failure($"You have specified {items.Count} items and only specified {amount.Count} item's amount, you need to specify the same amount of each.");

			Logger.Info($"item amount: {Chest.Items.Count}    amount: {amount.Count}");
			for (int i = 0; i < items.Count; i++)
			{
				Logger.Info($"item: {items[i].Stack}   {items[i].DisplayName}    amount: {amount[i]}");
				if (items[i].Stack >= amount[i] && amount[i] > 0) continue;
				
				return ExecutionResult.Failure($"You do not have {amount[i]} {items[i].DisplayName}");
			}
			
			if (items.Count > Chest.GetActualCapacity() - Chest.Items.Count(item => item is not null))
			{
				return ExecutionResult.Failure($"You have tried to add too many items to this chest.");
			}

			resultData = new(items,amount);
			
			List<string> itemNames = new();
			resultData.Key.ForEach(item => itemNames.Add(item.DisplayName));
			return ExecutionResult.Success($"You have added: {string.Join("\n",itemNames)} to the chest");
		}

		protected override void Execute(KeyValuePair<List<Item>,List<int>> resultData)
		{
			if (Chest is null) return;
			for (int i = 0; i < resultData.Key.Count; i++)
			{
				Main.Bot.ItemGrabMenu.AddItemAmount(resultData.Key[i],resultData.Value[i]);
			}
			
			RegisterChestActions();
		}
	}

	private class TakeItemsFromChest : NeuroAction<KeyValuePair<List<Item>,List<int>>>
	{
		public override string Name => "take_items";
		protected override string Description => $"Take items from this chest, indexes are calculated from 0 - amount of items, the amount being in this case {Chest?.Items.Count - 1}.";
		protected override JsonSchema Schema => new()
		{
			Type = JsonSchemaType.Object,
			Required = new List<string> { "item_index" },
			Properties = new Dictionary<string, JsonSchema>
			{
				["item_index"] = new()
				{
					Type = JsonSchemaType.Array,
					Items = new JsonSchema { Enum =  ItemEnum(Main.Bot.Chest.GetItems(Chest!))}
				},
				["amount"] = new()
				{
					Type = JsonSchemaType.Array,
					Items = new JsonSchema { Type = JsonSchemaType.Integer}
 				}
			}
		};
		protected override ExecutionResult Validate(ActionData actionData,out KeyValuePair<List<Item>,List<int>> resultData)
		{
			if (Chest is null)
			{
				resultData = new();
				return ExecutionResult.ModFailure($"A chest is not currently opened, that means this action should not have been registered. Sorry.");
			}
			var itemArray = actionData.Data?.Value<object>("item_index");
			var amountArray = actionData.Data?.Value<object>("amount");

			resultData = new();
			if (itemArray is null || amountArray is null)
			{
				return ExecutionResult.Failure($"you have either not specified any items or any amounts.");
			}

			List<int> amount = (from token in (JArray)amountArray select token.Value<int>()).Select(i => i).ToList();

			List<string> itemStrings = new();
			foreach (var token in (JArray)itemArray)
			{
				if (token.Value<string?>() is null) continue;
				
				itemStrings.Add(token.Value<string>() ?? string.Empty);
			}

			List<Item> items = EnumToItem(Main.Bot.ItemGrabMenu.Menu.ItemsToGrabMenu.actualInventory.ToList(),itemStrings);
			
			if (items.Count != amount.Count) return ExecutionResult.Failure($"You have specified {items.Count} items and only specified {amount.Count} item's amount, you need to specify the same amount of each.");

			Logger.Info($"item amount: {Chest.Items.Count}    amount: {amount.Count}");
			for (int i = 0; i < items.Count; i++)
			{
				Logger.Info($"item: {items[i].Stack}   {items[i].DisplayName}    amount: {amount[i]}");
				if (items[i].Stack >= amount[i] && amount[i] > 0) continue;
				
				return ExecutionResult.Failure($"The chest does not have {amount[i]} {items[i].DisplayName}");
			}

			resultData = new(items,amount);
			
			List<string> itemNames = new();
			for (int i = 0; i < resultData.Key.Count; i++)
			{
				itemNames.Add($"{resultData.Value[i]}: {resultData.Key[i].DisplayName}");
			}
			return ExecutionResult.Success($"You have removed: {string.Join("\n",itemNames)} from the chest.");
		}

		protected override void Execute(KeyValuePair<List<Item>,List<int>> resultData)
		{
			if (Chest is null) return;
			for (int i = 0; i < resultData.Key.Count; i++)
			{
				Item? item = Main.Bot.ItemGrabMenu.GetItemAmount(Chest.Items.ToList(),resultData.Key[i], resultData.Value[i]);
				if (item is null) continue;
				// Can't find a way to take and add :(
				Main.Bot.Chest.TakeItemFromChest(Chest,item,Main.Bot._farmer);
				Main.Bot._farmer.addItemToInventory(item);
			}
			
			RegisterChestActions();
		}
	}
	
	private static List<object> ItemEnum(IInventory inventory)
	{
		List<object> items = new();
		for (int i = 0; i < inventory.Count; i++)
		{
			if (inventory[i] is null) continue;
			items.Add($"{i}: {inventory[i].DisplayName}");
		}

		return items;
	}

	private static List<Item> EnumToItem(IInventory inventory,List<string> select) => EnumToItem(inventory.GetRange(0,inventory.Count).ToList(), select);
	
	private static List<Item> EnumToItem(List<Item?> inventory,List<string> select)
	{
		List<Item> items = new();
		foreach (var str in select)
		{
			for (int i = 0; i < inventory.Count; i++)
			{
				if (inventory[i] is null || str != $"{i}: {inventory[i]?.DisplayName}") continue;

				items.Add(inventory[i]!);
				break;
			}
		}
			
		return items;
	}
	
	public static void RegisterChestActions()
	{
		Logger.Info($"registering chest actions");
		ActionWindow window = ActionWindow.Create(Main.GameInstance);

		window.AddAction(new CloseChest()).AddAction(new AddItemsToChest()).AddAction(new TakeItemsFromChest());
		if (Main.Bot.ItemGrabMenu.Menu.colorPickerToggleButton.visible) window.AddAction(new ItemGrabActions.SelectColour());
		
		string nameList = InventoryContext.GetInventoryString(Chest!.Items, true);
		window.SetForce(0,$"You are now interacting with a chest", 
			$"These are the items in this chest: {nameList}.\n This is your inventory: " +
			$"{InventoryContext.GetInventoryString(Main.Bot.Inventory.Inventory,true)}",true);
		window.Register();
	}

	public struct ItemJson
	{
		public ItemJson(string item, int quantity)
		{
			Item = item;
			Quantity = quantity;
		}
			
		public readonly string Item;
		public readonly int Quantity;
	}
	public class TakeItemFromChest : NeuroAction<KeyValuePair<List<Chest>, List<ItemJson>>>
	{
		public override string Name => "take_items_to_chest";
		protected override string Description => "Take the provided items from chests near you";
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
							["item"] = new() { Type = JsonSchemaType.String, Enum = GetItemsFromChest().Values.SelectMany(inv => inv).Select(item => item.DisplayName).ToList<object>()},
							["quantity"] = new()
							{
								Type = JsonSchemaType.Integer,
							}
						}
					}
				}
			}
		};
		protected override ExecutionResult Validate(ActionData actionData, out KeyValuePair<List<Chest>, List<ItemJson>> resultData)
		{
			var itemIndex = actionData.Data?.Value<object>("items");

			resultData = new();
			if (itemIndex is null)
			{
				return ExecutionResult.Failure($"You provided an invalid value.");
			}

			List<ItemJson> items = new();
			try
			{
				foreach (var kvp in (JArray)itemIndex)
				{
					string? itemStr = kvp.Value<string>("item");
					int? quantity = kvp.Value<int>("quantity");
					if (itemStr is null || quantity is null) continue;
					var itemJson = new ItemJson(itemStr,quantity.Value);
					items.Add(itemJson);
					Logger.Info($"kvp 1: {kvp}     item json: {itemJson.Item}  {itemJson.Quantity}");
				}
			}
			catch (Exception e)
			{
				Logger.Error($"{e}");
				return ExecutionResult.Failure(
					$"You provided invalid json, look at this error message and think about the many mistakes" +
					$" you have made in your life to get to this point. {e}");
			}

			if (!InventoryUtils.CanFitAmount(items.Count))
			{
				return ExecutionResult.Failure($"You cannot fit this many items in your inventory.");
			}
			
			// TODO: make getting correct stack size work
			Dictionary<Chest,List<Item>> validItems = new();
			foreach (var kvp in GetItemsFromChest())
			{
				foreach (var item in kvp.Value)
				{
					if (items.All(json => json.Item != item.DisplayName)) continue;
					if (items.Any(json => json.Item == item.DisplayName && json.Quantity > item.Stack)) continue;

					if (!validItems.ContainsKey(kvp.Key))
					{
						validItems.Add(kvp.Key,new() {item});
						continue;
					}
					
					validItems[kvp.Key].Add(item);
				}
			}

			if (validItems.Any(kvp => !InventoryUtils.CanFitAmount(kvp.Value)))
			{
				return ExecutionResult.Failure($"You cannot fit certain items in your inventory.");
			}
			
			return ExecutionResult.Success($"");
		}

		protected override void Execute(KeyValuePair<List<Chest>, List<ItemJson>> resultData)
		{
			throw new NotImplementedException();
		}
		private static Dictionary<Chest, IInventory> GetItemsFromChest()
		{
			Dictionary<Chest, IInventory> items = new();
			foreach (var kvp in Main.Bot._currentLocation.Objects.Pairs)
			{
				if (kvp.Value is Chest chest)
				{
					items.Add(chest, chest.Items);	
				}
			}

			return items;
		}
	}

	public class AddItemToChest : NeuroAction<KeyValuePair<List<Item>,List<int>>>
	{
		public override string Name => "add_items_to_chest";
		protected override string Description => "Add items to chest";
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
							["item"] = new() { Type = JsonSchemaType.String, Enum = ItemEnum(Main.Bot.Inventory.Inventory)},
							["quantity"] = new()
							{
								Type = JsonSchemaType.Integer,
							}
						}
					}
				}
			}
		};
		protected override ExecutionResult Validate(ActionData actionData, out KeyValuePair<List<Item>, List<int>> resultData)
		{
			var itemIndex = actionData.Data?.Value<object>("items");

			resultData = new();
			if (itemIndex is null)
			{
				return ExecutionResult.Failure($"You provided an invalid value.");
			}

			if (!GetNearestChests().Any()) 
				return ExecutionResult.Failure($"There are no chests in this location, so this action should not be registered." +
				                               $" That means the developer of this integration is stupid :(");

			List<ItemJson> items = new();
			try
			{
				foreach (var kvp in (JArray)itemIndex)
				{
					string? itemStr = kvp.Value<string>("item");
					int? quantity = kvp.Value<int>("quantity");
					if (itemStr is null || quantity is null) continue;
					var itemJson = new ItemJson(itemStr,quantity.Value);
					items.Add(itemJson);
					Logger.Info($"kvp 1: {kvp}     item json: {itemJson.Item}  {itemJson.Quantity}");
				}
			}
			catch (Exception e)
			{
				Logger.Error($"{e}");
				return ExecutionResult.Failure(
					$"You provided invalid json, look at this error message and think about the many mistakes" +
					$" you have made in your life to get to this point. {e}");
			}

			resultData = new(new(), new());
			foreach (var item in Main.Bot.Inventory.Inventory)
			{
				if (item is null) continue;
				foreach (var json in items)
				{
					if (json.Item != item.DisplayName) continue;
					if ((json.Quantity + item.Stack) > item.maximumStackSize()) continue;
					
					resultData.Key.Add(item);
					resultData.Value.Add(json.Quantity);
				}	
			}

			if (resultData.Key.Count != items.Count || resultData.Value.Count != items.Count)
			{
				return ExecutionResult.Failure($"You do not have certain items.");
			}


			return ExecutionResult.Success();
		}

		protected override async void Execute(KeyValuePair<List<Item>, List<int>> resultData)
		{
			try
			{
				for (int i = 0; i < resultData.Key.Count; i++)
				{
					Item item = resultData.Key[i];
					int amount = resultData.Value[i];

					Chest? chest = null;
					foreach (var c in GetNearestChests())
					{
						if (!c.Items.Contains(item)) continue;

						chest = c;
					}

					chest ??= GetNearestChests()[0];
					
					// TODO: pathfinding and all that stuff

					await PathfindToChest(chest);
					await Util.WaitForSeconds(0.2);
					Main.Bot.Chest.OpenChest(chest);
					await Util.WaitForSeconds(0.1);

					if (Game1.activeClickableMenu is not ItemGrabMenu)
					{
						continue;
					}

					Main.Bot.ItemGrabMenu.AddItemAmount(item, amount);
				}
			}
			catch (Exception e)
			{
				await TaskDispatcher.SwitchToMainThread();
				Logger.Error($"{e}");
				RegisterMainActions.RegisterPostAction();
			}
		}
	}

	private static async Task PathfindToChest(Chest chest)
	{
		await TaskDispatcher.SwitchToMainThread();
		Point point = chest.TileLocation.ToPoint();
		await Main.Bot.Pathfinding.Goto(new Goal.GetToTile(point.X, point.Y));
	}

	private static List<Chest> GetNearestChests()
	{
		List<Chest> chests = new();

		foreach (var kvp in Main.Bot._currentLocation.Objects.Pairs)
		{
			if (kvp.Value is not Chest chest) continue;
			
			chests.Add(chest);
		}
		
		chests.Sort(Util.SortObjectsByDistance);
		return chests;
	}
}