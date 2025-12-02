using NeuroSDKCsharp.Actions;
using NeuroSDKCsharp.Json;
using NeuroSDKCsharp.Websocket;
using NeuroStardewValley.Debug;
using NeuroStardewValley.Source.Utilities;
using StardewValley;

namespace NeuroStardewValley.Source.Actions.Menus;

public class CraftingActions
{
	public class GoToCrafting : NeuroAction
	{
		public override string Name => "go_to_crafting";
		protected override string Description => "Move to the crafting page";
		protected override JsonSchema Schema => new();
		protected override ExecutionResult Validate(ActionData actionData)
		{
			return ExecutionResult.Success();
		}

		protected override void Execute()
		{ 
			BotHandler.Bot.CraftingMenu.SetPageUI();
		}
	}
	private class CraftItem : NeuroAction<KeyValuePair<CraftingRecipe,int>>
	{
		public override string Name => "craft_item";
		protected override string Description => "Craft an item to be added to your inventory, it will be placed in the first empty slot.";
		protected override JsonSchema Schema => new()
		{
			Type = JsonSchemaType.Object,
			Required = new List<string> { "recipe","amount" },
			Properties = new Dictionary<string, JsonSchema>
			{
				["recipe"] = QJS.Enum(GetSchema()),
				["amount"] = QJS.Type(JsonSchemaType.Integer)
			}
		};
		protected override ExecutionResult Validate(ActionData actionData, out KeyValuePair<CraftingRecipe, int> resultData)
		{
			string? recipeString = actionData.Data?.Value<string>("recipe");
			int? amount = actionData.Data?.Value<int>("amount");

			resultData = new();
			if (recipeString is null || amount is null)
			{
				return ExecutionResult.Failure($"A value you provided was null.");
			}

			if (amount < 1)
			{
				return ExecutionResult.Failure($"You need to provide at least a value of one to amount.");
			}
			
			CraftingRecipe? recipe = null;
			var craftingRecipes = BotHandler.Bot.CraftingMenu.GetAllItems();
			for (int i = 0; i < craftingRecipes.Count; i++)
			{
				var recipes = craftingRecipes[i].Values.Where(crafting => crafting.DisplayName == recipeString).ToList();
				if (recipes.Any())
				{
					recipe = recipes[0];
				}
			}
			if (recipe is null)
			{
				return ExecutionResult.Failure($"The recipe you selected does not exist");
			}

			int lowestMaxAmount = -1;
			foreach (var kvp in recipe.recipeList)
			{
				Item item = ItemRegistry.Create(kvp.Key, kvp.Value);
				List<Item> items = BotHandler.Bot.Inventory.Inventory.Where(i => i is not null && i.Name == item.Name).ToList();
				if (items.Count < 1) return ExecutionResult.Failure($"You do not have the {item.DisplayName} necessary to create this.");
				
				int index = BotHandler.Bot.Inventory.Inventory.IndexOf(items[0]);
				int createAmount = BotHandler.Bot.Inventory.Inventory[index].Stack / item.Stack;
				if (lowestMaxAmount < createAmount) lowestMaxAmount = createAmount;
			}

			if (lowestMaxAmount < amount)
			{
				return ExecutionResult.Failure($"You do not have enough items to make {amount} {recipe.DisplayName}s.");
			}
			
			resultData = new(recipe, (int)amount);
			return ExecutionResult.Success($"You have crafted {amount} {recipe.DisplayName}s");
		}

		protected override void Execute(KeyValuePair<CraftingRecipe, int> resultData)
		{
			for (int i = 0; i < BotHandler.Bot.CraftingMenu.GetAllItems().Count; i++) // change page
			{
				if (!BotHandler.Bot.CraftingMenu.GetAllItems()[i].ContainsValue(resultData.Key)) continue;
				
				if (i == BotHandler.Bot.CraftingMenu.CurrentPage) break;

				if (i > BotHandler.Bot.CraftingMenu.CurrentPage)
				{
					for (int j = BotHandler.Bot.CraftingMenu.CurrentPage; j < i; j++)
					{
						BotHandler.Bot.CraftingMenu.ChangePage(false);
					}
				}
				else
				{
					for (int j = BotHandler.Bot.CraftingMenu.CurrentPage; j > i; j--)
					{
						BotHandler.Bot.CraftingMenu.ChangePage(true);
					}
				}
			}
			
			BotHandler.Bot.CraftingMenu.CraftItem(resultData.Key, resultData.Value);
			InventoryUtils.ClickFirstEmptySlot(BotHandler.Bot.CraftingMenu.Menu.inventory.inventory,BotHandler.Bot.CraftingMenu.Menu);
			RegisterActions();
		}

		private static List<string> GetSchema()
		{
			var recipes = BotHandler.Bot.CraftingMenu.GetAllItems();

			List<string> itemStrings = new();
			foreach (var dict in recipes)
			{
				Logger.Info($"dict: {dict.Count}");
				List<string> str = dict.Where(pair => pair.Value.doesFarmerHaveIngredientsInInventory())
					.Select(pair => pair.Value.DisplayName).ToList(); 
				itemStrings.AddRange(str);
			}

			return itemStrings;
		}
	}
	private class ExitMenu : NeuroAction
	{
		public override string Name => "exit_menu";
		protected override string Description => "Exit the crafting menu and start playing the game again.";
		protected override JsonSchema Schema => new();
		protected override ExecutionResult Validate(ActionData actionData)
		{
			// if is null
			try
			{
				BotHandler.Bot.CraftingMenu.Menu.readyToClose();
			}
			catch (Exception e)
			{
				Logger.Error($"Issue with crafting menu: {e}");
				return ExecutionResult.Failure(string.Format(ResultStrings.ModVarFailure,"CraftingMenu.Menu"));
			}
			
			return ExecutionResult.Success($"Exiting crafting menu.");
		}

		protected override void Execute()
		{
			BotHandler.Bot.CraftingMenu.RemoveMenu();
		}
	}

	public static void RegisterActions()
	{
		ActionWindow window = ActionWindow.Create(Main.GameInstance);
		window.AddAction(new ExitMenu());
		string state = $"There are no items available to craft.";
		if (CanCraftContext().Length > 0)
		{
			state = $"These are the items available to craft: {CanCraftContext()}";
			window.AddAction(new CraftItem());
		}
		
		window.SetForce(0, "You are in the crafting page.", state);
		window.Register();
	}

	private static string CanCraftContext()
	{
		var recipes = BotHandler.Bot.CraftingMenu.GetAllItems();

		string itemStrings = "";
		foreach (var dict in recipes)
		{
			foreach (var kvp in dict.Where(pair => pair.Value.doesFarmerHaveIngredientsInInventory()))
			{
				if (!BotHandler.Bot.CraftingMenu.Menu.cooking && kvp.Value.isCookingRecipe) continue;
				itemStrings += $"\n{kvp.Value.DisplayName}, Ingredients:";
				foreach (var recipe in kvp.Value.recipeList)
				{
					itemStrings += $" {ItemRegistry.Create(recipe.Key).DisplayName} amount: {recipe.Value}";
				}
			}
		}

		return itemStrings;
	}
}