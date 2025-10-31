using Cysharp.Threading.Tasks;
using NeuroSdk.Actions;
using NeuroSdk.Messages.Outgoing;
using NeuroSdk.Websocket;
using System.Linq;
using UnityEngine;

namespace NeuroMod;

/// <summary>
/// Bridge class that connects ONI duplicate bio data systems with Neuro SDK
/// Monitors bio data events and sends real-time context updates to the AI
/// </summary>
public class NeuroIntegrationBridge : KMonoBehaviour
{
    // Singleton instance
    public static NeuroIntegrationBridge? Instance { get; private set; }

    // Configuration
    [SerializeField]
    private readonly bool enableBioDataStreaming = true;

    [SerializeField]
    private readonly float bioDataUpdateInterval = 5f; // Send updates every 5 seconds

    [SerializeField]
    private readonly bool enableEmergencyActionWindows = true;

    // Runtime data
    private MinionIdentity? neuroMinion;

    private bool isInitialized = false;
    private float lastBioDataUpdate = 0f;

    // Emergency tracking
    private bool isEmergencyActive = false;

    private float lastEmergencyTime = 0f;
    private const float EMERGENCY_COOLDOWN = 30f; // 30 seconds between emergencies

    protected override void OnPrefabInit()
    {
        base.OnPrefabInit();
        Instance = this;
    }

    protected override void OnSpawn()
    {
        base.OnSpawn();
        InitializeBridge();
    }

    private void InitializeBridge()
    {
        try
        {
            // Find the Neuro duplicate
            FindNeuroMinion();

            if (neuroMinion != null)
            {
                // Subscribe to bio data events
                SubscribeToBioDataEvents();

                // Send initial context message
                SendInitialContextMessage();

                isInitialized = true;
                NeuroLogger.Log($"Successfully initialized bridge for {neuroMinion.GetProperName()}", "NeuroIntegrationBridge");
            }
            else
            {
                NeuroLogger.LogWarning("Could not find Neuro duplicate", "NeuroIntegrationBridge");
            }
        }
        catch (System.Exception ex)
        {
            NeuroLogger.LogException(ex, "bridge initialization", "NeuroIntegrationBridge");
        }
    }

    private void FindNeuroMinion()
    {
        try
        {
            // Safety check: ensure Components.MinionIdentities is available
            if (Components.MinionIdentities?.Items == null)
            {
                NeuroLogger.LogWarning("Components.MinionIdentities not available yet", "NeuroIntegrationBridge");
                return;
            }

            // Look for a duplicate named "Neuro"
            System.Collections.Generic.List<MinionIdentity> allMinions = Components.MinionIdentities.Items;
            neuroMinion = allMinions.FirstOrDefault(minion =>
                minion != null &&
                minion.GetProperName().ToLower().Contains("neuro"));

            if (neuroMinion == null && allMinions.Count > 0)
            {
                // Fallback to first duplicate if no "Neuro" duplicate exists
                neuroMinion = allMinions[0];
                NeuroLogger.Log($"Using fallback duplicate: {neuroMinion.GetProperName()}", "NeuroIntegrationBridge");
            }
        }
        catch (System.Exception ex)
        {
            NeuroLogger.LogException(ex, "finding Neuro minion", "NeuroIntegrationBridge");
        }
    }

    private void SubscribeToBioDataEvents()
    {
        if (DuplicateBioDataMonitor.Instance != null)
        {
            // Subscribe to critical events
            DuplicateBioDataMonitor.Instance.OnCriticalHealthChange += OnHealthChange;
            DuplicateBioDataMonitor.Instance.OnStarvationWarning += OnHungerChange;
            DuplicateBioDataMonitor.Instance.OnStressWarning += OnStressChange;
            DuplicateBioDataMonitor.Instance.OnSicknessDetected += OnSicknessDetected;
            DuplicateBioDataMonitor.Instance.OnTemperatureWarning += OnTemperatureWarning;

            NeuroLogger.Log("Subscribed to bio data events", "NeuroIntegrationBridge");
        }
    }

    #region Event Handlers

    private void OnHealthChange(MinionIdentity minion, DuplicateBioData bioData)
    {
        if (minion != neuroMinion)
        {
            return;
        }

        string message = $"Health Alert: {minion.GetProperName()} health is at {bioData.HealthPercentage:P1}.";

        if (bioData.HealthPercentage < 0.3f)
        {
            message += " - Critical health! Immediate medical attention required!";

            // Trigger emergency action window for critical health
            if (enableEmergencyActionWindows && CanTriggerEmergency())
            {
                TriggerHealthEmergencyWindow(minion, bioData);
            }

            Context.Send(message, true);
        }
        else if (bioData.HealthPercentage < 0.5f)
        {
            message += " - Low health, medical attention recommended.";
            Context.Send(message, false);
        }
    }

