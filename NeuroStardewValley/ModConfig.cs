using StardewModdingAPI.Utilities;

namespace NeuroStardewValley;

public class ModConfig
{
    // this allows for many debug features to be used, many triggerable through hotkeys.
    #if DEBUG
        public bool Debug { get; set; } = true;
    #else
        public bool Debug { get; set; } = false;
    #endif
    public string WebsocketUri { get; set; } = "ws://localhost:8000/ws/";
    public bool AllowCharacterCreation { get; set; } = false; // Allow Neuro to create her own character. If this is false a singleplayer world will be loaded.
    public int SaveSlot { get; set; } = 0; // save slot to use.
    
    // If this is set to anything but null, it will try to connect to a lan game that is on this IP.
    // Be aware that an empty string will not count as null and will be interpreted as localhost by the game.
    public string? MultiplayerIp { get; set; } = null;
    
    // registering
    public bool RegisterIfPausedForLong { get; set; } = true; // re-register main actions if paused for too long
    public int TimeUntilRegisterAgain { get; set; } = 60000; // time until register actions again in milliseconds.
    
    [Obsolete("The objects in the radius are no longer sent, might make this a config option later so its being kept")]
    public int TileContextRadius { get; set; } = 50; // The radius of tiles to send as context.
    public int StaminaSendInterval { get; set; } = 400; // The amount of in-game hours between each stamina context, sent every hour divisible by four would be 400.
    public bool WaitTimeAction { get; set; } = true; // Allow Neuro to use an action that allows her wait for until a provided time.
    
    // specific actions config

    // When Neuro uses a query action it will ask her for a radius, this limits the size of what she can receive.
    public int MinQueryRange { get; set; } = 3;
    public int MaxQueryRange { get; set; } = 100;
    // for actions that have both a rectangle and range variant this will register the range version if true.
    public bool UseRange { get; set; } = true;
    // GetQuestItem use quest title instead of item name
    public bool UseQuestTitleInsteadOfItemName { get; set; } = true;
    public bool SeparateBuyAndShopkeeperActions { get; set; } = false;
    
    // Stop main menu automation keybind, These are the valid keys: https://stardewvalleywiki.com/Modding:Player_Guide/Key_Bindings
    public KeybindList MainMenuAutomation { get; set; } = KeybindList.Parse("F");
    public Dictionary<string, bool> CharacterCreationOptions { get; set; } = new()
    {
        { "skin", true },
        { "gender", true },
        { "hair", true },
        { "shirt", true },
        { "pants", true },
        { "accessories", true },
        { "name", true },
        { "farm_name", true },
        { "favourite_thing", true },
        { "animal_preference", true },
        { "animal_breed", true },
        { "eye_hue", true },
        { "eye_saturation", true },
        { "eye_brightness", true },
        { "hair_hue", true },
        { "hair_saturation", true },
        { "hair_brightness", true },
        { "pants_hue", true },
        { "pants_saturation", true },
        { "pants_brightness", true },
        { "farm_type", true }
    };

    public Dictionary<string, string> CharacterCreationDefault { get; set; } = new()
    {
        { "skin", "" },
        { "gender", "" },
        { "hair", "" },
        { "shirt", "" },
        { "pants", "" },
        { "accessories", "" },
        { "name", "" },
        { "farm_name", "" },
        { "favourite_thing", "" },
        { "animal_preference", "" },
        { "animal_breed", "" },
        { "eye_hue", "" },
        { "eye_saturation", "" },
        { "eye_brightness", "" },
        { "hair_hue", "" },
        { "hair_saturation", "" },
        { "hair_brightness", "" },
        { "pants_hue", "" },
        { "pants_saturation", "" },
        { "pants_brightness", "" },
        { "farm_type", "" }
    };
}