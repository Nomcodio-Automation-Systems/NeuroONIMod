using HarmonyLib;
using KMod;
using PeterHan.PLib.Core;
using PeterHan.PLib.Options;

namespace NeuroMod;

/// <summary>
/// Main mod entry point for NeuroMod
/// Registers PLib options and initializes Harmony patches
/// </summary>
public class NeuroModUserMod : UserMod2
{
    /// <summary>
    /// Called when the mod is loaded - registers PLib options
    /// </summary>
    public override void OnLoad(Harmony harmony)
    {
        base.OnLoad(harmony);

        // Initialize PLib
        PUtil.InitLibrary();

        // Register options for the Mods menu
        new POptions().RegisterOptions(this, typeof(NeuroModOptions));

        NeuroLogger.Log("NeuroMod UserMod2 loaded - PLib options registered", "NeuroMod");
    }
}