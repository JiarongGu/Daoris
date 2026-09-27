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
/// <param name="asks">The asks, for the one session that publishes as one — an intake (D65 §1b).</param>
/// <param name="intake">
/// Which ask this connector speaks for, when the session is an intake; and which session it is, when
/// the driver started it — what a rule proposal records as its author (PERM2).
/// </param>
/// <param name="proposals">Where a proposal to change the rules is written (PERM2, D74) — under the home.</param>
[McpServerToolType]
public sealed class KnowledgeTools(
    KnowledgeService service, QuestStore quests, QuestExchange exchange, AmbientWorkspace ambient,
    AskDesk? asks = null, IntakeScope? intake = null, RuleProposalBox? proposals = null,
    SessionLedger? ledger = null)
{
    /// <summary>
    /// Which circle this call answers from: what the caller named, or the workspace of the repository
    /// this session is running in (D48 §4).
    /// </summary>
    /// <remarks>
    /// An agent asking "has anyone solved this" means its own family, not every family the machine can
    /// see — and it has no reason to know the wiring, because the wiring is a registry row and not
    /// anything in its tree. So the ambient answer is resolved here rather than asked for; naming one
    /// explicitly is how a person looks across.
    /// </remarks>
    private async Task<string?> ScopeAsync(string? named, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(named))
        {
            // "all" is the deliberate way out of the scope — spelled, so it can never be reached by
            // an empty string or a typo.
            return named.Equals("all", StringComparison.OrdinalIgnoreCase) ? null : named.Trim();
        }

        return await ambient.ResolveAsync(service, ct).ConfigureAwait(false);
    }

    private const string WorkspaceArgument =
        "Which workspace to answer from. Omit for the one this session's repository belongs to, which "
        + "is almost always right; `all` deliberately spans every workspace on this machine.";

    /// <summary>
    /// The scope that ran, said on every answer — including when it was every workspace.
    /// </summary>
    /// <remarks>
    /// An unscoped answer looks exactly like one family's answer, and a caller cannot tell the
    /// difference from the results. Naming it is the same discipline as reporting which recall tier
    /// answered (D24): the shape of the answer is part of the answer.
    /// </remarks>
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

    private static string Scoped(string? scope) =>
        scope is null ? " across every workspace on this machine" : $" in workspace `{scope}`";

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
        [Description(WorkspaceArgument)] string? workspace = null,
        CancellationToken ct = default)
    {
        if (UnknownKinds(kinds) is { } refused) return refused;
        var scope = await ScopeAsync(workspace, ct).ConfigureAwait(false);
        var answer = await service.AnswerAsync(
            new KnowledgeQuery(query)
            {
                Kinds = KnowledgeQuery.ParseKinds(kinds),
                Repositories = KnowledgeQuery.ParseSet(repositories),
                Provenance = localOnly ? Provenance.Local : null,
                Limit = Math.Clamp(limit, 1, 50),
                Workspace = scope,
            }, ct).ConfigureAwait(false);
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
            text.AppendLine($"### {hit.Entry.Title}");
            text.AppendLine($"`{hit.Entry.Repository}` · {hit.Entry.Kind} · `{hit.Entry.RelativePath}`");
            text.AppendLine($"id: `{hit.Entry.Id}`");
            if (hit.Excerpt is { Length: > 0 }) text.AppendLine($"\n> {hit.Excerpt}");
            text.AppendLine();
        }

        text.AppendLine("Call `knowledge_get` with an id for the full text.");
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
    public async Task<string> RepositoriesAsync(
        [Description(WorkspaceArgument)] string? workspace = null,
        CancellationToken ct = default)
    {
        var scope = await ScopeAsync(workspace, ct).ConfigureAwait(false);
        var summary = await service.SummarizeAsync(scope, ct).ConfigureAwait(false);
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
        var scope = await ScopeAsync(workspace, ct).ConfigureAwait(false);
        var candidates = await service.FindConvergenceAsync(
            new ConvergenceOptions(
                minimumSimilarity, KnowledgeQuery.ParseKinds(kinds), Math.Clamp(limit, 1, 50), scope), ct)
            .ConfigureAwait(false);

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
        var scope = await ScopeAsync(workspace, ct).ConfigureAwait(false);
        var registered = await service.RegistryAsync(scope, ct).ConfigureAwait(false);
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

    [McpServerTool(Name = "quest_publish")]
    [Description(
        "Ask ANOTHER repository in this family to do something. Repositories here are not developed "
        + "across: you never edit a sibling, you publish a quest and its own agent takes it. Say what "
        + "is needed and why, with the evidence — not the change you would make. Use before touching "
        + "any repository that is not the one you are working in, and before reading into one: what it "
        + "owns and why its code is shaped that way is its agent's to say. If your work depends on the "
        + "answer, wait on this quest with quest_respond (action wait) and end your turn.")]
    public async Task<string> PublishQuestAsync(
        [Description("The repository asking — the one you are working in. An intake publishes as its ask, whatever this says.")]
        string from,
        [Description("The repository being asked. It must be in the registry as addressable, or nobody there can see it.")]
        string to,
        [Description("One line: what is wanted.")] string title,
        [Description("Why, and the evidence. Whoever works there may see a better answer than you did.")]
        string body,
        [Description("Addresses the quest carries — a ticket, a page, a document. Absolute http or https only.")]
        string[]? links = null,
        [Description(
            "Files the quest carries, by their path on this machine — a screenshot, a log, a document. "
            + "Relative paths are the repository you are working in. They are kept on this machine and "
            + "handed to whoever takes the quest here; a remote learns only their names.")]
        string[]? attachments = null,
        [Description(
            "What to ask next, in order, once this quest is DONE — develop, then verify, then report. "
            + "Each step is published automatically when the one before it closes done, on behalf of "
            + "the same asker; a decline stops the chain. Write {parent} in a step's title or body for "
            + "the id of the quest it follows.")]
        ChainStep[]? then = null,
        CancellationToken ct = default)
    {
        // A path becomes bytes at the door, on the machine that has the file (D65 §2): the exchange
        // judges and keeps them, exactly as it does for the HTTP host's uploads.
        var uploads = new List<QuestUpload>();
        foreach (var path in attachments ?? [])
        {
            var (upload, refusal) = await QuestFiles.ReadAsync(path, Directory.GetCurrentDirectory(), ct)
                .ConfigureAwait(false);
            if (refusal is not null) return refusal + " Nothing was published.";
            uploads.Add(upload!);
        }

        var steps = (then ?? []).Select(step => new QuestStep(step.To ?? "", step.Title ?? "", step.Body ?? "")).ToList();

        // An intake publishes AS ITS ASK (D65 §1b): the room is no repository, so `from` could name
        // nothing addressable — the ask is the asker, in its own circle, carrying its own links and
        // files beside these. The desk judges through the same exchange, so nothing else differs.
        if (intake is { Active: true, Ask: { } askId } && asks is not null)
        {
            var answered = await asks.PublishAsync(
                    askId, to, DateTimeOffset.UtcNow, ct,
                    new AskDraft(title, body) { Links = links ?? [], Uploads = uploads, Then = steps },
                    intake.Session)
                .ConfigureAwait(false);
            return answered.Refusal == AskRefusal.None
                ? $"As ask `#{askId}`: {answered.Message}"
                : answered.Message;
        }

        // The judgement — who may be addressed, what a refusal says — lives in the exchange, shared
        // with the HTTP host so the same ask cannot be deliverable through one door and refused at
        // the other.
        var outcome = await exchange.PublishAsync(
                new QuestAsk(from, to, title, body)
                {
                    Links = links ?? [],
                    Uploads = uploads,
                    Then = steps,
                },
                DateTimeOffset.UtcNow, ct)
            .ConfigureAwait(false);
        return outcome.Message;
    }

    [McpServerTool(Name = "quest_list")]
    [Description(
        "Quests across the family: what has been asked of whom, and what is still outstanding. Give a "
        + "repository name to see only what it owes.")]
    public async Task<string> ListQuestsAsync(
        [Description("Only quests addressed to this repository. Omit for the whole family.")]
        string? repository = null,
        [Description("Include finished and declined ones. Default false — outstanding work is the question.")]
        bool includeClosed = false,
        [Description(WorkspaceArgument)] string? workspace = null,
        CancellationToken ct = default)
    {
        var scope = await ScopeAsync(workspace, ct).ConfigureAwait(false);
        var found = await quests.ListAsync(repository, includeClosed, scope, ct).ConfigureAwait(false);
        if (found.Count == 0)
        {
            return repository is null
                ? $"No open quests{Scoped(scope)}."
                : $"Nothing asked of `{repository}`{Scoped(scope)}.";
        }

        var text = new StringBuilder($"{found.Count} quest(s){Scoped(scope)}:\n\n");
        foreach (var group in found.GroupBy(q => q.To).OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            text.AppendLine($"## `{group.Key}` — {group.Count()}");
            foreach (var quest in group)
            {
                text.AppendLine($"- `#{quest.Id}` **{quest.Title}** — {quest.Status}, from `{quest.From}`");
                text.AppendLine($"  {Text.Excerpt(quest.Body, null, 200)}");
                if (quest.Links.Count > 0) text.AppendLine($"  links: {string.Join(" · ", quest.Links)}");
                if (quest.Attachments.Count > 0)
                {
                    // Names only: whoever takes the quest on the machine that keeps them is handed them.
                    text.AppendLine($"  files: {string.Join(" · ", quest.Attachments.Select(a => a.Name))}");
                }

                // The chain around it (D65 §4): what this one follows, and what its close publishes.
                if (quest.Parent is { } parent) text.AppendLine($"  follows `#{parent}`");
                // Ask and wait (D79): what its taker waits on.
                if (quest.Awaits is { } awaits) text.AppendLine($"  waits on `#{awaits}`");
                foreach (var step in quest.Then) text.AppendLine($"  then → `{step.To}`: {step.Title}");

                if (quest.Note is { Length: > 0 }) text.AppendLine($"  _{quest.Note}_");
            }

            text.AppendLine();
        }

        return text.ToString();
    }

    [McpServerTool(Name = "quest_respond")]
    [Description(
        "Answer a quest addressed to the repository you are working in: take it, finish it, or decline "
        + "it. Declining is a real answer and often the right one — it needs a reason, because a bare "
        + "refusal gives the asker nothing to act on. When your work needs something another repository "
        + "owns — a change there, or a fact about its code — publish the question to it with "
        + "quest_publish, then WAIT on that question here (action wait, on the question's id) and end "
        + "your turn: your quest stays yours, and the driver resumes you in the same tree with the answer.")]
    public async Task<string> RespondToQuestAsync(
        [Description("The quest id, from quest_list.")] string id,
        [Description("take, done, decline, or wait.")] string action,
        [Description("Required to decline; worth giving when finishing.")] string? reason = null,
        [Description("For wait: the id of the question you published to another repository.")] string? on = null,
        CancellationToken ct = default)
    {
        var outcome = await exchange.RespondAsync(id, action, reason, DateTimeOffset.UtcNow, ct, on: on)
            .ConfigureAwait(false);

        // A take by a session the driver started is written on its record (STANDDOWN2): how its end is
        // told apart from a stand-down, which the quest's state alone cannot say.
        if (outcome.Refusal == QuestRespondRefusal.None
            && string.Equals(action, "take", StringComparison.OrdinalIgnoreCase)
            && intake?.Session is { } session && ledger is not null)
        {
            await ledger.MarkTookAsync(session, id, ct).ConfigureAwait(false);
        }

        return outcome.Message;
    }

    [McpServerTool(Name = "permission_propose")]
    [Description(
        "Propose a change to what the agents Daoris starts may do on this machine — its permission rules, "
        + "in Claude Code's own shape. Use it when you were refused something you needed, or when a rule "
        + "lets agents do more than they should. A change that NARROWS (a new deny or ask, an allow "
        + "removed, an allowing default switched off) is applied at the driver's next tick; one that "
        + "WIDENS waits for the person's yes. Either way it records which session proposed it, and why. "
        + "Not for reaching into another repository — to read its code or change it, ask it with "
        + "quest_publish and wait on the answer.")]
    public string ProposePermission(
        [Description("add (a rule to a list), remove (a rule from a scope), or default (switch one of Daoris's defaults).")]
        string action,
        [Description("Where it reaches: machine (every session here), workspace (every session in one workspace), or repository (one repository). Prefer the narrowest that is enough.")]
        string scope,
        [Description("Why: what you were doing, and what the change would allow or stop. The person decides on this.")]
        string why,
        [Description("For add: allow, ask or deny. An ask is refused for anything Daoris starts — nobody is there to answer it.")]
        string? list = null,
        [Description("For add and remove: the rule, like `Bash(npm run test:*)`, `Edit(/docs/**)` or `mcp__daoris-knowledge__quest_list`.")]
        string? rule = null,
        [Description("For workspace and repository: which one.")]
        string? name = null,
        [Description("For default: the id it ships under — connector, commit, no-push or tree-guard.")]
        string? id = null,
        [Description("For default: true to switch it on, false to switch it off.")]
        bool? on = null)
    {
        var box = proposals ?? RuleProposalBox.FromEnvironment();
        var (_, message) = box.Propose(
            new RuleChange(action.Trim().ToLowerInvariant(), scope.Trim().ToLowerInvariant(), name, list?.Trim().ToLowerInvariant(), rule, id, on),
            why, intake?.Session, intake?.Ask, Directory.GetCurrentDirectory(), DateTimeOffset.UtcNow);
        return message;
    }

    [McpServerTool(Name = "knowledge_refresh")]
    [Description("Re-read every repository from disk and rebuild the index. Use after doctrine or decisions have changed; it takes about a second.")]
    public async Task<string> RefreshAsync(CancellationToken ct = default)
    {
        var report = await service.RefreshAsync(ct).ConfigureAwait(false);
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
        return $"Indexed {report.Entries} entries from {report.Repositories} repositories{withheld}.{absent}\n{recall}";
    }

}

/// <summary>
/// One step of a chain as an agent writes it (D65 §4) — the door's own shape, so each field can
/// describe itself to the model that fills it. Nullable because an agent may leave one out, and the
/// exchange refuses a step without its words naming which step it was.
/// </summary>
public sealed record ChainStep(
    [property: Description("The repository asked at this step. Every step is asked on behalf of the chain's asker.")]
    string? To,
    [property: Description("One line: what is wanted. {parent} becomes the id of the quest this step follows.")]
    string? Title,
    [property: Description("Why, and how to tell it is done — e.g. where to look in the browser. {parent} works here too.")]
    string? Body);
