using NeuroSDKCsharp.Actions;
using NeuroSDKCsharp.Json;
using NeuroSDKCsharp.Websocket;
using StardewBotFramework.Debug;

namespace NeuroStardewValley.Source.Actions.Menus;

public static class ItemListMenuActions
{
	private class ExitMenu : NeuroAction
	{
		public override string Name => "exit_menu";
		protected override string Description => "Exit this menu.";
		protected override JsonSchema Schema => new();
		protected override ExecutionResult Validate(ActionData actionData)
		{
			return ExecutionResult.Success($"");
		}

		protected override void Execute()
		{
			BotHandler.Bot.ItemListMenu.ClickOk();
			BotHandler.Bot.ItemListMenu.RemoveMenu();
		}
	}

	public static void RegisterActions()
	{
		ActionWindow window = ActionWindow.Create(Main.GameInstance);
		window.AddAction(new ExitMenu());
		var items = BotHandler.Bot.ItemListMenu.GetItems().Where(item => item is not null).Select(item => $"\n{item.DisplayName} amount: {item.stack}");
		window.SetForce(0, "You are in a menu, that tells you about the items you lost.",
			$"{string.Concat(items.ToList())}");
		window.Register();
	}
}