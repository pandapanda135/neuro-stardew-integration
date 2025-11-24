using NeuroSDKCsharp.Actions;
using NeuroSDKCsharp.Json;
using NeuroSDKCsharp.Websocket;
using NeuroStardewValley.Source.RegisterActions;
using StardewValley;
using Farmer = StardewValley.Farmer;

namespace NeuroStardewValley.Source.Actions;

public static class ChatActions
{
    public class SendChatMessage : NeuroAction<List<string>>
    {
        private static string[] Action => new[] { "Public", "Private" };
        
        private static string[] GetOtherPlayers() => Game1.otherFarmers.Values.Select(farmer => farmer.Name).ToArray();
        
        public override string Name => "talk_in_game_chat";
        protected override string Description => "This will allow for you to talk in the game's chat.";
        protected override JsonSchema Schema
        {
            get
            {
                JsonSchema s =  new()
                {
                    Type = JsonSchemaType.Object,
                    Required = new List<string> { "message" },
                    Properties = new Dictionary<string, JsonSchema>
                    {
                        ["message"] = QJS.Type(JsonSchemaType.String),
                    }
                };

                if (!GetOtherPlayers().Any()) return s;
                
                s.Required.Add("action");
                s.Properties.TryAdd("action", QJS.Enum(Action));
                s.Properties.TryAdd("private_player_name",QJS.Enum(GetOtherPlayers()));

                return s;
            }
        }

        protected override ExecutionResult Validate(ActionData actionData, out List<string>? resultData)
        {
            string? action = actionData.Data?.Value<string>("action");
            string? message = actionData.Data?.Value<string>("message");
            string? playerName = actionData.Data?.Value<string>("private_player_name");
            resultData = new ();

            if (message is null || (GetOtherPlayers().Any() && action is not null && action == "Private" && playerName is null))
            {
                return ExecutionResult.Failure("A parameter was not set correctly.");
            }

            if (action is not null && !Action.Contains(action))
            {
                return ExecutionResult.Failure($"{action} is not a valid action.");
            }
            
            if (action is null && playerName is null)
            {
                resultData.Add(message);
                return ExecutionResult.Success($"You have sent {message} in chat.");    
            }
            
            if (action is null)
            {
                return ExecutionResult.Failure($"You must provide a value for action.");
            }
            
            // if private and player doesn't exist
            if (action == Action[1] && (playerName is null || !GetOtherPlayers().Contains(playerName)))
            {
                return ExecutionResult.Failure($"{playerName} is not a valid player name.");
            }

            // this shouldn't really affect anything but might as well check
            if (action == Action[0] && !string.IsNullOrEmpty(playerName))
            {
                return ExecutionResult.Failure($"If you want to send a public message you cannot provide a player name.");
            }
            
            resultData.Add(action);
            resultData.Add(message);
            if (playerName is not null) resultData.Add(playerName);

            var successString = $"You have sent {message} in {action} chat.";
            if (action == "Private") successString += $" to {playerName}.";
            return ExecutionResult.Success(successString);
        }

        protected override void Execute(List<string>? resultData)
        {
            if (resultData is null) return;

            if (resultData.Count <= 1)
            {
                Main.Bot.Chat.SendPublicMessage(resultData[0]);
                RegisterMainActions.RegisterPostAction();
                return;
            }
            
            switch (resultData[0])
            {
                case "Private":
                    Main.Bot.Chat.SendPrivateMessage(resultData[2],resultData[1]);
                    break;
                case "Public":
                    Main.Bot.Chat.SendPublicMessage(resultData[1]);
                    break;
            }
            
            RegisterMainActions.RegisterPostAction();
        }
    }

    public class UseEmote : NeuroAction<string>
    {
        public override string Name => "use_emote";
        protected override string Description => "Use an emote";
        protected override JsonSchema Schema => new()
        {
            Type = JsonSchemaType.Object,
            Required = new List<string> { "emote" },
            Properties = new Dictionary<string, JsonSchema>
            {
                ["emote"] = QJS.Enum(Farmer.EMOTES.Where(emoteType =>
                    !emoteType.hidden || Main.Bot._farmer.performedEmotes.ContainsKey(emoteType.emoteString))
                    .Select(emote => emote.displayName))
            }
        };
        protected override ExecutionResult Validate(ActionData actionData, out string? resultData)
        {
            string? emote = actionData.Data?.Value<string>("emote");

            resultData = "";
            if (emote is null)
            {
                return ExecutionResult.Failure($"You cannot provide a null value.");
            }

            if (Farmer.EMOTES.Where(emoteType => emoteType.hidden).Select(type => type.displayName).Contains(emote) 
                || !Farmer.EMOTES.Select(type => type.displayName).Contains(emote))
            {
                return ExecutionResult.Failure($"The value you provided is not a valid emote.");
            }
            
            resultData = emote;
            return ExecutionResult.Success($"Doing {emote}");
        }

        protected override void Execute(string? resultData)
        {
            var emote = Farmer.EMOTES.Where(emote => emote.displayName == resultData).ToArray()[0];
            Main.Bot.Chat.UseEmote(emote.emoteString);
            
            DelayedAction.functionAfterDelay(() => RegisterMainActions.RegisterPostAction(), 2000);
        }
    }
}