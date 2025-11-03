using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using StardewBotFramework.Source;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using NeuroStardewValley.Source.Actions.Menus;
using NeuroStardewValley.Source.EventMethods;
using NeuroStardewValley.Source.RegisterActions;
using NeuroStardewValley.Source.Utilities;
using StardewBotFramework.Source.Modules.Pathfinding.Base;
using StardewValley;
using StardewValley.Menus;
using Logger = NeuroStardewValley.Debug.Logger;

namespace NeuroStardewValley;

/// <summary>The mod entry point.</summary>
internal sealed class Main : Mod
{
    /// <summary>
    /// This should be used for all ActionWindows
    /// </summary>
    public static Game GameInstance => GameRunner.instance;
    
    public static StardewClient Bot = null!;

    public static ModConfig Config = null!;
    
    public override void Entry(IModHelper helper)
    {
        TaskDispatcher.Initialize();
        Logger.SetMonitor(Monitor);
        Bot = new StardewClient(helper, ModManifest, Monitor, helper.Multiplayer);

        try
        {
            Config = Helper.ReadConfig<ModConfig>();
        }
        catch (Exception e)
        {
            Logger.Error($"The config.json file for the neuro integration was set incorrectly\n {e}");
            return;
        }
        
        #region Events

        helper.Events.GameLoop.GameLaunched += GameLaunched;
        helper.Events.GameLoop.UpdateTicking += Update;
        Bot.GameEvents.DayStarted += MainGameLoopEvents.OnDayStarted;
        Bot.GameEvents.DayEnded += MainGameLoopEvents.OnDayEnded;
        Bot.GameEvents.BotWarped += MainGameLoopEvents.OnWarped;
        Bot.GameEvents.MenuChanged += MainGameLoopEvents.OnMenuChanged;
        Bot.GameEvents.BotLocationNpcChanged += MainGameLoopEvents.LocationNpcChanged;
        Bot.GameEvents.OnBotDamaged += MainGameLoopEvents.OnBotDamaged;
        Bot.GameEvents.EventFinished += MainGameLoopEvents.EventFinished;

        helper.Events.GameLoop.SaveLoaded += OneTimeEvents.OnSaveLoaded;

        Bot.GameEvents.ChatMessageReceived += LessImportantEvents.OnChatMessage;
        Bot.GameEvents.UiTimeChanged += LessImportantEvents.OnUiTimeChanged;
        Bot.GameEvents.HUDMessageAdded += OneTimeEvents.OnHUDMessageAdded;
        Bot.GameEvents.OnBotDeath += LessImportantEvents.OnBotDeath;
        Bot.GameEvents.BotInventoryChanged += LessImportantEvents.InventoryChanged;
        Bot.GameEvents.CaughtFish += LessImportantEvents.CaughtFish;

        Bot.GameEvents.BotObjectChanged += WorldEvents.WorldObjectChanged;
        Bot.GameEvents.BotTerrainFeatureChanged += WorldEvents.TerrainFeatureChanged;
        Bot.GameEvents.BotLargeTerrainFeatureChanged += WorldEvents.LargeTerrainFeatureChanged;
        Bot.GameEvents.BotLocationFurnitureChanged += WorldEvents.LocationFurnitureChanged;
        
        CharacterController.FailedPathFinding += OneTimeEvents.FailedCharacterController;

        #endregion

        if (Config.Debug)
        {
            helper.Events.Display.Rendered += StardewBotFramework.Debug.DebugDraw.RenderMap;
            helper.Events.Display.Rendered += StardewBotFramework.Debug.DebugDraw.OnRenderPathNode;
            helper.Events.Input.ButtonPressed += InputOnButtonPressed;
        }
    }

