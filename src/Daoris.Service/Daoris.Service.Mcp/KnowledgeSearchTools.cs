using System.ComponentModel;
using System.Text;
using ModelContextProtocol.Server;
using static Daoris.Knowledge.Mcp.WorkspaceScope;

namespace Daoris.Knowledge.Mcp;

/// <summary>
/// The tools that read the index (PART10a): <c>knowledge_search</c>, <c>knowledge_get</c>, <c>knowledge_repositories</c>,
/// <c>knowledge_convergence</c>, <c>registry</c> and <c>knowledge_refresh</c>, over the service and the workspace scope.
/// How every tool's words are written is said on <see cref="KnowledgeServer"/>.
/// </summary>
[McpServerToolType]
public sealed class KnowledgeSearchTools(KnowledgeService service, WorkspaceScope circle)
{
    /// <summary>The sentence for a kind nobody has, or null when every name is one (REV3).</summary>
    private static string? UnknownKinds(string? kinds)
    {
        try
        {
            KnowledgeQuery.ParseKinds(kinds);
            return null;
        }
        catch (ArgumentException refused)
        {
            return refused.Message;
        }
    }

    /// <summary>
    /// What an index tool reads, or the index's refusal as the tool's answer (KSCHEMA1): a newer Daoris wrote the index,
    /// and the agent is told so in its sentence rather than handed the protocol's bare error.
    /// </summary>
    private static async Task<(T? Read, string? Refused)> FromIndexAsync<T>(Task<T> reading)
    {
        try
        {
            return (await reading.ConfigureAwait(false), null);
        }
        catch (NewerIndexException refused)
        {
            return (default, refused.Message);
        }
    }

    [McpServerTool(Name = "knowledge_search")]
    [Description(
        "Search engineering knowledge across every repository in this family: decisions and their "
        + "reasoning, recorded fixes and root causes, completed task outcomes, rules and skills, and "
        + "where things are, from each repository's own index of where things are. "
        + "Use it before solving a problem that another repository may already have solved, to "
        + "find out why something was done the way it was, or to find where something is before searching "
        + "the files. Each hit names its file and lines, so read those lines rather than the file.")]
    public async Task<string> SearchAsync(
        [Description("What to look for, in plain words.")] string query,
        [Description("Restrict to kinds: rule, knowledge, skill, decision, fix, task, index. Comma-separated; omit for all, "
            + "which answers with at most two index entries.")]
        string? kinds = null,
        [Description("Restrict to repositories by name. Comma-separated; omit for all.")]
        string? repositories = null,
        [Description("Only this repository's own knowledge, excluding canonical doctrine installed everywhere. Default true, because canonical content is identical in every repository and rarely what a cross-repository search is for.")]
        bool localOnly = true,
        [Description("Maximum results. Default 10.")] int limit = 10,
        [Description(WorkspaceArgument)] string? workspace = null,
        CancellationToken ct = default)
    {
        if (UnknownKinds(kinds) is { } refused) return refused;
        if (circle.Refused(workspace) is { } outside) return outside;
        var scope = await circle.ResolveAsync(workspace, ct).ConfigureAwait(false);
        var (answered, refusedIndex) = await FromIndexAsync(service.AnswerAsync(
            new KnowledgeQuery(query)
            {
                Kinds = KnowledgeQuery.ParseKinds(kinds),
                Repositories = KnowledgeQuery.ParseSet(repositories),
                Provenance = localOnly ? Provenance.Local : null,
                Limit = Math.Clamp(limit, 1, 50),
                Workspace = scope,
            }, ct)).ConfigureAwait(false);
        if (refusedIndex is not null) return refusedIndex;
        var answer = answered!;
        var hits = answer.Hits;

        // 🔴 Nothing answering is not nothing matching (TIER1): an agent told "no matches" by an index
        // nobody could read concludes the family never learned it.
        if (answer.Tier == "none")
        {
            return $"No search answered for \"{query}\"{Scoped(scope)} — {answer.Failure}. This is not an "
                 + "empty result: nothing could read the index. Try again, or call `knowledge_refresh`.";
        }

        if (hits.Count == 0)
        {
            return answer.Semantic
                ? $"No matches for \"{query}\"{Scoped(scope)}, by words or by meaning. "
                  + "`knowledge_convergence` finds where two repositories reached one conclusion."
                : $"No matches for \"{query}\"{Scoped(scope)}.\n\n"
                  + "Note this searches by WORD OVERLAP, so a repository that reached the same "
                  + "conclusion in different vocabulary will not match. Try the vocabulary that "
                  + "repository would have used." + DidNotAnswer(answer);
        }

        var text = new StringBuilder();
        text.AppendLine($"{hits.Count} result(s) for \"{query}\"{Scoped(scope)}:\n");
        foreach (var hit in hits)
        {
            // Its file and lines (ORIENT2e), so the read that follows is a range and not the file; the excerpt
            // says the line it starts on.
            var place = hit.Entry.Lines is { } lines ? $"{hit.Entry.RelativePath}:{lines}" : hit.Entry.RelativePath;
            text.AppendLine($"### {hit.Entry.Title}");
            text.AppendLine($"`{hit.Entry.Repository}` · {hit.Entry.Kind} · `{place}`");
            text.AppendLine($"id: `{hit.Entry.Id}`");
            if (hit.Excerpt is { Length: > 0 })
            {
                text.AppendLine(hit.ExcerptLine is { } line ? $"\n> line {line}: {hit.Excerpt}" : $"\n> {hit.Excerpt}");
            }
            text.AppendLine();
        }

        text.AppendLine("Call `knowledge_get` with an id for the full text, and with `lines` for only the lines a hit names.");
        if (answer.MoreIndex)
        {
            text.AppendLine($"More index entries matched than the {KnowledgeService.IndexEntriesUnasked} named here: "
                          + "search with kinds `index` for where things are.");
        }
        // Which tier ANSWERED, on EVERY result and not only on an empty one (D24) — the answer's, not
        // the configuration's (TIER1). A caller who gets results has no way to know the semantic half
        // was absent, and will read "these are the matches" as complete rather than as
        // complete-for-word-overlap.
        if (!answer.Semantic)
        {
            text.AppendLine("_Matched on words only. A repository that reached the same conclusion in "
                          + "different vocabulary will not appear — try its vocabulary._" + DidNotAnswer(answer));
        }

        return text.ToString();
    }