    private void OnHungerChange(MinionIdentity minion, DuplicateBioData bioData)
    {
        if (minion != neuroMinion)
        {
            return;
        }

        string message = $"Nutrition Alert: {minion.GetProperName()} calories at {bioData.CaloriePercentage:P1}.";

        if (bioData.CaloriePercentage < 0.2f)
        {
            message += " - STARVATION WARNING! Immediate food required!";

            // Trigger emergency action window for starvation
            if (enableEmergencyActionWindows && CanTriggerEmergency())
            {
                TriggerHungerEmergencyWindow(minion, bioData);
            }

            Context.Send(message, true);
        }
        else if (bioData.CaloriePercentage < 0.5f)
        {
            message += " - Getting hungry, should eat soon.";
            Context.Send(message, false);
        }
    }

    private void OnStressChange(MinionIdentity minion, DuplicateBioData bioData)
    {
        if (minion != neuroMinion)
        {
            return;
        }

        string message = $"Stress Alert: {minion.GetProperName()} stress level at {bioData.StressPercentage:P1}.";

        if (bioData.StressPercentage > 0.8f)
        {
            message += " - CRITICAL STRESS! Mental break risk is very high!";

            // Trigger emergency action window for critical stress
            if (enableEmergencyActionWindows && CanTriggerEmergency())
            {
                TriggerStressEmergencyWindow(minion, bioData);
            }

            Context.Send(message, true);
        }
        else if (bioData.StressPercentage > 0.6f)
        {
            message += " - High stress levels, recreation or rest recommended.";
            Context.Send(message, false);
        }
    }

    private void OnSicknessDetected(MinionIdentity minion, DuplicateBioData bioData)
    {
        if (minion != neuroMinion)
        {
            return;
        }

        // Enhanced message using bioData for more detailed information
        string sicknesses = string.Join(", ", bioData.CurrentSicknesses);
        string message = $"Medical Alert: {minion.GetProperName()} has detected illness. " +
            $"Current conditions: {sicknesses}. Health is at {bioData.HealthPercentage:P1}. " +
            "Medical treatment is recommended.";

        Context.Send(message, true);
    }

    private void OnTemperatureWarning(MinionIdentity minion, DuplicateBioData bioData)
    {
        if (minion != neuroMinion)
        {
            return;
        }

        // Enhanced message using bioData for more detailed temperature information
        string tempStatus = bioData.IsOverheating ? "overheating" : bioData.IsFreezing ? "freezing" : "temperature stress";
        string message = $"Temperature Warning: {minion.GetProperName()} is experiencing {tempStatus}. " +
            $"Body temperature: {bioData.BodyTemperature:F1}�K. " +
            $"Health at {bioData.HealthPercentage:P1}. Environmental conditions may be unsafe.";

        Context.Send(message, true);
    }

    #endregion Event Handlers

    // Emergency ActionWindow management
    private bool CanTriggerEmergency()
    {
        return !isEmergencyActive && (Time.time - lastEmergencyTime) > EMERGENCY_COOLDOWN;
    }

    private void TriggerHealthEmergencyWindow(MinionIdentity minion, DuplicateBioData bioData)
    {
        isEmergencyActive = true;
        lastEmergencyTime = Time.time;

        ActionWindow actionWindow = ActionWindow.Create(gameObject);
        actionWindow.SetContext($"{minion.GetProperName()} has critical health ({bioData.HealthPercentage:P1})! What action should be taken?", false);

        // Add emergency action options
        actionWindow.AddAction(new EmergencyAction("immediate_medical", "Send to Medical Bay", "Queue immediate medical treatment at the nearest medical facility"));
        actionWindow.AddAction(new EmergencyAction("emergency_healing", "Use Emergency Healing", "Consume healing items or use emergency medical supplies"));
        actionWindow.AddAction(new EmergencyAction("assign_doctor", "Assign Doctor", "Have a skilled doctor treat the duplicant immediately"));
        actionWindow.AddAction(new EmergencyAction("monitor_only", "Monitor Only", "Continue monitoring but take no immediate action"));

        actionWindow.Register();
    }

    private void TriggerHungerEmergencyWindow(MinionIdentity minion, DuplicateBioData bioData)
    {
        isEmergencyActive = true;
        lastEmergencyTime = Time.time;

        ActionWindow actionWindow = ActionWindow.Create(gameObject);
        actionWindow.SetContext($"{minion.GetProperName()} is starving ({bioData.CaloriePercentage:P1} calories)! How should we respond?", false);

        // Add emergency action options
        actionWindow.AddAction(new EmergencyAction("emergency_food", "Emergency Food", "Deliver high-calorie emergency rations immediately"));
        actionWindow.AddAction(new EmergencyAction("nearest_food", "Nearest Food Source", "Send to the closest available food immediately"));
        actionWindow.AddAction(new EmergencyAction("cooking_priority", "Cooking Priority", "Prioritize cooking tasks to feed the duplicant"));
        actionWindow.AddAction(new EmergencyAction("accept_starvation", "Accept Risk", "Continue current tasks despite starvation risk"));

        actionWindow.Register();
    }