    private void InputOnButtonPressed(object? sender, ButtonPressedEventArgs e)
    {
        switch (e.Button)
        {
            case SButton.B:
                Game1.activeClickableMenu = new CarpenterMenu("Robin");
                break;
            case SButton.I:
                Logger.Info(
                    $"pixel tile: {Game1.currentCursorTile.X * Game1.tileSize}  {Game1.currentCursorTile.Y * Game1.tileSize}");
                break;
            case SButton.Y:
                foreach (var building in Game1.getFarm().buildings)
                {
                    Logger.Info($"building: {building.humanDoor.Value}");
                }
                
                break;
            case SButton.G:
                Game1.player.Position = Game1.currentCursorTile * 64;
                break;
            case SButton.X:
                Bot.FishingBar.Fish(100);
                break;
            case SButton.H:
                MouseState mouseState = Game1.input.GetMouseState();
                Logger.Info($"mouse state: {mouseState}");
                Logger.Info($"{new Vector2((int)((Utility.ModifyCoordinateFromUIScale(mouseState.X) + Game1.viewport.X) / 64f), (int)((Utility.ModifyCoordinateFromUIScale(mouseState.Y) + Game1.viewport.Y) / 64f))}");
                break;
            case SButton.R:
                foreach (var building in Game1.currentLocation.buildings)
                {
                    building.FinishConstruction();
                    Logger.Info(
                        $"{building.GetIndoors()}    {building.GetIndoorsName()}    {building.GetIndoorsType()}");
                }

                break;
            case SButton.U:
                bool result = Bot._currentLocation.isActionableTile((int)Game1.currentCursorTile.X,
                    (int)Game1.currentCursorTile.Y,
                    Game1.player);
                Logger.Warning($"is action: {result}");
                break;
            case SButton.V:
                Logger.Info($"update levels");
                // Bot._farmer.setSkillLevel("Fishing", 10);
                Bot._farmer.setSkillLevel("Combat", 10);
                // Bot._farmer.setSkillLevel("Farming", 10);
                break;
        }
    }

    private void GameLaunched(object? sender, GameLaunchedEventArgs e)
    {
        try
        {
            if (string.IsNullOrEmpty(Config.WebsocketUri)) throw new Exception($"UriString was not set correctly");
            NeuroSDKCsharp.SdkSetup.Initialize(GameInstance,"Stardew Valley",Config.WebsocketUri);
        }
        catch (Exception exception)
        {
            Console.WriteLine(exception);
            throw;
        }
    }
    private int _mainMenuTimer;
    
    private static bool _hasSentCharacter;
    
    private int _registerTimer;
    private Vector2 _lastPlayerPos;
    private void Update(object? sender, UpdateTickingEventArgs e)
    {
        TaskDispatcher.RunPending();
        // this is for if neuro has been frozen for too long
        Vector2 newPos = Bot._farmer.Position;
        // this covers most stuff like EventUp and current menu not null. We include waiting time in case Neuro decides to wait for a long time.  
        if (Context.IsPlayerFree && Config.RegisterIfPausedForLong && LessImportantEvents.WaitingTime == -1)
        {
            if (_lastPlayerPos == newPos)
            {
                _registerTimer += Game1.currentGameTime.ElapsedGameTime.Milliseconds;
            }
            else
            {
                _registerTimer = 0;
            }

            _lastPlayerPos = newPos;
        }

        if (_registerTimer >= Config.TimeUntilRegisterAgain)
        {
            _registerTimer = 0;
            RegisterMainActions.RegisterPostAction();
        }

        if (Game1.activeClickableMenu is not TitleMenu || Game1.currentGameTime is null)
        {
            _mainMenuTimer = 0;
            return;
        }

        if (_mainMenuTimer < 5000)
        {
            _mainMenuTimer += Game1.currentGameTime.ElapsedGameTime.Milliseconds;
            return;
        }
        
        Bot.MainMenuNavigation.SetTitleMenu((TitleMenu)Game1.activeClickableMenu);

        // load game
        if (!Config.AllowCharacterCreation)
        {
            Bot.MainMenuNavigation.GotoLoad();

            if (TitleMenu.subMenu is LoadGameMenu)
            {
                Bot.LoadMenu.SetLoadMenu((LoadGameMenu)TitleMenu.subMenu);
                if (!Bot.LoadMenu.Loading) Bot.LoadMenu.LoadSlot(Config.SaveSlot);
            }
        }

        if (TitleMenu.subMenu is not null || _hasSentCharacter || !Config.AllowCharacterCreation) return;
        
        Bot.MainMenuNavigation.GotoCreateNewCharacter();

        if (TitleMenu.subMenu is not CharacterCustomization) return;
        
        _hasSentCharacter = true;
        Bot.CharacterCreation.SetCreator((CharacterCustomization)TitleMenu.subMenu);
        MainMenuActions.RegisterAction();
    }
}