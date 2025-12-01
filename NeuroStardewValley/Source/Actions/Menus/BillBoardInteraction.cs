using NeuroSDKCsharp.Actions;
using NeuroSDKCsharp.Json;
using NeuroSDKCsharp.Websocket;
using NeuroStardewValley.Source.ContextStrings;
using StardewValley.Menus;

namespace NeuroStardewValley.Source.Actions.Menus;

public static class BillBoardInteraction
{
	public class AcceptDailyQuest : NeuroAction
	{
		public override string Name => "accept_quest";
		protected override string Description => "Accept this quest, if you accept this quest it will close the menu " +
		                                         "automatically. If you want to look at the quest again, you can use the quest log.";
		protected override JsonSchema Schema => new();
		protected override ExecutionResult Validate(ActionData actionData)
		{
			return ExecutionResult.Success();
		}

		protected override void Execute()
		{
			Main.Bot.BillBoard.AcceptDailyQuest();
			Main.Bot.BillBoard.RemoveMenu();
		}
	}

	public static void RegisterQuestActions()
	{
		Main.Bot.BillBoard.GetDailyQuest(out string title,out string description,out var objective);
		ActionWindow window = ActionWindow.Create(Main.GameInstance);
		window.AddAction(new AcceptDailyQuest());
		window.SetForce(0, "You have opened the daily quest billboard.",
			$"This is the quest today: {title}.\n{QuestContext.FormatDailyQuest(description)}\n\n{objective}");
		window.Register();
	}

	public static string GetCalendarContext()
	{
		List<string> contextList = Main.Bot.BillBoard.GetCalendar()
			.Where(kvp => kvp.Value.Type != Billboard.BillboardEventType.None).Select(kvp =>
				$"\nDay: {kvp.Key}, event type: {kvp.Value.Type}, event description: {kvp.Value.HoverText}").ToList();
		
		return string.Concat(contextList);
	}
}