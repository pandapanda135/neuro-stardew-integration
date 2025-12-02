using NeuroSDKCsharp.Actions;
using NeuroSDKCsharp.Json;
using NeuroSDKCsharp.Websocket;
using NeuroStardewValley.Debug;
using NeuroStardewValley.Source.Actions.Menus;
using NeuroStardewValley.Source.ContextStrings;
using StardewValley;
using StardewValley.Inventories;
using StardewValley.Menus;
using StardewValley.Objects;
using StardewValley.Objects.Trinkets;
using Context = NeuroSDKCsharp.Messages.Outgoing.Context;
using Object = StardewValley.Object;

namespace NeuroStardewValley.Source.Actions;

 static class InventoryActions
{
    #region BaseUI

    public class OpenInventory : NeuroAction
    {
        public override string Name => "open_inventory";
        protected override string Description => "Open your inventory allowing the altering of items, this will also" +
                                                 " allow for you to craft items. Being in the inventory or crafting items stop time.";
        protected override JsonSchema Schema => new();
        protected override ExecutionResult Validate(ActionData actionData)
        {
            return ExecutionResult.Success();
        }
        protected override void Execute()
        {
            BotHandler.Bot.PlayerInformation.OpenInventory();
        }
    }
    private class ExitInventory : NeuroAction
    {
        public override string Name => "close_inventory";
        protected override string Description => "Close your inventory and go back to playing the game, this will make time start again and also send you the inventory.";
        protected override JsonSchema Schema => new();
        protected override ExecutionResult Validate(ActionData actionData)
        {
            
            return ExecutionResult.Success();
        }

        protected override void Execute()
        {
            string nameList = InventoryContext.GetInventoryString(BotHandler.Bot.Inventory.Inventory, true, true);
            Context.Send($"These are the items in your inventory as of when you last closed it: {nameList}");
            BotHandler.Bot.PlayerInformation.ExitMenu();
        }
    }

    #endregion

    #region ItemInteraction

    private class MoveItem : NeuroAction<Item>
    {
        private int _position;
        private static Inventory Inventory => BotHandler.Bot.Inventory.Inventory;
        public override string Name => "move_item";
        protected override string Description => $"Move an item in inventory to another slot, you have {BotHandler.Bot.Inventory.MaxInventory}" +
                                                 $" slots in your inventory. If there is already an item in the provided slot, the item will occupy the provided item's previous slot.";
        protected override JsonSchema Schema => new ()
        {
            Type = JsonSchemaType.Object,
            Required = new List<string> { "item", "position" },
            Properties = new Dictionary<string, JsonSchema>
        {
                ["item"] = QJS.Enum(Inventory.Where(item => item is not null)
                    .Select(item => $"{Inventory.IndexOf(item)}: {item.DisplayName}")),
                ["position"] = QJS.Type(JsonSchemaType.Integer)
            }
        };
        
        protected override ExecutionResult Validate(ActionData actionData, out Item? resultData)
        {
            string? movingItem = actionData.Data?.Value<string>("item");
            int? itemPosition = actionData.Data?.Value<int>("position");

            if (itemPosition is null || movingItem is null)
            {
                resultData = null;
                return ExecutionResult.Failure($"An argument you gave was null");
            }
            
            if (itemPosition > BotHandler.Bot.Inventory.MaxInventory || itemPosition < 0)
            {
                resultData = null;
                return ExecutionResult.Failure($"You have given a position that is larger or smaller than the size of your inventory");
            }

            Item? item = StringToItem(movingItem);
            if (item is null)
            {
                resultData = null;
                return ExecutionResult.Failure($"The item you selected to move does not exist.");
            }

            resultData = item;
            _position = (int)itemPosition;
            
            return ExecutionResult.Success();
        }

        protected override void Execute(Item? resultData)
        {
            if (resultData is null) return;
            BotHandler.Bot.Inventory.MoveItem(resultData, _position);
            RegisterInventoryActions();
        }
        
