#nullable enable
using Cysharp.Threading.Tasks;
using Klei.AI;
using NeuroSdk.Actions;
using NeuroSdk.Json;
using NeuroSdk.Websocket;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace NeuroMod;

// ─── get_morale_sources ──────────────────────────────────────────────────────

/// <summary>
/// Returns Neuro's morale sources — both positive and negative — so she understands
/// what is making her happy or stressed right now.
/// </summary>
/// <pre>The Neuro duplicant must exist in the scene with an <see cref="AttributeInstance"/> for morale.</pre>
/// <post>Returns a snapshot of morale modifiers without mutating game state.</post>
public class GetMoraleSourcesAction(MinionIdentity minion) : BaseNeuroAction
{
    private readonly MinionIdentity neuroMinion = minion;

    /// <summary>Gets the protocol name for this action.</summary>
    public override string Name => "get_morale_sources";

    /// <summary>Gets the human-readable description registered with the Neuro SDK.</summary>
    protected override string Description =>
        "Returns a breakdown of Neuro's current morale: her total morale value, " +
        "what the colony expectation is, and all individual positive and negative morale sources " +
        "(e.g. good food, cozy room, no bathroom, disease). " +
        "Use this to understand what's making you happy or stressed.";

    /// <summary>Gets the JSON schema (optional format parameter).</summary>
    protected override JsonSchema? Schema => new()
    {
        Type = JsonSchemaType.Object,
        Properties = new Dictionary<string, JsonSchema>
        {
            ["format"] = new JsonSchema
            {
                Type = JsonSchemaType.String,
                Enum = new List<object> { "text", "json" }
            }
        }
    };

    /// <summary>
    /// Reads morale attribute modifiers for the Neuro duplicant.
    /// </summary>
    /// <param name="actionData">Incoming JSON payload.</param>
    /// <param name="parsedData">Always null; result embedded in <see cref="ExecutionResult"/>.</param>
    /// <returns>Success with morale breakdown, or failure when morale data is unavailable.</returns>
    /// <pre>Neuro duplicant is alive and has a morale attribute.</pre>
    /// <post>Game state unchanged.</post>
    protected override ExecutionResult Validate(ActionJData actionData, out object? parsedData)
    {
        parsedData = null;
        try
        {
            if (neuroMinion == null || neuroMinion.gameObject == null)
                return ExecutionResult.Failure("Neuro duplicant not found.");

            string format = actionData.Data?["format"]?.Value<string>() ?? "text";

            MinionModifiers? modifiers = neuroMinion.GetComponent<MinionModifiers>();
            Attributes? attrs = modifiers?.attributes;
            if (attrs == null)
                return ExecutionResult.Failure("Morale attributes are not available for this duplicant.");

            AttributeInstance? moraleAttr = attrs.Get(Db.Get().Attributes.QualityOfLife);
            if (moraleAttr == null)
                return ExecutionResult.Failure("Morale (QualityOfLife) attribute not found.");

            float total      = moraleAttr.GetTotalValue();
            float expectation = GetExpectation(neuroMinion);

            // Collect modifiers from active Effects — this is the same source the game's UI uses.
            // Each Effect carries a list of AttributeModifiers; we filter to QualityOfLife ones.
            var positives = new List<(string Name, float Value)>();
            var negatives = new List<(string Name, float Value)>();
            CollectEffectModifiers(neuroMinion, positives, negatives);

            // Also pick up any direct attribute modifiers that didn't come from Effects
            // (e.g. skill bonuses applied directly to the attribute).
            CollectDirectModifiers(moraleAttr, positives, negatives);

            NeuroLogger.Log($"[GetMoraleSourcesAction] total={total:F1} expectation={expectation:F1} pos={positives.Count} neg={negatives.Count}", "GetMoraleSourcesAction", ActionWindow?.TraceId);

            string result = format == "json"
                ? BuildJson(total, expectation, positives, negatives)
                : BuildText(total, expectation, positives, negatives);

            return ExecutionResult.Success(result);
        }
        catch (Exception ex)
        {
            NeuroLogger.LogError($"[GetMoraleSourcesAction] Error: {ex.Message}", "GetMoraleSourcesAction", ActionWindow?.TraceId);
            return ExecutionResult.Failure($"Error retrieving morale sources: {ex.Message}");
        }
    }

