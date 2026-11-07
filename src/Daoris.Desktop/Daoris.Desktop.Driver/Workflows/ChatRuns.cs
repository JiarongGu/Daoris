namespace Daoris.Driver;

/// <summary>What a chat run's bind came to: bound, already bound, or failed.</summary>
/// <param name="Binding">The binding kept or already there; null where nothing was bound.</param>
/// <param name="Failed">Why the home could not keep the binding; null otherwise.</param>
public sealed record ChatRunBound(WorkflowRunBinding? Binding, string? Failed = null)
{
    /// <summary>Whether this bind wrote the binding: false where it was there already, or nothing was bound.</summary>
    public bool Wrote { get; init; }
}

/// <summary>
/// A chat's run: a conversation's work in a tree of its own runs its repository's workflow,
/// keyed by the line the record stores, <c>chat-&lt;first&gt;</c>, and bound when its tree opens.
/// </summary>
/// <remarks>
/// <para><b>The key</b> is the line's first record in that tree: the record its stored origin <c>session:&lt;first&gt;</c> names where that
/// record names the same tree, else the line's earliest record there, else the record's own. A hand-over and a limit's go-on open a new
/// record on the same tree in the same line, so they read the same run. A teammate's id holds <c>/</c> and is never a run's.</para>
///
/// <para><b>A graph version binds</b>: the run's subject is its candidate, submitted at the person's press
/// (<see cref="ChatCandidates"/>), and its <c>land</c> is the runtime's. A named workflow that cannot be read, or whose graph holds a
/// <c>quest.close</c>, is bound with its <c>Problem</c>, which holds the press by it; only a home that cannot be written fails the bind.</para>
///
/// <para><b>Its files</b> go with the session delete of the record its key names (<see cref="Files"/>), and the leftover rule takes one
/// whose record is gone.</para>
/// </remarks>
public static class ChatRuns
{
    /// <summary>What a chat run's id begins with, apart from a quest's 12 hex digits by its spelling.</summary>
    public const string Prefix = WorkplaceRunFiles.ChatPrefix;

    /// <summary>Whether a run's id is a chat run's.</summary>
    public static bool IsChatRun(string? run) => WorkplaceRunFiles.IsChatRun(run);

    /// <summary>The record a chat run's id names, its line's first in its tree; null for a run that is not a chat's.</summary>
    public static string? RecordOf(string run) => WorkplaceRunFiles.RecordOf(run);

    /// <summary>
    /// The chat run a record is one of, as the view classifies it, or null: a chat in a tree this home opened that continues no
    /// quest's work and serves no ask, and is not Ask Daoris, a reviewer, a partner, a helper or a teammate's. The press binds and gates
    /// by it, and a turn's end binds by it, so the view and the doors read one run.
    /// </summary>
    public static string? ChatOf(TracedSession session, IReadOnlyList<TracedSession> sessions, Func<string, bool> holds)
    {
        if (session.Teammate || session.Quest is not null || session.Ask is not null || session.Opinion is not null || session.Piece is not null
            || session.Serves is not null || Same(session.Repository, ParticipantWork.HelpRepository)
            || session.Origin?.StartsWith("ask:", StringComparison.Ordinal) == true
            || WorkflowRunReader.WorkOf(session, sessions) is not null
            || session.Tree is not { Length: > 0 } tree || !holds(tree))
        {
            return null;
        }

        return KeyOf(session, sessions);
    }

    /// <summary>
    /// The quest whose work a record continues though its own <c>quest</c> is none: a chat that took one,
    /// a quest's worker gone on as a conversation, a line whose stored origin is a quest's; null for any other, and for a record not read.
    /// </summary>
    public static string? TakenOf(string session, string recordsJson)
    {
        var sessions = Trace.ParseSessions(recordsJson);
        return sessions.FirstOrDefault(each => Same(each.Id, session)) is { Quest: null } record ? WorkflowRunReader.WorkOf(record, sessions) : null;
    }

    /// <summary>The chat run of the record <paramref name="session"/> names, read from the service's records as they were answered.</summary>
    public static string? ChatOf(string session, string recordsJson, Func<string, bool> holds)
    {
        var sessions = Trace.ParseSessions(recordsJson);
        return sessions.FirstOrDefault(each => Same(each.Id, session)) is { } record ? ChatOf(record, sessions, holds) : null;
    }

