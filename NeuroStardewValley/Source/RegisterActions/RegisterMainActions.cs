using Microsoft.Xna.Framework;
using NeuroSDKCsharp.Actions;
using NeuroStardewValley.Debug;
using NeuroStardewValley.Source.Actions;
using NeuroStardewValley.Source.Actions.Menus;
using NeuroStardewValley.Source.Actions.ObjectActions;
using NeuroStardewValley.Source.Actions.WorldQuery;
using NeuroStardewValley.Source.ContextStrings;
using StardewBotFramework.Source.Events.EventArgs;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.Locations;
using StardewValley.Objects;
using StardewValley.Tools;

namespace NeuroStardewValley.Source.RegisterActions;

public static class RegisterMainActions
{
	#region AddActions

	private static void RegisterActions(ActionWindow window)
	{
		window.AddAction(new PathFindingActions.Pathfinding()).AddAction(new PathFindingActions.PathFindToExit())
			.AddAction(new InventoryActions.OpenInventory()).AddAction(new InventoryActions.ChangeSelectedToolbarSlot());

		if (Main.Config.WaitTimeAction)
		{
			window.AddAction(new WaitForTime());
		}

		if (TileContext.GetNameAmountInLocation(Main.Bot._currentLocation).Any())
		{
			window.AddAction(new QueryWorldActions.GetObjectsInRadius())
				.AddAction(new QueryWorldActions.GetObjectTypeInRadius());
		}
		
		if (Main.Bot._currentLocation.Objects.Any() || TileContext.ActionableTiles.Any() ||
		    Main.Bot._currentLocation.buildings.Any() || Main.Bot._currentLocation.furniture.Any())
		{
			window.AddAction(new InteractAtTile());
		}

		if (Main.Bot._farmer.Items.Any(item => item is not null && item.isPlaceable()))
		{
			window.AddAction(new WorldObjectActions.PlaceObjects()).AddAction(new WorldObjectActions.PlaceObject());
		}
		
		if (Main.Bot._currentLocation.characters.Any(monster => monster.IsMonster))
		{
			window.AddAction(new PathFindingActions.AttackMonster());
		}
		
		if (Main.Bot._currentLocation.characters.Any(character => !character.IsMonster))
		{
			window.AddAction(new PathFindingActions.InteractCharacter());
		}
		
		if (Main.Bot._farmer.CurrentItem is not null)
		{
			window.AddAction(new ToolActions.UseItem());
		}

		if (Main.Bot._farmer.questLog.Any())
		{
			window.AddAction(new QuestLogActions.OpenLog());
		}

		if (Main.Bot._farmer.CanEmote()) window.AddAction(new ChatActions.UseEmote());
	}

	private static void RegisterToolActions(ActionWindow window, BotWarpedEventArgs? e = null,GameLocation? location = null)
	{
		GameLocation? newLocation = location ?? e?.NewLocation;
		if (newLocation is null) return;
		
		switch (newLocation)
		{
			case Farm:
			{
				if (Main.Config.UseRange) window.AddAction(new ToolActions.UseToolInRadius());
				else window.AddAction(new ToolActions.UseToolInRect());
				
				if (Main.Bot.Inventory.Inventory.Any(item => item is WateringCan))
				{
					var wateringCan = Main.Bot.Inventory.Inventory.OfType<WateringCan>().ToList()[0];
					
					if ((!wateringCan.isBottomless.Value || wateringCan.WaterLeft < wateringCan.waterCanMax)
					    && newLocation.waterTiles.waterTiles.Length > 0) 
						window.AddAction(new ToolActions.RefillWateringCan());

					if (Main.Config.UseRange) window.AddAction(new ToolActions.WaterFarmLandInRadius());
					else window.AddAction(new ToolActions.WaterFarmLand());
							
				}

				if (Main.Bot.Inventory.Inventory.Any(item => item is Pickaxe or Axe or MeleeWeapon))
				{
					window.AddAction(new ToolActions.DestroyObject());
				}

				break;
			}
			case Mine:
			{
				if (Main.Bot.Inventory.Inventory.Any(item => item.GetType() == typeof(Pickaxe)))
				{
					window.AddAction(new ToolActions.DestroyObject());
				}
				
				break;
			}
		}
	}