        private static Item? StringToItem(string str)
        {
            Item? item = null;
            for (int i = 0; i < Inventory.Count; i++)
            {
                if (Inventory[i] is null) continue;

                if (str == $"{i}: {Inventory[i].DisplayName}") item = Inventory[i];
            }

            return item;
        }
    }
    private class RemoveItem : NeuroAction<KeyValuePair<Item,int>>
    {
        private IEnumerable<string> Options => new[] { "bin", "drop" };
        private string _selectedOption = "";
        public override string Name => "remove_item";
        protected override string Description => "remove an item from your inventory, this can be done by either dropping" +
                                                 " it or putting it in the bin. If you specify 0 as the amount, the whole stack will be removed.";
        protected override JsonSchema Schema => new()
        {
            Type = JsonSchemaType.Object,
            Required = new List<string> { "item", "amount","option" },
            Properties = new Dictionary<string, JsonSchema>
            {
                ["item"] = QJS.Enum(BotHandler.Bot.Inventory.Inventory.Where(i => i is not null && i.canBeTrashed()).Select(i => BotHandler.Bot.Inventory.Inventory.IndexOf(i)).ToList()),
                ["amount"] = QJS.Type(JsonSchemaType.Integer),
                ["option"] = QJS.Enum(Options)
            }
        };
        protected override ExecutionResult Validate(ActionData actionData, out KeyValuePair<Item,int> resultData)
        {
            string? selectedIndex = actionData.Data?.Value<string>("item");
            int? selectedAmount = actionData.Data?.Value<int>("amount");
            string? selectedOption = actionData.Data?.Value<string>("option");

            resultData = new();
            if (selectedIndex is null || selectedAmount is null || selectedOption is null)
            {
                return ExecutionResult.Failure($"you provided a null value");
            }

            if (!Options.Contains(selectedOption))
            {
                return ExecutionResult.Failure($"You provided an invalid option.");
            }

            Item? item = null;
            for (int i = 0; i < BotHandler.Bot.Inventory.MaxInventory; i++)
            {
                if (BotHandler.Bot.Inventory.Inventory[i] is not null && i == int.Parse(selectedIndex))
                {
                    item = BotHandler.Bot.Inventory.Inventory[i];
                }
            }

            if (item is null)
            {
                return ExecutionResult.Failure($"The item you provided does not exist");
            }

            if (selectedAmount > item.Stack || selectedAmount < 0)
            {
                return ExecutionResult.Failure($"You have selected more or too little items then are available.");
            }
            
            resultData = new(item,(int)selectedAmount);
            _selectedOption = selectedOption;
            return ExecutionResult.Success($"You are binning {selectedAmount} {item.DisplayName}");
        }

        protected override void Execute(KeyValuePair<Item,int> resultData)
        {
            GameMenu? menu = Game1.activeClickableMenu as GameMenu;
            if (menu?.GetCurrentPage() is not InventoryPage page) return;
            
            BotHandler.Bot.Inventory.SetPage(page);
            int stack = resultData.Value == 0 ? resultData.Key.Stack : resultData.Value;
            
            BotHandler.Bot.Inventory.SetHeldItem(resultData.Key,stack);
            if (_selectedOption == "bin")
            {
                BotHandler.Bot.Inventory.Hover(BotHandler.Bot.Inventory.Page.trashCan,1);
                BotHandler.Bot.Inventory.ClickBin();
            }
            else
            {
                BotHandler.Bot.Inventory.ClickOutOfBounds();
            }
            RegisterInventoryActions();
        }
    }
    private class InteractWithTrinkets : NeuroAction<Dictionary<string,string>>
    {
        private string[] TrinketAction()
        {
            return new[] { "Equip", "Unequip" };
        }

        private IEnumerable<string> TrinketSlots()
        {
            Logger.Info($"amount: {Farmer.MaximumTrinkets}    {Game1.player.stats.Get("trinketSlots")}     {Game1.player.trinketItems.Count}");
            string[] trinketSlots = new string[Game1.player.stats.Get("trinketSlots")];
            for (int i = 0; i < Game1.player.stats.Get("trinketSlots"); i++)
            {
                Logger.Info($"i: {i}");
                trinketSlots = (string[])trinketSlots.Append(i.ToString());
            }

            return trinketSlots;
        } 
        
