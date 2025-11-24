using NeuroSDKCsharp.Messages.Outgoing;
using NeuroStardewValley.Debug;
using NeuroStardewValley.Source.Actions;
using NeuroStardewValley.Source.Actions.Menus;
using NeuroStardewValley.Source.Actions.ObjectActions;
using NeuroStardewValley.Source.ContextStrings;
using NeuroStardewValley.Source.RegisterActions;
using NeuroStardewValley.Source.Utilities;
using StardewBotFramework.Source.Events.EventArgs;
using StardewBotFramework.Source.Events.World_Events;
using StardewBotFramework.Source.Modules.Pathfinding.Algorithms;
using StardewBotFramework.Source.Modules.Pathfinding.Base;
using StardewModdingAPI.Utilities;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.Menus;
using StardewValley.Objects;

namespace NeuroStardewValley.Source.EventMethods;

public static class MainGameLoopEvents
{
	#region RegisterActions

	public static void OnWarped(object? sender, BotWarpedEventArgs e)
	{
		TileContext.ActionableTiles.Clear();
		AlgorithmBase.IPathing.CollisionMap.Clear();
		Main.Bot.Pathfinding.BuildCollisionMap();

		if (e.Player.passedOut || Game1.eventUp) return;
		string warps = TileContext.GetWarpTiles(e.NewLocation,true);
		string warpsString = !string.IsNullOrEmpty(warps) ? TileContext.GetWarpTilesString(warps) : "There are no warps in this location";
		
		string characterContext = string.Concat(Main.Bot.Characters.GetCharactersInCurrentLocation(e.NewLocation)
			.Select(kvp => $"\n{kvp.Value.displayName} is at {kvp.Key}").ToList());

		characterContext = characterContext.Length < 1
			? $"There are no characters in {e.NewLocation.DisplayName} as of when you entered it."
			: $"These are the characters in {e.NewLocation.DisplayName} when you entered it: {characterContext}";
		
		warpsString = warpsString.Length < 1
			? $"There are no warps in {e.NewLocation.DisplayName} as of when you entered it"
			: $"These are the warps to other places in {e.NewLocation.DisplayName} when you entered it: {warpsString}";
		
		Context.Send($"{warpsString}\n{characterContext}", true);
		string query =
			$"You are at the tile {Main.Bot.Player.BotTilePosition()} facing {PlayerContext.DirectionNames[Main.Bot.Player.FacingDirection].ToLower()}," +
			$" if you are unsure about what's around you in the world, you should use the query actions to learn more." +
			$"You are at {e.NewLocation.DisplayName} from {e.OldLocation.DisplayName}, The current weather is {Main.Bot.WorldState.GetCurrentLocationWeather().Weather}." +
			$" These are the items in your inventory: {InventoryContext.GetInventoryString(Main.Bot._farmer.Items, true)} " +
			$"\nIf you want more information about your items should open your inventory."; 
		RegisterMainActions.RegisterPostAction(e, 0, query);
	}