	private static void RegisterLocationActions(ActionWindow window,GameLocation location)
	{
		bool madeChestAction = false;
		foreach (var dict in location.Objects)
		{
			foreach (var kvp in dict)
			{
				switch (kvp.Value)
				{
					case Chest:
						if (!madeChestAction && Game1.activeClickableMenu is null)
						{
							window.AddAction(new ChestActions.OpenChest());
							madeChestAction = true;
						}
						break;
				}
			}
		}
		
		List<Point> propertyTile = WorldObjectActions.InteractWithTileProperty.GetSchema();
		switch (location)
		{
			case Farm farm:
				if (farm.buildings.Any(building => building.GetType() == typeof(ShippingBin)))
				{
					window.AddAction(new UseShippingBin());
				}

				if (farm.Objects.Values.Any(obj => obj.GetMachineData() != null))
				{
					window.AddAction(new UseMachine());
				}

				break;
			case AnimalHouse:
				if (!propertyTile.Any()) break;
				NeuroSDKCsharp.Messages.Outgoing.Context.Send($"These are the locations of troughs in this area," +
				                                              $" you can should put hay in them to feed your animals: {string.Join(",",propertyTile)}");
				
				window.AddAction(new WorldObjectActions.InteractWithTileProperty("interact_with_trough"));
				break;
			case MineShaft:
				if (!propertyTile.Any()) break;
				NeuroSDKCsharp.Messages.Outgoing.Context.Send($"These are the locations of the ladders in this cave," +
				                                              $" you may need to destroy the object over them to use them." +
				                                              $" {string.Join(",",propertyTile)}");

				window.AddAction(new WorldObjectActions.InteractWithTileProperty("interact_with_ladder"));
				break;
		}
	}

	#endregion

	public static void RegisterPostAction(BotWarpedEventArgs? e = null,int afterSeconds = 0,string query = "",string state = "",bool? ephemeral = null)
	{
		if (Main.Bot._farmer.IsSitting())
		{
			var actionWindow = ActionWindow.Create(Main.GameInstance);
			actionWindow.AddAction(new WorldObjectActions.StopSitting()).Register();
			NeuroSDKCsharp.Messages.Outgoing.Context.Send($"You are currently sitting, if you would like to get up you should use the stop sitting action.");
			return;
		}
		if (!Context.IsPlayerFree) return;
		
		Logger.Info($"register actions again.");
		ActionWindow window = ActionWindow.Create(Main.GameInstance);
		RegisterActions(window);
		RegisterToolActions(window,e,Main.Bot._currentLocation);
		RegisterLocationActions(window,Main.Bot._currentLocation);
		if (afterSeconds != 0 || query == "" || state == "" || ephemeral != null)
		{
			if (query == "")
			{
				query =
					$"You are at the tile {Main.Bot.Player.BotTilePosition()} facing {PlayerContext.DirectionNames[Main.Bot.Player.FacingDirection].ToLower()}," +
					$" if you are unsure about what's around you in the world, you should use the query actions to learn more." +
					$" The current weather is {Main.Bot.WorldState.GetCurrentLocationWeather().Weather}." +
					$" These are the items in your inventory: {InventoryContext.GetInventoryString(Main.Bot.Inventory.Inventory, true)}";
			}
			if (state == "")
			{
				state = GetSeparatedState();
			}
			window.SetForce(afterSeconds, query, state, ephemeral is null || ephemeral.Value);
		}
		window.Register();
	}

	private const string ObjectPrefix = "These are the objects around you: {0}{1}";
	private static string GetSeparatedState()
	{
		var objects = TileContext.GetObjectsInLocation(Main.Bot._currentLocation);
		Dictionary<string,int> nameAmount = TileContext.GetNameAmountInLocation(objects);
		string context = "";
		string building = "";
		foreach (var kvp in objects)
		{
			string name = TileContext.SimpleObjectName(kvp.Value);
			if (name.Length == 0 || name.Contains("Error",StringComparison.Ordinal)) continue;
			switch (kvp.Value)
			{
				case Building:
					if (building.Contains(name)) continue;
					building += $"\n{name} amount: {nameAmount[name]}";
					break;
				default:
					if (context.Contains(name)) continue;
					context += $"\n{name} amount: {nameAmount[name]}";
					break;
			}
		}
		if (building.Length == 0) return string.Format(ObjectPrefix,context, "");
		
		building = $"\nThese are the buildings around you: {building}";
		context = string.Format(ObjectPrefix,context, building);
		return context;
	}
}