    /// <summary>The run a record's line in its tree is, <c>chat-&lt;first&gt;</c>; null for a teammate's record or one with no tree.</summary>
    public static string? KeyOf(TracedSession session, IReadOnlyList<TracedSession> sessions)
    {
        if (session.Teammate || session.Tree is not { Length: > 0 } tree) return null;
        if (session.Origin is { } origin && origin.StartsWith("session:", StringComparison.Ordinal)
            && sessions.FirstOrDefault(each => Same(each.Id, origin["session:".Length..])) is { Teammate: false } first && SameTree(first.Tree, tree))
        {
            return Prefix + first.Id;
        }

        return Prefix + (LineOf(session, sessions).FirstOrDefault()?.Id ?? session.Id);
    }

    /// <summary>
    /// The records of a record's line in its tree, oldest first: the same stored line and the same tree, never a teammate's. A
    /// record from a host that stored no line is its line alone.
    /// </summary>
    public static IReadOnlyList<TracedSession> LineOf(TracedSession session, IReadOnlyList<TracedSession> sessions)
    {
        if (session.Entry is not { Length: > 0 } entry || session.Tree is not { Length: > 0 } tree) return [session];
        var line = sessions
            .Where(each => !each.Teammate && Same(each.Entry, entry) && SameTree(each.Tree, tree))
            .OrderBy(each => each.Created ?? DateTimeOffset.MinValue)
            .ToList();
        return line.Count == 0 ? [session] : line;
    }

    /// <summary>
    /// A chat run's conversation's opening: the person's own first words to the line's first record, which
    /// the run is keyed by; null where none is kept.
    /// </summary>
    public static string? OpeningOf(SessionEvents events, string run) =>
        RecordOf(run) is { } first && events.Openings([first]).TryGetValue(first, out var opening) && opening.Length > 0 ? opening : null;

    /// <summary>The states a record ends in, as the run's reader reads them: any other is a live conversation some runner holds.</summary>
    private static readonly HashSet<string> Endings = new(StringComparer.Ordinal) { "completed", "declined", "failed", "stopped", "stood-down" };

    /// <summary>Whether a record is a live conversation: not ended, so its press reserves it against another turn.</summary>
    public static bool Live(TracedSession session) => !Endings.Contains(session.State);

    /// <summary>
    /// The press's view of a chat run's record: the ids of its line in its tree, oldest first, whose words the person's turn count
    /// reads, and whether the record is live; its own id alone, and live, where the records do not hold it.
    /// </summary>
    public static (IReadOnlyList<string> Line, bool Live) PressOf(string session, string recordsJson)
    {
        var sessions = Trace.ParseSessions(recordsJson);
        return sessions.FirstOrDefault(each => Same(each.Id, session)) is { } record
            ? ([.. LineOf(record, sessions).Select(each => each.Id)], Live(record))
            : ([session], true);
    }

    /// <summary>
    /// Why a graph version cannot run a conversation's work: a conversation closes no quest, so a graph holding a
    /// <c>quest.close</c> anywhere is refused at the bind, as a binding's problem; null for any other version.
    /// </summary>
    public static string? GraphRefusal(IWorkflowStore store, WorkflowRunBinding binding)
    {
        if (!GraphRun.Names(binding) || !WorkflowNamed.IsId(binding.Workflow)) return null;
        var graph = store.Load(binding.Workflow)?.Read?.Versions.FirstOrDefault(each => each.Version == binding.Version)?.Graph;
        return graph is not null && graph.Nodes.Any(node => node.Kind == NodeKinds.QuestClose)
            ? $"`{binding.Workflow}` v{binding.Version} holds a `{NodeKinds.QuestClose}`, and a conversation closes no quest, so its work cannot follow it"
            : null;
    }

