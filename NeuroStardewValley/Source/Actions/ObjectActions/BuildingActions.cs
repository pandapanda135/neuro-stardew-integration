using Microsoft.Xna.Framework;
using NeuroSDKCsharp.Actions;
using NeuroSDKCsharp.Json;
using NeuroSDKCsharp.Messages.Outgoing;
using NeuroSDKCsharp.Websocket;
using NeuroStardewValley.Debug;
using NeuroStardewValley.Source.RegisterActions;
using NeuroStardewValley.Source.Utilities;
using StardewBotFramework.Source.Modules.Pathfinding.Base;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.GameData.Buildings;
using StardewValley.Locations;
using StardewValley.TokenizableStrings;

namespace NeuroStardewValley.Source.Actions.ObjectActions;

public static class BuildingActions
{
	public class InteractWithBuilding : NeuroAction<Building>
	{
		public override string Name => "interact_with_building";
		protected override string Description => "This will interact with the building specified, you can use this to either enter the interacted building or interact with a part of it. e.g. the mail-box.";

		protected override JsonSchema Schema => new()
		{
			Type = JsonSchemaType.Object,
			Required = new List<string> { "building" },
			Properties = new Dictionary<string, JsonSchema>
			{
				["building"] = QJS.Enum(GetLocationsBuildings().Select(StringUtilities.GetBuildingName))
			}
		};
		protected override ExecutionResult Validate(ActionData actionData, out Building? resultData)
		{
			string? buildingString = actionData.Data?.Value<string>("building");

			resultData = null;
			if (string.IsNullOrEmpty(buildingString))
			{
				return ExecutionResult.Failure($"Building was null or empty");
			}

			if (!GetLocationsBuildings().Select(StringUtilities.GetBuildingName).Contains(buildingString))
			{
				return ExecutionResult.Failure($"Building you gave is not valid.");
			}
			
			int index = GetLocationsBuildings().Select(StringUtilities.GetBuildingName).ToList().IndexOf(buildingString);
			Building building = GetLocationsBuildings()[index];

			if (GetBuildingsActionTiles(new List<Building> { building })[building].Count == 0 && building.GetIndoors() == null)
			{
				return ExecutionResult.Failure($"You cannot select this building, as it has no action tiles or entrances.");
			}
			
			resultData = building;
			return ExecutionResult.Success($"You are now interacting with the {buildingString}.");
		}

		protected override void Execute(Building? resultData)
		{
			if (resultData is null) return;
			
			ActionWindow window = ActionWindow.Create(Main.GameInstance);
			string stateString = $"Select an action to do with the selected {StringUtilities.GetBuildingName(resultData)}," +
			                     $" the bottom left of the building is at {resultData.tileX},{resultData.tileY} and is " +
			                     $"{resultData.tilesHigh} tiles high and {resultData.tilesWide} tiles wide.";
			if (GetBuildingsActionTiles(new List<Building> { resultData })[resultData].Count > 0)
			{
				window.AddAction(new BuildingAction(resultData));
			}
			if (resultData.GetIndoors() != null && resultData.OnUseHumanDoor(BotHandler.Farmer))
			{
				stateString += $" The building's door being at {resultData.getPointForHumanDoor()}.";
				window.AddAction(new EnterBuilding(resultData));
			}

			if (resultData.isCabin || resultData.GetIndoors() is FarmHouse)
			{
				stateString += $" The mailbox is located at {resultData.getMailboxPosition()}";
			}
			
			window.SetForce(0, $"You are interacting with a building.", stateString);
			window.Register();
		}
	}

	private class BuildingAction : NeuroAction<KeyValuePair<BuildingActionTile,bool>>
	{
		private readonly Building _building;

		public BuildingAction(Building building)
		{
			_building = building;
		}
		
		public override string Name => "building_action";
		protected override string Description => $"Select an action to use, these are on the building you selected earlier";
		protected override JsonSchema Schema => new()
		{
			Type = JsonSchemaType.Object,
			Required = new List<string> { "action" },
			Properties = new Dictionary<string, JsonSchema>
			{
				["action"] = QJS.Enum(GetBuildingTiles(GetBuildingsActionTiles(new() { _building }))),
				["path-find"] = QJS.Type(JsonSchemaType.Boolean)
			}
		};
		protected override ExecutionResult Validate(ActionData actionData, out KeyValuePair<BuildingActionTile,bool> resultData)
		{
			string? action = actionData.Data?.Value<string>("action");
			bool? pathfind = actionData.Data?.Value<bool>("path-find");

			resultData = new();
			if (string.IsNullOrEmpty(action) || pathfind is null)
			{
				return ExecutionResult.Failure($"action was null or empty");
			}

			if (!GetBuildingTiles(GetBuildingsActionTiles(new() { _building })).Contains(action))
			{
				return ExecutionResult.Failure($"You gave an action that is not an option.");
			}
			
			int index = GetBuildingTiles(GetBuildingsActionTiles(new() { _building })).IndexOf(action);
			BuildingActionTile tile = GetBuildingsActionTiles(new() { _building })[_building][index];
			Point buildingTile = TileUtilities.BuildingTile(_building);
			if (!Utility.tileWithinRadiusOfPlayer(buildingTile.X + tile.Tile.X, buildingTile.Y + tile.Tile.Y,1, BotHandler.Farmer))
			{
				return ExecutionResult.Failure($"This action is not within radius of you."); // maybe make it so walk toward it? maybe add it to schema?
			}

			resultData = new(tile, pathfind.Value);
			return ExecutionResult.Success();
		}