    protected override UniTask ExecuteAsync(object? data) => UniTask.CompletedTask;

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Iterates active <see cref="Effects"/> on the duplicant and collects every
    /// <see cref="AttributeModifier"/> that targets QualityOfLife.
    /// </summary>
    private static void CollectEffectModifiers(MinionIdentity minion,
        List<(string, float)> positives, List<(string, float)> negatives)
    {
        try
        {
            Effects? effects = minion.GetComponent<Effects>();
            if (effects == null) return;

            string moraleAttrId = Db.Get().Attributes.QualityOfLife.Id;

            // GetAllEffectsForSerialization returns SaveLoadEffect structs (id + timeRemaining).
            // We look up each live Effect from Db to access its SelfModifiers.
            foreach (Effects.SaveLoadEffect saved in effects.GetAllEffectsForSerialization())
            {
                if (!Db.Get().effects.Exists(saved.id)) continue;
                Effect effect = Db.Get().effects.Get(saved.id);
                if (effect?.SelfModifiers == null) continue;

                string effectName = ResolveString(effect.Name) ?? effect.Id ?? "Unknown";

                for (int m = 0; m < effect.SelfModifiers.Count; m++)
                {
                    AttributeModifier mod = effect.SelfModifiers[m];
                    if (mod == null || mod.AttributeId != moraleAttrId) continue;
                    float val = mod.Value;
                    if (val > 0) positives.Add((effectName, val));
                    else if (val < 0) negatives.Add((effectName, val));
                }
            }
        }
        catch { }
    }

    /// <summary>
    /// Reads modifiers directly stored on the <see cref="AttributeInstance"/> (e.g. skill-level bonuses),
    /// skipping any already captured via Effects to avoid duplicates.
    /// </summary>
    private static void CollectDirectModifiers(AttributeInstance moraleAttr,
        List<(string, float)> positives, List<(string, float)> negatives)
    {
        try
        {
            for (int i = 0; i < moraleAttr.Modifiers.Count; i++)
            {
                AttributeModifier mod = moraleAttr.Modifiers[i];
                if (mod == null) continue;
                float val = mod.Value;
                if (val == 0f) continue;

                string name = ResolveString(mod.Description) ?? string.Empty;
                if (string.IsNullOrWhiteSpace(name)) continue; // skip unnamed base-value modifiers

                // De-duplicate: skip if the same name+value pair is already in either list.
                bool alreadyCaptured =
                    positives.Exists(p => p.Item1 == name && Math.Abs(p.Item2 - val) < 0.01f) ||
                    negatives.Exists(n => n.Item1 == name && Math.Abs(n.Item2 - val) < 0.01f);
                if (alreadyCaptured) continue;

                if (val > 0) positives.Add((name, val));
                else negatives.Add((name, val));
            }
        }
        catch { }
    }

    /// <summary>
    /// Returns a clean display name from a raw string that may be either an ONI STRINGS key path
    /// (e.g. "STRINGS.DUPLICANTS.MODIFIERS.LATRINE.NAME") or already-resolved plain/rich text.
    /// Rich-text markup such as &lt;link="..."&gt;...&lt;/link&gt; is stripped.
    /// Returns null when the input is null or empty.
    /// </summary>
    private static string? ResolveString(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;

        string result = raw!;

        // Only attempt Strings.Get when the input looks like a STRINGS key path:
        // all-uppercase, dot-separated (e.g. "STRINGS.DUPLICANTS.MODIFIERS.X.NAME").
        // Passing plain text like "Grisly Meal" through Strings.Get returns "MISSING.Grisly Meal".
        if (result.IndexOf('.') >= 0 && result.Equals(result.ToUpperInvariant(), StringComparison.Ordinal))
        {
            try
            {
                string? resolved = Strings.Get(result).String;
                // Strings.Get returns "MISSING.xxx" for unknown keys — treat as plain text in that case.
                if (!string.IsNullOrWhiteSpace(resolved) &&
                    !resolved.StartsWith("MISSING.", StringComparison.Ordinal))
                    result = resolved;
            }
            catch { }
        }

        return StripRichText(result);
    }

