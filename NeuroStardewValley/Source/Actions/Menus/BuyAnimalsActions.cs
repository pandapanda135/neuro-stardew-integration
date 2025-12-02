using NeuroSDKCsharp.Actions;
using NeuroSDKCsharp.Json;
using NeuroSDKCsharp.Websocket;
using NeuroStardewValley.Debug;
using NeuroStardewValley.Source.Utilities;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.Menus;

namespace NeuroStardewValley.Source.Actions.Menus;

public static class BuyAnimalsActions
{
	private class SelectAnimal : NeuroAction<ClickableTextureComponent>
	{
		public override string Name => "select_animal";
		protected override string Description => "Select the animal you want to buy.";
		protected override JsonSchema Schema => new()
		{
			Type = JsonSchemaType.Object,
			Required = new List<string> { "animal" },
			Properties = new Dictionary<string, JsonSchema>
			{
				["animal"] = QJS.Enum(BotHandler.Bot.AnimalMenu.GetAvailableButtons().Select(cc => cc.hoverText))
			}
		};
		protected override ExecutionResult Validate(ActionData actionData, out ClickableTextureComponent? resultData)
		{
			string? animalString = actionData.Data?.Value<string>("animal");

			resultData = null;
			if (animalString is null)
			{
				return ExecutionResult.Failure($"You gave a null value in animal");
			}

			int index = BotHandler.Bot.AnimalMenu.GetAvailableButtons().Select(cc => cc.hoverText).ToList().IndexOf(animalString);
			if (index == -1)
			{
				return ExecutionResult.Failure($"The animal you gave is not a valid option.");
			}

			ClickableTextureComponent cc = BotHandler.Bot.AnimalMenu.GetAvailableButtons()[index];
			if (cc.item.salePrice() > BotHandler.Bot.PlayerInformation.Money)
			{
				return ExecutionResult.Failure($"You do not have enough money to buy this animal.");
			}

			resultData = cc;
			return ExecutionResult.Success($"Selecting {animalString}");
		}

		protected override void Execute(ClickableTextureComponent? resultData)
		{
			if (resultData is null) return;
			
			BotHandler.Bot.AnimalMenu.SelectAnimal(resultData);
			RegisterActions(3);
		}
	}

	private class ExitMenu : NeuroAction
	{
		public override string Name => "exit_menu";
		protected override string Description => "Exit the menu";
		protected override JsonSchema Schema => new();
		protected override ExecutionResult Validate(ActionData actionData)
		{
			return ExecutionResult.Success();
		}

		protected override void Execute()
		{
			if (BotHandler.Bot.AnimalMenu.Menu.onFarm)
			{
				BotHandler.Bot.AnimalMenu.ExitFarmMenu();
				return;
			}

			BotHandler.Bot.AnimalMenu.RemoveMenu();
		}
	}

	private class SelectBuilding : NeuroAction<Building>
	{
		public override string Name => "select_building";
		protected override string Description => "Select the building to put this animal in.";
		protected override JsonSchema Schema => new()
		{
			Type = JsonSchemaType.Object,
			Required = new List<string> { "building" },
			Properties = new Dictionary<string, JsonSchema>
			{
				["building"] = QJS.Enum(GetSchema(out _))
			}
		};
		protected override ExecutionResult Validate(ActionData actionData, out Building? resultData)
		{
			string? buildingString = actionData.Data?.Value<string>("building");

			resultData = null;
			if (buildingString is null)
			{
				return ExecutionResult.Failure($"building cannot be null");
			}

			int index = GetSchema(out var buildings).ToList().IndexOf(buildingString);
			if (index == -1)
			{
				return ExecutionResult.Failure($"The building you gave was not valid.");
			}

			Building building = buildings[index];
			if (!BotHandler.Bot.AnimalMenu.BuildingCheck(building, BotHandler.Bot.AnimalMenu.Menu.animalBeingPurchased!))
			{
				return ExecutionResult.Failure(string.Format(ResultStrings.ModVarFailure,"BuildingCheck"));
			}
			resultData = building;
			return ExecutionResult.Success($"You have selected a {StringUtilities.GetBuildingName(building)}");
		}

		protected override void Execute(Building? resultData)
		{
			if (resultData is null) return;
			BotHandler.Bot.AnimalMenu.SelectBuilding(resultData);
			RegisterActions();
		}

		public static IEnumerable<string> GetSchema(out List<Building> buildings)
		{
			List<string> builds = new();
			List<string> usedBuildingTypes = new();
			buildings = new();
			foreach (var building in BotHandler.Bot.AnimalMenu.GetAvailableBuildings())
			{
				int buildingAmount = usedBuildingTypes.Count(str => str == building.buildingType.Value);
				string str = $"{StringUtilities.GetBuildingName(building)}{(buildingAmount > 0 ? $": {buildingAmount}" : "")}";
				usedBuildingTypes.Add(building.buildingType.Value);

				builds.Add(str);
				buildings.Add(building);	
			}

			return builds;
		}
	}

