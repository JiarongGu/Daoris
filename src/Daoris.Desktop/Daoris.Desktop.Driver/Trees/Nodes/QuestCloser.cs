using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>One quest a close names (FABRIC-CLOSE1b), with the submission it judged: what the close door checks is still current.</summary>
public sealed record QuestCloseItem(string Id, string Machine, long Sequence);

/// <summary>A <c>quest.close</c> attempt's close as its record keeps it (§E5 step 1): the token, the quests named, and a refusal's words.</summary>
public sealed record QuestCloseCall(string Token, IReadOnlyList<QuestCloseItem> Quests)
{
    /// <summary>The door's words when it refused the close, kept so the refusal is said as it came; null otherwise.</summary>
    public string? Words { get; init; }

    public bool Equals(QuestCloseCall? other) =>
        other is not null && Token == other.Token && Words == other.Words && Quests.SequenceEqual(other.Quests);

    public override int GetHashCode() => HashCode.Combine(Token, Quests.Count, Words);
}

/// <summary>What the close door answered (<see cref="QuestCloseAnswer.Outcome"/>).</summary>
public static class QuestCloseOutcomes
{
    /// <summary>Every quest named is Done by this close: made now, or made already under the same key.</summary>
    public const string Closed = "closed";

    /// <summary>A check refused it, now or when it was first sent under this key, or the key went with another close: nothing was written.</summary>
    public const string Refused = "refused";

    /// <summary>The host has no close door: it is older than this driver.</summary>
    public const string NoDoor = "no-door";
}

/// <summary>The close door's answer (FABRIC-CLOSE1a's <c>POST /api/quests/{id}/close</c>), as the driver reads it.</summary>
/// <param name="Outcome">One of <see cref="QuestCloseOutcomes"/>.</param>
/// <param name="Code">A refusal's check, as the door names it (<c>stale</c>, <c>held</c>, <c>key-reused</c>, …); null otherwise.</param>
/// <param name="Message">The door's sentence, verbatim.</param>
/// <param name="Quests">The quests named, as they now stand, where the door answered them.</param>
public sealed record QuestCloseAnswer(string Outcome, string? Code, string Message, IReadOnlyList<QuestView> Quests);

/// <summary>The close door, as the closer calls it (FABRIC-CLOSE1b): a port, so the fence is proved without a host.</summary>
public interface IQuestClosePort
{
    /// <exception cref="HttpRequestException">The host did not answer: the close's outcome is unknown, and the same close is sent again.</exception>
    Task<QuestCloseAnswer> CloseQuestsAsync(string quest, string key, string token, IReadOnlyList<QuestCloseItem> quests, CancellationToken ct);
}

/// <summary>What a close node may do now (<see cref="QuestClosing.Judge"/>).</summary>
/// <param name="Named">The quests it closes, its chain's in this repository oldest first, each with its current submission; empty when it waits.</param>
/// <param name="Waits">Why nothing is sent now, or null.</param>
/// <param name="Overridden">Every quest it would close is Done already: by its chain's close, or by the person (§E5's override).</param>
public sealed record QuestCloseJudged(IReadOnlyList<QuestCloseItem> Named, string? Waits, bool Overridden);

/// <summary>
/// The judgements a <c>quest.close</c> node rests on (FABRIC-CLOSE1b; the fabric events design §E5): whether a graph closes, the close token,
/// and which quests a close names. Pure.
/// </summary>
/// <remarks>
/// <b>The chain closes at its last quest here</b> (D173's CLOSE1b note, choice 2): a quest whose own steps still name this repository waits,
/// and the last one's close names every quest of the chain in this repository that is not Done yet, oldest first, in one call (§E5: all of
/// them or none). A quest the person closed is left as it is.
/// </remarks>
public static class QuestClosing
{
    /// <summary>The sender an attempt of Daoris's own close names: never a plugin's, so no plugin's frame is taken about it.</summary>
    public const string Sender = NodeKinds.QuestClose;

