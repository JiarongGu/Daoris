using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;

namespace Daoris.Driver;

/// <summary>
/// What an <c>integrate</c> node's options say (INTEGRATE1n; the integration design §17.4): the unit's half of the install's policy, the
/// <c>request</c> it asks with and the <c>conflicts</c> its line's run takes it to. A node naming none, or a value that is no choice,
/// reads the design's <i>Absent</i> column, as the rule's key does.
/// </summary>
public static class IntegrateNodes
{
    /// <summary>The sender an attempt of Daoris's own integrate request names: never a plugin's, so no plugin's frame is taken about it.</summary>
    public const string Sender = NodeKinds.Integrate;

    /// <summary>The <c>request</c> the node asks with: the owner's where it names none or a value that is no choice.</summary>
    public static string RequestOf(NodeDeclaration node) =>
        NodeOptions.Text(node, "request") is { } request && LandingIntegrate.Requests.Contains(request) ? request : LandingIntegrate.Owner;

    /// <summary>The <c>conflicts</c> the node's unit takes: the member's session where it names none or a value that is no choice.</summary>
    public static string ConflictsOf(NodeDeclaration node) =>
        NodeOptions.Text(node, "conflicts") is { } conflicts && LandingIntegrate.ConflictChoices.Contains(conflicts) ? conflicts : LandingIntegrate.Session;

    /// <summary>
    /// The key a unit's request at a landing's tip is found by (§17.4: <i>keyed by the unit and that tip</i>): the same for every start of
    /// that unit at that tip, so a restart finds the request it made and never makes two.
    /// </summary>
    public static string Key(string unit, string tip) =>
        "ik-" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes($"integrate\n{unit}\n{tip.ToLowerInvariant()}\n")))[..32];

    /// <summary>Whether an attempt is one of the node's own that its record has not finished with: not answered, or held unreconciled by its node.</summary>
    internal static bool Waiting(NodeRecord record, NodeAttempt attempt) =>
        attempt.Plugin == Sender
        && (attempt.State is AttemptStates.Recorded or AttemptStates.Unknown
            || record.Statuses.GetValueOrDefault(attempt.Node) is { } status && status.Unreconciled == attempt.Attempt);

    /// <summary>
    /// The nodes of <paramref name="record"/> whose open request the integration record says promoted (§17.4, F50): their stop is refused,
    /// since nothing reverses the line, and the node reads its record's reach. A request that could not be read is not counted, so nothing is
    /// refused on a guess; it is judged again at the next ask.
    /// </summary>
    public static async Task<IReadOnlySet<string>> PromotedAsync(IIntegrationDoor door, NodeRecord record, CancellationToken ct)
    {
        var promoted = new HashSet<string>(StringComparer.Ordinal);
        foreach (var attempt in record.Attempts.Where(attempt => Waiting(record, attempt)))
        {
            try
            {
                if (await door.FindAsync(record.Unit, attempt.Call.SubjectRevision, ct).ConfigureAwait(false) is { ClosedAt: null } request
                    && await door.PromotedAsync(request, ct).ConfigureAwait(false))
                {
                    promoted.Add(attempt.Node);
                }
            }
            catch (Exception error) when (!ct.IsCancellationRequested && error is IOException or SqliteException or TimeoutException or DriverException)
            {
                // Not read: judged again at the next ask.
            }
        }

        return promoted;
    }
}

/// <summary>
/// What the <c>integrate</c> node asks of row e's one door (INTEGRATE1n part n2): the request kept for a unit at a tip, open or closed; the
/// ask for a new one; and, of the member the line's run formed from it, whether it promoted. A port, so a node is judged over a stand-in;
/// <see cref="HomeIntegrationDoor"/> is the home's.
/// </summary>
public interface IIntegrationDoor
{
    /// <summary>The request kept for <paramref name="unit"/> at <paramref name="tip"/>, open or closed; null where none was made.</summary>
    Task<IntegrationRequest?> FindAsync(string unit, string tip, CancellationToken ct);

    /// <summary>The ask for a request, answered by the door's rules as they stand under <paramref name="config"/>.</summary>
    Task<IntegrationAdmission> AskAsync(IntegrationAsk ask, DriverConfig config, CancellationToken ct);

    /// <summary>Whether the request's member promoted onto the line, which no stop reverses (§17.4, F50).</summary>
    Task<bool> PromotedAsync(IntegrationRequest request, CancellationToken ct);

    /// <summary>Drop a request standing in no candidate, as the person's stop does; whether it was dropped.</summary>
    Task<bool> DropAsync(IntegrationRequest request, CancellationToken ct);
}

