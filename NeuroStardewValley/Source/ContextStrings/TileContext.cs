using Microsoft.Xna.Framework;
using NeuroStardewValley.Debug;
using NeuroStardewValley.Source.Utilities;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.Objects;
using StardewValley.TerrainFeatures;
using Object = StardewValley.Object;

namespace NeuroStardewValley.Source.ContextStrings;

public static class TileContext
{
    public static readonly HashSet<Point> ActionableTiles = new();

    private static GameLocation? _location;
    private static WaterTiles.WaterTileData[,]? WaterTileData => _location?.waterTiles?.waterTiles;

    /// <summary>
    /// Get tiles in the specified location, to be used as context.
    /// </summary>
    /// <param name="location">The <see cref="GameLocation"/> you want to get the tiles of.</param>
    /// <param name="startTile">The Character you want to base the radius off</param>
    /// <param name="radius">the radius of tiles.</param>
    public static List<string> GetTilesInLocation(GameLocation location, Point? startTile = null,int radius = 0)
    {
        _location = location;
        string str = radius == 0 ? "" : $"closest {radius} ";
        List<string> tileList = new() {$"These are the {str}tiles in this location, they are sent in the format of X,Y with a \\n separating each tile." +
                                       " If a tile has an action you can try to use it with the interact_with_tile action." +
                                       " If a tile is \"block\" that means it has collisions."};

        Point tile = startTile ?? new Point();
        int minX = startTile is null ? 0 : tile.X - radius;
        int minY = startTile is null ? 0 : tile.Y - radius;
        
        int maxX = startTile is null ? location.Map.DisplayWidth / Game1.tileSize : tile.X + radius;
        int maxY = startTile is null ? location.Map.DisplayHeight / Game1.tileSize : tile.Y + radius;
        
        Rectangle rangeRect = new();
        if (startTile is not null)
        {
            rangeRect = new(Math.Clamp((tile.X - radius) * 64,0,maxX * 64),
                Math.Clamp((tile.Y - radius) * 64,0,maxY * 64),
                (radius * 2) * 64, (radius * 2) * 64); // this should reach to startTile.x + radius
        }

        Logger.Info($"map size X: {maxX}  maxY: {maxY}");

        for (int x = minX; x < maxX; x++)
        {
            for (int y = minY; y < maxY; y++)
            {
                Rectangle rect = new Rectangle(x * Game1.tileSize, y * Game1.tileSize, Game1.tileSize, Game1.tileSize);
                if (startTile is not null && !rangeRect.Intersects(rect)) continue; // is outside of range

                if (x < WaterTileData?.GetLength(0) && y < WaterTileData.GetLength(1) && WaterTileData[x, y].isWater)
                {
                    tileList.Add($"Water: {x},{y}");
                    continue;
                }
                object? obj = TileUtilities.GetTileType(location, new Point(x, y));
                
                if (!Main.Bot._currentLocation.isCollidingPosition(rect, Game1.viewport, true, 0, 
                        false, Main.Bot._farmer, true,false,false,true)
                    && obj is null)
                    continue;

                if (obj is null)
                {
                    string tileString = $"Block: {x},{y}";
                    if (location.isActionableTile(x, y, Main.Bot._farmer))
                    {
                        tileString += " has an action";
                        ActionableTiles.Add(new Point(x, y));
                        string[] action = ArgUtility.SplitBySpace(Main.Bot._currentLocation.doesTileHaveProperty(x, y, "Action", "Buildings"));
                        if (action.Length < 1)
                        {
                            tileList.Add(tileString);
                            continue;
                        }
                        
                        switch (action[0])
                        {
                            case "Dialogue":
                            case "Message":
                            case "MessageOnce":
                            case "NPCMessage":
                            case "MessageSpeech":
                                tileList.Add(tileString);
                                continue;
                            case "Letter":
                                tileList.Add(action[0]);
                                continue;
                        }
                        tileString += $": {string.Join(" ", action)}";
                    }
                    tileList.Add(tileString);
                    continue;
                }
                string? objectContext = GetTileContext(location, x, y);
                if (objectContext is null or "") continue;
                
                tileList.Add(objectContext);
            }
        }
        SentBuildings.Clear();
        SentFurniture.Clear();
        return tileList;
    }
    