    /// <summary>The history entry a graph ended by the person's own done keeps: what says it once.</summary>
    public const string Overridden = "overridden";

    /// <summary>The history entry a close found closed only in part keeps, with the quest it names: what says it once.</summary>
    public const string Partial = "partially-closed";

    private const string Done = "Done";

    private const string Taken = "Taken";

    /// <summary>Whether a graph holds <c>quest.close</c>: a closure graph (§E5).</summary>
    public static bool Holds(NodeGraph? graph) => graph?.Nodes.Any(node => node.Kind == NodeKinds.QuestClose) == true;

    /// <summary>The close token (§E5 step 1): the unit, its revision, the graph's digest and the prerequisite acceptances' ids.</summary>
    public static string Token(string unit, string? revision, string graphDigest, IReadOnlyList<string> receipts)
    {
        var text = new StringBuilder("quest-close\n").Append(unit).Append('\n').Append(revision ?? "").Append('\n').Append(graphDigest).Append('\n');
        foreach (var receipt in receipts.Order(StringComparer.Ordinal)) text.Append(receipt).Append('\n');
        return "ctk-" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString())))[..32];
    }

    /// <summary>Which quests the close of <paramref name="quest"/>'s unit names now, or why it waits, or that every one is Done already.</summary>
    public static QuestCloseJudged Judge(QuestView quest, IReadOnlyDictionary<string, QuestView> quests)
    {
        var chain = Chain(quest, quests);
        var open = chain.Where(each => each.Status != Done).ToList();
        if (open.Count == 0) return new([], null, true);
        if (quest.Status != Done && quest.Then.Any(step => string.Equals(step.To, quest.To, StringComparison.Ordinal)))
        {
            return new([], $"#{quest.Id}'s chain has a step in this repository still to come, and closes at its last", false);
        }

        foreach (var each in open)
        {
            if (each.Status != Taken) return new([], $"#{each.Id} is {each.Status}", false);
            if (each.Submission is null) return new([], $"#{each.Id} has no submission yet: its taker says `ready` when its work is done", false);
            if (each.Held) return new([], $"#{each.Id}'s submission is held ({each.Hold ?? "for the person"})", false);
        }

        return new([.. open.Select(each => new QuestCloseItem(each.Id, each.Submission!.Machine, each.Submission.Sequence))], null, false);
    }

    /// <summary>The quest and each quest it grew from in this repository under the same graph, oldest first.</summary>
    private static List<QuestView> Chain(QuestView quest, IReadOnlyDictionary<string, QuestView> quests)
    {
        var chain = new List<QuestView> { quest };
        var seen = new HashSet<string>(StringComparer.Ordinal) { quest.Id };
        for (var at = quest; at.Parent is { } parent && quests.TryGetValue(parent, out var before)
             && string.Equals(before.To, quest.To, StringComparison.Ordinal) && string.Equals(before.Closure, quest.Closure, StringComparison.Ordinal)
             && seen.Add(before.Id); at = before)
        {
            chain.Insert(0, before);
        }

        return chain;
    }

    /// <summary>The close attempts kept on <paramref name="records"/> that name <paramref name="quest"/>.</summary>
    public static IEnumerable<NodeAttempt> Naming(IEnumerable<NodeRecord> records, string quest) =>
        records.SelectMany(record => record.Attempts)
            .Where(attempt => attempt.Close?.Quests.Any(named => string.Equals(named.Id, quest, StringComparison.Ordinal)) == true);
}

