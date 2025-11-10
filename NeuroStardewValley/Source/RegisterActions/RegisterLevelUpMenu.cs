using NeuroSDKCsharp.Actions;
using NeuroSDKCsharp.Messages.Outgoing;
using NeuroStardewValley.Debug;
using NeuroStardewValley.Source.Actions;
using NeuroStardewValley.Source.Utilities;
using StardewModdingAPI.Enums;
using StardewValley;
using StardewValley.Menus;

namespace NeuroStardewValley.Source.RegisterActions;

public static class RegisterLevelUpMenu
{
	public static void GetSkillContext()
	{
		if (Game1.activeClickableMenu is not LevelUpMenu menu) return;
		
		string skillContext = "The professions you can pick, The first sent profession will always be \"Left\" with the other being \"Right\": ";
		if (menu.leftProfession is not null || menu.rightProfession is not null)
		{
			foreach (var profession in Main.Bot.EndDaySkillMenu.ProfessionsToChoose)
			{
				skillContext += "\n";
				foreach (string desc in LevelUpMenu.getProfessionDescription(profession))
				{
					skillContext += $"{desc} ";
				}
			}
			
			ActionWindow window = ActionWindow.Create(Main.GameInstance);
			window.AddAction(new EndDayActions.PickProfession());
			window.SetForce(1, "You have ended the day and have to select a profession for one of your skills", skillContext);
			window.Register();
			return;
		}
		
		skillContext = "Skills that have been changed this day: ";
		List<string> info = menu.getExtraInfoForLevel(Main.Bot.EndDaySkillMenu.CurrentSkill, Main.Bot.EndDaySkillMenu.CurrentLevel);
		foreach (var str in info)
		{
			skillContext += str;
		}
		Context.Send(skillContext);
		// function after delay doesn't work here
		Task.Run(async () =>
		{
			await Utils.WaitForSeconds(3);
			Main.Bot.EndDaySkillMenu.SelectOkButton();
		});
	}
}