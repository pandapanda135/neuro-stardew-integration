using NeuroSDKCsharp.Actions;
using NeuroSDKCsharp.Json;
using NeuroSDKCsharp.Websocket;
using NeuroStardewValley.Source.EventMethods;
using NeuroStardewValley.Source.Utilities;
using NeuroStardewValley.Debug;
using StardewValley;

namespace NeuroStardewValley.Source.Actions;

public class WaitForTime : NeuroAction<int>
{
	// passage of time from wiki https://stardewvalleywiki.com/Day_Cycle
	private const double Standard = 0.7;
	// in-game its actually 14 seconds I'm just going to go with the wiki
	private const double Indoor = 0.9;
	public override string Name => "wait_for_time";
	protected override string Description =>
		$"Wait until a specified in-game time, each 10 in-game minutes lasts for {GetTime() * 10} seconds. You should" +
		$" send the time as a string, in the format hour:minute.";
	protected override JsonSchema Schema => new()
	{
		Type = JsonSchemaType.Object,
		Required = new List<string> { "time" },
		Properties = new Dictionary<string, JsonSchema>
		{
			["time"] = QJS.Type(JsonSchemaType.String)
		}
	};
	protected override ExecutionResult Validate(ActionData actionData, out int resultData)
	{
		string? timeStr = actionData.Data?.Value<string>("time");

		resultData = -1;
		if (string.IsNullOrEmpty(timeStr))
		{
			return ExecutionResult.Failure($"The time you provided was not in the correct format.");
		}

		int time = StringUtilities.TimeStringToInt(timeStr);

		Logger.Info($"time: {time}");
		if (time == -1)
		{
			return ExecutionResult.Failure($"The time you provided was not in the correct format.");
		}

		if (!Game1.shouldTimePass())
		{
			return ExecutionResult.Failure($"You cannot use this as time is currently pause and will not advance");
		}
		
		if (time < Game1.timeOfDay) return ExecutionResult.Failure($"The time you provided is earlier than the current time.");
		resultData = time;
		return ExecutionResult.Success($"Waiting until {timeStr}");
	}

	protected override void Execute(int resultData)
	{
		LessImportantEvents.WaitingTime = resultData;
	}

	private static double GetTime() => BotHandler.CurrentLocation.IsOutdoors ? Standard : Indoor;
}