    // Strips ONI rich-text tags such as <link="X">text</link>, <color=#fff>text</color>, <b>, etc.
    // Keeps only the inner readable text.
    private static string StripRichText(string s)
    {
        if (s.IndexOf('<') < 0) return s.Trim();
        return System.Text.RegularExpressions.Regex
            .Replace(s, @"<[^>]+>", string.Empty)
            .Trim();
    }

    /// <summary>Reads the duplicant's morale expectation from their MinionResume skill tier.</summary>
    private static float GetExpectation(MinionIdentity minion)
    {
        try
        {
            MinionResume? resume = minion.GetComponent<MinionResume>();
            if (resume == null) return 5f;
            // Count mastered skills — each adds to the expectation tier
            int mastered = 0;
            foreach (var kv in resume.MasteryBySkillID)
                if (kv.Value) mastered++;
            return Mathf.Max(5f + mastered * 2f, 5f);
        }
        catch { return 5f; }
    }

    private static string BuildText(float total, float expect, List<(string Name, float Value)> pos, List<(string Name, float Value)> neg)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Morale: {total:F1} (expectation: {expect:F1})");
        if (pos.Count > 0)
        {
            sb.AppendLine("  Positives:");
            foreach (var (n, v) in pos.OrderByDescending(x => x.Value))
                sb.AppendLine($"    +{v:F1}  {n}");
        }
        if (neg.Count > 0)
        {
            sb.AppendLine("  Negatives:");
            foreach (var (n, v) in neg.OrderBy(x => x.Value))
                sb.AppendLine($"    {v:F1}  {n}");
        }
        return sb.ToString().TrimEnd();
    }

    private static string BuildJson(float total, float expect, List<(string Name, float Value)> pos, List<(string Name, float Value)> neg)
    {
        JArray ToArr(List<(string Name, float Value)> list) =>
            new JArray(list.Select(x => new JObject { ["source"] = x.Name, ["value"] = x.Value }));

        return new JObject
        {
            ["total_morale"]  = total,
            ["expectation"]   = expect,
            ["positives"]     = ToArr(pos),
            ["negatives"]     = ToArr(neg),
        }.ToString();
    }
}

// ─── get_duplicant_thoughts ──────────────────────────────────────────────────

/// <summary>
/// Returns Neuro's current thought bubbles plus contextual colony chatter from the Party Phone.
/// When Neuro is physically on a Party Phone building the lines come from the <c>party_phone</c>
/// category (quiet, personal, one-on-one tone). When she is hallway-socializing the lines come
/// from <c>rumors</c>, <c>small_talk</c>, and <c>observations</c> (louder, group-chat tone).
/// When she is doing neither, the social section is omitted unless explicitly requested.
/// Flavor text is loaded from <c>NeuroMod/Data/party_phone.json</c>.
/// </summary>
/// <pre>The Neuro duplicant must exist in the scene.</pre>
/// <post>Returns thoughts and contextual social lines without mutating game state.</post>
public class GetDuplicantThoughtsAction(MinionIdentity minion) : BaseNeuroAction
{
    private readonly MinionIdentity neuroMinion = minion;

    private static readonly System.Random _rng = new System.Random();

    /// <summary>Describes which social context the duplicant is currently in.</summary>
    private enum SocialContext { None, PartyPhone, HallawaySocial }

    /// <summary>Gets the protocol name for this action.</summary>
    public override string Name => "get_duplicant_thoughts";

    /// <summary>Gets the human-readable description registered with the Neuro SDK.</summary>
    protected override string Description =>
        "Returns Neuro's current thought bubbles (what she is actually thinking right now) " +
        "plus contextual colony chatter. " +
        "When she is on a Party Phone the lines reflect a quiet personal call. " +
        "When she is hallway-socializing the lines reflect group gossip and small talk. " +
        "social_lines controls how many chatter lines to include (default 3, max 10). " +
        "Set include_social to false to suppress chatter entirely.";