    /// <summary>
    /// Gets the objects in either the location of the range specified.
    /// </summary>
    /// <returns>a Dictionary with the key as a <see cref="Point"/> and the value as the <see cref="object"/></returns>
    public static Dictionary<Point, object> GetObjectsInLocation(GameLocation location, Point? startTile = null, int radius = 0)
    {
        _location = location;
        Dictionary<Point,object> objectTiles = new();

        HashSet<Building> sentBuildings = new();
        HashSet<Furniture> sentFurniture = new();

        Point tile = startTile ?? new Point();
        int minX = startTile is null ? 0 : tile.X - radius;
        int minY = startTile is null ? 0 : tile.Y - radius;
        
        int maxX = startTile is null ? location.Map.DisplayWidth / Game1.tileSize : tile.X + radius;
        int maxY = startTile is null ? location.Map.DisplayHeight / Game1.tileSize : tile.Y + radius;
        
        Rectangle rangeRect = new();
        if (startTile is not null)
        {
            rangeRect = new(Math.Clamp((tile.X - radius) * 64,0,maxX * 64),
                Math.Clamp((tile.Y - radius) * 64,0,maxY * 64),
                (radius * 2) * 64, (radius * 2) * 64); // this should reach to startTile.x + radius
        }

        Logger.Info($"map size X: {maxX}  maxY: {maxY}");

        for (int x = minX; x < maxX; x++)
        {
            for (int y = minY; y < maxY; y++)
            {
                Rectangle rect = new Rectangle(x * Game1.tileSize, y * Game1.tileSize, Game1.tileSize, Game1.tileSize);
                if (startTile is not null && !rangeRect.Intersects(rect)) continue; // is outside of range

                if (x < WaterTileData?.GetLength(0) && y < WaterTileData.GetLength(1) && WaterTileData[x, y].isWater)
                {
                    objectTiles.Add(new Point(x,y),WaterTileData[x,y]);
                    continue;
                }
                object? obj = TileUtilities.GetTileType(location, new Point(x, y));
                
                if (obj is null)
                {
                    if (!location.isActionableTile(x,y,Main.Bot._farmer)) continue;
                    
                    ActionableTiles.Add(new Point(x, y));
                    string[] action = ArgUtility.SplitBySpace(Main.Bot._currentLocation.doesTileHaveProperty(x, y, "Action", "Buildings"));
                    if (action.Length < 1)
                    {
                        objectTiles.Add(new Point(x,y),"Action");
                        continue;
                    }
                    
                    Logger.Error($"action: {string.Join(" ",action)}");
                    switch (action[0])
                    {
                        
                        case "Dialogue":
                        case "Message":
                        case "MessageOnce":
                        case "NPCMessage":
                        case "MessageSpeech":
                        case "Letter":
                            objectTiles.Add(new Point(x,y),action[0]);
                            continue;
                    }
                    objectTiles.Add(new Point(x,y),"Action");
                    continue;
                }
                object? tileObj = TileUtilities.GetTileType(location, new Point(x, y));
                if (tileObj is null || (tileObj is Building building && !sentBuildings.Add(building)) 
                                    || (tileObj is Furniture furniture && !sentFurniture.Add(furniture))) continue;
                objectTiles.Add(new Point(x,y), tileObj);
            }
        }
        return objectTiles;
    }
    
    public static readonly HashSet<Building> SentBuildings = new();
    public static readonly HashSet<Furniture> SentFurniture = new();
    public static Dictionary<string, int> GetNameAmountInLocation(GameLocation location, Point? startTile = null, int radius = 0)
    {
        _location = location;
        var objects = GetObjectsInLocation(location, startTile, radius);

        Dictionary<string, int> amountOfObject = new();
        foreach (var kvp in objects)
        {
            string name = SimpleObjectName(kvp.Value);
            
            // ordinal should make it faster :prayge:
            if (name.Length == 0 || name.Contains("Error",StringComparison.Ordinal)) continue;
            if (!amountOfObject.TryAdd(name, 1)) amountOfObject[name]++;
        }
        return amountOfObject;
    }
    
