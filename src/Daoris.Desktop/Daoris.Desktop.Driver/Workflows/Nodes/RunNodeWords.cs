namespace Daoris.Driver;

/// <summary>What the person's word on a run's node came to at its door: kept, or refused with its code.</summary>
/// <param name="Run">The run, as its binding names it.</param>
/// <param name="Node">The node's id in the run's graph.</param>
/// <param name="Act">One of <see cref="NodeWordActs"/>, or the word as given where it is neither.</param>
/// <param name="Refused">Why nothing is kept, or null where the word is kept.</param>
public sealed record NodeWorded(string Run, string Node, string Act, string? Refused)
{
    public bool Done => Refused is null;

    /// <summary>What a refusal needs to say itself: the quest the run ended at, or why its version does not read.</summary>
    public string? Detail { get; init; }

    /// <summary>Where the node stands, for a move the one table does not make from there.</summary>
    public string? State { get; init; }

    /// <summary>An Accept kept for a review that has not begun, applied at the first pass that finds it waiting on the record named.</summary>
    public bool Early { get; init; }
}

/// <summary>
/// The person's skip and stop of a run's node: the door that keeps their
/// word on the run's record, the runtime's application of it, and what the run's view offers. A go-ahead nobody raises would otherwise strand a
/// run once the doors hold on its graph, and so would a node after <c>handed</c> told for two runs at one tip.
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
    /// <summary>No run here by that id is bound to a graph version: a steps run's steps have no node to say this of.</summary>
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

    /// <summary>The word is <c>done</c>, <c>approve</c> or <c>reject</c>, which only a role's task (<c>task</c>) takes.</summary>
    public const string NotTask = "not-task";

    /// <summary>A <c>done</c>, <c>approve</c> or <c>reject</c> of a task that does not wait for that press where it stands now.</summary>
    public const string NotWaiting = "not-waiting";

    /// <summary>The word is a choice of a file changed on both sides, which only a <c>return</c> waiting on one takes.</summary>
    public const string NotReturn = "not-return";

    /// <summary>A <c>decide</c> naming no outcome its decision declares.</summary>
    public const string NotOutcome = "not-outcome";

    /// <summary>A <c>done-outside</c> of a step that is not failed, or whose kind reads no proof of it (<see cref="Outside"/>).</summary>
    public const string NotOutside = "not-outside";

    /// <summary>An <c>again</c> of a plugin step whose failed attempt is held for the person: its own <i>Try again</i> is that attempt's.</summary>
    public const string HeldAgain = "held-again";

    /// <summary>The person's words at the step a run ended failed at, each of which governs the run again (<see cref="FailedHeldRuns"/>).</summary>
    private static readonly string[] Settles = [NodeWordActs.Stop, NodeWordActs.Again, NodeWordActs.DoneOutside];

    /// <summary>
    /// Whether the person may claim a failed step done outside Daoris: its kind reads its proof once claimed (a push, origin's ref at the
    /// landed tip), so the claim is kept on its status and the step's observation settles it; a plugin's is said on its attempt instead.
    /// </summary>
    public static bool Outside(NodeDeclaration node, NodeStatus status) =>
        status.State == NodeStates.Failed && !NodeKit.IsPlugin(node.Kind) && NodeKit.Of(node.Kind) is IObserves observes
        && observes.ObservesFailed(status with { Holds = NodeWordActs.DoneOutside });

    /// <summary>The acts kept as a mark of the node's attempt, moving no state: a request's send and a return's choices.</summary>
    private static bool Marked(string act) => act == NodeWordActs.Send || NodeWordActs.Choices.Contains(act, StringComparer.Ordinal);

    /// <summary>The presses a node takes only while it waits for them: a person's task's, and a decision's answer, which its outcome names.</summary>
    private static readonly string[] Waited = [NodeWordActs.Done, NodeWordActs.Approve, NodeWordActs.Reject, NodeWordActs.Decide];

    /// <summary>Why a <paramref name="word"/> cannot apply at <paramref name="status"/> beyond its press being offered: a decide's outcome and holder.</summary>
    private static string? Answer(NodeDeclaration node, NodeStatus status, NodeWord word) =>
        word.Act == NodeWordActs.Decide ? TaskDecisions.Refused(node, status, word.Outcome, word.Holder) : null;

    /// <summary>The record's mark of the person's send of <paramref name="node"/>'s attempt, which the next pass reads as the start's say-so.</summary>
    public static string SendKey(string node, int attempt) => $"send:{node}:{attempt}";

    /// <summary>Whether <paramref name="node"/>'s kind offers its own <paramref name="act"/> where the node stands at <paramref name="status"/>.</summary>
    private static bool Offers(NodeDeclaration node, NodeStatus status, string act) =>
        NodeKit.Of(node.Kind) is IPressed pressed && pressed.Acts(node, status).Contains(act, StringComparer.Ordinal);

    /// <summary>The kind whose own act <paramref name="act"/> is, which no other kind takes; null for the lifecycle's acts every kind takes.</summary>
    private static IPressed? TakenBy(string act) =>
        NodeKit.Built.Units.OfType<IPressed>().FirstOrDefault(pressed => pressed.Takes.Contains(act, StringComparer.Ordinal));

    /// <summary>The word is <c>accept</c> or <c>changes</c>, which only a <c>review</c> takes.</summary>
    public const string NotReview = "not-review";

    /// <summary>The run's record holds its end: nothing of its graph moves again until its chain comes back here.</summary>
    public const string Ended = "ended";

    /// <summary>The node already holds a word of the person's that the next pass has not applied yet.</summary>
    public const string Worded = SupervisorWhy.Worded;

    /// <summary>A supervisor's word on an attempt the node has left, or whose decision is not live in this run: dropped, the step the person's.</summary>
    public const string Stale = "stale";

    /// <summary>
    /// The stop of an <c>integrate</c> whose member the integration record says promoted: nothing reverses the line, so nothing to stop.
    /// </summary>
    public const string Promoted = "promoted";

    /// <summary>The hold <paramref name="node"/> stands at now, or null where it stands at none.</summary>
    private static string? HoldOf(NodeDeclaration node, NodeStatus status) =>
        status.State == NodeStates.Active && NodeKit.Of(node.Kind) is IWaitsOnRole waits ? waits.Waits(node, status) : null;

    private static string? AnswersHold(NodeDeclaration node, NodeStatus status, string act) =>
        HoldOf(node, status) ?? (act == NodeWordActs.Accept && status.State is NodeStates.Blocked or NodeStates.Ready
            && HoldOf(node, status with { State = NodeStates.Active }) is PersonWaits.Accept and var coming ? coming : null);

    /// <summary>
    /// Keep the person's <paramref name="act"/> on <paramref name="node"/> of <paramref name="run"/>, with their <paramref name="words"/>, on the
    /// run's record for the next pass: judged first on the run's graph and record by the one table, so a skip with no words, a node the person may
    /// not pass over, or one that cannot move from where it stands is refused here and nothing is kept.
    /// </summary>
    /// <param name="promoted">
    /// The nodes whose integration the record says promoted (<see cref="IntegrateNodes.PromotedAsync"/>): a stop of one is refused
    /// (<see cref="Promoted"/>). Null where the caller reads none, and nothing is refused on a guess.
    /// </param>
    /// <param name="outcome">A <c>decide</c>'s answer, one of its decision's outcomes.</param>
    /// <param name="holder">Who holds the actor a decide's proposal is from, as its channel names it; null for the owner's own word.</param>
    /// <param name="epoch">The holder's epoch the word is given in (<see cref="StepTenureFiles"/>): an ended one is refused; null names none.</param>
    /// <param name="settle">Who the word is from, where its caller names it (an observation, an agent's channel); null reads it from the act (<see cref="SettleBy.Of"/>).</param>
    public static NodeWorded Word(
        NodeRecordFiles records, string run, string node, string act, string? words, Func<DateTimeOffset>? clock = null, IReadOnlySet<string>? promoted = null,
        NodeWordBy? by = null, int? attempt = null, string? revision = null, string? part = null, string? outcome = null, string? holder = null, int? epoch = null,
        SettleBy? settle = null, IReadOnlyCollection<string>? workers = null)
    {
        run = run.Trim().TrimStart('#');
        words = string.IsNullOrWhiteSpace(words) ? null : words.Trim();
        outcome = act == NodeWordActs.Decide && !string.IsNullOrWhiteSpace(outcome) ? outcome.Trim() : null;
        holder = act == NodeWordActs.Decide && !string.IsNullOrWhiteSpace(holder) ? holder.Trim() : null;
        NodeWorded Refuse(string code) => new(run, node, act, code);

        if (!NodeWordActs.All.Contains(act, StringComparer.Ordinal)) return Refuse(NotAct);
        if (new GraphRuns(records).Find(run) is not { } bound) return Refuse(NotRun);
        if (bound.Version is not { Graph: { } graph, GraphDigest: { } digest }) return Refuse(UnreadVersion) with { Detail = bound.Version.Unreadable };
        if (graph.Nodes.FirstOrDefault(each => each.Id == node) is not { } declared) return Refuse(NotNode);
        if (TakenBy(act) is { } owner && !(NodeKit.Of(declared.Kind) is IPressed own && own.Takes.Contains(act, StringComparer.Ordinal))) return Refuse(owner.Refusal);

        var now = (clock ?? (() => DateTimeOffset.UtcNow))();
        // Judged and kept in one hold of the tenures' lock: no hand-over lands between the holder the door read and the word's write.
        var tenures = new StepTenureFiles(records.Home, records.HoldsLease);
        var (refused, result) = tenures.Settling(bound.Unit, hold => records.Edit(bound.Unit, record =>
        {
            // The one step a run ended failed at has the person's stop, try again and done outside too, each of which governs the run again.
            var failedAt = record.Ended is { Outcome: RunOutcomes.Failed } failedRun && failedRun.Node == node && Settles.Contains(act) && by is null
                && WorkPerformer.StatusOf(record, node).State == NodeStates.Failed;
            if (record.Ended is { } ended && !failedAt) return (null, Refuse(Ended) with { Detail = ended.Quest });
            if (failedAt) record = record with { Ended = null };
            var standing = EarlyAccepts.Standing(record, node, by);
            if (SupervisedWords.Refused(standing, by) is { } held) return (null, Refuse(held));
            if (act == NodeWordActs.Stop && promoted?.Contains(node) == true) return (null, Refuse(Promoted));
            var status = WorkPerformer.StatusOf(record, node);
            if (by is not null && attempt != status.Attempt) return (null, Refuse(Stale) with { State = status.State });
            // A step not reached (an early Accept on the way to its review) has no attempt standing: the tenure at its last attempt is an ended one's.
            var holding = status.State is NodeStates.Blocked or NodeStates.Ready ? null : hold.Current(node, status.Attempt);
            var speaker = settle ?? SettleBy.Of(act, by, holder, epoch);
            if (SettleDoor.Own(declared, AnswersHold(declared, status, act), act, speaker, workers) is { } own) return (null, Refuse(own.Code) with { Detail = own.Detail, State = status.State });
            // The person's press on a step another holder holds takes it as the word is kept, and settles it: one press.
            var takes = StepHolding.TakesOnPress(holding, speaker);
            if (!takes && SettleDoor.Admit(holding, speaker, hold.Unread) is { } refusal)
            {
                if (refusal.Code == StepTenureFiles.Unread) tenures.SayUnread(bound.Unit, $"{act} on {node} of #{run}");
                return (null, Refuse(refusal.Code) with { Detail = refusal.Detail, State = status.State });
            }

            // Kept in the epoch it settles in (the press's take's, where it takes), which the pass reads again before it applies the word.
            (NodeRecord?, NodeWorded) Kept(NodeWord kept, Func<NodeWord, NodeRecord> edit, NodeWorded worded)
            {
                var held = holding;
                if (takes && (held = StepHolding.TakeOnPress(hold, records, run, holding!, act, now)) is null) return (null, Refuse(StepHolding.Unwritable));
                return (edit(kept with { Epoch = NodeWordEpoch.Of(held, speaker) }), worded);
            }

            var word = new NodeWord(node, act, words, now)
            {
                By = by, Attempt = attempt, Supersedes = SupervisedWords.Replaced(standing, by),
                Part = act == NodeWordActs.Changes && !string.IsNullOrWhiteSpace(part) ? part.Trim().TrimStart('#') : null,
                Revision = by is null ? null : revision?.Trim(), Outcome = outcome, Holder = holder,
                Decider = act == NodeWordActs.Decide && holder is null && SettleDoor.Holds(CastHolders.Agent, speaker.Who) ? speaker.Who : null,
            };
            if (act == NodeWordActs.Accept && by is null)
            {
                // On the way to its review the press is kept for it, on the record named; on an active one it is bound to the record
                // the review stands on, the one named or, unnamed, the one standing as it is kept.
                if (status.State is NodeStates.Blocked or NodeStates.Ready)
                {
                    var early = word with { Kept = true, Revision = revision?.Trim() };
                    return Kept(early, kept => record with { Pending = [.. record.Pending.Where(each => each.Node != node), kept] }, new NodeWorded(run, node, act, null) { Early = true });
                }

                if (status.State == NodeStates.Active)
                {
                    var (moved, at) = ReviewAccepts.AtKeep(records.Home, run, node, status, attempt, revision);
                    if (moved is not null) return (null, Refuse(moved) with { Detail = at, State = status.State });
                    word = word with { Attempt = status.Attempt, Revision = at };
                }
            }

            // A check not known is asked again on its attempt, as its held Run again: the table moves no unknown on the person's word.
            if (act == NodeWordActs.Again && UnknownChecks.Takes(declared, status))
            {
                var (again, notAgain) = UnknownChecks.Again(record, status, now);
                return again is null ? (null, Refuse(notAgain!) with { State = status.State }) : Kept(word, _ => again, new NodeWorded(run, node, act, null));
            }

            if (act == NodeWordActs.Again && PluginFailureHeld.Of(record, declared, status) is { Words.Count: > 0 }) return (null, Refuse(HeldAgain) with { State = status.State });
            if (act == NodeWordActs.DoneOutside)
            {
                if (!Outside(declared, status)) return (null, Refuse(NotOutside) with { State = status.State });
            }
            else if (Marked(act))
            {
                if (!Offers(declared, status, act)) return (null, Refuse(act == NodeWordActs.Send ? NotSendable : NotWaiting) with { State = status.State });
                if (act != NodeWordActs.Send && words is null) return (null, Refuse(NodeRefusal.NoWords));
            }
            else
            {
                if (Waited.Contains(act) && !Offers(declared, status, act)) return (null, Refuse(NotWaiting) with { State = status.State });
                if (Answer(declared, status, word) is { } unanswered) return (null, Refuse(unanswered) with { State = status.State });
                var judged = NodeLifecycle.Apply(declared, status, Move(declared, word, new NodeScope(digest, bound.Run), status));
                if (!judged.Moved) return (null, Refuse(judged.Refused!) with { State = status.State });
            }

            var replaced = (NodeWord each) => each.Node == node && !(NodeWordActs.Choices.Contains(act) && NodeWordActs.Choices.Contains(each.Act) && each.Words != words);
            return Kept(word, kept => record with { Pending = [.. record.Pending.Where(each => !replaced(each)), kept] }, new NodeWorded(run, node, act, null) { Detail = word.Revision });
        }));
        return refused is null ? result! : Refuse(refused);
    }

    /// <summary>
    /// <see cref="Word"/> for the doors the person presses through: a stop first reads the integration record through
    /// <paramref name="door"/> for the run's nodes whose open request's member promoted, so it is refused before anything is kept. A skip, a run
    /// that is not bound and a record with no request of an <c>integrate</c> of its own read nothing of it. The pass judges the word again.
    /// </summary>
    public static async Task<NodeWorded> WordAsync(
        NodeRecordFiles records, IIntegrationDoor door, string run, string node, string act, string? words, Func<DateTimeOffset>? clock = null,
        CancellationToken ct = default, int? attempt = null, string? revision = null, string? part = null, string? outcome = null, int? epoch = null,
        SettleBy? settle = null, IReadOnlyCollection<string>? workers = null)
    {
        IReadOnlySet<string>? promoted = null;
        if (act == NodeWordActs.Stop && new GraphRuns(records).Find(run.Trim().TrimStart('#')) is { } bound && records.Read(bound.Unit) is { } record)
        {
            promoted = await IntegrateNodes.PromotedAsync(door, record, ct).ConfigureAwait(false);
        }

        return Word(records, run, node, act, words, clock, promoted, attempt: attempt, revision: revision, part: part, outcome: outcome, epoch: epoch, settle: settle,
            workers: workers);
    }

    /// <summary>
    /// The person's words a run's record holds, applied through the one table under the run's declarations and its scope (the graph's digest and
    /// the run, as its starts are scoped), each kept with what it cleared; every word is taken off, applied or not, but an Accept kept for a
    /// review not begun yet (<see cref="EarlyAccepts"/>). Pure: the runtime writes it.
    /// </summary>
    /// <param name="tenures">The run's tenures, read in the lock the pass applies the words in; null reads none.</param>
    /// <returns>The record after them, null where none waited; and each word with why it is not applied, or null where it is.</returns>
    public static (NodeRecord? Next, IReadOnlyList<(NodeWord Word, string? Refused)> Applied) Apply(
        NodeRecord record, IReadOnlyDictionary<string, NodeDeclaration> declared, NodeScope scope, DateTimeOffset now, IReadOnlySet<string>? promoted = null,
        ILandingWords? landings = null, IReadOnlyDictionary<string, string>? present = null, StepTenureFiles.TenureHold? tenures = null)
    {
        if (record.Pending.Count == 0) return (null, []);
        var next = record;
        var applied = new List<(NodeWord, string?)>();
        var waiting = new List<NodeWord>();
        foreach (var word in record.Pending)
        {
            if (!declared.TryGetValue(word.Node, out var node))
            {
                applied.Add((word, NotNode));
                continue;
            }

            // A stop said before the member promoted, and applied after, is refused as the door refuses it.
            if (word.Act == NodeWordActs.Stop && promoted?.Contains(word.Node) == true)
            {
                applied.Add((word, Promoted));
                continue;
            }

            var standing = WorkPerformer.StatusOf(next, node.Id);
            if (NodeWordEpoch.Refused(word.Epoch, node.Id, standing.Attempt, tenures) is { } ended)
            {
                applied.Add((word, ended));
                continue;
            }

            if (ReviewAccepts.MovedFrom(word, standing, present))
            {
                applied.Add((word, ReviewMoved.Word));
                continue;
            }

            if (EarlyAccepts.Held(word, standing) is { } early)
            {
                if (early == EarlyAccepts.Waiting) waiting.Add(word);
                else applied.Add((word, early));
                continue;
            }

            if (SupervisedWords.Dropped(word, standing, landings) is { } dropped)
            {
                applied.Add((word, dropped));
                continue;
            }

            // The claim moves nothing: kept on the failed status, which the step's kind then observes (Outside).
            if (word.Act == NodeWordActs.DoneOutside)
            {
                var claimed = Outside(node, standing);
                if (claimed) next = next with { Statuses = new Dictionary<string, NodeStatus>(next.Statuses, StringComparer.Ordinal) { [node.Id] = standing with { Holds = word.Act } } };
                applied.Add((word, claimed ? null : NotOutside));
                continue;
            }

            if (Marked(word.Act))
            {
                if (!Offers(node, standing, word.Act))
                {
                    applied.Add((word, word.Act == NodeWordActs.Send ? NotSendable : NotWaiting));
                    continue;
                }

                var mark = word.Act == NodeWordActs.Send ? SendKey(node.Id, standing.Attempt)
                    : ReturnNode.ChoiceKey(node.Id, standing.Attempt, word.Act, word.Words ?? "", ReturnHolds.Read(standing.Holds).Waiting.GetValueOrDefault(word.Words ?? ""));
                next = next with { Fired = [.. next.Fired, mark] };
                applied.Add((word, null));
                continue;
            }

            if (Waited.Contains(word.Act) && !Offers(node, standing, word.Act))
            {
                applied.Add((word, NotWaiting));
                continue;
            }

            if (Answer(node, standing, word) is { } unanswered)
            {
                applied.Add((word, unanswered));
                continue;
            }

            var moved = NodeLifecycle.Apply(node, standing, Move(node, word, scope, standing));
            if (!moved.Moved)
            {
                applied.Add((word, moved.Refused));
                continue;
            }

            next = WorkPerformer.Kept(next, moved, word.Source + "-" + word.Act, now);
            // Not needed this run, or the work ended: the plugin's attempt in flight stands down, so the run waits on it no more.
            if (word.Act is NodeWordActs.Skip or NodeWordActs.Stop) next = StandDowns.Settle(next, node.Id, standing.Attempt, word.Act);
            if (word is { Act: NodeWordActs.Changes, Part: { } part }) next = next with { Fired = [.. next.Fired, ChangesParts.Key(node.Id, standing.Attempt, part)] };
            if (word.Act == NodeWordActs.Changes) next = next with { Repairs = [.. next.Repairs.Where(each => each.Node != node.Id)] };
            applied.Add((word, null));
        }

        // An Accept kept for a review not begun yet stays, and a pass that met nothing else writes nothing.
        return applied.Count == 0 ? (null, []) : (next with { Pending = waiting }, applied);
    }

    /// <summary>
    /// A word as the one table's move: a kind's own act the move its kind asks, the door having refused it on any other kind; else the
    /// person's skip with their words and the run's scope, or their stop.
    /// </summary>
    private static NodeMove Move(NodeDeclaration node, NodeWord word, NodeScope scope, NodeStatus status) =>
        TakenBy(word.Act) is { } owner && owner.Move(node, word, status, scope) is { } own ? own
        : word.Act == NodeWordActs.Skip ? new NodeMove(NodeStates.Skipped, NodeCause.Person) { Words = word.Words, Scope = scope }
        : word.Act == NodeWordActs.Again ? new NodeMove(NodeStates.Ready, NodeCause.Person)
        : new NodeMove(NodeStates.Stopped, word.GaveUp ? NodeCause.Daoris : NodeCause.Person) { Words = word.Words };


    /// <summary>
    /// What the run's view offers the person on a node standing at <paramref name="status"/>: its skip where it is ready or under way and its kind
    /// lets the person pass it over, and its stop wherever it is under way, about to be judged or failed. A node not reached yet is offered neither, though
    /// the door takes a word on it as the table does; a run that ended or does not read offers none, which its reader decides.
    /// </summary>
    /// <param name="promoted">
    /// Whether the integration record says this node's member promoted onto the line: its stop is then not offered,
    /// since nothing reverses the line, and the node reads its record's reach.
    /// </param>
    public static IReadOnlyList<string> Acts(NodeDeclaration node, NodeStatus status, bool promoted = false)
    {
        // A kind's own acts lead: a review's changes, a request's send, or its asking again and going on; a decision's decide once per outcome.
        var acts = NodeKit.Of(node.Kind) is IPressed pressed
            ? pressed.Acts(node, status).SelectMany(act => act == NodeWordActs.Decide ? TaskDecisions.Offered(node) : [act]).ToList()
            : [];
        if (status.State is NodeStates.Ready or NodeStates.Active && NodeAuthorities.Of(node)?.PersonSkips == true) acts.Add(NodeWordActs.Skip);
        if (status.State is NodeStates.Ready or NodeStates.Active or NodeStates.Unknown or NodeStates.Invalidated or NodeStates.Failed && !promoted) acts.Add(NodeWordActs.Stop);
        return acts;
    }

    /// <summary>The usage of the terminal's skip and stop, as it says them when their words are not understood.</summary>
    public const string Usage =
        "usage: daoris workflow skip <run> <node> \"…\"  ·  daoris workflow stop <run> <node> [\"…\"]\n"
        + "       daoris workflow send <run> <node>  (a ready request under `start: you`)\n"
        + "       daoris workflow ask-again <run> <node>  ·  daoris workflow go-on <run> <node> \"…\"  (a declined request)\n"
        + "       daoris workflow accept <run> <node> [--at <revision>]  ·  daoris workflow changes <run> <node> \"…\"  (a review: your Accept of the work, or what it should change)\n"
        + "       --at names the review record you read, and a review standing on another is refused; without it, the record standing when the\n"
        + "       word is kept is accepted, which proves nothing was read\n"
        + "       daoris workflow done <run> <node> [\"…\"]  ·  daoris workflow approve|reject <run> <node> [\"…\"]  (your task, with a note if you like)\n"
        + "       daoris workflow decide <run> <node> <outcome> [\"…\"]  (a decision's answer, with its reason; again until the run ends)\n"
        + "       daoris workflow again <run> <node>  ·  daoris workflow done-outside <run> <node>  (a failed step: a new attempt, or your word it\n"
        + "       was done outside Daoris where its kind reads the proof, a push's origin at the landed tip)\n"
        + "       pass a node of a run bound to a graph version over with your words, or stop it, your words if you have any; the run's\n"
        + "       id and its nodes are what `workflow run` prints. Kept on the run's record, and Daoris's next look applies it.";

    /// <summary>The sentence a door says for what the person's word came to: kept, or refused and why.</summary>
    public static string Said(NodeWorded done) => (done.Refused, done.Act) switch
    {
        (null, _) when done.Early =>
            $"workflow: your accept of `{done.Node}` on run {done.Run} is kept until its review begins, and applied there only where the work is as you read it.",
        (null, NodeWordActs.Accept) when done.Detail is { } at =>
            $"workflow: your accept of `{done.Node}` on run {done.Run} is kept on its record at revision {ReviewMoved.Short(at)}; Daoris's next look "
            + "applies it only where the work still reads so.",
        (null, _) => $"workflow: your {done.Act} of `{done.Node}` on run {done.Run} is kept; Daoris's next look applies it.",
        (ReviewMoved.Word, _) =>
            $"workflow: `{done.Node}` on run {done.Run} no longer stands on the record you named: it now reads revision {ReviewMoved.Short(done.Detail)}, "
            + "so read that and accept it, or say what it should change.",
        (NodeRecordFiles.NotHeld, _) =>
            "workflow: this process does not hold the home, so it keeps nothing: the desktop holds it while it runs, so say it there, or close the "
            + "desktop and run this again.",
        (NodeRecordFiles.Unreadable, _) => $"workflow: run {done.Run}'s node record does not read, so nothing on it is changed.",
        (NotRun, _) => $"workflow: no run `{done.Run}` here is bound to a graph version, so it has no node to pass over or stop.",
        (UnreadVersion, _) =>
            $"workflow: the graph version run {done.Run} is bound to does not read here ({done.Detail}), so the run is held whole and nothing on it is changed.",
        (NotNode, _) => $"workflow: run {done.Run}'s graph has no node `{done.Node}`; `workflow run` names its nodes.",
        (NotAct, _) =>
            $"workflow: `{done.Act}` is not a word for a run's node: say skip, stop, send, ask-again, go-on, accept, changes, done, approve, reject, decide, again or done-outside.",
        (NotOutside, _) =>
            $"workflow: `{done.Node}` on run {done.Run} is {done.State} now, or its kind reads no proof of work done outside Daoris, so there is nothing to claim: try it again or stop it.",
        (HeldAgain, _) =>
            $"workflow: `{done.Node}` on run {done.Run} holds its failed attempt for you, so it is tried again on that attempt: Try again on its step, or `daoris work held`.",
        (NotTask, _) => $"workflow: `{done.Node}` is no task of yours, and only your task is done, only an approval approved or rejected, and only a decision decided.",
        (NotOutcome, _) => $"workflow: a decision is answered with one of its outcomes, and `{done.Node}` on run {done.Run} declares no such outcome.",
        (NotWaiting, NodeWordActs.Decide) =>
            $"workflow: `{done.Node}` on run {done.Run} is {done.State} now and takes no proposal: a decision takes one while it waits or a proposal stands, and the owner's answer until the run ends.",
        (NotWaiting, _) =>
            $"workflow: `{done.Node}` on run {done.Run} is {done.State} now and waits for no {done.Act} of yours: a waiting task takes done, a waiting approval approve or reject.",
        (NotRequest, _) => $"workflow: `{done.Node}` is no request, and only a request is sent, and only a declined one asked again or gone on without.",
        (NotSendable, _) => $"workflow: `{done.Node}` on run {done.Run} is {done.State} now, or sends itself, so it has no send to wait for.",
        (NotReview, _) => $"workflow: `{done.Node}` is no review, and only a review is accepted or asked for changes.",
        (NodeRefusal.NoWords, NodeWordActs.Changes) =>
            $"workflow: asking for changes on `{done.Node}` sends your words to the work, and none were given: say what should change.",
        (NodeRefusal.NotInTable, NodeWordActs.Changes) =>
            $"workflow: `{done.Node}` on run {done.Run} is {done.State} now, so it waits on no Accept to ask changes of.",
        (NodeRefusal.NotInTable, NodeWordActs.Accept) => $"workflow: `{done.Node}` on run {done.Run} is {done.State} now, so it waits on no Accept of yours.",
        (Ended, _) => $"workflow: run {done.Run} ended at quest #{done.Detail}, so nothing of its graph moves again until its chain comes back here.",
        (Worded, _) => $"workflow: `{done.Node}` on run {done.Run} already has your word, which Daoris's next look applies.",
        (SettleDoor.NotHolder, _) or (StepTenureFiles.EndedEpoch, _) =>
            $"workflow: `{done.Node}` on run {done.Run} is {done.Detail}, so only its holder settles it: take it first (`daoris workflow take {done.Run} {done.Node}`) to act on it.",
        (SettleDoor.Marked, _) => $"workflow: `{done.Node}` on run {done.Run} is {done.Detail}: only the owner role settles it.",
        (SettleDoor.OwnWork, _) =>
            $"workflow: `{done.Node}` on run {done.Run} holds the run's own work, and its maker never answers its own gate: {done.Detail}; "
            + "another holder of the step's role answers it.",
        (StepTenureFiles.Unread, _) => $"workflow: `{done.Node}` on run {done.Run} was not settled: {done.Detail}, so only your own word is taken until they are mended.",
        (Promoted, _) => $"workflow: `{done.Node}` on run {done.Run} has promoted onto the line, and nothing reverses the line, so there is nothing to stop: its record reads its reach.",
        (NodeRefusal.NoWords, _) => $"workflow: passing `{done.Node}` over keeps your words with it, and none were given: say why.",
        (NodeRefusal.NotSkippable, _) =>
            $"workflow: `{done.Node}` on run {done.Run} is not passed over, since a skip would read as its proof: if it was done outside Daoris, say so "
            + "with its link (Done outside Daoris on its step, or `daoris work held <unit> <node> <attempt> done-outside <link> [\"…\"]`), and the run "
            + "goes on; ending this work (`daoris workflow stop`) ends the run and leaves its quest to close by your word.",
        (NodeRefusal.NotInTable, NodeWordActs.Skip) =>
            $"workflow: `{done.Node}` on run {done.Run} is {done.State} now, so there is nothing to pass over.",
        (NodeRefusal.NotInTable, NodeWordActs.Again) => $"workflow: `{done.Node}` on run {done.Run} is {done.State} now, not failed, so there is nothing to try again.",
        (NodeRefusal.NotInTable, _) => $"workflow: `{done.Node}` on run {done.Run} is {done.State} now, so there is nothing to stop.",
        (NodeRefusal.NotCause, _) =>
            $"workflow: `{done.Node}` on run {done.Run} is {done.State} now, and only Daoris moves it from there, not your {done.Act}: "
            + "`daoris workflow run` says what it waits for and what you may say of it.",
        (var code, _) => $"workflow: your {done.Act} of `{done.Node}` on run {done.Run} was not kept ({code}).",
    };

    /// <summary>What the pass says of a word it applies, or why one is not applied, on the run's unit.</summary>
    public static string AppliedSaid(string unit, NodeWord word, string? refused) => (word.By is null, refused) switch
    {
        (true, null) => $"work  {unit}: your {word.Act} of `{word.Node}` is applied{(word.Words is null ? "" : ", with your words")}.",
        (false, null) => $"work  {unit}: the supervisor's {word.Act} of `{word.Node}` is applied.",
        (false, _) => $"work  {unit}: the supervisor's {word.Act} of `{word.Node}` was not applied ({refused}): the step waits for you.",
        _ => $"work  {unit}: your {word.Act} of `{word.Node}` was not applied ({refused}): it moved before Daoris's look came, so say again what it should do.",
    };
}
