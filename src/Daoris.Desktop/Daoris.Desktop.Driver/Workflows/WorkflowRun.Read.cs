using System.Text.Json;

namespace Daoris.Driver;

/// <summary>Whose runs are read (WORKFLOW1c): a session's, a quest's, or an ask's. Exactly one is named.</summary>
public sealed record WorkflowRunAsk(string? Session = null, string? Quest = null, string? Ask = null);

/// <summary>
/// Where a run's records are read (WORKFLOW1c; design §5.1): this machine's service, its home (the landing record, the due list and
/// each session's conversation and bound workflow), its live declarations and plugins, and the registry's workspace for a repository.
/// </summary>
/// <param name="WorkspaceOf">The workspace the registry holds a repository in, or null where it holds none (read as in no workspace).</param>
public sealed record WorkflowRunSources(
    ServiceClient Service, string Home, DriverConfig Config, IReadOnlyList<WorkflowPlugin> Plugins, Func<string, string?> WorkspaceOf);

/// <summary>The runs read, in the chain's order, or the sentence that says why none could be.</summary>
public sealed record WorkflowRunRead(IReadOnlyList<WorkflowRun> Runs, string? Problem = null)
{
    /// <summary>
    /// The service did not answer, so nothing could be read: a tool error, where every other <see cref="Problem"/> is an answer
    /// that names no run (WORKFLOW1c2: the terminal's exit 2, not 1).
    /// </summary>
    public bool Unanswered { get; init; }

    /// <summary>
    /// Why no run is read, as a code the page words in en and zh (<see cref="WorkflowNoRun"/>, CHATFLOW1a), where <see cref="Problem"/>
    /// is its English sentence; null for a problem that has none.
    /// </summary>
    public string? Code { get; init; }

    /// <summary>The record's id or the workflow's id the code's words name, beside <see cref="Code"/>; null where they name none.</summary>
    public string? Id { get; init; }

    /// <summary>The workflow's id <see cref="WorkflowNoRun.ChatGraphLater"/>'s words name; null otherwise.</summary>
    public string? Workflow { get; init; }

    /// <summary>What the service's failure said, beside <see cref="WorkflowNoRun.Unanswered"/>'s sentence, for the terminal.</summary>
    public string? Why { get; init; }
}

/// <summary>
/// The runs' reads (WORKFLOW1c): nothing here writes anywhere. Every request to the service is a <c>GET</c>. A run's records beside
/// the service are read through <see cref="IWorkflowRunRecords"/>, its port (STRUCT3b, D166).
/// </summary>
public static class WorkflowRunReader
{
    /// <summary>
    /// The runs of what <paramref name="asked"/> names (design §3.8, §5.1): a quest's chain in its repository; a session's, through
    /// its quest, or through its ask where it is an intake; and an ask's, every chain it asked, one run per repository each reaches.
    /// A session that serves no quest and no ask has none, and says so.
    /// </summary>
    public static Task<WorkflowRunRead> ReadAsync(WorkflowRunAsk asked, WorkflowRunSources sources, CancellationToken ct = default) =>
        ReadAsync(asked, sources, WorkflowPorts.Records(sources.Home), ct);