/// <summary>The door over a home's own records and its integration store, opened for each use as the store is.</summary>
public sealed class HomeIntegrationDoor(string home) : IIntegrationDoor
{
    public Task<IntegrationRequest?> FindAsync(string unit, string tip, CancellationToken ct) =>
        Task.FromResult(new IntegrationStore(home).FindByKey(unit, tip));

    public Task<IntegrationAdmission> AskAsync(IntegrationAsk ask, DriverConfig config, CancellationToken ct) =>
        new IntegrationRequests(new HomeIntegrationRecords(home), new IntegrationStore(home), () => config).AdmitAsync(ask, ct);

    public Task<bool> PromotedAsync(IntegrationRequest request, CancellationToken ct) =>
        Task.FromResult(new IntegrationStore(home).MemberOf(request.Id) is { State: IntegrationMemberStates.Promoted or IntegrationMemberStates.Settled });

    public Task<bool> DropAsync(IntegrationRequest request, CancellationToken ct)
    {
        try
        {
            return Task.FromResult(new IntegrationStore(home).Close(request.Id, IntegrationRequestCloses.Dropped));
        }
        catch (IntegrationRefusedException)
        {
            // It stands in a candidate, which closes it with its own step under its claim (row g); nothing is dropped here.
            return Task.FromResult(false);
        }
    }
}

/// <summary>
/// Daoris's own <c>integrate</c> (INTEGRATE1n part n2; §17.4): a waited node whose start asks row e's one door for its landed branch, keyed by
/// the unit and the landing's tip, and whose state is read from the integration record, never copied. Nothing here runs the integration: an
/// admitted request's line is sent to the transport (<see cref="Lines"/>, INTEGRATE1g), which runs it off the pass.
/// </summary>
/// <remarks>
/// <para><b>The fence</b> mirrors the close's (<see cref="QuestCloser"/>): step 1 is one locked edit that judges the start on the record as the
/// evaluation left it and writes the attempt, its key and the landing's tip; step 2 asks the door, which finds a request already made for the
/// key before it makes one; step 3 keeps the answer on that attempt, only while it still waits for it. Nothing is awaited inside an edit.</para>
/// <para><b>The attempt stays unreconciled until the record says how its request ended</b> (<see cref="NodeStatus.Unreconciled"/>, F41): an
/// invalidation by a new landing tip keeps it, no new attempt starts while one is, so the second revision's request is made once the first is
/// promoted, handed back or dropped. Each pass asks the door what became of every such request before any new start, and a request the door
/// never took (a crash between steps 1 and 2) is withdrawn, its node judged again on what is there now.</para>
/// <para><b>Settled here means the door has the request</b>, not that the integration ended: the node's own state, through the one table, is the
/// request's (active while it is open, reached once settled, failed once handed back or dropped).</para>
/// </remarks>
public sealed class IntegrateNode(NodeRecordFiles records, Func<IIntegrationDoor?> door, Func<DateTimeOffset> clock)
{
    private readonly List<string> _said = [];

    /// <summary>The transport an open request's line is sent to, at each look that reads it open (INTEGRATE1g); none leaves it queued.</summary>
    public Func<IIntegrationLines?> Lines { get; init; } = () => null;

    /// <summary>What the node said since it was last asked: each refusal and each outcome, in words.</summary>
    public IReadOnlyList<string> TakeSaid()
    {
        var said = _said.ToList();
        _said.Clear();
        return said;
    }

