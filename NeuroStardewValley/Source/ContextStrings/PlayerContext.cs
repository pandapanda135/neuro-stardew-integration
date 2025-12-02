
namespace NeuroStardewValley.Source.ContextStrings;

public static class PlayerContext
{
	public static readonly Dictionary<int, string> DirectionNames = new()
	{
		{ 0, "North" },
		{ 1, "East" },
		{ 2, "South" },
		{ 3, "West" }
	};
	
	public static string GetAllCharactersLevel()
	{
		string charString = "";
		foreach (var social in BotHandler.Bot.PlayerInformation.RelationshipLevel())
		{
			string contextString = $"\n{social.DisplayName}: heart level: {social.HeartLevel}";
			if (social.IsRoommateForCurrentPlayer()) contextString += $", is your roommate";
			if (social.IsDatable) contextString += $", is dating you";
			charString = string.Concat(charString, contextString);
		}

		return charString;
	}

	public static string GetAllSkillLevel(bool showUi = false)
	{
		Dictionary<string,int> skills = BotHandler.Bot.PlayerInformation.SkillLevel(showUi);

		string contextString = "";
		foreach (var kvp in skills)
		{
			contextString = string.Concat(contextString, $"\n{kvp.Key}: {kvp.Value}");
		}

		return contextString;
	}
}