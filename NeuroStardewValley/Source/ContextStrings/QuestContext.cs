using NeuroStardewValley.Debug;
using NeuroStardewValley.Source.Utilities;
using StardewValley.Quests;

namespace NeuroStardewValley.Source.ContextStrings;

public static class QuestContext
{
	public static string GetSingleQuest(Quest quest)
	{
		string questString = "";
		questString = string.Concat(questString, $"\nTitle: {quest.questTitle}. Description: {quest.questDescription} Objective: {quest.currentObjective}");
		if (quest.HasMoneyReward())
		{
			questString = string.Concat(questString, $" Reward: ${quest.moneyReward.Value}");
		}
		else if (quest.HasReward())
		{
			questString = string.Concat(questString, $" Reward: {quest.rewardDescription}");
		}

		if (quest.completed.Value)
		{
			questString = string.Concat(questString, $"This quest has been completed, you should get your reward from it.");
		}
		else
		{
			questString = string.Concat(questString, $"You have not completed this quest yet.");
		}

		return string.Concat(questString, quest.IsTimedQuest() ? $" Time left: {quest.GetDaysLeft()}" : $" There is no time limit on this quest.");
	}
	
	public static string GetQuestsStrings()
	{
		string questString = "These are the current quests that are active.";
		foreach (var quest in Main.Bot.QuestLog.Quests)
		{
			questString = string.Concat(questString,GetSingleQuest(quest));
		}

		return questString;
	}
	
	public static string GetQuestTitles()
	{
		return Main.Bot.QuestLog.Quests.Aggregate("", (current, quest) => string.Concat(current, $"\n{quest.questTitle}"));
	}
	
	public static string FormatDailyQuest(string description)
	{
		string formattedMessage = description;
		char lastChar = '#';
		int spaceRepeat = 0;
		foreach (var c in formattedMessage) // we do this to remove the large gaps in text
		{
			if (c == lastChar && c == ' ')
			{
				spaceRepeat++;
			}

			lastChar = c;
		}

		string str = "";
		for (int i = 0; i < spaceRepeat; i++)
		{
			str += " ";
		}

		if (str != "")
		{
			formattedMessage = formattedMessage.Replace(str, "");
		}

		return formattedMessage;
	}


	public static async Task GetQuestsRewards()
	{
		if (Main.Bot.QuestLog.Quests.All(quest => !quest.completed.Value)) return;
		
		await Util.WaitForSeconds(1);
		Main.Bot.QuestLog.OpenLog();
		if (Main.Bot.QuestLog.PageQuests is null || Main.Bot.QuestLog.CurrentPage is null)
		{
			Main.Bot.QuestLog.CloseLog();
			return;
		}
		
		await Util.WaitForSeconds(1);
		for (int pageI = 0; pageI < Main.Bot.QuestLog.PageQuests.Count; pageI++)
		{
			Logger.Info($"page I: {pageI}   current page: {Main.Bot.QuestLog.CurrentPage}");
			var page = Main.Bot.QuestLog.PageQuests[pageI];
			for (int i = 0; i < page.Count; i++)
			{
				// I feel increasing this will cause issues in the future, but I hope not.
				i++;
				if (Main.Bot.QuestLog.InQuestSubMenu) Main.Bot.QuestLog.CloseQuest();
				
				Logger.Info($"quest: {page[i].GetName()}  {page[i].GetDescription()}   i: {i}");
				if (!page[i].ShouldDisplayAsComplete()) continue;
				// I have not tested this code, hope it works :)
				if (Main.Bot.QuestLog.CurrentPage != pageI)
				{
					if (Main.Bot.QuestLog.CurrentPage > pageI)
					{
						for (int left = (int)Main.Bot.QuestLog.CurrentPage - pageI; left > 0; left--)
						{
							Main.Bot.QuestLog.BackLeftPage();
						}
					}
					else
					{
						for (int right = pageI - (int)Main.Bot.QuestLog.CurrentPage; right > 0; right--)
						{
							Main.Bot.QuestLog.ForwardRightPage();
						}
					}

					await Util.WaitForSeconds(1);
				}
			
				await Util.WaitForSeconds(0.5);
				Main.Bot.QuestLog.OpenQuestIndex(i + 1);
				await Util.WaitForSeconds(0.5);
				Main.Bot.QuestLog.GetReward();
			}	
		}

		await Util.WaitForSeconds(1);
		Main.Bot.QuestLog.CloseLog();
	}
}