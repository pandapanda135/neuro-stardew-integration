using Microsoft.Xna.Framework;
using NeuroSDKCsharp.Actions;
using NeuroSDKCsharp.Json;
using NeuroSDKCsharp.Websocket;
using NeuroStardewValley.Debug;
using NeuroStardewValley.Source.RegisterActions;
using NeuroStardewValley.Source.Utilities;
using StardewBotFramework.Source.Modules.Pathfinding.Base;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Objects;
using StardewValley.Quests;
using Object = StardewValley.Object;

namespace NeuroStardewValley.Source.Actions;

public class GetQuestItem : NeuroAction<Point>
{
	private static List<KeyValuePair<Vector2, Object>> ValidObjects
	{
		get
		{
			var overlayObjects = BotHandler.CurrentLocation.overlayObjects.Where(kvp =>
				kvp.Value.questItem.Value && BotHandler.Farmer.questLog.Any(quest => quest.id == kvp.Value.questId)).ToList();
			
			if (BotHandler.CurrentLocation is not Cabin and not FarmHouse) return overlayObjects;
		
			List<KeyValuePair<Vector2, Object>> objs = new();
			foreach (var kvp in BotHandler.CurrentLocation.Objects.Pairs)
			{
				if (kvp.Value is not Chest chest) continue;
				if (!chest.giftboxIsStarterGift.Value) continue;
				
				objs.Add(kvp);
			}

			overlayObjects.AddRange(objs);
			return overlayObjects;
		}
	}
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
			var quests = BotHandler.Farmer.questLog.Where(quest => quest.GetName() == itemName).ToList();
			if (!quests.Any())
			{
				var titleObject = ObjectFromFakeTitle(itemName);
				if (titleObject is null)
				{
					return ExecutionResult.Failure($"You provided an invalid name.");
				}

				var objs = ValidObjects.Where(kvp => kvp.Value == titleObject).ToList();
				if (!objs.Any()) return ExecutionResult.Failure($"You provided an invalid name.");
				// we check vectors as checking kvp is inefficient
				index = ValidObjects.Select(kvp => kvp.Key).ToList().IndexOf(objs[0].Key);
			}
			else
			{
				Quest quest = quests[0]; 
				var kvp	 = ValidObjects.Where(kvp => kvp.Value.questId.Value == quest.id.Value).ToList()[0];
				index = ValidObjects.Select(k => k.Key).ToList().IndexOf(kvp.Key);
				if (index == -1) return ExecutionResult.Failure($"The quest you provided is not a valid quest");
			
				Object obj = ValidObjects[index].Value;
				if (BotHandler.Farmer.questLog.All(q => q.id.Value != obj.questId.Value))
					return ExecutionResult.Failure($"The quest you provided is not valid.");
			}
		}
		
		resultData = ValidObjects[index].Value.TileLocation.ToPoint();
		if (!CheckIfInLocation(resultData.ToVector2(),out var o) || o is null) 
			return ExecutionResult.Failure($"The object you provided could not be found, this is an issue with the integration.");
		return ExecutionResult.Success($"Grabbing {o.DisplayName}");
	}

	protected override async void Execute(Point resultData)
	{
		try
		{
			await TaskDispatcher.SwitchToMainThread();
			// shouldn't happen probably need to check though
			if (!CheckIfInLocation(resultData.ToVector2(),out var obj) || obj is null)
			{
				Logger.Error($"can't find item in location");
				RegisterMainActions.RegisterPostAction();
				return;
			}
			
			Point point = obj.TileLocation.ToPoint();
			await BotHandler.Bot.Pathfinding.Goto(new Goal.GetToTile(point.X, point.Y),true);
			await Util.WaitForSeconds(0.1);

			if (!Graph.IsInNeighbours(BotHandler.Farmer.TilePoint, point, out var direction, 4))
			{
				RegisterMainActions.RegisterPostAction();	
				return;
			}
			BotHandler.Bot.Player.ChangeFacingDirection(direction);
			await Util.WaitForSeconds(0.5,false);

			// This is here to, hopefully, fix issues with not picking up the object
			// might cause concurrency issues, couldn't find any rn :)
			while (CheckIfInLocation(resultData.ToVector2(),out _, obj))
			{
				BotHandler.Bot.ObjectInteraction.InteractWithQuestObject(obj);
				await Util.WaitForSeconds(0.25,false);
			}
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

	/// <summary>
	/// Check if there is an object at the point.
	/// </summary>
	/// <param name="point"></param>
	/// <param name="tileObj"></param>
	/// <param name="checkObj">If this is specified, obj will only be set if there is an object that is of the same item id as this item at the specified point.</param>
	/// <returns>If you specify checkObj it will return if there is an object that is valid, else it
	/// will just check if there is either an overlay object or object in this location at that tile.</returns>
	private static bool CheckIfInLocation(Vector2 point, out Object? tileObj, Object? checkObj = null)
	{
		bool overlay = BotHandler.CurrentLocation.overlayObjects.TryGetValue(point, out var overObj);
		bool standard = BotHandler.CurrentLocation.Objects.TryGetValue(point, out var standardObj);
		if (checkObj is null)
		{
			tileObj = standardObj ?? overObj;
			return overlay || standard;
		}

		tileObj = null;
		if (overObj?.ItemId == checkObj.ItemId)
		{
			tileObj = overObj;
		}
		
		if (standardObj?.ItemId == checkObj.ItemId)
		{
			tileObj = standardObj;
		}

		return tileObj is not null;
	}

	public static async Task<List<string>> GetSchema()
	{
		List<string> names = new();
		BotHandler.Bot.Pathfinding.BuildCollisionMap();
		foreach (var kvp in ValidObjects)
		{
			Point point = kvp.Key.ToPoint();
			var path = await BotHandler.Bot.Pathfinding.GetPathTo(new Goal.GetToTile(point.X, point.Y), 1000,true,false);
			// check if pathfinding is empty as the farmer is next to the object
			if (!path.Any() && !Graph.IsInNeighbours(BotHandler.Farmer.TilePoint, point, out _, 4)) continue;
			var pathNodes = path.Reverse().ToList();
			
			// check if the final node is a neighbour
			if (path.Any() && !Graph.IsInNeighbours(pathNodes[0].VectorLocation, point, out _, 4)) continue;
			if (Main.Config.UseQuestTitleInsteadOfItemName)
			{
				var quests = BotHandler.Farmer.questLog.Where(quest => quest.id.Value == kvp.Value.questId.Value).ToList();
				if (!quests.Any())
				{
					var title = GetFakeQuestTitles(kvp.Value);
					if (title == "") continue;
					
					names.Add(title);
					continue;
				}
				names.Add(quests[0].GetName());
				continue;
			}
			
			names.Add(kvp.Value.DisplayName);
		}

		Logger.Info($"first end of get schema in quest items");
		return names;
	}
	private static string GetFakeQuestTitles(Object obj)
	{
		Logger.Info($"{obj.DisplayName}    {obj.ItemId}    {obj.QualifiedItemId}");
		if (obj is Chest chest && chest.giftboxIsStarterGift.Value)
		{
			return "Grab starter seeds";
		}

		return "";
	}

	private static Object? ObjectFromFakeTitle(string title)
	{
		foreach (var kvp in ValidObjects)
		{
			if (GetFakeQuestTitles(kvp.Value) != title) continue;
			return kvp.Value;
		}
		
		return null;
	}
}