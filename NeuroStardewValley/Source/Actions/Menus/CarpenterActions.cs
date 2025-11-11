using Microsoft.Xna.Framework;
using NeuroSDKCsharp.Actions;
using NeuroSDKCsharp.Json;
using NeuroSDKCsharp.Websocket;
using NeuroStardewValley.Source.ContextStrings;
using NeuroStardewValley.Source.RegisterActions;
using NeuroStardewValley.Source.Utilities;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.GameData.Buildings;
using StardewValley.Menus;

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
				if (Main.Bot.FarmBuilding.CarpenterMenu!.Blueprints[i].DisplayName == blueprintEntry)
				{
					resultData = Main.Bot.FarmBuilding.CarpenterMenu.Blueprints[i];
				}
			}
			return ExecutionResult.Success($"You have selected {resultData.DisplayName}");
		}

		protected override void Execute(CarpenterMenu.BlueprintEntry? resultData)
		{
			Main.Bot.FarmBuilding.ChangeBuilding(resultData!);
			RegisterStoreActions.RegisterCarpenterActions();
		}

		private static IEnumerable<string> GetSchema()
		{
			List<string> nameList = new();
			if (Main.Bot.FarmBuilding.CarpenterMenu is null) return nameList;
			foreach (var blueprint in Main.Bot.FarmBuilding.CarpenterMenu.Blueprints)
			{
				nameList.Add(blueprint.DisplayName);
			}

			return nameList;
		}
	}
	
	public class CreateBuilding : NeuroAction
	{
		public override string Name => "create_building";
		protected override string Description => "This will move you to creating the building";
		protected override JsonSchema Schema => new();
		protected override ExecutionResult Validate(ActionData actionData)
		{
			if (Main.Bot.FarmBuilding.CarpenterMenu is null)
			{
				return ExecutionResult.ModFailure(string.Format(ResultStrings.ModVarFailure,Main.Bot.FarmBuilding.CarpenterMenu));
			}
			
			if (Main.Bot.FarmBuilding.CarpenterMenu.CanBuildCurrentBlueprint())
			{
				return ExecutionResult.Success($"building {Main.Bot.FarmBuilding.BlueprintEntry?.DisplayName}");
			}
			return ExecutionResult.Failure($"You cannot build this blueprint.");
		}

		protected override void Execute()
		{
			Main.Bot.FarmBuilding.InteractWithButton(Main.Bot.FarmBuilding.CarpenterMenu!.okButton);
			PlaceBuildingActions.RegisterPlaceBuilding();
		}
	}

	public class DemolishBuilding : NeuroAction
	{
		public override string Name => "demolish_building";
		protected override string Description => "Demolish the building of this type that is on your farm";
		protected override JsonSchema Schema => new();
		protected override ExecutionResult Validate(ActionData actionData)
		{
			if (Main.Bot.FarmBuilding.CarpenterMenu is null)
			{
				return ExecutionResult.ModFailure(string.Format(ResultStrings.ModVarFailure,Main.Bot.FarmBuilding.CarpenterMenu));
			}
				
			if (Main.Bot.FarmBuilding.CarpenterMenu.CanDemolishThis())
			{
				return ExecutionResult.Success();
			}
			return ExecutionResult.Failure($"You cannot destroy this building");
		}

		protected override void Execute()
		{
			Main.Bot.FarmBuilding.InteractWithButton(Main.Bot.FarmBuilding.CarpenterMenu!.demolishButton);
			PlaceBuildingActions.RegisterPlaceBuilding(true);
		}
	}

	public class UpgradeBuilding : NeuroAction
	{
		public override string Name => "upgrade_building";
		protected override string Description => "Upgrade the currently selected building.";
		protected override JsonSchema Schema => new();
		protected override ExecutionResult Validate(ActionData actionData)
		{
			if (Main.Bot.FarmBuilding.CarpenterMenu is null)
			{
				return ExecutionResult.Failure(string.Format(ResultStrings.ModVarFailure,"Main.Bot.FarmBuilding.CarpenterMenu"));
			}
			if (Main.Bot.FarmBuilding.BlueprintEntry is null) return ExecutionResult.Failure($"There is not a blueprint entry currently.");
			if (!Main.Bot.FarmBuilding.CarpenterMenu!.CanBuildCurrentBlueprint())
			{
				return ExecutionResult.Failure($"You cannot build the: {Main.Bot.FarmBuilding.BlueprintEntry?.DisplayName}");
			}
			return ExecutionResult.Success();
		}

		protected override void Execute()
		{
			Main.Bot.FarmBuilding.InteractWithButton(Main.Bot.FarmBuilding.CarpenterMenu!.okButton);
			PlaceBuildingActions.RegisterPlaceBuilding(true);
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
			
			foreach (var skin in Main.Bot.FarmBuilding.GetBuildingSkins().Where(skin => skin.Index.ToString() == selectedSkin))
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
			Main.Bot.FarmBuilding.InteractWithButton(Main.Bot.FarmBuilding.CarpenterMenu!.appearanceButton);
			Main.Bot.FarmBuilding.ChangeSkin(resultData!);
			RegisterStoreActions.RegisterCarpenterActions();
		}

		private static List<string> GetSchema()
		{
			List<string> strings = new();
			foreach (var skin in Main.Bot.FarmBuilding.GetBuildingSkins())
			{
				strings.Add($"{skin.Index}");
			}

			return strings;
		}
	}

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

			Main.Bot.FarmBuilding.SetSelectedTile(new Point(x,y));
			if (!Main.Bot.FarmBuilding.CanPlaceBuilding(Main.Bot.FarmBuilding.Building))
			{
				return ExecutionResult.Failure($"You cannot build at {x},{y}.");
			}
			
			resultData = new (x,y);
			return ExecutionResult.Success();
		}

		protected override void Execute(Point resultData) // This is run in validation
		{
			Main.Bot.FarmBuilding.CreateBuilding(resultData); // one day maybe replicate how it checks if you can build so this isn't in validate. A bit lazy for that rn.
		}
	}

	private class SelectBuilding : NeuroAction<Building>
	{
		public override string Name => "select_building";
		protected override string Description => $"Select a building to {Main.Bot.FarmBuilding.CarpenterMenu?.Action}." +
		                                         $" The building you select should be of the same type you selected in the last menu.";
		protected override JsonSchema Schema => new()
		{
			Type = JsonSchemaType.Object,
			Required = new List<string> { "building" },
			Properties = new Dictionary<string, JsonSchema>
			{
				["building"] = QJS.Enum(GetSchema())
			}
		};
		protected override ExecutionResult Validate(ActionData actionData, out Building? resultData)
		{
			string? buildingStr = actionData.Data?.Value<string>("building");

			if (buildingStr is null)
			{
				resultData = null;
				return ExecutionResult.Failure($"You have provided a null value in building");
			}

			Building? building = GetBuilding(buildingStr);
			if (!GetSchema().Contains(buildingStr) || building is null)
			{
				resultData = null;
				return ExecutionResult.Failure($"You have provided an invalid value in building");
			}
			
			if (building.buildingType.Value == Main.Bot.FarmBuilding.BlueprintEntry?.UpgradeFrom)
			{
				resultData = building;
				return ExecutionResult.Success($"selected: {StringUtilities.GetBuildingName(building)}"); 
			}

			resultData = null;
			return ExecutionResult.Failure($"You have provided a tile that does not have a valid building in it.");
		}

		protected override void Execute(Building? resultData)
		{
			if (resultData is null) return;
			Main.Bot.FarmBuilding.SelectBuilding(resultData);
		}

		private static List<string> GetSchema()
		{
			if (Main.Bot.FarmBuilding.CarpenterMenu is null) return new();
			List<string> names = new();
			List<Building> buildings;
			if (Main.Bot.FarmBuilding.CarpenterMenu.Action == CarpenterMenu.CarpentryAction.Upgrade)
			{
				buildings = Game1.currentLocation.buildings.Where(building =>
					building.buildingType.Value == Main.Bot.FarmBuilding.CarpenterMenu.Blueprint.UpgradeFrom).ToList();
			}
			else
			{
				buildings = Game1.currentLocation.buildings.ToList();
			}
			foreach (var building in buildings)
			{
				names.Add($"{StringUtilities.GetBuildingName(building)}  pos: {building.tileX.Value},{building.tileY.Value}");
			}

			return names;
		}

		private static Building? GetBuilding(string str)
		{
			foreach (var building in Game1.currentLocation.buildings)
			{
				if ($"{StringUtilities.GetBuildingName(building)}  pos: {building.tileX.Value},{building.tileY.Value}" == str)
				{
					return building;
				}
			}

			return null;
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
			return ExecutionResult.Success($"{string.Join("\n",TileContext.GetTilesInLocation(Main.Bot._currentLocation,resultData,_radius))}");
		}

		protected override void Execute(Point resultData)
		{
			RegisterPlaceBuilding();
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
			Main.Bot.FarmBuilding.InteractWithButton(Main.Bot.FarmBuilding.CarpenterMenu!.cancelButton);
			RegisterStoreActions.RegisterCarpenterActions();
		}
	}

	public static void RegisterPlaceBuilding(bool select = false)
	{
		// This stops from running SelectBuilding schema too early leading to incorrect schema
		var task = Task.Run(async () => await Util.WaitForSeconds(5));
		task.Wait();
		
		ActionWindow window = ActionWindow.Create(Main.GameInstance);
		if (select)
		{
			window.AddAction(new SelectBuilding());
		}
		else
		{
			window.AddAction(new PlaceBuilding());
		}
		window.AddAction(new CancelPlacingBuilding()).AddAction(new CanBuildOnTile());
		string state = "";
		for (int x = 0; x < TileUtilities.MaxX; x++)
		{
			for (int y = 0; y < TileUtilities.MaxY; y++)
			{
				string? str = TileContext.GetTileContext(Main.Bot._currentLocation,x,y);
				if (str is null || str.Contains("Weeds") || str.Contains("Stone")) continue; // remove litter
				state += $"\n{str}";
			}
		}
		TileContext.SentFurniture.Clear();
		TileContext.SentBuildings.Clear();
		
		window.SetForce(5, $"You are now in {Main.Bot._currentLocation.DisplayName}", $"These" +
			$" are the important objects in this location, you may need to check if a building can still be" +
			$" placed somewhere: {state}",true);
		window.Register();
	}
}