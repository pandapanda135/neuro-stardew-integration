using Microsoft.Xna.Framework;
using NeuroSDKCsharp.Actions;
using NeuroSDKCsharp.Json;
using NeuroSDKCsharp.Websocket;
using NeuroStardewValley.Debug;
using NeuroStardewValley.Source.ContextStrings;
using NeuroStardewValley.Source.Utilities;
using StardewBotFramework.Source.Modules.Pathfinding.Base;
using StardewValley;
using StardewValley.GameData.Shops;
using StardewValley.Locations;

namespace NeuroStardewValley.Source.Actions;

public class InteractWithShopkeeper : NeuroAction<Point>
{
	public override string Name => "interact_with_shopkeeper";
	protected override string Description => "";
	protected override JsonSchema Schema => new()
	{
		Type = JsonSchemaType.Object,
		Required = new List<string> { "shopkeeper", "item", "amount" },
		Properties = new Dictionary<string, JsonSchema>
		{
			["shopkeeper"] = QJS.Enum(ShopkeepersToPoint().Keys.ToList()),
			["item"] = QJS.Enum(GetItems().Select(item => item.DisplayName)),
			["amount"] = QJS.Type(JsonSchemaType.Integer)
		}
	};

	private readonly List<Item> _items = new();
	private readonly List<int> _amounts = new();
	protected override ExecutionResult Validate(ActionData actionData, out Point resultData)
	{
		string? shopKeeper = actionData.Data?.Value<string>("shopkeeper");
		string? item = actionData.Data?.Value<string>("item");
		int? itemAmount = actionData.Data?.Value<int>("amount");

		resultData = new();
		if (shopKeeper is null || item is null || itemAmount is null) return ExecutionResult.Failure($"");

		var shopKeepers = ShopkeepersToPoint();
		if (!shopKeepers.TryGetValue(shopKeeper, out var keeperPoint)) return ExecutionResult.Failure($"");
		if (GetItems().All(i => i.DisplayName != item)) return ExecutionResult.Failure($"");
		
		resultData = keeperPoint;
		_items.Add(GetItems().Where(i => i.DisplayName == item).ToList()[0]);
		_amounts.Add(itemAmount.Value);
		return ExecutionResult.Success($"");
	}

	protected override async void Execute(Point resultData)
	{
		try
		{
			await Main.Bot.Pathfinding.Goto(new Goal.GetToTile(resultData.X, resultData.Y));
			await TaskDispatcher.SwitchToMainThread();
			
			Main.Bot.Shop.OpenShopUi(resultData.X,resultData.Y);
			await Utils.WaitForSeconds(1);
			await TaskDispatcher.SwitchToMainThread();

			for (int i = 0; i < _items.Count; i++)
			{
				await Main.Bot.Shop.BuyItem(_items[i],_amounts[i]);
				await Utils.WaitForSeconds(0.1);
				await TaskDispatcher.SwitchToMainThread();
			}

			await Utils.WaitForSeconds(1);
			await TaskDispatcher.SwitchToMainThread();

			Main.Bot.Shop.RemoveMenu();
		}
		catch (Exception e)
		{
			Logger.Error($"{e}");
		}
	}

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
			foreach (var kvp in shops.Where(kvp => kvp.Value.Owners.Any(data => data.IsValid(npc.Name))))
			{
				if (!names.TryAdd(npc.displayName, shopPoint)) continue;
			}
		}

		Logger.Info($"names count: {names.Count}");
		return names;
	}

	private static List<Item> GetItems()
	{
		var shops = DataLoader.Shops(Game1.content);
		List<Item> items = new List<Item>();
		List<ShopData> shopData = new List<ShopData>();
		if (Main.Bot._currentLocation is Forest && Main.Bot._farmer.achievements.Count > 0)
		{
			items.AddRange(CreateItemsFromShopData(shops["HatMouse"].Items));
		}
		
		foreach (var npc in Main.Bot._currentLocation.characters)
		{
			if (!ValidShopKeeper(npc,TileContext.GetActionAndTile(),out _)) continue;
			foreach (var kvp in shops.Where(kvp => kvp.Value.Owners.Any(data => data.IsValid(npc.Name))))
			{
				Logger.Info($"kvp: {kvp.Key}  {kvp.Value.Items.Count}");
				if (shopData.Contains(kvp.Value)) continue;
				shopData.Add(kvp.Value);

				items.AddRange(CreateItemsFromShopData(kvp.Value.Items));
			}
		}

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
			// Logger.Info($"item name: {item.DisplayName}");
			if (items.Any(i => i.DisplayName == item.DisplayName)) continue;
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
}