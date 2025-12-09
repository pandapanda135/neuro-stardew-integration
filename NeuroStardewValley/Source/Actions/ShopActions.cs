using NeuroSDKCsharp.Actions;
using NeuroSDKCsharp.Json;
using NeuroSDKCsharp.Websocket;
using NeuroStardewValley.Source.RegisterActions;
using NeuroStardewValley.Source.Utilities;
using StardewValley;

namespace NeuroStardewValley.Source.Actions;

public static class ShopActions
{
	public class BuyItem : NeuroAction<KeyValuePair<ISalable,int>>
	{
		public override string Name => "buy_item";
		protected override string Description => "Buy an item from this shop";
		protected override JsonSchema Schema => new()
		{
			Type = JsonSchemaType.Object,
			Required = new List<string> { "item_index" },
			Properties = new Dictionary<string, JsonSchema>
			{
				["item_index"] = QJS.Enum(Enumerable.Range(0,BotHandler.Bot.Shop.ListAllItems().Count)), // get shop menu items
				["amount"] = QJS.Type(JsonSchemaType.Integer)
			}
		};
		protected override ExecutionResult Validate(ActionData actionData, out KeyValuePair<ISalable,int> resultData)
		{
			int? itemIndex = actionData.Data?.Value<int>("item_index");
			int? itemAmount = actionData.Data?.Value<int>("amount");

			resultData = new();
			if (itemIndex is null || itemAmount is null)
			{
				return ExecutionResult.Failure($"A value you provided was null.");
			}

			int index = (int)itemIndex;
			int amount = (int)itemAmount;

			if (!Enumerable.Range(0, BotHandler.Bot.Shop.ListAllItems().Count).Contains(index))
			{
				return ExecutionResult.Failure($"{index} is not a valid index.");
			}

			ISalable sellItem = BotHandler.Bot.Shop.ListAllItems()[index];
			BotHandler.Bot.Shop.ForSaleStats(out List<ISalable> _, out var currency);
			switch (currency)
			{
				case 0:
					if (sellItem.salePrice() * amount > Game1.player.Money)
					{
						return ExecutionResult.Failure($"You cannot buy this item or the amount of this item, as you do not have enough money for this.");
					}
					break;
				case 1: // this is star gems idk how to get this.
					if (true)
					{
						return ExecutionResult.Failure(
							$"You cannot buy this item or the amount of this item, as you do not have enough money for this.");
					}
				case 2:
				case 3:
					if (sellItem.salePrice() * amount > Game1.player.QiGems)
					{
						return ExecutionResult.Failure($"You cannot buy this item or the amount of this item, as you do not have enough money for this.");
					}
					break;
			}

			if (!sellItem.CanBuyItem(Game1.player))
			{
				return ExecutionResult.Failure($"You cannot buy this item, this is most likely because you do not have either the items or money necessary.");
			}

			if (amount == 0) amount = 1;
			resultData = new KeyValuePair<ISalable, int>(sellItem, amount);
			return ExecutionResult.Success();
		}

		protected override void Execute(KeyValuePair<ISalable,int> resultData)
		{
			int index = BotHandler.Bot.Shop.ListAllItems().IndexOf(resultData.Key);
			BotHandler.Bot.Shop.BuyItem(index,resultData.Value);
			RegisterStoreActions.RegisterDefaultShop();
		}
	}

	public class SellBackItem : NeuroAction<KeyValuePair<int, int>> // index of item and amount
	{
		public override string Name => "sell_back_item";
		protected override string Description => "Sell back an item you have bought from here, this index should be from an" +
		                                         " index in your inventory";
		protected override JsonSchema Schema => new()
		{
			Type = JsonSchemaType.Object,
			Required = new List<string> { "item_index" },
			Properties = new Dictionary<string, JsonSchema>
			{
				["item_index"] = QJS.Enum(BotHandler.Bot.Shop.Menu?.inventory.actualInventory.Where(item => item is not null && BotHandler.Bot.Shop.Menu.inventory.highlightMethod(item)).Select(item => BotHandler.Bot.Shop.Menu.inventory.actualInventory.IndexOf(item)) ?? Array.Empty<int>()), // get shop menu items
				["amount"] = QJS.Type(JsonSchemaType.Integer)
			}
		};
		protected override ExecutionResult Validate(ActionData actionData, out KeyValuePair<int, int> resultData)
		{
			int? itemIndex = actionData.Data?.Value<int>("item_index");
			int? itemAmount = actionData.Data?.Value<int>("amount");
			
			resultData = new();
			if (BotHandler.Bot.Shop.Menu is null) return ExecutionResult.Failure(string.Format(ResultStrings.ModVarFailure,"BotHandler.Bot.Shop.Menu"));
			if (itemIndex is null || itemAmount is null)
			{
				return ExecutionResult.Failure($"A value you provided was null.");
			}

			int index = (int)itemIndex;
			int amount = (int)itemAmount;

			if (BotHandler.Bot.Inventory.Inventory[index] is null)
			{
				return ExecutionResult.Failure($"{index} is not a valid index.");
			}

			Item sellItem = BotHandler.Bot.Inventory.Inventory[index];
			
			if (sellItem.salePrice() == -1)
			{
				return ExecutionResult.Failure($"You cannot sell this item.");
			}

			if (amount == 0) amount = 1;
			resultData = new KeyValuePair<int, int>(index, amount);
			return ExecutionResult.Success();
		}

		protected override void Execute(KeyValuePair<int, int> resultData)
		{
			BotHandler.Bot.Shop.SellBackItem(resultData.Key);
		}
	}

	public class CloseShop : NeuroAction
	{
		public override string Name => "close_shop";
		protected override string Description => "Close the currently open shop menu.";
		protected override JsonSchema Schema => new();
		protected override ExecutionResult Validate(ActionData actionData)
		{
			return ExecutionResult.Success($"Exiting shop");
		}

		protected override void Execute()
		{
			BotHandler.Bot.Shop.RemoveMenu();
		}
	}
}