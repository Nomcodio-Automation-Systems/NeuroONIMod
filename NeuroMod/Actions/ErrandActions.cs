using Cysharp.Threading.Tasks;
using NeuroSdk.Actions;
using NeuroSdk.Json;
using NeuroSdk.Websocket;
using Newtonsoft.Json.Linq;
using NeuroMod.Integration;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace NeuroMod;

/// <summary>
/// Action to list available errands (chores) for the Neuro duplicate.
/// </summary>
/// <remarks>
/// Returns concrete world errands (mop, build, repair, etc.) rather than configured
/// priority groups. Supports filtering by distance, chore types and result limits.
/// The action performs scanning during <see cref="Validate"/> so the response is
/// available immediately through the action/result contract.
/// </remarks>
/// <pre>
/// The connected duplicant and the global chore provider must be available while validation scans errands.
/// </pre>
/// <post>
/// Successful validation returns a summary of matching errands and execution performs no further work.
/// </post>
public class ListErrandsAction(MinionIdentity minion) : NeuroAction<ListErrandsAction.ErrandFilter>
{
    internal sealed class ErrandScanReference
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ErrandScanReference"/> class.
        /// </summary>
        /// <param name="choreTypeId">The stable chore type id captured during the scan.</param>
        /// <param name="locationX">The scanned target X coordinate.</param>
        /// <param name="locationY">The scanned target Y coordinate.</param>
        /// <pre>The values come from one errand discovered during list_errands.</pre>
        /// <post>The instance stores the minimum metadata required to re-resolve a stale errand id.</post>
        public ErrandScanReference(string choreTypeId, int locationX, int locationY)
        {
            ChoreTypeId = choreTypeId;
            LocationX = locationX;
            LocationY = locationY;
        }

        /// <summary>
        /// Gets the stable chore type id captured during the scan.
        /// </summary>
        /// <pre>The reference object was created for one scanned errand.</pre>
        /// <post>The property returns the chore type id needed for stale errand-id recovery.</post>
        public string ChoreTypeId { get; }

        /// <summary>
        /// Gets the scanned target X coordinate.
        /// </summary>
        /// <pre>The reference object was created for one scanned errand.</pre>
        /// <post>The property returns the target X tile used to re-find the chore.</post>
        public int LocationX { get; }