    public static Dictionary<string, int> GetNameAmountInLocation(Dictionary<Point,object> objects)
    {
        Dictionary<string, int> amountOfObject = new();
        foreach (var kvp in objects)
        {
            string name = SimpleObjectName(kvp.Value);
            
            if (name.Length == 0 || name.Contains("Error",StringComparison.Ordinal)) continue;
            if (!amountOfObject.TryAdd(name, 1)) amountOfObject[name]++;
        }
        return amountOfObject;
    }

    /// <summary>
    /// Gets the object at the provided tile and returns a string for that type of object, if there is no object it will be an empty string.
    /// </summary>
    /// <param name="location"></param>
    /// <param name="x"></param>
    /// <param name="y"></param>
    /// <param name="simple">This will return a more simplified version of an object's context.</param>
    public static string? GetTileContext(GameLocation location, int x, int y, bool simple = false)
    {
        _location = location;
        object? obj = TileUtilities.GetTileType(location, new Point(x, y));
        if (obj is null) return null;
        return simple ? SimpleObjectName(obj) : GetObjectContext(obj,x,y);
    }
    
    /// <summary>
    /// The bare minimum name for an object, this is used in <see cref="GetNameAmountInLocation"/> 
    /// </summary>
    /// <returns>This will return either the object name or an empty string if the object is not valid.</returns>
    public static string SimpleObjectName(object obj)
    {
        string name = "";
        switch (obj)
        {
            case Object objs:
                name = objs.DisplayName;
                break;
            case ResourceClump clump:
                name = $"{StringUtilities.CorrectObjectName(clump)}";
                break;
            case LargeTerrainFeature largeTerrainFeature:
                name = $"{StringUtilities.CorrectObjectName(largeTerrainFeature)}";
                break;
            case TerrainFeature feature:
                name = $"{StringUtilities.CorrectObjectName(feature)}";
                break;
            case WaterTiles.WaterTileData:
                name = "Water";
                break;
            case Building building:
                name = StringUtilities.GetBuildingName(building);
                break;
            case string str:
                name = str;
                break;
        }

        return name;
    }

    private static string? GetObjectContext(object obj,int x,int y)
    {
        switch (obj)
        {
            case Chest chest:
                string name = chest.giftbox.Value ? "Gift-box" : "Chest";
                string tileString = $"{name} tile: {x},{y}";
                if (!chest.giftbox.Value) tileString += $", colour: {chest.getCategoryColor()}";
                return tileString;
            case Sign sign when sign.displayItem.Value is not null:
                return $"{sign.DisplayName}: {x},{y}. This holds a {sign.displayItem.Value.DisplayName}";
            case Object signObject when signObject.IsTextSign():
                return $"{signObject.DisplayName}: {x},{y}. This says: {signObject.SignText}";
            case Furniture furniture:
                if (!SentFurniture.Add(furniture)) return "";
                return $"Name: {furniture.DisplayName}, X: {furniture.GetBoundingBox().X / 64} Y: {furniture.GetBoundingBox().Y / 64}" +
                       $" Width: {furniture.GetBoundingBox().Width / 64} Height: {furniture.GetBoundingBox().Height / 64}";
            case Object objectValue:
                return $"{x},{y}, Name: {objectValue.DisplayName}{(objectValue.heldObject.Value is not null ? $", holding an {objectValue.heldObject.Value.DisplayName}" : "")}"; 
            case Building building:
                if (building.isActionableTile(x, y, Main.Bot._farmer))
                {
                    return $"{x},{y} has an action for the {StringUtilities.GetBuildingName(building)}";
                }
                if (!SentBuildings.Add(building)) return null; // we do this as buildings take up multiple tiles
                int buildX = building.tileX.Value;
                int buildY = building.tileY.Value;
                Point humanDoor = building.getPointForHumanDoor();
                string contextString = $"The top left tile of the {StringUtilities.GetBuildingName(building)} is: {buildX},{buildY}." +
                                    $" The bottom right is {buildX + building.tilesWide.Value}, {buildY + building.tilesHigh.Value}.";
                if (humanDoor != new Point(-1,-1)) contextString += $" The door is at {humanDoor.X},{humanDoor.Y}.";
                if (building.animalDoor.Value != new Point(-1, -1)) 
                    contextString += $" The animal door is at: {building.animalDoor.Value}.";
                return contextString;
            case ResourceClump resourceClump:
                // substring will always get "ResourceClump" as object name is not a part of modData
                string subStr = StringUtilities.CorrectObjectName(resourceClump);
                return $"{subStr} is at: {x},{y}";
            case TerrainFeature terrainFeature:
                switch (terrainFeature)
                {
                    case HoeDirt dirt:
                        string context = $"{dirt.Tile.X},{dirt.Tile.Y}: empty dirt";
                        if (dirt.crop is not null)
                        {
                            Item item = ItemRegistry.Create(dirt.crop.indexOfHarvest.Value);
                            context = $"{dirt.Tile.X},{dirt.Tile.Y}: {item.DisplayName} fully grown: {dirt.crop.fullyGrown.Value}";
                        }
                        return context;
                    default:
                        string substring = StringUtilities.CorrectObjectName(terrainFeature);
                        return $"{substring} is at: {x},{y}";
                }
            case string str:
                return $"{str}: {x},{y}";
        }
        
        return "";
    }

