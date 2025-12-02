using Microsoft.Xna.Framework;
using NeuroSDKCsharp.Actions;
using NeuroSDKCsharp.Json;
using NeuroSDKCsharp.Websocket;
using NeuroStardewValley.Debug;
using NeuroStardewValley.Source.ContextStrings;
using NeuroStardewValley.Source.EventMethods;
using NeuroStardewValley.Source.RegisterActions;
using NeuroStardewValley.Source.Utilities;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.GameData.Buildings;
using StardewValley.Menus;
using StardewValley.Util;

namespace NeuroStardewValley.Source.Actions.Menus;


public static class CarpenterActions
{
	public class ChangeBuildingBlueprint : NeuroAction<CarpenterMenu.BlueprintEntry>
	{
		public override string Name => "change_building";
		protected override string Description => "Change the currently selected building out of what is available, just because you can select it does not mean you can do anything with it.";

		protected override JsonSchema Schema => new()
		{
			Type = JsonSchemaType.Object,
			Required = new List<string> { "building" },
			Properties = new Dictionary<string, JsonSchema>
			{
				["building"] = QJS.Enum(GetSchema())
			}
		};
		protected override ExecutionResult Validate(ActionData actionData, out CarpenterMenu.BlueprintEntry? resultData)
		{
			string? blueprintEntry =
				actionData.Data?.Value<string>("building");

			resultData = new CarpenterMenu.BlueprintEntry(0,"",new BuildingData(),"");
			if (blueprintEntry is null)
			{
				return ExecutionResult.Failure($"You provided a null value");
			}

			if (!GetSchema().Contains(blueprintEntry))
			{
				return ExecutionResult.Failure($"You gave an invalid value");
			}

			for (int i = 0; i < GetSchema().Count(); i++)
			{
				if (BotHandler.Bot.FarmBuilding.CarpenterMenu.Blueprints[i].DisplayName == blueprintEntry)
				{
					resultData = BotHandler.Bot.FarmBuilding.Blueprints[i];
				}
			}
			return ExecutionResult.Success($"You have selected {resultData.DisplayName}");
		}

		protected override void Execute(CarpenterMenu.BlueprintEntry? resultData)
		{
			BotHandler.Bot.FarmBuilding.ChangeBuilding(resultData!);
			RegisterStoreActions.RegisterCarpenterActions();
		}

		private static IEnumerable<string> GetSchema()
		{
			List<string> nameList = new();
			foreach (var blueprint in BotHandler.Bot.FarmBuilding.Blueprints)
			{
				nameList.Add(blueprint.DisplayName);
			}

			return nameList;
		}
	}
	
	[Obsolete("This is no longer used in favour of the new actions for this menu")]
	public class CreateBuilding : NeuroAction
	{
		public override string Name => "create_building";
		protected override string Description => "This will move you to creating the building";
		protected override JsonSchema Schema => new();
		protected override ExecutionResult Validate(ActionData actionData)
		{
			if (BotHandler.Bot.FarmBuilding.CarpenterMenu.CanBuildCurrentBlueprint())
			{
				return ExecutionResult.Success($"building {BotHandler.Bot.FarmBuilding.BlueprintEntry?.DisplayName}");
			}
			return ExecutionResult.Failure($"You cannot build this blueprint.");
		}

		protected override async void Execute()
		{
			try
			{
				BotHandler.Bot.FarmBuilding.InteractWithButton(BotHandler.Bot.FarmBuilding.CarpenterMenu.okButton);
				await PlaceBuildingActions.RegisterPlaceBuilding();
			}
			catch (Exception e)
			{
				Logger.Error($"{e}");
			}
		}
	}

	[Obsolete("This is no longer used in favour of the new actions for this menu")]
	public class DemolishBuilding : NeuroAction
	{
		public override string Name => "demolish_building";
		protected override string Description => "Demolish the building of this type that is on your farm";
		protected override JsonSchema Schema => new();
		protected override ExecutionResult Validate(ActionData actionData)
		{
			if (BotHandler.Bot.FarmBuilding.CarpenterMenu.CanDemolishThis())
			{
				return ExecutionResult.Success();
			}
			return ExecutionResult.Failure($"You cannot destroy this building");
		}

