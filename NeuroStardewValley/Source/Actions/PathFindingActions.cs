using System.Collections.Concurrent;
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
            if (pathing.FindPath(new PathNode(BotHandler.Farmer.TilePoint.X, BotHandler.Farmer.TilePoint.Y, null),
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
                await BotHandler.Bot.Pathfinding.Goto(goal, _destructive);
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
        private GameLocation _oldLocation = BotHandler.CurrentLocation;
        public override string Name => "move_to_exit";
        protected override string Description => "This will move the character to the provided tile to go to an exit, " +
                                                 "the provided coordinates are sent as X and Y in that order.";
        protected override JsonSchema Schema => new()
        {
            Type = JsonSchemaType.Object,
            Required = new List<string> { "exit" },
            Properties = new Dictionary<string, JsonSchema>
            {
                ["exit"] = QJS.Enum(GetPathfindExits().Result),
                ["destructive"] = QJS.Type(JsonSchemaType.Boolean)
            }
        };

        protected override ExecutionResult Validate(ActionData actionData, out Goal? goal)
        {
            string? pointStr = actionData.Data?.Value<string>("exit");
            bool? destructive = actionData.Data?.Value<bool>("destructive");

            Logger.Info($"data: {pointStr}");
            goal = null;
            
            if (pointStr is null)
            {
                Logger.Error($"data or yData is null");
                return ExecutionResult.Failure($"A value you gave was null");
            }

            // I think it is false if not specified but might as well double check
            destructive ??= false;
            
            // this is where we get points from
            if (!_selectedWarps.TryGetValue(pointStr, out var exitPoint)) return ExecutionResult.Failure($"{pointStr} is not a valid warp.");

            if (!TileContext.GetWarpsAsPoint(BotHandler.CurrentLocation,true,true, true).ContainsKey(exitPoint))
            { 
                return ExecutionResult.Failure($"The value you provided was not a valid exit.");
            }

            if (exitPoint.X > TileUtilities.MaxX || exitPoint.X < -1 || // some exits are at -1
                exitPoint.Y > TileUtilities.MaxY || exitPoint.Y < -1)
            {
                Logger.Error($"Values are invalid due to either being larger than map size or less than 0");
                return ExecutionResult.Failure($"The value was either less than 0 or greater than the size of the map. If you were provided this position by the game, it is an issue with the mod.");
            }

            // if exit point is part of building
            if (Utility.tileWithinRadiusOfPlayer(exitPoint.X, exitPoint.Y, 1, BotHandler.Farmer)
                && !TileContext.GetWarpsAsPoint(BotHandler.CurrentLocation, false, true)
                    .ContainsKey(exitPoint))
            {
                goal = new Goal.GoalPosition(exitPoint.X,exitPoint.Y);
                return ExecutionResult.Success($"Entering {exitPoint}");
            }
            
            BotHandler.Bot.Pathfinding.BuildCollisionMapInRadius(exitPoint,3);
            if (BotHandler.Bot.Pathfinding.IsBlocked(exitPoint.X, exitPoint.Y) &&
                // this is here as actionable tiles block something most of the time, may have side effects that I don't know about though.
                !TileUtilities.Actionable(exitPoint) && (bool)!destructive)
            {
                return ExecutionResult.Failure("You gave a position that is blocked. Maybe try something else!");
            }

            AlgorithmBase.IPathing pathing = new AStar.Pathing();
            Goal testGoal = new Goal.GoalPosition(exitPoint.X, exitPoint.Y);
            if (TileUtilities.Actionable(exitPoint))
            {
                testGoal = new Goal.GetToTile(exitPoint.X, exitPoint.Y);
            }
            if (!pathing.FindPath(new PathNode(BotHandler.Farmer.TilePoint.X, BotHandler.Farmer.TilePoint.Y, null),
                    testGoal, BotHandler.CurrentLocation, 10000,_destructive).Result.Any())
            {
                return ExecutionResult.Failure("You cannot make it to this exit, you should try something else.");
            }

            goal = new Goal.GoalPosition(exitPoint.X,exitPoint.Y);
            _destructive = (bool)destructive;
            _oldLocation = BotHandler.CurrentLocation;
            return ExecutionResult.Success($"Going to {goal.VectorLocation}");
        }

        protected override async void Execute(Goal? goal)
        {
            try
            {
                await TaskDispatcher.SwitchToMainThread();
                
                if (goal is null)
                {
                    RegisterMainActions.RegisterPostAction();
                    return; // probably fine
                }
                Building? building = GetSurroundingBuilding(goal.VectorLocation);
                Logger.Info($"building: {building is null}");
                if (building is not null)
                {
                    Point point = building.getPointForHumanDoor();
                    goal = new Goal.GetToTile(point.X, point.Y);
                }
                
                var actions = TileContext.GetActionAndTile();
                bool actionTile = false;
                Logger.Info($"{goal.VectorLocation}");
                actions.TryGetValue(goal.VectorLocation, out var value);
                if (TileUtilities.Actionable(goal.VectorLocation) || 
                    TileContext.ActionWarpString(new(goal.VectorLocation, value ?? string.Empty),true) != "")
                {
                    actionTile = true;
                    goal = new Goal.GetToTile(goal.VectorLocation.X, goal.VectorLocation.Y);
                }
                
                if (!Utility.tileWithinRadiusOfPlayer(goal.X, goal.Y, 1, BotHandler.Farmer))
                {
                    await BotHandler.Bot.Pathfinding.Goto(goal, _destructive);
                    await TaskDispatcher.SwitchToMainThread();

                    // probably don't need to do lower checks if these are different
                    if (!BotHandler.CurrentLocation.Equals(_oldLocation)) return;
                }

                // pathfinding can't go within 1 tile of current position so we do this.
                if (building is null && actionTile)
                {
                    Logger.Info($"using action tile");
                    BotHandler.Bot.ActionTiles.DoActionTile(goal.VectorLocation);
                    return;
                }
                
                if (building is null)
                {
                    List<Warp> warps = BotHandler.CurrentLocation.warps.Where(warp => warp.X == goal.X && warp.Y == goal.Y)
                        .ToList();
                    if (!warps.Any()) return;
                    var warp = warps[0];

                    BotHandler.Farmer.warpFarmer(warp);
                
                    // warps can take a second to register sometimes
                    await Util.WaitForSeconds(3);
                    if (BotHandler.CurrentLocation.Equals(_oldLocation))
                    {
                        RegisterMainActions.RegisterPostAction();
                    }
                    return;
                }
            
                Logger.Info($"entering human door");
                BotHandler.Bot.Building.UseHumanDoor(building);
            }
            catch (Exception e)
            {
                Logger.Error($"exception in path-find to exit: {e}");
                await TaskDispatcher.SwitchToMainThread();
                if (BotHandler.CurrentLocation.Equals(_oldLocation)) RegisterMainActions.RegisterPostAction();
            }
        }

        private readonly ConcurrentDictionary<string, Point> _selectedWarps = new();

        private async Task<List<string>> GetPathfindExits()
        {
            await TaskDispatcher.SwitchToMainThread();
            var warpsAsPoint = TileContext.GetWarpsAsPoint(BotHandler.CurrentLocation,true,true , true);
            BotHandler.Bot.Pathfinding.BuildCollisionMap();

            foreach (var warpStr in warpsAsPoint)
            {
                Logger.Info($"kvp: {warpStr.Key}   {warpStr.Value}");
                
                // we only want to check for duplicates from buildings
                if (_selectedWarps.ContainsKey(warpStr.Value) && 
                    !TileContext.GetWarpsAsPoint(BotHandler.CurrentLocation, false, true).ContainsKey(warpStr.Key)) continue;

                var pathNodes = await BotHandler.Bot.Pathfinding.GetPathTo(new Goal.GetToTile(warpStr.Key.X,warpStr.Key.Y), 2500,true,false);
                Building? building = TileUtilities.BuildingContainsTile(warpStr.Key);
                if (!pathNodes.Any() && !Graph.IsInNeighbours(BotHandler.Farmer.TilePoint, warpStr.Key, out _, 4) && building is null) continue;

                if (building is not null)
                {
                    Logger.Info($"building: {building}");
                    if (!building.HasIndoors()) continue;

                    string buildingName = StringUtilities.GetBuildingName(building);
                    AddDuplicateAmount(warpStr.Value, ref buildingName);

                    _selectedWarps.TryAdd(buildingName,warpStr.Key);
                    continue;
                }
                
                // this is due to building warps and both tile warps handling greenhouse
                if (warpStr.Value.ToLower() == "greenhouse" && !BotHandler.Farmer.mailReceived.Contains("ccPantry"))
                {
                    continue;
                }

                var location = Game1.getLocationFromName(warpStr.Value);
                string name = warpStr.Value;
                // this stops buildings like the greenhouse from adding the current location
                if (location is not null && location.DisplayName != BotHandler.CurrentLocation.DisplayName)
                {
                    name = location.DisplayName;
                }
                AddDuplicateAmount(warpStr.Value, ref name);

                _selectedWarps.TryAdd(name, warpStr.Key);
            }

            return _selectedWarps.Keys.ToList();
        }

        private void AddDuplicateAmount(string preFormatName, ref string postFormatName)
        {
            int amount = _selectedWarps.Keys.Count(preFormatName.Contains);
            if (amount > 0)
            {
                postFormatName = $"{postFormatName}: {amount}";
            }
        } 

        private static Building? GetSurroundingBuilding(Point tile)
        {
            if (TileUtilities.BuildingContainsTile(tile) is not null) return TileUtilities.BuildingContainsTile(tile);
            var graph = new Graph();
            return graph.GroupNeighbours(tile, 4).Select(TileUtilities.BuildingContainsTile).OfType<Building>().FirstOrDefault();
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
                ["character"] = QJS.Enum(BotHandler.CurrentLocation.characters.Where(npc => !npc.IsMonster)
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

            int index = BotHandler.CurrentLocation.characters.Select(npc => npc.Name).ToList().IndexOf(charName);
            if (index == -1)
            {
                return ExecutionResult.Failure($"The value you provided was invalid.");
            }
            
            resultData = new(BotHandler.CurrentLocation.characters[index],interact.Value);
            string resultString = interact.Value
                ? $"Interacting with {resultData.Key.GetTokenizedDisplayName()}"
                : $"Walking over to {resultData.Key.GetTokenizedDisplayName()}";
            return ExecutionResult.Success(resultString);
        }

        protected override async void Execute(KeyValuePair<NPC,bool> resultData)
        {
            try
            {
                await BotHandler.Bot.Pathfinding.Goto(new Goal.GoalDynamic(resultData.Key, 1));
                await TaskDispatcher.SwitchToMainThread();
                // we check neighbour to prevent opening dialogue from large distances away
                if (resultData.Value && Graph.IsInNeighbours(BotHandler.Farmer.TilePoint,resultData.Key.TilePoint,out _))
                {
                    BotHandler.Bot.Characters.InteractWithCharacter(resultData.Key);
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
            
            NPC monster = BotHandler.CurrentLocation.characters.Where(monster => monster.IsMonster)
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
                await BotHandler.Bot.Pathfinding.AttackMonster(new Goal.GoalDynamic(resultData, 1),true);
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