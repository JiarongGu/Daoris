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

    /// <summary>The word is <c>ask-again</c>, <c>go-on</c> or <c>send</c>, which only a <c>request</c> takes.</summary>
    public const string NotRequest = "not-request";

    /// <summary>A <c>send</c> of a request that is not ready to be sent, or that sends itself (<c>start: automatic</c>).</summary>
    public const string NotSendable = "not-sendable";

    /// <summary>The word is <c>done</c>, <c>approve</c> or <c>reject</c>, which only a person's task (<c>human</c>) takes.</summary>
    public const string NotTask = "not-task";

    /// <summary>A <c>done</c>, <c>approve</c> or <c>reject</c> of a task that does not wait for that press where it stands now.</summary>
    public const string NotWaiting = "not-waiting";

    /// <summary>The presses a node takes only while it waits for them: a person's task's.</summary>
    private static readonly string[] Waited = [NodeWordActs.Done, NodeWordActs.Approve, NodeWordActs.Reject];

    /// <summary>The record's mark of the person's send of <paramref name="node"/>'s attempt, which the next pass reads as the start's say-so (INTEGRATE1p).</summary>
    public static string SendKey(string node, int attempt) => $"send:{node}:{attempt}";

    /// <summary>Whether <paramref name="node"/>'s kind offers its own <paramref name="act"/> where the node stands at <paramref name="status"/>.</summary>
    private static bool Offers(NodeDeclaration node, NodeStatus status, string act) =>
        NodeKit.Of(node.Kind) is IPressed pressed && pressed.Acts(node, status).Contains(act, StringComparer.Ordinal);

    /// <summary>The kind whose own act <paramref name="act"/> is, which no other kind takes; null for the lifecycle's acts every kind takes.</summary>
    private static IPressed? TakenBy(string act) =>
        NodeKit.Built.Units.OfType<IPressed>().FirstOrDefault(pressed => pressed.Takes.Contains(act, StringComparer.Ordinal));

    /// <summary>The word is <c>accept</c> or <c>changes</c>, which only a <c>review</c> takes (LANDNODE1c2, DECIDE1c).</summary>
    public const string NotReview = "not-review";

    /// <summary>The run's record holds its end: nothing of its graph moves again until its chain comes back here.</summary>
    public const string Ended = "ended";

    /// <summary>The node already holds a word of the person's that the next pass has not applied yet.</summary>
    public const string Worded = "worded";

    /// <summary>
    /// The stop of an <c>integrate</c> whose member the integration record says promoted (INTEGRATE1n; §17.4, F50): nothing reverses the line,
    /// so there is nothing to stop, and the node reads its record's reach.
    /// </summary>
    public const string Promoted = "promoted";

    /// <summary>
    /// Keep the person's <paramref name="act"/> on <paramref name="node"/> of <paramref name="run"/>, with their <paramref name="words"/>, on the
    /// run's record for the next pass: judged first on the run's graph and record by the one table, so a skip with no words, a node the person may
    /// not pass over, or one that cannot move from where it stands is refused here and nothing is kept.
    /// </summary>
    /// <param name="promoted">
    /// The nodes whose integration the record says promoted (<see cref="IntegrateNodes.PromotedAsync"/>): a stop of one is refused
    /// (<see cref="Promoted"/>, F50). Null where the caller reads none, and nothing is refused on a guess.
    /// </param>
    public static NodeWorded Word(
        NodeRecordFiles records, string run, string node, string act, string? words, Func<DateTimeOffset>? clock = null, IReadOnlySet<string>? promoted = null)
    {
        run = run.Trim().TrimStart('#');
        words = string.IsNullOrWhiteSpace(words) ? null : words.Trim();
        NodeWorded Refuse(string code) => new(run, node, act, code);

        if (!NodeWordActs.All.Contains(act, StringComparer.Ordinal)) return Refuse(NotAct);
        if (new GraphRuns(records).Find(run) is not { } bound) return Refuse(NotRun);
        if (bound.Version is not { Graph: { } graph, GraphDigest: { } digest }) return Refuse(UnreadVersion) with { Detail = bound.Version.Unreadable };
        if (graph.Nodes.FirstOrDefault(each => each.Id == node) is not { } declared) return Refuse(NotNode);
        if (TakenBy(act) is { } owner && !ReferenceEquals(NodeKit.Of(declared.Kind), owner)) return Refuse(owner.Refusal);

        var now = (clock ?? (() => DateTimeOffset.UtcNow))();
        var (refused, result) = records.Edit(bound.Unit, record =>
        {
            if (record.Ended is { } ended) return (null, Refuse(Ended) with { Detail = ended.Quest });
            if (record.Pending.Any(each => each.Node == node)) return (null, Refuse(Worded));
            if (act == NodeWordActs.Stop && promoted?.Contains(node) == true) return (null, Refuse(Promoted));
            var status = WorkPerformer.StatusOf(record, node);
            if (act == NodeWordActs.Send)
            {
                if (!Offers(declared, status, NodeWordActs.Send)) return (null, Refuse(NotSendable) with { State = status.State });
            }
            else
            {
                if (Waited.Contains(act) && !Offers(declared, status, act)) return (null, Refuse(NotWaiting) with { State = status.State });
                var judged = NodeLifecycle.Apply(declared, status, Move(declared, new NodeWord(node, act, words, now), new NodeScope(digest, bound.Run), status));
                if (!judged.Moved) return (null, Refuse(judged.Refused!) with { State = status.State });
            }

            return (record with { Pending = [.. record.Pending, new NodeWord(node, act, words, now)] }, new NodeWorded(run, node, act, null));
        });
        return refused is null ? result! : Refuse(refused);
    }

    /// <summary>
    /// The person's words where <paramref name="node"/> of the run's record stands skipped by them, or their skip waits there for the next
    /// look to apply; null otherwise. The gate of a chat run's look reads it (CHATFLOW1d2).
    /// </summary>
    public static string? SkippedLook(NodeRecord? record, string? node)
    {
        if (node is null || record is null) return null;
        if (record.Statuses.GetValueOrDefault(node) is { State: NodeStates.Skipped, By: NodeCause.Person, Words: { Length: > 0 } words }) return words;
        return record.Pending.LastOrDefault(each => each.Node == node && each.Act == NodeWordActs.Skip && !string.IsNullOrWhiteSpace(each.Words))?.Words;
    }

    /// <summary>
    /// <see cref="Word"/> for the doors the person presses through (INTEGRATE1n part n3, F50): a stop first reads the integration record through
    /// <paramref name="door"/> for the run's nodes whose open request's member promoted, so it is refused before anything is kept. A skip, a run
    /// that is not bound and a record with no request of an <c>integrate</c> of its own read nothing of it. The pass judges the word again.
    /// </summary>
    public static async Task<NodeWorded> WordAsync(
        NodeRecordFiles records, IIntegrationDoor door, string run, string node, string act, string? words, Func<DateTimeOffset>? clock = null,
        CancellationToken ct = default)
    {
        IReadOnlySet<string>? promoted = null;
        if (act == NodeWordActs.Stop && new GraphRuns(records).Find(run.Trim().TrimStart('#')) is { } bound && records.Read(bound.Unit) is { } record)
        {
            promoted = await IntegrateNodes.PromotedAsync(door, record, ct).ConfigureAwait(false);
        }

        return Word(records, run, node, act, words, clock, promoted);
    }

    /// <summary>
    /// The person's words a run's record holds, applied through the one table under the run's declarations and its scope (the graph's digest and
    /// the run, as its starts are scoped), each kept with what it cleared; every word is taken off, applied or not. Pure: the runtime writes it.
    /// </summary>
    /// <returns>The record after them, null where none waited; and each word with why it was not applied, or null where it was.</returns>
    public static (NodeRecord? Next, IReadOnlyList<(NodeWord Word, string? Refused)> Applied) Apply(
        NodeRecord record, IReadOnlyDictionary<string, NodeDeclaration> declared, NodeScope scope, DateTimeOffset now, IReadOnlySet<string>? promoted = null)
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

            // INTEGRATE1n (F50): a stop said before the member promoted, and applied after, is refused as the door refuses it.
            if (word.Act == NodeWordActs.Stop && promoted?.Contains(word.Node) == true)
            {
                applied.Add((word, Promoted));
                continue;
            }

            var standing = WorkPerformer.StatusOf(next, node.Id);
            if (word.Act == NodeWordActs.Send)
            {
                if (!Offers(node, standing, NodeWordActs.Send))
                {
                    applied.Add((word, NotSendable));
                    continue;
                }

                next = next with { Fired = [.. next.Fired, SendKey(node.Id, standing.Attempt)] };
                applied.Add((word, null));
                continue;
            }

            if (Waited.Contains(word.Act) && !Offers(node, standing, word.Act))
            {
                applied.Add((word, NotWaiting));
                continue;
            }

            var moved = NodeLifecycle.Apply(node, standing, Move(node, word, scope, standing));
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

    /// <summary>
    /// A word as the one table's move: a kind's own act the move its kind asks (D247), the door having refused it on any other kind; else the
    /// person's skip with their words and the run's scope, or their stop.
    /// </summary>
    private static NodeMove Move(NodeDeclaration node, NodeWord word, NodeScope scope, NodeStatus status) =>
        TakenBy(word.Act) is { } owner && owner.Move(node, word, status, scope) is { } own ? own
        : word.Act == NodeWordActs.Skip ? new NodeMove(NodeStates.Skipped, NodeCause.Person) { Words = word.Words, Scope = scope }
        : new NodeMove(NodeStates.Stopped, NodeCause.Person) { Words = word.Words };


    /// <summary>
    /// What the run's view offers the person on a node standing at <paramref name="status"/>: its skip where it is ready or under way and its kind
    /// lets the person pass it over, and its stop wherever it is under way or about to be judged. A node not reached yet is offered neither, though
    /// the door takes a word on it as the table does; a run that ended or does not read offers none, which its reader decides.
    /// </summary>
    /// <param name="promoted">
    /// Whether the integration record says this node's member promoted onto the line (INTEGRATE1n; §17.4, F50): its stop is then not offered,
    /// since nothing reverses the line, and the node reads its record's reach.
    /// </param>
    public static IReadOnlyList<string> Acts(NodeDeclaration node, NodeStatus status, bool promoted = false)
    {
        // A kind's own acts lead (D247): a review's changes, a request's send, or its asking again and going on.
        var acts = NodeKit.Of(node.Kind) is IPressed pressed ? new List<string>(pressed.Acts(node, status)) : [];
        if (status.State is NodeStates.Ready or NodeStates.Active && NodeAuthorities.Of(node)?.PersonSkips == true) acts.Add(NodeWordActs.Skip);
        if (status.State is NodeStates.Ready or NodeStates.Active or NodeStates.Unknown or NodeStates.Invalidated && !promoted) acts.Add(NodeWordActs.Stop);
        return acts;
    }

    /// <summary>The usage of the terminal's skip and stop, as it says them when their words are not understood.</summary>
    public const string Usage =
        "usage: daoris workflow skip <run> <node> \"…\"  ·  daoris workflow stop <run> <node> [\"…\"]\n"
        + "       daoris workflow send <run> <node>  (a ready request under `start: you`)\n"
        + "       daoris workflow ask-again <run> <node>  ·  daoris workflow go-on <run> <node> \"…\"  (a declined request)\n"
        + "       daoris workflow changes <run> <node> \"…\"  (a review: what the work should change before it lands)\n"
        + "       daoris workflow done <run> <node> [\"…\"]  ·  daoris workflow approve|reject <run> <node> [\"…\"]  (your task, with a note if you like)\n"
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
        (NotAct, _) => $"workflow: `{done.Act}` is not a word for a run's node: say skip, stop, send, ask-again, go-on, changes, done, approve or reject.",
        (NotTask, _) => $"workflow: `{done.Node}` is no task of yours, and only your task is done, and only an approval approved or rejected.",
        (NotWaiting, _) =>
            $"workflow: `{done.Node}` on run {done.Run} is {done.State} now and waits for no {done.Act} of yours: a waiting task takes done, a waiting approval approve or reject.",
        (NotRequest, _) => $"workflow: `{done.Node}` is no request, and only a request is sent, and only a declined one asked again or gone on without.",
        (NotSendable, _) => $"workflow: `{done.Node}` on run {done.Run} is {done.State} now, or sends itself, so it has no send to wait for.",
        (NotReview, _) => $"workflow: `{done.Node}` is no review, and only a review is accepted or asked for changes.",
        (NodeRefusal.NoWords, NodeWordActs.Changes) =>
            $"workflow: asking for changes on `{done.Node}` sends your words to the work, and none were given: say what should change.",
        (NodeRefusal.NotInTable, NodeWordActs.Changes) =>
            $"workflow: `{done.Node}` on run {done.Run} is {done.State} now, so it waits on no Accept to ask changes of.",
        (Ended, _) => $"workflow: run {done.Run} ended at quest #{done.Detail}, so nothing of its graph moves again until its chain comes back here.",
        (Worded, _) => $"workflow: `{done.Node}` on run {done.Run} already has your word, which Daoris's next look applies.",
        (Promoted, _) => $"workflow: `{done.Node}` on run {done.Run} has promoted onto the line, and nothing reverses the line, so there is nothing to stop: its record reads its reach.",
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
