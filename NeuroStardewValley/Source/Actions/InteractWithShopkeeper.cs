using Microsoft.Xna.Framework;
using NeuroSDKCsharp.Actions;
using NeuroSDKCsharp.Json;
using NeuroSDKCsharp.Websocket;
using NeuroStardewValley.Debug;
using NeuroStardewValley.Source.ContextStrings;
using NeuroStardewValley.Source.RegisterActions;
using NeuroStardewValley.Source.Utilities;
using StardewBotFramework.Source.Modules.Pathfinding.Base;
using StardewValley;
using StardewValley.GameData.Shops;
using StardewValley.Locations;
using StardewValley.Menus;

namespace NeuroStardewValley.Source.Actions;

public static class ShopKeeperActions
{
	#region ItemAndKeepersHelpers
	
	// uses tile actions from https://stardewvalleywiki.com/Modding:Maps#Action
	// "Shop" is used for festival shops, OpenShop has additional parameters, WizardBook is buying buildings IDK if it's that necessary
	private static readonly List<string> ValidShopActions = new() { "Buy", "Blacksmith", "Saloon", "AdventureShop",
		"Carpenter", "AnimalShop","BuyBackPack","ClubShop","JojaShop","OpenShop","Shop","WizardBook" };
	
	// shop id and shop owner area. These values are from the game as I would rather not make a transpiler for TryOpenShopMenu
	// apparently modded shops use custom field for theirs
	private static readonly Dictionary<string, Rectangle> ShopOwnerArea = new()
	{
		{ "Saloon", new Rectangle(9, 17, 10, 2) },
	};
	/// <summary>
	/// Check if the shopkeeper is valid from rules hardcoded in the game, if there are no shops will return false.
	/// else if the shopkeeper is not implemented it will check if they are in a tile that is a neighbour of the shop action.
	/// </summary>
	private static bool ValidShopKeeper(NPC npc, Dictionary<Point,string?> actions, out Point shopPoint)
	{
		shopPoint = new();

		var kvp = actions.Where(kvp => kvp.Value is not null && ValidShopActions.Contains(ArgUtility.SplitBySpace(kvp.Value)[0])).ToList();
		if (!kvp.Any()) return false;
		
		var point = kvp[0].Key;
		Logger.Info($"point: {point}");
		shopPoint = point;
		switch (npc.Name)
		{
			case "Robin":
				// if robin is close to the tile with the action "Carpenter". We don't check if the character is lower as it will walk to shop.
				return Vector2.Distance(npc.Tile, point.ToVector2()) <= 3f;
			case "Clint":
				return npc.Tile == new Vector2(point.X, point.Y - 1);
			case "Marnie":
				return npc.Tile == new Vector2(point.X, point.Y - 1) || npc.Tile == new Vector2(point.X - 1, point.Y - 1) || Main.Bot._farmer.stats.Get("Book_AnimalCatalogue") != 0;
			case "Gus":
				// move two down to account for action tile
				shopPoint = new(npc.TilePoint.X,npc.TilePoint.Y + 2);
				return ShopOwnerArea["Saloon"].Contains(npc.Tile);
			default:
				return Graph.IsInNeighbours(npc.TilePoint, point, out _, 4);
		}
	}
	
	
	private static List<ShopItemData> GetShopItems()
	{
		var shops = DataLoader.Shops(Game1.content);
		List<ShopItemData> items = new List<ShopItemData>();
		List<ShopData> shopData = new List<ShopData>();
		if (Main.Bot._currentLocation is Forest && Main.Bot._farmer.achievements.Count > 0)
		{
			items.AddRange(shops["HatMouse"].Items);
		}
		
		foreach (var npc in Main.Bot._currentLocation.characters)
		{
			if (!ValidShopKeeper(npc,TileContext.GetActionAndTile(),out _)) continue;
			// TODO: this leads to items from shops different from the one that will actually be opened. Fix this
			// data.Name == npc.Name should maybe be removed at some point as it stops shops where name is Any or AnyOrNone need to do upper first
			foreach (var kvp in shops.Where(kvp => kvp.Value.Owners.Any(data => data.IsValid(npc.Name) && data.Name == npc.Name)))
			{
				Logger.Info($"store data: {kvp.Key}  {kvp.Value.Items.Count}");
				bool query = true;
				foreach (var ownerData in kvp.Value.Owners)
				{
					if (!GameStateQuery.CheckConditions(ownerData.Condition)) query = false;
				}
				if (!query) break;
				if (shopData.Contains(kvp.Value)) continue;
				shopData.Add(kvp.Value);
	
				items.AddRange(kvp.Value.Items);
			}
		}
	
		return items;
	}
	