		protected override async void Execute()
		{
			try
			{
				BotHandler.Bot.FarmBuilding.InteractWithButton(BotHandler.Bot.FarmBuilding.CarpenterMenu.demolishButton);
				await PlaceBuildingActions.RegisterPlaceBuilding(true);
			}
			catch (Exception e)
			{
				Logger.Error($"{e}");
			}
		}
	}

	[Obsolete("This is no longer used in favour of the new actions for this menu")]
	public class UpgradeBuilding : NeuroAction
	{
		public override string Name => "upgrade_building";
		protected override string Description => "Upgrade the currently selected building.";
		protected override JsonSchema Schema => new();
		protected override ExecutionResult Validate(ActionData actionData)
		{
			if (BotHandler.Bot.FarmBuilding.BlueprintEntry is null) return ExecutionResult.Failure($"There is not a blueprint entry currently.");
			if (!BotHandler.Bot.FarmBuilding.CarpenterMenu.CanBuildCurrentBlueprint())
			{
				return ExecutionResult.Failure($"You cannot build the: {BotHandler.Bot.FarmBuilding.BlueprintEntry?.DisplayName}");
			}
			return ExecutionResult.Success();
		}

		protected override async void Execute()
		{
			try
			{
				BotHandler.Bot.FarmBuilding.InteractWithButton(BotHandler.Bot.FarmBuilding.CarpenterMenu.okButton);
				await PlaceBuildingActions.RegisterPlaceBuilding(true);
			}
			catch (Exception e)
			{
				Logger.Error($"{e}");
			}
		}
	}

	public class ChangeBuildingSkin : NeuroAction<BuildingSkinMenu.SkinEntry>
	{
		public override string Name => "change_building_skin";
		protected override string Description => "Change how the currently selected building looks, this will not change the cost of making the building.";
		protected override JsonSchema Schema => new()
		{
			Type = JsonSchemaType.Object,
			Required = new List<string> { "skin" },
			Properties = new Dictionary<string, JsonSchema>
			{
				["skin"] = QJS.Enum(GetSchema())
			}
		};
		protected override ExecutionResult Validate(ActionData actionData, out BuildingSkinMenu.SkinEntry? resultData)
		{
			string? selectedSkin = actionData.Data?.Value<string>("skin");

			resultData = null;
			if (selectedSkin is null)
			{
				return ExecutionResult.Failure($"You gave a null value for skin");
			}

			if (!GetSchema().Contains(selectedSkin))
			{
				return ExecutionResult.Failure($"The skin you provided is not a valid option");
			}
			
			foreach (var skin in BotHandler.Bot.FarmBuilding.GetBuildingSkins().Where(skin => skin.Index.ToString() == selectedSkin))
			{
				resultData = skin;
			}

			if (resultData is null)
			{
				return ExecutionResult.Failure($"The skin you selected could not be selected.");
			}
			return ExecutionResult.Success($"using the skin: {selectedSkin}");
		}

		protected override void Execute(BuildingSkinMenu.SkinEntry? resultData)
		{
			BotHandler.Bot.FarmBuilding.InteractWithButton(BotHandler.Bot.FarmBuilding.CarpenterMenu.appearanceButton);
			BotHandler.Bot.FarmBuilding.ChangeSkin(resultData!);
			RegisterStoreActions.RegisterCarpenterActions();
		}

		private static List<string> GetSchema()
		{
			List<string> strings = new();
			foreach (var skin in BotHandler.Bot.FarmBuilding.GetBuildingSkins())
			{
				strings.Add($"{skin.Index}");
			}

			return strings;
		}
	}
	
	#region NewMenuActions

	private static void HandleRegister()
	{
		if (Game1.activeClickableMenu is not null)
		{
			Game1.activeClickableMenu.exitThisMenu();
			return;
		}
		RegisterMainActions.RegisterPostAction();
	}

