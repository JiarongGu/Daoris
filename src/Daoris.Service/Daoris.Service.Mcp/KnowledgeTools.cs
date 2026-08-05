using System.ComponentModel;
using System.Text;
using Daoris.Knowledge;
using ModelContextProtocol.Server;

namespace Daoris.Knowledge.Mcp;

/// <summary>
/// The tools a session can call.
/// </summary>
/// <remarks>
/// Descriptions are written for the moment of choosing, not the moment of reading documentation —
/// they are what a model matches against when it decides whether a tool applies, so they say what
/// the tool answers rather than what it does internally.
///
/// Results are markdown rather than JSON on purpose: the caller is a language model, and a table it
/// can read beats a structure it has to re-serialise into prose.
/// </remarks>
[McpServerToolType]
public sealed class KnowledgeTools(KnowledgeService service)
{
    [McpServerTool(Name = "knowledge_search")]
    [Description(
        "Search engineering knowledge across every repository in this family: decisions and their "
        + "reasoning, recorded fixes and root causes, completed task outcomes, rules and skills. "
        + "Use it before solving a problem that another repository may already have solved, or to "
        + "find out why something was done the way it was.")]
    public async Task<string> SearchAsync(
        [Description("What to look for, in plain words.")] string query,
        [Description("Restrict to kinds: rule, knowledge, skill, decision, fix, task. Comma-separated; omit for all.")]
        string? kinds = null,
        [Description("Restrict to repositories by name. Comma-separated; omit for all.")]
        string? repositories = null,
        [Description("Only this repository's own knowledge, excluding canonical doctrine installed everywhere. Default true, because canonical content is identical in every repository and rarely what a cross-repository search is for.")]
        bool localOnly = true,
        [Description("Maximum results. Default 10.")] int limit = 10,
        CancellationToken ct = default)
    {
        var hits = await service.SearchAsync(
            new KnowledgeQuery(query)
            {
                Kinds = ParseKinds(kinds),
                Repositories = ParseSet(repositories),
                Provenance = localOnly ? Provenance.Local : null,
                Limit = Math.Clamp(limit, 1, 50),
            }, ct).ConfigureAwait(false);

        if (hits.Count == 0)
        {
            return $"No matches for \"{query}\".\n\n"
                 + "Note this searches by WORD OVERLAP, so a repository that reached the same "
                 + "conclusion in different vocabulary will not match. Try the vocabulary that "
                 + "repository would have used.";
        }

        var text = new StringBuilder();
        text.AppendLine($"{hits.Count} result(s) for \"{query}\":\n");
        foreach (var hit in hits)
        {
            text.AppendLine($"### {hit.Entry.Title}");
            text.AppendLine($"`{hit.Entry.Repository}` · {hit.Entry.Kind} · `{hit.Entry.RelativePath}`");
            text.AppendLine($"id: `{hit.Entry.Id}`");
            if (hit.Excerpt is { Length: > 0 }) text.AppendLine($"\n> {hit.Excerpt}");
            text.AppendLine();
        }

        text.AppendLine("Call `knowledge_get` with an id for the full text.");
        // Which tier answered, on EVERY result and not only on an empty one (D24). A caller who gets
        // results has no way to know the semantic half was absent, and will read "these are the
        // matches" as complete rather than as complete-for-word-overlap.
        if (!service.SemanticEnabled)
        {
            text.AppendLine("_Matched on words only. A repository that reached the same conclusion in "
                          + "different vocabulary will not appear — try its vocabulary._");
        }

        return text.ToString();
    }

    [McpServerTool(Name = "knowledge_get")]
    [Description("Read one knowledge entry in full, by the id returned from knowledge_search.")]
    public async Task<string> GetAsync(
        [Description("The entry id, e.g. `Lyntai:docs/DECISIONS.md#D12 — ...`")] string id,
        CancellationToken ct = default)
    {
        var entry = await service.FindAsync(id, ct).ConfigureAwait(false);
        if (entry is null) return $"No entry with id `{id}`. Ids come from `knowledge_search`.";

        return $"""
                # {entry.Title}

                `{entry.Repository}` · {entry.Kind} · {entry.Provenance} · `{entry.RelativePath}`

                {entry.Body}
                """;
    }

    [McpServerTool(Name = "knowledge_repositories")]
    [Description("List the repositories in the index and how much each contributes. Use it to see what is searchable before searching.")]
    public async Task<string> RepositoriesAsync(CancellationToken ct = default)
    {
        var summary = await service.SummarizeAsync(ct).ConfigureAwait(false);
        if (summary.Count == 0) return "The index is empty. Call `knowledge_refresh` first.";

        var text = new StringBuilder("| Repository | Entries | Local | Canonical |\n|---|---:|---:|---:|\n");
        foreach (var row in summary)
        {
            text.AppendLine($"| {row.Repository} | {row.Total} | {row.Local} | {row.Canonical} |");
        }

        return text.ToString();
    }

