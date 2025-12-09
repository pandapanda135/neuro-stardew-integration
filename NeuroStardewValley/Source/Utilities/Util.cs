using Microsoft.Xna.Framework;
using StardewValley;
using Object = StardewValley.Object;

namespace NeuroStardewValley.Source.Utilities;

public static class Util
{
	/// <summary>
	/// This is meant to replace Task.Delay so both seconds and switching to the main thread can be handled .
	/// </summary>
	/// <param name="seconds"></param>
	/// <param name="switchMainThread">If this is true anything executed after this will be on the main thread.</param>
	public static async Task WaitForSeconds(double seconds, bool switchMainThread = true)
	{
		await Task.Delay(TimeSpan.FromSeconds(seconds));
		if (switchMainThread) await TaskDispatcher.SwitchToMainThread();
	}

	public static int SortObjectsByDistance(Point tile1, Point tile2)
	{
		var farmerTile = BotHandler.Farmer.TilePoint;
		float distance = Utility.distance(tile1.X, farmerTile.X, tile1.Y, farmerTile.Y);
		float distance2 = Utility.distance(tile2.X, farmerTile.X, tile2.Y, farmerTile.Y);

		if (distance < distance2)
		{
			return -1;
		}

		if (Math.Abs(distance - distance2) < 0)
		{
			return 0;
		}

		if (distance > distance2)
		{
			return 1;
		}

		return 0;
	}
	
	public static int SortObjectsByDistance(Object obj, Object obj2)
	{
		return SortObjectsByDistance(obj.TileLocation.ToPoint(), obj2.TileLocation.ToPoint());
	}
}