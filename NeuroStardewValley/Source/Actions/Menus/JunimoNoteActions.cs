using NeuroSDKCsharp.Actions;
using NeuroSDKCsharp.Json;
using NeuroSDKCsharp.Websocket;
using NeuroStardewValley.Debug;
using NeuroStardewValley.Source.ContextStrings;
using NeuroStardewValley.Source.RegisterActions;
using NeuroStardewValley.Source.Utilities;
using StardewValley;
using StardewValley.Menus;

namespace NeuroStardewValley.Source.Actions.Menus;

public static class JunimoNoteActions
{
	[Obsolete("AddItem now handles bundle interaction.")]
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

	[Obsolete("AddItem now handles bundle interaction")]
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
	
	private class AddItem : NeuroAction<Item>
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

			Bundle? selectedBundle = null;
			foreach (var kvp in BundleItems.Where(kvp => kvp.Value.Any(i => i.DisplayName == item)))
			{
				// I would rather not run this here, but there is no other way to access the ingredients slots without copying and pasting a lot of code
				BotHandler.Bot.JunimoNote.SelectBundle(BotHandler.Bot.JunimoNote.Menu.bundles.IndexOf(kvp.Key));
				selectedBundle = kvp.Key;
				break;
			}

			if (selectedBundle is null)
				return ExecutionResult.Failure($"There was an issue getting the bundle for this item, should try another item.");
			
			Item? i = BotHandler.Bot.Inventory.Inventory.ToList().Find(i => i.DisplayName == item);
			if (i is null)
			{
				return ExecutionResult.Failure($"The item you provided does not exist.");
			}
			
			if (!selectedBundle.depositsAllowed)
			{
				return ExecutionResult.Failure($"You cannot deposit in this bundle, this is most likely because you are not at the bundles in the community center.");
			}

			var canAccept = false;
			foreach (var ingCc in BotHandler.Bot.JunimoNote.Menu.ingredientSlots)
			{
				Logger.Info($"can accept item: {ingCc}");
				if (!selectedBundle.canAcceptThisItem(i, ingCc)) continue;
				
				canAccept = true;
				break;
			}
			if (!canAccept)
			{
				return ExecutionResult.Failure($"You cannot place this item in any slot, This is most likely because this item is not valid.");
			}
			
			resultData = i;
			return ExecutionResult.Success();
		}

		protected override async void Execute(Item? resultData)
		{
			try
			{
				if (resultData is null) return;
				// I know this is ugly, but otherwise it is hard for viewers to see what is happening :(
				await Util.WaitForSeconds(1);
				BotHandler.Bot.JunimoNote.AddItem(resultData);
				await Util.WaitForSeconds(1);
				BotHandler.Bot.JunimoNote.ExitCurrentBundle();
				await Util.WaitForSeconds(0.5);
				RegisterActions();
			}
			catch (Exception e)
			{
				Logger.Error($"error in AddItem: {e}");
				if (Game1.activeClickableMenu != null)
				{
					Game1.activeClickableMenu.exitThisMenu();
					return;
				}

				RegisterMainActions.RegisterPostAction();
				throw;
			}
		}

		private static readonly Dictionary<Bundle, List<Item>> BundleItems = new();
		public static IEnumerable<string> GetSchema()
		{
			BundleItems.Clear();
			foreach (var bundle in BotHandler.Bot.JunimoNote.Menu.bundles)
			{
				Logger.Info($"checking bundle: {bundle.name}  {bundle.ingredients.Count}");
				var i = BotHandler.Bot.JunimoNote.Menu.inventory.actualInventory.Where(item => item is not null && 
					bundle.ingredients.Exists(desc => !desc.completed && desc.id == item.ItemId && item.Stack >= desc.stack)).ToList();
				Logger.Info($"i amount: {i.Count}");
				BundleItems.Add(bundle,i);
			}

			return BundleItems.SelectMany(kvp => kvp.Value.Select(item => item.DisplayName));
		}
	}

	private class ExitMenu : NeuroAction
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

		if (AddItem.GetSchema().Any())
		{
			window.AddAction(new AddItem());
		}

		string state = $"";
		foreach (var bundle in BotHandler.Bot.JunimoNote.Menu.bundles)
		{
			string itemsString = string.Join("",bundle.ingredients.Select(desc =>
				$"\n## {ItemRegistry.Create(desc.id).DisplayName}" +
				$"\n### Quality: {InventoryContext.QualityStrings[ItemRegistry.Create(desc.id).Quality]}" +
				$"\n### Amount: {desc.stack}"));
			
			state += $"\n# {bundle.name}{itemsString}";
		}
		
		window.AddAction(new ExitMenu());
		window.SetForce(0, "", $"These are the items needed in the bundle: {state}");
		window.Register();
	}
}