    /// <summary>The runs of what <paramref name="asked"/> names, a run's records beside the service read through <paramref name="records"/>.</summary>
    public static async Task<WorkflowRunRead> ReadAsync(
        WorkflowRunAsk asked, WorkflowRunSources sources, IWorkflowRunRecords records, CancellationToken ct = default)
    {
        IReadOnlyList<QuestView> quests;
        IReadOnlyList<TracedSession> sessions;
        try
        {
            quests = await sources.Service.EveryQuestAsync(ct).ConfigureAwait(false);
            sessions = Trace.ParseSessions(await sources.Service.SessionRecordsJsonAsync(ct).ConfigureAwait(false));
        }
        catch (Exception error) when (Unanswered(error, ct))
        {
            return new WorkflowRunRead([], WorkflowNoRun.Said(WorkflowNoRun.Unanswered))
            {
                Unanswered = true, Code = WorkflowNoRun.Unanswered, Why = error.Message.TrimEnd().TrimEnd('.'),
            };
        }

        var (chains, problem, only, code, id) = ChainsOf(asked, quests, sessions, records);
        if (problem is not null) return new WorkflowRunRead([], problem) { Code = code, Id = id };

        var reads = new Reads(sources, quests, records, ct);
        var runs = new List<WorkflowRun>();
        foreach (var chain in chains)
        {
            // A quest's run (or its session's) is its own repository's, read over the whole chain; an ask's is one run per repository.
            var repositories = only is null ? chain.Select(quest => quest.To).Distinct(StringComparer.OrdinalIgnoreCase) : [only];
            foreach (var repository in repositories)
            {
                runs.Add(await RunAsync(chain, repository, sessions, reads).ConfigureAwait(false));
            }
        }

        return new WorkflowRunRead(runs);
    }

    /// <summary>
    /// The chains whose runs are asked for, each whole across its repositories, or the sentence that says why there are none with its
    /// code (<see cref="WorkflowNoRun"/>) and the id its words name. A session is classified in the chat-flow design §4.2's order
    /// (CHATFLOW1a, corrected by Codex's finding 6): its quest's run; the concrete work its line continues (<see cref="WorkOf"/>), that
    /// quest's; an intake, its ask's; a line of an ask whose quest no record names, the ask's; and each kind that runs nothing, by its code.
    /// </summary>
    internal static (IReadOnlyList<IReadOnlyList<QuestView>> Chains, string? Problem, string? Only, string? Code, string? Id) ChainsOf(
        WorkflowRunAsk asked, IReadOnlyList<QuestView> quests, IReadOnlyList<TracedSession> sessions, IWorkflowRunRecords trees)
    {
        static (IReadOnlyList<IReadOnlyList<QuestView>>, string?, string?, string?, string?) None(string code, string? id = null) =>
            ([], WorkflowNoRun.Said(code, id), null, code, id);

        if (asked.Session is { } sessionId)
        {
            var session = sessions.FirstOrDefault(each => Same(each.Id, sessionId.Trim()));
            if (session is null) return None(WorkflowNoRun.NoSession, sessionId.Trim());
            if (session.Teammate) return None(WorkflowNoRun.Teammate);
            if (session.Quest is { } quest) return ChainsOf(new WorkflowRunAsk(Quest: quest), quests, sessions, trees);
            // The concrete work the line continues comes before its origin (§4.1, Codex's finding 6): a quest's worker gone on as a
            // conversation, a chat that took a quest and a line whose stored origin is a quest's each read that quest's run, never a
            // chat's, and never every run of the ask whose origin the line inherited. Only an intake, or a line of an ask whose quest no
            // record here names, reads the ask's runs.
            if (WorkOf(session, sessions) is { } work) return ChainsOf(new WorkflowRunAsk(Quest: work), quests, sessions, trees);
            if (session.Ask is { } ask) return ChainsOf(new WorkflowRunAsk(Ask: ask), quests, sessions, trees);
            if (OriginNamed(session, "ask") is { } asking) return ChainsOf(new WorkflowRunAsk(Ask: asking), quests, sessions, trees);
            if (Same(session.Repository, HelpRoom.Repository)) return None(WorkflowNoRun.AskDaoris);
            if (session.Opinion is not null) return None(WorkflowNoRun.Reviewer);
            if (session.Piece is not null) return None(WorkflowNoRun.Partner);
            if (session.Serves is not null) return None(WorkflowNoRun.Helper);
            if (session.Tree is { Length: > 0 } tree && !trees.Stands(tree)) return None(WorkflowNoRun.ChatTreeGone);
            if (session.Tree is not { Length: > 0 } own || !trees.Holds(own)) return None(WorkflowNoRun.ChatCheckout);
            return None(WorkflowNoRun.ChatOwnTree);
        }

        if (asked.Quest is { } questId)
        {
            var id = questId.Trim().TrimStart('#');
            var quest = quests.FirstOrDefault(each => Same(each.Id, id));
            if (quest is null) return None(WorkflowNoRun.NoQuest, id);
            var chain = ReviewGate.ChainOf(quests, quest.Id);
            // A quest's run is its own repository's, over the whole chain: a step or source in another repository seals, submits and
            // reopens it as a same-repository one does (FABRIC-WORKFLOW2e), and the chain's other repositories are their own runs.
            return ([chain], null, quest.To, null, null);
        }

        if (asked.Ask is { } askId)
        {
            var sender = AskWork.SenderOf(askId.Trim().TrimStart('#'));
            // Each chain the ask asked, by its first quest; a chain's later step names the ask as its sender too (D65 §4).
            var asks = quests.Where(each => Same(each.From, sender)).ToList();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var chains = new List<IReadOnlyList<QuestView>>();
            foreach (var quest in asks)
            {
                if (seen.Contains(quest.Id)) continue;
                var chain = ReviewGate.ChainOf(quests, quest.Id);
                foreach (var each in chain) seen.Add(each.Id);
                chains.Add(chain);
            }

            return chains.Count == 0 ? None(WorkflowNoRun.AskNoQuest, askId.Trim().TrimStart('#')) : (chains, null, null, null, null);
        }

        return ([], "a run is read for a `session`, a `quest` or an `ask` — name one of them.", null, null, null);
    }