	public static void OnMenuChanged(object? sender, BotMenuChangedEventArgs e)
	{
		Logger.Info($"current menu: {e.NewMenu}");
		switch (e.OldMenu)
		{
			// handled by caughtFish event
			case BobberBar:
				return;
			// we need to check if old is dialogue box to stop issues with changing menus while standing in bed
			case DialogueBox when Game1.player.isInBed.Value:
				if (!Game1.fadeToBlack) break; // in case says no to going to sleep
				return;
			case DialogueBox when e.NewMenu is not DialogueBox:
				Main.Bot.Dialogue.CurrentDialogueBox = null;
				break;
			case LevelUpMenu: // lower thing doesn't work, and I'm too lazy to find out why
				return;
		}

		if (RegisterMainActions.BlockRegistering)
		{
			RegisterMainActions.BlockRegistering = false;
			return;
		}

		switch (e.NewMenu)
		{
			case CharacterCustomization customization:
				Main.Bot.CharacterCreation.SetCreator(customization);
				MainMenuActions.RegisterAction();
				break;
			case DialogueBox dialogueBox:
				Logger.Info($"add new dialogue box");
				Main.Bot.Dialogue.CurrentDialogueBox = dialogueBox;
				RegisterDialogueActions.RegisterActions();
				break;
			case GameMenu menu:
				switch (menu.GetCurrentPage())
				{
					case InventoryPage:
						InventoryActions.RegisterInventoryActions();
						break;
					case CraftingPage:
						CraftingActions.RegisterActions();
						break;
				}
				break;
			case ShopMenu shopMenu:
				Main.Bot.Shop.OpenShop(shopMenu); // this should also be handled by OpenShopUi
				RegisterStoreActions.RegisterDefaultShop();
				break;
			case CarpenterMenu carpenterMenu:
				Main.Bot.FarmBuilding.SetCarpenterUi(carpenterMenu);
				RegisterStoreActions.RegisterCarpenterActions();
				break;
			case GeodeMenu geodeMenu:
				Main.Bot.Blacksmith.OpenGeodeMenu(geodeMenu);
				RegisterStoreActions.RegisterBlacksmithActions();
				break;
			case LevelUpMenu levelUpMenu:
				Main.Bot.EndDaySkillMenu.SetMenu(levelUpMenu);
				RegisterLevelUpMenu.GetSkillContext();
				break;
			case ShippingMenu shippingMenu:
				Main.Bot.EndDayShippingMenu.SetMenu(shippingMenu);
				break;
			case ItemGrabMenu itemGrabMenu:
				switch (itemGrabMenu.context)
				{
					case Chest chest:
						Main.Bot.ItemGrabMenu.SetUI(itemGrabMenu);
						Main.Bot.Chest.SetChest(chest);
						ChestActions.Chest = chest;
						ChestActions.RegisterChestActions();
						return;
					case ShippingBin:
						Main.Bot.ShippingBinInteraction.SetUI(itemGrabMenu);
						ShippingBinActions.RegisterBinActions();
						return;
				}

				Main.Bot.ItemGrabMenu.SetUI(itemGrabMenu);
				ItemGrabActions.RegisterActions(itemGrabMenu);
				break;
			case Billboard billboard:
				Main.Bot.BillBoard.SetMenu(billboard);
				if (billboard.acceptQuestButton.visible)
				{
					BillBoardInteraction.RegisterQuestActions();
				}
				else
				{
					Context.Send($"These are the event that are happening this season, {BillBoardInteraction.GetCalendarContext()}" +
					             $"\nThere are: {billboard.calendarDays.Count} days in this season. It is currently day {SDate.Now().Day} of {SDate.Now().Season}.");
					DelayedAction.functionAfterDelay(() => Main.Bot.BillBoard.RemoveMenu(), 6500);
				}
				break;
			case LetterViewerMenu letterViewerMenu:
				Main.Bot.LetterViewer.SetMenu(letterViewerMenu);
				
				if (letterViewerMenu.HasQuestOrSpecialOrder || letterViewerMenu.itemsLeftToGrab())
				{
					LetterActions.RegisterActions();
				}
				else
				{
					int waitTime = 9000 * Main.Bot.LetterViewer.GetMessage().Count;
					for (int i = 0; i < Main.Bot.LetterViewer.GetMessage().Count; i++)
					{
						int j = i;
						// ugly, but it works, sorry.
						Task.Run(async () =>
						{
							await TaskDispatcher.SwitchToMainThread();
							Logger.Info($"{Main.Bot.LetterViewer.GetMessage().Count}");
							string message = LetterContext.GetStringContext(Main.Bot.LetterViewer.GetMessage()[j],
								j == Main.Bot.LetterViewer.GetMessage().Count - 1, j); // only send extra on last page
							Context.Send($"{message}");

							await Util.WaitForSeconds(9);
							if (j != Main.Bot.LetterViewer.GetMessage().Count - 1)
							{
								Main.Bot.LetterViewer.NextPage();
							}
						});
					}

					// this gets ran after all the tasks are set up
					DelayedAction.functionAfterDelay(() => Main.Bot.LetterViewer.ClickCloseButton(), waitTime + 9000);
				}

				break;
			case JunimoNoteMenu junimoNoteMenu:
				Main.Bot.JunimoNote.SetMenu(junimoNoteMenu);
				JunimoNoteActions.RegisterActions();
				break;
			case PurchaseAnimalsMenu animalsMenu:
				Main.Bot.AnimalMenu.SetUI(animalsMenu);
				BuyAnimalsActions.RegisterActions();
				break;
			case ItemListMenu itemListMenu:
				Main.Bot.ItemListMenu.SetMenu(itemListMenu);
				ItemListMenuActions.RegisterActions();
				break;
			case MineElevatorMenu mineElevatorMenu:
				Main.Bot.ElevatorMenu.SetMenu(mineElevatorMenu);
				ElevatorMenuActions.RegisterAction();
				break;
			case NamingMenu namingMenu: // works for naming horses and placing signs
				Main.Bot.NamingMenu.setUI(namingMenu);
				NamingMenuActions.RegisterActions();
				break;
			case QuestLog questLog:
				Main.Bot.QuestLog.SetMenu(questLog);
				QuestLogActions.RegisterActions();
				break;
			default:
				if (e.NewMenu is null or BobberBar || Main.Config.Debug) break;
				Context.Send(string.Format(ResultStrings.InvalidClickableMenu,$"{e.NewMenu}"));
				e.NewMenu.exitThisMenu(false);
				break;
		}
		
		// ugly but it gets rid of warning and double send at start of game and other double sends
		if (e.NewMenu is not null || e.OldMenu is MineElevatorMenu || e.OldMenu is TitleMenu || e.OldMenu is LevelUpMenu) return;
		
		Logger.Info($"Re-registering post action: old menu: {e.OldMenu}  new: {e.NewMenu}");
		RegisterMainActions.RegisterPostAction();
	}
	