        /// <summary>
        /// Gets the scanned target Y coordinate.
        /// </summary>
        /// <pre>The reference object was created for one scanned errand.</pre>
        /// <post>The property returns the target Y tile used to re-find the chore.</post>
        public int LocationY { get; }
    }

    private readonly MinionIdentity _neuroMinion = minion;

    /// <summary>
    /// Cache of the most recent list_errands scan results, keyed by errand_id.
    /// Allows assign_errand to look up a specific chore by id without re-scanning.
    /// </summary>
    /// <invariant>Keys are monotonically increasing integers assigned per scan; the dictionary is replaced on every new scan.</invariant>
    internal static Dictionary<int, Chore> LastScanCache { get; private set; } = new Dictionary<int, Chore>();

    /// <summary>
    /// Metadata captured for the most recent scan so assign_errand can try to re-resolve a stale errand_id.
    /// </summary>
    /// <pre>The dictionary is rebuilt together with <see cref="LastScanCache"/> for each list_errands scan.</pre>
    /// <post>Each errand id maps to the chore type and tile location observed during the scan that produced it.</post>
    internal static Dictionary<int, ErrandScanReference> LastScanReferenceCache { get; private set; } = new Dictionary<int, ErrandScanReference>();

    /// <summary>
    /// Defines the filters used to scan available errands.
    /// </summary>
    /// <pre>
    /// Public properties are populated from the incoming JSON payload.
    /// </pre>
    /// <post>
    /// <see cref="ResultMessage"/> contains the computed summary returned by validation.
    /// </post>
    public class ErrandFilter
    {
        /// <summary>
        /// Gets or sets the errand filter mode.
        /// </summary>
        /// <pre>The filter object represents a parsed list-errands request.</pre>
        /// <post>The property stores which selection mode should be applied during errand scanning.</post>
        public string FilterType { get; set; } = "nearby"; // all, nearby, priority, unassigned

        /// <summary>
        /// Gets or sets the maximum search distance in tiles.
        /// </summary>
        /// <pre>The filter object represents a parsed list-errands request.</pre>
        /// <post>The property stores the maximum distance used by distance-aware scans.</post>
        public int MaxDistance { get; set; } = 50;

        /// <summary>
        /// Gets or sets the maximum number of results to return.
        /// </summary>
        /// <pre>The filter object represents a parsed list-errands request.</pre>
        /// <post>The property stores the maximum number of errands that should be collected.</post>
        public int MaxResults { get; set; } = 20;

        /// <summary>
        /// Gets or sets the optional list of chore types to include.
        /// </summary>
        /// <pre>The filter object represents a parsed list-errands request.</pre>
        /// <post>The property stores the set of chore type ids or names used for filtering.</post>
        public List<string> ChoreTypes { get; set; } = new List<string>();
        /// <summary>Result message computed during validation for the action/result contract.</summary>
        /// <pre>The filter object represents a parsed list-errands request.</pre>
        /// <post>The property stores the summary message prepared during validation.</post>
        public string ResultMessage { get; set; } = "";
    }

    public override string Name => "list_errands";

    protected override string Description =>
        "List available errands (actual work items) that the duplicate can perform. " +
        "This shows specific tasks in the world like 'Mop tile at (25,10)' or 'Build ladder at (30,15)'. " +
        "Use filter_type 'performable' to only show errands the duplicate is allowed to do. " +
        "Each result includes a CanPerform field indicating if the duplicate's personal settings allow it.";

    protected override JsonSchema Schema => new()
    {
        Type = JsonSchemaType.Object,
        Properties = new Dictionary<string, JsonSchema>
        {
            ["filter_type"] = new JsonSchema
            {
                Type = JsonSchemaType.String,
                Enum = new List<object>{ "all", "nearby", "priority", "unassigned", "performable" }
            },
            ["max_distance"] = new JsonSchema
            {
                Type = JsonSchemaType.Integer
            },
            ["max_results"] = new JsonSchema
            {
                Type = JsonSchemaType.Integer
            },
            ["chore_types"] = new JsonSchema
            {
                Type = JsonSchemaType.Array,
                Items = new JsonSchema { Type = JsonSchemaType.String }
            }
        }
    };

    /// <summary>
    /// Parse filter parameters and scan the world for matching errands.
    /// </summary>
    /// <param name="actionData">Incoming JSON action payload.</param>
    /// <param name="parsedData">Parsed filter object returned to the action pipeline.</param>
    /// <returns>ExecutionResult.Success with a summary message when errands found or a failure message.</returns>
    /// <pre><paramref name="actionData"/> contains optional list-errands filters and the Neuro duplicant is available for permission checks.</pre>
    /// <post>On success <paramref name="parsedData"/> contains the parsed filter plus the computed summary message returned to the caller.</post>
    protected override ExecutionResult Validate(
        ActionJData actionData,
        out ErrandFilter? parsedData
    )
    {
        parsedData = null;

        if (_neuroMinion == null || _neuroMinion.gameObject == null)
        {
            return ExecutionResult.Failure("Neuro duplicate not found or not available");
        }

        // Parse filter parameters
        string filterType = actionData.Data?["filter_type"]?.Value<string>() ?? "nearby";
        int maxDistance = actionData.Data?["max_distance"]?.Value<int>() ?? 50;
        int maxResults = actionData.Data?["max_results"]?.Value<int>() ?? 20;

        // Validate max_results
        if (maxResults is < 1 or > 100)
        {
            return ExecutionResult.Failure("max_results must be between 1 and 100");
        }

        // Parse chore types filter
        List<string> choreTypes = new List<string>();
        if (actionData.Data?["chore_types"] is JArray choreTypesArray)
        {
            foreach (JToken token in choreTypesArray)
            {
                string? choreType = token.Value<string>();
                if (!string.IsNullOrEmpty(choreType))
                {
                    choreTypes.Add(choreType!);
                }
            }
        }

        parsedData = new ErrandFilter
        {
            FilterType = filterType,
            MaxDistance = maxDistance,
            MaxResults = maxResults,
            ChoreTypes = choreTypes
        };

        // Do all chore scanning in Validate so result goes through action/result contract
        try
        {
            NeuroLogger.Log($"ListErrandsAction: Filter={filterType}, MaxDistance={maxDistance}, MaxResults={maxResults}", "ListErrandsAction", null);

            ChoreConsumer? choreConsumer = _neuroMinion.GetComponent<ChoreConsumer>();
            if (choreConsumer == null)
            {
                return ExecutionResult.Failure("Cannot list errands - ChoreConsumer not found");
            }

            Vector3 minionPosition = _neuroMinion.transform.position;
            List<ErrandInfo> errands = CollectErrands(parsedData, choreConsumer, minionPosition);

            // Sort by distance (closest first) then apply the result cap
            errands = errands.OrderBy(e => e.Distance).Take(parsedData.MaxResults).ToList();

            // Assign stable errand_ids and rebuild the lookup cache for this scan
            Dictionary<int, Chore> newCache = new Dictionary<int, Chore>();
            Dictionary<int, ErrandScanReference> newReferenceCache = new Dictionary<int, ErrandScanReference>();
            for (int idx = 0; idx < errands.Count; idx++)
            {
                errands[idx].ErrandId = idx + 1;
                newReferenceCache[idx + 1] = new ErrandScanReference(
                    errands[idx].ChoreTypeId,
                    errands[idx].LocationX,
                    errands[idx].LocationY);

                if (errands[idx].SourceChore != null)
                {
                    newCache[idx + 1] = errands[idx].SourceChore!;
                }
            }
            LastScanCache = newCache;
            LastScanReferenceCache = newReferenceCache;

            if (errands.Count == 0)
            {
                parsedData.ResultMessage = $"No errands found matching filter '{filterType}'";
            }
            else
            {
                string summary = $"Found {errands.Count} errands (use errand_id with assign_errand):\n";
                foreach (ErrandInfo e in errands)
                {
                    summary += $"  [{e.ErrandId}] {e.Description} at ({e.LocationX},{e.LocationY})";
                    summary += $" | group:{e.ChoreGroup} distance:{e.Distance:F1}";
                    if (!e.CanPerform)
                    {
                        summary += " [NOT PERMITTED]";
                    }
                    if (!string.IsNullOrEmpty(e.AssignedTo))
                    {
                        summary += $" (taken by {e.AssignedTo})";
                    }
                    summary += "\n";
                }
                parsedData.ResultMessage = summary;
            }

            NeuroLogger.Log(parsedData.ResultMessage, "ListErrandsAction", null);
            return ExecutionResult.Success(parsedData.ResultMessage);
        }
        catch (Exception ex)
        {
            NeuroLogger.LogError($"Error listing errands: {ex.Message}", "ListErrandsAction", null);
            return ExecutionResult.Failure($"Error listing errands: {ex.Message}");
        }
    }

    /// <summary>
    /// No asynchronous work is required; validation already produces the response.
    /// </summary>
    /// <param name="parsedData">The parsed filter data prepared during validation.</param>
    /// <pre>The list-errands action computes its full response during validation.</pre>
    /// <post>No additional side effects occur during execution.</post>
    protected override UniTask ExecuteAsync(ErrandFilter? parsedData)
    {
        return UniTask.CompletedTask;
    }

    /// <summary>
    /// Collect matching errands from the global chore provider using the provided filter.
    /// </summary>
    /// <param name="filter">Filter parameters parsed from the action payload.</param>
    /// <param name="choreConsumer">The duplicant's <see cref="ChoreConsumer"/> for permission checks.</param>
    /// <param name="minionPosition">The duplicant's current world position.</param>
    /// <returns>List of matching errands (may be empty).</returns>
    /// <pre><paramref name="filter"/>, <paramref name="choreConsumer"/>, and <paramref name="minionPosition"/> describe the current scan constraints for the target duplicant.</pre>
    /// <post>The returned list contains at most <paramref name="filter"/>'s max-results count and only errands that satisfy the requested filters.</post>
    private List<ErrandInfo> CollectErrands(ErrandFilter filter, ChoreConsumer choreConsumer, Vector3 minionPosition)
    {
        List<ErrandInfo> errands = new List<ErrandInfo>();

        if (GlobalChoreProvider.Instance == null)
        {
            return errands;
        }

        // Use a HashSet to deduplicate chore instances that appear in multiple world-map buckets.
        HashSet<Chore> seen = new HashSet<Chore>();

        foreach (KeyValuePair<int, List<Chore>> kvp in GlobalChoreProvider.Instance.choreWorldMap)
        {
            foreach (Chore chore in kvp.Value)
            {
                if (chore == null || chore.target == null || chore.isNull)
                {
                    continue;
                }

                // Skip duplicates that appear across multiple world-map buckets.
                if (!seen.Add(chore))
                {
                    continue;
                }

                try
                {
                    Vector3 chorePosition = chore.target.transform.position;
                    float distance = Vector3.Distance(minionPosition, chorePosition);

                    if (filter.FilterType == "nearby" && distance > filter.MaxDistance)
                    {
                        continue;
                    }

                    if (filter.ChoreTypes.Count > 0)
                    {
                        bool matchesType = filter.ChoreTypes.Any(ct =>
                            chore.choreType.Id.Equals(ct, StringComparison.OrdinalIgnoreCase) ||
                            chore.choreType.Name.Equals(ct, StringComparison.OrdinalIgnoreCase)
                        );
                        if (!matchesType)
                        {
                            continue;
                        }
                    }

                    if (filter.FilterType == "priority" && chore.masterPriority.priority_value < 7)
                    {
                        continue;
                    }

                    // Hide taken chores in every mode except 'all'.
                    // 'unassigned' mode is kept for explicit unassigned-only queries.
                    if (filter.FilterType != "all" && chore.driver != null)
                    {
                        continue;
                    }

                    ChoreGroup? choreGroup = GetChoreGroup(chore.choreType);
                    bool canPerform = choreGroup != null && choreConsumer.IsPermittedByUser(choreGroup);

                    if (filter.FilterType == "performable" && !canPerform)
                    {
                        continue;
                    }

                    // MaxResults is applied after sorting — collect everything that passes filters.
                    errands.Add(new ErrandInfo
                    {
                        ChoreTypeId = chore.choreType.Id,
                        ChoreType = chore.choreType.Name,
                        ChoreGroup = choreGroup?.Name ?? "Unknown",
                        Description = GetChoreDescription(chore),
                        LocationX = (int)chorePosition.x,
                        LocationY = (int)chorePosition.y,
                        Distance = distance,
                        Priority = chore.masterPriority.priority_value,
                        AssignedTo = chore.driver?.GetComponent<MinionIdentity>()?.GetProperName() ?? "",
                        CanPerform = canPerform,
                        SourceChore = chore
                    });
                }
                catch (Exception ex)
                {
                    NeuroLogger.LogError($"Error processing chore: {ex.Message}", "ListErrandsAction", null);
                }
            }
        }

        return errands;
    }

    /// <summary>
    /// Finds the chore group that owns the supplied chore type.
    /// </summary>
    /// <param name="choreType">The chore type whose group should be resolved.</param>
    /// <returns>The containing chore group, or null when none is found.</returns>
    /// <pre><paramref name="choreType"/> is a valid chore type in the current game database.</pre>
    /// <post>The returned group contains <paramref name="choreType"/> when non-null.</post>
    private static ChoreGroup? GetChoreGroup(ChoreType choreType)
    {
        foreach (ChoreGroup group in Db.Get().ChoreGroups.resources)
        {
            if (group.choreTypes.Contains(choreType))
            {
                return group;
            }
        }
        return null;
    }

    /// <summary>
    /// Builds a human-readable description for a chore.
    /// </summary>
    /// <param name="chore">The chore to describe.</param>
    /// <returns>A human-readable chore description.</returns>
    /// <pre><paramref name="chore"/> refers to a chore discovered during errand scanning.</pre>
    /// <post>A best-effort human-readable description is returned even if target details cannot be read.</post>
    private static string GetChoreDescription(Chore chore)
    {
        try
        {
            if (chore.target != null)
            {
                string targetName = chore.target.name;
                return $"{chore.choreType.Name} {targetName}";
            }
            return chore.choreType.Name;
        }
        catch
        {
            return chore.choreType.Name;
        }
    }

    /// <summary>
    /// Lightweight representation of an errand suitable for serialization and summaries.
    /// </summary>
    /// <pre>Instances are populated from concrete chores discovered during scanning.</pre>
    /// <post>Each instance captures the serializable summary fields for one errand candidate.</post>
    private class ErrandInfo
    {
        /// <summary>Stable id assigned during a scan, referenced by assign_errand as errand_id.</summary>
        /// <pre>The info object represents a scanned errand.</pre>
        /// <post>The property stores the 1-based id for this errand within the current scan.</post>
        public int ErrandId { get; set; }

        /// <summary>The original chore instance, kept for cache population (not serialized).</summary>
        /// <pre>The info object represents a scanned errand.</pre>
        /// <post>The property stores the source chore reference so the cache can map errand_id to chore.</post>
        public Chore? SourceChore { get; set; }

        /// <summary>The chore type display name.</summary>
        /// <pre>The info object represents a scanned errand.</pre>
        /// <post>The property stores the errand's stable chore type id for cache-based reassignment.</post>
        public string ChoreTypeId { get; set; } = "";

        /// <summary>The chore type display name.</summary>
        /// <pre>The info object represents a scanned errand.</pre>
        /// <post>The property stores the errand's chore type name.</post>
        public string ChoreType { get; set; } = "";

        /// <summary>The chore group display name.</summary>
        /// <pre>The info object represents a scanned errand.</pre>
        /// <post>The property stores the errand's chore group name.</post>
        public string ChoreGroup { get; set; } = "";

        /// <summary>The human-readable errand description.</summary>
        /// <pre>The info object represents a scanned errand.</pre>
        /// <post>The property stores the best-effort description prepared for summaries.</post>
        public string Description { get; set; } = "";

        /// <summary>The errand target X coordinate.</summary>
        /// <pre>The info object represents a scanned errand.</pre>
        /// <post>The property stores the target X coordinate.</post>
        public int LocationX { get; set; }

        /// <summary>The errand target Y coordinate.</summary>
        /// <pre>The info object represents a scanned errand.</pre>
        /// <post>The property stores the target Y coordinate.</post>
        public int LocationY { get; set; }

        /// <summary>The errand distance from the duplicant.</summary>
        /// <pre>The info object represents a scanned errand.</pre>
        /// <post>The property stores the distance in world units from the target duplicant.</post>
        public float Distance { get; set; }

        /// <summary>The errand priority value.</summary>
        /// <pre>The info object represents a scanned errand.</pre>
        /// <post>The property stores the chore's master priority value.</post>
        public int Priority { get; set; }

        /// <summary>The name of the duplicant currently assigned to the errand, if any.</summary>
        /// <pre>The info object represents a scanned errand.</pre>
        /// <post>The property stores the assigned duplicant name or an empty string when unassigned.</post>
        public string AssignedTo { get; set; } = "";

        /// <summary>Whether the target duplicant is allowed to perform the errand.</summary>
        /// <pre>The info object represents a scanned errand.</pre>
        /// <post>The property stores the permission-check outcome for the target duplicant.</post>
        public bool CanPerform { get; set; }
    }
}