    /// <summary>Gets the JSON schema (optional parameters).</summary>
    protected override JsonSchema? Schema => new()
    {
        Type = JsonSchemaType.Object,
        Properties = new Dictionary<string, JsonSchema>
        {
            ["include_social"] = new JsonSchema { Type = JsonSchemaType.Boolean },
            ["social_lines"]   = new JsonSchema { Type = JsonSchemaType.Integer },
        }
    };

    /// <summary>
    /// Reads Neuro's active thought items and picks social lines from the correct category
    /// based on the current social context (party phone vs. hallway vs. neither).
    /// </summary>
    /// <param name="actionData">Incoming JSON payload.</param>
    /// <param name="parsedData">Always null; result embedded in <see cref="ExecutionResult"/>.</param>
    /// <returns>Success with thoughts and contextual social lines.</returns>
    /// <pre>Neuro duplicant is alive.</pre>
    /// <post>Game state unchanged; JSON file is read-only.</post>
    protected override ExecutionResult Validate(ActionJData actionData, out object? parsedData)
    {
        parsedData = null;
        try
        {
            if (neuroMinion == null || neuroMinion.gameObject == null)
                return ExecutionResult.Failure("Neuro duplicant not found.");

            bool includeSocial = actionData.Data?["include_social"]?.Value<bool>() ?? true;
            int  socialLines   = actionData.Data?["social_lines"]?.Value<int>()    ?? 3;
            if (socialLines < 1 || socialLines > 10) socialLines = 3;

            SocialContext context = DetectSocialContext();
            List<string> thoughts = CollectThoughts();
            List<string> social   = includeSocial ? PickSocialLines(socialLines, context) : new List<string>();

            var sb = new System.Text.StringBuilder();

            sb.AppendLine("=== What I'm Thinking ===");
            if (thoughts.Count > 0)
                foreach (string t in thoughts)
                    sb.AppendLine($"  • {t}");
            else
                sb.AppendLine("  (mind is quiet right now)");

            if (social.Count > 0)
            {
                sb.AppendLine();
                string header = context == SocialContext.PartyPhone
                    ? "=== Party Phone Call ==="
                    : "=== Hallway Chat ===";
                sb.AppendLine(header);
                foreach (string line in social)
                    sb.AppendLine($"  \"{line}\"");
            }

            NeuroLogger.Log($"[GetDuplicantThoughtsAction] {thoughts.Count} thoughts, {social.Count} social lines ({context})", "GetDuplicantThoughtsAction", ActionWindow?.TraceId);
            return ExecutionResult.Success(sb.ToString().TrimEnd());
        }
        catch (Exception ex)
        {
            NeuroLogger.LogError($"[GetDuplicantThoughtsAction] Error: {ex.Message}", "GetDuplicantThoughtsAction", ActionWindow?.TraceId);
            return ExecutionResult.Failure($"Error retrieving thoughts: {ex.Message}");
        }
    }

    protected override UniTask ExecuteAsync(object? data) => UniTask.CompletedTask;

    // ── Social context detection ──────────────────────────────────────────────

    /// <summary>
    /// Determines whether the duplicant is on a Party Phone, hallway-socializing, or neither.
    /// </summary>
    private SocialContext DetectSocialContext()
    {
        try
        {
            ChoreConsumer? cc = neuroMinion.GetComponent<ChoreConsumer>();
            if (cc?.choreDriver.HasChore() != true) return SocialContext.None;

            Chore? chore = cc.choreDriver.GetCurrentChore();
            if (chore == null) return SocialContext.None;

            GameObject? target = chore.target?.gameObject;
            if (target != null && target.GetComponent<Telephone>() != null)
                return SocialContext.PartyPhone;

            if (IsSocialChore(chore))
            {
                return SocialContext.HallawaySocial;
            }
        }
        catch { }
        return SocialContext.None;
    }

    /// <summary>
    /// Detects common hallway social chores even when the underlying chore identifier does not use
    /// the exact social keywords expected by the original implementation.
    /// </summary>
    private static bool IsSocialChore(Chore chore)
    {
        string choreId = chore.choreType?.Id ?? string.Empty;
        string choreName = chore.choreType?.Name ?? string.Empty;

        return ContainsIgnoreCase(choreId, "Social")
            || ContainsIgnoreCase(choreName, "Social")
            || ContainsIgnoreCase(choreId, "Chat")
            || ContainsIgnoreCase(choreName, "Chat")
            || ContainsIgnoreCase(choreId, "Talk")
            || ContainsIgnoreCase(choreName, "Talk");
    }

