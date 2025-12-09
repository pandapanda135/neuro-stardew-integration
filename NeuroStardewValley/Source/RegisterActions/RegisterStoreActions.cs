using NeuroSDKCsharp.Actions;
using NeuroStardewValley.Debug;
using NeuroStardewValley.Source.Actions;
using NeuroStardewValley.Source.Actions.Menus;
using NeuroStardewValley.Source.Utilities;
using StardewValley;
using StardewValley.Menus;

namespace NeuroStardewValley.Source.RegisterActions;

public static class RegisterStoreActions
{
	public static void RegisterDefaultShop()
	{
		ActionWindow window = ActionWindow.Create(Main.GameInstance);

		window.AddAction(new ShopActions.CloseShop()).AddAction(new ShopActions.BuyItem());

		// in case this is somehow null
		try
		{
			BotHandler.Bot.Shop.ListAllItems();
		}
		catch (Exception e)
		{
			Logger.Error($"Issue with shop {e}");
			BotHandler.Bot.Shop.RemoveMenu();
			RegisterMainActions.RegisterPostAction();
			return;
		}

		string itemString = "These are the items in the shop and their sale prices. If an item contains an upgrade item," +
		                    " you will not be able to buy that item without the required item.";
		List<ISalable> items = BotHandler.Bot.Shop.ListAllItems();
		if (items.Count < 1)
		{
			Game1.activeClickableMenu = null;
			RegisterMainActions.RegisterPostAction();
			return;
		}

		itemString += "\n# Shop Items:";
		for (int i = 0; i < items.Count - 1; i++)
		{
			ISalable itemISalable = items[i];
			ItemStockInformation stockInformation = BotHandler.Bot.Shop.StockInformation[itemISalable];
			itemString += "\n## Item\n";
			itemString += $"- Name: {itemISalable.DisplayName}\n" +
			              $"- Description: {StringUtilities.FormatItemString(itemISalable.getDescription())}\n" +
			              $"- Cost: {stockInformation.Price}\n";
			if (ItemRegistry.Create(itemISalable.QualifiedItemId) is Tool && !string.IsNullOrEmpty(stockInformation.TradeItem))
			{
				Item upgradeItem = ItemRegistry.Create(stockInformation.TradeItem);
				itemString += $"### Upgrade Item Requirement:\n- Name: {upgradeItem.DisplayName}\n- Amount Needed: {stockInformation.TradeItemCount}";
			}
		}
		
		if (BotHandler.Bot.Shop.Menu.inventory.actualInventory.Any(item =>
			    item is not null && BotHandler.Bot.Shop.Menu.inventory.highlightMethod(item)))
		{
			window.AddAction(new ShopActions.SellBackItem());
		}

		List<Item> sellableItems = BotHandler.Bot.Shop.Menu.inventory.actualInventory.Where(item =>
			item is not null && BotHandler.Bot.Shop.Menu.inventory.highlightMethod(item)).ToList();
		if (sellableItems.Any()) itemString += "\n## Sellable Items:";
		
		foreach (var item in sellableItems)
		{
			itemString += "\n### Sellable Item:\n";
			itemString += $"- Name: {item.DisplayName}\n" +
			              $"- Sell price: {item.sellToStorePrice()}\n";
		}
		window.SetForce(0, "You are in a shop's menu, you can either buy or sell back items here", itemString);
		
		window.Register();
	}

	public static void RegisterCarpenterActions()
	{
		ActionWindow window = ActionWindow.Create(Main.GameInstance);

		window.AddAction(new CarpenterActions.BuildBluePrint());

		if (PlaceBuildingActions.SelectBuilding.GetBuildings(Game1.getFarm(), out _, CarpenterMenu.CarpentryAction.Upgrade).Any())
			window.AddAction(new CarpenterActions.UpgradeBlueprint());
		
		if (PlaceBuildingActions.SelectBuilding.GetBuildings(Game1.getFarm(), out _, CarpenterMenu.CarpentryAction.Demolish).Any())
			window.AddAction(new CarpenterActions.DestroyBuilding());
		
		// if (BotHandler.Bot.FarmBuilding.Building.CanBeReskinned())
		// {
		// 	BotHandler.Bot.FarmBuilding.SetSkinUi(new BuildingSkinMenu(BotHandler.Bot.FarmBuilding.Building, true));
		// 	window.AddAction(new CarpenterActions.ChangeBuildingSkin());	
		// }

		string state = "These are the possible buildings that you can either build, upgrade or demolish: ";
		foreach (var entry in BotHandler.Bot.FarmBuilding.CarpenterMenu.Blueprints)
		{
			state += $"\n#Building name: {entry.DisplayName}\n## Time to build: {entry.BuildDays} days\n## Cost to build: {entry.BuildCost} gold";
			if (entry.BuildMaterials is null) continue;
			state += "\n## Materials to build: ";
			foreach (var material in entry.BuildMaterials)
			{
				Item item = ItemRegistry.Create(material.Id);
				state += $"\n### {item.DisplayName} amount: {material.Amount}";
			}
		}
		window.SetForce(0, $"You are now in the carpenter menu", state,true);
		
		window.Register();
	}

	public static void RegisterBlacksmithActions()
	{
		ActionWindow window = ActionWindow.Create(Main.GameInstance);
		List<Item> items = new();
		foreach (var item in BotHandler.Bot.Inventory.Inventory)
		{
			if (!Utility.IsGeode(item))
			{
				continue;
			}
			
			items.Add(item);
		}
		if (items.Count > 0)
		{
			window.AddAction(new BlacksmithActions.OpenGeode());
		}

		window.AddAction(new BlacksmithActions.CloseMenu());
		
		window.SetForce(0, $"You are in the blacksmiths, you can select a geode to open here.",
			$"You should either select a geode to open or close the menu.");
		
		window.Register();
	}
}