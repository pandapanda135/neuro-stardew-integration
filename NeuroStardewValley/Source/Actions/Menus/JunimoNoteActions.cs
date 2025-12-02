using NeuroSDKCsharp.Actions;
using NeuroSDKCsharp.Json;
using NeuroSDKCsharp.Websocket;
using NeuroStardewValley.Debug;
using NeuroStardewValley.Source.ContextStrings;
using StardewValley;
using StardewValley.Menus;

namespace NeuroStardewValley.Source.Actions.Menus;

public static class JunimoNoteActions
{
	private class SelectBundle : NeuroAction<Bundle>
	{
		public override string Name => "select_bundle";
		protected override string Description => "Select a bundle to open from the options.";
		protected override JsonSchema Schema => new()
		{
			Type = JsonSchemaType.Object,
			Required = new List<string> { "bundle" },
			Properties = new Dictionary<string, JsonSchema>
			{
				["bundle"] = QJS.Enum(BotHandler.Bot.JunimoNote.Menu.bundles.Where(bundle => !bundle.complete && bundle.canBeClicked()).Select(bundle => bundle.name))
			}
		};
		protected override ExecutionResult Validate(ActionData actionData, out Bundle? resultData)
		{
			string? s = actionData.Data?.Value<string>("bundle");

			resultData = null;
			if (s is null)
			{
				return ExecutionResult.Failure($"You provided a null value to bundle");
			}

			if (BotHandler.Bot.JunimoNote.Menu.bundles.All(bundle => bundle.name != s))
			{
				return ExecutionResult.Failure($"You provided a value that does not exist.");
			}

			resultData = BotHandler.Bot.JunimoNote.Menu.bundles.Find(bundle => bundle.name == s);
			return ExecutionResult.Success($"You have selected: {resultData?.name}");
		}

		protected override void Execute(Bundle? resultData)
		{
			BotHandler.Bot.JunimoNote.SelectBundle(BotHandler.Bot.JunimoNote.Menu.bundles.IndexOf(resultData));
			RegisterActions();
		}
	}

	private class ExitBundle : NeuroAction
	{
		public override string Name => "exit_bundle";
		protected override string Description => "Exit this bundle to see the others.";
		protected override JsonSchema Schema => new();
		protected override ExecutionResult Validate(ActionData actionData)
		{
			if (!BotHandler.Bot.JunimoNote.Menu.isReadyToCloseMenuOrBundle())
			{
				return ExecutionResult.Failure($"You cannot close this menu right now.");
			}
			return ExecutionResult.Success();
		}

		protected override void Execute()
		{
			var cc = BotHandler.Bot.JunimoNote.Menu.backButton;
			BotHandler.Bot.JunimoNote.Menu.receiveLeftClick(cc.bounds.X,cc.bounds.Y);
			RegisterActions();
		}
	}
	
	public class AddItem : NeuroAction<Item>
	{
		public override string Name => "add_item";
		protected override string Description => "Add an item to this bundle.";

		protected override JsonSchema Schema => new()
		{
			Type = JsonSchemaType.Object,
			Required = new List<string> { "item" },
			Properties = new Dictionary<string, JsonSchema>
			{
				["item"] = QJS.Enum(GetSchema())
			}
		};
		protected override ExecutionResult Validate(ActionData actionData, out Item? resultData)
		{
			string? item = actionData.Data?.Value<string>("item");

			resultData = null;
			if (item is null)
			{
				return ExecutionResult.Failure($"You have provided a null value that is not allowed");
			}
			
			Item? i = BotHandler.Bot.JunimoNote.Menu.inventory.actualInventory.ToList().Find(i => i.DisplayName == item);
			if (i is null)
			{
				return ExecutionResult.Failure($"The item you provided does not exist.");
			}
			var menu = BotHandler.Bot.JunimoNote.Menu;
			
			if (!menu.currentPageBundle.depositsAllowed)
			{
				return ExecutionResult.Failure($"You cannot deposit in this bundle, this is most likely because you are not at the bundles in the community center.");
			}

			bool canAccept = false;
			foreach (var ingCc in menu.ingredientSlots)
			{
				Logger.Info($"can accept item: {ingCc}");
				if (menu.currentPageBundle.canAcceptThisItem(i, ingCc))
				{
					canAccept = true;
				}
			}
			if (!canAccept)
			{
				return ExecutionResult.Failure($"You cannot place this item in any slot, This is most likely because this item is not valid.");
			}
			
			resultData = i;
			return ExecutionResult.Success();
		}

		protected override void Execute(Item? resultData)
		{
			if (resultData is null) return;
			BotHandler.Bot.JunimoNote.AddItem(resultData);
			RegisterActions();
		}

		public static List<string> GetSchema()
		{
			IEnumerable<Item> items = BotHandler.Bot.JunimoNote.Menu.inventory.actualInventory.Where(item => item is not null &&
				BotHandler.Bot.JunimoNote.Menu.currentPageBundle.ingredients.Exists(desc => !desc.completed && ItemRegistry.Create(desc.id).Name == item.Name && desc.id == item.ItemId));
			
			List<string> itemString = new();
			using var enumerator = items.GetEnumerator();
			while (enumerator.MoveNext())
			{
				if (enumerator.Current is null) continue;
				itemString.Add($"{enumerator.Current.DisplayName}");
			}

			return itemString;
		}
	}

	public class ExitMenu : NeuroAction
	{
		public override string Name => "exit_menu";
		protected override string Description => "Exit the menu, not the current bundle.";
		protected override JsonSchema Schema => new();
		protected override ExecutionResult Validate(ActionData actionData)
		{
			if (!BotHandler.Bot.JunimoNote.Menu.isReadyToCloseMenuOrBundle())
			{
				return ExecutionResult.Failure($"You cannot close this menu right now.");
			}
			return ExecutionResult.Success();
		}

		protected override void Execute()
		{
			BotHandler.Bot.JunimoNote.RemoveMenu();
		}
	}

	public static void RegisterActions()
	{
		ActionWindow window = ActionWindow.Create(Main.GameInstance);

		if (BotHandler.Bot.JunimoNote.Menu.currentPageBundle is null || !BotHandler.Bot.JunimoNote.Menu.specificBundlePage || !BotHandler.Bot.JunimoNote.Menu.backButton.visible)
		{
			string reward = BotHandler.Bot.JunimoNote.Menu.getRewardNameForArea(BotHandler.Bot.JunimoNote.Menu.whichArea);
			window.AddAction(new SelectBundle()).AddAction(new ExitMenu());
			window.SetForce(0, "You are now able to select a bundle, adding items to all of the bundles" +
			                   " in this page will lead to completing this page and getting the reward.",
				$"For completing this page you will get a {reward.Substring(8)}.");
		}
		else
		{
			if (AddItem.GetSchema().Count > 0)
			{
				window.AddAction(new AddItem());
			}
			string state = string.Concat(BotHandler.Bot.JunimoNote.Menu.currentPageBundle.ingredients
				.Where(desc => !desc.completed).Select(desc => 
					$"\n-{ItemRegistry.Create(desc.id).Name}:\n-- Rarity: {InventoryContext.QualityStrings[ItemRegistry.Create(desc.id).Quality]}\n-- Amount: {desc.stack}"));
			window.AddAction(new ExitMenu()).AddAction(new ExitBundle());
			window.SetForce(0, "This is an item you can add items to.", $"These are the items needed in the bundle: {state}");
		}
		
		window.Register();
	}
}