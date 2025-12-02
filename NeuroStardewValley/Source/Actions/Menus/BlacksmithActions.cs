using NeuroSDKCsharp.Actions;
using NeuroSDKCsharp.Json;
using NeuroSDKCsharp.Messages.Outgoing;
using NeuroSDKCsharp.Websocket;
using NeuroStardewValley.Source.RegisterActions;
using StardewValley;
using StardewValley.Menus;

namespace NeuroStardewValley.Source.Actions.Menus;

public static class BlacksmithActions
{
	public class OpenGeode : NeuroAction<int>
	{
		public override string Name => "open_geode";
		protected override string Description => "Opens the geode that you select from your inventory.";
		protected override JsonSchema Schema => new()
		{
			Type = JsonSchemaType.Object,
			Required = new List<string> { "item_index" },
			Properties = new Dictionary<string, JsonSchema>
			{
				["item_index"] = QJS.Enum(GetSchema())
			}
		};
		protected override ExecutionResult Validate(ActionData actionData, out int resultData)
		{
			string? stringIndex= actionData.Data?.Value<string>("item_index");
			
			resultData = -1;
			if (string.IsNullOrEmpty(stringIndex) || !GetSchema().Contains(stringIndex))
			{
				return ExecutionResult.Failure($"{stringIndex} is not valid. This is either because it is null or not a valid item in the schema");	
			}
			
			int index = int.Parse(stringIndex);

			if (!Utility.IsGeode(BotHandler.Bot.Inventory.Inventory[index]))
			{
				return ExecutionResult.Failure($"{index} is not a geode");
			}

			if (Game1.player._money < 25)
			{
				return ExecutionResult.Failure($"You cannot afford to open a geode, you need 25g to open a geode.");
			}
			
			if (Game1.player.freeSpotsInInventory() == 0 && BotHandler.Bot.Inventory.Inventory[index].Stack > 1)
			{
				return ExecutionResult.Failure($"You do not have enough free space in your inventory, so you cannot open this geode. You should try to free some space.");
			}

			resultData = index;
			return ExecutionResult.Success();
		}

		protected override void Execute(int resultData)
		{
			BotHandler.Bot.Blacksmith.OpenGeode(resultData);
			
			// geode item takes time to be added to menu
			DelayedAction.functionAfterDelay(() =>
			{
				GeodeMenu? menu = Game1.activeClickableMenu as GeodeMenu;
				Context.Send($"You got a {menu?.geodeTreasure.DisplayName} from the geode!");
				RegisterStoreActions.RegisterBlacksmithActions();
			}, 3000);
		}

		private static string[] GetSchema()
		{
			List<string> strings = new();
			foreach (var item in BotHandler.Bot.Inventory.Inventory)
			{
				if (!Utility.IsGeode(item))
				{
					continue;
				}
				
				strings.Add(BotHandler.Bot.Inventory.Inventory.IndexOf(item).ToString());
			}

			return strings.ToArray();
		} 
	}

	public class CloseMenu : NeuroAction
	{
		public override string Name => "close_menu";
		protected override string Description => "Exit this menu.";
		protected override JsonSchema Schema => new();
		protected override ExecutionResult Validate(ActionData actionData)
		{
			return ExecutionResult.Success();
		}

		protected override void Execute()
		{
			BotHandler.Bot.Blacksmith.CloseGeodeMenu();
		}
	}
}