/// <summary>
/// Action to get information about the duplicate's current errand.
/// </summary>
/// <remarks>
/// Returns a concise summary of the current chore, its group and priority and location.
/// Falls back to reporting idle state when no chore is active.
/// </remarks>
/// <pre>
/// The connected duplicant must still exist when validation inspects the active chore driver.
/// </pre>
/// <post>
/// Successful validation returns either the active errand summary or an explicit idle message.
/// </post>
public class GetCurrentErrandAction(MinionIdentity minion) : NeuroAction<GetCurrentErrandAction.EmptyData>
{
    private readonly MinionIdentity _neuroMinion = minion;

    /// <summary>
    /// Represents the empty payload accepted by <see cref="GetCurrentErrandAction"/>.
    /// </summary>
    /// <pre>The get-current-errand action accepts no structured payload.</pre>
    /// <post>The type serves only as a marker indicating successful parsing of an empty request.</post>
    public class EmptyData
    { }

    public override string Name => "get_current_errand";

    protected override string Description =>
        "Get detailed information about what errand (task) the duplicate is currently performing.";

    protected override JsonSchema? Schema => null;

    /// <summary>
    /// Inspect the duplicant's <see cref="ChoreConsumer"/> and return a human-readable summary
    /// of the current errand or an idle message.
    /// </summary>
    /// <param name="actionData">The incoming request payload.</param>
    /// <param name="parsedData">Receives the parsed empty payload marker.</param>
    /// <returns>The validation result containing the current errand summary or an idle/error message.</returns>
    /// <pre><paramref name="actionData"/> may be empty because this action accepts no structured input.</pre>
    /// <post>On success <paramref name="parsedData"/> contains the empty marker and the returned message summarizes the duplicant's current errand state.</post>
    protected override ExecutionResult Validate(
        ActionJData actionData,
        out EmptyData? parsedData
    )
    {
        parsedData = new EmptyData();

        if (_neuroMinion == null || _neuroMinion.gameObject == null)
            return ExecutionResult.Failure("Neuro duplicate not found or not available");

        try
        {
            ChoreConsumer? choreConsumer = _neuroMinion.GetComponent<ChoreConsumer>();
            if (choreConsumer == null)
                return ExecutionResult.Failure("Cannot get current errand - ChoreConsumer not found");

            bool hasChore = choreConsumer.choreDriver.HasChore();

            if (!hasChore)
            {
                string message = $"{_neuroMinion.GetProperName()} is currently idle (no active errand)";
                NeuroLogger.Log(message, "GetCurrentErrandAction", null);
                return ExecutionResult.Success(message);
            }

            Chore? currentChore = choreConsumer.choreDriver.GetCurrentChore();
            if (currentChore == null)
            {
                NeuroLogger.LogError("HasChore returned true but GetCurrentChore returned null", "GetCurrentErrandAction", null);
                return ExecutionResult.Failure("Internal error: chore driver inconsistency");
            }

            string choreType = currentChore.choreType.Name;
            ChoreGroup? choreGroup = GetChoreGroup(currentChore.choreType);
            string groupName = choreGroup?.Name ?? "Unknown";

            Vector3 targetPos = currentChore.target != null
                ? currentChore.target.transform.position
                : Vector3.zero;

            int priority = currentChore.masterPriority.priority_value;

            string resultMessage = $"{_neuroMinion.GetProperName()} is currently doing: {choreType} ({groupName})\n" +
                $"Location: ({(int)targetPos.x}, {(int)targetPos.y})\n" +
                $"Priority: {priority}/9";

            NeuroLogger.Log($"Current errand: {choreType} at ({(int)targetPos.x},{(int)targetPos.y})", "GetCurrentErrandAction", null);
            return ExecutionResult.Success(resultMessage);
        }
        catch (Exception ex)
        {
            NeuroLogger.LogError($"Error getting current errand: {ex.Message}", "GetCurrentErrandAction", null);
            return ExecutionResult.Failure($"Error getting current errand: {ex.Message}");
        }
    }

    /// <summary>No asynchronous execution required.</summary>
    /// <param name="parsedData">The parsed empty payload marker.</param>
    /// <pre>The get-current-errand action computes its full response during validation.</pre>
    /// <post>No additional side effects occur during execution.</post>
    protected override UniTask ExecuteAsync(EmptyData? parsedData)
    {
        return UniTask.CompletedTask;
    }