    [McpServerTool(Name = "knowledge_convergence")]
    [Description(
        "Find where different repositories learned the SAME lesson independently, including when they "
        + "wrote it in completely different words. Use when deciding what should become shared "
        + "doctrine, or before writing a rule that another repository may already have. Works "
        + "without a model; an embedding endpoint additionally finds different-wording matches.")]
    public async Task<string> ConvergenceAsync(
        [Description("How similar a pair must be, 0 to 1. Higher is stricter. Default 0.82.")]
        double minimumSimilarity = 0.82,
        [Description("Restrict to kinds: rule, knowledge, skill, decision, fix, task. Comma-separated; omit for all.")]
        string? kinds = null,
        [Description("Maximum groups to return. Default 15.")] int limit = 15,
        CancellationToken ct = default)
    {
        var candidates = await service.FindConvergenceAsync(
            new ConvergenceOptions(minimumSimilarity, ParseKinds(kinds), Math.Clamp(limit, 1, 50)), ct)
            .ConfigureAwait(false);

        if (candidates.Count == 0)
        {
            return $"Nothing converges above {minimumSimilarity:0.00}. Lower the threshold to see "
                 + "weaker overlaps — the right value depends on the comparison in use, so it is worth "
                 + "sweeping rather than trusting a default.";
        }

        // Hardest finding first: a convergence is the one nobody could have made by reading file
        // names, and ordering by score alone would bury it under the copies.
        var text = new StringBuilder("A prompt to look, not a merge.\n\n");
        foreach (var group in candidates.GroupBy(c => c.Method).OrderByDescending(g => g.Key))
        {
            text.AppendLine($"## {Heading(group.Key)} ({group.Count()})");
            foreach (var candidate in group) Append(text, candidate);
        }

        if (!service.SemanticEnabled)
        {
            text.AppendLine("_Found by comparing text, which sees copies and restatements. Two "
                          + "repositories that reached the same conclusion in DIFFERENT words will not "
                          + "appear here — read for those, or configure an embedding endpoint to "
                          + "compute them._\n");
        }

        text.AppendLine("Read each group before acting. What they share may be canonical; what differs "
                      + "is usually the repository's own and must stay local.");
        return text.ToString();

        static string Heading(ConvergenceMethod method) => method switch
        {
            ConvergenceMethod.Convergent => "Convergent — same lesson, different words",
            ConvergenceMethod.Restatement => "Restatement — substantially the same words",
            _ => "Identical copies — the same document, pasted",
        };

        static void Append(StringBuilder text, ConvergenceCandidate candidate)
        {
            text.AppendLine($"### {string.Join(" ↔ ", candidate.Repositories)}  ({candidate.Similarity:0.000})");
            foreach (var entry in candidate.Entries)
            {
                text.AppendLine($"- `{entry.Repository}` {entry.Kind} **{entry.Title}** — `{entry.RelativePath}`");
            }
            text.AppendLine();
        }
    }

    [McpServerTool(Name = "knowledge_quests")]
    [Description(
        "List the open quests other repositories have filed across the family — work one repository "
        + "is waiting on another to do. Use it to see what this repository owes, or what is sitting "
        + "unanswered elsewhere. Repositories in this family do not edit each other; they file requests.")]
    public async Task<string> QuestsAsync(CancellationToken ct = default)
    {
        var quests = await service.OpenQuestsAsync(ct).ConfigureAwait(false);
        if (quests.Count == 0) return "No open quests anywhere in the family.";

        var text = new StringBuilder($"{quests.Count} open quest(s):\n");
        foreach (var group in quests.GroupBy(r => r.Repository).OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            text.AppendLine($"## {group.Key} owes {group.Count()}");
            foreach (var quest in group)
            {
                text.AppendLine($"- **{quest.Title}** — `{quest.RelativePath}`");
                text.AppendLine($"  {Text.Excerpt(quest.Body, null, 200)}");
            }

            text.AppendLine();
        }

        return text.ToString();
    }

    [McpServerTool(Name = "knowledge_refresh")]
    [Description("Re-read every repository from disk and rebuild the index. Use after doctrine or decisions have changed; it takes about a second.")]
    public async Task<string> RefreshAsync(CancellationToken ct = default)
    {
        var report = await service.RefreshAsync(ct).ConfigureAwait(false);
        var withheld = report.Withheld > 0 ? $", {report.Withheld} withheld by policy" : "";
        var recall = report.SemanticError is { Length: > 0 } error
            ? $"Lexical recall only — semantic indexing failed and was skipped: {error}"
            : service.SemanticEnabled
                ? "Lexical and semantic recall are both active."
                : "Lexical recall only — set DAORIS_EMBED_MODEL to enable semantic search, which is "
                  + "what finds two repositories that reached the same conclusion in different words.";
        return $"Indexed {report.Entries} entries from {report.Repositories} repositories{withheld}.\n{recall}";
    }

    private static IReadOnlySet<EntryKind>? ParseKinds(string? value)
    {
        var names = ParseSet(value);
        if (names is null) return null;

        var kinds = new HashSet<EntryKind>();
        foreach (var name in names)
        {
            // "task" is friendlier than "taskoutcome", and a model will reach for the short form.
            var normalized = name.Equals("task", StringComparison.OrdinalIgnoreCase) ? "TaskOutcome" : name;
            if (Enum.TryParse<EntryKind>(normalized, ignoreCase: true, out var kind)) kinds.Add(kind);
        }

        return kinds.Count > 0 ? kinds : null;
    }

    private static IReadOnlySet<string>? ParseSet(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var items = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return items.Length > 0 ? new HashSet<string>(items, StringComparer.OrdinalIgnoreCase) : null;
    }
}