	// may need to check what event was ended in the future
	public static void EventFinished(object? sender, EventEndedEventArgs e)
	{
		Logger.Info($"Running event finished: event: {e.Event}  can move after: {e.Event.canMoveAfterDialogue()}");
		if (e.Event.exitLocation is not null)
		{
			Logger.Error($"exit location: {e.Event.exitLocation.Name}  {e.Event.exitLocation.Location}  {e.Event.exitLocation.IsRequestFor(e.Event.exitLocation.Location)}");
			return; // player should get warped after event
		}

		DelayedAction.functionAfterDelay(() =>
		{
			Logger.Info($"post event task delay");
			RegisterMainActions.RegisterPostAction();
		}, 1500);
	}

	#endregion

	#region ContextEvents

	public static void OnDayStarted(object? sender, BotDayStartedEventArgs e)
	{
		Context.Send(NewDayContext());
		AlgorithmBase.IPathing pathing = new AStar.Pathing();
		// stop ugly stuttering
		pathing.BuildCollisionMap(Main.Bot._currentLocation);
	}

	public static void OnDayEnded(object? sender, BotDayEndedEventArgs e)
	{
		Task.Run(async () => await ShippingMenuContext()); // we cannot send in MenuChanged as it is reset by then.

		if (!Game1.player.passedOut)
		{
			Context.Send($"You have gone to bed and the day has ended. Good night.");
			return;
		}

		Context.Send($"You have passed out and the day has ended. Maybe you should go back home earlier tomorrow.");
	}
	
	public static void OnBotDamaged(object? sender, BotDamagedEventArgs e)
	{
		Context.Send($"You were damaged by {e.Damager.Name}, they did {e.Damaged} damage. You now have {Main.Bot.PlayerInformation.Health} health left.");
	}
	
	public static void LocationNpcChanged(object? sender, BotCharacterListChangedEventArgs e)
	{
		string contextString = "These are the characters that have either joined or exited this location recently:";
		
		contextString = e.Added.Aggregate(contextString, (current, npc) => current + $"\n{npc.Name} has entered this location, they are at {npc.TilePoint}.");
		contextString = e.Removed.Aggregate(contextString, (current, npc) => current + $"\n{npc.Name} has exited this location from {npc.currentLocation.DisplayName}.");

		Context.Send(contextString,true);
	}

	#endregion

	#region ContextHelpers

	private static async Task ShippingMenuContext()
	{
		List<Item> soldItems = new();
		using var enumerator = Game1.getAllFarmers().GetEnumerator();
		while (enumerator.MoveNext())
		{
			if (enumerator.Current is null) continue;
			soldItems.AddRange(Game1.getFarm().getShippingBin(enumerator.Current));
		}
		if (soldItems.Count == 0) return;

		string shipString = "These are the items you have shipped today:";
		foreach (var item in soldItems)
		{
			int sell = item.sellToStorePrice();
			shipString = string.Concat(shipString, $"\n{item.Name}: total sell price: {sell * item.Stack} single sell price: {sell}");
		}
		await Util.WaitForSeconds(soldItems.Count * 0.75);
		Context.Send(shipString);
		Main.Bot.EndDayShippingMenu.AdvanceToNextDay();
	}

	public static string NewDayContext(bool sendQuests = true)
	{
		string time = StringUtilities.FormatTimeString();
		if (sendQuests) Context.Send($"These are the title's of the quests that are available, " +
		                             $"you can see more about them in your quest log.{QuestContext.GetQuestTitles()}");
		Main.Bot.Time.GetTodayFestivalData(out _, out _, out int startTime, out int endTime);
		string passedOut = Game1.player.passedOut
			? "A new day has started, you are in your farm-house after you passed out, "
			: "A new day has started, you are in your farm-house,"; 
		string contextString = $"{passedOut} the current day is {SDate.Now().DayOfWeek} {SDate.Now().Day} of {SDate.Now().Season} in year {SDate.Now().Year} at time: {time}.";
		
		if (Main.Bot.Time.IsFestival())
		{
			contextString += $" There is a festival today! It is located at {Game1.whereIsTodaysFest}, it will start at {startTime} and end at {endTime}," +
			                $" but you should try to go there as soon as possible so you can fully experience it.";
		}

		if (Main.Bot._farmer.mailbox.Any())
		{
			contextString += $" There is some mail in your mailbox!";
		}
		
		contextString += $" This is the level of your relationship with all the characters you have interacted with: " +
		                 $"{PlayerContext.GetAllCharactersLevel()}.\nAnd these are the levels of all your skills:" +
		                 $" {PlayerContext.GetAllSkillLevel()}.";
		
		return contextString;
	}

	#endregion
}