namespace Daoris.Knowledge;

/// <summary>The binding of a taken quest to the graph that closes it, and what a take says of it to its taker.</summary>
internal sealed class QuestGraphBinds(QuestStore quests, IRemotes? remotes)
{
    /// <summary>
    /// The driver binds a taken quest to the graph that closes it (§E5), at the run's first start.
    /// </summary>
    /// <remarks>
    /// The driver's door, never an agent's: no connector tool reaches it, since the graph is the workspace's, set by the person.
    /// The same graph again is the same bind, answered as it stands; another graph is refused, as a quest follows the graph
    /// bound at its start. It commits here and travels like any verb (D68).
    /// </remarks>
    public async Task<QuestRespondOutcome> BindAsync(
        string id, string? graph, DateTimeOffset now, CancellationToken ct = default, bool atTake = false, string? session = null)
    {
        var name = id.TrimStart('#');
        if (string.IsNullOrWhiteSpace(graph))
        {
            return new(QuestRespondRefusal.NotInGraph, "A bind names the graph's digest (`graph`). Nothing moved.", Quest: null);
        }

        if (atTake) return await BindAtTakeAsync(name, graph.Trim(), session ?? "", now, ct).ConfigureAwait(false);

        // The plain bind reads the remote map the take's bind reads, so neither door binds a quest whose
        // circle syncs through a remote. An unknown id falls through to the store, which answers it as before.
        if (await quests.FindAsync(name, ct).ConfigureAwait(false) is { } found && SyncsThroughRemote(found.Workspace))
        {
            return new(QuestRespondRefusal.NotInGraph, ThroughRemote(name, found.Workspace), Quest: null);
        }

        var move = await quests.BindAsync(name, graph.Trim(), now, ct).ConfigureAwait(false);
        if (move.Quest is not { } standing)
        {
            return new(QuestRespondRefusal.NotFound, $"No quest `#{name}`. Ids come from `quest_list`.", Quest: null);
        }

        if (move.ClaimLost)
        {
            return new(QuestRespondRefusal.AlreadyTaken, QuestWords.LostTake(name, "so no graph of this machine's closes it. Nothing moved."), Quest: null);
        }

        if (move.Moved) return new(QuestRespondRefusal.None, $"Quest `#{name}` closes through graph `{graph.Trim()}`: its taker says `ready`.", standing);
        if (standing.Closure == graph.Trim()) return new(QuestRespondRefusal.None, $"Quest `#{name}` already closes through graph `{standing.Closure}`.", standing);
        return new(QuestRespondRefusal.NotInGraph, standing switch
        {
            { Closure: { } other } => $"Quest `#{name}` closes through graph `{other}`, bound at its start: a quest follows the graph "
                                      + "it started under. Nothing moved.",
            { Status: QuestStatus.Taken, Submission: null } => $"Quest `#{name}` is taken on another machine: its graph is that "
                                                               + "machine's to bind. Nothing moved.",
            { Status: QuestStatus.Taken } => $"Quest `#{name}` already has a submission: a graph binds a quest at its start. Nothing moved.",
            _ => $"Quest `#{name}` is {standing.Status}: a graph binds a quest once it is taken, at its run's first start. Nothing moved.",
        }, Quest: null);
    }

