using StardewValley;
using StardewValley.Menus;

namespace NeuroStardewValley.Source.Utilities;

public static class InventoryUtils
{
	public static void ClickFirstEmptySlot(List<ClickableComponent> clickableComponents, IClickableMenu menu)
	{
		for (int i = 0; i < Main.Bot.Inventory.Inventory.Count; i++)
		{
			if (Main.Bot.Inventory.Inventory[i] is not null) continue;

			ClickableComponent cc = clickableComponents[i];
			menu.receiveLeftClick(cc.bounds.X,cc.bounds.Y);
			break;
		}
	}

	/// <summary>
	/// Add item to the inventory, accounting for if and item of that type already exists in the inventory.
	/// </summary>
	public static void AddToInventory(Item item, List<ClickableComponent> clickableComponents, IClickableMenu menu)
	{
		if (!Main.Bot.Inventory.Inventory.Any(i => i is not null && i.ItemId == item.ItemId))
		{
			ClickFirstEmptySlot(clickableComponents,menu);
			return;
		}
		
		for (int i = 0; i < Main.Bot.Inventory.Inventory.Count; i++)
		{
			Item? inventoryItem = Main.Bot.Inventory.Inventory[i];
			if (inventoryItem is null || inventoryItem.ItemId != item.ItemId) continue;

			ClickableComponent cc = clickableComponents[i];
			if (inventoryItem.Stack < inventoryItem.maximumStackSize())
			{
				menu.receiveLeftClick(cc.bounds.X,cc.bounds.Y);
			}

			if (item.Stack > 0)
			{
				ClickFirstEmptySlot(clickableComponents,menu);
			}

			break;
		}
	}

	public static bool CanFitAmount(List<Item> items)
	{
		foreach (var item in items)
		{
			foreach (var i in Main.Bot.Inventory.Inventory)
			{
				if (i is null) continue;
				if (item.ItemId != i.ItemId) continue;

				if ((i.Stack + item.Stack) > i.maximumStackSize())
				{
					return false;
				}
			}
		}

		return CanFitAmount(items.Count);
	}

	public static bool CanFitAmount(int count)
	{
		if (Main.Bot.Inventory.Inventory.Any(item => item is null))
		{
			return count > Main.Bot.Inventory.Inventory.Count(item => item is not null) - Main.Bot.Inventory.Inventory.Count;
		}
		
		return count > Main.Bot.Inventory.MaxInventory - Main.Bot.Inventory.Inventory.Count;
	}
}