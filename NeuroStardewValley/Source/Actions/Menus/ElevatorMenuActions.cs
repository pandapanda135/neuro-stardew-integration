using NeuroSDKCsharp.Actions;
using NeuroSDKCsharp.Json;
using NeuroSDKCsharp.Websocket;
using StardewValley;

namespace NeuroStardewValley.Source.Actions.Menus;

public static class ElevatorMenuActions
{
	public class SelectButton : NeuroAction<int>
	{
		public override string Name => "select_button";
		protected override string Description => "Select the floor to move to using a button.";
		protected override JsonSchema Schema => new()
		{
			Type = JsonSchemaType.Object,
			Required = new List<string> { "button" },
			Properties = new Dictionary<string, JsonSchema>
			{
				["button"] = QJS.Enum(BotHandler.Bot.ElevatorMenu.Menu.elevators.Where(cc => int.Parse(cc.name) != Game1.CurrentMineLevel).Select(cc => cc.name).ToList())
			}
		};
		protected override ExecutionResult Validate(ActionData actionData, out int resultData)
		{
			string? buttonName = actionData.Data?.Value<string>("button");

			resultData = -1;
			if (buttonName is null) return ExecutionResult.Failure($"You cannot provide a null value.");

			List<string> ccNames = BotHandler.Bot.ElevatorMenu.Menu.elevators
				.Where(cc => int.Parse(cc.name) != Game1.CurrentMineLevel).Select(cc => cc.name).ToList();
			if (!ccNames.Contains(buttonName))
			{
				return ExecutionResult.Failure($"You provided a value that is not valid.");
			}

			if (int.Parse(buttonName) == Game1.CurrentMineLevel) // shouldn't be needed, I don't trust my code though.
			{
				return ExecutionResult.Failure($"This value cannot be provided as it is the same as your current mine level");
			}

			resultData = ccNames.IndexOf(buttonName) + BotHandler.Bot.ElevatorMenu.Menu.elevators
				.Count(cc => int.Parse(cc.name) == Game1.CurrentMineLevel); // add amount of non-valid buttons
			return ExecutionResult.Success($"You selected {buttonName}");
		}

		protected override void Execute(int resultData)
		{
			BotHandler.Bot.ElevatorMenu.SelectButton(resultData);
			BotHandler.Bot.ElevatorMenu.RemoveMenu();
		}
	}

	public static void RegisterAction()
	{
		ActionWindow window = ActionWindow.Create(Main.GameInstance);

		window.AddAction(new SelectButton());
		window.SetForce(0, "You are interacting with the elevator.",
			"The buttons represent the floor they will take you to. Generally you should go to the lowest floor," +
			" unless you are searching for specific monster, item or ore.");
		window.Register();
	}
}