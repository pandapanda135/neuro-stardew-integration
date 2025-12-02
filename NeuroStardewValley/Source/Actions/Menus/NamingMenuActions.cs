using NeuroSDKCsharp.Actions;
using NeuroSDKCsharp.Json;
using NeuroSDKCsharp.Websocket;
using StardewValley.Menus;

namespace NeuroStardewValley.Source.Actions.Menus;

public static class NamingMenuActions
{
	// We use text instead of name as this is also used for signs and having it be more universal is probably better
	// than made for something like naming a horse as I doubt she can reach that on her own.
	private class SetText : NeuroAction<string>
	{
		public override string Name => "change_text";

		protected override string Description => $"Set the text for this, you can use a maximum of " +
		                                         $"{BotHandler.Bot.NamingMenu.Menu.textBox.textLimit} characters with a " +
		                                         $"minimum of {BotHandler.Bot.NamingMenu.Menu.minLength} character";
		protected override JsonSchema Schema => new()
		{
			Type = JsonSchemaType.Object,
			Required = new List<string> { "text" },
			Properties = new Dictionary<string, JsonSchema>
			{
				["text"] = QJS.Type(JsonSchemaType.String)
			}
		};
		protected override ExecutionResult Validate(ActionData actionData, out string? resultData)
		{
			string? name = actionData.Data?.Value<string>("text");

			resultData = null;
			if (name is null)
			{
				return ExecutionResult.Failure($"You provided a null value to a string.");
			}

			if (name.Length > BotHandler.Bot.NamingMenu.Menu.textBox.textLimit ||
			    name.Length <= BotHandler.Bot.NamingMenu.Menu.minLength)
			{
				return ExecutionResult.Failure(
					$"The text you added did not adhere to the text box's limit, you can only type a max of" +
					$" {BotHandler.Bot.NamingMenu.Menu.textBox.textLimit} character and a minimum of" +
					$" {BotHandler.Bot.NamingMenu.Menu.minLength} character.");
			}

			resultData = name;
			return ExecutionResult.Success($"You have added {name}");
		}

		protected override void Execute(string? resultData)
		{
			if (resultData is null) return;
			BotHandler.Bot.NamingMenu.ChangeName(resultData);
			BotHandler.Bot.NamingMenu.DoneNaming();
		}
	}
	private class RandomName : NeuroAction
	{
		public override string Name => "randomize_name";
		protected override string Description => "Press the random name button and get a random name from the game.";
		protected override JsonSchema Schema => new();
		protected override ExecutionResult Validate(ActionData actionData)
		{
			return ExecutionResult.Success();
		}

		protected override void Execute()
		{
			BotHandler.Bot.NamingMenu.RandomizeName();
			RegisterActions();
		}
	}

	public static void RegisterActions()
	{
		ActionWindow window = ActionWindow.Create(Main.GameInstance);

		window.AddAction(new SetText());
		if (BotHandler.Bot.NamingMenu.Menu.randomButton.visible) window.AddAction(new RandomName());

		string state = $"{BotHandler.Bot.NamingMenu.Menu.title}";
		
		if (BotHandler.Bot.NamingMenu.Menu is TitleTextInputMenu textInputMenu) state = $"{textInputMenu.title}";

		if (BotHandler.Bot.NamingMenu.Menu.textBox.Text != "") state += $" The current text is {BotHandler.Bot.NamingMenu.Menu.textBox.Text}";
		window.SetForce(0, $"You are now interacting with a menu that has the title " +
		                   $"\"{BotHandler.Bot.NamingMenu.Menu.title}\", you should enter text to fit this.", state);
		window.Register();
	}
}