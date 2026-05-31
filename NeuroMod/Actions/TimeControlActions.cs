using Cysharp.Threading.Tasks;
using NeuroSdk.Actions;
using NeuroSdk.Json;
using NeuroSdk.Websocket;
using System.Collections.Generic;

namespace NeuroMod;

/// <summary>
/// Reads the current game speed and returns it as an integer speed index (0–3).
/// Speed 0 = paused, 1 = normal, 2 = fast, 3 = very fast.
/// </summary>
/// <pre>The game must be loaded and <see cref="SpeedControlScreen"/> accessible.</pre>
/// <post>Successful validation returns the current speed index without mutating game state.</post>
public class GetGameSpeedAction : BaseNeuroAction
{
    public override string Name => "get_game_speed";

    protected override string Description =>
        "Returns the current game speed. " +
        "Speed index: 0 = paused, 1 = normal, 2 = fast, 3 = very fast.";

    protected override JsonSchema? Schema => null;

    protected override ExecutionResult Validate(ActionJData actionData, out object? parsedData)
    {
        parsedData = null;

        SpeedControlScreen? scs = SpeedControlScreen.Instance;
        if (scs == null)
            return ExecutionResult.Failure("Game speed control is not available right now.");

        // IsPaused is independent of the speed index; GetSpeed() returns the active
        // speed (0=normal, 1=fast, 2=very fast) even while paused.
        bool paused = scs.IsPaused;
        int speed   = scs.GetSpeed();
        string speedLabel = speed == 0 ? "normal"
            : speed == 1 ? "fast"
            : speed == 2 ? "very fast"
            : string.Concat("unknown (", speed.ToString(), ")");

        string state = paused
            ? string.Concat("paused (speed when unpaused: ", speedLabel, ")")
            : speedLabel;

        return ExecutionResult.Success(string.Concat("Current game speed: ", state));
    }

    protected override UniTask ExecuteAsync(object? parsedData) => UniTask.CompletedTask;
}

/// <summary>
/// Sets the game speed. Use speed 0 to pause/unpause; 1 = normal, 2 = fast, 3 = very fast.
/// Internally ONI uses indices 0/1/2 for normal/fast/very-fast, with pause as a separate toggle.
/// </summary>
/// <pre>The game must be loaded and <see cref="SpeedControlScreen"/> accessible.</pre>
/// <post>After successful execution the game time scale reflects the requested speed.</post>
public class SetGameSpeedAction : NeuroAction<SetGameSpeedAction.SpeedRequest>
{
    /// <summary>
    /// Payload describing the desired game speed.
    /// </summary>
    /// <pre>Property value is populated from the incoming JSON payload.</pre>
    /// <post>A populated instance drives the speed change in <see cref="ExecuteAsync"/>.</post>
    public class SpeedRequest
    {
        /// <summary>Gets or sets the target speed index (0–3).</summary>
        public int Speed { get; set; }
    }

    public override string Name => "set_game_speed";

    protected override string Description =>
        "Sets the game speed. " +
        "speed: 0 = pause/unpause toggle, 1 = normal, 2 = fast, 3 = very fast.";

    protected override JsonSchema Schema => new()
    {
        Type = JsonSchemaType.Object,
        Properties = new Dictionary<string, JsonSchema>
        {
            ["speed"] = new JsonSchema
            {
                Type = JsonSchemaType.Integer,
                Enum = new List<object> { 0, 1, 2, 3 }
            }
        },
        Required = new List<string> { "speed" },
    };

    protected override ExecutionResult Validate(ActionJData actionData, out SpeedRequest? parsedData)
    {
        parsedData = null;

        SpeedControlScreen? scs = SpeedControlScreen.Instance;
        if (scs == null)
            return ExecutionResult.Failure("Game speed control is not available right now.");

        int? raw = actionData.Data?["speed"]?.ToObject<int>();
        if (raw == null)
            return ExecutionResult.Failure("Missing required parameter: speed");

        if (raw < 0 || raw > 3)
            return ExecutionResult.Failure(string.Concat("Invalid speed value ", raw.ToString(), ". Must be 0-3 (0=pause toggle, 1=normal, 2=fast, 3=very fast)."));

        parsedData = new SpeedRequest { Speed = raw.Value };
        return ExecutionResult.Success();
    }

    protected override UniTask ExecuteAsync(SpeedRequest? parsedData)
    {
        if (parsedData == null)
            return UniTask.CompletedTask;

        SpeedControlScreen? scs = SpeedControlScreen.Instance;
        if (scs == null)
            return UniTask.CompletedTask;

        // ONI's SetSpeed uses 0=normal, 1=fast, 2=very fast; pause is a separate toggle.
        // We expose 0=pause toggle, 1=normal, 2=fast, 3=very fast to the AI.
        if (parsedData.Speed == 0)
        {
            scs.TogglePause();
            NeuroLogger.Log("[SetGameSpeedAction] Toggled pause.", "SetGameSpeedAction", ActionWindow?.TraceId);
        }
        else
        {
            // If currently paused, unpause first so the speed is visible
            if (scs.IsPaused)
                scs.TogglePause();
            scs.SetSpeed(parsedData.Speed - 1); // shift: 1->0, 2->1, 3->2
            NeuroLogger.Log(string.Concat("[SetGameSpeedAction] Speed set to ", parsedData.Speed.ToString()), "SetGameSpeedAction", ActionWindow?.TraceId);
        }
        return UniTask.CompletedTask;
    }
}
