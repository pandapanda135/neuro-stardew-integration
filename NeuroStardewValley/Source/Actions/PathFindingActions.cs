using Microsoft.Xna.Framework;
using NeuroSDKCsharp.Actions;
using NeuroSDKCsharp.Json;
using NeuroSDKCsharp.Websocket;
using NeuroStardewValley.Debug;
using NeuroStardewValley.Source.ContextStrings;
using NeuroStardewValley.Source.RegisterActions;
using NeuroStardewValley.Source.Utilities;
using StardewBotFramework.Source.Modules.Pathfinding.Algorithms;
using StardewBotFramework.Source.Modules.Pathfinding.Base;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.Monsters;

namespace NeuroStardewValley.Source.Actions;

public static class PathFindingActions
{
    public class Pathfinding : NeuroAction<Goal?>
    {
        private bool _destructive;
        public override string Name => "move_character";
        protected override string Description =>
            "This will move the character to the provided tile location in the world.";
        protected override JsonSchema Schema => new()
        {
            Type = JsonSchemaType.Object,
            Required = new List<string> { "x_tile", "y_tile" },
            Properties = new Dictionary<string, JsonSchema>
            {
                ["x_tile"] = QJS.Type(JsonSchemaType.Integer),
                ["y_tile"] = QJS.Type(JsonSchemaType.Integer),
                ["destructive"] = QJS.Type(JsonSchemaType.Boolean)
            }
        };

        protected override ExecutionResult Validate(ActionData actionData, out Goal? goal)
        {
            string? xStr = actionData.Data?.Value<string>("x_tile");
            string? yStr = actionData.Data?.Value<string>("y_tile");
            bool? destructive = actionData.Data?.Value<bool>("destructive");

            Logger.Info($"data: {xStr}  yData: {yStr}");

            if (xStr is null || yStr is null || destructive is null)
            {
                Logger.Error($"data or yData is null");
                goal = new Goal();
                return ExecutionResult.Failure($"A value you gave was null");
            }

            if (!int.TryParse(xStr, out int x) || !int.TryParse(yStr, out int y))
            {
                Logger.Error("Invalid or missing x/y position values.");
                goal = null;
                return ExecutionResult.Failure("Invalid or missing x/y position values.");
            }

            if (!TileUtilities.IsValidTile(new Point(x, y), out var reason, _destructive))
            {
                goal = null;
                return ExecutionResult.Failure(reason);
            }

            goal = new Goal.GoalPosition(int.Parse(xStr), int.Parse(yStr));
            AlgorithmBase.IPathing pathing = new AStar.Pathing();
            if (pathing.FindPath(new PathNode(Main.Bot._farmer.TilePoint.X, Main.Bot._farmer.TilePoint.Y, null),
                    goal, Game1.currentLocation, 10000,_destructive).Result.Count == 0)
            {
                return ExecutionResult.Failure("You cannot make it to the provided tile, you should either try to go somewhere else or allow destruction.");
            }
            
            goal = new Goal.GoalPosition(int.Parse(xStr), int.Parse(yStr));
            _destructive = (bool)destructive;
            return ExecutionResult.Success($"You are walking towards {goal.VectorLocation}.");
        }

        protected override async void Execute(Goal? goal)
        {
            try
            {
                if (goal is null) return; // probably fine
                await Main.Bot.Pathfinding.Goto(goal, _destructive);
                await TaskDispatcher.SwitchToMainThread();
                RegisterMainActions.RegisterPostAction();
            }
            catch (Exception e)
            {
                Logger.Error($"{e}");
                await TaskDispatcher.SwitchToMainThread();
                RegisterMainActions.RegisterPostAction();
            }
        }

    }

    public class PathFindToExit : NeuroAction<Goal?>
    {
        private bool _destructive;
        private GameLocation _oldLocation = Main.Bot._currentLocation;
        public override string Name => "move_to_exit";
        protected override string Description => "This will move the character to the provided tile to go to an exit, " +
                                                 "the provided coordinates are sent as X and Y in that order.";
        protected override JsonSchema Schema => new()
        {
            Type = JsonSchemaType.Object,
            Required = new List<string> { "exit" },
            Properties = new Dictionary<string, JsonSchema>
            {
                ["exit"] = QJS.Enum(TileContext.GetWarpTilesStrings(TileContext.GetWarpTiles(Main.Bot._currentLocation,true,true))),
                ["destructive"] = QJS.Type(JsonSchemaType.Boolean)
            }
        };

