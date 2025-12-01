using Microsoft.Xna.Framework;
using Netcode;
using NeuroStardewValley.Debug;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.TerrainFeatures;
using StardewValley.TokenizableStrings;
using Object = StardewValley.Object;

namespace NeuroStardewValley.Source.Utilities;

public static class StringUtilities
{
	public static string FormatTimeString()
	{
		int intHours;
		if (Game1.timeOfDay >= 1200 && Game1.timeOfDay < 1300) // for 12pm
		{
			intHours = 12;
		}
		else
		{
			intHours = Game1.timeOfDay / 100 % 12;
		}

		string text = FormatTime(intHours);
		if (Game1.timeOfDay < 1200 || Game1.timeOfDay >= 2400)
		{
			text = text.Insert(text.Length, " AM");
		}
		else
		{
			text = text.Insert(text.Length, " PM");
		}

		return text;
	}

	public static string Format24HourString()
	{
		int intHours;
		if (Game1.timeOfDay >= 1000 && Game1.timeOfDay <= 2400)
		{
			intHours = Game1.timeOfDay / 100;
		}
		else if(Game1.timeOfDay > 2400)
		{
			intHours = Game1.timeOfDay - 2400;
			intHours /= 100;
		}
		else
		{
			intHours = Game1.timeOfDay / 100;
		}

		string text = FormatTime(intHours);
		return text;
	}

	private static string FormatTime(int hour)
	{
		int minutes = Game1.timeOfDay % 100;
		string padZeros = "";
		string hours = hour.ToString();
		string text = "";
		if (Game1.timeOfDay % 100 == 0)
		{
			padZeros = padZeros.Insert(0,"0");
		}
		text = text.Insert(text.Length,hours);
		text = text.Insert(text.Length,":");
		text = text.Insert(text.Length, minutes.ToString());
		text = text.Insert(text.Length, padZeros);

		return text;
	}

	public static int TimeStringToInt(string time)
	{
		var split = time.Split(':');

		Logger.Info($"split length: {split.Length}   game time: {Game1.timeOfDay}  string: {Game1.getTimeOfDayString(Game1.timeOfDay)}");
		if (split.Length != 2) return -1;
		int hour = int.Parse(split[0]) * 100;
		int minute = int.Parse(split[1]);
		Logger.Info($"hour: {hour} minute: {minute}");
		// 2600 as automatically falls asleep at 02:00 am
		if (hour > 2600 || hour < 0 || minute > 60 || minute < 0) return -1;

		return hour + minute;
	}

	public static string FormatBannerMessage(string message)
	{
		string formattedMessage = message.Replace("\n", "");

		return formattedMessage;
	}

	public static Dictionary<Point,Object> GetObjectsInLocation(Object obj)
	{
		GameLocation location = Game1.currentLocation;

		Dictionary<Point,Object> points = new();
		foreach (var kvp in location.Objects.Pairs.Where(kvp => kvp.Value.GetType() == obj.GetType()))
		{
			points.Add(kvp.Key.ToPoint(),kvp.Value);
		}

		return points;
	}

	public static string FormatItemString(string itemString)
	{
		itemString = itemString.Replace("\n", " ");
		return itemString;
	}
	
	public static string GetBuildingName(Building building)
	{
		return TokenParser.ParseText(building.GetData().Name);
	}

	private static readonly Dictionary<int, string> ResourceClumpNames = new()
	{
		{44, "Green rain bush"},
		{46, "Green rain bush"},
		{600, "Stump"},
		{602, "Hollow log"},
		{622, "Meteorite"},
		{672, "Boulder"},
		{752, "Mine rock"},
		{754, "Mine rock"},
		{756, "Mine rock"},
		{758, "Mine rock"},
		{148, "Quarry boulder"}
	};
	
	/// <summary>
	/// Corrects the name given from modData.Name for <see cref="TerrainFeature"/> and modded <see cref="ResourceClump"/>
	/// , in the case of a vanilla resource clump it will get the name from <see cref="ResourceClumpNames"/>
	/// </summary>
	public static string CorrectObjectName(INetObject<NetFields> netObject)
	{
		switch (netObject)
		{
			case ResourceClump clump:
				if (ResourceClumpNames.TryGetValue(clump.parentSheetIndex.Value, out string? test)) return test;
				
				// this is for non-vanilla and will always output "ResourceClump"
				int start = clump.modData.Name.IndexOf('(');
				return clump.modData.Name.Substring(start + 1,
					clump.modData.Name.IndexOf(')') - start - 1);
			case TerrainFeature feature:
				int startIndex = feature.modData.Name.IndexOf('(');
				return feature.modData.Name.Substring(startIndex + 1,
					feature.modData.Name.IndexOf(')') - startIndex - 1);
		}

		return "";
	}
}