namespace NeuroStardewValley.Source.Utilities;

public static class Utils
{
	public static async Task WaitForSeconds(double seconds) => await Task.Delay(TimeSpan.FromSeconds(seconds));
}