        protected override ExecutionResult Validate(ActionData actionData, out Goal? goal)
        {
            string? pointStr = actionData.Data?.Value<string>("exit");
            bool? destructive = actionData.Data?.Value<bool>("destructive");

            Logger.Info($"data: {pointStr}");
            goal = null;
            
            if (pointStr is null || destructive is null)
            {
                Logger.Error($"data or yData is null");
                return ExecutionResult.Failure($"A value you gave was null");
            }

            if (!TileContext.GetWarpTilesStrings(TileContext.GetWarpTiles(Main.Bot._currentLocation,
                        true, true)).Contains(pointStr))
            {
                return ExecutionResult.Failure($"{pointStr} is not a valid exit.");
            }
            
            string[] splitName = pointStr.Split(":");
            string[] coords = splitName[1].Split(',');

            Point exitPoint = new Point(int.Parse(coords[0]), int.Parse(coords[1]));

            if (!TileContext.GetWarpsAsPoint(TileContext.GetWarpTiles(Main.Bot._currentLocation,true,true)).ContainsKey(exitPoint))
            { 
                return ExecutionResult.Failure($"The provided tile is not an exit");
            }

            if (exitPoint.X > TileUtilities.MaxX || exitPoint.X < -1 || // some exits are at -1
                exitPoint.Y > TileUtilities.MaxY || exitPoint.Y < -1)
            {
                Logger.Error($"Values are invalid due to either being larger than map size or less than 0");
                return ExecutionResult.Failure($"The value was either less than 0 or greater than the size of the map. If you were provided this position by the game, it is an issue with the mod.");
            }

            // if exit point is part of building and close to warp
            if (Utility.tileWithinRadiusOfPlayer(exitPoint.X, exitPoint.Y, 1, Main.Bot._farmer)
                && !TileContext.GetWarpsAsPoint(TileContext.GetWarpTiles(Main.Bot._currentLocation))
                    .ContainsKey(exitPoint))
            {
                goal = new Goal.GoalPosition(exitPoint.X,exitPoint.Y);
                return ExecutionResult.Success($"Entering {exitPoint}");
            }
            
            // the exit point should never be blocked I don't know what I was thinking about here
            // Main.Bot.Pathfinding.BuildCollisionMapInRadius(exitPoint,3);
            // if (Main.Bot.Pathfinding.IsBlocked(exitPoint.X, exitPoint.Y) && (bool)!destructive)
            // {
            //     return ExecutionResult.Failure("You gave a position that is blocked. Maybe try something else!");
            // }

            AlgorithmBase.IPathing pathing = new AStar.Pathing();
            if (pathing.FindPath(new PathNode(Main.Bot._farmer.TilePoint.X, Main.Bot._farmer.TilePoint.Y, null),
                    new Goal.GetToTile(exitPoint.X, exitPoint.Y), Main.Bot._currentLocation, 10000,_destructive).Result.Count == 0
                && !Graph.IsInNeighbours(Main.Bot._farmer.TilePoint,exitPoint,out _,4))
            {
                return ExecutionResult.Failure("You cannot make it to this exit, you should try something else.");
            }

            goal = new Goal.GetToTile(exitPoint.X,exitPoint.Y);
            _destructive = (bool)destructive;
            _oldLocation = Main.Bot._currentLocation;
            return ExecutionResult.Success($"Going to {goal.VectorLocation}");
        }

        protected override async void Execute(Goal? goal)
        {
            try
            {
                if (goal is null) return; // probably fine
                Vector2 vec2 = goal.VectorLocation.ToVector2() * 64;
                var buildings = Main.Bot._currentLocation.buildings
                    .Where(building => building.GetBoundingBox().Contains(vec2)).ToList();
                Building? building = null;
                if (buildings.Count > 0)
                {
                    building = buildings[0];
                    Point point = building.getPointForHumanDoor();
                    goal = new Goal.GetToTile(point.X, point.Y);
                }
            
                if (!Utility.tileWithinRadiusOfPlayer(goal.X, goal.Y, 1, Main.Bot._farmer))
                {
                    await Main.Bot.Pathfinding.Goto(goal, _destructive);
                    await TaskDispatcher.SwitchToMainThread();

                    // probably don't need to do lower checks if location changes
                    if (!Main.Bot._currentLocation.Equals(_oldLocation)) return;
                }

                // pathfinding can't go within 1 tile of current position so we do this. People probably won't notice.
                if (building is null)
                {
                    Logger.Info($"building is null");
                    List<Warp> warps = Main.Bot._currentLocation.warps.Where(warp => warp.X == goal.X && warp.Y == goal.Y)
                        .ToList();
                    Warp? doorWarp = Main.Bot._currentLocation.getWarpFromDoor(goal.VectorLocation,Main.Bot._farmer);

                    if (!warps.Any() && doorWarp is null)
                    {
                        Logger.Error($"No warp at: {goal.VectorLocation}");
                        RegisterMainActions.RegisterPostAction();
                        return;
                    }

                    var warp = doorWarp;
                    if (doorWarp is null) warp = warps[0];
                    
                    Main.Bot._farmer.warpFarmer(warp);
                
                    // warps can take a second to register sometimes
                    await Task.Delay(3000);
                    await TaskDispatcher.SwitchToMainThread();
                    if (Main.Bot._currentLocation.Equals(_oldLocation))
                    {
                        RegisterMainActions.RegisterPostAction();
                    }
                    return;
                }
            
                Main.Bot.Building.UseHumanDoor(building);
            }
            catch (Exception e)
            {
                Logger.Error($"exception in path-find to exit: {e}");
                await TaskDispatcher.SwitchToMainThread();
                if (Main.Bot._currentLocation.Equals(_oldLocation)) RegisterMainActions.RegisterPostAction();
            }
        }
    }