        public override string Name => "interact_with_trinkets";
        protected override string Description =>
            "This will allow you to interact with trinkets, you can either remove or equip trinkets. If you do not specify an inventory slot, it will be placed in the first empty slot.";
        protected override JsonSchema Schema => new ()
        {
            Type = JsonSchemaType.Object,
            Required = new List<string> { "slot", "action" },
            Properties = new Dictionary<string, JsonSchema>
            {
                ["slot"] = QJS.Enum(TrinketSlots()), // explain what these are as I don't even know
                ["action"] = QJS.Enum(TrinketAction()),
                ["inventory_slot"] = QJS.Enum(Enumerable.Range(0,BotHandler.Bot.Inventory.MaxInventory - 1))
            }
        };
        protected override ExecutionResult Validate(ActionData actionData, out Dictionary<string,string>? resultData)
        {
            string? slot = actionData.Data?.Value<string>("slot");
            string? action = actionData.Data?.Value<string>("action");
            int? inventoryInt = actionData.Data?.Value<int>("inventory_slot");

            resultData = new();
            if (slot is null || action is null || inventoryInt is null)
            {
                resultData = null;
                return ExecutionResult.Failure("Can not be null");
            }
            string inventory = inventoryInt.ToString()!;

            if (!TrinketSlots().Contains(slot))
            {
                resultData = null;
                return ExecutionResult.Failure($"{slot} is not a valid slot");
            }

            if (!TrinketAction().Contains(action))
            {
                resultData = null;
                return ExecutionResult.Failure($"{action} is not a valid action");
            }

            if (Enumerable.Range(0, BotHandler.Bot.Inventory.MaxInventory - 1).Contains(int.Parse(inventory)))
            {
                resultData = null;
                return ExecutionResult.Failure($"{inventory} is not a valid inventory slot");
            }

            IEnumerable<string> trinketSlots = TrinketSlots();
            int index = trinketSlots.ToList().IndexOf(slot);
            
            resultData.Add("TrinketSlot", index.ToString());
            resultData.Add("Action", action);
            resultData.Add("Inventory", inventory);
            return ExecutionResult.Success();
        }

        protected override void Execute(Dictionary<string,string>? resultData)
        {
            if (resultData is null) return;
            if (resultData["Action"] == "Equip")
            {
                Trinket? trinket = Game1.player.trinketItems[int.Parse(resultData["Inventory"])];
                BotHandler.Bot.Inventory.EquipTrinket(trinket,int.Parse(resultData["TrinketSlot"]));
            }
            else
            {
                Trinket? trinket = Game1.player.trinketItems[int.Parse(resultData["TrinketSlot"])];
                BotHandler.Bot.Inventory.RemoveTrinket(trinket);
            }
            RegisterInventoryActions();
        }
    }
    private class ChangeClothing : NeuroAction<Dictionary<string,string>>
    {
        private string[] Actions()
        {
            return new[] { "Equip", "Unequip" };
        }

        private string[] Slots() // I'm pretty sure they don't add any during the game
        {
            return new[] { "hat", "shirt", "pants", "top_ring", "bottom_ring", "boots" };
        }

