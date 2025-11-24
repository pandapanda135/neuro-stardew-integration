using NeuroStardewValley.Debug;
using Newtonsoft.Json.Linq;
using StardewValley;
using StardewValley.Inventories;

namespace NeuroStardewValley.Source.Utilities;

public static class SchemaUtilities
{
	
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

	public static bool SchemaToItemJson(object itemIndex, out List<ItemJson> jsons, out Exception? exception)
	{
		List<ItemJson> items = new();
		exception = null;
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
			jsons = new();
			exception = e;
			return false;
		}

		jsons = items;
		return true;
	}
	
	public static List<object> ItemEnum(IInventory inventory, Func<Item,bool>? itemCheck = null, bool addStack = false)
	{
		return ItemEnum(inventory.ToList(), itemCheck,addStack);
	}
	
	public static List<object> ItemEnum(List<Item?> inventory, Func<Item,bool>? itemCheck = null, bool addStack = false)
	{
		List<object> items = new();
		for (int i = 0; i < inventory.Count; i++)
		{
			Item? item = inventory[i];
			if (item is null || (itemCheck != null && !itemCheck(item))) continue;
			items.Add($"{i}: {item.DisplayName}{(addStack ? $" amount: {item.Stack}" : string.Empty)}");
		}

		return items;
	}
	
	public static List<Item> EnumToItem(IInventory inventory,List<string> select) => EnumToItem(inventory.GetRange(0,inventory.Count).ToList(), select);
	
	public static List<Item> EnumToItem(List<Item?> inventory,List<string> select)
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
}