/// <summary>
/// Daoris's own <c>quest.close</c> (FABRIC-CLOSE1b; the fabric events design §E5's fence): the attempt written with its key and token before
/// the close is sent, the door's answer kept on it, and a timeout or a restart sending the same close again.
/// </summary>
/// <remarks>
/// <para><b>The fence</b> (§E5, D173's CLOSE1b note, choice 1): step 1 is one locked edit that judges the start on the record as the evaluation
/// left it and writes the attempt with its key and token; step 2 is the door's one transaction; step 3 is one locked edit that keeps the answer
/// on that attempt, only while it still waits for it. Nothing is awaited inside an edit (PERFORM1h). What holds the driver's facts still between
/// steps 1 and 3 is the lease's one writer and the pass's one-at-a-time: no other writer edits the unit's record meanwhile.</para>
/// <para><b>Step 4:</b> an unanswered close is unknown, and the next pass sends the same close again, its key, token and quests unchanged: the
/// door answers its first outcome, a refusal included, and writes nothing twice.</para>
/// </remarks>
public sealed class QuestCloser(NodeRecordFiles records, IQuestClosePort port, Func<DateTimeOffset> clock)
{
    /// <summary>A refused close is never a transient failure: a new close is the person's, under a new key.</summary>
    public const string Refused = "refused";

    /// <summary>Why a close attempt is held: its host has no close door.</summary>
    public const string NoDoor = QuestCloseOutcomes.NoDoor;

    private readonly List<string> _said = [];

    /// <summary>What the closer said since it was last asked: each refusal in the door's words.</summary>
    public IReadOnlyList<string> TakeSaid()
    {
        var said = _said.ToList();
        _said.Clear();
        return said;
    }