        public override string Name => "change_equipped";
        protected override string Description => "This allows you to change equipped clothing and items, if you are " +
                                                 "equipping an item, you should make sure the inventory slot you specify is a valid item.";
        protected override JsonSchema Schema => new()
        {
            Type = JsonSchemaType.Object,
            Required = new List<string> { "slot", "action" },
            Properties = new Dictionary<string, JsonSchema>
            {
                ["slot"] = QJS.Enum(Slots()),
                ["action"] = QJS.Enum(Actions()),
                ["inventory_slot"] = QJS.Enum(Enumerable.Range(0, BotHandler.Bot.Inventory.MaxInventory))
            }
        };
        protected override ExecutionResult Validate(ActionData actionData, out Dictionary<string,string>? resultData)
        {
            string? slot = actionData.Data?.Value<string>("slot");
            string? action = actionData.Data?.Value<string>("action");
            string? inventory = actionData.Data?.Value<string>("inventory_slot");

            if (action is null || slot is null)
            {
                resultData = new();
                return ExecutionResult.Failure("You gave an invalid value to either slot or action.");
            }

            if (!Slots().Contains(slot))
            {
                resultData = new();
                return ExecutionResult.Failure("slot was not set correctly");
            }

            if (!Actions().Contains(action))
            {
                resultData = new();
                return ExecutionResult.Failure("action was not set correctly");
            }

            if (inventory is not null && action == "Equip" &&
                (!Enumerable.Range(0, BotHandler.Bot.Inventory.MaxInventory).Contains(int.Parse(inventory)) ||
                 BotHandler.Bot.Inventory.Inventory[int.Parse(inventory)] is not Clothing or Ring or Boots or Hat))
            {
                resultData = new();
                return ExecutionResult.Failure($"inventory slot was not set correctly, you can only give a slot that is between 0 and {BotHandler.Bot.Inventory.MaxInventory} and is a piece of clothing.");
            }
            
            resultData = new()
            {
                { "slot", slot },
                { "action", action }
            };
            if (inventory is not null)
            {
                resultData.Add("inventory_slot",inventory);
            }
            return ExecutionResult.Success();
        }

        protected override void Execute(Dictionary<string,string>? resultData)
        {
            if (resultData is null) return;
            switch (resultData["slot"])
            {
                case "hat":
                    if (resultData["action"] == "Unequip") BotHandler.Bot.Inventory.ChangeHat(null);
                    else BotHandler.Bot.Inventory.ChangeHat((Hat)Game1.player.Items[int.Parse(resultData["inventory_slot"])]);
                    break;
                case "shirt":
                    if (resultData["action"] == "Unequip") BotHandler.Bot.Inventory.ChangeClothing(true, null);
                    else BotHandler.Bot.Inventory.ChangeClothing(true, (Clothing)Game1.player.Items[int.Parse(resultData["inventory_slot"])]);
                    break;
                case "pants":
                    if (resultData["action"] == "Unequip") BotHandler.Bot.Inventory.ChangeClothing(false, null);
                    else BotHandler.Bot.Inventory.ChangeClothing(false, (Clothing)Game1.player.Items[int.Parse(resultData["inventory_slot"])]);
                    break;
                case "top_ring":
                    if (resultData["action"] == "Unequip") BotHandler.Bot.Inventory.ChangeRings(null,true);
                    else BotHandler.Bot.Inventory.ChangeRings((Ring)Game1.player.Items[int.Parse(resultData["inventory_slot"])], true);
                    break;
                case "bottom_ring":
                    if (resultData["action"] == "Unequip") BotHandler.Bot.Inventory.ChangeRings(null,false);
                    else BotHandler.Bot.Inventory.ChangeRings((Ring)Game1.player.Items[int.Parse(resultData["inventory_slot"])], false);
                    break;
                case "boots":
                    if (resultData["action"] == "Unequip") BotHandler.Bot.Inventory.ChangeBoots(null);
                    else BotHandler.Bot.Inventory.ChangeBoots((Boots)Game1.player.Items[int.Parse(resultData["inventory_slot"])]);
                    break;
            }
            RegisterInventoryActions();
        }
    }

    #endregion
    
    #region Attach

    public class AttachItem : NeuroAction<KeyValuePair<Item, Item>>
    {
        public override string Name => "attach_item";

