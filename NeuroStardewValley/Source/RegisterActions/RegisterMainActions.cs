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

		if (BotHandler.Bot.Inventory.Inventory.Any(item => item is not null && item.Category == -74))
		{
			window.AddAction(new PlantSeeds());
		}

		if (CreateItem.GetSchema().Any())
		{
			window.AddAction(new CreateItem());
		}


		if (GetQuestItem.GetSchema().Result.Any()) window.AddAction(new GetQuestItem());

		window.AddAction(new ChestActions.TakeItemFromChest()).AddAction(new ChestActions.AddItemToChest());
		
		// foreach (var kvp in DataLoader.Shops(Game1.content))
		// {
		// 	Logger.Info($"{kvp.Key}  {BotHandler.CurrentLocation.DisplayName == kvp.Key}");
		// 	foreach (var data in kvp.Value.Owners)
		// 	{
		// 		Logger.Info($"data: {data.Condition}    {data.Type}");
		// 	}
		// }
		window.AddAction(new ShopKeeperActions.InteractWithShopkeeper());
		
		if (TileContext.GetNameAmountInLocation(BotHandler.CurrentLocation).Any())
		{
			window.AddAction(new QueryWorldActions.GetObjectsInRadius())
				.AddAction(new QueryWorldActions.GetObjectTypeInRadius());
		}
		
		if (BotHandler.CurrentLocation.Objects.Any() || TileContext.ActionableTiles.Any() ||
		    BotHandler.CurrentLocation.buildings.Any() || BotHandler.CurrentLocation.furniture.Any())
		{
			window.AddAction(new InteractAtTile());
		}

		if (BotHandler.Farmer.Items.Any(item => item is not null && item.isPlaceable()))
		{
			window.AddAction(new WorldObjectActions.PlaceObjects()).AddAction(new WorldObjectActions.PlaceObject());
		}
		
		if (BotHandler.CurrentLocation.characters.Any(monster => monster.IsMonster))
		{
			window.AddAction(new PathFindingActions.AttackMonster());
		}
		
		if (BotHandler.CurrentLocation.characters.Any(character => !character.IsMonster))
		{
			window.AddAction(new PathFindingActions.FollowCharacter());
		}
		
		if (BotHandler.Farmer.CurrentItem is not null)
		{
			window.AddAction(new ToolActions.UseItem());
		}

		if (BotHandler.Farmer.questLog.Any())
		{
			window.AddAction(new QuestLogActions.OpenLog());
		}

		if (BotHandler.Farmer.CanEmote())
			window.AddAction(new ChatActions.UseEmote()).AddAction(new ChatActions.SendChatMessage());

		if (BotHandler.Bot.Debris.Debris.Any()) window.AddAction(new PickupItems());
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
				
				if (BotHandler.Bot.Inventory.Inventory.Any(item => item is WateringCan))
				{
					var wateringCan = BotHandler.Bot.Inventory.Inventory.OfType<WateringCan>().ToList()[0];
					
					if ((!wateringCan.isBottomless.Value || wateringCan.WaterLeft < wateringCan.waterCanMax)
					    && newLocation.waterTiles.waterTiles.Length > 0) 
						window.AddAction(new ToolActions.RefillWateringCan());

					if (Main.Config.UseRange) window.AddAction(new ToolActions.WaterFarmLandInRadius());
					else window.AddAction(new ToolActions.WaterFarmLand());
							
				}

				if (BotHandler.Bot.Inventory.Inventory.Any(item => item is Pickaxe or Axe or MeleeWeapon))
				{
					window.AddAction(new ToolActions.DestroyObject());
				}

				break;
			}
			case Mine:
			{
				if (BotHandler.Bot.Inventory.Inventory.Any(item => item.GetType() == typeof(Pickaxe)))
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
		foreach (var kvp in location.Objects.Pairs)
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

	/// <summary>
	/// When <see cref="RegisterPostAction"/> is ran next if this is true registering will be blocked then this will be set back to false.
	/// </summary>
	public static bool BlockRegistering { get; set; }
	public static void RegisterPostAction(BotWarpedEventArgs? e = null,int afterSeconds = 0,string query = "",string state = "",bool? ephemeral = null)
	{
		if (BlockRegistering)
		{
			BlockRegistering = false;
			return;
		}
		
		if (BotHandler.Farmer.IsSitting())
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
		RegisterToolActions(window,e,BotHandler.CurrentLocation);
		RegisterLocationActions(window,BotHandler.CurrentLocation);
		if (afterSeconds != 0 || query == "" || state == "" || ephemeral != null)
		{
			if (query == "")
			{
				query =
					$"You are at the tile {BotHandler.Bot.Player.BotTilePosition()} facing {PlayerContext.DirectionNames[BotHandler.Bot.Player.FacingDirection].ToLower()}," +
					$" if you are unsure about what's around you in the world, you should use the query actions to learn more." +
					$" The current weather is {BotHandler.Bot.WorldState.GetCurrentLocationWeather().Weather}." +
					$" These are the items in your inventory: {InventoryContext.GetInventoryString(BotHandler.Bot.Inventory.Inventory, true)}";
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
		var objects = TileContext.GetObjectsInLocation(BotHandler.CurrentLocation);
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