		protected override async void Execute(KeyValuePair<BuildingActionTile,bool> resultData)
		{
			try
			{
				if (resultData.Value)
				{
					Point pos = resultData.Key.Tile;
					await BotHandler.Bot.Pathfinding.Goto(new Goal.GetToTile(pos.X, pos.Y));
					await TaskDispatcher.SwitchToMainThread();
					if (!RangeCheck.InRange(pos)) // in case pathfinding can't get to tile
					{
						Context.Send($"The pathfinding had an issue, leading to you not being able to go to the tile. You should try something else.");
						RegisterMainActions.RegisterPostAction();
						return;
					}
				}
				BotHandler.Bot.Building.DoBuildingAction(_building, resultData.Key.Tile.ToVector2());
				if (Game1.activeClickableMenu is null)
				{
					RegisterMainActions.RegisterPostAction();
				}
			}
			catch (Exception e)
			{
				Logger.Error($"{e}");
				await TaskDispatcher.SwitchToMainThread();
				RegisterMainActions.RegisterPostAction();
			}
		}

		private static List<string> GetBuildingTiles(Dictionary<Building, List<BuildingActionTile>> dictionary)
		{
			List<string> buildingsStrings = new();
			foreach (var kvp in dictionary)
			{
				foreach (var tile in kvp.Value)
				{
					string buildingString = "";
					buildingString = string.Concat(buildingString,$"Action: {TokenParser.ParseText(tile.Action)} Tile position: {tile.Tile}");
					buildingsStrings.Add(buildingString);
				}
			}

			return buildingsStrings;
		}
	}

	private class EnterBuilding : NeuroAction<bool>
	{
		private readonly Building _building;

		private Point Pos => _building.getPointForHumanDoor();

		public EnterBuilding(Building building)
		{
			_building = building;
		}
		
		public override string Name => "enter_building";
		protected override string Description => "Enter the building you selected before, if you are not in a radius of 1 to the door, you can path-find to it.";
		protected override JsonSchema Schema => new()
		{
			Type = JsonSchemaType.Object,
			Required = new List<string> { "path-find" },
			Properties = new Dictionary<string, JsonSchema>
			{
				["pathfind"] = QJS.Type(JsonSchemaType.Boolean)
			}
		};
		protected override ExecutionResult Validate(ActionData actionData,out bool resultData)
		{
			resultData = false;

			bool? pathfind = actionData.Data?.Value<bool>("path-find");
			if (pathfind is null) return ExecutionResult.Failure($"You cannot send null");
			resultData = pathfind.Value;
			// these are ran in doAction however I don't want to run that in validation so we do this
			if (!_building.OnUseHumanDoor(Game1.player))
			{
				return ExecutionResult.Failure($"You cannot enter this building");
			}
			if (Game1.player.mount != null)
			{
				Game1.showRedMessage(Game1.content.LoadString("Strings\\Buildings:DismountBeforeEntering")); // keep these so people can laugh at her.
				return ExecutionResult.Failure($"You need to dismount before being able to enter this building.");
			}
			if (Game1.player.team.demolishLock.IsLocked())
			{
				Game1.showRedMessage(Game1.content.LoadString("Strings\\Buildings:CantEnter"));
				return ExecutionResult.Failure($"You cannot enter this building right now.");
			}
			
			// This shouldn't happen as we only send if indoors exists, but I don't trust my code.
			if (_building.GetIndoors() is null) return ExecutionResult.Failure($"You cannot enter this building as it does not have an interior.");

			if (!RangeCheck.InRange(Pos) && !pathfind.Value)
			{
				return ExecutionResult.Failure($"The door is not in radius of you"); // maybe make it so work toward it? maybe add it to schema?
			}
			return ExecutionResult.Success();
		}

		protected override async void Execute(bool pathfinding)
		{
			try
			{
				if (pathfinding)
				{
					await BotHandler.Bot.Pathfinding.Goto(new Goal.GetToTile(Pos.X,Pos.Y));
					await TaskDispatcher.SwitchToMainThread();
					if (!RangeCheck.InRange(Pos)) // in case pathfinding can't get to door
					{
						Context.Send($"The pathfinding had an issue, leading to you not being able to enter the building. You should try something else.");
						RegisterMainActions.RegisterPostAction();
						return;
					}
				}
				BotHandler.Bot.Building.UseHumanDoor(_building);
			}
			catch (Exception e)
			{
				Logger.Error($"{e}");
				await TaskDispatcher.SwitchToMainThread();
				RegisterMainActions.RegisterPostAction();
			}
		}
	}

	private static List<Building> GetLocationsBuildings()
	{
		return Game1.currentLocation.buildings.ToList();
	}

	private static Dictionary<Building, List<BuildingActionTile>> GetBuildingsActionTiles(List<Building> buildings)
	{
		Dictionary<Building, List<BuildingActionTile>> result = new();
		foreach (var building in buildings)
		{
			result.Add(building,building.GetData().ActionTiles);
		}

		return result;
	}
}