	private static List<Item> GetItems()
	{
		List<Item> items = new List<Item>();
		var shopItems = GetShopItems();
		items.AddRange(CreateItemsFromShopData(shopItems));
	
		return items;
	}
	private static List<Item> CreateItemsFromShopData(List<ShopItemData> shopItems)
	{
		List<Item> items = new();
		foreach (var itemData in shopItems)
		{
			if (!GameStateQuery.CheckConditions(itemData.Condition)) continue;
			if (itemData.ItemId is null || !ItemRegistry.Exists(itemData.ItemId)) continue;
				
			Item item = ItemRegistry.Create(itemData.ItemId);
			if (items.Any(i => i.DisplayName == item.DisplayName)) continue;
			Logger.Info($"item data price: {itemData.ObjectDisplayName}  {itemData.Price}");
			if (itemData.Price > Main.Bot._farmer.Money) continue;
				
			if (itemData.TradeItemId is not null && !Main.Bot.Inventory.Inventory.Any(i
				    => i is not null && i.ItemId == ItemRegistry.Create(itemData.TradeItemId).ItemId))
			{
				continue;
			}
			items.Add(item);
		}
	
		return items;
	}
	
	#endregion
	
	#region ActionHelpers
	
	private static ExecutionResult? HandleItemValidation(string selectedItemName, int itemAmount, ref List<Item> items, ref List<int> amount)
	{
		if (GetItems().All(i => i.DisplayName != selectedItemName))
		{
			return ExecutionResult.Failure($"The item you provided is not valid.");
		}
			
		Item item = GetItems().Where(i => i.DisplayName == selectedItemName).ToList()[0];
		ShopItemData shopItem = GetShopItems().Where(i => i.ItemId == item.QualifiedItemId).ToList()[0];
			
		if (itemAmount < 1) return ExecutionResult.Failure($"You must provide a positive amount of items.");
			
		if (shopItem.Price * itemAmount > Main.Bot.PlayerInformation.Money) return ExecutionResult.Failure($"You do not have enough money to buy so many {item.DisplayName}");
		// TODO: maybe just make this apart of execute so Neuro doesn't have to worry about it
		if (item.maximumStackSize() < itemAmount) 
			return ExecutionResult.Failure($"You can only hold {item.maximumStackSize()} {item.DisplayName} at a time," +
			                               $" if you want to buy multiple you will have to call this action multiple times.");
		
		items.Add(item);
		amount.Add(itemAmount);
		return null;
	}
	
	private static async Task ItemExecution(List<Item> items, List<int> amounts)
	{
		if (Game1.activeClickableMenu is not ShopMenu shopMenu) return;
		Main.Bot.Shop.OpenShop(shopMenu);
			
		for (int i = 0; i < items.Count; i++)
		{
			await Main.Bot.Shop.BuyItem(items[i],amounts[i]);
			await Util.WaitForSeconds(0.1);
		}
	
		await Util.WaitForSeconds(1);
		Main.Bot.Shop.RemoveMenu();
	}
	
	#endregion
	
	public class InteractWithShopkeeper : NeuroAction<Point>
	{
		public override string Name => "interact_with_shopkeeper";
		protected override string Description => "Interact with a shopkeeper in this location.";
		protected override JsonSchema Schema => GetSchema();
	
		private List<Item> _items = new();
		private List<int> _amounts = new();
		protected override ExecutionResult Validate(ActionData actionData, out Point resultData)
		{
			string? shopKeeper = actionData.Data?.Value<string>("shopkeeper");
			string? itemName = "";
			int? itemAmount = -1;
			if (!Main.Config.SeparateBuyAndShopkeeperActions)
			{
				itemName = actionData.Data?.Value<string>("item");
				itemAmount = actionData.Data?.Value<int>("amount");
			}
			
			resultData = new();
			if (shopKeeper is null || itemName is null || itemAmount is null) return ExecutionResult.Failure($"You cannot provide a null value.");
	
			var shopKeepers = ShopkeepersToPoint();
			if (!shopKeepers.TryGetValue(shopKeeper, out var keeperPoint)) return ExecutionResult.Failure($"The shopkeeper you provided is not valid.");
	
			if (!Main.Config.SeparateBuyAndShopkeeperActions)
			{
				var validation = HandleItemValidation(itemName, itemAmount.Value,ref _items, ref _amounts);
				if (validation is not null) return validation;
			}
			
			resultData = keeperPoint;
			return ExecutionResult.Success(_items.Any() ? $"Buying {itemAmount} {_items[0].DisplayName} from {shopKeeper}."
				: $"Walking over to {shopKeeper}.");
		}
	