	public class BuildBluePrint : NeuroAction<CarpenterMenu.BlueprintEntry>
	{
		public override string Name => "build_building";
		protected override string Description => "Select the building you want to build";
		protected override JsonSchema Schema => new()
		{
			Type = JsonSchemaType.Object,
			Required = new List<string> { "building" },
			Properties = new Dictionary<string, JsonSchema>
			{
				["building"] = QJS.Enum(BotHandler.Bot.FarmBuilding.Blueprints.Where(entry => BotHandler.Bot.FarmBuilding.CanCreateBluePrint(entry)).Select(blueprint => blueprint.DisplayName))
			}
		};
		protected override ExecutionResult Validate(ActionData actionData, out CarpenterMenu.BlueprintEntry? resultData)
		{
			string? buildingName = actionData.Data?.Value<string>("building");

			resultData = null;
			if (buildingName is null ||
			    BotHandler.Bot.FarmBuilding.Blueprints.All(entry => entry.DisplayName != buildingName))
			{
				return ExecutionResult.Failure($"");
			}

			resultData = BotHandler.Bot.FarmBuilding.Blueprints.FirstOrDefault(entry => entry.DisplayName == buildingName);
			if (resultData is null)
				return ExecutionResult.Failure($"{string.Format(ResultStrings.ModVarFailure, "resultData in BuildBluePrint")}");
			if (!BotHandler.Bot.FarmBuilding.CarpenterMenu.DoesFarmerHaveEnoughResourcesToBuild())
				return ExecutionResult.Failure($"You do not have the resources needed to building a {resultData.DisplayName}");
			
			return ExecutionResult.Success($"Moving so you can select where to build the {resultData.DisplayName}.");
		}

		protected override async void Execute(CarpenterMenu.BlueprintEntry? resultData)
		{
			try
			{
				// this should never happen, should probably check for it though.
				if (resultData is null)
				{
					Logger.Error($"Result data was null in BuildBlueprint: {StackTraceHelper.StackTrace}");
					HandleRegister();
					return;
				}
				BotHandler.Bot.FarmBuilding.ChangeBuilding(resultData);
				await Util.WaitForSeconds(2);
				BotHandler.Bot.FarmBuilding.InteractWithButton(BotHandler.Bot.FarmBuilding.CarpenterMenu.okButton);
				await PlaceBuildingActions.RegisterPlaceBuilding();
			}
			catch (Exception e)
			{
				await TaskDispatcher.SwitchToMainThread();
				Logger.Error($"There was an error in BuildBlueprint {e}");
				HandleRegister();
			}
		}
	}

	public class DestroyBuilding : NeuroAction
	{
		public override string Name => "destroy_building";
		protected override string Description => "Select a building to destroy";
		protected override JsonSchema Schema => new();
		protected override ExecutionResult Validate(ActionData actionData)
		{
			if (BotHandler.Bot.FarmBuilding.CarpenterMenu.Blueprints.FirstOrDefault(entry =>
				    BotHandler.Bot.FarmBuilding.CanDestroyBluePrint(entry)) is null)
			{
				return ExecutionResult.Failure($"There are no buildings that you can destroy here.");
			}
			
			return ExecutionResult.Success($"Moving to allow you to select a building to destroy");
		}

		protected override async void Execute()
		{
			try
			{
				BotHandler.Bot.FarmBuilding.ChangeBuilding(BotHandler.Bot.FarmBuilding.CarpenterMenu.Blueprints.First(entry => BotHandler.Bot.FarmBuilding.CanDestroyBluePrint(entry)));
				await Util.WaitForSeconds(1);
				BotHandler.Bot.FarmBuilding.LeftClick(BotHandler.Bot.FarmBuilding.CarpenterMenu.demolishButton);
				await PlaceBuildingActions.RegisterPlaceBuilding(true);
			}
			catch (Exception e)
			{
				await TaskDispatcher.SwitchToMainThread();
				Logger.Error($"There was an error in BuildBlueprint {e}");
				HandleRegister();
			}
		}
	}

