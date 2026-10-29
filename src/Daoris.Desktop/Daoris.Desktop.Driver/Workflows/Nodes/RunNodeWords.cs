namespace Daoris.Driver;

/// <summary>What the person's word on a run's node came to at its door (FABRIC-CUTOVER1f): kept, or refused with its code.</summary>
/// <param name="Run">The run, as its binding names it.</param>
/// <param name="Node">The node's id in the run's graph.</param>
/// <param name="Act">One of <see cref="NodeWordActs"/>, or the word as given where it is neither.</param>
/// <param name="Refused">Why nothing was kept, or null where it was.</param>
public sealed record NodeWorded(string Run, string Node, string Act, string? Refused)
{
    public bool Done => Refused is null;

    /// <summary>What a refusal needs to say itself: the quest the run ended at, or why its version does not read.</summary>
    public string? Detail { get; init; }

    /// <summary>Where the node stood, for a move the one table does not make from there.</summary>
    public string? State { get; init; }
}

/// <summary>
/// The person's skip and stop of a run's node (FABRIC-CUTOVER1f; D190 points 3 and 6, the cutover design §3, §5.2): the door that keeps their
/// word on the run's record, the runtime's application of it, and what the run's view offers. A go-ahead nobody raises would otherwise strand a
/// run once the doors hold on its graph (CUTOVER1d), and so would a node after <c>handed</c> told for two runs at one tip (§2.4.1).
/// </summary>
/// <remarks>
/// <para><b>One writer.</b> The door writes only the word, in its own field (<see cref="NodeRecord.Pending"/>), under the record's lock with the
/// lease asked inside it (<see cref="NodeRecordFiles.Edit{T}"/>), as a held attempt's word is kept (<see cref="WorkPerformer.Word"/>); the runtime
/// applies it at its next pass through the one table (<see cref="NodeLifecycle"/>), scoped to the run as it scopes the run's starts, and takes it
/// off whether it moved the node or not, saying which.</para>
/// <para><b>Judged twice, by one table.</b> The door judges the word on the run's graph and record as they stand, so a word that cannot apply is
/// refused where it is given; the pass judges it again on the record it applies to, since the node may have moved between.</para>
/// </remarks>
public static class RunNodeWords
{
    /// <summary>No run here by that id is bound to a graph version: a steps run's steps are D157's, and have no node to say this of.</summary>
    public const string NotRun = "not-run";

    /// <summary>The graph version the run is bound to does not read here, so the run is held whole and nothing on it is changed.</summary>
    public const string UnreadVersion = "unread-version";

    /// <summary>The run's graph holds no node by that id.</summary>
    public const string NotNode = "not-node";

    /// <summary>The word is neither <c>skip</c> nor <c>stop</c>.</summary>
    public const string NotAct = "not-act";

    /// <summary>The run's record holds its end: nothing of its graph moves again until its chain comes back here.</summary>
    public const string Ended = "ended";

    /// <summary>The node already holds a word of the person's that the next pass has not applied yet.</summary>
    public const string Worded = "worded";

    /// <summary>
    /// Keep the person's <paramref name="act"/> on <paramref name="node"/> of <paramref name="run"/>, with their <paramref name="words"/>, on the
    /// run's record for the next pass: judged first on the run's graph and record by the one table, so a skip with no words, a node the person may
    /// not pass over, or one that cannot move from where it stands is refused here and nothing is kept.
    /// </summary>
    public static NodeWorded Word(NodeRecordFiles records, string run, string node, string act, string? words, Func<DateTimeOffset>? clock = null)
    {
        run = run.Trim().TrimStart('#');
        words = string.IsNullOrWhiteSpace(words) ? null : words.Trim();
        NodeWorded Refuse(string code) => new(run, node, act, code);

        if (!NodeWordActs.All.Contains(act, StringComparer.Ordinal)) return Refuse(NotAct);
        if (new GraphRuns(records).Find(run) is not { } bound) return Refuse(NotRun);
        if (bound.Version is not { Graph: { } graph, GraphDigest: { } digest }) return Refuse(UnreadVersion) with { Detail = bound.Version.Unreadable };
        if (graph.Nodes.FirstOrDefault(each => each.Id == node) is not { } declared) return Refuse(NotNode);

        var now = (clock ?? (() => DateTimeOffset.UtcNow))();
        var (refused, result) = records.Edit(bound.Unit, record =>
        {
            if (record.Ended is { } ended) return (null, Refuse(Ended) with { Detail = ended.Quest });
            if (record.Pending.Any(each => each.Node == node)) return (null, Refuse(Worded));
            var status = WorkPerformer.StatusOf(record, node);
            var judged = NodeLifecycle.Apply(declared, status, Move(new NodeWord(node, act, words, now), new NodeScope(digest, bound.Run)));
            if (!judged.Moved) return (null, Refuse(judged.Refused!) with { State = status.State });
            return (record with { Pending = [.. record.Pending, new NodeWord(node, act, words, now)] }, new NodeWorded(run, node, act, null));
        });
        return refused is null ? result! : Refuse(refused);
    }

