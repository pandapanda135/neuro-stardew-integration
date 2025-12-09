using NeuroSDKCsharp.Actions;
using NeuroSDKCsharp.Json;
using NeuroSDKCsharp.Websocket;
using NeuroStardewValley.Source.RegisterActions;
using NeuroStardewValley.Source.Utilities;
using StardewValley;
using StardewValley.Menus;

namespace NeuroStardewValley.Source.Actions;

public static class DialogueActions
{
	public class AdvanceDialogue : NeuroAction
	{
		public override string Name => "advance_dialogue";
		protected override string Description => "This will advance the current dialogue.";
		protected override JsonSchema Schema => new();
		protected override ExecutionResult Validate(ActionData actionData)
		{
			if (Game1.activeClickableMenu is not DialogueBox dialogueBox || dialogueBox.responses.Length > 0)
				return ExecutionResult.ModFailure($"There is no dialogue currently, this is most likely an issue with the mod.");
			
			if (BotHandler.Bot.Dialogue.CurrentDialogueBox is null) return ExecutionResult.Failure(string.Format(ResultStrings.ModVarFailure,$"BotHandler.Bot.Dialogue.CurrentDialogueBox"));
			
			if (BotHandler.Bot.Dialogue.CurrentDialogueBox.transitioning || BotHandler.Bot.Dialogue.CurrentDialogueBox.safetyTimer > 0
				|| BotHandler.Bot.Dialogue.CurrentDialogueBox.characterIndexInDialogue < BotHandler.Bot.Dialogue.CurrentDialogueBox.getCurrentString().Length - 1)
			{
				return ExecutionResult.Failure($"You have tried to run this action before the dialogue has finished appearing.");
			}
			
			return ExecutionResult.Success();
		}

		protected override void Execute()
		{
			bool sendAction = BotHandler.Bot.Dialogue.CurrentDialogue?.dialogues.Count > 1 && !BotHandler.Bot.Dialogue.CurrentDialogue.isOnFinalDialogue();
			BotHandler.Bot.Dialogue.AdvanceDialogBox(out _);
			if (sendAction) RegisterDialogueActions.RegisterActions();
		}
	}
	
	public class DialogueResponse : NeuroAction<int>
	{
		public override string Name => "dialogue_reply";
		protected override string Description => "Reply to the current dialogue question you have been asked.";
		protected override JsonSchema Schema => new()
		{
			Type = JsonSchemaType.Object,
			Required = new List<string> { "response" },
			Properties = new Dictionary<string, JsonSchema>
			{
				["response"] = QJS.Enum(Enumerable.Range(0, BotHandler.Bot.Dialogue.PossibleResponses().Length))
			}
		};
		protected override ExecutionResult Validate(ActionData actionData, out int resultData)
		{
			int? selectedResponse = actionData.Data?.Value<int>("response");

			resultData = -1;
			if (selectedResponse is null)
			{
				return ExecutionResult.Failure($"You gave a null value to response");
			}

			if (BotHandler.Bot.Dialogue.PossibleResponses().Length < 1)
			{
				return ExecutionResult.ModFailure($"There are no possible responses as of right now. This is most likely a mod issue");
			}
			
			int possibleResponsesAmount = BotHandler.Bot.Dialogue.PossibleResponses().Length;
			if (!Enumerable.Range(0, possibleResponsesAmount).ToList().Contains(selectedResponse.Value))
			{
				return ExecutionResult.Failure($"You have given a value that is No a valid response index.");
			}
			
			if (BotHandler.Bot.Dialogue.CurrentDialogueBox is null) return ExecutionResult.Failure(string.Format(ResultStrings.ModVarFailure,$"BotHandler.Bot.Dialogue.CurrentDialogueBox"));
			
			if (BotHandler.Bot.Dialogue.CurrentDialogueBox.transitioning || BotHandler.Bot.Dialogue.CurrentDialogueBox.safetyTimer > 0
				|| BotHandler.Bot.Dialogue.CurrentDialogueBox.characterIndexInDialogue < BotHandler.Bot.Dialogue.CurrentDialogueBox.getCurrentString().Length - 1)
			{
				return ExecutionResult.Failure($"You have tried to run this action before the dialogue has finished appearing.");
			}

			resultData = selectedResponse.Value;
			return ExecutionResult.Success($"You have replied: {BotHandler.Bot.Dialogue.PossibleResponses()[selectedResponse.Value].responseText}");
		}

		protected override void Execute(int resultData)
		{
			BotHandler.Bot.Dialogue.ChooseResponse(BotHandler.Bot.Dialogue.PossibleResponses()[resultData]);
		}
	}
}