	public class UpgradeBlueprint : NeuroAction<CarpenterMenu.BlueprintEntry>
	{
		private readonly List<CarpenterMenu.BlueprintEntry> _validEntries = BotHandler.Bot.FarmBuilding.Blueprints
			.Where(entry => entry.IsUpgrade && BotHandler.Bot.FarmBuilding.CanCreateBluePrint(entry)).ToList();
		public override string Name => "upgrade_building";
		protected override string Description => "Upgrade a building";
		protected override JsonSchema Schema => new()
		{
			Type = JsonSchemaType.Object,
			Required = new List<string> { "building" },
			Properties = new Dictionary<string, JsonSchema>
			{
				["building"] = QJS.Enum(_validEntries.Select(entry => entry.DisplayName))
			}
		};
		protected override ExecutionResult Validate(ActionData actionData, out CarpenterMenu.BlueprintEntry? resultData)
		{
			string? buildingName = actionData.Data?.Value<string>("building");

			resultData = null;
			if (_validEntries.All(entry => entry.DisplayName != buildingName))
				return ExecutionResult.Failure($"");

			resultData = _validEntries.FirstOrDefault(entry => entry.DisplayName == buildingName);
			if (resultData is null) return ExecutionResult.Failure($"");
			
			return ExecutionResult.Success($"Upgrading {resultData.DisplayName} to {resultData.GetDisplayNameForBuildingToUpgrade()}");
		}

		protected override async void Execute(CarpenterMenu.BlueprintEntry? resultData)
		{
			try
			{
				if (resultData is null)
				{
					HandleRegister();
					return;
				}
				BotHandler.Bot.FarmBuilding.ChangeBuilding(resultData);
				await Util.WaitForSeconds(2);
				BotHandler.Bot.FarmBuilding.LeftClick(BotHandler.Bot.FarmBuilding.CarpenterMenu.okButton);
				await PlaceBuildingActions.RegisterPlaceBuilding(true);
			}
			catch (Exception e)
			{
				await TaskDispatcher.SwitchToMainThread();
				Logger.Error($"There was an error in BuildBlueprint {e}");
				HandleRegister();
			}
		}
	}

	#endregion
}

public static class PlaceBuildingActions
{
	private class PlaceBuilding : NeuroAction<Point>
	{
		public override string Name => "place_building";
		protected override string Description => "Place building at the specified location, you should make sure the specified tile or it's neighbours will not block this building.";
		protected override JsonSchema Schema => new()
		{
			Type = JsonSchemaType.Object,
			Required = new List<string> { "tile_x", "tile_y" },
			Properties = new Dictionary<string, JsonSchema>
			{
				["tile_x"] = QJS.Type(JsonSchemaType.Integer),
				["tile_y"] = QJS.Type(JsonSchemaType.Integer)
			}
		};
		protected override ExecutionResult Validate(ActionData actionData, out Point resultData)
		{
			int? tileX = actionData.Data?.Value<int>("tile_x");
			int? tileY = actionData.Data?.Value<int>("tile_y");

			resultData = new();
			if (tileX is null || tileY is null)
			{
				return ExecutionResult.Failure($"You have provided a null value in tile_x or tile_y");
			}

			int x = (int)tileX;
			int y = (int)tileY;

			BotHandler.Bot.FarmBuilding.SetSelectedTile(new Point(x,y));
			if (!BotHandler.Bot.FarmBuilding.CanPlaceBuilding(BotHandler.Bot.FarmBuilding.Building))
			{
				return ExecutionResult.Failure($"You cannot build at {x},{y}.");
			}
			
			resultData = new (x,y);
			return ExecutionResult.Success();
		}

		protected override void Execute(Point resultData) // This is run in validation
		{
			BotHandler.Bot.FarmBuilding.CreateBuilding(resultData); // one day maybe replicate how it checks if you can build so this isn't in validate. A bit lazy for that rn.
		}
	}

