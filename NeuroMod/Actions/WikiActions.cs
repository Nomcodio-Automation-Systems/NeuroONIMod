#nullable enable
using Cysharp.Threading.Tasks;
using NeuroSdk.Actions;
using NeuroSdk.Json;
using NeuroSdk.Websocket;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NeuroMod;

// ─────────────────────────────────────────────────────────────────────────────
// Helper that extracts plain text from a CodexEntry / SubEntry
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Utility that reads plain text out of ONI's in-game Codex (the internal wiki).
/// </summary>
/// <invariant>Stateless; all methods are pure helpers.</invariant>
internal static class CodexReader
{
    /// <summary>
    /// Collects every unique top-level category name from the codex.
    /// </summary>
    /// <returns>Sorted list of distinct category names.</returns>
    /// <pre><see cref="CodexCache.entries"/> has been populated by the game.</pre>
    /// <post>Result contains no duplicates and is sorted A-Z.</post>
    public static List<string> GetCategories()
    {
        var cats = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (CodexCache.entries == null) return new List<string>();
        foreach (var kvp in CodexCache.entries)
        {
            string cat = kvp.Value?.category ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(cat))
                cats.Add(cat);
        }
        var result = cats.ToList();
        result.Sort(StringComparer.OrdinalIgnoreCase);
        return result;
    }

    /// <summary>
    /// Searches for codex entries whose title, id, or body text contains
    /// <paramref name="query"/> (case-insensitive).
    /// </summary>
    /// <param name="query">Search term.</param>
    /// <param name="maxResults">Maximum number of results to return.</param>
    /// <returns>List of (id, title, category) tuples matching the query.</returns>
    /// <pre><paramref name="query"/> is non-null and non-empty.</pre>
    /// <post>Returns at most <paramref name="maxResults"/> items.</post>
    public static List<(string Id, string Title, string Category)> Search(string query, int maxResults = 20)
    {
        var results = new List<(string, string, string)>();
        if (CodexCache.entries == null || string.IsNullOrWhiteSpace(query))
            return results;

        foreach (var kvp in CodexCache.entries)
        {
            if (results.Count >= maxResults) break;
            CodexEntry? entry = kvp.Value;
            if (entry == null) continue;

            bool hit = ContainsIgnoreCase(entry.title, query)
                    || ContainsIgnoreCase(entry.id,    query)
                    || ContainsIgnoreCase(entry.name,  query)
                    || EntryBodyContains(entry, query);

            if (hit)
            {
                string cleanTitle = StripRichText(entry.title ?? entry.name ?? kvp.Key);
                results.Add((entry.id ?? kvp.Key, cleanTitle, entry.category ?? ""));
            }
        }
        return results;
    }

    /// <summary>
    /// Reads the full text of a codex entry by its ID.
    /// Includes sub-entries if present.
    /// </summary>
    /// <param name="id">The codex entry ID (case-insensitive).</param>
    /// <returns>Human-readable text, or <c>null</c> if not found.</returns>
    /// <pre><paramref name="id"/> is non-null.</pre>
    /// <post>Returns <c>null</c> when the entry does not exist.</post>
    public static string? ReadEntry(string id)
    {
        if (CodexCache.entries == null) return null;
        CodexEntry? entry = CodexCache.FindEntry(CodexCache.FormatLinkID(id));
        if (entry == null)
        {
            // Try a case-insensitive scan as fallback
            foreach (var kvp in CodexCache.entries)
            {
                if (string.Equals(kvp.Key, id, StringComparison.OrdinalIgnoreCase)
                 || string.Equals(kvp.Value?.title, id, StringComparison.OrdinalIgnoreCase)
                 || string.Equals(kvp.Value?.name,  id, StringComparison.OrdinalIgnoreCase))
                {
                    entry = kvp.Value;
                    break;
                }
            }
        }
        if (entry == null) return null;

        var sb = new StringBuilder();
        sb.AppendLine($"# {StripRichText(entry.title ?? entry.name ?? entry.id)}");
        if (!string.IsNullOrWhiteSpace(entry.subtitle))
            sb.AppendLine($"_{StripRichText(entry.subtitle)}_");
        sb.AppendLine();

        AppendContainers(sb, entry.contentContainers, indent: "");

        // Sub-entries (e.g. research-tier descriptions, element variants)
        if (entry.subEntries != null && entry.subEntries.Count > 0)
        {
            foreach (SubEntry sub in entry.subEntries)
            {
                if (sub == null) continue;
                sb.AppendLine();
                sb.AppendLine($"## {StripRichText(sub.name ?? sub.id)}");
                AppendContainers(sb, sub.contentContainers, indent: "  ");
            }
        }

        return sb.ToString().TrimEnd();
    }

    // ── private helpers ────────────────────────────────────────────────────

    private static void AppendContainers(StringBuilder sb,
                                          List<ContentContainer>? containers,
                                          string indent)
    {
        if (containers == null) return;
        foreach (ContentContainer cc in containers)
        {
            if (cc?.content == null) continue;
            foreach (ICodexWidget widget in cc.content)
            {
                string? text = ExtractText(widget);
                if (!string.IsNullOrWhiteSpace(text))
                    sb.AppendLine($"{indent}{text}");
            }
        }
    }

    private static string? ExtractText(ICodexWidget widget)
    {
        string? raw = widget switch
        {
            CodexText                   ct => ct.text,
            CodexLabelWithIcon          lw => lw.label?.text,
            CodexIndentedLabelWithIcon  il => il.label?.text,
            _                              => null,
        };
        return raw == null ? null : StripRichText(raw);
    }

    private static bool EntryBodyContains(CodexEntry entry, string query)
    {
        if (entry.contentContainers == null) return false;
        foreach (ContentContainer cc in entry.contentContainers)
        {
            if (cc?.content == null) continue;
            foreach (ICodexWidget w in cc.content)
            {
                string? t = ExtractText(w);
                if (ContainsIgnoreCase(t, query)) return true;
            }
        }
        return false;
    }

    private static bool ContainsIgnoreCase(string? source, string query)
        => source != null && source.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;

    /// <summary>
    /// Strips ONI rich-text tags (e.g. &lt;link="X"&gt;text&lt;/link&gt;, &lt;color&gt;, &lt;b&gt;) from a string.
    /// </summary>
    /// <param name="input">Raw string possibly containing markup.</param>
    /// <returns>Plain text, or empty string if input is null/whitespace.</returns>
    /// <pre>None.</pre>
    /// <post>No XML/HTML-style tags remain in the result.</post>
    internal static string StripRichText(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;
        return System.Text.RegularExpressions.Regex
            .Replace(input!, @"<[^>]+>", string.Empty)
            .Trim();
    }
}


