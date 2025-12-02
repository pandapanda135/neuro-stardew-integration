using Microsoft.Xna.Framework;
using NeuroSDKCsharp.Actions;
using NeuroSDKCsharp.Json;
using NeuroSDKCsharp.Websocket;
using NeuroStardewValley.Debug;
using NeuroStardewValley.Source.ContextStrings;
using NeuroStardewValley.Source.RegisterActions;
using NeuroStardewValley.Source.Utilities;
using StardewBotFramework.Source.Modules.Pathfinding.Base;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.GameData.Buildings;
using StardewValley.Menus;
using StardewValley.Objects;
using StardewValley.TerrainFeatures;
using Object = StardewValley.Object;

namespace NeuroStardewValley.Source.Actions;

public class InteractAtTile : NeuroAction<Point>
{
	private static List<Point> ActionTiles => TileContext.ActionableTiles.ToList();
	public override string Name => "interact_at_tile";
	protected override string Description => "Interact with the object at the specified tile, these also include buildings whose tiles have an action.";
	protected override JsonSchema Schema => new()
	{
		Type = JsonSchemaType.Object,
		Required = new List<string> { "tile_x", "tile_y" },
		Properties = new Dictionary<string, JsonSchema>
		{
			["tile_x"] = QJS.Type(JsonSchemaType.Integer),
			["tile_y"] = QJS.Type(JsonSchemaType.Integer),
			["use_held_item"] = QJS.Type(JsonSchemaType.Boolean)
		}
	};
	protected override ExecutionResult Validate(ActionData actionData, out Point resultData)
	{
		int tileX = actionData.Data?.Value<int>("tile_x") ?? -1;
		int tileY = actionData.Data?.Value<int>("tile_y") ?? -1;
		bool useHeldItem = actionData.Data?.Value<bool>("use_held_item") ?? false;
		
		resultData = new();
		if (tileX is -1 || tileY is -1)
		{
			return ExecutionResult.Failure($"You have provided a null value for either tileX or TileY.");
		}

		Point point = new Point(tileX, tileY);
		object? o = GetLocationObjects(point);

		bool result;
		string reason;
		
		switch (o)
		{
			case TerrainFeature:
			case Object:
				result = ObjectValidation(tileX,tileY,useHeldItem,out reason);
				break;
			case Building:
				Logger.Info($"interacting with building");
				result = BuildingValidation(tileX,tileY,out reason);
				break;
			case Point:
				reason = $"Interacting with the tile at {tileX},{tileY}";
				result = true;
				break;
			default:
				reason = $"There is no available object at {point}";
				result = false;
				break;
		}

		if (!result)
		{
			return ExecutionResult.Failure(reason);
		}
		
		resultData = point;
		return ExecutionResult.Success(reason);
	}
	private bool _useHeld;
	protected override void Execute(Point resultData)
	{
		object? o = GetLocationObjects(resultData);
		
		switch (o)
		{
			case TerrainFeature:
			case Object:
				ObjectExecution(o);
				break;
			case Building building:
				Logger.Info($"building execution");
				BuildingExecution(building, resultData);
				break;
			// action tile
			case Point point:
				string[] action = ArgUtility.SplitBySpace(Main.Bot._currentLocation.doesTileHaveProperty(point.X, point.Y, "Action", "Buildings"));
				Logger.Info($"interacting with action tile: {string.Join(" ", action)}");
				Main.Bot.ActionTiles.DoActionTile(point);
				break;
		}
		
		// In case whatever is at the tile leads to getting teleported or making menu
		GameLocation oldLocation = Main.Bot._currentLocation;
		IClickableMenu oldMenu = Game1.activeClickableMenu;
		DelayedAction.functionAfterDelay(() =>
		{
			if (!Main.Bot._currentLocation.Equals(oldLocation) ||
			    (Game1.activeClickableMenu is not null && !Game1.activeClickableMenu.Equals(oldMenu))) return;
			RegisterMainActions.RegisterPostAction();
		}, 1000);
	}
	
	private static object? GetLocationObjects(Point point)
	{
		Main.Bot._currentLocation.terrainFeatures.TryGetValue(point.ToVector2(),out var feature);
		object? o = null;
		if (feature is not null) o = feature;

		foreach (var kvp in Main.Bot._currentLocation.Objects.Pairs)
		{
			if (!kvp.Value.GetBoundingBox().Contains(point.ToVector2() * 64)) continue;
				
			o = kvp.Value;
		}
		
		foreach (var furniture in Main.Bot._currentLocation.furniture)
		{
			if (!furniture.GetBoundingBox().Contains(point.ToVector2() * 64)) continue;
				
			o = furniture;
		}
		
