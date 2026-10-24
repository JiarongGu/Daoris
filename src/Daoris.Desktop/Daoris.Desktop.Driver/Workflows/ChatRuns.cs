namespace Daoris.Driver;

/// <summary>What a chat run's bind came to (CHATFLOW1c): bound, already bound, a graph version this build binds nothing to, or failed.</summary>
/// <param name="Binding">The binding kept or already there; null where nothing was bound.</param>
/// <param name="Graph">The graph version's workflow chosen, which binds nothing before CHATFLOW1f; null otherwise.</param>
/// <param name="Failed">Why the home could not keep the binding; null otherwise.</param>
public sealed record ChatRunBound(WorkflowRunBinding? Binding, string? Graph = null, string? Failed = null)
{
    /// <summary>Whether this bind wrote the binding: false where it was there already, or nothing was bound.</summary>
    public bool Wrote { get; init; }
}

/// <summary>
/// A chat's run (CHATFLOW1c; the chat-flow design §4.1, §4.3): a conversation's work in a tree of its own runs its repository's workflow,
/// keyed by the line the record stores, <c>chat-&lt;first&gt;</c>, and bound when its tree opens, to Current or a steps version only.
/// </summary>
/// <remarks>
/// <para><b>The key</b> is the line's first record in that tree: the record its stored origin <c>session:&lt;first&gt;</c> names where that
/// record names the same tree, else the line's earliest record there, else the record's own. A hand-over and a limit's go-on open a new
/// record on the same tree in the same line, so they read the same run. A teammate's id holds <c>/</c> and is never a run's.</para>
///
/// <para><b>Never a graph version before CHATFLOW1f</b> (§4.3, §4.5): a choice that gives a graph version binds nothing, and the view
/// answers <see cref="WorkflowNoRun.ChatGraphLater"/>. A named workflow that cannot be read is bound with its <c>Problem</c>, which holds
/// the press by it; only a home that cannot be written fails the bind.</para>
///
/// <para><b>Its files</b> go with the session delete of the record its key names (<see cref="Files"/>), and the leftover rule takes one
/// whose record is gone.</para>
/// </remarks>
public static class ChatRuns
{
    /// <summary>What a chat run's id begins with, apart from a quest's 12 hex digits by its spelling.</summary>
    public const string Prefix = "chat-";

    /// <summary>Whether a run's id is a chat run's.</summary>
    public static bool IsChatRun(string? run) => run is not null && run.StartsWith(Prefix, StringComparison.Ordinal) && run.Length > Prefix.Length;

    /// <summary>The record a chat run's id names, its line's first in its tree; null for a run that is not a chat's.</summary>
    public static string? RecordOf(string run) => IsChatRun(run) ? run[Prefix.Length..] : null;

    /// <summary>
    /// The chat run a record is one of (§4.2), as the view classifies it, or null: a chat in a tree this home opened that continues no
    /// quest's work and serves no ask, and is not Ask Daoris, a reviewer, a partner, a helper or a teammate's. The press binds and gates
    /// by it, and a turn's end binds by it, so the view and the doors read one run.
    /// </summary>
    public static string? ChatOf(TracedSession session, IReadOnlyList<TracedSession> sessions, Func<string, bool> holds)
    {
        if (session.Teammate || session.Quest is not null || session.Ask is not null || session.Opinion is not null || session.Piece is not null
            || session.Serves is not null || Same(session.Repository, HelpRoom.Repository)
            || session.Origin?.StartsWith("ask:", StringComparison.Ordinal) == true
            || WorkflowRunReader.WorkOf(session, sessions) is not null
            || session.Tree is not { Length: > 0 } tree || !holds(tree))
        {
            return null;
        }

        return KeyOf(session, sessions);
    }

    /// <summary>The chat run of the record <paramref name="session"/> names, read from the service's records as they were answered.</summary>
    public static string? ChatOf(string session, string recordsJson, Func<string, bool> holds)
    {
        var sessions = Trace.ParseSessions(recordsJson);
        return sessions.FirstOrDefault(each => Same(each.Id, session)) is { } record ? ChatOf(record, sessions, holds) : null;
    }

    /// <summary>The run a record's line in its tree is, <c>chat-&lt;first&gt;</c> (§4.1); null for a teammate's record or one with no tree.</summary>
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
    /// The records of a record's line in its tree, oldest first (§4.2): the same stored line and the same tree, never a teammate's. A
    /// record from a host before the line was stored is its line alone.
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
    /// The graph version a chat's choice would bind (§4.3), which this build binds nothing to: the workflow's id where the workflow design
    /// §4.1's rows, with no task and no kind, choose a named workflow whose newest version is a graph; null otherwise.
    /// </summary>
    public static string? GraphChosen(DriverConfig config, string home, string repository, string? workspace) =>
        GraphChosen(config, WorkflowPorts.Store(home), repository, workspace);

    /// <inheritdoc cref="GraphChosen(DriverConfig, string, string, string?)"/>
    public static string? GraphChosen(DriverConfig config, IWorkflowStore store, string repository, string? workspace)
    {
        var selected = WorkflowSelection.Resolve(config, repository, workspace, null);
        if (selected.Workflow == WorkflowSelection.Current || !WorkflowNamed.IsId(selected.Workflow)) return null;
        return store.Load(selected.Workflow) is { Read: { Problem: null, Versions.Count: > 0 } read } && read.Versions[^1].Graph is not null
            ? selected.Workflow : null;
    }