    /// <summary>
    /// The graph named for an open quest's take: the driver names it as it opens the quest's first
    /// session, and the take on this machine binds it in the take's own write. Refused on a quest no longer open, whose take has passed
    /// (§E5's cutover: it keeps today's done), and on a quest that leaves this machine for a remote whose handshake has not said every
    /// machine of its workspace runs a build that knows a bound quest.
    /// </summary>
    private async Task<QuestRespondOutcome> BindAtTakeAsync(string name, string graph, string session, DateTimeOffset now, CancellationToken ct)
    {
        if (await quests.FindAsync(name, ct).ConfigureAwait(false) is not { } found)
        {
            return new(QuestRespondRefusal.NotFound, $"No quest `#{name}`. Ids come from `quest_list`.", Quest: null);
        }

        // Named for one session, whose take alone binds it; a driver before it names none, and its quest closes by its done.
        if (string.IsNullOrWhiteSpace(session))
        {
            return new(QuestRespondRefusal.NotInGraph, $"Quest `#{name}`'s graph is named for its take by the session that will take it "
                + "(`session`), and this names none: it closes by its done. Nothing moved.", Quest: null);
        }

        // The map sync reads, as the driver read it before naming the graph, and as the take reads it again, with the
        // remote's handshake as the circle's last pass heard it.
        if (SyncsThroughRemote(found.Workspace)) return new(QuestRespondRefusal.NotInGraph, ThroughRemote(name, found.Workspace), Quest: null);

        // A store a newer build stamped keeps binds this build would misread, so it writes none there.
        if (quests.NewerVersion is not null)
        {
            return new(QuestRespondRefusal.NotInGraph, $"Quest `#{name}`'s store was written by a newer Daoris than this one, so a graph "
                + "is not named for its take here: it closes by its done. Nothing moved.", Quest: null);
        }

        var move = await quests.BindAtTakeAsync(name, graph, session.Trim(), now, ct).ConfigureAwait(false);
        if (move.Quest is not { } standing)
        {
            return new(QuestRespondRefusal.NotFound, $"No quest `#{name}`. Ids come from `quest_list`.", Quest: null);
        }

        return move.Moved
            ? new(QuestRespondRefusal.None, $"Quest `#{name}` binds to graph `{graph}` at its take: its taker says `ready`.", standing)
            : new(QuestRespondRefusal.NotInGraph, $"Quest `#{name}` is {standing.Status}: its take has passed, so it keeps the done it started "
                + "with. Nothing moved.", Quest: null);
    }

    /// <summary>What a take bound at its take tells its taker: its result is `ready`, never `done`.</summary>
    public static string BoundAtTake(Quest quest) => quest is { Status: QuestStatus.Taken, Closure: { } graph }
        ? $" It closes through its graph `{graph}`: when its work is done, say `ready` with your note and an answer for each requirement, "
          + "never `done`; the graph writes its Done at the work's end, once its holds and gates are through."
        : "";

    /// <summary>
    /// What a take that went unbound tells the session its graph was named for, once, in its own answer: that session
    /// was told `ready` at its start, and finishes with `done`, as any quest. Nothing for a take no graph was named for, nor for another
    /// session's take or an unowned row's, whose sessions were told nothing of a graph.
    /// </summary>
    public static string UnboundAtTake(QuestTakeBind bind)
    {
        var why = bind switch
        {
            QuestTakeBind.Expired => "its closure graph was named for this take, and that naming expired before the take came",
            QuestTakeBind.Replaced => "its closure graph was named for an earlier publication of this quest, deleted and asked again since",
            QuestTakeBind.Remote => "its workspace syncs through a remote, and not every machine of it has declared it knows a bound quest "
                                    + "since its closure graph was named for this take",
            _ => null,
        };
        return why is null ? "" : $" It is not bound to a graph: {why}. Finish it with `done` when its work is done, as any quest, whatever your instruction said.";
    }

    /// <summary>
    /// Whether <paramref name="workspace"/> syncs through a remote by this machine's map, read when asked, whose handshake
    /// has not said every machine of it knows a bound quest: the map sync reads, and the remote's answer as the circle's
    /// last pass heard it. A circle with a remote binds only once that pass heard every machine declared, whoever of it is joined.
    /// </summary>
    public bool SyncsThroughRemote(string workspace) => remotes?.For(workspace) is not null && quests.ClosureOf(workspace) is not { All: true };

    /// <summary>
    /// Why a bind, at the take or not, is refused a quest whose circle syncs through a remote that has not said every machine of it knows a
    /// bound quest: the machines it names, or that no pass has heard it say.
    /// </summary>
    private string ThroughRemote(string name, string workspace) => quests.ClosureOf(workspace) is { Undeclared: { Count: > 0 } undeclared }
        ? $"Quest `#{name}` syncs through a remote where {Machines(undeclared)} of its workspace "
          + (undeclared.Count == 1 ? "has not declared it knows" : "have not declared they know")
          + " a bound quest (a Daoris older than this one, or one not synced since it was updated): it closes by its done. Nothing moved."
        : $"Quest `#{name}` syncs through a remote that has not said every machine of its workspace knows a bound quest (no sync has heard it "
          + "since, or the remote is older than this host): it closes by its done. Nothing moved.";

    /// <summary>The machines a refusal names, by id, at most three and how many more.</summary>
    private static string Machines(IReadOnlyList<string> machines) =>
        (machines.Count == 1 ? "machine " : "machines ") + string.Join(", ", machines.Take(3).Select(m => $"`{m}`"))
        + (machines.Count > 3 ? $" and {machines.Count - 3} more" : "");
}