		Building build = Main.Bot._currentLocation.buildings.FirstOrDefault(bu => BuildingActionAtTile(bu,point)
			,new Building());
		if (Main.Bot._currentLocation.buildings.Contains(build))
		{
			o = build;
		}
		
		// need to check if object is within boundary of building and is actionable
		if (ActionTiles.Any(tile => tile != point) && o is not Building ||
		    (o is Building b && !b.isActionableTile(point.X, point.Y, Main.Bot._farmer)))
		{
			o = point;
		}
		
		return o;
	}

	private bool ObjectValidation(int tileX, int tileY, bool useHeld, out string reason)
	{
		if (useHeld && Main.Bot._farmer.ActiveItem is null)
		{
			reason = $"You are not holding anything so you cannot use the held item.";
			return false;
		}

		Point point = new Point(tileX, tileY);
		object? obj = TileUtilities.GetTileType(Main.Bot._currentLocation, point);
		if (obj is null or Building)
		{
			reason = $"There is no object valid at the tile you provided.";
			return false;
		}
			
		if (!RangeCheck.InRange(point))
		{
			reason = $"This object is not within range of you.";
			return false;
		}

		_useHeld = useHeld;
		reason = $"Interacting with the object at {point}";
		return true;
	}

	private void ObjectExecution(object resultData)
	{
		if (resultData is null or Building)
		{
			return;
		}

		Logger.Info($"interacting with {resultData}   {resultData}");
		switch (resultData)
		{
			case Furniture furniture:
				Main.Bot.Input.ChangeMousePosition((int)furniture.TileLocation.X,(int)furniture.TileLocation.Y);
				Main.Bot.Input.SetButton(SButton.MouseLeft,true);
				furniture.checkForAction(Main.Bot._farmer);
				break;
			case Object obj:
				if (obj.GetMachineData() is not null && (obj.heldObject.Value is null || 
				                                         (obj.GetMachineData().AllowLoadWhenFull && obj.heldObject.Value is not null)) && _useHeld)
				{
					Main.Bot.Player.AddItemToObject(obj, Main.Bot._farmer.ActiveItem);
					break;
				}

				Main.Bot.ObjectInteraction.InteractWithObject(obj);
				break;
			case TerrainFeature feature:
				if (_useHeld)
				{
					Graph.IsInNeighbours(Main.Bot._farmer.TilePoint, feature.Tile.ToPoint(), out var direction,4);
					Main.Bot.Tool.UseTool(direction);
				}
				Main.Bot.ObjectInteraction.InteractWithTerrainFeature(feature, feature.Tile);
				break;
			default:
				Logger.Error($"InteractWithObject execute result data was not a valid class");
				break;
		}
	}

	private static BuildingActionTile? GetBuildingTile(Building building,Point point) => 
		building.GetData().ActionTiles.FirstOrDefault(tile => tile.Tile.X + building.tileX.Value == point.X &&
		                                                      tile.Tile.Y + building.tileY.Value == point.Y);
	
	private static bool BuildingValidation(int tileX, int tileY, out string reason)
	{
		Point point = new Point(tileX, tileY);
		Building building = Main.Bot._currentLocation.buildings.FirstOrDefault(b => BuildingActionAtTile(b,point),new Building());
		if (!Main.Bot._currentLocation.buildings.Contains(building))
		{
			reason = $"There is no building at {tileX},{tileY}";
			return false;
		}

		if (building is null)
		{
			reason = $"There is no building at {tileX},{tileY}";
			return false;
		}

		foreach (var tiles in building.GetData().ActionTiles)
		{
			Logger.Info($"building tiles: {tiles.Tile}");
		}

		BuildingActionTile? tile = GetBuildingTile(building, point);
			
		if (tile is null)
		{
			reason = $"There is no action for {StringUtilities.GetBuildingName(building)} at {tileX},{tileY}";
			return false;
		}
		
		reason = $"Interacting with {StringUtilities.GetBuildingName(building)}";
		return true;
	}

	private static void BuildingExecution(Building building,Point tile)
	{
		var buildingTile = GetBuildingTile(building, tile);
		if (buildingTile is null)
		{
			Logger.Info($"building tile is null");
			return;
		}
		Logger.Info($"building tile: {buildingTile.Tile}  {buildingTile.Action}");
		Main.Bot.Building.DoBuildingAction(building, buildingTile.Tile.ToVector2());
	}

	private static bool BuildingActionAtTile(Building building,Point tile)
	{
		Point adjustedTile = tile - TileUtilities.BuildingTile(building);
		List<BuildingActionTile> tiles = building.GetData().ActionTiles
			.Where(buildingTile => buildingTile.Tile == adjustedTile).ToList();
		return tiles.Count >= 1;
	}
}