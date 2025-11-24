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
using static NeuroStardewValley.Source.Utilities.SchemaUtilities;
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

	public class TakeItemFromChest : NeuroAction<Dictionary<Chest, List<Item>>>
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
							["item"] = new() { Type = JsonSchemaType.String, Enum = GetItemsFromChest().Values.SelectMany(inv => ItemEnum(inv,null,true)).ToList()},
							["quantity"] = new()
							{
								Type = JsonSchemaType.Integer
							}
						}
					}
				}
			}
		};
		protected override ExecutionResult Validate(ActionData actionData, out Dictionary<Chest, List<Item>> resultData)
		{
			var itemIndex = actionData.Data?.Value<object>("items");

			resultData = new();
			if (itemIndex is null)
			{
				return ExecutionResult.Failure($"You provided an invalid value.");
			}
			
			if (!SchemaToItemJson(itemIndex, out var items, out var e))
			{
				return ExecutionResult.Failure(
					$"You provided invalid json, look at this error message and think about the many mistakes" +
					$" you have made in your life to get to this point. {e}");
			}

			Logger.Info($"items amount: {items.Count}");
			if (!items.Any())
				return ExecutionResult.Failure($"You have either not provided any items or not provided any valid items.");

			if (!InventoryUtils.CanFitAmount(items.Count))
				return ExecutionResult.Failure($"You cannot fit this many items in your inventory.");
			
			Dictionary<Chest,List<Item>> validItems = new();
			Dictionary<Chest,List<ItemJson>> quantities = new();
			foreach (var kvp in GetItemsFromChest())
			{
				foreach (var item in kvp.Value)
				{
					foreach (var json in items.Where(json =>
						         json.Item == $"{kvp.Value.IndexOf(item)}: {item.DisplayName} amount: {item.Stack}" &&
						         json.Quantity <= item.Stack))
					{
						if (!validItems.ContainsKey(kvp.Key))
						{
							validItems.Add(kvp.Key,new() {item});
							if (!quantities.ContainsKey(kvp.Key))
							{
								quantities.Add(kvp.Key,new() {json});
								continue;
							}
							
							quantities[kvp.Key].Add(json);
							continue;
						}
						
						validItems[kvp.Key].Add(item);
						quantities[kvp.Key].Add(json);
					}
				}
			}

			if (validItems.Any(kvp => !InventoryUtils.CanFitAmount(kvp.Value)))
			{
				return ExecutionResult.Failure($"You are either missing certain items or you have provided a higher quantity than the item actually has.");
			}
			
			resultData = validItems;
			_quantities = quantities;
			return ExecutionResult.Success($"Taking the items from the nearest chests.");
		}

		private Dictionary<Chest, List<ItemJson>> _quantities = new();
		protected override async void Execute(Dictionary<Chest, List<Item>>? resultData)
		{
			try
			{
				if (resultData is null) return;

				Chest? previousChest = null;
				foreach (var chest in GetNearestChests().Where(resultData.ContainsKey))
				{
					if (Game1.activeClickableMenu is ItemGrabMenu && chest != previousChest) Main.Bot.Chest.CloseChest();
					previousChest = chest;
					
					await TileUtilities.PathfindToObject(chest);
					await Util.WaitForSeconds(0.3);
					Main.Bot.Chest.OpenChest(chest);
					await Util.WaitForSeconds(0.3);
					
					// if bot couldn't open chest for whatever reason
					if (Game1.activeClickableMenu is not ItemGrabMenu) continue;

					for (int i = 0; i < resultData[chest].Count; i++)
					{
						// TODO: this does remove the correct amount of the item it just doesn't add it to the inventory. There is a temporary solution but I don't like it.
						Main.Bot.ItemGrabMenu.RemoveItemAmount(resultData[chest][i],_quantities[chest][i].Quantity);
						var item = resultData[chest][i].getOne();
						item.Stack = _quantities[chest][i].Quantity;
						Main.Bot._farmer.addItemToInventory(item);
						await Util.WaitForSeconds(0.3);
					}
				}
				
				Main.Bot.Chest.CloseChest();
			}
			catch (Exception e)
			{
				Logger.Error($"{e}");
				await TaskDispatcher.SwitchToMainThread();
				if (Game1.activeClickableMenu is not null)
				{
					Game1.activeClickableMenu = null;
					return;
				}

				RegisterMainActions.RegisterPostAction();
			}
		}
		private static Dictionary<Chest, IInventory> GetItemsFromChest()
		{
			return GetNearestChests().ToDictionary<Chest, Chest, IInventory>(chest => chest, chest => chest.Items);
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

			if (!SchemaToItemJson(itemIndex, out var items, out var e))
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
				return ExecutionResult.Success($"Adding items to the nearest chests.");
			
			Logger.Error($"key count: {resultData.Key.Count}  value count: {resultData.Value.Count}   item count: {items.Count}");
			return ExecutionResult.Failure($"You are either missing certain items or you have provided a higher quantity than the item actually has.");
		}

		protected override async void Execute(KeyValuePair<List<Item>, List<int>> resultData)
		{
			try
			{
				Chest? previousChest = null;
				for (int i = 0; i < resultData.Key.Count; i++)
				{
					Item item = resultData.Key[i];
					int amount = resultData.Value[i];
					Logger.Info($"item: {item.DisplayName} {item.Stack}  amount: {amount}");

					// this shouldn't happen here just in case though
					if (!GetNearestChests().Any())
					{
						Logger.Error($"There are no longer any chests nearby :(    i: {i}");
						continue;
					}
					
					Chest? chest = null;
					foreach (var c in GetNearestChests())
					{
						List<Item> matchingItems = c.Items.Where(item1 =>
							item1.ItemId == item.ItemId && item1.Stack + item.Stack < item.maximumStackSize()).ToList();
						if (!matchingItems.Any()) continue;

						chest = c;
						break;
					}
					
					// we do this here to prevent stackable item chests from the above foreach
					if (chest is null)
					{
						foreach (var c in GetNearestChests().Where(c => c.Items.Count != c.GetActualCapacity()))
						{
							chest = c;
							break;
						}
					}
					if (chest is null) continue;
					
					if (previousChest is not null && previousChest.TileLocation != chest.TileLocation)
					{
						RegisterMainActions.BlockRegistering = true;
						Main.Bot.Chest.CloseChest();
					}

					previousChest = chest;

					if (Game1.activeClickableMenu is ItemGrabMenu)
					{
						Main.Bot.ItemGrabMenu.AddItemAmount(item, amount);
						continue;
					}
			
					await TileUtilities.PathfindToObject(chest);
					await Util.WaitForSeconds(0.3);
					Main.Bot.Chest.OpenChest(chest);
					await Util.WaitForSeconds(0.3);	
					
					// if bot couldn't open chest for whatever reason
					if (Game1.activeClickableMenu is not ItemGrabMenu) continue;

					Main.Bot.ItemGrabMenu.AddItemAmount(item, amount);
				}

				await Util.WaitForSeconds(0.75);
				Main.Bot.Chest.CloseChest();
			}
			catch (Exception e)
			{
				await TaskDispatcher.SwitchToMainThread();
				Logger.Error($"{e}");
				RegisterMainActions.RegisterPostAction();
			}
		}
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