    /// <summary>
    /// Bind a chat run where none is (§4.3): <see cref="WorkflowRunBindings.Plan(DriverConfig, string, string, string, string?, string?, WorkflowTaskChoice?, IReadOnlyList{WorkflowPlugin}, DateTimeOffset)"/>
    /// with no ask and no task, so the rows from the repository's default down choose. A graph version binds nothing; a named workflow
    /// that cannot be read is bound with its problem; a home that cannot be written fails, saying why. A run bound already is read back.
    /// </summary>
    public static ChatRunBound Bind(
        DriverConfig config, string home, string run, string repository, string? workspace, IReadOnlyList<WorkflowPlugin> plugins, DateTimeOffset at)
    {
        try
        {
            if (WorkflowRunBindings.Read(home, run) is { } kept) return new(kept);
            if (WorkflowRunBindings.Bound(home, run)) return new(null, Failed: "its stored run binding could not be read");
            if (GraphChosen(config, home, repository, workspace) is { } graph) return new(null, graph);
            var binding = WorkflowRunBindings.Plan(config, home, run, repository, workspace, null, null, plugins, at);
            var wrote = WorkflowRunBindings.Bind(home, binding);
            return new(wrote ? binding : WorkflowRunBindings.Read(home, run) ?? binding) { Wrote = wrote };
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return new(null, Failed: error.Message.TrimEnd().TrimEnd('.'));
        }
    }

    /// <summary>
    /// A press's bind-first (§4.3): a chat run not bound yet is bound before its press is gated, by its tree's owner, as the gate reads the
    /// tree. A run bound already reads its binding back; a graph choice binds nothing and is gated as before; a home that cannot keep the
    /// binding comes back <see cref="ChatRunBound.Failed"/>, which refuses the press (<c>bind-failed</c>).
    /// </summary>
    public static ChatRunBound BindFirst(DriverConfig config, string home, string run, string tree, IReadOnlyList<WorkflowPlugin> plugins, DateTimeOffset at)
    {
        var (workspace, repository) = new SessionTrees(home).Owner(tree);
        return Bind(config, home, run, repository, workspace, plugins, at);
    }

    /// <summary>What the conversation is told once of a bind the home could not keep (§4.3), by its code.</summary>
    public static Noted FailedLine(string problem) => Noted.Of(
        NoteCodes.ChatWorkflowFailed,
        $"Its repository's workflow could not be bound, so this conversation's work cannot be landed yet: {problem}.", ("problem", problem));

    /// <summary>
    /// What the conversation's first Daoris line says of its run under a named workflow (§4.3), as the look says a start's, by its code:
    /// the workflow and its version, and the repository or the workspace that chose it, a chat having no task and no kind; or the one bound
    /// with why it cannot land the work. Null under Current, which says nothing, as a start's binding says nothing.
    /// </summary>
    public static Noted? Line(WorkflowRunBinding binding)
    {
        if (binding.IsCurrent) return null;
        var owner = binding.Level is WorkflowLevels.Workspace or WorkflowLevels.WorkspaceKind ? binding.Workspace : binding.Repository;
        return binding.Problem is { } problem
            ? Noted.Of(
                NoteCodes.ChatWorkflowUnread,
                $"This conversation's work was bound to `{binding.Workflow}`, the workflow `{owner}` chose, which cannot land it: {problem}",
                ("workflow", binding.Workflow), ("owner", owner), ("problem", problem))
            : Noted.Of(
                NoteCodes.ChatWorkflow, $"This conversation's work follows `{binding.Workflow}` v{binding.Version}, the workflow `{owner}` chose.",
                ("workflow", binding.Workflow), ("version", binding.Version), ("owner", owner));
    }

    /// <summary>
    /// A chat run's three files under a home (§4.3): its binding, its opinion gate's file and its look's file, each named by its key,
    /// as names relative to the home's own entries.
    /// </summary>
    public static IReadOnlyList<string> Files(string home, string run) =>
        IsChatRun(run) && WorkflowRunBindings.IsRunId(run)
            ? [WorkflowRunBindings.PathOf(home, run), Path.Combine(GatesFolder(home), run + ".json"), Path.Combine(LooksFolder(home), run + ".json")]
            : [];

    /// <summary>The folder of the opinion gates' files, by chain or by chat run.</summary>
    public static string GatesFolder(string home) => DaorisHome.Entry(home, HomeEntry.OpinionGates);

    /// <summary>The folder of a chat run's look files (§5.3), apart from a quest's, which lives on its quest.</summary>
    public static string LooksFolder(string home) => Path.Combine(WorkflowStore.FolderOf(home), "looks");

    private static bool Same(string? left, string? right) => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    internal static bool SameTree(string? left, string right) =>
        left is { Length: > 0 } && string.Equals(Path.TrimEndingDirectorySeparator(left), Path.TrimEndingDirectorySeparator(right), StringComparison.OrdinalIgnoreCase);
}