        protected override string Description =>
            "Attach an item to another, a common use of this attaching bait on fishing rods.";
        protected override JsonSchema Schema => new()
        {
            Type = JsonSchemaType.Object,
            Required = new List<string> { "item_to_attach", "attached_item" },
            Properties = new Dictionary<string, JsonSchema>
            {
                ["item_to_attach"] = QJS.Enum(Enumerable.Range(0,BotHandler.Bot.Inventory.MaxInventory)), // item to attach
                ["attached_item"] = QJS.Enum(Enumerable.Range(0,BotHandler.Bot.Inventory.MaxInventory)) // item we attach to
            }
        };
        protected override ExecutionResult Validate(ActionData actionData, out KeyValuePair<Item, Item> resultData)
        {
            int? item = actionData.Data?.Value<int>("item_to_attach");
            int? attachToItem = actionData.Data?.Value<int>("attached_item");

            resultData = new();
            if (item is null || attachToItem is null)
            {
                return ExecutionResult.Failure($"You have not selected both items.");
            }
            
            if (!Enumerable.Range(0, BotHandler.Bot.Inventory.MaxInventory).Contains((int)item) ||
                !Enumerable.Range(0, BotHandler.Bot.Inventory.MaxInventory).Contains((int)attachToItem))
            {
                return ExecutionResult.Failure($"The index you provided was not a valid index");
            }

            Item toolItem = BotHandler.Bot.Inventory.Inventory[(int)item];
            Item attachItem = BotHandler.Bot.Inventory.Inventory[(int)attachToItem];
            if (toolItem is not Tool tool)
            {
                return ExecutionResult.Failure($"The index you provided does not point to an item that has an attachment slot.");
            }
            if (!tool.canThisBeAttached((Object)attachItem))
            {
                return ExecutionResult.Failure($"{attachItem.DisplayName} cannot be attached to {tool.DisplayName}");
            }

            if (tool.attachments.Count(obj => obj is not null) >= tool.AttachmentSlotsCount)
            {
                return ExecutionResult.Failure($"This item already has too many attachments on it.");
            }

            resultData = new KeyValuePair<Item, Item>(tool, attachItem);
            return ExecutionResult.Success();
        }

        protected override void Execute(KeyValuePair<Item, Item> resultData)
        {
            BotHandler.Bot.Inventory.AttachItem(resultData.Value,resultData.Key);
            RegisterInventoryActions();
        }
    }
    
    public class RemoveFromItem : NeuroAction<Item>
    {
        public override string Name => "remove_attached_item";
        protected override string Description => "Remove the item attached to another item. The attachment will either," +
                                                 " be placed the first empty slot in your inventory or it will be" +
                                                 " dropped on the floor. This depends on how much free space you have.";
        protected override JsonSchema Schema => new()
        {
            Type = JsonSchemaType.Object,
            Required = new List<string> { "item" },
            Properties = new Dictionary<string, JsonSchema>
            {
                ["item"] = QJS.Enum(Enumerable.Range(0, BotHandler.Bot.Inventory.MaxInventory))
            }
        };
        protected override ExecutionResult Validate(ActionData actionData, out Item? resultData)
        {
            int? index = actionData.Data?.Value<int>("item");

            resultData = null;
            if (index is null)
            {
                return ExecutionResult.Failure($"The index you provided was null.");
            }

            if (!Enumerable.Range(0, BotHandler.Bot.Inventory.MaxInventory).Contains((int)index))
            {
                return ExecutionResult.Failure($"The index you provided was not a valid index");
            }

            Item i = BotHandler.Bot.Inventory.Inventory[(int)index];
            if (i is not Tool tool)
            {
                return ExecutionResult.Failure($"The index you provided does not point to an item that has an attachment slot.");
            }

            if (tool.attachments.Count(obj => obj is not null) == 0)
            {
                return ExecutionResult.Failure($"This item does not have an attachment on it.");
            }

            resultData = tool;
            return ExecutionResult.Success($"Removing the {tool.DisplayName}'s attachment");
        }

        protected override void Execute(Item? resultData)
        {
            if (resultData is not Tool tool)
            {
                return;
            }
            
            BotHandler.Bot.Inventory.RemoveAttached(tool);
            RegisterInventoryActions();
        }
    }
    
    #endregion

    #region ToolBar