    /// <summary>The fence's steps 1 to 3 on the start the evaluation proposed: the attempt written first, then the close sent, then its answer kept.</summary>
    public async Task<WorkPerformed> StartAsync(NodeDeclaration node, WireStart start, NodeStatus evaluated, IReadOnlyList<QuestCloseItem> named, CancellationToken ct)
    {
        if (start.Move.Binding is not { } binding) return new("", start.Node, PerformedWhat.Refused, NodeRefusal.NoBinding);
        var unit = binding.Unit;
        if (node.Kind != NodeKinds.QuestClose || named.Count == 0) return new(unit, node.Id, PerformedWhat.Refused, WorkPerformer.NotDeclared);
        var now = clock();
        var (refused, started) = records.Edit<(NodeAttempt? Attempt, string? Refused)>(unit, record =>
        {
            var held = WorkPerformer.StatusOf(record, node.Id);
            if (!WorkPerformer.Same(held, evaluated)) return (null, (null, WorkPerformer.Moved));
            var moved = NodeLifecycle.Apply(node, held, start.Move);
            if (!moved.Moved) return (null, (null, moved.Refused));
            var call = new WorkPerformCall(unit, node.Kind, node.Version, WorkPerformer.Random("i-", 12), WorkPerformer.Random("k-", 16), binding.SubjectRevision ?? "",
                [.. binding.Inputs.OrderBy(input => input.Key, StringComparer.Ordinal).Select(input => input.Value.Ref)], now + WorkPerformer.Timeout(node));
            var attempt = new NodeAttempt(node.Id, moved.Status.Attempt, QuestClosing.Sender, call, AttemptStates.Recorded)
            {
                RecordedAt = now,
                Close = new QuestCloseCall(QuestClosing.Token(unit, binding.SubjectRevision, binding.GraphDigest, binding.PrerequisiteReceipts), [.. named]),
            };
            var fired = record.Fired.Concat(start.Firings.Select(firing => firing.Key)).Distinct(StringComparer.Ordinal).ToList();
            return (WorkPerformer.Kept(record, moved, "start", now) with { Attempts = [.. record.Attempts, attempt], Fired = fired }, (attempt, null));
        });
        if (refused is not null) return new(unit, node.Id, PerformedWhat.Refused, refused);
        if (started.Attempt is not { } recorded) return new(unit, node.Id, PerformedWhat.Refused, started.Refused);
        return await SendAsync(unit, recorded, node, PerformedWhat.Sent, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Step 4: every close attempt whose outcome is not established (written and never answered, or unanswered), sent again with the same key,
    /// token and quests, each taking one of <paramref name="sends"/>. Asked whatever graph governs its unit now: a close in flight is reconciled.
    /// </summary>
    public async Task<IReadOnlyList<WorkPerformed>> ReconcileAsync(WorkSends sends, CancellationToken ct)
    {
        var done = new List<WorkPerformed>();
        foreach (var record in records.All().Records)
        {
            foreach (var attempt in record.Attempts.Where(Waiting).ToList())
            {
                if (!sends.Take()) return done;
                var node = new NodeDeclaration(attempt.Node, NodeKinds.QuestClose, attempt.Call.StepVersion, NodeReach.Performed);
                done.Add(await SendAsync(record.Unit, attempt, node, PerformedWhat.Redelivered, ct).ConfigureAwait(false));
            }
        }

        return done;
    }

    /// <summary>
    /// The chain's close reached on a quest it closed (D173's CLOSE1b note, choice 2): a chain quest's own close node, proposed to start once its
    /// quest is Done by the close its chain's last quest made, is started and reached in one locked edit on that close's key, never sent.
    /// </summary>
    public WorkPerformed ReachByChain(NodeDeclaration node, WireStart start, NodeStatus evaluated, NodeAttempt chained)
    {
        if (start.Move.Binding is not { } binding) return new("", start.Node, PerformedWhat.Refused, NodeRefusal.NoBinding);
        var unit = binding.Unit;
        var now = clock();
        var (refused, result) = records.Edit(unit, record =>
        {
            var held = WorkPerformer.StatusOf(record, node.Id);
            if (!WorkPerformer.Same(held, evaluated)) return (null, new WorkPerformed(unit, node.Id, PerformedWhat.Refused, WorkPerformer.Moved));
            var begun = NodeLifecycle.Apply(node, held, start.Move);
            if (!begun.Moved) return (null, new WorkPerformed(unit, node.Id, PerformedWhat.Refused, begun.Refused));
            var reached = NodeLifecycle.Apply(node, begun.Status, Reach(node, begun.Status, chained.Call.IdempotencyKey));
            if (!reached.Moved) return (null, new WorkPerformed(unit, node.Id, PerformedWhat.Refused, reached.Refused));
            var fired = record.Fired.Concat(start.Firings.Select(firing => firing.Key)).Distinct(StringComparer.Ordinal).ToList();
            return (WorkPerformer.Kept(WorkPerformer.Kept(record, begun, "start", now), reached, "chain", now) with { Fired = fired },
                new WorkPerformed(unit, node.Id, PerformedWhat.Applied, WorkStates.Reached));
        });
        return refused is null ? result! : new(unit, node.Id, PerformedWhat.Refused, refused);
    }

    /// <summary>Whether a close attempt waits for its door's answer: written and never answered, or unanswered.</summary>
    private static bool Waiting(NodeAttempt attempt) =>
        attempt.Plugin == QuestClosing.Sender && attempt.Close is not null && attempt.State is AttemptStates.Recorded or AttemptStates.Unknown;

    /// <summary>
    /// One send of a close, its key, token and quests as written (step 2), and its answer kept on its attempt after the await (step 3), only
    /// while that attempt still waits for it. Unanswered, the attempt and its node are unknown until it is sent again.
    /// </summary>
    private async Task<WorkPerformed> SendAsync(string unit, NodeAttempt attempt, NodeDeclaration node, string what, CancellationToken ct)
    {
        var close = attempt.Close!;
        var invocation = attempt.Call.Invocation;
        QuestCloseAnswer answer;
        try
        {
            answer = await port.CloseQuestsAsync(close.Quests[^1].Id, attempt.Call.IdempotencyKey, close.Token, close.Quests, ct).ConfigureAwait(false);
        }
        catch (Exception error) when (Unanswered(error, ct))
        {
            var now = clock();
            var (unsent, _) = records.Edit<bool>(unit, record =>
            {
                if (record.Invoked(invocation) is not { State: AttemptStates.Recorded or AttemptStates.Unknown } current) return (null, false);
                var status = WorkPerformer.StatusOf(record, node.Id);
                if (status.State == NodeStates.Active && status.Attempt == current.Attempt)
                {
                    record = WorkPerformer.Kept(record, NodeLifecycle.Apply(node, status, new NodeMove(NodeStates.Unknown, NodeCause.Daoris) { Attempt = current.Attempt }), "unknown", now);
                }

                return (WorkPerformer.With(record, current, current with { State = AttemptStates.Unknown }), true);
            });
            return new(unit, node.Id, PerformedWhat.Unknown, unsent ?? PerformedWhat.Unanswered);
        }

        var (refused, result) = records.Edit(unit, record => record.Invoked(invocation) is { State: AttemptStates.Recorded or AttemptStates.Unknown } current
            ? Outcome(record, current, node, answer)
            : (null, new WorkPerformed(unit, node.Id, PerformedWhat.NotTaken, WorkPerformer.Moved)));
        if (refused is not null) return new(unit, node.Id, PerformedWhat.Unknown, refused);
        if (result is { What: PerformedWhat.Applied, Code: WorkStates.Failed } || answer.Outcome == QuestCloseOutcomes.NoDoor)
        {
            _said.Add($"work  {unit}: its close was not made ({answer.Code ?? answer.Outcome}): {answer.Message}");
        }

        return result!.What == PerformedWhat.Applied && what == PerformedWhat.Redelivered ? result with { What = PerformedWhat.Applied } : result;
    }

    /// <summary>
    /// The door's answer applied to its attempt's node through the one table: closed is the reach, on the close's key as its evidence; refused
    /// is the failure, with the door's code and its words kept; no door holds the attempt for the person. An attempt its node has moved off is
    /// settled as a stale one's, lifting the gate it held (§D3).
    /// </summary>
    private (NodeRecord? Next, WorkPerformed Result) Outcome(NodeRecord record, NodeAttempt attempt, NodeDeclaration node, QuestCloseAnswer answer)
    {
        var unit = record.Unit;
        var now = clock();
        var delivered = attempt with { Deliveries = attempt.Deliveries + 1 };
        if (answer.Outcome == QuestCloseOutcomes.NoDoor) return WorkPerformer.Hold(record, delivered, NoDoor);
        var closed = answer.Outcome == QuestCloseOutcomes.Closed;
        var code = closed ? null : answer.Code ?? Refused;
        var settled = delivered with
        {
            State = AttemptStates.Settled, Outcome = closed ? WorkStates.Reached : WorkStates.Failed, Code = code,
            Close = closed ? attempt.Close : attempt.Close! with { Words = answer.Message },
        };
        var status = WorkPerformer.StatusOf(record, node.Id);
        if (status.Attempt != attempt.Attempt || status.State is not (NodeStates.Active or NodeStates.Unknown))
        {
            var stale = WorkPerformer.With(record, attempt, settled);
            return (stale with { Statuses = WorkPerformer.With(stale.Statuses, NodeLifecycle.Reconciled(status, attempt.Attempt)) }, new(unit, node.Id, PerformedWhat.Stale));
        }

        var move = closed
            ? Reach(node, status, attempt.Call.IdempotencyKey)
            : new NodeMove(NodeStates.Failed, NodeCause.Authority) { Source = NodeSource.CloseDoor, Attempt = attempt.Attempt, Code = code };
        var moved = NodeLifecycle.Apply(node, status, move);
        if (!moved.Moved) return WorkPerformer.Hold(record, delivered with { Outcome = settled.Outcome }, moved.Refused!);
        return (WorkPerformer.With(WorkPerformer.Kept(record, moved, settled.Outcome!, now), attempt, settled),
            new(unit, node.Id, PerformedWhat.Applied, settled.Outcome));
    }

    /// <summary>The close's reach (§E2: the close door's write is its authority): its binding, with the close's key as its evidence.</summary>
    private static NodeMove Reach(NodeDeclaration node, NodeStatus status, string key) => new(NodeStates.Reached, NodeCause.Authority)
    {
        Source = NodeSource.CloseDoor,
        Attempt = status.Attempt,
        Acceptance = status.Binding is { } binding
            ? binding with { Evidence = new NodePort(node.Evidence ?? node.Kind, "close:" + key, binding.SubjectRevision, null) }
            : null,
    };

    /// <summary>A failure of the call itself, not the loop ending: the close's outcome is unknown, and it is sent again, never made anew.</summary>
    private static bool Unanswered(Exception error, CancellationToken ct) =>
        !ct.IsCancellationRequested
        && error is HttpRequestException or IOException or TimeoutException or TaskCanceledException or DriverException or JsonException;
}

/// <summary>Whether a quest was bound to its closure graph at its take, and what is said (<see cref="ClosureBinding.AtTakeAsync"/>).</summary>
public sealed record ClosureBound(bool Bound, string Said);

/// <summary>The service's two doors a bind reads and writes (FABRIC-CLOSE1b): the handshake, and the bind itself. A port, so a bind is proved without a host.</summary>
public interface IClosureBindPort
{
    /// <summary>Whether the local host knows a closure graph's operations (<c>bound</c>, <c>submitted</c>) and doors; false from a host before them.</summary>
    Task<bool> KnowsClosureAsync(CancellationToken ct);

    Task<(bool Ok, string Message)> BindQuestAsync(string id, string graph, CancellationToken ct);
}

/// <summary>
/// The bind at the take (FABRIC-CLOSE1b; §E5's cutover): a quest whose work cycle holds <c>quest.close</c> is bound to that graph when its
/// session opens, before the session can say <c>done</c>, and only where every machine of its circle and the host run a build that knows a
/// bound quest (FABRIC-CLOSE1e's compatibility).
/// </summary>
/// <remarks>
/// D173's CLOSE1b note, choice 3: no host or machine says yet which builds the other machines of a circle run, so a circle synced through a
/// remote is never bound, and its quests close by their done as today; the local host is asked (<c>/api/status</c>'s <c>closure</c>).
/// </remarks>
public static class ClosureBinding
{
    /// <summary>The bind for a quest just taken, or null where no closure graph governs it: nothing is asked, nothing changes.</summary>
    public static async Task<ClosureBound?> AtTakeAsync(
        IClosureBindPort port, DriverConfig config, QuestView quest, string workspace, bool remote, CancellationToken ct)
    {
        var home = NodeGraphHomes.Work(config, quest.To, workspace, WorkUnitKind.Quest);
        if (!QuestClosing.Holds(home.Graph)) return null;
        const string AsToday = "so it closes by its done, as today.";
        if (!home.Readable)
        {
            return new(false, $"#{quest.Id} is not bound to its closure graph, which does not hold ({home.Problems[0].Code}), {AsToday}");
        }

        var digest = NodeGraphs.Digest(home.Graph!);
        if (remote)
        {
            return new(false, $"#{quest.Id} is not bound to its closure graph `{digest}`: its workspace `{workspace}` syncs through a remote, and "
                              + $"Daoris cannot yet tell that the remote and every machine sharing it run a build that knows a bound quest, {AsToday}");
        }

        bool knows;
        try
        {
            knows = await port.KnowsClosureAsync(ct).ConfigureAwait(false);
        }
        catch (Exception error) when (error is DriverException or HttpRequestException or JsonException or IOException)
        {
            knows = false;
        }

        if (!knows)
        {
            return new(false, $"#{quest.Id} is not bound to its closure graph `{digest}`: the service here does not know a bound quest (it is older "
                              + $"than this driver), {AsToday}");
        }

        var (ok, message) = await port.BindQuestAsync(quest.Id, digest, ct).ConfigureAwait(false);
        return ok
            ? new(true, $"#{quest.Id} closes through its graph `{digest}`: its session says `ready`, and the graph writes its Done at the work's end.")
            : new(false, $"#{quest.Id} is not bound to its closure graph `{digest}`: {message} It closes by its done, as today.");
    }
}
