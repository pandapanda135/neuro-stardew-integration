using NeuroStardewValley.Debug;
using NeuroStardewValley.Source.Actions.Menus;
using StardewValley;
using StardewValley.Menus;

namespace NeuroStardewValley.Source;

public class MainMenuHandler
{
	private int _mainMenuTimer;
	private bool _blockAutomation;
	public void Update()
	{
		if (_blockAutomation) return;

		// we wait 5 seconds before doing anything.
		if (Game1.activeClickableMenu is not TitleMenu || Game1.currentGameTime is null)
		{
			_mainMenuTimer = 0;
			return;
		}

		if (Main.Config.MainMenuAutomation.IsDown())
		{
			Logger.Info($"You have blocked main menu automation");
			_blockAutomation = true;
			return;
		}

		if (_mainMenuTimer < 5000)
		{
			_mainMenuTimer += Game1.currentGameTime.ElapsedGameTime.Milliseconds;
			return;
		}
        
		BotHandler.Bot.MainMenuNavigation.SetTitleMenu((TitleMenu)Game1.activeClickableMenu);

		if (Main.Config.MultiplayerIp is not null)
		{
			HandleConnectMultiplayer();
			return;
		}

		if (!Main.Config.AllowCharacterCreation)
		{
			HandleLoadSinglePlayer();
			return;
		}
        
		HandleCreateSinglePlayer();
	}
	
	private void HandleLoadSinglePlayer()
	{
		BotHandler.Bot.MainMenuNavigation.GotoLoad();

		if (TitleMenu.subMenu is not LoadGameMenu loadMenu) return;
		
		BotHandler.Bot.LoadMenu.SetLoadMenu(loadMenu);
		if (!BotHandler.Bot.LoadMenu.Loading) BotHandler.Bot.LoadMenu.LoadSlot(Main.Config.SaveSlot);
	}

	private bool _openedCustomizer;
	private void HandleCreateSinglePlayer()
	{
		if (TitleMenu.subMenu is not null || _openedCustomizer || !Main.Config.AllowCharacterCreation) return;
        
		BotHandler.Bot.MainMenuNavigation.GotoCreateNewCharacter();

		if (TitleMenu.subMenu is not CharacterCustomization customizer) return;
        
		_openedCustomizer = true;
		BotHandler.Bot.CharacterCreation.SetCreator(customizer);
		MainMenuActions.RegisterAction();
	}

	private void HandleConnectMultiplayer()
	{
		if (TitleMenu.subMenu is not CoopMenu coopMenu)
		{
			BotHandler.Bot.MainMenuNavigation.GotoMultiplayer();
			return;
		}
		
		BotHandler.Bot.MultiplayerMenu.SetTitleMenu(coopMenu);
		if (BotHandler.Bot.MultiplayerMenu.ConnectingFinished is null || !BotHandler.Bot.MultiplayerMenu.ConnectingFinished.Value) return;

		if (!BotHandler.Bot.MultiplayerMenu.ReadyForAddress)
		{
			BotHandler.Bot.MultiplayerMenu.ClickJoinLan();
			return;
		}
		
		if (Main.Config.MultiplayerIp is null) return;
		
		BotHandler.Bot.MultiplayerMenu.SetAndJoinAddress(Main.Config.MultiplayerIp);
	}
}