    public class ChangeSelectedToolbarSlot : NeuroAction<int>
    	{
    		public override string Name => "change_toolbar_slot";
    		protected override string Description => $"Change currently selected toolbar slot, the slots available are between 0,{BotHandler.Farmer.MaxItems}.";
    		protected override JsonSchema Schema => new()
    		{
    			Type = JsonSchemaType.Object,
    			Required = new List<string> { "slot" },
    			Properties = new Dictionary<string, JsonSchema>
    			{
    				["slot"] = QJS.Enum(Enumerable.Range(0, BotHandler.Farmer.MaxItems))
    			}
    		};
            
    		protected override ExecutionResult Validate(ActionData actionData, out int resultData)
    		{
    			string? slotStr = actionData.Data?.Value<string>("slot");
    
    			if (string.IsNullOrEmpty(slotStr))
    			{
    				resultData = -1;
    				return ExecutionResult.Failure($"slot can not be null");
    			}
                
    			int slot = int.Parse(slotStr);
    
    			if (!Enumerable.Range(0, BotHandler.Farmer.MaxItems).Contains(slot))
    			{
    				resultData = -1;
    				return ExecutionResult.Failure($"{slot} is not a valid slot index");
    			}
    
    			resultData = slot;
    			return ExecutionResult.Success($"Changing to slot: {slot}");
    		}
    
    		protected override void Execute(int resultData)
    		{
    			int? toolbarRotates = resultData / 12;
    			for (int i = 0; i < toolbarRotates; i++)
    			{
    				BotHandler.Bot.Inventory.SelectInventoryRowForToolbar(true);
    				resultData -= 12;
    			}
    			
    			BotHandler.Bot.Inventory.SelectSlot(resultData);
    		}
    	}

    #endregion
    
    public static void RegisterInventoryActions()
    {
        ActionWindow actionWindow = ActionWindow.Create(Main.GameInstance);
        actionWindow.AddAction(new MoveItem()).AddAction(new ExitInventory()).AddAction(new InteractWithTrinkets()).AddAction(new ChangeClothing())
            .AddAction(new CraftingActions.GoToCrafting()).AddAction(new RemoveItem());
        
        bool attach = BotHandler.Bot.Inventory.Inventory.Any(item => item is Tool tool && tool.AttachmentSlotsCount > 0);
        if (attach) actionWindow.AddAction(new AttachItem());

        bool remove = BotHandler.Bot.Inventory.Inventory.Any(item => 
            item is Tool tool && tool.attachments.Any(att => att is not null));
        if (remove) actionWindow.AddAction(new RemoveFromItem());

        string nameList = InventoryContext.GetInventoryString(BotHandler.Bot.Inventory.Inventory, true, true);
        List<string> itemList = PrepareItemStringList(BotHandler.Bot.Inventory.GetEquippedClothing()).ToList();
        List<string> trinkets = BotHandler.Bot.Inventory.GetCurrentEquippedTrinkets(Game1.player)
            .Where(trinket => trinket is not null).Select(trinket => trinket.DisplayName).ToList();
        
        string state = $"These are the items in your inventory: {nameList}" +
                       $"\nThese are the clothes you have equipped {string.Concat(itemList)}";
        if (trinkets.Count > 0)
        {
            state += $"\nThis is the trinket you have equipped currently: {string.Concat(trinkets)}";
        }
        actionWindow.SetForce(0, "You are in your inventory.", state,true);
        actionWindow.Register();
    }

    private static IEnumerable<string> PrepareItemStringList(Dictionary<string,Item> getEquippedClothing)
    {
        Inventory inventory = new Inventory();
        inventory.AddRange(getEquippedClothing.Values);
        IEnumerable<Item> items = PrepareItemStringList(inventory);
        List<string> itemString = new(); 
        using var enumerator = items.GetEnumerator();
        while (enumerator.MoveNext())
        {
            foreach (var kvp in getEquippedClothing)
            {
                if (kvp.Value == enumerator.Current)
                {
                    itemString.Add($"\n{kvp.Key}: {enumerator.Current.DisplayName}");
                }
            }
        }

        return itemString;
    }

    private static IEnumerable<Item> PrepareItemStringList(Inventory items)
    {
        IEnumerable<Item> list = items.Where(item => item is not null).Where(item => !string.IsNullOrEmpty(item.DisplayName));
        return list;
    }
}