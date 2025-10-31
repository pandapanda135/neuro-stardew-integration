using NeuroSDKCsharp.Actions;
using NeuroStardewValley.Debug;
using NeuroStardewValley.Source.Actions;
using NeuroStardewValley.Source.Utilities;
using StardewValley;
using StardewValley.Menus;

namespace NeuroStardewValley.Source.RegisterActions;

public static class RegisterDialogueActions
{
	public static void RegisterActions()
	{
		Logger.Info($"running dialogue register actions");
		ActionWindow window = ActionWindow.Create(Main.GameInstance);
		if (Game1.activeClickableMenu is DialogueBox dialogueBox)
		{
			string stateString;
			Logger.Info($"dialoguebox response length: {dialogueBox.responses.Length}    getCurrentDialogue: {string.Concat(dialogueBox.dialogues)}");
			if (dialogueBox.responses.Length > 0)
			{
				window.AddAction(new DialogueActions.DialogueResponse());				
				stateString = $"These are the possible responses:\n";
				for (int i = 0; i < dialogueBox.responses.Length; i++)
				{
					stateString += $"{i}: {dialogueBox.responses[i].responseText} \n";	
				}
			}
			else
			{
				window.AddAction(new DialogueActions.AdvanceDialogue());
				if (dialogueBox.characterDialogue is not null)
				{
					stateString = $"{dialogueBox.characterDialogue.speaker.Name} is talking to you, they said: {dialogueBox.characterDialogue.getCurrentDialogue()}";
					if (DialogueUtils.ReplaceEmotionSymbols(dialogueBox.characterDialogue.CurrentEmotion) != "")
					{
						stateString +=
							$"\n They look {DialogueUtils.ReplaceEmotionSymbols(dialogueBox.characterDialogue.CurrentEmotion)}";
					}
				}
				else
				{
					stateString = $"The current dialogue is {dialogueBox.getCurrentString()}";
				}
				
				stateString = $"This is the dialogue: \"{stateString}\"";
			}

			int delayMs = (int)Math.Ceiling(0.04f * dialogueBox.getCurrentString().Length * 1000f);
			if (delayMs < 0) delayMs = 0;
			DelayedAction.functionAfterDelay(() =>
			{
				string query = "You are currently engaging in dialogue.";
				if (Main.Bot.Dialogue.CurrentDialogueBox?.characterDialogue is not null)
				{
					query += $" With {Main.Bot.Dialogue.CurrentDialogueBox?.characterDialogue.speaker.displayName}";
				}
				window.SetForce(0, query, stateString);
				window.Register();
			}, delayMs + 2000); // add two for reading if typewriter
		}
		else
		{
			Logger.Error($"Current active clickable menu is not a DialogueBox");
		}
	}
}