	public class SelectBuilding : NeuroAction<Building>
	{
		public override string Name => "select_building";
		protected override string Description => $"Select a building to {BotHandler.Bot.FarmBuilding.CarpenterMenu.Action}." +
		                                         $" The building you select should be of the same type you selected in the last menu.";
		protected override JsonSchema Schema => new()
		{
			Type = JsonSchemaType.Object,
			Required = new List<string> { "building" },
			Properties = new Dictionary<string, JsonSchema>
			{
				["building"] = QJS.Enum(GetBuildings(BotHandler.CurrentLocation,out _))
			}
		};
		protected override ExecutionResult Validate(ActionData actionData, out Building? resultData)
		{
			string? buildingStr = actionData.Data?.Value<string>("building");

			resultData = null;
			if (buildingStr is null)
				return ExecutionResult.Failure($"You have provided a null value in building");

			var buildingSchemas = GetBuildings(BotHandler.CurrentLocation,out var buildings);
			int index = buildingSchemas.IndexOf(buildingStr);
			if (index == -1) return ExecutionResult.Failure($"You have provided an invalid building.");
			
			Building building = buildings[buildingSchemas.IndexOf(buildingStr)];
			if (!buildingSchemas.Contains(buildingStr)) 
				return ExecutionResult.Failure($"You have provided an invalid building.");
			
			if (building.buildingType.Value == BotHandler.Bot.FarmBuilding.BlueprintEntry?.UpgradeFrom ||
			    BotHandler.Bot.FarmBuilding.CarpenterMenu.Action == CarpenterMenu.CarpentryAction.Demolish)
			{
				resultData = building;
				return ExecutionResult.Success($"selected: {StringUtilities.GetBuildingName(building)}"); 
			}

			return ExecutionResult.Failure($"You have provided a tile that does not have a valid building in it.");
		}

		protected override void Execute(Building? resultData)
		{
			if (resultData is null) return;
			BotHandler.Bot.FarmBuilding.SelectBuilding(resultData);
		}
		public static List<string> GetBuildings(GameLocation location,  out List<Building> buildings, CarpenterMenu.CarpentryAction? imitateAction = null)
		{
			List<string> builds = new();
			List<string> usedBuildingTypes = new();
			buildings = new();
			foreach (var building in location.buildings)
			{
				switch (imitateAction ?? BotHandler.Bot.FarmBuilding.CarpenterMenu.Action)
				{
					case CarpenterMenu.CarpentryAction.Upgrade:
						if (building.buildingType.Value != BotHandler.Bot.FarmBuilding.CarpenterMenu.Blueprint.UpgradeFrom)
							continue;
						break;
					case CarpenterMenu.CarpentryAction.Demolish:
						// CanDemolishThis does not account for not being able to move buildings with animals in them.
						if (!BotHandler.Bot.FarmBuilding.CarpenterMenu.CanDemolishThis(building) ||
						     building.HasIndoors() && building.GetIndoors().Animals.Any()) continue;
						break;
					default:
						throw new ArgumentOutOfRangeException();
				}
				
				int buildingAmount = usedBuildingTypes.Count(str => str == building.buildingType.Value);
				string str = $"{StringUtilities.GetBuildingName(building)}{(buildingAmount > 0 ? $": {buildingAmount}" : "")}";
				usedBuildingTypes.Add(building.buildingType.Value);

				builds.Add(str);
				buildings.Add(building);
			}

			return builds;
		}
	}