		// the first is for building and blacksmith shop supplies is from animal shop. 
		private readonly List<string> _questionShopStrings = new() { "shop", "supplies", };
		protected override async void Execute(Point resultData)
		{
			try
			{
				await Main.Bot.Pathfinding.Goto(new Goal.GetToTile(resultData.X, resultData.Y));
				await TaskDispatcher.SwitchToMainThread();
				
				Main.Bot.Shop.OpenShopUi(resultData.X,resultData.Y);
				
				await Util.WaitForSeconds(1,false);
				// this handles selecting the dialogue option to open the shop menu if it exists
				if (Game1.activeClickableMenu is DialogueBox box)
				{
					while (box.transitioning)
					{
					}
	
					Logger.Info($"box dialogues: {box.dialogues.Count}");
					for (var i = 0; i < box.dialogues.Count; i++)
					{
						while (box.transitioning)
						{
						}
						
						Logger.Info($"pre wait");
						await Util.WaitForSeconds(1, false);
						
						if (!box.isQuestion)
						{
							box.receiveLeftClick(0,0);
						}
						else
						{
							for (var r = 0; r < box.responses.Length; r++)
							{
								if (!_questionShopStrings.Contains(box.responses[r].responseKey.ToLower())) continue;
								box.selectedResponse = r;
								box.receiveLeftClick(box.responseCC[r].bounds.X + 5,box.responseCC[r].bounds.Y + 5);	
							}	
						}
					}
				}
				
				await Util.WaitForSeconds(2);
				if (Main.Config.SeparateBuyAndShopkeeperActions)
				{
					Logger.Info($"creating buy from store action");
					ActionWindow.Create(Main.GameInstance).AddAction(new BuyFromStore()).Register();
					return;
				}
				
				await ItemExecution(_items, _amounts);
			}
			catch (Exception e)
			{
				Logger.Error($"{e}");
			}
		}

		#region ExtraMethods
		
		private static JsonSchema GetSchema()
		{
			JsonSchema schema = new()
			{
				Type = JsonSchemaType.Object,
				Required = new List<string> { "shopkeeper" },
				Properties = new Dictionary<string, JsonSchema>
				{
					["shopkeeper"] = QJS.Enum(ShopkeepersToPoint().Keys.ToList()),
				}
			};
	
			if (Main.Config.SeparateBuyAndShopkeeperActions) return schema;
	
			schema.Required.AddRange(new List<string> {"item", "amount"});
			schema.Properties.Add("item", QJS.Enum(GetItems().Select(item => item.DisplayName)));
			schema.Properties.Add("amount", QJS.Type(JsonSchemaType.Integer));
	
			return schema;
		}
	
		// TODO: does not check if the shop is currently active. Kinda does now
		private static Dictionary<string, Point> ShopkeepersToPoint()
		{
			var actions = TileContext.GetActionAndTile();
			var shops = DataLoader.Shops(Game1.content);
			var names = new Dictionary<string, Point>();
			// Hard coded as there is no other way :(
			if (Main.Bot._currentLocation is Forest && Main.Bot._farmer.achievements.Count > 0)
			{
				Logger.Warning($"adding hat mouse");
				names.Add($"Hat Mouse",new Point(34,95));
			}
			foreach (var npc in Main.Bot._currentLocation.characters)
			{
				if (!ValidShopKeeper(npc,actions,out var shopPoint)) continue;
				foreach (var _ in shops.Where(kvp => kvp.Value.Owners.Any(data => data.IsValid(npc.Name))))
				{
					names.TryAdd(npc.displayName, shopPoint);
				}
			}
	
			Logger.Info($"names count: {names.Count}");
			return names;
		}
		#endregion
	}
	
	private class BuyFromStore : NeuroAction<KeyValuePair<List<Item>,List<int>>>
	{
		public override string Name => "buy_item_store";
		protected override string Description => "Buy an item from this shop.";
		protected override JsonSchema Schema => new()
		{
			Type = JsonSchemaType.Object,
			Required = new List<string> { "item", "amount" },
			Properties = new Dictionary<string, JsonSchema>
			{
				["item"] = QJS.Enum(GetItems().Select(item => item.DisplayName)),
				["amount"] = QJS.Type(JsonSchemaType.Integer)
			}
		};
		
		protected override ExecutionResult Validate(ActionData actionData, out KeyValuePair<List<Item>,List<int>> resultData)
		{
			string? itemName = actionData.Data?.Value<string>("item");
			int? itemAmount = actionData.Data?.Value<int>("amount");
	
			resultData = new();
			if (itemName is null || itemAmount is null) return ExecutionResult.Failure($"You cannot provide a null value.");
	
			List<Item> items = new();
			List<int> amounts = new();
			var validation = HandleItemValidation(itemName, itemAmount.Value, ref items, ref amounts);
			resultData = new(items, amounts);
			if (validation is not null) return validation;

			if (!resultData.Key.Any() || resultData.Key.Count != resultData.Value.Count)
			{
				return ExecutionResult.Failure(
					$"There was an issue with BuyFromStore resultData.Key.Any() check or amount of result data count:" +
					$" (These are {resultData.Key.Count}, {resultData.Value.Count} You should mention these if" +
					$" their values are different)");
			}
			
			return ExecutionResult.Success($"Buying {itemAmount} {resultData.Key[0].DisplayName}.");
		}
	
		protected override async void Execute(KeyValuePair<List<Item>,List<int>> resultData)
		{
			try
			{
				await ItemExecution(resultData.Key, resultData.Value);
			}
			catch (Exception e)
			{
				await TaskDispatcher.SwitchToMainThread();
				Logger.Error($"{e}");
				if (Game1.activeClickableMenu != null)
				{
					Game1.activeClickableMenu = null;
					return;
				}
	
				RegisterMainActions.RegisterPostAction();
			}
		}
	}
}
