using Microsoft.Xna.Framework;
using NeuroSDKCsharp.Actions;
using NeuroSDKCsharp.Json;
using NeuroSDKCsharp.Websocket;
using NeuroStardewValley.Debug;
using NeuroStardewValley.Source.Actions.ObjectActions;
using NeuroStardewValley.Source.ContextStrings;
using NeuroStardewValley.Source.Utilities;
using StardewValley;
using StardewValley.Inventories;
using StardewValley.Menus;

namespace NeuroStardewValley.Source.Actions;

public static class ItemGrabActions
{
	private static ItemGrabMenu? Menu { get; set; }
	private class TakeItem : NeuroAction<Dictionary<Item,int>>
	{
		public override string Name => "take_items";
		protected override string Description => "Take Items from this menu.";

		protected override JsonSchema Schema => new()
		{
			Type = JsonSchemaType.Object,
			Required = new List<string> { "item_menu_name","inventory_index" },
			Properties = new Dictionary<string, JsonSchema>
			{
				["item_menu_name"] = QJS.Enum(GetMenuItems()),
				["inventory_index"] = QJS.Enum(Enumerable.Range(0, BotHandler.Bot.Inventory.MaxInventory))
			}
		};
		protected override ExecutionResult Validate(ActionData actionData, out Dictionary<Item,int>? resultData)
		{
			string? menuItems = actionData.Data?.Value<string>("item_menu_name");
			int? invIndex = actionData.Data?.Value<int>("inventory_index");
			
			resultData = null;
			
			if (Menu is null)
			{
				return ExecutionResult.Failure(string.Format(ResultStrings.ModVarFailure,$"Menu")); 
			}
			
			if (menuItems is null || invIndex is null)
			{
				return ExecutionResult.Failure($"You gave a null value, this is not allowed.");
			}

			resultData = new Dictionary<Item, int>();
			if (!GetMenuItems().Contains(menuItems))
			{
				return ExecutionResult.Failure($"{menuItems} is not a valid item in the menu.");
			}

			int itemIndex = GetMenuItems().IndexOf(menuItems);
			resultData.Add(Menu.ItemsToGrabMenu.actualInventory[itemIndex],(int)invIndex);
			
			return ExecutionResult.Success();
		}

		protected override void Execute(Dictionary<Item,int>? resultData)
		{
			if (resultData is null) return;
			foreach (var kvp in resultData)
			{
				BotHandler.Bot.ItemGrabMenu.TakeItem(kvp.Key);
			}
		}
	}
	
	public class SelectColour : NeuroAction<int>
	{
		private static readonly Dictionary<int, Color> Colours = new()
		{
			{0, Color.Black},
			{1, new Color(85, 85, 255) },
			{2, new Color(119, 191, 255)},
			{3, new Color(0, 170, 170)},
			{4, new Color(0, 234, 175)},
			{5, new Color(0, 170, 0)},
			{6, new Color(159, 236, 0)},
			{7, new Color(255, 234, 18)},
			{8, new Color(255, 167, 18)},
			{9, new Color(255, 105, 18)},
			{10, new Color(255, 0, 0)},
			{11, new Color(135, 0, 35)},
			{12, new Color(255, 173, 199)},
			{13, new Color(255, 117, 195)},
			{14, new Color(172, 0, 198)},
			{15, new Color(143, 0, 255)},
			{16, new Color(89, 11, 142)},
			{17, new Color(64, 64, 64)},
			{18, new Color(100, 100, 100)},
			{19, new Color(200, 200, 200)},
			{20, new Color(254, 254, 254)},
		};
		
		public override string Name => "select_colour";
		protected override string Description => "Select a colour to make this object.";
		protected override JsonSchema Schema => new()
		{
			Type = JsonSchemaType.Object,
			Required = new List<string> { "colour" },
			Properties = new Dictionary<string, JsonSchema>
			{
				["colour"] = QJS.Enum(GetColours().Select(colour => colour.ToString()))
			}
		};
		protected override ExecutionResult Validate(ActionData actionData, out int resultData)
		{
			string? colourString = actionData.Data?.Value<string>("colour");

			resultData = -1;
			if (string.IsNullOrEmpty(colourString))
			{
				return ExecutionResult.Failure($"colour must be a value in the enum.");
			}

			int index = GetColours().Select(colour => colour.ToString()).ToList().IndexOf(colourString);
			if (index == -1)
			{
				return ExecutionResult.Failure($"{colourString} is not a valid value, you should try something else.");
			}

			resultData = index;
			return ExecutionResult.Success($"You selected {colourString}");
		}

		protected override void Execute(int resultData)
		{
			Color color = GetColours()[resultData];
			BotHandler.Bot.ItemGrabMenu.ChangeColour(DiscreteColorPicker.getSelectionFromColor(color));
			ChestActions.RegisterChestActions();
		}

		private static List<Color> GetColours()
		{
			List<Color> colours = new();
			colours.AddRange(SelectColour.Colours.Values);
			return colours;
		}
	}

	private class AddItem : NeuroAction<Item>
	{
		public override string Name => "add_items";
		protected override string Description => "Add items to this menu";
		protected override JsonSchema Schema => new()
		{
			Type = JsonSchemaType.Object,
			Required = new List<string> { "item_menu_name" },
			Properties = new Dictionary<string, JsonSchema>
			{
				["item_menu_name"] = QJS.Enum(GetMenuItems(true)),
			}
		};
		protected override ExecutionResult Validate(ActionData actionData, out Item? resultData)
		{
			string? itemString = actionData.Data?.Value<string>("item_menu_name");

			resultData = null;
			
			if (Menu is null)
			{
				return ExecutionResult.Failure(string.Format(ResultStrings.ModVarFailure,$"Menu")); 
			}
			
			if (itemString is null)
			{
				return ExecutionResult.Failure($"You gave item_menu_name as null.");
			}

			if (!GetMenuItems(true).Contains(itemString))
			{
				return ExecutionResult.Failure($"{itemString} is not a valid item in the menu.");
			}
			resultData = Game1.player.Items[GetMenuItems(true).IndexOf(itemString)];

			return ExecutionResult.Success($"Added: {resultData.DisplayName} to the menu");
		}

		protected override void Execute(Item? resultData)
		{
			if (resultData is null) return;
			Logger.Info($"can add item: {BotHandler.Bot.ItemGrabMenu.AddItem(resultData)}");
		}
	}

	public static void RegisterActions(ItemGrabMenu menu)
	{
		Menu = menu;
		ActionWindow window = ActionWindow.Create(Main.GameInstance);

		window.AddAction(new TakeItem()).AddAction(new AddItem());
		if (menu.colorPickerToggleButton.visible && menu.CanHaveColorPicker())
		{
			window.AddAction(new SelectColour());
		}

		Inventory inv = new Inventory();
		inv.AddRange(Menu.ItemsToGrabMenu.actualInventory);
		window.SetForce(0, $"You are in a menu that has items in it.",
			$"Menu items: {InventoryContext.GetInventoryString(inv)}" +
			$".\n\n Inventory items: {InventoryContext.GetInventoryString(Game1.player.Items)}");
		
		window.Register();
	}

	private static List<string> GetMenuItems(bool inventory = false)
	{
		List<string> itemStrings = new();
		List<Item?> items = new();
		items.AddRange(inventory ? Game1.player.Items : Menu!.ItemsToGrabMenu.actualInventory);
		foreach (var item in items)
		{
			if (item is null) continue;
			
			itemStrings.Add(item.DisplayName);
		}

		return itemStrings;
	}
}