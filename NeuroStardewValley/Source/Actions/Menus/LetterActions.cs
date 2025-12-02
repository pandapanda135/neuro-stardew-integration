using NeuroSDKCsharp.Actions;
using NeuroSDKCsharp.Json;
using NeuroSDKCsharp.Websocket;
using NeuroStardewValley.Source.ContextStrings;
using StardewValley;

namespace NeuroStardewValley.Source.Actions.Menus;

public static class LetterActions
{
	private class AcceptQuest : NeuroAction
	{
		public override string Name => "accept_quest";
		protected override string Description => "Accept the quest or special order in this quest.";
		protected override JsonSchema Schema => new();
		protected override ExecutionResult Validate(ActionData actionData)
		{
			return ExecutionResult.Success();
		}

		protected override void Execute()
		{
			BotHandler.Bot.LetterViewer.AcceptQuest();
			BotHandler.Bot.LetterViewer.ClickCloseButton();
		}
	}

	private class TakeItems : NeuroAction<Item>
	{
		public override string Name => "take_item";
		protected override string Description => "Take the items in this letter.";
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
			string? action = actionData.Data?.Value<string>("item");

			resultData = null;
			if (action is null) return ExecutionResult.Failure($"The item you provided was null");
			
			if (GetSchema().IndexOf(action) == -1) return ExecutionResult.Failure($"The item you provided does not exist");
			
			resultData = BotHandler.Bot.LetterViewer.Items[GetSchema().IndexOf(action)].item;
			return ExecutionResult.Success();
		}

		protected override void Execute(Item? resultData)
		{
			if (resultData is null) return;
			BotHandler.Bot.LetterViewer.GrabItem(resultData);
			RegisterActions();
		}

		private static List<string> GetSchema()
		{
			List<string> schema = new();
			foreach (var cc in BotHandler.Bot.LetterViewer.Items)
			{
				schema.Add($"item name: {cc.item.DisplayName} name: {cc.name}");
			}

			return schema;
		}
	}

	private class CloseMenu : NeuroAction
	{
		public override string Name => "exit_letter";
		protected override string Description => "Exit this letter, this still may accept the quest or order for you automatically.";
		protected override JsonSchema Schema => new();
		protected override ExecutionResult Validate(ActionData actionData)
		{
			return ExecutionResult.Success();
		}

		protected override void Execute()
		{
			BotHandler.Bot.LetterViewer.ClickCloseButton();
		}
	}

	public static void RegisterActions()
	{
		ActionWindow window = ActionWindow.Create(Main.GameInstance);
		if (BotHandler.Bot.LetterViewer.Items.Count > 0)
		{
			window.AddAction(new TakeItems());
		}

		if (BotHandler.Bot.LetterViewer.HasQuest == true)
		{
			window.AddAction(new AcceptQuest());
		}
		window.AddAction(new CloseMenu());
		window.SetForce(0, $"You have opened a letter, it has either a quest or special order in it.", $"{LetterContext.GetFullLetterString()}");
		window.Register();
	}
}