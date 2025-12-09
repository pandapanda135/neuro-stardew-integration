using System.Collections;
using System.Dynamic;
using System.Reflection.Metadata;
using NeuroSDKCsharp.Actions;
using NeuroSDKCsharp.Json;
using NeuroSDKCsharp.Utilities;
using NeuroSDKCsharp.Websocket;
using StardewValley;
using StardewValley.Menus;

namespace NeuroStardewValley.Source.Actions;

public static class EndDayActions
{
	public class PickProfession : NeuroAction<bool>
	{
		public override string Name => "select_profession";

		protected override string Description =>
			"When you get to level 5 or 10 in a skill you can select a profession, these will give permanent buffs.";

		private IEnumerable<string> Options()
		{
			List<string> choices = new()
			{
				"Left",
				"Right"
			};
			return choices;
		}

		protected override JsonSchema? Schema => new JsonSchema()
		{
			Type = JsonSchemaType.Object,
			Required = new List<string> { "profession" },
			Properties = new Dictionary<string, JsonSchema>
			{
				["profession"] = QJS.Enum(Options()),
			}
		};
		protected override ExecutionResult Validate(ActionData actionData, out bool resultData)
		{
			string? action = actionData.Data?.Value<string>("profession");

			if (!Options().Contains(action))
			{
				resultData = false;
				return ExecutionResult.Failure($"You have passed an invalid value in profession.");
			}

			resultData = action == "Left";

			return ExecutionResult.Success();
		}

		protected override void Execute(bool resultData)
		{
			BotHandler.Bot.EndDaySkillMenu.SelectPerk(resultData);
		}
	}
}