    /// <summary>
    /// Bind a chat run where none is: <see cref="WorkflowRunBindings.Plan(DriverConfig, string, string, string, string?, string?, WorkflowTaskChoice?, IReadOnlyList{WorkflowPlugin}, DateTimeOffset)"/>
    /// with no ask and no task, so the rows from the repository's default down choose. A graph version binds as a steps version does,
    /// except one holding a <c>quest.close</c>, bound with that as its problem; a named workflow that cannot be read is bound
    /// with its problem; a home that cannot be written fails, saying why. A run bound already is read back.
    /// </summary>
    public static ChatRunBound Bind(
        DriverConfig config, string home, string run, string repository, string? workspace, IReadOnlyList<WorkflowPlugin> plugins, DateTimeOffset at)
    {
        try
        {
            if (WorkflowRunBindings.Read(home, run) is { } kept) return new(kept);
            if (WorkflowRunBindings.Bound(home, run)) return new(null, Failed: "its stored run binding could not be read");
            var binding = WorkflowRunBindings.Plan(config, home, run, repository, workspace, null, null, plugins, at);
            if (binding.Problem is null && GraphRefusal(WorkflowPorts.Store(home), binding) is { } refused) binding = binding with { Problem = refused };
            var wrote = WorkflowRunBindings.Bind(home, binding);
            return new(wrote ? binding : WorkflowRunBindings.Read(home, run) ?? binding) { Wrote = wrote };
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return new(null, Failed: error.Message.TrimEnd().TrimEnd('.'));
        }
    }

    /// <summary>
    /// A press's bind-first: a chat run not bound yet is bound before its press is gated, by its tree's owner, as the gate reads the
    /// tree. A run bound already reads its binding back; a home that cannot keep the
    /// binding comes back <see cref="ChatRunBound.Failed"/>, which refuses the press (<c>bind-failed</c>).
    /// </summary>
    public static ChatRunBound BindFirst(DriverConfig config, string home, string run, string tree, IReadOnlyList<WorkflowPlugin> plugins, DateTimeOffset at)
    {
        var (workspace, repository) = new SessionTrees(home).Owner(tree);
        return Bind(config, home, run, repository, workspace, plugins, at);
    }

    /// <summary>What the conversation is told once of a bind the home could not keep, by its code.</summary>
    public static Noted FailedLine(string problem) => Noted.Of(
        ChatNoteCodes.ChatWorkflowFailed,
        $"Its repository's workflow could not be bound, so this conversation's work cannot be landed yet: {problem}.", ("problem", problem));

    /// <summary>
    /// What the conversation's first Daoris line says of its run under a named workflow, as the look says a start's, by its code:
    /// the workflow and its version, and the repository or the workspace that chose it, a chat having no task and no kind; or the one bound
    /// with why it cannot land the work. Null under Current, which says nothing, as a start's binding says nothing.
    /// </summary>
    public static Noted? Line(WorkflowRunBinding binding)
    {
        if (binding.IsCurrent) return null;
        var owner = binding.Level is WorkflowLevels.Workspace or WorkflowLevels.WorkspaceKind ? binding.Workspace : binding.Repository;
        return binding.Problem is { } problem
            ? Noted.Of(
                ChatNoteCodes.ChatWorkflowUnread,
                $"This conversation's work was bound to `{binding.Workflow}`, the workflow `{owner}` chose, which cannot land it: {problem}",
                ("workflow", binding.Workflow), ("owner", owner), ("problem", problem))
            : Noted.Of(
                ChatNoteCodes.ChatWorkflow, $"This conversation's work follows `{binding.Workflow}` v{binding.Version}, the workflow `{owner}` chose.",
                ("workflow", binding.Workflow), ("version", binding.Version), ("owner", owner));
    }

    /// <summary>
    /// A chat run's three files under a home: its binding, its opinion gate's file and its look's file, each named by its key,
    /// as names relative to the home's own entries.
    /// </summary>
    public static IReadOnlyList<string> Files(string home, string run) =>
        IsChatRun(run) && WorkflowRunBindings.IsRunId(run)
            ? [WorkflowRunBindings.PathOf(home, run), Path.Combine(GatesFolder(home), run + ".json"), Path.Combine(LooksFolder(home), run + ".json"),
                ChatCandidates.PathOf(home, run)]
            : [];

    /// <summary>The folder of the opinion gates' files, by chain or by chat run.</summary>
    public static string GatesFolder(string home) => WorkplaceRunFiles.GatesFolder(home);

    /// <summary>The folder of a chat run's look files, apart from a quest's, which lives on its quest.</summary>
    public static string LooksFolder(string home) => WorkplaceRunFiles.LooksFolder(home);

    private static bool Same(string? left, string? right) => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    internal static bool SameTree(string? left, string right) =>
        left is { Length: > 0 } && string.Equals(Path.TrimEndingDirectorySeparator(left), Path.TrimEndingDirectorySeparator(right), StringComparison.OrdinalIgnoreCase);
}