    public class InteractCharacter : NeuroAction<KeyValuePair<NPC,bool>>
    {
        public override string Name => "interact_with_character";
        protected override string Description => "Interact with a character that is in this location, if they are too" +
                                                 " far away you will walk to them. When you decide to interact with the" +
                                                 " character it will try to talk to the character unless they cannot be" +
                                                 " talked to or you are holding something that can be gifted.";
        protected override JsonSchema Schema => new()
        {
            Type = JsonSchemaType.Object,
            Required = new List<string> { "character","interact" },
            Properties = new Dictionary<string, JsonSchema>
            {
                ["character"] = QJS.Enum(Main.Bot._currentLocation.characters.Where(npc => !npc.IsMonster)
                    .Select(npc => $"{npc.Name}").ToList()),
                ["interact"] = QJS.Type(JsonSchemaType.Boolean)
            }
        };
        protected override ExecutionResult Validate(ActionData actionData, out KeyValuePair<NPC,bool> resultData)
        {
            string? charName = actionData.Data?.Value<string>("character");
            bool? interact = actionData.Data?.Value<bool>("interact");

            resultData = new();
            if (string.IsNullOrEmpty(charName) || interact is null)
            {
                return ExecutionResult.Failure($"You provided either an empty or null string");
            }

            int index = Main.Bot._currentLocation.characters.Select(npc => npc.Name).ToList().IndexOf(charName);
            if (index == -1)
            {
                return ExecutionResult.Failure($"The value you provided was invalid.");
            }
            
            resultData = new(Main.Bot._currentLocation.characters[index],interact.Value);
            return ExecutionResult.Success();
        }

        protected override async void Execute(KeyValuePair<NPC,bool> resultData)
        {
            try
            {
                await Main.Bot.Pathfinding.Goto(new Goal.GoalDynamic(resultData.Key, 1));
                await TaskDispatcher.SwitchToMainThread();
                if (resultData.Value)
                {
                    Main.Bot.Characters.InteractWithCharacter(resultData.Key);
                }
                RegisterMainActions.RegisterPostAction(); // this should not run if character starts talking
            }
            catch (Exception e)
            {
                Logger.Error($"Error in InteractCharacter: {e}");
                await TaskDispatcher.SwitchToMainThread();
                RegisterMainActions.RegisterPostAction();
            }
        }
    }

    // TODO: this will interact with the first monster that is called that not in that position
    public class AttackMonster : NeuroAction<Monster>
    {
        public override string Name => "attack_monster";
        protected override string Description => "Select a monster to attack.";
        protected override JsonSchema Schema => new()
        {
            Type = JsonSchemaType.Object,
            Required = new List<string> { "monster" },
            Properties = new Dictionary<string, JsonSchema>
            {
                ["monster"] = QJS.Enum(Game1.currentLocation.characters.Where(monster => monster.IsMonster)
                    .Select(monster => $"{monster.Name}").ToList())
            }
        };
        protected override ExecutionResult Validate(ActionData actionData, out Monster? resultData)
        {
            string? monsterName = actionData.Data?.Value<string>("monster");

            resultData = null;
            if (string.IsNullOrEmpty(monsterName))
            {
                return ExecutionResult.Failure($"You need to provide a monster to attack.");
            }
            
            NPC monster = Main.Bot._currentLocation.characters.Where(monster => monster.IsMonster)
                .Where(monster => $"{monster.Name}" == monsterName).ToArray()[0];

            if (monster is null)
            {
                return ExecutionResult.Failure($"That monster no longer exists in this location");
            }
            resultData = monster as Monster;
            return ExecutionResult.Success($"You are attacking the {monster.Name} at {monster.TilePoint}");
        }

        protected override async void Execute(Monster? resultData)
        {
            try
            {
                if (resultData is null) return;
                await Main.Bot.Pathfinding.AttackMonster(new Goal.GoalDynamic(resultData, 1),true);
                await TaskDispatcher.SwitchToMainThread();
                RegisterMainActions.RegisterPostAction();
            }
            catch (Exception e)
            {
                Logger.Error($"Error in AttackMonster: {e}");
                await TaskDispatcher.SwitchToMainThread();
                RegisterMainActions.RegisterPostAction();
            }
        }
    }
}