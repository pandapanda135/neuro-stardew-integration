using Microsoft.Xna.Framework;
using NeuroStardewValley.Source.ContextStrings;
using StardewBotFramework.Source.Modules.Pathfinding.Base;
using StardewValley;
using StardewValley.Buildings;
using Object = StardewValley.Object;

namespace NeuroStardewValley.Source.Utilities;

public static class TileUtilities
{
	public static int MaxX => Game1.currentLocation.Map.DisplayWidth / Game1.tileSize;
	public static int MaxY => Game1.currentLocation.Map.DisplayHeight / Game1.tileSize;
	public static bool IsValidTile(Point tile, out string reason,bool destructive = false,bool collisionMap = true)
	{
		if (tile.X > MaxX || tile.X < 0 ||
		    tile.Y > MaxY || tile.Y < 0)
		{
			reason = $"The value was either less than 0 or greater than the size of the map";
			return false;
		}

		if (collisionMap)
		{
			Main.Bot.Pathfinding.BuildCollisionMapInRadius(tile,3);
			if (Main.Bot.Pathfinding.IsBlocked(tile.X, tile.Y) && !destructive)
			{
				reason = "You gave a position that is blocked.";
				return false;
			}
		}
		reason = "";
		return true;
	}
	
	/// <summary>
	/// <see cref="Graph.IsInNeighbours"/>
	/// </summary>
	public static bool IsNeighbour(Point tile, Point neighbour,out int direction,int directions = 8)
	{
		bool result = Graph.IsInNeighbours(tile, neighbour, out var d,directions);
		direction = d;
		return result;
	}
	
	public static object? GetTileType(GameLocation location,Point tile)
	{
		if (location.Objects.ContainsKey(tile.ToVector2()) ||
		    location.furniture.Any(furn => furn.GetBoundingBox().Contains(new Rectangle(tile.X * 64,tile.Y * 64,64,64))))
		{
			location.Objects.TryGetValue(tile.ToVector2(),out var value);
			return value ?? location.furniture.Where(furn =>
				furn.GetBoundingBox().Contains(new Rectangle(tile.X * 64,tile.Y * 64,64,64))).ToList()[0];
		}

		foreach (var building in location.buildings.Where(building => building.occupiesTile(tile.ToVector2())))
		{
			return building;
		}
        
		foreach (var resourceClump in location.resourceClumps.Where(resourceClump => resourceClump.getBoundingBox()
			         .Contains(tile.ToVector2() * 64) || resourceClump.Tile == tile.ToVector2()))
		{
			return resourceClump;
		}

		if (location.terrainFeatures.TryGetValue(tile.ToVector2(), out var feature))
		{
			if (feature.getBoundingBox().Contains(tile.ToVector2() * 64) || feature.Tile == tile.ToVector2())
			{
				return feature;
			}
		}
        
		return null;
	}

	public static Object? GetClosestObjectId(string itemId, Vector2 startPoint)
	{
		PriorityQueue<Object,int> points = new();
		foreach (var kvp in TileContext.GetObjectsInLocation(Main.Bot._currentLocation))
		{
			if (kvp.Value is not Object obj || obj.ItemId != itemId) continue;
			points.Enqueue(obj,(int)Vector2.Distance(startPoint, obj.TileLocation));
		}

		points.TryDequeue(out Object? element, out _);
		return element;
	}

	/// <summary>
	/// If there is a building at this tile return it, else return null.
	/// </summary>
	/// <param name="tile">The tile location</param>
	public static Building? BuildingContainsTile(Point tile)
	{
		return Main.Bot._currentLocation.getBuildingAt(tile.ToVector2());
	}

	public static bool Actionable(Point tile)
	{
		return Main.Bot._currentLocation.isActionableTile(tile.X, tile.Y, Main.Bot._farmer);
	}
}