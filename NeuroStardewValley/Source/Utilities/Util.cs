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

	
	public static int SortObjectsByDistance(Object obj, Object obj2)
	{
		var point = obj.TileLocation.ToPoint();
		var point1 = obj2.TileLocation.ToPoint();
		var farmerTile = Main.Bot._farmer.TilePoint;

		float distance = Utility.distance(point.X, farmerTile.X, point.Y, farmerTile.Y);
		float distance2 = Utility.distance(point1.X, farmerTile.X, point1.Y, farmerTile.Y);

		if (distance < distance2)
		{
			return 1;
		}

		if (Math.Abs(distance - distance2) < 0)
		{
			return 0;
		}

		if (distance > distance2)
		{
			return -1;
		}

		return 0;
	}
}