	private class CanBuildOnTile : NeuroAction<Point>
	{
		public override string Name => "check_can_build";
		protected override string Description => $"Check if you can place a building at the specified tile, when you" +
		                                         $" specify a radius it can only be between {MaxRadius} and {MinRadius}.";
		protected override JsonSchema Schema => new()
		{
			Type = JsonSchemaType.Object,
			Required = new List<string> { "tile_x", "tile_y","radius" },
			Properties = new Dictionary<string, JsonSchema>
			{
				["tile_x"] = QJS.Type(JsonSchemaType.Integer),
				["tile_y"] = QJS.Type(JsonSchemaType.Integer),
				["radius"] = QJS.Type(JsonSchemaType.Integer)
			}
		};
		private int _radius;
		private const int MaxRadius = 15;
		private const int MinRadius = 1;
		protected override ExecutionResult Validate(ActionData actionData, out Point resultData)
		{
			int? x = actionData.Data?.Value<int>("tile_x");
			int? y = actionData.Data?.Value<int>("tile_y");
			int? rad = actionData.Data?.Value<int>("radius");

			resultData = new();
			if (x is null || y is null || rad is null)
			{
				return ExecutionResult.Failure($"You cannot provide a null value");
			}

			if (!TileUtilities.IsValidTile(new Point((int)x, (int)y),out var reason,false,false))
			{
				return ExecutionResult.Failure($"You can not check here as: {reason}");
			}

			if (rad is > 15 or < 1)
			{
				return ExecutionResult.Failure($"The radius can only be between 15 and 1");
			}

			_radius = (int)rad;
			resultData = new Point((int)x, (int)y);
			return ExecutionResult.Success($"{string.Join("\n",TileContext.GetTilesInLocation(BotHandler.CurrentLocation,resultData,_radius))}");
		}

		protected override async void Execute(Point resultData)
		{
			try
			{
				await RegisterPlaceBuilding();
			}
			catch (Exception e)
			{
				await TaskDispatcher.SwitchToMainThread();
				Logger.Error($"{e}");
				BotHandler.Bot.FarmBuilding.ExitPlacingBuilding();
				await Util.WaitForSeconds(2);
				RegisterStoreActions.RegisterCarpenterActions();
			}
		}
	}
	
	private class CancelPlacingBuilding : NeuroAction
	{
		public override string Name => "cancel_building";
		protected override string Description => "Cancel placing the current building.";
		protected override JsonSchema Schema => new();
		protected override ExecutionResult Validate(ActionData actionData)
		{
			return ExecutionResult.Success();
		}

		protected override void Execute()
		{
			BotHandler.Bot.FarmBuilding.InteractWithButton(BotHandler.Bot.FarmBuilding.CarpenterMenu.cancelButton);
			RegisterStoreActions.RegisterCarpenterActions();
		}
	}

	public static async Task RegisterPlaceBuilding(bool select = false)
	{
		// This stops from running SelectBuilding schema too early leading to incorrect schema
		await Util.WaitForSeconds(5);
		
		string state;
		ActionWindow window = ActionWindow.Create(Main.GameInstance);
		if (select)
		{
			var list = SelectBuilding.GetBuildings(BotHandler.CurrentLocation,out var buildings);
			if (list.Any())
			{
				window.SetContext(string.Join("\n",StringUtilities.GetAnimalsPerBuilding(buildings)),true);
				window.AddAction(new SelectBuilding());
			}
			state = $"You should either, select a valid building to " +
			        $"{(BotHandler.Bot.FarmBuilding.CarpenterMenu.Action == CarpenterMenu.CarpentryAction.Demolish ? "Demolish" : "Upgrade")}" +
			        $" or decide to cancel selecting a building. If you do not have an action for selecting a building that means there were no valid buildings.";
		}
		else
		{
			window.AddAction(new PlaceBuilding()).AddAction(new CanBuildOnTile());
			state = $"These are the important objects in this location, you may need to check if a building can still be" +
			        $" placed somewhere:";
			for (int x = 0; x < TileUtilities.MaxX; x++)
			{
				for (int y = 0; y < TileUtilities.MaxY; y++)
				{
					string? str = TileContext.GetTileContext(BotHandler.CurrentLocation,x,y);
					if (str is null || str.Contains("Weeds") || str.Contains("Stone")) continue; // remove litter
					state += $"\n{str}";
				}
			}
		}
		window.AddAction(new CancelPlacingBuilding());
		TileContext.SentFurniture.Clear();
		TileContext.SentBuildings.Clear();
		
		window.SetForce(5, $"You are now in {BotHandler.CurrentLocation.DisplayName}", state,true);
		window.Register();
	}
}