    public static string GetSpecifiedObjects(string simpleName,Point startTile,int radius, GameLocation location)
    {
        var objects = GetObjectsInLocation(location, startTile,radius);

        string str = "";
        foreach (var kvp in objects)
        {
            string objSimpleName = SimpleObjectName(kvp.Value);
            string? name = GetObjectContext(kvp.Value, kvp.Key.X, kvp.Key.Y);
            if (name is null || objSimpleName != simpleName) continue;
            str +=$"\n{name}";
        }
        SentFurniture.Clear();
        SentBuildings.Clear();

        return str;
    }

    public static string GetWarpTiles(GameLocation location,bool addBuildings = false)
    {
        location.TryGetMapProperty("Warp", out var warps);
        if (!addBuildings) return warps;

        warps += GetBuildingWarps(location);
        return warps;
    }

    private static string GetBuildingWarps(GameLocation location)
    {
        string warps = "";
        foreach (var building in location.buildings)
        {
            Rectangle door = building.getRectForHumanDoor();
            if (!building.HasIndoors() || building.getPointForHumanDoor() == new Point(-1,-1)) continue;
            
            door.Inflate(128,128); // adjust by two tiles as warp teleport location typically a few tiles away from entrance
            foreach (var warp in building.GetIndoors().warps.Where(warp => door.Contains(new Vector2(warp.TargetX,warp.TargetY) * 64))) 
            {
                warps += $" {warp.TargetX} {warp.TargetY} {building.GetIndoors().Name} {warp.X} {warp.Y}";
            }
        }
        return warps;
    }

    public static Dictionary<Point, string> GetWarpsAsPoint(string warps)
    {
        string[] warpExtracts = warps.Split(' ');
        Dictionary<Point, string> warpLocation = new();
        Logger.Info($"length: {warpExtracts.Length}   {warpExtracts[0]}");
        if (warpExtracts.Length < 5) return new();
        for (int i = 0; i < warpExtracts.Length; i += 5) // divide by five to only get tile locations
        {
            Point tile = new Point(int.Parse(warpExtracts[i]), int.Parse(warpExtracts[i + 1]));
            
            string locationName = warpExtracts[i + 2];
            warpLocation.Add(tile,locationName);
        }

        return warpLocation;
    }

    public static string GetWarpTilesString(string warpTiles)
    {
        var warpLocation = GetWarpsAsPoint(warpTiles);

        return warpLocation.Aggregate("", (current, kvp) => current + $"\n{kvp.Value}: {kvp.Key}");
    }

    public static List<string> GetWarpTilesStrings(string warpTiles)
    {
        var warpLocation = GetWarpsAsPoint(warpTiles);

        return warpLocation.Select(kvp => $"{kvp.Value}: {kvp.Key.X},{kvp.Key.Y}").ToList();
    }
}