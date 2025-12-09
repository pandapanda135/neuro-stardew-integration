using NeuroSDKCsharp.Actions;
using NeuroSDKCsharp.Json;
using NeuroSDKCsharp.Websocket;
using NeuroStardewValley.Debug;
using NeuroStardewValley.Source.Utilities;
using StardewValley;
using StardewValley.Menus;

namespace NeuroStardewValley.Source.Actions;

// item and amount
public class CreateItem : NeuroAction<KeyValuePair<CraftingRecipe,int>>
{
	public override string Name => "create_item";
	protected override string Description => "";
	protected override JsonSchema Schema => new()
	{
		Type = JsonSchemaType.Object,
		Required = new List<string> { "item", "amount" },
		Properties = new Dictionary<string, JsonSchema>
		{
			["item"] = QJS.Enum(GetSchema()),
			["amount"] = QJS.Type(JsonSchemaType.Integer)
		}
	};
	
	protected override ExecutionResult Validate(ActionData actionData, out KeyValuePair<CraftingRecipe, int> resultData)
	{
		string? itemName = actionData.Data?.Value<string>("item");
		int? amount = actionData.Data?.Value<int>("amount");

		resultData = new(new(""),int.MaxValue);
		if (itemName is null || amount is null)
		{
			return ExecutionResult.Failure($"You cannot provide a null value");
		}

		if (amount.Value < 1)
		{
			return ExecutionResult.Failure($"The amount you specified was too low.");
		}

		if (!GetSchema().Contains(itemName))
		{
			return ExecutionResult.Failure($"You provided an invalid item");
		}
		
		SetMenu();

		var allItems = BotHandler.Bot.CraftingMenu.GetAllItems();

		foreach (var list in allItems.Select(dict => dict.Where(kvp => kvp.Value.DisplayName == itemName).ToList()))
		{
			if (!list.Any()) continue;
			
			resultData = new(list[0].Value, amount.Value);
		}
		
		if (resultData.Value == int.MaxValue)
		{
			return ExecutionResult.Failure(string.Format(ResultStrings.ModVarFailure,"The CreateItem Schema"));	
		}
		
		CraftingRecipe? recipe = null;
		var craftingRecipes = BotHandler.Bot.CraftingMenu.GetAllItems();
		foreach (var dict in craftingRecipes)
		{
			var recipes = dict.Values.Where(crafting => crafting.DisplayName == itemName).ToList();
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
			Item it = ItemRegistry.Create(kvp.Key, kvp.Value);
			List<Item> items = BotHandler.Bot.Inventory.Inventory.Where(i => i is not null && i.Name == it.Name).ToList();
			if (items.Count < 1) return ExecutionResult.Failure($"You do not have the {it.DisplayName} necessary to create this.");
				
			int index = BotHandler.Bot.Inventory.Inventory.IndexOf(items[0]);
			int createAmount = BotHandler.Bot.Inventory.Inventory[index].Stack / it.Stack;
			if (lowestMaxAmount < createAmount) lowestMaxAmount = createAmount;
		}

		if (lowestMaxAmount < amount.Value)
		{
			return ExecutionResult.Failure($"You do not have the resources to create {amount.Value} {itemName}");
		}

		resultData = new(recipe,amount.Value);
		return ExecutionResult.Success();
	}

	protected override async void Execute(KeyValuePair<CraftingRecipe, int> resultData)
	{
		BotHandler.Bot.CraftingMenu.SetPageUI();
		await Util.WaitForSeconds(1);

		for (int i = 0; i < BotHandler.Bot.CraftingMenu.GetAllItems().Count; i++) // change page
		{
			await Util.WaitForSeconds(0.5);
			// we do this as the object in resultData and GetAllItems are different
			if (!BotHandler.Bot.CraftingMenu.GetAllItems()[i].Select(kvp => kvp.Value.createItem().ItemId).Contains(resultData.Key.createItem().ItemId)) continue;
			
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
		
		Logger.Info($"result data: {resultData.Key.DisplayName}   {resultData.Value}");
		await Util.WaitForSeconds(3);
		BotHandler.Bot.CraftingMenu.CraftItem(resultData.Key, resultData.Value);
		// handle if there is already that same item in the inventory
		InventoryUtils.AddToInventory(BotHandler.Bot.CraftingMenu.Menu.heldItem,BotHandler.Bot.CraftingMenu.Menu.inventory.inventory,BotHandler.Bot.CraftingMenu.Menu);	

		BotHandler.Bot.CraftingMenu.RemoveMenu();
	}
	
	public static List<string> GetSchema()
	{
		SetMenu();
		var recipes = BotHandler.Bot.CraftingMenu.GetAllItems();
		
		List<string> itemStrings = new();
		foreach (var dict in recipes)
		{
			List<string> str = dict.Where(pair => pair.Value.doesFarmerHaveIngredientsInInventory())
				.Select(pair => pair.Value.DisplayName).ToList(); 
			itemStrings.AddRange(str);
		}

		SetMenu(true);
		return itemStrings;
	}
	
	private static void SetMenu(bool remove = false)
	{
		if (remove)
		{
			BotHandler.Bot.CraftingMenu.RemoveStoredMenu();
			return;
		}
		
		var gameMenu = new GameMenu(4,-1,false);
		BotHandler.Bot.CraftingMenu.SetUI((CraftingPage)gameMenu.GetCurrentPage());
	}
}