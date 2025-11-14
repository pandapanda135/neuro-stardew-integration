using Microsoft.Xna.Framework;
using NeuroSDKCsharp.Actions;
using NeuroSDKCsharp.Json;
using NeuroSDKCsharp.Websocket;
using NeuroStardewValley.Debug;
using NeuroStardewValley.Source.RegisterActions;
using NeuroStardewValley.Source.Utilities;
using StardewBotFramework.Source.Modules.Pathfinding.Base;
using StardewValley;
using StardewValley.Quests;
using Object = StardewValley.Object;

namespace NeuroStardewValley.Source.Actions;

public class GetQuestItem : NeuroAction<Point>
{
	private static List<KeyValuePair<Vector2,Object>> ValidObjects => Main.Bot._currentLocation.overlayObjects.Where(kvp =>
		kvp.Value.questItem.Value && Main.Bot._farmer.questLog.Any(quest => quest.id == kvp.Value.questId)).ToList();
	private static string SchemaKey => Main.Config.UseQuestTitleInsteadOfItemName ? "title" : "item";
	
	public override string Name => "get_quest_item";
	protected override string Description => "This will allow for you to pick up an item that is required for a quest.";
	protected override JsonSchema Schema => new()
	{
		Type = JsonSchemaType.Object,
		Required = new List<string> { SchemaKey },
		Properties = new Dictionary<string, JsonSchema>
		{
			[SchemaKey] = QJS.Enum(GetSchema().Result),
		}
	};
	protected override ExecutionResult Validate(ActionData actionData, out Point resultData)
	{
		string? itemName = actionData.Data?.Value<string>(SchemaKey);

		resultData = new();
		if (itemName is null) return ExecutionResult.Failure($"You must provide a value");

		int index;
		if (!Main.Config.UseQuestTitleInsteadOfItemName)
		{
			index = ValidObjects.Select(kvp => kvp.Value.DisplayName).ToList().IndexOf(itemName);
			if (index == -1)
			{
				return ExecutionResult.Failure($"The item you provided is not valid.");
			}
		}
		else
		{
			Quest quest = Main.Bot._farmer.questLog.Where(quest => quest.GetName() == itemName).ToList()[0]; 
			var kvp	 = ValidObjects.Where(kvp => kvp.Value.questId.Value == quest.id.Value).ToList()[0];
			index = ValidObjects.Select(k => k.Key).ToList().IndexOf(kvp.Key);
			if (index == -1) return ExecutionResult.Failure($"The quest you provided is not a valid quest");
			
			Object obj = ValidObjects[index].Value;
			if (Main.Bot._farmer.questLog.All(q => q.id.Value != obj.questId.Value))
			{
				return ExecutionResult.Failure($"The quest you provided is not valid.");
			}
		}
		
		resultData = ValidObjects[index].Value.TileLocation.ToPoint();
		if (!Main.Bot._currentLocation.overlayObjects.TryGetValue(resultData.ToVector2(), out Object o))
		{
			return ExecutionResult.Failure($"The object you provided could not be found, this is an issue with the integration.");
		}
		return ExecutionResult.Success($"Grabbing {o.DisplayName}");
	}

	protected override async void Execute(Point resultData)
	{
		try
		{
			await TaskDispatcher.SwitchToMainThread();
			// shouldn't happen probably need to check though
			if (!Main.Bot._currentLocation.overlayObjects.TryGetValue(resultData.ToVector2(), out Object? obj))
			{
				RegisterMainActions.RegisterPostAction();
				return;
			}
			
			Point point = obj.TileLocation.ToPoint();
			await Main.Bot.Pathfinding.Goto(new Goal.GetToTile(point.X, point.Y),true);
			await Util.WaitForSeconds(0.1);

			if (!Graph.IsInNeighbours(Main.Bot._farmer.TilePoint, point, out var direction, 4))
			{
				RegisterMainActions.RegisterPostAction();	
				return;
			}
			Main.Bot.Player.ChangeFacingDirection(direction);
			await Util.WaitForSeconds(0.25);
			
			Main.Bot.ObjectInteraction.InteractWithQuestObject(obj);
			await Util.WaitForSeconds(0.5);
			// most open up a dialogue box
			if (Game1.activeClickableMenu is not null) return;
			RegisterMainActions.RegisterPostAction();
		}
		catch (Exception e)
		{
			Logger.Error($"{e}");
			await TaskDispatcher.SwitchToMainThread();
			RegisterMainActions.RegisterPostAction();
		}
	}

	public static async Task<List<string>> GetSchema()
	{
		List<string> names = new();
		Main.Bot.Pathfinding.BuildCollisionMap();
		foreach (var kvp in ValidObjects)
		{
			Point point = kvp.Key.ToPoint();
			var path = await Main.Bot.Pathfinding.GetPathTo(new Goal.GetToTile(point.X, point.Y), 1000,true,false);
			// check if pathfinding is empty as the farmer is next to the object
			if (!path.Any() && !Graph.IsInNeighbours(Main.Bot._farmer.TilePoint, point, out _, 4)) continue;
			var pathNodes = path.Reverse().ToList();
			
			// check if the final node is a neighbour
			if (path.Any() && !Graph.IsInNeighbours(pathNodes[0].VectorLocation, point, out _, 4)) continue;
			if (Main.Config.UseQuestTitleInsteadOfItemName)
			{
				var quests = Main.Bot._farmer.questLog.Where(quest => quest.id.Value == kvp.Value.questId.Value).ToList();
				names.Add(quests[0].GetName());
				continue;
			}
			
			names.Add(kvp.Value.DisplayName);
		}

		return names;
	}
}