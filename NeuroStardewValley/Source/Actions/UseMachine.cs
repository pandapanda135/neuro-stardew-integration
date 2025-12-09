using NeuroSDKCsharp.Actions;
using NeuroSDKCsharp.Json;
using NeuroSDKCsharp.Websocket;
using NeuroStardewValley.Debug;
using NeuroStardewValley.Source.ContextStrings;
using NeuroStardewValley.Source.Utilities;
using StardewBotFramework.Source.Modules.Pathfinding.Base;
using StardewBotFramework.Source.ObjectToolSwaps;
using StardewValley;
using StardewValley.TokenizableStrings;
using Object = StardewValley.Object;

namespace NeuroStardewValley.Source.Actions;

public class UseMachine : NeuroAction<KeyValuePair<Object,Item>>
{
	private readonly List<Object> _objects = BotHandler.CurrentLocation.Objects.Values.Where(o =>
		o is { } objs && objs.HasContextTag("is_machine") && objs.HasContextTag("machine_input")).ToList();
	public override string Name => "use_machine";
	protected override string Description => "Use an item with the specified machine, some machines may need multiple items to work e.g. furnaces need ore and coal.";
	protected override JsonSchema Schema => new()
	{
		Type = JsonSchemaType.Object,
		// TODO: Maybe add not adding item for emptying machines, make different action?
		// Or just send context. Also, how do we handle multiple machines both in general and that need emptying
		Required = new List<string> { "machine","item" },
		Properties = new Dictionary<string, JsonSchema>
		{
			["machine"] = QJS.Enum(_objects.Select(obj => obj.DisplayName)),
			["item"] = QJS.Enum(BotHandler.Bot.Inventory.Inventory.Where(item => item is not null && IsValidItem(item)).Select(item => item.DisplayName))
		}
	};
	
	protected override ExecutionResult Validate(ActionData actionData, out KeyValuePair<Object, Item> resultData)
	{
		string? machineName = actionData.Data?.Value<string>("machine");
		string? itemName = actionData.Data?.Value<string>("item");

		resultData = new();
		if (machineName is null || itemName is null)
		{
			return ExecutionResult.Failure($"You must provide a non-null value for both machine and item.");
		}

		Object? obj = null;
		foreach (var kvp in TileContext.GetObjectsInLocation(BotHandler.CurrentLocation))
		{
			if (kvp.Value is not Object objs) continue;
			
			// check these again in case she specifies an object that is not a valid machine. 
			if (objs.DisplayName != machineName || _objects.All(o => o.DisplayName != machineName) 
			    || !objs.HasContextTag("is_machine") || !objs.HasContextTag("machine_input"))
				continue;
			
			obj = objs;
			break;
		}

		if (obj is null)
		{
			return ExecutionResult.Failure($"The object you provided is not a valid machine.");
		}
		obj = TileUtilities.GetClosestObjectId(obj.ItemId, BotHandler.Farmer.Tile);
		if (obj?.GetMachineData() is null)
		{
			return ExecutionResult.Failure($"There is no machine of the type you specified around you.");
		}

		if (!MachineDataUtility.HasAdditionalRequirements(BotHandler.Farmer.Items,
			    obj.GetMachineData().AdditionalConsumedItems, out var requirement))
		{
			return ExecutionResult.Failure($"You cannot use this machine due to: {TokenParser.ParseText(requirement.InvalidCountMessage, null, obj.ParseItemCount)}");
		}

		// ugly solution but it works
		var objHeldObject = obj.heldObject.Value;
		obj.heldObject.Value = null;
		var items = BotHandler.Bot.Inventory.Inventory.Where(i => 
			i is not null && i.DisplayName == itemName && obj.PlaceInMachine(obj.GetMachineData(), i, true, BotHandler.Farmer)).ToList();
		obj.heldObject.Value = objHeldObject;
		if (!items.Any())
		{
			return ExecutionResult.Failure($"The item you provided is not a valid item");
		}
		
		resultData = new(obj, items[0]);
		return ExecutionResult.Success($"Inserting {items[0].DisplayName} into {obj.DisplayName}");
	}

	protected override async void Execute(KeyValuePair<Object, Item> resultData)
	{
		try
		{
			SwapItemHandler.SwapItem(resultData.Value);
			
			await BotHandler.Bot.Pathfinding.Goto(new Goal.GetToTile((int)resultData.Key.TileLocation.X, (int)resultData.Key.TileLocation.Y));
			await TaskDispatcher.SwitchToMainThread();

			if (!Graph.IsInNeighbours(BotHandler.Farmer.TilePoint, resultData.Key.TileLocation.ToPoint(),
				    out var direction, 4)) return;
			
			BotHandler.Bot.Player.ChangeFacingDirection(direction);
			
			// grab if there is a finished item in the machine
			if (resultData.Key.heldObject is not null && resultData.Key.heldObject.Value != resultData.Key.lastInputItem.Value)
			{
				BotHandler.Bot.ObjectInteraction.InteractWithObject(resultData.Key);
			}

			BotHandler.Bot.Player.AddItemToObject(resultData.Key, resultData.Value);
		}
		catch (Exception e)
		{
			Logger.Error($"{e}");
		}
	}

	// private static string FormatObjectName(Object obj)
	// {
	// 	return $"{obj.TileLocation}: {obj.DisplayName}";
	// }

	private bool IsValidItem(Item item)
	{
		foreach (var obj in _objects)
		{
			if (obj.GetMachineData() == null) continue;
			
			var heldObjectValue = obj.heldObject.Value;
			obj.heldObject.Value = null;
			bool result = obj.PlaceInMachine(obj.GetMachineData(), item, true, BotHandler.Farmer);
			obj.heldObject.Value = heldObjectValue;
			if (result) return true;

			// as we have validation that checks if this is valid I'm going to include this in case Neuro has issues with knowing how machines with multiple items work.  
			if (MachineDataUtility.HasAdditionalRequirements(BotHandler.Farmer.Items,
				    obj.GetMachineData().AdditionalConsumedItems, out _))
			{
				return true;
			}
		}

		return false;
	}
}