    /// <summary>
    /// The person's words a run's record holds, applied through the one table under the run's declarations and its scope (the graph's digest and
    /// the run, as its starts are scoped), each kept with what it cleared; every word is taken off, applied or not. Pure: the runtime writes it.
    /// </summary>
    /// <returns>The record after them, null where none waited; and each word with why it was not applied, or null where it was.</returns>
    public static (NodeRecord? Next, IReadOnlyList<(NodeWord Word, string? Refused)> Applied) Apply(
        NodeRecord record, IReadOnlyDictionary<string, NodeDeclaration> declared, NodeScope scope, DateTimeOffset now)
    {
        if (record.Pending.Count == 0) return (null, []);
        var next = record;
        var applied = new List<(NodeWord, string?)>();
        foreach (var word in record.Pending)
        {
            if (!declared.TryGetValue(word.Node, out var node))
            {
                applied.Add((word, NotNode));
                continue;
            }

            var moved = NodeLifecycle.Apply(node, WorkPerformer.StatusOf(next, node.Id), Move(word, scope));
            if (!moved.Moved)
            {
                applied.Add((word, moved.Refused));
                continue;
            }

            next = WorkPerformer.Kept(next, moved, "person-" + word.Act, now);
            applied.Add((word, null));
        }

        return (next with { Pending = [] }, applied);
    }

    /// <summary>A word as the one table's move: the person's skip with their words and the run's scope, or their stop.</summary>
    private static NodeMove Move(NodeWord word, NodeScope scope) => word.Act == NodeWordActs.Skip
        ? new NodeMove(NodeStates.Skipped, NodeCause.Person) { Words = word.Words, Scope = scope }
        : new NodeMove(NodeStates.Stopped, NodeCause.Person) { Words = word.Words };

    /// <summary>
    /// What the run's view offers the person on a node standing at <paramref name="status"/>: its skip where it is ready or under way and its kind
    /// lets the person pass it over, and its stop wherever it is under way or about to be judged. A node not reached yet is offered neither, though
    /// the door takes a word on it as the table does; a run that ended or does not read offers none, which its reader decides.
    /// </summary>
    public static IReadOnlyList<string> Acts(NodeDeclaration node, NodeStatus status)
    {
        var acts = new List<string>();
        if (status.State is NodeStates.Ready or NodeStates.Active && NodeAuthorities.Of(node)?.PersonSkips == true) acts.Add(NodeWordActs.Skip);
        if (status.State is NodeStates.Ready or NodeStates.Active or NodeStates.Unknown or NodeStates.Invalidated) acts.Add(NodeWordActs.Stop);
        return acts;
    }

    /// <summary>The usage of the terminal's skip and stop, as it says them when their words are not understood.</summary>
    public const string Usage =
        "usage: daoris-driver workflow skip <run> <node> \"…\"  ·  daoris-driver workflow stop <run> <node> [\"…\"]\n"
        + "       pass a node of a run bound to a graph version over with your words, or stop it, your words if you have any; the run's\n"
        + "       id and its nodes are what `workflow run` prints. Kept on the run's record, and Daoris's next look applies it.";

    /// <summary>The sentence a door says for what the person's word came to: kept, or refused and why.</summary>
    public static string Said(NodeWorded done) => (done.Refused, done.Act) switch
    {
        (null, _) => $"workflow: your {done.Act} of `{done.Node}` on run {done.Run} is kept; Daoris's next look applies it.",
        (NodeRecordFiles.NotHeld, _) =>
            "workflow: this process does not hold the home, so it keeps nothing: the desktop holds it while it runs, so say it there, or close the "
            + "desktop and run this again.",
        (NodeRecordFiles.Unreadable, _) => $"workflow: run {done.Run}'s node record does not read, so nothing on it is changed.",
        (NotRun, _) => $"workflow: no run `{done.Run}` here is bound to a graph version, so it has no node to pass over or stop.",
        (UnreadVersion, _) =>
            $"workflow: the graph version run {done.Run} is bound to does not read here ({done.Detail}), so the run is held whole and nothing on it is changed.",
        (NotNode, _) => $"workflow: run {done.Run}'s graph has no node `{done.Node}`; `workflow run` names its nodes.",
        (NotAct, _) => $"workflow: `{done.Act}` is not a word for a run's node: say skip or stop.",
        (Ended, _) => $"workflow: run {done.Run} ended at quest #{done.Detail}, so nothing of its graph moves again until its chain comes back here.",
        (Worded, _) => $"workflow: `{done.Node}` on run {done.Run} already has your word, which Daoris's next look applies.",
        (NodeRefusal.NoWords, _) => $"workflow: passing `{done.Node}` over keeps your words with it, and none were given: say why.",
        (NodeRefusal.NotSkippable, _) =>
            $"workflow: `{done.Node}` is a step its kind never lets be passed over, since a skip would read as its proof: stop it instead.",
        (NodeRefusal.NotInTable, NodeWordActs.Skip) =>
            $"workflow: `{done.Node}` on run {done.Run} is {done.State} now, so there is nothing to pass over.",
        (NodeRefusal.NotInTable, _) => $"workflow: `{done.Node}` on run {done.Run} is {done.State} now, so there is nothing to stop.",
        (var code, _) => $"workflow: your {done.Act} of `{done.Node}` on run {done.Run} was not kept ({code}).",
    };

    /// <summary>What the pass says of a word it applied, or why one was not applied, on the run's unit.</summary>
    public static string AppliedSaid(string unit, NodeWord word, string? refused) => refused is null
        ? $"work  {unit}: your {word.Act} of `{word.Node}` is applied{(word.Words is null ? "" : ", with your words")}."
        : $"work  {unit}: your {word.Act} of `{word.Node}` was not applied ({refused}): it moved before Daoris's look came, so say again what it should do.";
}