	#region Naming
	private class NameAnimal : NeuroAction<string>
	{
		public override string Name => "name_animal";
		protected override string Description => "Select a name for this animal, you should try to avoid duplicates and long names.";
		protected override JsonSchema Schema => new()
		{
			Type = JsonSchemaType.Object,
			Required = new List<string> { "name" },
			Properties = new Dictionary<string, JsonSchema>
			{
				["name"] = new()
				{
					Type = JsonSchemaType.String,
					MaxLength = 20, // Does not have defined text limit as is based on font size :)
					MinLength = 1
				}
			}
		};
		protected override ExecutionResult Validate(ActionData actionData, out string? resultData)
		{
			string? nameString = actionData.Data?.Value<string>("name");

			resultData = null;
			if (string.IsNullOrEmpty(nameString))
			{
				return ExecutionResult.Failure($"If you want to name an animal you must provide a string value.");
			}

			if (!BotHandler.Bot.AdheresToTextBoxLimit(nameString, BotHandler.Bot.AnimalMenu.Menu.textBox))
			{
				return ExecutionResult.Failure($"{nameString} is too long, you should try a different name next time.");
			}

			if (Utility.areThereAnyOtherAnimalsWithThisName(nameString))
			{
				return ExecutionResult.Failure($"Another animal already has that name, you should try another name.");
			}

			resultData = nameString;
			return ExecutionResult.Success();
		}

		protected override async void Execute(string? resultData)
		{
			try
			{
				if (string.IsNullOrEmpty(resultData))
				{
					RegisterActions();
					return;
				}
				
				BotHandler.Bot.AnimalMenu.NameAnimal(resultData);
				await Util.WaitForSeconds(3);
				BotHandler.Bot.AnimalMenu.ConfirmName();
			}
			catch (Exception e)
			{
				Logger.Error($"There was an issue in NameAnimal:\n{e}");
				RegisterActions();
			}
		}
	}

	private class RandomName : NeuroAction
	{
		public override string Name => "randomise_name";
		protected override string Description => "Randomise the name of this animal, If you do not like the random name" +
		                                         " you will be able to change it.";
		protected override JsonSchema Schema => new();
		protected override ExecutionResult Validate(ActionData actionData)
		{
			return ExecutionResult.Success();
		}

		protected override void Execute()
		{
			BotHandler.Bot.AnimalMenu.RandomName();
			RegisterActions();
		}
	}

	private class AcceptName : NeuroAction
	{
		public override string Name => "accept_name";
		protected override string Description => "Accept the currently proposed name of the animal";
		protected override JsonSchema Schema => new();
		protected override ExecutionResult Validate(ActionData actionData)
		{
			return ExecutionResult.Success($"You have selected the name: {BotHandler.Bot.AnimalMenu.Menu.textBox.Text}, for the {BotHandler.Bot.AnimalMenu.Menu.animalBeingPurchased.displayType}.");
		}

		protected override void Execute()
		{
			BotHandler.Bot.AnimalMenu.ConfirmName();
		}
	}
	#endregion

	public static void RegisterActions(int waitTime = 0)
	{
		ActionWindow window = ActionWindow.Create(Main.GameInstance);
		string stateString;
		string queryString;
		if (BotHandler.Bot.AnimalMenu.Menu.onFarm && !BotHandler.Bot.AnimalMenu.Menu.namingAnimal)
		{
			queryString = "You are selecting the building for the animal to be in.";
			stateString = "None of the buildings in this location are valid.";
			if (SelectBuilding.GetSchema(out var buildings).Any())
			{
				string animalAmount = string.Join("\n",StringUtilities.GetAnimalsPerBuilding(buildings,
					(b, _, _) => $"\n-- Position: {TileUtilities.BuildingTile(b)}"));
				stateString = $"These are the other animal in the valid buildings:\n{animalAmount}";
				window.AddAction(new SelectBuilding());
			}
			window.AddAction(new ExitMenu());
		}
		else if (BotHandler.Bot.AnimalMenu.Menu.onFarm)
		{
			queryString = "You are selecting a name for the animal.";
			stateString = $"The animal's current name is: {BotHandler.Bot.AnimalMenu.Menu.textBox.Text}, you can either change this or accept it.";
			window.AddAction(new NameAnimal()).AddAction(new RandomName()).AddAction(new AcceptName());
		}
		else
		{
			List<ClickableTextureComponent> cc = BotHandler.Bot.AnimalMenu.GetAvailableButtons(); 
			queryString = "You are selecting an animal to buy";
			stateString = $"You have {BotHandler.Bot.PlayerInformation.Money} gold.";
			stateString += $"\n{string.Concat(cc.Select(c => $"{c.hoverText} price: {c.item.salePrice()}"))}";
			window.AddAction(new SelectAnimal()).AddAction(new ExitMenu());
		}
		
		window.SetForce(waitTime, queryString, stateString);
		window.Register();
	}
}