using NeuroSDKCsharp.Messages.Outgoing;
using NeuroStardewValley.Debug;
using NeuroStardewValley.Source.RegisterActions;
using NeuroStardewValley.Source.Utilities;
using StardewBotFramework.Source.Events.EventArgs;
using StardewValley;

namespace NeuroStardewValley.Source.EventMethods;

public static class LessImportantEvents
{
	public static void OnBotDeath(object? sender, BotOnDeathEventArgs e)
	{
		Context.Send($"Oh no you died! It was at {e.DeathLocation.Name} {e.DeathPoint}.");
	}

	public static int WaitingTime = -1;
	public static void OnUiTimeChanged(object? sender, TimeEventArgs e)
	{
		if (Game1.timeOfDay == WaitingTime)
		{
			WaitingTime = -1;
			RegisterMainActions.RegisterPostAction();
		}
		if (Game1.timeOfDay % 100 != 0)
		{
			return;
		}

		string time = StringUtilities.Format24HourString();
		Context.Send($"The current time is {time}, This is sent in the 24 hour notion.", true);
		if (Game1.timeOfDay % Main.Config.StaminaSendInterval == 0)
		{
			Context.Send($"Your current stamina is {Game1.player.Stamina} from the max of {Game1.player.MaxStamina}", true);
		}
	}

	public static void OnChatMessage(object? sender, ChatMessageReceivedEventArgs e)
	{
		if (e.IsBot) return;
		
		string message;
		switch (e.ChatKind) // magic number are from the game not me :(
		{
			case -1: // This is for messages add to the chat box via ChatBox.addMessage
				message = $"In Stardew Valley, a message saying {e.Message} has appeared in chat.";
				break;
			case 0: // normal public message
				message = $"In Stardew Valley, {e.PlayerName} has said {e.Message} in public chat." +
				        $" You can use the action to talk back to them if you want";
				break;
			case 1:
				Logger.Error($"error message in OnChatMessage: {e.Message}   chat kind: {e.ChatKind}");
				message = $"There was an error message in the in-game chat: {e.Message}";
				break;
			case 2: // notification
				message = $"In Stardew Valley, {e.PlayerName} has said {e.Message} in public chat." +
				        $" You can use the action to talk back to them if you want";
				break;
			case 3: // private
				message = $"In Stardew Valley, {e.PlayerName} has said {e.Message} to you in a private message." +
				        $" You can use the action to talk back to them if you want";
				break;
			default:
				Logger.Warning($"There was a chat message that was outside of the bounds of chat kind." +
				               $" chat kind: {e.ChatKind} message: {e.Message}");
				return;
		}

		// probably don't want her talking about private messages.
		Context.Send($"{message}", e.ChatKind == 3);
	}
	public static void InventoryChanged(object? sender, BotInventoryChangedEventArgs e)
	{
		string contextString = "";
		Item item;
		if (e.Added.Any())
		{
			using var enumerator = e.Added.GetEnumerator();
			while (enumerator.MoveNext())
			{
				item = enumerator.Current;
				contextString += $"\nAdded {item.Stack} {item.DisplayName} to your inventory";
			}
		}

		if (e.Removed.Any())
		{
			using var enumerator = e.Removed.GetEnumerator();
			while (enumerator.MoveNext())
			{
				item = enumerator.Current;
				contextString += $"\nRemoved {item.Stack} {item.DisplayName} from your inventory";
			}
		}

		if (e.QuantityChanged.Any())
		{
			using var enumerator = e.QuantityChanged.GetEnumerator();
			while (enumerator.MoveNext())
			{
				contextString += $"\nModified {enumerator.Current.Item.DisplayName}'s stack amount from " +
				                 $"{enumerator.Current.OldSize} to {enumerator.Current.NewSize}.";
			}
		}

		Context.Send(contextString, true);
	}

	private static bool _ranCaughtFish;
	public static void CaughtFish(object? sender, EventArgs e)
	{
		Task.Run(async () =>
		{
			if (_ranCaughtFish) return;
			_ranCaughtFish = true;
			await Util.WaitForSeconds(1.5);
			
			Main.Bot.FishingBar.CloseRewardMenu();
			_ranCaughtFish = false;
			RegisterMainActions.RegisterPostAction();
		});
	}
}