    private void TriggerStressEmergencyWindow(MinionIdentity minion, DuplicateBioData bioData)
    {
        isEmergencyActive = true;
        lastEmergencyTime = Time.time;

        ActionWindow actionWindow = ActionWindow.Create(gameObject);
        actionWindow.SetContext($"{minion.GetProperName()} has critical stress levels ({bioData.StressPercentage:P1})! Mental breakdown imminent - what's your response?", false);

        // Add emergency action options
        actionWindow.AddAction(new EmergencyAction("immediate_recreation", "Immediate Recreation", "Send to the highest-quality recreation facility available"));
        actionWindow.AddAction(new EmergencyAction("stress_relief_massage", "Stress Relief", "Provide massage or other stress-relief treatment"));
        actionWindow.AddAction(new EmergencyAction("reduce_workload", "Reduce Workload", "Remove from current tasks and assign lighter duties"));
        actionWindow.AddAction(new EmergencyAction("isolate_rest", "Isolation Rest", "Send to private quarters for extended rest period"));
        actionWindow.AddAction(new EmergencyAction("continue_monitoring", "Continue Monitoring", "Monitor stress but maintain current assignments"));

        actionWindow.Register();
    }

    private void SendInitialContextMessage()
    {
        if (neuroMinion == null)
        {
            return;
        }

        DuplicateBioData bioData = new(neuroMinion);

        string contextMessage = $"I am now connected to duplicate '{neuroMinion.GetProperName()}'. " +
            $"Current status: Health {bioData.HealthPercentage:P0}, Stress {bioData.StressPercentage:P0}, " +
            $"Calories {bioData.CaloriePercentage:P0}. I can control their tasks and schedule.";

        NeuroLogger.SendContext(contextMessage, false, "NeuroIntegrationBridge");
    }

    private void Update()
    {
        if (!isInitialized || neuroMinion == null)
        {
            return;
        }

        // Send periodic bio data updates
        if (enableBioDataStreaming && Time.time - lastBioDataUpdate >= bioDataUpdateInterval)
        {
            SendBioDataUpdate();
            lastBioDataUpdate = Time.time;
        }
    }

    private void SendBioDataUpdate()
    {
        if (neuroMinion == null)
        {
            return;
        }

        DuplicateBioData bioData = new(neuroMinion);

        string message = $"Neuro Status Update: " +
            $"Health: {bioData.HealthPercentage:P0}, " +
            $"Stress: {bioData.StressPercentage:P0}, " +
            $"Calories: {bioData.CaloriePercentage:P0}, " +
            $"Stamina: {bioData.StaminaPercentage:P0}";

        // Add current activity if available
        ChoreConsumer choreConsumer = neuroMinion.GetComponent<ChoreConsumer>();
        if (choreConsumer?.choreDriver.HasChore() == true)
        {
            Chore currentChore = choreConsumer.choreDriver.GetCurrentChore();
            if (currentChore != null)
            {
                message += $", Current Task: {currentChore.choreType.Name}";
            }
        }
        else
        {
            message += ", Status: Idle";
        }

        NeuroLogger.SendContext(message, false, "NeuroIntegrationBridge");
    }

    /// <summary>
    /// Force refresh of the bridge when duplicate is renamed or changed
    /// </summary>
    public void RefreshBridge()
    {
        isInitialized = false;
        neuroMinion = null;
        InitializeBridge();
    }

    /// <summary>
    /// Get the current Neuro duplicate
    /// </summary>
    /// <returns>The current Neuro minion, or null if not found</returns>
    public MinionIdentity? GetNeuroMinion()
    {
        return neuroMinion;
    }
}

/// <summary>
/// Simple emergency action implementation for ActionWindow choices
/// </summary>
public class EmergencyAction(string name, string title, string description) : NeuroAction
{
    private readonly string actionName = name;
    private readonly string actionDescription = $"{title}: {description}";

    public override string Name => actionName;
    protected override string Description => actionDescription;
    protected override NeuroSdk.Json.JsonSchema? Schema => null; // No parameters needed for emergency actions

    protected override ExecutionResult Validate(ActionJData actionData)
    {
        return ExecutionResult.Success($"Emergency action '{actionName}' selected.");
    }

    protected override UniTask ExecuteAsync()
    {
        try
        {
            NeuroLogger.SendContext($"Emergency action '{actionName}' executed. Response logged for crisis management.", false, "NeuroIntegrationBridge");

            // Reset emergency state when action is taken
            if (NeuroIntegrationBridge.Instance != null)
            {
                NeuroIntegrationBridge bridge = NeuroIntegrationBridge.Instance;
                System.Reflection.FieldInfo? isEmergencyActiveField = typeof(NeuroIntegrationBridge).GetField("isEmergencyActive",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                isEmergencyActiveField?.SetValue(bridge, false);
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[EmergencyAction] Error executing emergency action: {ex.Message}");
            NeuroLogger.SendContext($"Error executing emergency action '{actionName}': {ex.Message}", false, "NeuroIntegrationBridge");
        }

        return UniTask.CompletedTask;
    }
}