    /// <summary>Why a configured half did not answer, when one did not — said, never folded away (TIER1).</summary>
    private string DidNotAnswer(SearchAnswer answer) =>
        service.SemanticEnabled && answer.Failure is { Length: > 0 } failure ? $" ({failure}.)" : "";

    [McpServerTool(Name = "knowledge_get")]
    [Description("Read one knowledge entry in full, by the id returned from knowledge_search; or only the lines a hit "
        + "names, so a long entry is read as a passage and not whole.")]
    public async Task<string> GetAsync(
        [Description("The entry id, e.g. `Lyntai:docs/DECISIONS.md#D12 — ...`")] string id,
        [Description("Only these lines of the entry's file, as a hit names them: `12-30`, or `12` for one line. "
            + "Omit for the whole entry.")]
        string? lines = null,
        CancellationToken ct = default)
    {
        var (entry, refusedIndex) = await FromIndexAsync(service.FindAsync(id, ct)).ConfigureAwait(false);
        if (refusedIndex is not null) return refusedIndex;
        if (entry is null) return $"No entry with id `{id}`. Ids come from `knowledge_search`.";
        // ASKSCOPE1c: an id is no argument past the workspace a conversation is bound to.
        if (circle.Outside(entry) is { } outside) return outside;

        var whole = entry.Lines is { } span ? $"{entry.RelativePath}:{span}" : entry.RelativePath;
        if (string.IsNullOrWhiteSpace(lines)) return Read(entry, whole, entry.Body);

        // A range is the file's lines as a hit names them (ORIENT2e), read from the entry indexed: refused when it
        // is no range, said when it misses the entry, and the whole entry when it names no lines to cut at.
        if (LineSpan.Parse(lines) is not { } range)
        {
            return $"`{lines}` is not a range of lines: write `12-30`, or `12` for one line, as a hit names them.";
        }
        if (entry.Lines is not { } own)
        {
            return $"`{entry.Id}` names no lines of its file (it is not its file's lines as written), so here it is whole.\n\n"
                 + Read(entry, whole, entry.Body);
        }
        if (entry.Cut(range) is not { } cut)
        {
            return $"Lines {range} are outside `{entry.Id}`, which is lines {own} of `{entry.RelativePath}`. "
                 + "Ask for a range inside them, or search for the entry that holds those lines.";
        }
        return Read(entry, $"{entry.RelativePath}:{cut.Lines}", cut.Text);

        static string Read(KnowledgeEntry entry, string place, string text) => $"""
            # {entry.Title}

            `{entry.Repository}` · {entry.Kind} · {entry.Provenance} · `{place}`

            {text}
            """;
    }