    /// <summary>Find the chore group that contains the provided chore type.</summary>
    /// <param name="choreType">The chore type whose group should be resolved.</param>
    /// <returns>The containing chore group, or null when none is found.</returns>
    /// <pre><paramref name="choreType"/> is a valid chore type in the current game database.</pre>
    /// <post>The returned group contains <paramref name="choreType"/> when non-null.</post>
    private static ChoreGroup? GetChoreGroup(ChoreType choreType)
    {
        foreach (ChoreGroup group in Db.Get().ChoreGroups.resources)
        {
            if (group.choreTypes.Contains(choreType))
            {
                return group;
            }
        }
        return null;
    }
}

/// <summary>
/// Action to assign a specific errand to the Neuro duplicate and ensure it finishes.
/// </summary>
/// <remarks>
/// Temporarily boosts the duplicant's personal priority for the chore group, attaches an
/// <see cref="ErrandMonitor"/>, reserves the chore and initiates the completion tracker.
/// Use <see cref="GetErrandProgressAction"/> to observe progress.
/// </remarks>
/// <pre>
/// The target errand type must resolve to a valid chore, remain unassigned, and fall within the requested search radius.
/// </pre>
/// <post>
/// Successful validation captures the target chore and execution locks the duplicant onto that errand until completion or interruption.
/// </post>
public class AssignErrandAction(MinionIdentity minion) : NeuroAction<AssignErrandAction.AssignData>
{
    private readonly MinionIdentity _neuroMinion = minion;

    /// <summary>
    /// Stores the requested errand parameters and the runtime objects resolved during validation.
    /// </summary>
    /// <pre>The request payload has been parsed for the assign-errand action.</pre>
    /// <post>The instance stores the requested errand parameters and runtime objects resolved during validation.</post>
    public class AssignData
    {
        /// <summary>The errand_id from the most recent list_errands scan, if provided.</summary>
        /// <pre>The data object represents a parsed assign-errand request.</pre>
        /// <post>The property stores the errand_id when the caller used the preferred id-based targeting.</post>
        public int? ErrandId { get; set; }

        /// <summary>The requested errand type identifier.</summary>
        /// <pre>The data object represents a parsed assign-errand request.</pre>
        /// <post>The property stores the chore type name or id supplied by the caller.</post>
        public string? ErrandType { get; set; }

        /// <summary>The maximum search radius in tiles.</summary>
        /// <pre>The data object represents a parsed assign-errand request.</pre>
        /// <post>The property stores the maximum distance used when searching for a candidate chore.</post>
        public int MaxDistance { get; set; } = 50;

        /// <summary>The optional target X coordinate used to bias selection.</summary>
        /// <pre>The data object represents a parsed assign-errand request.</pre>
        /// <post>The property stores the optional target X coordinate supplied by the caller.</post>
        public int? TargetX { get; set; }

        /// <summary>The optional target Y coordinate used to bias selection.</summary>
        /// <pre>The data object represents a parsed assign-errand request.</pre>
        /// <post>The property stores the optional target Y coordinate supplied by the caller.</post>
        public int? TargetY { get; set; }

        // Resolved during validation for use by ExecuteAsync
        /// <summary>The resolved chore consumer for the target duplicant.</summary>
        /// <pre>Validation may resolve runtime objects needed during execution.</pre>
        /// <post>The property stores the target duplicant's chore consumer when validation succeeds.</post>
        internal ChoreConsumer? Consumer { get; set; }

        /// <summary>The resolved chore group for the selected errand type.</summary>
        /// <pre>Validation may resolve runtime objects needed during execution.</pre>
        /// <post>The property stores the chore group associated with the selected errand type.</post>
        internal ChoreGroup? ResolvedGroup { get; set; }

        /// <summary>The resolved target chore chosen during validation.</summary>
        /// <pre>Validation may resolve runtime objects needed during execution.</pre>
        /// <post>The property stores the chore that execution should reserve and track.</post>
        internal Chore? TargetChore { get; set; }

        /// <summary>The duplicant's original personal priority for the resolved chore group.</summary>
        /// <pre>Validation may resolve runtime objects needed during execution.</pre>
        /// <post>The property stores the original priority value so result messages can reflect the boost.</post>
        internal int OldPriority { get; set; }
    }

    public override string Name => "assign_errand";

    protected override string Description =>
        "Assign a specific errand to the duplicate and ensure it gets finished. " +
        "Boosts the ChoreGroup priority to maximum and locks the duplicate onto the task. " +
        "The duplicate will not start other work errands until this one completes (or times out). " +
        "PREFERRED: call list_errands first, then pass the errand_id from that result — errand_id is only valid for the current scan and will be reassigned on the next list_errands call. " +
        "FALLBACK: omit errand_id and supply errand_type as either an exact chore type or a broader chore-group category (e.g. 'Dig', 'Build', 'Mop', 'Harvest', 'Life Support', 'Tidying') to find the nearest matching chore. " +
        "target_x and target_y are optional — when omitted the nearest matching chore to the duplicate is chosen automatically. " +
        "Use get_errand_progress to check if the errand is still in progress.";

    protected override JsonSchema Schema => new()
    {
        Type = JsonSchemaType.Object,
        // errand_type is only required when errand_id is absent; both are accepted.
        Properties = new Dictionary<string, JsonSchema>
        {
            ["errand_id"] = new JsonSchema
            {
                Type = JsonSchemaType.Integer
                // ID from list_errands. Preferred over errand_type for exact targeting.
            },
            ["errand_type"] = new JsonSchema
            {
                Type = JsonSchemaType.String,
                // Fallback: chore type name from list_errands (e.g. 'Dig', 'Build').
            },
            ["max_distance"] = new JsonSchema
            {
                Type = JsonSchemaType.Integer
            },
            ["target_x"] = new JsonSchema
            {
                Type = JsonSchemaType.Integer
            },
            ["target_y"] = new JsonSchema
            {
                Type = JsonSchemaType.Integer
            }
        }
    };

