using Microsoft.Xna.Framework;
using StardewValley;

namespace NeuroStardewValley.Source.Utilities;

public static class RangeCheck
{
	public static bool InRange(Point point, int range = 1)
	{
		return Utility.tileWithinRadiusOfPlayer(point.X, point.Y,range, BotHandler.Farmer);
	}
}