using NeuroSDKCsharp.Actions;
using NeuroSDKCsharp.Json;
using NeuroSDKCsharp.Messages.Outgoing;
using NeuroSDKCsharp.Websocket;
using NeuroStardewValley.Source.ContextStrings;
using StardewValley;
using StardewValley.Menus;

namespace NeuroStardewValley.Source.Actions.Menus;

public static class QuestLogActions
{
	public class OpenLog : NeuroAction
	{
		public override string Name => "open_quest_log";
		protected override string Description => "See all of your current quests and accept their rewards if they are completed.";
		protected override JsonSchema Schema => new();
		protected override ExecutionResult Validate(ActionData actionData)
		{
			return ExecutionResult.Success();
		}

		protected override void Execute()
		{
			Game1.activeClickableMenu = new QuestLog();
		}
	}
	
	private class GetQuestReward : NeuroAction<int>
	{
		public override string Name => "get_reward";
		protected override string Description =>
			"Get the reward for a quest, this will only work if the quest has been completed and has a reward.";
		protected override JsonSchema Schema => new JsonSchema()
		{
			Type = JsonSchemaType.Object,
			Required = new List<string> { "quest_index" },
			Properties = new Dictionary<string, JsonSchema>
			{
				["quest_index"] = QJS.Enum(Enumerable.Range(0, Main.Bot._farmer.questLog.Count))
			}
		};
		protected override ExecutionResult Validate(ActionData actionData, out int resultData)
		{
			int? selectedIndex = actionData.Data?.Value<int>("quest_index");

			if (selectedIndex is null)
			{
				resultData = -1;
				return ExecutionResult.Failure($"You provided a null value as the index");
			}

			int index = (int)selectedIndex;
			if (!Enumerable.Range(0, Game1.player.questLog.Count).Contains(index))
			{
				resultData = -1;
				return ExecutionResult.Failure($"You have provided an invalid index.");
			}

			if (!Main.Bot.QuestLog.Quests[index].completed.Value || !Main.Bot.QuestLog.Quests[index].HasReward())
			{
				resultData = -1;
				return ExecutionResult.Failure($"This quest does not have a reward or you have not completed it.");
			}

			resultData = index;
			return ExecutionResult.Success($"opening the quest at {index}");
		}

		protected override void Execute(int resultData)
		{
			int pageFlips = resultData / QuestLog.questsPerPage;

			for (int i = 0; i < pageFlips; i++)
			{
				Main.Bot.QuestLog.NextRightPage();
			}

			Main.Bot.QuestLog.OpenQuestIndex(resultData - pageFlips * QuestLog.questsPerPage);
			Main.Bot.QuestLog.GetReward();
			Main.Bot.QuestLog.CloseQuest();
			RegisterActions();
		}
	}

	private class CloseLog : NeuroAction
	{
		public override string Name => "close_log";
		protected override string Description => "Close the quest log";
		protected override JsonSchema Schema => new();
		protected override ExecutionResult Validate(ActionData actionData)
		{
			return ExecutionResult.Success();
		}

		protected override void Execute()
		{
			Main.Bot.QuestLog.CloseLog();
		}
	}

	private class SaveContext : NeuroAction<int>
	{
		public override string Name => "save_in_context";
		protected override string Description => "You should call this if you want to save a specific quest in your" +
		                                         " context. An example of using this is if you want to complete a quest.";
		protected override JsonSchema Schema => new()
		{
			Type = JsonSchemaType.Object,
			Required = new List<string> { "quest_index" },
			Properties = new Dictionary<string, JsonSchema>
			{
				["quest_index"] = QJS.Enum(Enumerable.Range(0, Game1.player.questLog.Count))
			}
		};
		protected override ExecutionResult Validate(ActionData actionData, out int resultData)
		{
			int? selectedIndex = actionData.Data?.Value<int>("quest_index");

			if (selectedIndex is null)
			{
				resultData = -1;
				return ExecutionResult.Failure($"You provided a null value as the index");
			}

			int index = (int)selectedIndex;
			if (!Enumerable.Range(0, Game1.player.questLog.Count).Contains(index))
			{
				resultData = -1;
				return ExecutionResult.Failure($"You have provided an invalid index.");
			}

			resultData = index;
			return ExecutionResult.Success($"You have added {Main.Bot.QuestLog.Quests[resultData].questTitle} to your context.");
		}

		protected override void Execute(int resultData)
		{
			Context.Send($"{QuestContext.GetSingleQuest(Main.Bot.QuestLog.Quests[resultData])}",true);
			RegisterActions();
		}
	}

	public static void RegisterActions()
	{
		ActionWindow window = ActionWindow.Create(Main.GameInstance);
		window.AddAction(new GetQuestReward()).AddAction(new CloseLog()).AddAction(new SaveContext());
		window.SetForce(0, "You are now in the quest log.",
			$"if you want to complete a quest, you should add it to your context and complete the objective. " +
			$"These are all of the quests you currently have {QuestContext.GetQuestsStrings()}.",true);
		window.Register();
	}
}