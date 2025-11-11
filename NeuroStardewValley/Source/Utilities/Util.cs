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
}