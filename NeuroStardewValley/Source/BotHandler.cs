using StardewBotFramework.Source;
using StardewValley;

namespace NeuroStardewValley.Source;

public static class BotHandler
{
	public static Farmer Farmer => Bot._farmer;
	public static GameLocation CurrentLocation => Bot._currentLocation;

	public static StardewClient Bot { get; set; } = null!;
}