    /// <summary>
    /// Resolve the requested errand type, find a nearby available chore, and prepare
    /// execution-time helpers stored in <see cref="AssignData"/>.
    /// </summary>
    /// <param name="actionData">The incoming request payload.</param>
    /// <param name="parsedData">Receives the parsed request plus runtime objects resolved during validation.</param>
    /// <returns>The validation result describing whether an assignable chore was found.</returns>
    /// <pre><paramref name="actionData"/> contains the requested errand type and optional selection constraints.</pre>
    /// <post>On success <paramref name="parsedData"/> contains the parsed request plus the runtime objects needed for execution.</post>
    protected override ExecutionResult Validate(ActionJData actionData, out AssignData? parsedData)
    {
        parsedData = new AssignData
        {
            ErrandId = actionData.Data?["errand_id"]?.Value<int>(),
            ErrandType = actionData.Data?["errand_type"]?.Value<string>(),
            MaxDistance = actionData.Data?["max_distance"]?.Value<int>() ?? 50,
            TargetX = actionData.Data?["target_x"]?.Value<int>(),
            TargetY = actionData.Data?["target_y"]?.Value<int>()
        };

        // At least one targeting mode must be present.
        if (parsedData.ErrandId == null && string.IsNullOrEmpty(parsedData.ErrandType))
            return ExecutionResult.Failure("Provide errand_id (from list_errands) or errand_type to assign an errand");

        if (parsedData.MaxDistance < 0)
            return ExecutionResult.Failure("max_distance must be zero or greater");

        if (_neuroMinion == null || _neuroMinion.gameObject == null)
            return ExecutionResult.Failure("Neuro duplicate not found");

        // Resolve ChoreConsumer
        ChoreConsumer? choreConsumer = _neuroMinion.GetComponent<ChoreConsumer>();
        if (choreConsumer == null)
            return ExecutionResult.Failure("Failed to assign errand: ChoreConsumer not found on Neuro");

        Chore? targetChore = null;
        ChoreType? choreType = null;
        ChoreGroup? choreGroup = null;

        if (parsedData.ErrandId.HasValue)
        {
            // Preferred path: look up the exact chore from the last list_errands scan.
            targetChore = ResolveChoreFromErrandId(parsedData.ErrandId.Value, parsedData);
            if (targetChore == null)
            {
                return ExecutionResult.Failure(
                    $"errand_id {parsedData.ErrandId} is no longer valid — call list_errands again to refresh the list");
            }

            choreType = targetChore.choreType;
            choreGroup = GetChoreGroup(choreType);

            if (choreGroup == null)
                return ExecutionResult.Failure($"Failed to assign errand: ChoreGroup not found for chore type '{choreType.Name}'");

            if (!choreConsumer.IsPermittedByUser(choreGroup))
            {
                return ExecutionResult.Failure(
                    $"Cannot assign errand [{parsedData.ErrandId}] — the duplicant's '{choreGroup.Name}' " +
                    $"chore group is disabled. Enable '{choreGroup.Name}' in the duplicant's priorities panel.");
            }

            if (targetChore.driver != null)
            {
                string takenBy = targetChore.driver.GetComponent<MinionIdentity>()?.GetProperName() ?? "another duplicant";
                return ExecutionResult.Failure(
                    $"Errand [{parsedData.ErrandId}] is already being performed by {takenBy}. Call list_errands to find a free one.");
            }

            // Populate ErrandType from the resolved chore for the success message.
            parsedData.ErrandType = choreType.Name;
        }
        else
        {
            ChoreTypeMatch choreMatch = ResolveChoreTypeMatch(parsedData.ErrandType!);
            if (!choreMatch.IsMatch)
            {
                if (IsScheduleGatedErrandRequest(parsedData.ErrandType!))
                {
                    return ExecutionResult.Failure(
                        $"Cannot assign '{parsedData.ErrandType}' with the current schedule. " +
                        "This errand only becomes available during the appropriate schedule block.");
                }

                return ExecutionResult.Failure(
                    $"Failed to assign errand: errand type or chore group '{parsedData.ErrandType}' not found");
            }

            choreType = choreMatch.ChoreType;
            choreGroup = choreMatch.ChoreGroup;

            if (choreGroup == null)
            {
                return ExecutionResult.Failure(
                    $"Failed to assign errand: ChoreGroup not found for '{parsedData.ErrandType}'");
            }

            if (!choreConsumer.IsPermittedByUser(choreGroup))
            {
                return ExecutionResult.Failure(
                    $"Cannot assign '{parsedData.ErrandType}' — the duplicant's '{choreGroup.Name}' " +
                    $"chore group is disabled in their personal settings. " +
                    $"Enable '{choreGroup.Name}' in the duplicant's priorities panel, or choose a different errand type.");
            }

            targetChore = FindNearestChore(choreMatch, parsedData);
            if (targetChore == null)
            {
                return ExecutionResult.Failure(
                    $"No available {parsedData.ErrandType} errands found within {parsedData.MaxDistance} tiles");
            }

            choreType = targetChore.choreType;
            choreGroup = GetChoreGroup(choreType);
            parsedData.ErrandType = choreMatch.DisplayName;
        }

        // Store resolved data for ExecuteAsync
        parsedData.Consumer = choreConsumer;
        parsedData.ResolvedGroup = choreGroup;
        parsedData.TargetChore = targetChore;
        parsedData.OldPriority = choreConsumer.GetPersonalPriority(choreGroup);

        // Build success message
        Vector3 chorePos = targetChore.target.transform.position;
        float distance = Vector3.Distance(_neuroMinion.transform.position, chorePos);

        string description = $"{parsedData.ErrandType} at ({chorePos.x:F0}, {chorePos.y:F0}) - {distance:F1} tiles away";
        string message = $"Assigned errand: {description}. " +
            $"Boosted {choreGroup.Name} priority from {parsedData.OldPriority} to 5. " +
            $"The duplicate will finish this task before starting other work. " +
            $"Use get_errand_progress to check status.";

        NeuroLogger.Log(message, "AssignErrand", null);
        return ExecutionResult.Success(message);
    }

    /// <summary>
    /// Apply side-effects required to assign and lock the errand: boost priority, attach monitor,
    /// reserve the chore and start tracking completion.
    /// </summary>
    /// <param name="parsedData">The parsed request with the runtime objects resolved during validation.</param>
    /// <pre><paramref name="parsedData"/> contains the resolved chore, chore group, and consumer selected during validation.</pre>
    /// <post>The target duplicant's priority, errand monitor, reservation state, and completion tracking have been updated for the selected errand.</post>
    protected override UniTask ExecuteAsync(AssignData? parsedData)
    {
        if (parsedData?.Consumer == null || parsedData.ResolvedGroup == null ||
            parsedData.TargetChore == null || _neuroMinion == null)
        {
            NeuroLogger.LogError("[AssignErrandAction] Invalid state during execution", "AssignErrand", null);
            return UniTask.CompletedTask;
        }

        try
        {
            const int MAX_PRIORITY = 5;

            // Step 1: Boost priority to maximum
            parsedData.Consumer.SetPersonalPriority(parsedData.ResolvedGroup, MAX_PRIORITY);

            // Step 2: Set up the ErrandMonitor to track completion
            ErrandMonitor? monitor = _neuroMinion.GetComponent<ErrandMonitor>();
            if (monitor == null)
            {
                monitor = _neuroMinion.gameObject.AddComponent<ErrandMonitor>();
                NeuroLogger.Log("Added ErrandMonitor component to Neuro", "AssignErrand", null);
            }

            // Step 3: Begin completion tracking
            Vector3 chorePos = parsedData.TargetChore.target.transform.position;
            ErrandCompletionTracker.Instance.BeginTracking(
                parsedData.TargetChore.choreType.Name,
                parsedData.ResolvedGroup.Name,
                (int)chorePos.x,
                (int)chorePos.y
            );

            // Step 4: Start acquiring the exact chore and remember the temporary priority override
            monitor.StartAcquiring(parsedData.TargetChore, parsedData.ResolvedGroup, parsedData.OldPriority);

            NeuroLogger.Log("AssignErrandAction side effects completed successfully", "AssignErrand", null);
        }
        catch (Exception ex)
        {
            NeuroLogger.LogError($"Error during errand assignment execution: {ex.Message}", "AssignErrand", null);
        }

        return UniTask.CompletedTask;
    }

    /// <summary>
    /// Find the nearest available chore of the specified type, optionally honoring a target location.
    /// </summary>
    /// <param name="choreType">The chore type to search for.</param>
    /// <param name="parsedData">The parsed assign-errand request.</param>
    /// <returns>The nearest matching available chore, or null when none qualifies.</returns>
    /// <pre><paramref name="choreType"/> is valid in the current chore database and <paramref name="parsedData"/> contains the caller's selection constraints.</pre>
    /// <post>The returned chore is the nearest available match that satisfies assignment, permission, and distance constraints.</post>
    private Chore? FindNearestChore(ChoreTypeMatch choreMatch, AssignData parsedData)
    {
        if (GlobalChoreProvider.Instance == null)
        {
            return null;
        }

        // Get ChoreConsumer for permission check
        ChoreConsumer? choreConsumer = _neuroMinion.GetComponent<ChoreConsumer>();
        ChoreGroup? targetGroup = choreMatch.ChoreGroup;

        Vector3 neuroPos = _neuroMinion.transform.position;
        Chore? nearestChore = null;
        float nearestDistance = float.MaxValue;

        // Check if we have a specific target location
        bool hasTargetLocation = parsedData.TargetX.HasValue && parsedData.TargetY.HasValue;
        Vector3 targetPos = hasTargetLocation
            ? new Vector3(parsedData.TargetX!.Value, parsedData.TargetY!.Value, 0)
            : neuroPos;

        foreach (KeyValuePair<int, List<Chore>> kvp in GlobalChoreProvider.Instance.choreWorldMap)
        {
            foreach (Chore chore in kvp.Value)
            {
                if (chore == null || chore.target == null || chore.isComplete)
                {
                    continue;
                }

                if (!choreMatch.Matches(chore.choreType))
                {
                    continue;
                }

                // Check if assigned
                if (chore.driver != null)
                {
                    continue; // Skip already assigned chores
                }

                // Check if permitted by user settings (defense-in-depth)
                if (choreConsumer != null && targetGroup != null && !choreConsumer.IsPermittedByUser(targetGroup))
                {
                    continue;
                }

                Vector3 chorePos = chore.target.transform.position;
                float distance = Vector3.Distance(targetPos, chorePos);

                if (distance > parsedData.MaxDistance)
                {
                    continue;
                }

                // Track nearest
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearestChore = chore;
                }
            }
        }