    /// <summary>
    /// Checks whether a string contains a value using ordinal case-insensitive comparison.
    /// </summary>
    private static bool ContainsIgnoreCase(string source, string value)
    {
        return source.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    // ── Thought collection ────────────────────────────────────────────────────

    private List<string> CollectThoughts()
    {
        var thoughts = new List<string>();
        try
        {
            ThoughtGraph.Instance? tg = neuroMinion.GetSMI<ThoughtGraph.Instance>();
            if (tg == null) return thoughts;

            Thought? current = tg.currentThought;
            if (current != null)
            {
                string text = current.hoverText?.ToString() ?? current.Name ?? "...";
                if (!string.IsNullOrWhiteSpace(text))
                    thoughts.Add(text);
            }
        }
        catch { }
        return thoughts;
    }

    // ── Social line selection ─────────────────────────────────────────────────

    /// <summary>
    /// Picks random lines from the appropriate category pool based on social context.
    /// Party Phone → <c>party_phone</c> category only.
    /// Hallway social → <c>rumors</c>, <c>small_talk</c>, <c>observations</c> combined.
    /// None → empty list.
    /// </summary>
    private static List<string> PickSocialLines(int count, SocialContext context)
    {
        if (context == SocialContext.None) return new List<string>();

        EnsurePhoneData();
        if (_phoneData == null) return new List<string>();

        var pool = new List<string>();
        if (context == SocialContext.PartyPhone)
        {
            pool.AddRange(_phoneData.party_phone);
        }
        else
        {
            pool.AddRange(_phoneData.rumors);
            pool.AddRange(_phoneData.small_talk);
            pool.AddRange(_phoneData.observations);
        }

        if (pool.Count == 0) return new List<string>();

        // Fisher-Yates partial shuffle
        var indices = Enumerable.Range(0, pool.Count).ToList();
        var result  = new List<string>();
        for (int i = 0; i < Math.Min(count, pool.Count); i++)
        {
            int j   = _rng.Next(i, indices.Count);
            int tmp = indices[i]; indices[i] = indices[j]; indices[j] = tmp;
            result.Add(pool[indices[i]]);
        }
        return result;
    }

    // ── JSON loading ──────────────────────────────────────────────────────────

    private static PartyPhoneData? _phoneData;
    private static float _phoneLoadedAt = -999f;

    private static void EnsurePhoneData()
    {
        if (_phoneData != null && (UnityEngine.Time.realtimeSinceStartup - _phoneLoadedAt) < 60f)
            return;

        try
        {
            string? path = FindPartyPhonePath();
            if (path == null || !File.Exists(path))
            {
                _phoneData = new PartyPhoneData();
                return;
            }

            string json = File.ReadAllText(path);
            _phoneData = Newtonsoft.Json.JsonConvert.DeserializeObject<PartyPhoneData>(json)
                         ?? new PartyPhoneData();
            _phoneLoadedAt = UnityEngine.Time.realtimeSinceStartup;
        }
        catch
        {
            _phoneData = new PartyPhoneData();
        }
    }

    private static string? FindPartyPhonePath()
    {
        try
        {
            string? modPath = KMod.Manager.GetDirectory();
            if (!string.IsNullOrWhiteSpace(modPath))
            {
                string candidate = Path.Combine(modPath, "NeuroMod", "Data", "party_phone.json");
                if (File.Exists(candidate)) return candidate;
            }
        }
        catch { }

        try
        {
            string asmDir = Path.GetDirectoryName(typeof(GetDuplicantThoughtsAction).Assembly.Location) ?? ".";
            return Path.Combine(asmDir, "Data", "party_phone.json");
        }
        catch { return null; }
    }

    /// <summary>Mirrors the top-level structure of <c>party_phone.json</c>.</summary>
    private class PartyPhoneData
    {
        public List<string> rumors       { get; set; } = new();
        public List<string> small_talk   { get; set; } = new();
        public List<string> observations { get; set; } = new();
        public List<string> party_phone  { get; set; } = new();
    }
}