    /// <summary>
    /// The quest whose workflow governs a record's work (the chat-flow design §4.1, Codex's finding 6), or null: its own quest; else the
    /// nearest record its <c>From</c> links reach that names a quest or took one, so a worker gone on as a conversation stays that quest's
    /// work whatever origin its line inherited (an ask's, or a publisher's in another tree); else a quest a chat of its line in its tree
    /// took (CHATTAKE1); else a stored origin <c>quest:&lt;id&gt;</c>. An intake, a reviewer, a helper and a teammate's record continue
    /// no work here, so they read none. The walk follows the records read, guarded against a cycle.
    /// </summary>
    internal static string? WorkOf(TracedSession session, IReadOnlyList<TracedSession> sessions)
    {
        if (session.Quest is { } own) return own;
        if (session.Teammate || session.Ask is not null || session.Opinion is not null || session.Serves is not null) return null;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var at = session; at is not null && seen.Add(at.Id); at = at.From is { } from ? sessions.FirstOrDefault(each => Same(each.Id, from)) : null)
        {
            if ((at.Quest ?? at.TookQuest) is { } work) return work;
        }

        return TookQuest(session, sessions) ?? OriginNamed(session, "quest");
    }

    /// <summary>The id a session's stored origin names, where that origin is of <paramref name="kind"/> (<c>quest</c> or <c>ask</c>).</summary>
    private static string? OriginNamed(TracedSession session, string kind) =>
        session.Origin is { } origin && origin.StartsWith(kind + ":", StringComparison.Ordinal) && origin[(kind.Length + 1)..] is { Length: > 0 } id
            ? id : null;

    /// <summary>
    /// The quest a chat took (CHATTAKE1), read from any record of its line in its tree, the newest first: <c>took_quest</c> is the taking
    /// record's alone, so a hand-over's next record reads the taken quest's run as the record before it did (the chat-flow design §4.2).
    /// </summary>
    private static string? TookQuest(TracedSession session, IReadOnlyList<TracedSession> sessions)
    {
        if (session.TookQuest is { } own) return own;
        if (session.Entry is not { Length: > 0 } entry || session.Tree is not { Length: > 0 } tree) return null;
        return sessions
            .Where(each => each.TookQuest is not null && !each.Teammate && Same(each.Entry, entry) && SameTree(each.Tree, tree))
            .OrderByDescending(each => each.Created ?? DateTimeOffset.MinValue)
            .Select(each => each.TookQuest)
            .FirstOrDefault();
    }

    private static bool SameTree(string? left, string right) =>
        left is { Length: > 0 } && string.Equals(Path.TrimEndingDirectorySeparator(left), Path.TrimEndingDirectorySeparator(right), StringComparison.OrdinalIgnoreCase);

    /// <summary>One run: the chain's work in <paramref name="repository"/>, from every store that keeps a part of it.</summary>
    private static async Task<WorkflowRun> RunAsync(
        IReadOnlyList<QuestView> chain, string repository, IReadOnlyList<TracedSession> sessions, Reads reads)
    {
        var sources = reads.Sources;
        var part = chain.Where(quest => Same(quest.To, repository)).ToList();
        var ids = new HashSet<string>(part.Select(quest => quest.Id), StringComparer.OrdinalIgnoreCase);
        // The work's records are every record whose concrete work is a quest of this part (Codex's finding 6): its quests' own sessions,
        // and the conversations that continue them or took one, each read here as that quest's though its own record keeps no quest.
        var mine = sessions
            .Select(session => session.Quest is { } quest ? (ids.Contains(quest) ? session : null)
                : WorkOf(session, sessions) is { } work && ids.Contains(work) ? session with { Quest = work } : null)
            .OfType<TracedSession>()
            .ToList();
        var workspace = RemoteTarget.Workspace(sources.WorkspaceOf(repository));
        var current = WorkflowCurrent.Derive(sources.Config, repository, sources.WorkspaceOf(repository), sources.Plugins);
        var process = WorkflowProcesses.Read(sources.Home, sources.Config, repository, sources.WorkspaceOf(repository),
            WorkflowRunBindings.RunOf(chain, repository));
        current = WorkflowRunGraph.Read(process, current, sources.Plugins);

        // FABRIC-CUTOVER1e (D190 points 2 and 3): a run bound to a graph version is the runtime's, so it is drawn from its record, and its gates
        // read the version from its nodes (CUTOVER1d).
        if (process is { Graph: { } graph, Run: { } run }) return await GraphRunAsync(chain, repository, part, mine, workspace, current, process, graph, run, reads).ConfigureAwait(false);

        if (process.Problem is not null || process.Unread is not null)
            return new WorkflowRun(repository, workspace, current, [.. part.Select(quest => quest.Id)], [])
            {
                Process = process, Problem = process.Problem ?? process.Unread,
                Session = mine.MaxBy(session => session.Created ?? DateTimeOffset.MinValue)?.Id,
            };

        var askId = chain.Select(quest => AskWords.AskOf(quest.From)).FirstOrDefault(id => id is not null);
        var (ask, askUnread) = askId is null ? (null, null) : await reads.AskAsync(askId).ConfigureAwait(false);
        var sessionIds = new HashSet<string>(mine.Select(session => session.Id), StringComparer.OrdinalIgnoreCase);

        // The work's newest tree here that still stands: where both gates read the commit that would land, as a landing door does.
        var work = new HashSet<string>(part.Where(quest => quest.SetUpIn is null).Select(quest => quest.Id), StringComparer.OrdinalIgnoreCase);
        var standing = mine
            .Where(session => session.Quest is { } quest && work.Contains(quest) && !session.Teammate && session.Tree is { Length: > 0 })
            .OrderBy(session => session.Created ?? DateTimeOffset.MinValue)
            .LastOrDefault(session => reads.Records.Stands(session.Tree!));
        var review = await ReviewAsync(chain, repository, ask, askUnread, standing?.Tree, reads, process).ConfigureAwait(false);
        var trees = new SessionTrees(sources.Home);
        var workflow = process.Named && standing is not null
            ? await trees.WorkflowAsync(standing.Tree!, process, standing.Id, reads.Ct).ConfigureAwait(false) : null;

        // The second opinion's gate as every landing door reads it, through this same process. With no tree here it cannot be read.
        OpinionGateState? opinion = null;
        if (standing is not null && current.Steps.Any(step => step.Kind == WorkflowKinds.Opinion))
        {
            opinion = await trees.OpinionAsync(
                standing.Tree!, repository, workspace, standing.Quest, standing.Id, reads.World, review, OpinionRules.Landing, reads.Ct, process).ConfigureAwait(false);
        }
        else if (process.OpinionCannot is { } cannot)
        {
            opinion = new OpinionGateState(OpinionGateStates.CannotStart, repository) { Rule = process.Opinion, Problem = cannot };
        }

        var facts = new WorkflowRunFacts(repository, current)
        {
            Quests = part,
            // FABRIC-WORKFLOW2d: the whole chain, where a submission's next step another repository holds is read to seal it (§E5).
            Chain = chain,
            Sessions = mine,
            GoAheads = ask?.GoAheads ?? [],
            Review = review,
            Opinion = opinion,
            Process = process,
            WorkflowGate = workflow,
            Landings = [.. reads.Landings.Where(entry => sessionIds.Any(entry.Names))],
            Accepted = reads.Records.Acceptances(mine),
            AutoLandings = [.. reads.AutoLandings.Where(entry => sessionIds.Contains(entry.Session))],
        };
        return WorkflowRuns.Derive(facts, workspace, askId);
    }

    /// <summary>
    /// A run bound to a graph version (FABRIC-CUTOVER1e; D190 points 2, 3 and 7, the cutover design §2, §6): its graph's nodes drawn from its
    /// record, <c>run:&lt;run&gt;</c>, read and never written, each on the subject its binding names; and the review's gate read for a look the
    /// work's own choice adds, whose rows the graph replaces with none of the rule's (D154's rows 5 and 6 are its graph's).
    /// </summary>
    private static async Task<WorkflowRun> GraphRunAsync(
        IReadOnlyList<QuestView> chain, string repository, IReadOnlyList<QuestView> part, IReadOnlyList<TracedSession> mine, string workspace,
        CurrentWorkflow current, WorkflowProcess process, NodeGraph graph, string run, Reads reads)
    {
        var askId = chain.Select(quest => AskWords.AskOf(quest.From)).FirstOrDefault(id => id is not null);
        var (ask, askUnread) = askId is null ? (null, null) : await reads.AskAsync(askId).ConfigureAwait(false);
        var sessionIds = new HashSet<string>(mine.Select(session => session.Id), StringComparer.OrdinalIgnoreCase);
        var work = new HashSet<string>(part.Where(quest => quest.SetUpIn is null).Select(quest => quest.Id), StringComparer.OrdinalIgnoreCase);
        var standing = mine
            .Where(session => session.Quest is { } quest && work.Contains(quest) && !session.Teammate && session.Tree is { Length: > 0 })
            .OrderBy(session => session.Created ?? DateTimeOffset.MinValue)
            .LastOrDefault(session => reads.Records.Stands(session.Tree!));
        var review = await ReviewAsync(chain, repository, ask, askUnread, standing?.Tree, reads, process with { Review = null, Look = null }).ConfigureAwait(false);
        var facts = new WorkflowRunFacts(repository, current)
        {
            Quests = part,
            Chain = chain,
            Sessions = mine,
            GoAheads = ask?.GoAheads ?? [],
            Review = review,
            Process = process,
            Landings = [.. reads.Landings.Where(entry => sessionIds.Any(entry.Names))],
            AutoLandings = [.. reads.AutoLandings.Where(entry => sessionIds.Contains(entry.Session))],
        };
        var record = reads.Records.Record(RunUnits.IdOf(run));
        return WorkflowGraphRuns.Derive(new WorkflowGraphRunFacts(facts, run, graph, record) { Branches = reads.Branches }, workspace, askId);
    }

    /// <summary>
    /// The review's gate for the chain's work here, as every landing door reads it (<see cref="ReviewGate"/>): the level's table,
    /// then the work's tip in its newest tree here and git's ancestry. An ask that did not answer holds the gate unread where a
    /// rule stands that could ask for a review, as <see cref="ReviewGate.ReadAsync"/> holds it.
    /// </summary>
    /// <param name="tree">The work's newest tree here that still stands, whose tip would land; null where none does.</param>
    private static async Task<ReviewGateState> ReviewAsync(
        IReadOnlyList<QuestView> chain, string repository, AskView? ask, string? askUnread, string? tree, Reads reads, WorkflowProcess process)
    {
        var rule = process.Review;
        if (askUnread is not null)
        {
            var none = new ReviewDecision(ReviewLevels.Nothing, null) { Repository = repository };
            return rule is { Rule.IsNone: false } || process.Look is { Environment: not null } or { Cannot: not null }
                ? new(ReviewStates.Unread, none) { Problem = askUnread } : new(ReviewStates.None, none);
        }

        var decision = ReviewGate.Decide(chain, repository, ask, rule, process.Look, process.Name);
        // Git is asked only where a review is asked.
        var tip = decision.Reviews && tree is not null ? await ReviewGit.HeadAsync(tree, reads.Ct).ConfigureAwait(false) : null;
        return await ReviewGate.JudgeAsync(decision, tip, commit => tree is null || tip is null
            ? Task.FromResult<bool?>(null)
            : ReviewGit.HoldsAsync(tree, tip, commit, reads.Ct)).ConfigureAwait(false);
    }

    /// <summary>A read the service did not answer: unreachable, refused, unparsable, or a client's own time-out, never the person's cancel.</summary>
    private static bool Unanswered(Exception error, CancellationToken ct) =>
        error is DriverException or HttpRequestException or JsonException or InvalidOperationException
        || (error is OperationCanceledException && !ct.IsCancellationRequested);

    private static bool Same(string? left, string? right) => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// What one read of several runs reads once: each ask, the landing record and the due list; and the world the opinion's gate
    /// reads, over the quests already read.
    /// </summary>
    private sealed class Reads(WorkflowRunSources sources, IReadOnlyList<QuestView> quests, IWorkflowRunRecords records, CancellationToken ct)
        : IOpinionWorld
    {
        private readonly Dictionary<string, (AskView?, string?)> _asks = new(StringComparer.OrdinalIgnoreCase);

        public WorkflowRunSources Sources => sources;

        /// <summary>What the read reads beside the service: the acceptances, and whether a tree stands.</summary>
        public IWorkflowRunRecords Records => records;

        public CancellationToken Ct => ct;

        /// <summary>The opinion's gate's world (<see cref="IOpinionWorld"/>): the quests this read holds, an ask, an opinion by id.</summary>
        public IOpinionWorld World => this;

        public Task<IReadOnlyList<QuestView>> QuestsAsync(CancellationToken cancel) => Task.FromResult(quests);

        public Task<AskView?> AskAsync(string id, CancellationToken cancel) => sources.Service.FindAskAsync(id, cancel);

        public Task<OpinionView?> OpinionAsync(string id, CancellationToken cancel) => sources.Service.Opinions.ReadAsync(id, cancel);

        public IReadOnlyList<LandedBranch> Landings { get; } = records.Landings();

        public IReadOnlyList<AutoLanding> AutoLandings { get; } = records.AutoLandings();

        private IReadOnlyList<GrownBranch>? _branches;

        /// <summary>This machine's session branches, read once a read and only where a graph run names a branch by them (FABRIC-CUTOVER1e).</summary>
        public IReadOnlyList<GrownBranch> Branches => _branches ??= records.Branches();

        public async Task<(AskView? Ask, string? Unread)> AskAsync(string id)
        {
            if (_asks.TryGetValue(id, out var kept)) return kept;
            try
            {
                kept = (await sources.Service.FindAskAsync(id, ct).ConfigureAwait(false), null);
            }
            catch (Exception error) when (Unanswered(error, ct))
            {
                kept = (null, error.Message.TrimEnd().TrimEnd('.'));
            }

            return _asks[id] = kept;
        }
    }
}
