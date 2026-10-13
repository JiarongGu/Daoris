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
}

/// <summary>The runs' reads (WORKFLOW1c): nothing here writes anywhere. Every request to the service is a <c>GET</c>.</summary>
public static class WorkflowRunReader
{
    /// <summary>
    /// The runs of what <paramref name="asked"/> names (design §3.8, §5.1): a quest's chain in its repository; a session's, through
    /// its quest, or through its ask where it is an intake; and an ask's, every chain it asked, one run per repository each reaches.
    /// A session that serves no quest and no ask has none, and says so.
    /// </summary>
    public static async Task<WorkflowRunRead> ReadAsync(WorkflowRunAsk asked, WorkflowRunSources sources, CancellationToken ct = default)
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
            return new WorkflowRunRead([], $"the service did not answer, so no run could be read: {error.Message.TrimEnd().TrimEnd('.')}.")
            {
                Unanswered = true,
            };
        }

        var (chains, problem) = ChainsOf(asked, quests, sessions);
        if (problem is not null) return new WorkflowRunRead([], problem);

        var reads = new Reads(sources, quests, ct);
        var runs = new List<WorkflowRun>();
        foreach (var chain in chains)
        {
            foreach (var repository in chain.Select(quest => quest.To).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                runs.Add(await RunAsync(chain, repository, sessions, reads).ConfigureAwait(false));
            }
        }

        return new WorkflowRunRead(runs);
    }

    /// <summary>The chains whose runs are asked for, each whole across its repositories, or the sentence that says why there are none.</summary>
    internal static (IReadOnlyList<IReadOnlyList<QuestView>> Chains, string? Problem) ChainsOf(
        WorkflowRunAsk asked, IReadOnlyList<QuestView> quests, IReadOnlyList<TracedSession> sessions)
    {
        if (asked.Session is { } sessionId)
        {
            var session = sessions.FirstOrDefault(each => Same(each.Id, sessionId.Trim()));
            if (session is null) return ([], $"no session `{sessionId.Trim()}` is recorded here.");
            if (session.Quest is { } quest) return ChainsOf(new WorkflowRunAsk(Quest: quest), quests, sessions);
            if (session.Ask is { } ask) return ChainsOf(new WorkflowRunAsk(Ask: ask), quests, sessions);
            return ([], $"session `{session.Id}` serves no quest, so no workflow runs for it.");
        }

        if (asked.Quest is { } questId)
        {
            var id = questId.Trim().TrimStart('#');
            var quest = quests.FirstOrDefault(each => Same(each.Id, id));
            if (quest is null) return ([], $"no quest `#{id}` is here.");
            var chain = ReviewGate.ChainOf(quests, quest.Id);
            // A quest's run is its chain's work in its own repository, the chain's other repositories being their own runs.
            return ([[.. chain.Where(each => Same(each.To, quest.To))]], null);
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

            return (chains, null);
        }

        return ([], "a run is read for a `session`, a `quest` or an `ask` — name one of them.");
    }

    /// <summary>One run: the chain's work in <paramref name="repository"/>, from every store that keeps a part of it.</summary>
    private static async Task<WorkflowRun> RunAsync(
        IReadOnlyList<QuestView> chain, string repository, IReadOnlyList<TracedSession> sessions, Reads reads)
    {
        var sources = reads.Sources;
        var part = chain.Where(quest => Same(quest.To, repository)).ToList();
        var ids = new HashSet<string>(part.Select(quest => quest.Id), StringComparer.OrdinalIgnoreCase);
        var mine = sessions.Where(session => session.Quest is { } quest && ids.Contains(quest)).ToList();
        var workspace = RemoteTarget.Workspace(sources.WorkspaceOf(repository));
        var current = WorkflowCurrent.Derive(sources.Config, repository, sources.WorkspaceOf(repository), sources.Plugins);
        var process = WorkflowProcesses.Read(sources.Home, sources.Config, repository, sources.WorkspaceOf(repository),
            WorkflowRunBindings.RunOf(chain, repository));
        current = WorkflowRunGraph.Read(process, current, sources.Plugins);
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
            .LastOrDefault(session => Directory.Exists(session.Tree));
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
            Sessions = mine,
            GoAheads = ask?.GoAheads ?? [],
            Review = review,
            Opinion = opinion,
            Process = process,
            WorkflowGate = workflow,
            Landings = [.. reads.Landings.Where(entry => sessionIds.Any(entry.Names))],
            Accepted = Acceptances(mine, sources.Home),
            AutoLandings = [.. reads.AutoLandings.Where(entry => sessionIds.Contains(entry.Session))],
        };
        return WorkflowRuns.Derive(facts, workspace, askId);
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
        var tip = decision.Reviews && tree is not null ? await ReviewGate.HeadAsync(tree, reads.Ct).ConfigureAwait(false) : null;
        return await ReviewGate.JudgeAsync(decision, tip, commit => tree is null || tip is null
            ? Task.FromResult<bool?>(null)
            : ReviewGate.HoldsAsync(tree, tip, commit, reads.Ct)).ConfigureAwait(false);
    }

    /// <summary>
    /// When each session's work was accepted into its line (D100): the newest acceptance its conversation's record keeps, read
    /// from the writer's own words (<see cref="LandingRules.IsAcceptance"/>). A teammate's record is on its machine.
    /// </summary>
    private static IReadOnlyDictionary<string, DateTimeOffset> Acceptances(IReadOnlyList<TracedSession> sessions, string home)
    {
        var accepted = new Dictionary<string, DateTimeOffset>(StringComparer.OrdinalIgnoreCase);
        var events = new SessionEvents(Path.Combine(home, "sessions"));
        foreach (var session in sessions.Where(session => !session.Teammate && SessionEvents.IsId(session.Id)))
        {
            try
            {
                if (!File.Exists(events.PathOf(session.Id))) continue;
                var notes = events.After(session.Id, 0).Events
                    .Where(note => note.Kind == SessionEventKind.Note && note.Text is { } text && LandingRules.IsAcceptance(text))
                    .ToList();
                if (notes.Count > 0) accepted[session.Id] = notes[^1].At;
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or DriverException)
            {
                // A record that does not read keeps no acceptance: the landing then reads as not yet made, never as made.
            }
        }

        return accepted;
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
    private sealed class Reads(WorkflowRunSources sources, IReadOnlyList<QuestView> quests, CancellationToken ct) : IOpinionWorld
    {
        private readonly Dictionary<string, (AskView?, string?)> _asks = new(StringComparer.OrdinalIgnoreCase);

        public WorkflowRunSources Sources => sources;

        public CancellationToken Ct => ct;

        /// <summary>The opinion's gate's world (<see cref="IOpinionWorld"/>): the quests this read holds, an ask, an opinion by id.</summary>
        public IOpinionWorld World => this;

        public Task<IReadOnlyList<QuestView>> QuestsAsync(CancellationToken cancel) => Task.FromResult(quests);

        public Task<AskView?> AskAsync(string id, CancellationToken cancel) => sources.Service.FindAskAsync(id, cancel);

        public Task<OpinionView?> OpinionAsync(string id, CancellationToken cancel) => sources.Service.ReadOpinionAsync(id, cancel);

        public IReadOnlyList<LandedBranch> Landings { get; } = new LandedBranches(sources.Home).Entries();

        public IReadOnlyList<AutoLanding> AutoLandings { get; } = new AutoLandings(sources.Home).All();

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