        if (nearestChore != null)
        {
            NeuroLogger.Log($"Found nearest chore: {nearestChore.choreType.Name} at distance {nearestDistance:F1}", "AssignErrand", null);
        }

        return nearestChore;
    }

    /// <summary>
    /// Resolves an assign-errand fallback token to either one exact chore type or a chore-group category.
    /// </summary>
    /// <param name="requestedType">The caller-supplied errand type token.</param>
    /// <returns>The resolved match information.</returns>
    /// <pre><paramref name="requestedType"/> contains the raw errand_type value from the action payload.</pre>
    /// <post>The returned match identifies either an exact chore type or a chore-group category when one exists.</post>
    internal static ChoreTypeMatch ResolveChoreTypeMatch(string requestedType)
    {
        if (string.IsNullOrWhiteSpace(requestedType))
        {
            throw new ArgumentException("Value cannot be null or whitespace.", nameof(requestedType));
        }

        ChoreType? choreType = FindChoreType(requestedType);
        if (choreType != null)
        {
            return new ChoreTypeMatch(choreType, GetChoreGroup(choreType), requestedType.Trim());
        }

        ChoreGroup? choreGroup = FindChoreGroup(requestedType);
        if (choreGroup != null)
        {
            return new ChoreTypeMatch(choreGroup, requestedType.Trim());
        }

        return ChoreTypeMatch.None(requestedType.Trim());
    }

    /// <summary>
    /// Resolves an errand id from the most recent list scan and tries to re-find the chore when the cached reference went stale.
    /// </summary>
    /// <param name="errandId">The errand id returned by list_errands.</param>
    /// <param name="parsedData">The parsed assignment request used for fallback targeting.</param>
    /// <returns>The live chore for the errand id when it can still be found; otherwise null.</returns>
    /// <pre>The errand id originated from the latest list_errands response for this game session.</pre>
    /// <post>The return value is a live chore reference when the original or re-resolved errand still exists; stale cache entries are cleared when the chore cannot be recovered.</post>
    internal Chore? ResolveChoreFromErrandId(int errandId, AssignData parsedData)
    {
        if (ListErrandsAction.LastScanCache.TryGetValue(errandId, out Chore? cachedChore) &&
            ErrandMonitor.IsChoreAvailable(cachedChore))
        {
            return cachedChore;
        }

        if (!ListErrandsAction.LastScanReferenceCache.TryGetValue(errandId, out ListErrandsAction.ErrandScanReference? scanReference))
        {
            ListErrandsAction.LastScanCache.Remove(errandId);
            return null;
        }

        ChoreType? choreType = FindChoreType(scanReference.ChoreTypeId);
        if (choreType == null)
        {
            ClearCachedErrandId(errandId);
            return null;
        }

        Chore? resolvedChore = FindMatchingChore(choreType, scanReference.LocationX, scanReference.LocationY, parsedData.MaxDistance);
        if (resolvedChore == null)
        {
            ClearCachedErrandId(errandId);
            return null;
        }

        ListErrandsAction.LastScanCache[errandId] = resolvedChore;
        return resolvedChore;
    }

    /// <summary>
    /// Finds a live chore matching the supplied chore type and tile location within the allowed search distance.
    /// </summary>
    /// <param name="choreType">The chore type captured by the original scan.</param>
    /// <param name="targetX">The scanned target X coordinate.</param>
    /// <param name="targetY">The scanned target Y coordinate.</param>
    /// <param name="maxDistance">The maximum allowed distance from the Neuro duplicant.</param>
    /// <returns>The matching live chore when one exists; otherwise null.</returns>
    /// <pre>The original scan metadata identifies one chore candidate by type and tile location.</pre>
    /// <post>The returned chore is unassigned, still available, and matches the captured location for the errand id.</post>
    internal Chore? FindMatchingChore(ChoreType choreType, int targetX, int targetY, int maxDistance)
    {
        if (GlobalChoreProvider.Instance == null || _neuroMinion == null)
        {
            return null;
        }

        Vector3 neuroPos = _neuroMinion.transform.position;

        foreach (KeyValuePair<int, List<Chore>> kvp in GlobalChoreProvider.Instance.choreWorldMap)
        {
            foreach (Chore chore in kvp.Value)
            {
                if (!ErrandMonitor.IsChoreAvailable(chore) || chore.choreType != choreType || chore.driver != null)
                {
                    continue;
                }

                Vector3 chorePos = chore.target.transform.position;
                if ((int)chorePos.x != targetX || (int)chorePos.y != targetY)
                {
                    continue;
                }

                if (Vector3.Distance(neuroPos, chorePos) > maxDistance)
                {
                    continue;
                }

                return chore;
            }
        }

        return null;
    }

    /// <summary>
    /// Removes cached data for an errand id that no longer resolves to a live chore.
    /// </summary>
    /// <param name="errandId">The errand id whose cached entries should be cleared.</param>
    /// <pre>The errand id has been determined to no longer map to a live chore.</pre>
    /// <post>Both the live chore cache and the scan-reference cache no longer contain the supplied errand id.</post>
    internal static void ClearCachedErrandId(int errandId)
    {
        ListErrandsAction.LastScanCache.Remove(errandId);
        ListErrandsAction.LastScanReferenceCache.Remove(errandId);
    }

    /// <summary>Lookup a <see cref="ChoreType"/> by id or display name.</summary>
    /// <param name="typeName">The chore type id or display name.</param>
    /// <returns>The matching chore type, or null when none matches.</returns>
    /// <pre><paramref name="typeName"/> contains the caller-supplied chore type identifier.</pre>
    /// <post>The returned chore type matches the supplied id or display name ignoring case.</post>
    private static ChoreType? FindChoreType(string typeName)
    {
        return Db.Get()?.ChoreTypes == null
            ? null
            : Db.Get().ChoreTypes.resources.FirstOrDefault(
            ct => ct.Id.Equals(typeName, StringComparison.OrdinalIgnoreCase) ||
                  ct.Name.Equals(typeName, StringComparison.OrdinalIgnoreCase)
        );
    }

    /// <summary>Lookup a <see cref="ChoreGroup"/> by id or display name.</summary>
    /// <param name="groupName">The chore group id or display name.</param>
    /// <returns>The matching chore group, or null when none matches.</returns>
    /// <pre><paramref name="groupName"/> contains the caller-supplied chore group identifier.</pre>
    /// <post>The returned chore group matches the supplied id or display name ignoring case when available.</post>
    private static ChoreGroup? FindChoreGroup(string groupName)
    {
        if (Db.Get()?.ChoreGroups == null)
        {
            return null;
        }

        return Db.Get().ChoreGroups.resources.FirstOrDefault(group =>
            group.Id.Equals(groupName, StringComparison.OrdinalIgnoreCase) ||
            group.Name.Equals(groupName, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Determines whether the caller appears to be requesting an errand gated by the duplicant's current schedule.
    /// </summary>
    /// <param name="requestedType">The caller-supplied errand token.</param>
    /// <returns><see langword="true"/> when the token refers to a known schedule-gated activity such as relaxing or using the toilet.</returns>
    /// <pre><paramref name="requestedType"/> contains the raw errand_type value from the action payload.</pre>
    /// <post>The return value indicates whether assign_errand should report a schedule-specific failure instead of attempting to force the errand.</post>
    internal static bool IsScheduleGatedErrandRequest(string requestedType)
    {
        if (string.IsNullOrWhiteSpace(requestedType))
        {
            return false;
        }

        string normalized = requestedType.Trim();
        string[] scheduleGatedTokens =
        [
            "relax",
            "recreation",
            "rec",
            "downtime",
            "bathroom",
            "toilet",
            "use_toilet",
            "lavatory",
            "outhouse",
            "pee",
            "bladder"
        ];

        return scheduleGatedTokens.Any(token =>
            normalized.Equals(token, StringComparison.OrdinalIgnoreCase) ||
            normalized.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0);
    }

    /// <summary>Find the chore group that contains the provided chore type.</summary>
    /// <param name="choreType">The chore type whose group should be resolved.</param>
    /// <returns>The containing chore group, or null when none is found.</returns>
    /// <pre><paramref name="choreType"/> is a valid chore type in the current game database.</pre>
    /// <post>The returned group contains <paramref name="choreType"/> when non-null.</post>
    private static ChoreGroup? GetChoreGroup(ChoreType choreType)
    {
        if (Db.Get()?.ChoreGroups == null)
        {
            return null;
        }

        foreach (ChoreGroup group in Db.Get().ChoreGroups.resources)
        {
            if (group.choreTypes.Contains(choreType))
            {
                return group;
            }
        }
        return null;
    }

    /// <summary>
    /// Represents either an exact chore-type match or a broader chore-group match for assign_errand fallback selection.
    /// </summary>
    /// <param name="ChoreType">The exact resolved chore type when the caller named one directly.</param>
    /// <param name="ChoreGroup">The resolved chore group used for permission checks and group-category matching.</param>
    /// <param name="DisplayName">The normalized label used in messages.</param>
    internal sealed class ChoreTypeMatch
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ChoreTypeMatch"/> class.
        /// </summary>
        /// <param name="choreType">The exact resolved chore type when the caller named one directly.</param>
        /// <param name="choreGroup">The resolved chore group used for permission checks and group-category matching.</param>
        /// <param name="displayName">The normalized label used in messages.</param>
        /// <pre>The supplied type/group values were resolved from the caller's errand token.</pre>
        /// <post>The created instance represents either an exact chore-type match, a chore-group match, or no match.</post>
        public ChoreTypeMatch(ChoreType? choreType, ChoreGroup? choreGroup, string displayName)
        {
            ChoreType = choreType;
            ChoreGroup = choreGroup;
            DisplayName = displayName;
        }

        /// <summary>
        /// Gets the exact resolved chore type when the caller named one directly.
        /// </summary>
        /// <pre>The instance was created by <see cref="ResolveChoreTypeMatch(string)"/>.</pre>
        /// <post>The property returns the exact chore type for direct type matches, or null otherwise.</post>
        public ChoreType? ChoreType { get; }

        /// <summary>
        /// Gets the resolved chore group used for permission checks and group-category matching.
        /// </summary>
        /// <pre>The instance was created by <see cref="ResolveChoreTypeMatch(string)"/>.</pre>
        /// <post>The property returns the resolved chore group when one was found.</post>
        public ChoreGroup? ChoreGroup { get; }

        /// <summary>
        /// Gets the normalized label used in messages.
        /// </summary>
        /// <pre>The instance was created by <see cref="ResolveChoreTypeMatch(string)"/>.</pre>
        /// <post>The property returns the caller-supplied token after normalization.</post>
        public string DisplayName { get; }

        /// <summary>
        /// Gets a value indicating whether the resolution found a matching chore type or chore group.
        /// </summary>
        /// <pre>The instance was created by <see cref="ResolveChoreTypeMatch(string)"/>.</pre>
        /// <post>The property returns <see langword="true"/> when at least one match target is available.</post>
        public bool IsMatch => ChoreType != null || ChoreGroup != null;

        /// <summary>
        /// Gets a value indicating whether the match represents a broad chore-group category instead of an exact chore type.
        /// </summary>
        /// <pre>The instance was created by <see cref="ResolveChoreTypeMatch(string)"/>.</pre>
        /// <post>The property returns <see langword="true"/> when chores should be matched by group membership.</post>
        public bool IsGroupMatch => ChoreType == null && ChoreGroup != null;

        /// <summary>
        /// Determines whether the supplied chore type satisfies this match.
        /// </summary>
        /// <param name="candidate">The chore type to evaluate.</param>
        /// <returns><see langword="true"/> when the candidate matches the requested type or group.</returns>
        /// <pre><paramref name="candidate"/> refers to a live chore type from the current game database.</pre>
        /// <post>The return value reflects whether the candidate should be considered assignable for this match.</post>
        public bool Matches(ChoreType candidate)
        {
            if (candidate == null)
            {
                throw new ArgumentNullException(nameof(candidate));
            }

            if (ChoreType != null)
            {
                return candidate == ChoreType;
            }

            return ChoreGroup?.choreTypes.Contains(candidate) == true;
        }

        /// <summary>
        /// Creates a sentinel instance representing a failed lookup.
        /// </summary>
        /// <param name="displayName">The normalized caller-supplied token.</param>
        /// <returns>An unmatched resolution result.</returns>
        /// <pre><paramref name="displayName"/> contains the normalized errand token the caller supplied.</pre>
        /// <post>The returned instance reports <see cref="IsMatch"/> as <see langword="false"/>.</post>
        public static ChoreTypeMatch None(string displayName) => new(null, null, displayName);

        /// <summary>
        /// Initializes a new instance of the <see cref="ChoreTypeMatch"/> record for a chore-group category.
        /// </summary>
        /// <param name="choreGroup">The matched chore group.</param>
        /// <param name="displayName">The normalized label used in messages.</param>
        /// <pre><paramref name="choreGroup"/> refers to a live chore group from the current game database.</pre>
        /// <post>The created instance matches all chore types contained in <paramref name="choreGroup"/>.</post>
        public ChoreTypeMatch(ChoreGroup choreGroup, string displayName)
            : this(null, choreGroup, displayName)
        {
        }
    }
}

/// <summary>
/// Action to check the progress of the currently assigned errand.
/// </summary>
/// <remarks>
/// Returns a detailed lifecycle summary including monitor state, acquisition, interruptions
/// and the last completed errand when no active assignment exists.
/// </remarks>
/// <pre>
/// Tracking components must be available if an active errand is being monitored.
/// </pre>
/// <post>
/// Successful validation returns the best available progress summary for the current or most recent errand.
/// </post>
public class GetErrandProgressAction(MinionIdentity minion) : NeuroAction<GetErrandProgressAction.EmptyData>
{
    private readonly MinionIdentity _neuroMinion = minion;

    /// <summary>
    /// Represents the empty payload accepted by <see cref="GetErrandProgressAction"/>.
    /// </summary>
    /// <pre>The get-errand-progress action accepts no structured payload.</pre>
    /// <post>The type serves only as a marker indicating successful parsing of an empty request.</post>
    public class EmptyData { }

    public override string Name => "get_errand_progress";

    protected override string Description =>
        "Get the progress and status of the currently assigned errand. " +
        "Shows whether the errand is being acquired, in progress, interrupted, completed, or failed. " +
        "Also shows the last completed errand if no current errand is active.";

    protected override JsonSchema? Schema => null;

    /// <summary>
    /// Gather progress information from <see cref="ErrandCompletionTracker"/> and the duplicant's monitor.
    /// </summary>
    /// <param name="actionData">The incoming request payload.</param>
    /// <param name="parsedData">Receives the parsed empty payload marker.</param>
    /// <returns>The validation result containing the current or most recent errand progress summary.</returns>
    /// <pre><paramref name="actionData"/> may be empty because this action accepts no structured input.</pre>
    /// <post>On success <paramref name="parsedData"/> contains the empty marker and the returned message summarizes the current or last known errand state.</post>
    protected override ExecutionResult Validate(ActionJData actionData, out EmptyData? parsedData)
    {
        parsedData = new EmptyData();

        if (_neuroMinion == null || _neuroMinion.gameObject == null)
            return ExecutionResult.Failure("Neuro duplicate not found or not available");

        try
        {
            ErrandCompletionTracker tracker = ErrandCompletionTracker.Instance;
            ErrandCompletionTracker.ErrandProgress? current = tracker.CurrentProgress;
            ErrandCompletionTracker.ErrandProgress? last = tracker.LastCompletedProgress;

            string message;

            if (current != null)
            {
                message = $"Current errand: {current.GetSummary()}";

                // Add ErrandMonitor state info
                ErrandMonitor? monitor = _neuroMinion.GetComponent<ErrandMonitor>();
                if (monitor != null)
                {
                    message += $"\nMonitor: active:{monitor.HasActiveAssignment}, " +
                               $"acquiring:{monitor.IsAcquiring}";

                    if (monitor.AllowedChore != null)
                    {
                        message += $", locked:{monitor.AllowedChore.choreType.Name}";
                    }
                }

                // Add current chore from ChoreDriver for comparison
                ChoreConsumer? choreConsumer = _neuroMinion.GetComponent<ChoreConsumer>();
                if (choreConsumer?.choreDriver.HasChore() == true)
                {
                    Chore? currentChore = choreConsumer.choreDriver.GetCurrentChore();
                    if (currentChore != null)
                    {
                        message += $"\nDuplicate is doing: {currentChore.choreType.Name} " +
                                   $"(priority: {currentChore.masterPriority.priority_value})";
                    }
                }
                else
                {
                    message += "\nDuplicate is currently idle";
                }
            }
            else if (last != null)
            {
                message = $"No active errand. Last errand: {last.GetSummary()}";
            }
            else
            {
                // If the tracker doesn't have any data, still try to report
                // the duplicate's current chore via ChoreConsumer so tests
                // and clients receive useful information.
                ChoreConsumer? choreConsumer = _neuroMinion.GetComponent<ChoreConsumer>();
                if (choreConsumer?.choreDriver.HasChore() == true)
                {
                    Chore? currentChore = choreConsumer.choreDriver.GetCurrentChore();
                    if (currentChore != null)
                    {
                        message = $"Duplicate is currently doing: {currentChore.choreType.Name} (priority: {currentChore.masterPriority.priority_value})";
                    }
                    else
                    {
                        message = "Duplicate has a chore assigned but its details are unavailable.";
                    }
                }
                else
                {
                    message = "No errand has been assigned yet. Use assign_errand to assign one.";
                }
            }

            NeuroLogger.Log(message, "ErrandProgress", null);
            return ExecutionResult.Success(message);
        }
        catch (Exception ex)
        {
            NeuroLogger.LogError($"Error getting errand progress: {ex.Message}", "ErrandProgress", null);
            return ExecutionResult.Failure($"Error getting errand progress: {ex.Message}");
        }
    }

    /// <summary>No asynchronous work is required for this action.</summary>
    /// <param name="parsedData">The parsed empty payload marker.</param>
    /// <pre>The get-errand-progress action computes its full response during validation.</pre>
    /// <post>No additional side effects occur during execution.</post>
    protected override UniTask ExecuteAsync(EmptyData? parsedData)
    {
        return UniTask.CompletedTask;
    }
}

/// <summary>
/// Action to report whether the currently assigned errand has been successfully picked up.
/// </summary>
/// <remarks>
/// Returns a short acquisition-focused status so callers can quickly tell whether assign_errand
/// has only been accepted, is still acquiring, or has already been picked up by the duplicate.
/// </remarks>
/// <pre>
/// Tracking components must be available if an active errand assignment exists.
/// </pre>
/// <post>
/// Successful validation returns a concise pickup-state message for the current or most recent errand.
/// </post>
public class GetErrandPickupStatusAction(MinionIdentity minion) : NeuroAction<GetErrandPickupStatusAction.EmptyData>
{
    private readonly MinionIdentity _neuroMinion = minion;

    /// <summary>
    /// Represents the empty payload accepted by <see cref="GetErrandPickupStatusAction"/>.
    /// </summary>
    /// <pre>The pickup-status action accepts no structured payload.</pre>
    /// <post>The type serves only as a marker indicating successful parsing of an empty request.</post>
    public class EmptyData { }

    public override string Name => "get_errand_pickup_status";

    protected override string Description =>
        "Report whether the currently assigned errand has been picked up yet. " +
        "Use this right after assign_errand to confirm whether the duplicate is still acquiring the errand, has already picked it up, or has no active assignment.";

    protected override JsonSchema? Schema => null;

    /// <summary>
    /// Builds a concise pickup-status response for the currently tracked errand.
    /// </summary>
    /// <param name="actionData">The incoming request payload.</param>
    /// <param name="parsedData">Receives the parsed empty payload marker.</param>
    /// <returns>The validation result containing the current pickup status message.</returns>
    /// <pre><paramref name="actionData"/> may be empty because this action accepts no structured input.</pre>
    /// <post>On success <paramref name="parsedData"/> contains the empty marker and the returned message reports whether the current errand has been picked up.</post>
    protected override ExecutionResult Validate(ActionJData actionData, out EmptyData? parsedData)
    {
        parsedData = new EmptyData();

        if (_neuroMinion == null || _neuroMinion.gameObject == null)
        {
            return ExecutionResult.Failure("Neuro duplicate not found or not available");
        }

        try
        {
            ErrandCompletionTracker tracker = ErrandCompletionTracker.Instance;
            ErrandMonitor? monitor = _neuroMinion.GetComponent<ErrandMonitor>();
            string message = BuildPickupStatusMessage(
                tracker.CurrentProgress,
                tracker.LastCompletedProgress,
                monitor?.HasActiveAssignment == true,
                monitor?.IsAcquiring == true);
            NeuroLogger.Log(message, "ErrandPickupStatus", null);
            return ExecutionResult.Success(message);
        }
        catch (Exception ex)
        {
            NeuroLogger.LogError($"Error getting errand pickup status: {ex.Message}", "ErrandPickupStatus", null);
            return ExecutionResult.Failure($"Error getting errand pickup status: {ex.Message}");
        }
    }

    /// <summary>
    /// No asynchronous work is required for this action.
    /// </summary>
    /// <param name="parsedData">The parsed empty payload marker.</param>
    /// <pre>The pickup-status action computes its full response during validation.</pre>
    /// <post>No additional side effects occur during execution.</post>
    protected override UniTask ExecuteAsync(EmptyData? parsedData)
    {
        return UniTask.CompletedTask;
    }

    /// <summary>
    /// Builds a concise pickup-state message from the current tracker and monitor state.
    /// </summary>
    /// <param name="current">The currently tracked errand, if any.</param>
    /// <param name="last">The most recent completed or failed errand, if any.</param>
    /// <param name="hasActiveAssignment">Whether the errand monitor still reports an active assignment.</param>
    /// <param name="isAcquiring">Whether the errand monitor is still waiting for the duplicant to pick up the errand.</param>
    /// <returns>A concise status describing whether the current errand is acquiring, picked up, or absent.</returns>
    /// <pre>The supplied tracker and monitor values were captured from one point-in-time errand status query.</pre>
    /// <post>The returned string reports the best available pickup state for the current or last known errand.</post>
    internal static string BuildPickupStatusMessage(
        ErrandCompletionTracker.ErrandProgress? current,
        ErrandCompletionTracker.ErrandProgress? last,
        bool hasActiveAssignment,
        bool isAcquiring)
    {
        if (current == null)
        {
            if (last != null)
            {
                return $"No active errand. Last errand ended as {last.State}: {last.ChoreTypeName}.";
            }

            return "No errand is currently assigned.";
        }

        switch (current.State)
        {
            case ErrandCompletionTracker.ErrandState.Acquiring:
                return $"Errand not picked up yet: waiting for duplicant to start {current.ChoreTypeName} at ({current.TargetX},{current.TargetY}).";

            case ErrandCompletionTracker.ErrandState.InProgress:
                return $"Errand picked up successfully: duplicant is performing {current.ChoreTypeName} at ({current.TargetX},{current.TargetY}).";

            case ErrandCompletionTracker.ErrandState.Interrupted:
                return $"Errand was picked up, but is currently interrupted: {current.ChoreTypeName} at ({current.TargetX},{current.TargetY}).";

            default:
                if (hasActiveAssignment && !isAcquiring)
                {
                    return $"Errand picked up successfully: duplicant is locked onto {current.ChoreTypeName}.";
                }

                return $"Errand status is {current.State}: {current.ChoreTypeName} at ({current.TargetX},{current.TargetY}).";
        }
    }
}