// ─────────────────────────────────────────────────────────────────────────────
// Action 1: list_wiki_categories
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Lists all top-level categories available in the in-game Codex (wiki).
/// Use this first to understand what topics the wiki covers before searching.
/// </summary>
/// <invariant>Read-only; never mutates game state.</invariant>
public class ListWikiCategoriesAction : BaseNeuroAction
{
    /// <inheritdoc/>
    public override string Name => "list_wiki_categories";

    /// <inheritdoc/>
    protected override string Description =>
        "Lists every top-level category in the in-game Codex (the colony's internal wiki). " +
        "Use this to discover what topics are covered — e.g. Buildings, Creatures, Elements, " +
        "Plants, Medicine, Food — before calling search_wiki or get_wiki_entry.";

    /// <inheritdoc/>
    protected override JsonSchema Schema => new() { Type = JsonSchemaType.Object };

    /// <summary>
    /// Returns the sorted list of codex categories.
    /// </summary>
    /// <param name="actionData">Unused.</param>
    /// <param name="parsedData">Always null (no parameters needed).</param>
    /// <returns>A formatted category list.</returns>
    /// <pre>The game's CodexCache has been initialised.</pre>
    /// <post>Result is a bullet list of categories, or a message when none are found.</post>
    protected override ExecutionResult Validate(ActionJData actionData, out object? parsedData)
    {
        parsedData = null;
        try
        {
            List<string> cats = CodexReader.GetCategories();
            if (cats.Count == 0)
                return ExecutionResult.Success("Codex not yet initialised or empty.");

            var sb = new StringBuilder();
            sb.AppendLine($"Wiki Categories ({cats.Count}):");
            foreach (string c in cats)
                sb.AppendLine($"  • {c}");

            return ExecutionResult.Success(sb.ToString().TrimEnd());
        }
        catch (Exception ex)
        {
            NeuroLogger.LogError($"[ListWikiCategoriesAction] {ex.Message}", "ListWikiCategoriesAction", ActionWindow?.TraceId);
            return ExecutionResult.Failure($"Error listing wiki categories: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    protected override UniTask ExecuteAsync(object? parsedData) => UniTask.CompletedTask;
}


// ─────────────────────────────────────────────────────────────────────────────
// Action 2: search_wiki
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Full-text search over the in-game Codex.
/// Returns matching entry IDs and titles that can then be read with
/// <c>get_wiki_entry</c>.
/// </summary>
/// <invariant>Read-only; never mutates game state.</invariant>
public class SearchWikiAction : NeuroAction<SearchWikiAction.SearchRequest>
{
    /// <summary>Carries the search parameters.</summary>
    public class SearchRequest
    {
        /// <summary>Gets or sets the search query string.</summary>
        public string Query { get; set; } = string.Empty;

        /// <summary>Gets or sets the maximum number of results (default 15, max 30).</summary>
        public int MaxResults { get; set; } = 15;

        /// <summary>Gets or sets an optional category filter.</summary>
        public string? Category { get; set; }
    }

    /// <inheritdoc/>
    public override string Name => "search_wiki";

    /// <inheritdoc/>
    protected override string Description =>
        "Searches the in-game Codex for entries matching a keyword or phrase. " +
        "Searches titles, IDs, and body text. Returns a list of matching entries " +
        "(id + title + category) that you can then read in full with get_wiki_entry. " +
        "Optionally filter by 'category' (use list_wiki_categories to see valid values). " +
        "'max_results' defaults to 15, maximum 30.";

    /// <inheritdoc/>
    protected override JsonSchema Schema => new()
    {
        Type = JsonSchemaType.Object,
        Required = new List<string> { "query" },
        Properties = new Dictionary<string, JsonSchema>
        {
            ["query"]       = new JsonSchema { Type = JsonSchemaType.String },
            ["max_results"] = new JsonSchema { Type = JsonSchemaType.Integer },
            ["category"]    = new JsonSchema { Type = JsonSchemaType.String },
        }
    };

    /// <summary>
    /// Validates and performs the codex search.
    /// </summary>
    /// <param name="actionData">Incoming JSON payload.</param>
    /// <param name="parsedData">Parsed <see cref="SearchRequest"/> on success.</param>
    /// <returns>Search results or a failure message.</returns>
    /// <pre>CodexCache is populated.</pre>
    /// <post>Returns at most <c>SearchRequest.MaxResults</c> matches.</post>
    protected override ExecutionResult Validate(ActionJData actionData, out SearchRequest? parsedData)
    {
        parsedData = null;
        try
        {
            string? query = actionData.Data?["query"]?.ToObject<string>();
            if (string.IsNullOrWhiteSpace(query))
                return ExecutionResult.Failure("'query' is required.");

            int maxResults = actionData.Data?["max_results"]?.ToObject<int>() ?? 15;
            maxResults = System.Math.Min(System.Math.Max(1, maxResults), 30);

            string? category = actionData.Data?["category"]?.ToObject<string>();

            parsedData = new SearchRequest { Query = query!, MaxResults = maxResults, Category = category };

            var results = CodexReader.Search(query!, maxResults * 2);

            // Apply optional category filter
            if (!string.IsNullOrWhiteSpace(category))
                results = results.Where(r => ContainsIgnoreCase(r.Category, category)).ToList();

            results = results.Take(maxResults).ToList();

            if (results.Count == 0)
                return ExecutionResult.Success($"No wiki entries found for '{query}'.");

            var sb = new StringBuilder();
            sb.AppendLine($"Wiki search results for '{query}' ({results.Count} found):");
            foreach (var (id, title, cat) in results)
                sb.AppendLine($"  [{cat}] {title}  (id: {id})");
            sb.AppendLine();
            sb.AppendLine("Use get_wiki_entry with the id to read the full article.");

            return ExecutionResult.Success(sb.ToString().TrimEnd());
        }
        catch (Exception ex)
        {
            NeuroLogger.LogError($"[SearchWikiAction] {ex.Message}", "SearchWikiAction", ActionWindow?.TraceId);
            return ExecutionResult.Failure($"Error searching wiki: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    protected override UniTask ExecuteAsync(SearchRequest? parsedData) => UniTask.CompletedTask;

    private static bool ContainsIgnoreCase(string? source, string? query)
        => source != null && query != null
           && source.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
}


// ─────────────────────────────────────────────────────────────────────────────
// Action 3: get_wiki_entry
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Reads the full text of a single Codex entry by its ID.
/// Returns the title, subtitle, body text, and all sub-entries.
/// </summary>
/// <invariant>Read-only; never mutates game state.</invariant>
public class GetWikiEntryAction : NeuroAction<GetWikiEntryAction.WikiRequest>
{
    /// <summary>Carries the entry ID to retrieve.</summary>
    public class WikiRequest
    {
        /// <summary>Gets or sets the codex entry ID (from search_wiki results).</summary>
        public string Id { get; set; } = string.Empty;
    }

    /// <inheritdoc/>
    public override string Name => "get_wiki_entry";

    /// <inheritdoc/>
    protected override string Description =>
        "Reads the full text of a Codex (wiki) entry by its ID. " +
        "The ID comes from search_wiki results. " +
        "Returns the entry title, subtitle, all body paragraphs, and any sub-entries " +
        "(e.g. variants, research tiers, life-cycle stages).";

    /// <inheritdoc/>
    protected override JsonSchema Schema => new()
    {
        Type = JsonSchemaType.Object,
        Required = new List<string> { "id" },
        Properties = new Dictionary<string, JsonSchema>
        {
            ["id"] = new JsonSchema { Type = JsonSchemaType.String },
        }
    };

    /// <summary>
    /// Looks up and returns the full codex article for the requested ID.
    /// </summary>
    /// <param name="actionData">Incoming JSON payload containing 'id'.</param>
    /// <param name="parsedData">Parsed <see cref="WikiRequest"/> on success.</param>
    /// <returns>The article text or a failure when the entry is not found.</returns>
    /// <pre>CodexCache is populated and <c>id</c> is non-empty.</pre>
    /// <post>Returns the full article or a clear not-found message.</post>
    protected override ExecutionResult Validate(ActionJData actionData, out WikiRequest? parsedData)
    {
        parsedData = null;
        try
        {
            string? id = actionData.Data?["id"]?.ToObject<string>();
            if (string.IsNullOrWhiteSpace(id))
                return ExecutionResult.Failure("'id' is required.");

            parsedData = new WikiRequest { Id = id! };

            string? text = CodexReader.ReadEntry(id!);
            if (text == null)
                return ExecutionResult.Failure(
                    $"Wiki entry '{id}' not found. Use search_wiki to find the correct ID.");

            // Truncate very long articles to avoid flooding the context window
            const int MAX_CHARS = 4000;
            if (text.Length > MAX_CHARS)
                text = text.Substring(0, MAX_CHARS) + "\n\n[...article truncated — use search_wiki to find sub-topics]";

            NeuroLogger.Log($"[GetWikiEntryAction] Read entry '{id}'", "GetWikiEntryAction", ActionWindow?.TraceId);
            return ExecutionResult.Success(text);
        }
        catch (Exception ex)
        {
            NeuroLogger.LogError($"[GetWikiEntryAction] {ex.Message}", "GetWikiEntryAction", ActionWindow?.TraceId);
            return ExecutionResult.Failure($"Error reading wiki entry: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    protected override UniTask ExecuteAsync(WikiRequest? parsedData) => UniTask.CompletedTask;
}