    [McpServerTool(Name = "knowledge_repositories")]
    [Description("List the repositories in the index and how much each contributes. Use it to see what is searchable before searching.")]
    public async Task<string> RepositoriesAsync(
        [Description(WorkspaceArgument)] string? workspace = null,
        CancellationToken ct = default)
    {
        if (circle.Refused(workspace) is { } outside) return outside;
        var scope = await circle.ResolveAsync(workspace, ct).ConfigureAwait(false);
        var (summarized, refusedIndex) = await FromIndexAsync(service.SummarizeAsync(scope, ct)).ConfigureAwait(false);
        if (refusedIndex is not null) return refusedIndex;
        var summary = summarized!;
        if (summary.Count == 0)
        {
            return scope is null
                ? "The index is empty. Call `knowledge_refresh` first."
                : $"Nothing indexed in workspace `{scope}`. Pass `all` to see every workspace on this machine.";
        }

        var text = new StringBuilder($"What is searchable{Scoped(scope)}:\n\n");
        text.Append("| Repository | Workspace | Entries | Local | Canonical |\n|---|---|---:|---:|---:|\n");
        foreach (var row in summary)
        {
            text.AppendLine(
                $"| {row.Repository} | {row.Workspace} | {row.Total} | {row.Local} | {row.Canonical} |");
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
        [Description(WorkspaceArgument)] string? workspace = null,
        CancellationToken ct = default)
    {
        if (UnknownKinds(kinds) is { } refused) return refused;
        if (circle.Refused(workspace) is { } outside) return outside;
        var scope = await circle.ResolveAsync(workspace, ct).ConfigureAwait(false);
        var (found, refusedIndex) = await FromIndexAsync(service.FindConvergenceAsync(
            new ConvergenceOptions(
                minimumSimilarity, KnowledgeQuery.ParseKinds(kinds), Math.Clamp(limit, 1, 50), scope), ct))
            .ConfigureAwait(false);
        if (refusedIndex is not null) return refusedIndex;
        var candidates = found!;

        if (candidates.Count == 0)
        {
            return $"Nothing converges above {minimumSimilarity:0.00}{Scoped(scope)}. Lower the threshold to see "
                 + "weaker overlaps — the right value depends on the comparison in use, so it is worth "
                 + "sweeping rather than trusting a default.";
        }

        // Hardest finding first: a convergence is the one nobody could have made by reading file
        // names, and ordering by score alone would bury it under the copies.
        var text = new StringBuilder($"A prompt to look, not a merge —{Scoped(scope)}.\n\n");
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

    [McpServerTool(Name = "registry")]
    [Description(
        "Who is in this family, what each repository owns, and what kind of quest is worth addressing "
        + "to it. Use it BEFORE publishing a quest, and whenever a problem might belong to someone "
        + "else — search answers 'has anyone solved this', this answers 'whose problem is this'.")]
    public async Task<string> RegistryAsync(
        [Description(WorkspaceArgument)] string? workspace = null,
        CancellationToken ct = default)
    {
        if (circle.Refused(workspace) is { } outside) return outside;
        var scope = await circle.ResolveAsync(workspace, ct).ConfigureAwait(false);
        var registered =await service.RegistryAsync(scope, ct).ConfigureAwait(false);
        if (registered.Count == 0)
        {
            return scope is null
                ? "Nothing under the knowledge root. Call `knowledge_refresh` first."
                : $"No repositories in workspace `{scope}`. Pass `all` to see every workspace on this machine.";
        }

        var text = new StringBuilder($"The family{Scoped(scope)}:\n\n");
        foreach (var entry in registered.Where(r => r.Adopted))
        {
            text.AppendLine($"## `{entry.Repository}`{(entry.Registered ? "" : "  ⚠ has not declared a domain")}");
            if (entry.Summary is { Length: > 0 }) text.AppendLine(entry.Summary);
            if (entry.Owns.Count > 0) text.AppendLine($"- **owns:** {string.Join("; ", entry.Owns)}");
            if (entry.Accepts.Count > 0) text.AppendLine($"- **accepts:** {string.Join("; ", entry.Accepts)}");
            if (entry.DependsOn.Count > 0) text.AppendLine($"- **uses:** {string.Join(", ", entry.DependsOn)}");
            if (entry.DeclaredLanes.Count > 0)
            {
                // Its lanes (D115 §2.2): what an asker may address inside it, each by its own address.
                text.AppendLine("- **lanes** — a quest to one is work for that lane's session:");
                foreach (var lane in entry.DeclaredLanes)
                {
                    var named = string.Join(" — ", new[] { lane.Title, lane.Summary }.Where(words => words.Length > 0));
                    text.AppendLine($"  - `{entry.Repository}:{lane.Id}`{(named.Length > 0 ? $" — {named}" : "")}"
                                    + (lane.Steward ? " (the steward's: it keeps the records)" : ""));
                }
            }
            else
            {
                // None: said, with what would give it some, since its own sessions read this row too (SELFASK1).
                text.AppendLine($"- **lanes:** none. {QuestExchange.NoLanesWay(entry.Repository)}");
            }

            if (entry.Packs.Count > 0) text.AppendLine($"- packs: {string.Join(", ", entry.Packs)}");
            text.AppendLine($"- {entry.Entries} indexed entries");
            text.AppendLine();
        }

        // Registered is addressable; adopted is disciplined (D70). One that registered here without a
        // manifest can be asked, and only a session the driver starts over the protocol door answers.
        var undisciplined = registered.Where(r => !r.Adopted && r.Addressable).Select(r => r.Repository).ToList();
        if (undisciplined.Count > 0)
        {
            text.AppendLine(
                $"_Registered here but not adopted — addressable, and answered only by a session the driver "
                + $"starts over the protocol door, since none of them has a connector of its own: "
                + $"{string.Join(", ", undisciplined)}._");
        }

        var others = registered.Where(r => !r.Addressable).Select(r => r.Repository).ToList();
        if (others.Count > 0)
        {
            // Listed rather than hidden: "who cannot be asked yet" is the same question, and silence
            // reads as the repository not existing.
            text.AppendLine($"_Not adopted and no root here, so not addressable: {string.Join(", ", others)}._");
        }

        return text.ToString();
    }

    [McpServerTool(Name = "knowledge_refresh")]
    [Description("Re-read every repository from disk and rebuild the index. Use after doctrine or decisions have changed; it takes about a second.")]
    public async Task<string> RefreshAsync(CancellationToken ct = default)
    {
        var (refreshed, refusedIndex) = await FromIndexAsync(service.RefreshAsync(ct)).ConfigureAwait(false);
        if (refusedIndex is not null) return refusedIndex;
        var report = refreshed!;
        var withheld = report.Withheld > 0 ? $", {report.Withheld} withheld by policy" : "";
        // Named, never silently skipped (D48 §3): a registered checkout that has moved contributes
        // nothing while the count still looks healthy — the ghost failure from the other direction.
        var absent = report.Absent.Count > 0
            ? $"\n⚠ Registered but not on disk: {string.Join(", ", report.Absent)} — moved, deleted, or "
              + "registered from another machine. Re-register with `daoris connect`, or retire it."
            : "";
        var recall = report.SemanticError is { Length: > 0 } error
            ? $"Lexical recall only — semantic indexing failed and was skipped: {error}"
            : service.SemanticEnabled
                ? "Lexical and semantic recall are both active."
                : "Lexical recall only — set DAORIS_EMBED_MODEL to enable semantic search, which is "
                  + "what finds two repositories that reached the same conclusion in different words.";
        // Said, never assumed (SEM3, D123): an entry longer than the window is several vectors, and the
        // window is the deployment's statement of what its embedder takes.
        var embedded = report.Embedded is { } made
            ? $"\nEmbedded {made.Entries} entries as {made.Pieces} vectors of at most {made.Window} characters "
              + $"({ServiceOptions.WindowVariable}): "
              + (made.Split switch
              {
                  0 => "none was longer, so none was split.",
                  1 => "1 longer than that was split, each part a vector of its own.",
                  var split => $"{split} longer than that were split, each part a vector of its own.",
              })
            : "";
        return $"Indexed {report.Entries} entries from {report.Repositories} repositories{withheld}.{absent}\n{recall}{embedded}";
    }
}
