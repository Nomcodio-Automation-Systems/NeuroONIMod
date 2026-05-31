using HarmonyLib;
using KMod;
using PeterHan.PLib.Core;
using PeterHan.PLib.Options;

namespace NeuroMod;

/// <summary>
/// Provides the main mod entry point for NeuroMod.
/// </summary>
/// <pre>The mod loader invokes this type during game startup and supplies a Harmony instance.</pre>
/// <post>PLib is initialized, options are registered, and telemetry subscriptions are attached during load.</post>
public class NeuroModUserMod : UserMod2
{
    /// <summary>
    /// Called by the game when the mod is loaded.
    /// </summary>
    /// <param name="harmony">Harmony instance provided by the loader for applying patches.</param>
    /// <pre><paramref name="harmony"/> is supplied by the game mod loader during initialization.</pre>
    /// <post>PLib has been initialized, the options UI is registered, and selected event subscriptions are attached.</post>
    public override void OnLoad(Harmony harmony)
    {
        base.OnLoad(harmony);

        // Initialize PLib
        PUtil.InitLibrary();

        // Register options for the Mods menu
        new POptions().RegisterOptions(this, typeof(NeuroModOptions));

        NeuroLogger.Log("NeuroMod UserMod2 loaded - PLib options registered", "NeuroMod");
        // Subscribe to architecture events for telemetry/demo
        try
        {
            NeuroMod.Architecture.EventAggregator.Instance.Subscribe<NeuroMod.Architecture.Events.WindowRegisteredEvent>(e =>
            {
                NeuroLogger.Log($"WindowRegisteredEvent: TraceId={e.TraceId} Actions=[{string.Join(", ", e.ActionNames)}]", "EventSubscriber");
            });

            NeuroMod.Architecture.EventAggregator.Instance.Subscribe<NeuroMod.Architecture.Events.ActionResultEvent>(e =>
            {
                NeuroLogger.Log($"ActionResultEvent: TraceId={e.TraceId} Success={e.Successful} Message={e.Message}", "EventSubscriber");
            });
        }
        catch { }
    }
}