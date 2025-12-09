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

	public static KeyValuePair<List<Item>, List<int>> ItemJsonToItem(List<ItemJson> jsons, IInventory inventory, List<Item> enumItems)
	{
		return ItemJsonToItem(jsons, inventory.ToList(), enumItems);
	}
	
	public static KeyValuePair<List<Item>, List<int>> ItemJsonToItem(List<ItemJson> jsons, List<Item?> inventory, List<Item> enumItems)
	{
		KeyValuePair<List<Item>,List<int>> result = new(new(),new());
		foreach (var item in inventory.OfType<Item>())
		{
			for (int i = 0; i < jsons.Count; i++)
			{
				var json = enumItems[i];
				Logger.Info($"json item: {json.DisplayName}   count: {json.Stack}   item: {item.DisplayName}   {item.Stack}");
				if (json.DisplayName != item.DisplayName || json.Stack > item.Stack) continue;

				result.Key.Add(item);
				result.Value.Add(jsons[i].Quantity);
			}
		}

		return result;
	}
	
	public static List<object> ItemEnum(IInventory inventory, Func<Item,bool>? itemCheck = null, bool addStack = false, bool addIndex = true)
		=> ItemEnum(inventory.ToList(), itemCheck,addStack, addIndex);
	
	public static List<object> ItemEnum(List<Item?> inventory, Func<Item,bool>? itemCheck = null, bool addStack = false, bool addIndex = true)
	{
		List<object> items = new();
		for (int i = 0; i < inventory.Count; i++)
		{
			Item? item = inventory[i];
			if (item is null || (itemCheck != null && !itemCheck(item))) continue;
			items.Add($"{(addIndex ? $"{i}: " : "")}{item.DisplayName}{(addStack ? $" amount: {item.Stack}" : string.Empty)}");
		}

		return items;
	}
	
	public static List<Item> EnumToItem(IInventory inventory,List<string> select, bool checkStack = false, bool checkIndex = true) =>
		EnumToItem(inventory.GetRange(0,inventory.Count).ToList(), select, checkStack, checkIndex);
	
	public static List<Item> EnumToItem(List<Item?> inventory,List<string> select, bool checkStack = false, bool checkIndex = true)
	{
		List<Item> items = new();
		foreach (var str in select)
		{
			for (int i = 0; i < inventory.Count; i++)
			{
				if (inventory[i] is null || str != $"{(checkIndex ? $"{i}: " : "")}{inventory[i]?.DisplayName}{(checkStack ? $" amount: {inventory[i]?.Stack}" : "")}") continue;

				items.Add(inventory[i]!);
				break;
			}
		}
			
		return items;
	}
}