    /// <summary>
    /// The fence's steps 1 to 3 on the start the evaluation proposed, <paramref name="ask"/> naming the unit, the landing's branch and the tip it
    /// froze. The attempt is written first, then the door asked, then its answer kept.
    /// </summary>
    public async Task<WorkPerformed> StartAsync(NodeDeclaration node, WireStart start, NodeStatus evaluated, IntegrationAsk ask, DriverConfig config, CancellationToken ct)
    {
        if (start.Move.Binding is not { } binding) return new("", start.Node, PerformedWhat.Refused, NodeRefusal.NoBinding);
        var unit = binding.Unit;
        if (node.Kind != NodeKinds.Integrate || door() is null || ask.Tip is not { Length: > 0 } tip) return new(unit, node.Id, PerformedWhat.Refused, WorkPerformer.NotDeclared);
        var now = clock();
        var (refused, started) = records.Edit<(NodeAttempt? Attempt, string? Refused)>(unit, record =>
        {
            var held = WorkPerformer.StatusOf(record, node.Id);
            if (!WorkPerformer.Same(held, evaluated)) return (null, (null, WorkPerformer.Moved));
            var moved = NodeLifecycle.Apply(node, held, start.Move);
            if (!moved.Moved) return (null, (null, moved.Refused));
            var call = new WorkPerformCall(unit, node.Kind, node.Version, WorkPerformer.Random("i-", 12), IntegrateNodes.Key(unit, tip), tip, [], now);
            var attempt = new NodeAttempt(node.Id, moved.Status.Attempt, IntegrateNodes.Sender, call, AttemptStates.Recorded) { RecordedAt = now };
            var fired = record.Fired.Concat(start.Firings.Select(firing => firing.Key)).Distinct(StringComparer.Ordinal).ToList();
            return (WorkPerformer.Kept(record, moved, "start", now) with { Attempts = [.. record.Attempts, attempt], Fired = fired }, (attempt, null));
        });
        if (refused is not null) return new(unit, node.Id, PerformedWhat.Refused, refused);
        if (started.Attempt is not { } recorded) return new(unit, node.Id, PerformedWhat.Refused, started.Refused);
        return await AnswerAsync(unit, recorded, node, ask, config, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Every attempt whose request's outcome the record has not established (written and never answered, unknown, or still held unreconciled
    /// by its node) is asked after, before any new start: the door says what became of its key, and the node follows it through the one table.
    /// Asked whatever graph governs its unit now: a request in flight is reconciled.
    /// </summary>
    public async Task<IReadOnlyList<WorkPerformed>> ReconcileAsync(CancellationToken ct)
    {
        var done = new List<WorkPerformed>();
        if (door() is null) return done;
        foreach (var record in records.All().Records)
        {
            foreach (var attempt in record.Attempts.Where(attempt => Waiting(record, attempt)).ToList())
            {
                done.Add(await AnswerAsync(record.Unit, attempt, Declaration(attempt), null, null, ct).ConfigureAwait(false));
            }
        }

        return done;
    }

    private static bool Waiting(NodeRecord record, NodeAttempt attempt) => IntegrateNodes.Waiting(record, attempt);

    /// <summary>A node's declaration from its attempt: a request in flight is answered whatever graph governs its unit now.</summary>
    private static NodeDeclaration Declaration(NodeAttempt attempt) => new(attempt.Node, NodeKinds.Integrate, attempt.Call.StepVersion, NodeReach.Waited);

    /// <summary>
    /// One look at the door for an attempt's key (step 2), or, with <paramref name="ask"/>, the ask for a request where none was made; and the
    /// answer kept on the attempt after the await (step 3), only while that attempt still waits for it. Unanswered, the attempt and its node
    /// are unknown, and it is asked after again.
    /// </summary>
    private async Task<WorkPerformed> AnswerAsync(string unit, NodeAttempt attempt, NodeDeclaration node, IntegrationAsk? ask, DriverConfig? config, CancellationToken ct)
    {
        var port = door()!;
        var tip = attempt.Call.SubjectRevision;
        IntegrationRequest? request;
        IntegrationAdmission? refused = null;
        bool? dropped = null;
        try
        {
            request = await port.FindAsync(unit, tip, ct).ConfigureAwait(false);
            if (request is null && ask is not null)
            {
                var admission = await port.AskAsync(ask, config!, ct).ConfigureAwait(false);
                request = admission.Request;
                if (!admission.Admitted) refused = admission;
            }
            else if (request is { ClosedAt: null } open && StoppedBy(unit, attempt, node) && !await port.PromotedAsync(open, ct).ConfigureAwait(false))
            {
                // The person's stop before promotion drops the request as `--drop` does (§17.4); the record then says it closed.
                dropped = await port.DropAsync(open, ct).ConfigureAwait(false);
                if (dropped == true) request = await port.FindAsync(unit, tip, ct).ConfigureAwait(false) ?? open;
            }
        }
        catch (Exception error) when (Unanswered(error, ct))
        {
            return Unknown(unit, attempt, node);
        }

        // The door's own unread answer (git could not say) is no outcome: asked again, never read as a refusal.
        if (refused is { Refusal: IntegrationRefusals.Unread }) return Unknown(unit, attempt, node);

        var now = clock();
        var (failed, result) = records.Edit(unit, record =>
        {
            if (record.Invoked(attempt.Call.Invocation) is not { } current || !Waiting(record, current)) return (null, new WorkPerformed(unit, node.Id, PerformedWhat.NotTaken, WorkPerformer.Moved));
            return request is null ? Withdraw(record, current, node, refused, now) : Read(record, current, node, request, now);
        });

        // Open, it waits in its line: the transport runs that line off the pass, and its answer asks the next.
        if (request is { ClosedAt: null } waiting) Lines()?.Send(new IntegrationLineSend(waiting.Repository, waiting.Workspace, waiting.Line));
        return failed is null ? result! : new(unit, node.Id, PerformedWhat.Unknown, failed);
    }

    /// <summary>Whether the person stopped the node on the attempt: its request is theirs to drop, not the node's to wait on.</summary>
    private bool StoppedBy(string unit, NodeAttempt attempt, NodeDeclaration node) =>
        records.Read(unit) is { } record && WorkPerformer.StatusOf(record, node.Id) is { State: NodeStates.Stopped } status && status.Unreconciled == attempt.Attempt;

    /// <summary>
    /// No request was made for the key, so none ever will for this attempt: a refused ask fails the node with the door's code and words (a
    /// refusal is never transient, and a new tip is a new start); a door that never took the key settles the attempt as never started and
    /// judges the node again on what is there now.
    /// </summary>
    private (NodeRecord? Next, WorkPerformed Result) Withdraw(NodeRecord record, NodeAttempt attempt, NodeDeclaration node, IntegrationAdmission? refused, DateTimeOffset now)
    {
        var status = WorkPerformer.StatusOf(record, node.Id);
        var on = status.Attempt == attempt.Attempt && status.State is NodeStates.Active or NodeStates.Unknown;
        if (refused is { Refusal: { } code })
        {
            _said.Add($"work  {record.Unit}: its integration request was not made ({code}): {refused.Message}");
            var settled = attempt with { State = AttemptStates.Settled, Outcome = WorkStates.Failed, Code = code };
            if (!on) return (Stale(record, attempt, settled, status), new(record.Unit, node.Id, PerformedWhat.Stale));
            var failed = NodeLifecycle.Apply(node, status, new NodeMove(NodeStates.Failed, NodeCause.Authority)
            {
                Source = NodeSource.Integration, Attempt = attempt.Attempt, Code = code,
            });
            return failed.Moved
                ? (WorkPerformer.With(WorkPerformer.Kept(record, failed, WorkStates.Failed, now), attempt, settled), new(record.Unit, node.Id, PerformedWhat.Applied, WorkStates.Failed))
                : (WorkPerformer.With(record, attempt, settled), new(record.Unit, node.Id, PerformedWhat.Refused, failed.Refused));
        }

        var result = new WorkPerformed(record.Unit, node.Id, PerformedWhat.Stale);
        if (on)
        {
            var superseded = NodeLifecycle.Apply(node, status, new NodeMove(NodeStates.Invalidated, NodeCause.Daoris));
            if (superseded.Moved)
            {
                record = WorkPerformer.Kept(record, superseded, "withdrawn", now);
                result = result with { What = PerformedWhat.Applied, Code = NodeStates.Invalidated };
            }
        }

        _said.Add($"work  {record.Unit}: its integration request was never taken by its door, so it is judged again on what is there now.");
        var never = attempt with { State = AttemptStates.Settled, Outcome = ReconcileStates.NotStarted, Code = "withdrawn" };
        var after = WorkPerformer.StatusOf(record, node.Id);
        record = WorkPerformer.With(record, attempt, never);
        return (record with { Statuses = WorkPerformer.With(record.Statuses, NodeLifecycle.Reconciled(after, attempt.Attempt)) }, result);
    }

    /// <summary>
    /// The request the door holds, read onto the attempt and its node (§17.4, <i>its states are read from the integration record, never
    /// copied</i>): open, the attempt settled as the door's and the node active, queued until the line's run takes it; closed settled, the
    /// node reached on the request as its evidence; closed handed back or dropped, the node failed with that word. A node moved off the attempt
    /// (invalidated by a new tip, stopped) takes only the lift of the gate the attempt held (<see cref="NodeLifecycle.Reconciled"/>).
    /// </summary>
    private (NodeRecord? Next, WorkPerformed Result) Read(NodeRecord record, NodeAttempt attempt, NodeDeclaration node, IntegrationRequest request, DateTimeOffset now)
    {
        var unit = record.Unit;
        var status = WorkPerformer.StatusOf(record, node.Id);
        var on = status.Attempt == attempt.Attempt && status.State is NodeStates.Active or NodeStates.Unknown;
        if (request.ClosedAt is null)
        {
            // Open: the door has it, and the line's run owes its outcome. An unknown node on it is active again, the same attempt.
            var adopted = attempt with { State = AttemptStates.Settled, Code = null };
            var next = record;
            if (on && status.State == NodeStates.Unknown)
            {
                var back = NodeLifecycle.Apply(node, status, new NodeMove(NodeStates.Active, NodeCause.Daoris) { Attempt = attempt.Attempt });
                next = WorkPerformer.Kept(next, back, "active", now);
            }

            if (adopted == attempt && ReferenceEquals(next, record)) return (null, new(unit, node.Id, PerformedWhat.Running));
            return (WorkPerformer.With(next, attempt, adopted), new(unit, node.Id, attempt.State == AttemptStates.Settled ? PerformedWhat.Running : PerformedWhat.Sent));
        }

        var outcome = request.ClosedWhy switch
        {
            IntegrationRequestCloses.Settled => WorkStates.Reached,
            IntegrationRequestCloses.HandedBack or IntegrationRequestCloses.Dropped => WorkStates.Failed,
            _ => null,
        };
        if (outcome is null)
        {
            // A word this build does not read: nothing is claimed of it, and the node is unknown until a build that reads it.
            _said.Add($"work  {unit}: its integration request closed as `{request.ClosedWhy}`, which this build does not read, so its node waits.");
            return (null, new(unit, node.Id, PerformedWhat.Running));
        }

        var code = outcome == WorkStates.Failed ? request.ClosedWhy : null;
        var settled = attempt with { State = AttemptStates.Settled, Outcome = outcome, Code = code };
        if (!on) return (Stale(record, attempt, settled, status), new(unit, node.Id, PerformedWhat.Stale));
        var move = outcome == WorkStates.Reached
            ? new NodeMove(NodeStates.Reached, NodeCause.Authority)
            {
                Source = NodeSource.Integration, Attempt = attempt.Attempt,
                Acceptance = status.Binding is { } binding
                    ? binding with { Evidence = new NodePort(node.Evidence ?? node.Kind, "integration:" + request.Id, binding.SubjectRevision, null) }
                    : null,
            }
            : new NodeMove(NodeStates.Failed, NodeCause.Authority) { Source = NodeSource.Integration, Attempt = attempt.Attempt, Code = code };
        var moved = NodeLifecycle.Apply(node, status, move);
        if (!moved.Moved) return (WorkPerformer.With(record, attempt, settled), new(unit, node.Id, PerformedWhat.Refused, moved.Refused));
        if (outcome == WorkStates.Failed)
        {
            _said.Add($"work  {unit}: its integration request was {request.ClosedWhy!.Replace('-', ' ')}, so its node failed.");
        }

        return (WorkPerformer.With(WorkPerformer.Kept(record, moved, outcome, now), attempt, settled), new(unit, node.Id, PerformedWhat.Applied, outcome));
    }

    /// <summary>An attempt its node has moved off, settled with the record's outcome, the gate it held lifted (§D3).</summary>
    private static NodeRecord Stale(NodeRecord record, NodeAttempt attempt, NodeAttempt settled, NodeStatus status)
    {
        var stale = WorkPerformer.With(record, attempt, settled);
        return stale with { Statuses = WorkPerformer.With(stale.Statuses, NodeLifecycle.Reconciled(status, attempt.Attempt)) };
    }

    /// <summary>The door did not answer: the attempt and its node are unknown, and the next pass asks after it again, never sending anew.</summary>
    private WorkPerformed Unknown(string unit, NodeAttempt attempt, NodeDeclaration node)
    {
        var now = clock();
        var (unsent, _) = records.Edit<bool>(unit, record =>
        {
            if (record.Invoked(attempt.Call.Invocation) is not { State: AttemptStates.Recorded or AttemptStates.Unknown or AttemptStates.Settled } current) return (null, false);
            var status = WorkPerformer.StatusOf(record, node.Id);
            if (status.State == NodeStates.Active && status.Attempt == current.Attempt)
            {
                record = WorkPerformer.Kept(record, NodeLifecycle.Apply(node, status, new NodeMove(NodeStates.Unknown, NodeCause.Daoris) { Attempt = current.Attempt }), "unknown", now);
            }

            return current.State == AttemptStates.Settled ? (record, true) : (WorkPerformer.With(record, current, current with { State = AttemptStates.Unknown }), true);
        });
        return new(unit, node.Id, PerformedWhat.Unknown, unsent ?? PerformedWhat.Unanswered);
    }

    /// <summary>A failure of the door itself, not the loop ending: the request's outcome is unknown, and it is asked after again.</summary>
    private static bool Unanswered(Exception error, CancellationToken ct) =>
        !ct.IsCancellationRequested && error is IOException or SqliteException or TimeoutException or DriverException;
}
