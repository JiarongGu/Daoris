using System.Security.Cryptography;

namespace Daoris.Driver;

/// <summary>
/// Durable execution of a plugin's performed nodes: each attempt written on its unit's
/// record with its start and the trigger firings it consumes before <c>work/perform</c> is sent, one idempotency key per attempt and its
/// invocation bound to it there; on a restart, a lost process, a deadline or an unknown outcome, <c>work/reconcile</c> before any new
/// attempt; a failure retried only for a declared <c>transient</c> code within the declared attempts, after a persisted delay; an answer taken
/// only from its plugin's current process, a replay of an outcome applied once. Run by the loop that holds the home's lease, the record's one
/// writer; every move goes through <see cref="NodeLifecycle.Apply"/>.
/// </summary>
/// <remarks>
/// <b>Every change is one locked edit</b> (<see cref="NodeRecordFiles.Edit{T}"/>): read, judged and written in one hold of
/// the record's lock with the lease asked inside it, never decided on a record read earlier. Nothing is awaited inside an edit, so a send's
/// receipt and a reconciliation's answer are applied after their await to the record as it is then, to their own invocation alone, and only
/// while that attempt still waits for them: an outcome kept meanwhile is never taken back.
/// </remarks>
public sealed class WorkPerformer(NodeRecordFiles records, IWorkPerformPort port, Func<DateTimeOffset> clock)
{
    /// <summary>An attempt's deadline where its node declares no <see cref="NodeDeclaration.TimeoutSeconds"/>.</summary>
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(15);

    /// <summary>A failure's code where the plugin gave none: not transient, so it stops the node for the person.</summary>
    public const string NoCode = "unspecified";

    /// <summary>Why an answer or a start is not taken (<see cref="WorkPerformed.Code"/>).</summary>
    public const string NotAttempt = "not-attempt";

    public const string Unreadable = NodeRecordFiles.Unreadable;

    /// <summary>This process does not hold the home's lease, asked inside the record's lock: nothing is written or sent.</summary>
    public const string NotHeld = NodeRecordFiles.NotHeld;

    public const string NotPlugin = "not-plugin";

    public const string NotDeclared = WorkRefusals.NotDeclared;

    /// <summary>
    /// A start whose binding names an input the record no longer holds as its producer's current acceptance exposes it:
    /// nothing is written or sent, and the next evaluation judges the node again.
    /// </summary>
    public const string StaleInput = "stale-input";

    /// <summary>
    /// A status the record no longer holds as evaluated, or an attempt no longer waiting for the answer that came: another move has been kept
    /// since, so this one is not written over it.
    /// </summary>
    public const string Moved = "moved";

    /// <summary>Why an attempt is held: its step declares neither <c>idempotent</c> nor <c>reconcile</c>, or a done answer named no outcome.</summary>
    public const string NoReconcile = "no-reconcile";

    public const string NoOutcome = "no-outcome";

    /// <summary>
    /// Why an attempt of a step declaring <c>idempotent</c> alone is held: its node has moved off it (stopped, skipped,
    /// superseded), so it is not delivered again, and idempotency does not say whether its effect began.
    /// </summary>
    public const string NotCurrent = "not-current";

    /// <summary>A word the person's door does not know: nothing is recorded.</summary>
    public const string NotResolution = "not-resolution";

    /// <summary>The held attempt already has the person's word, which the runtime applies at its next pass.</summary>
    public const string Worded = "worded";

    /// <summary>
    /// A plugin's word on Daoris's own close: a close's outcome is its door's answer, so neither <i>it happened</i> nor <i>it
    /// did not</i> is taken; nothing is recorded, and a word recorded before is taken back.
    /// </summary>
    public const string NotCloseWord = "not-close-word";

    /// <summary>A close's word on a plugin's attempt: nothing is recorded, and its three words stay as they were.</summary>
    public const string NotOffered = "not-offered";

    /// <summary><c>record</c> on a held close whose door's answer is not kept: there is nothing to record again, so nothing is recorded.</summary>
    public const string NotKept = "not-kept";

    /// <summary>
    /// <c>record</c> or <c>stop</c> on a close whose submission is the quest's no more: the person reopened it, or a later
    /// <c>ready</c> replaced it. Its answer cannot close the work there is now, and a stop of that work through it would be a stop of the next
    /// submission's, so the word is taken back, saying why: an unknown close is still asked after with its key, and a held one waits for
    /// <c>resend</c>.
    /// </summary>
    public const string SubmissionMoved = "submission-moved";

    /// <summary>
    /// Why a failure is kept for the person: its code is not one its step declares transient, or it is and its tries
    /// are spent. Its plugin's word stands, and Daoris makes no new attempt by itself.
    /// </summary>
    public const string FailureKept = "failure-kept";

    public const string TriesSpent = "tries-spent";

    /// <summary>
    /// A held attempt's word on a failure kept for the person, or <c>again</c> on an attempt held: nothing is recorded, and
    /// a word found recorded anyway is taken back.
    /// </summary>
    public const string NotAgain = "not-again";

    /// <summary>
    /// A word other than <c>stop</c> on a close found closed only in part: its door's answer stands for the quests it
    /// kept, and the lost one's work is the winner's, so the person's recovery is its node's stop alone. Nothing is recorded.
    /// </summary>
    public const string PartStop = "part-stop";

    /// <summary>
    /// A completion naming more resources than its unit's record keeps owing (<see cref="ResourceRecord.ReceiptsKept"/>): none
    /// of them is taken, and the attempt is held for the person, since what it made could not be accounted for.
    /// </summary>
    public const string TooManyResources = "too-many-resources";

    /// <summary>
    /// The units' resource records, where a completion's receipts are taken before its outcome is applied. Null takes
    /// none: a caller outside the runtime.
    /// </summary>
    public ResourceRecordFiles? Receipts { get; init; }

    /// <summary>
    /// The resources each plugin's manifest declares (<see cref="PluginManifest.Resources"/>), as the pass reads the catalogue: a receipt's
    /// role. Null for a plugin that declares none, whose receipts are taken as <see cref="ResourceReceipt.Undeclared"/>.
    /// </summary>
    public Func<string, IReadOnlyList<PluginResource>?> Resources { get; set; } = _ => null;

    /// <summary>
    /// The doors this loop starts its nodes on (<see cref="NodeDoors"/>): a kind whose unit is started through this journal (<see cref="IJournalled"/>)
    /// has its attempts sent to its door, a port, and asked after only once it says it has an answer, or after a restart.
    /// None starts no such node: it stays ready.
    /// </summary>
    public NodeDoors Doors { get; set; } = NodeDoors.None;

    /// <summary>The port <paramref name="kind"/>'s nodes are started on, where its unit is started through this journal and its door is wired; else null.</summary>
    internal ICheckPort? PortOf(string kind) => NodeKit.Of(kind) is IJournalled ? Doors.Door<ICheckPort>(kind) : null;

    /// <summary>
    /// The invocations this performer's loop delivered: an attempt sent by a loop since ended is asked after before anything else, since a
    /// new loop's generations start again at 1 and cannot tell its process from the earlier loop's.
    /// </summary>
    private readonly HashSet<string> _sentHere = new(StringComparer.Ordinal);

    public IReadOnlyList<WorkPerformed> Keep(IReadOnlyDictionary<string, NodeStatus> before, WireEvaluated evaluated) => Keeping.Keep(before, evaluated);

    private WorkKeeping Keeping => new(records, clock);
    private WorkOutcomes Outcomes => new(clock);
    private WorkReceipting Receipting => new(records, clock, () => Receipts, plugin => Resources(plugin));
    private WorkHearing Hearing => new(records, port, clock, Receipting, Outcomes);
    private WorkPersonWord Person => new(records, clock);

    /// <summary>
    /// Starts a plugin's performed node on the start the evaluation proposed: the start, its firings and the attempt written first, then sent.
    /// The start is judged on the record under its lock, so a second start on the same node reads the first and is refused.
    /// A loop ended while the send is out (its token cancelled) writes nothing more: the next loop reconciles the attempt before anything else.
    /// </summary>
    public async Task<WorkPerformed> StartAsync(NodeDeclaration node, WireStart start, CancellationToken ct)
    {
        if (start.Move.Binding is not { } binding) return new("", start.Node, PerformedWhat.Refused, NodeRefusal.NoBinding);
        var unit = binding.Unit;
        // A kind of Daoris's own performed this way beside a plugin's, on its port, its attempt naming its kind.
        var onPort = PortOf(node.Kind) is not null;
        if (!(NodeKit.IsPlugin(node.Kind) || onPort) || node.Reach != NodeReach.Performed) return new(unit, node.Id, PerformedWhat.Refused, NotPlugin);

        // The attempt (invocation, key, revision, inputs, deadline, state) on the unit's record first, with its start and its firings.
        var now = clock();
        var (refused, started) = records.Edit<(NodeAttempt? Attempt, string? Refused)>(unit, record =>
        {
            // Each input resolved again at dispatch to its producer's current acceptance, never an old or unaccepted ref.
            if (WorkBelief.Unresolved(record, binding) is not null) return (null, (null, StaleInput));
            var moved = NodeLifecycle.Apply(node, StatusOf(record, node.Id), start.Move);
            if (!moved.Moved) return (null, (null, moved.Refused));
            // The call names the unit its node observes, which on a run's record is its subject's,
            // never `run:`; on a unit's own record it is that unit.
            var call = new WorkPerformCall(binding.Observes, node.Kind, node.Version, Random("i-", 12), Random("k-", 16), binding.SubjectRevision ?? "",
                [.. binding.Inputs.OrderBy(input => input.Key, StringComparer.Ordinal).Select(input => input.Value.Ref)], now + Timeout(node));
            var attempt = new NodeAttempt(node.Id, moved.Status.Attempt, onPort ? node.Kind : node.Kind[..node.Kind.IndexOf('/')], call, AttemptStates.Recorded) { RecordedAt = now };
            var fired = record.Fired.Concat(start.Firings.Select(firing => firing.Key)).Distinct(StringComparer.Ordinal).ToList();
            return (Kept(record, moved, "start", now) with { Attempts = [.. record.Attempts, attempt], Fired = fired }, (attempt, null));
        });
        if (refused is not null) return new(unit, node.Id, PerformedWhat.Refused, refused);
        if (started.Attempt is not { } recorded) return new(unit, node.Id, PerformedWhat.Refused, started.Refused);
        return await SendAsync(unit, recorded, node, PerformedWhat.Sent, ct).ConfigureAwait(false);
    }

    public WorkPerformed Begin(NodeDeclaration node, WireStart start, NodeStatus evaluated) => Keeping.Begin(node, start, evaluated);

    public IReadOnlyList<WorkPerformed> Hear(
        IReadOnlyList<WorkStepHeard> heard, Func<string, string, NodeDeclaration?> declared, Func<string, WatchedUnit?> watched) =>
        Hearing.Hear(heard, declared, watched);

    public IReadOnlyList<WorkPerformed> Hear(
        IReadOnlyList<WorkStepHeard> heard, Func<string, string, NodeDeclaration?> declared, Func<string, NodeAttempt, WatchedUnit?> watched) =>
        Hearing.Hear(heard, declared, watched);

    /// <summary>
    /// Asks after every attempt whose outcome is not established and is due (after a restart with an unresolved attempt, after its
    /// deadline, after a lost connection), before any new attempt: the one table refuses a start while its node holds it unreconciled.
    /// </summary>
    public Task<IReadOnlyList<WorkPerformed>> ReconcileAsync(Func<string, string, NodeDeclaration?> declared, CancellationToken ct) =>
        ReconcileAsync(declared, WorkSends.Unbounded, ct);

    /// <summary>
    /// <see cref="ReconcileAsync(Func{string, string, NodeDeclaration?}, CancellationToken)"/> within a pass's calls: each attempt
    /// asked after or delivered again takes one of <paramref name="sends"/>, and the attempts past them are asked at the next pass.
    /// </summary>
    /// <remarks>
    /// The attempts due are taken in turn, by unit and invocation, beginning after the last one the pass before reached and round
    /// again from the first, so attempts whose questions keep failing (an I/O error leaves one due at every pass) never spend the four at every
    /// pass while the rest wait.
    /// </remarks>
    public async Task<IReadOnlyList<WorkPerformed>> ReconcileAsync(Func<string, string, NodeDeclaration?> declared, WorkSends sends, CancellationToken ct)
    {
        var done = new List<WorkPerformed>();
        var now = clock();
        // Daoris's own close is asked after by its closer, which sends the same close again; no plugin is asked about it.
        // Nor about a request, which its node publishes again under its key.
        var due = records.All().Records
            .SelectMany(record => record.Attempts.Where(attempt => attempt.Plugin != QuestClosing.Sender && attempt.Plugin != RequestStart.Sender && Asked(attempt, now))
                .Select(attempt => (record.Unit, Attempt: attempt, Key: $"{record.Unit}\n{attempt.Call.Invocation}")))
            .OrderBy(each => each.Key, StringComparer.Ordinal).ToList();
        var next = _reconciled is null ? 0 : due.FindIndex(each => string.CompareOrdinal(each.Key, _reconciled) > 0);
        if (next < 0) next = 0;
        foreach (var (unit, attempt, key) in due.Skip(next).Concat(due.Take(next)))
        {
            // The pass's calls spent, the attempts left are asked at the next pass, still before any start there.
            if (sends.Left <= 0) return done;
            _reconciled = key;
            if (declared(unit, attempt.Node) is not { } node) continue;
            done.Add(await ReconcileOneAsync(unit, attempt.Call.Invocation, node, sends, ct).ConfigureAwait(false));
        }

        return done;
    }

    /// <summary>The last attempt reconciliation reached, as its unit and invocation: the next pass begins after it.</summary>
    private string? _reconciled;

    /// <summary>Every declared retry whose persisted delay has passed: its node made ready for a new attempt, with a new key at its start.</summary>
    public IReadOnlyList<WorkPerformed> Due(Func<string, string, NodeDeclaration?> declared)
    {
        var done = new List<WorkPerformed>();
        var now = clock();
        foreach (var read in records.All().Records)
        {
            if (!read.Statuses.Values.Any(Retrying)) continue;
            var (refused, retried) = records.Edit<List<WorkPerformed>>(read.Unit, current =>
            {
                var said = new List<WorkPerformed>();
                var record = current;
                foreach (var (id, status) in current.Statuses.Where(each => Retrying(each.Value)))
                {
                    if (record.Newest(id) is not { RetryAt: { } at } attempt || at > now || declared(record.Unit, id) is not { } node) continue;
                    var moved = NodeLifecycle.Apply(node, status, new NodeMove(NodeStates.Ready, NodeCause.Daoris));
                    record = Kept(record, moved, "retry", now);
                    record = With(record, attempt, attempt with { RetryAt = null });
                    said.Add(new(record.Unit, id, moved.Moved ? PerformedWhat.Retry : PerformedWhat.Refused, moved.Refused));
                }

                return (ReferenceEquals(record, current) ? null : record, said);
            });
            if (refused is not null) done.Add(new(read.Unit, "", PerformedWhat.Refused, refused));
            else done.AddRange(retried!);
        }

        return done;
    }

    public WorkPerformed Word(string unit, string node, int attempt, string resolution, string? outcome = null) =>
        Person.Word(unit, node, attempt, resolution, outcome);

    public IReadOnlyList<WorkPerformed> Resolve(Func<string, string, NodeDeclaration?> declared) => Person.Resolve(declared);

    internal static bool KeptFailure(NodeRecord record, NodeAttempt attempt) => WorkPersonWord.KeptFailure(record, attempt);

    internal static string? WordRefused(NodeAttempt held, string word) => WorkPersonWord.WordRefused(held, word);


    /// <summary>The current generation of the process that acknowledged the attempt: its place's, where its plugin is placed.</summary>
    private int? Held(NodeAttempt attempt) =>
        attempt.Generation is { } held ? port.GenerationOf(attempt.Plugin, held) : port.GenerationOf(attempt.Plugin);

    private static bool Retrying(NodeStatus status) => status is { State: NodeStates.Failed, Transient: true };

    /// <summary>Whether Daoris asks after an attempt now: one never acknowledged, unknown, or sent by a loop since ended, to a process since
    /// replaced, or past its deadline; a running answer waits to its <see cref="NodeAttempt.AskAt"/>.</summary>
    private bool Asked(NodeAttempt attempt, DateTimeOffset now) => attempt.State switch
    {
        AttemptStates.Recorded or AttemptStates.Unknown => attempt.AskAt is not { } ask || ask <= now,
        // The executor owns a check's bound, so it is asked once it says it has an answer, or after a restart; never at its deadline.
        AttemptStates.Sent when OnPort(attempt) || AgentsFor(attempt, null) is not null =>
            !SentHere(attempt.Call.Invocation) || (PortOf(attempt.Plugin) ?? AgentsFor(attempt, null))?.Answered(attempt.Call.Invocation) == true,
        AttemptStates.Sent => !SentHere(attempt.Call.Invocation) || Held(attempt) != attempt.Generation
            || (attempt.AskAt ?? attempt.Call.DeadlineAt) <= now,
        _ => false,
    };

    /// <remarks>
    /// An attempt called about takes one of <paramref name="sends"/>, its question and the redelivery that answer may bring
    /// together; one held with no call takes none, and neither does one whose plugin's process is not up, which is not asked now.
    /// </remarks>
    private async Task<WorkPerformed> ReconcileOneAsync(string unit, string invocation, NodeDeclaration node, WorkSends sends, CancellationToken ct)
    {
        var now = clock();

        // Active becomes unknown: its outcome cannot be established now. Written before the question, so a loop that ends here asks again,
        // and only while the record still holds the attempt as one to ask after.
        var (refused, asked) = records.Edit<NodeAttempt?>(unit, record =>
        {
            if (record.Invoked(invocation) is not { } attempt || !Asked(attempt, now)) return (null, null);
            var status = StatusOf(record, node.Id);
            if (status.State == NodeStates.Active && status.Attempt == attempt.Attempt)
            {
                record = Kept(record, NodeLifecycle.Apply(node, status, new NodeMove(NodeStates.Unknown, NodeCause.Daoris) { Attempt = attempt.Attempt }), "unknown", now);
            }

            var unknown = attempt with { State = AttemptStates.Unknown };
            return (With(record, attempt, unknown), unknown);
        });
        if (refused is not null) return new(unit, node.Id, PerformedWhat.Refused, refused);
        if (asked is null) return new(unit, node.Id, PerformedWhat.NotTaken, Moved);
        // An attempt begun on `work/perform` is its plugin's to settle by its own `check`, whatever the step declares since.
        var agents = AgentsFor(asked, null);
        if (agents is null && (node.Perform is not { } perform || !(perform.Reconcile || perform.Idempotent)))
        {
            return records.Edited(unit, node.Id, record => Still(record, invocation, node.Id, current => Hold(record, current, NoReconcile)));
        }

        if (agents is null && !node.Perform!.Reconcile)
        {
            // An idempotent step's redelivery uses the same key, its plugin deduplicates it durably, and its answer comes by `work/step`.
            // Idempotency allows the same key again; it does not say the effect never began. So an attempt its node has moved
            // off is held for the person, its gate kept, never settled as not started while its effect may still be running.
            var decided = records.Edited(unit, node.Id, record => Still(record, invocation, node.Id, current =>
            {
                var status = StatusOf(record, node.Id);
                if (status.State != NodeStates.Unknown || status.Attempt != current.Attempt) return Hold(record, current, NotCurrent);
                return (Kept(record, NodeLifecycle.Apply(node, status, new NodeMove(NodeStates.Active, NodeCause.Daoris)), "redeliver", now),
                    new WorkPerformed(unit, node.Id, PerformedWhat.Redelivered));
            }));
            return decided.What == PerformedWhat.Redelivered && sends.Take() ? await SendAsync(unit, asked, node, PerformedWhat.Redelivered, ct).ConfigureAwait(false) : decided;
        }

        var check = OnPort(asked) || agents is not null;
        var checks = OnPort(asked) ? PortOf(asked.Plugin) : agents;
        if ((check ? checks is null : port.GenerationOf(asked.Plugin) is null) || !sends.Take()) return new(unit, node.Id, PerformedWhat.Unanswered);
        WorkReconcileAnswer answer;
        int generation;
        try
        {
            // A check's answer is the executor's, read from its attempt's folder.
            // Asked under the unit its call is sent for, a run's subject's, never the record's.
            (answer, generation) = check
                ? (await checks!.ReconcileAsync(unit, asked, ct).ConfigureAwait(false), PortGeneration)
                : await port.ReconcileStepAsync(
                    asked.Plugin, new WorkReconcileCall(asked.Call.Unit, asked.Call.Step, invocation, asked.Call.IdempotencyKey, asked.Call.SubjectRevision), ct)
                    .ConfigureAwait(false);
        }
        catch (Exception error) when (Unanswered(error))
        {
            return new(unit, node.Id, PerformedWhat.Unanswered);
        }

        // After the await, the record as it is now: the answer is about this invocation alone, and applies only while the
        // attempt is still unknown, so an outcome heard while the question is out is kept, never taken back by a later `running`.
        now = clock();

        // What the attempt created so far, taken before its answer is applied, as a completion's are.
        IReadOnlyList<WorkResource> resources = [.. answer.Resources.Concat(answer.Outcome?.Resources ?? []).Distinct()];
        if (Receipting.Receipted(unit, asked, node, resources, lapsed: false) is { } unkept) return unkept;
        var applied = records.Edited(unit, node.Id, record => Still(record, invocation, node.Id, current =>
        {
            var status = StatusOf(record, node.Id);
            var isCurrent = status.Attempt == current.Attempt && status.State == NodeStates.Unknown;
            switch (answer.State)
            {
                case ReconcileStates.Done when answer.Outcome is { } outcome:
                    return Outcomes.Outcome(record, current, node, outcome, generation);
                case ReconcileStates.Done:
                    return Hold(record, current, NoOutcome);
                case ReconcileStates.Failed:
                    return Outcomes.Outcome(record, current, node, answer.Outcome is { State: WorkStates.Failed } failed ? failed : new WorkOutcome(WorkStates.Failed, NoCode), generation);
                case ReconcileStates.NotStarted when isCurrent:
                    return (Kept(record, NodeLifecycle.Apply(node, status, new NodeMove(NodeStates.Active, NodeCause.Daoris)), "redeliver", now),
                        new WorkPerformed(unit, node.Id, PerformedWhat.Redelivered));
                case ReconcileStates.NotStarted:
                    // Its plugin's answer that it never started, and no longer current (stopped, skipped, superseded): nothing to deliver, and the
                    // gate lifts.
                    var settled = With(record, current, current with { State = AttemptStates.Settled, Outcome = ReconcileStates.NotStarted, Generation = generation });
                    return (settled with { Statuses = With(settled.Statuses, NodeLifecycle.Reconciled(status, current.Attempt)) }, new WorkPerformed(unit, node.Id, PerformedWhat.Stale));
                case ReconcileStates.Running:
                    var running = isCurrent ? Kept(record, NodeLifecycle.Apply(node, status, new NodeMove(NodeStates.Active, NodeCause.Daoris)), "running", now) : record;
                    var wait = current.Call.DeadlineAt > now ? current.Call.DeadlineAt : now + Timeout(node);
                    return (With(running, current, current with { State = isCurrent ? AttemptStates.Sent : AttemptStates.Unknown, Generation = generation, AskAt = wait }),
                        new WorkPerformed(unit, node.Id, PerformedWhat.Running));
                case ReconcileStates.Unknown when check && !isCurrent:
                    // A check stopped meanwhile answers as a stale attempt's, settling it and moving nothing.
                    var stopped = With(record, current, current with { State = AttemptStates.Settled, Outcome = WorkStates.Unknown, Code = answer.Outcome?.Code, Generation = generation });
                    return (stopped with { Statuses = With(stopped.Statuses, NodeLifecycle.Reconciled(status, current.Attempt)) }, new WorkPerformed(unit, node.Id, PerformedWhat.Stale));
                default:
                    // Unknown stops the step, and the person sees it; no new attempt is made by itself. A check's says why.
                    return Hold(record, current with { Generation = generation }, check ? answer.Outcome?.Code ?? ReconcileStates.Unknown : ReconcileStates.Unknown);
            }
        }));
        Receipting.Supersede(unit, asked, resources, applied);
        if (applied.What == PerformedWhat.Running) lock (_sentHere) _sentHere.Add(invocation);
        return applied.What == PerformedWhat.Redelivered ? await SendAsync(unit, asked, node, PerformedWhat.Redelivered, ct).ConfigureAwait(false) : applied;
    }

    internal static NodeAcceptance Accepted(NodeDeclaration node, string word, NodeAcceptance binding, WorkOutcome outcome) =>
        WorkOutcomes.Accepted(node, word, binding, outcome);

    /// <summary>One delivery of an attempt under its own step's authority, its key unchanged: acknowledged, it is sent by this loop's process of that
    /// generation; failed or unanswered, its outcome is unknown until reconciled. A cancelled token is the loop ending, which writes nothing more. The receipt
    /// is applied after its await to the record as it is then: it counts its delivery, and moves to sent only an attempt still waiting for it; an outcome
    /// heard or held meanwhile stays, and so does every other attempt and node on the unit.</summary>
    private async Task<WorkPerformed> SendAsync(string unit, NodeAttempt attempt, NodeDeclaration node, string what, CancellationToken ct)
    {
        var invocation = attempt.Call.Invocation;
        int generation;
        try
        {
            generation = await StepAuthority.RunAsync(StepAuthorities.Of(unit, attempt), async () =>
            {
                var first = attempt.Deliveries == 0 ? node : null;
                var agents = AgentsFor(attempt, first);
                if (!OnPort(attempt) && agents is null && first?.Goal is null)
                    return await port.PerformAsync(attempt.Plugin, attempt.Call with { Options = node.Options }, ct).ConfigureAwait(false);

                // A hand-over; the executor or the step's session runs it on its own task, never the pass's.
                await (PortOf(attempt.Plugin) ?? agents ?? throw new DriverException(OnPort(attempt) ? "no executor takes a check here" : "no step agents perform a step declaring a goal here"))
                    .SendAsync(unit, attempt, node, ct).ConfigureAwait(false);
                return PortGeneration;
            }).ConfigureAwait(false);
        }
        catch (Exception error) when (Unanswered(error))
        {
            var now = clock();
            var (unsent, _) = records.Edit<bool>(unit, record =>
            {
                if (record.Invoked(invocation) is not { State: AttemptStates.Recorded or AttemptStates.Unknown } current) return (null, false);
                var status = StatusOf(record, node.Id);
                if (status.State == NodeStates.Active && status.Attempt == current.Attempt)
                {
                    record = Kept(record, NodeLifecycle.Apply(node, status, new NodeMove(NodeStates.Unknown, NodeCause.Daoris) { Attempt = current.Attempt }), "unknown", now);
                }

                return (With(record, current, current with { State = AttemptStates.Unknown }), true);
            });
            return new(unit, node.Id, PerformedWhat.Unknown, unsent ?? PerformedWhat.Unanswered);
        }

        lock (_sentHere) _sentHere.Add(invocation);
        var (refused, _) = records.Edit<bool>(unit, record =>
        {
            if (record.Invoked(invocation) is not { } current) return (null, false);
            var receipt = current.State is AttemptStates.Recorded or AttemptStates.Unknown
                ? current with { State = AttemptStates.Sent, Generation = generation, Deliveries = current.Deliveries + 1, AskAt = null }
                : current with { Deliveries = current.Deliveries + 1 };
            return (With(record, current, receipt), true);
        });

        // A receipt the record could not keep leaves the attempt as it stood, recorded or unknown: the next loop asks after it.
        return refused is null ? new(unit, node.Id, what) : new(unit, node.Id, PerformedWhat.Unknown, refused);
    }

    /// <summary>An attempt nothing establishes by itself: held for the person, its node left as it stands, its gate held.</summary>
    internal static (NodeRecord? Next, WorkPerformed Result) Hold(NodeRecord record, NodeAttempt attempt, string code) =>
        (With(record, attempt, attempt with { State = AttemptStates.Held, Code = code }), new(record.Unit, attempt.Node, PerformedWhat.Held, code));

    /// <summary>
    /// A reconciliation's change, made on the attempt as the record holds it now and only while it is still as this performer left it before
    /// the question (unknown): one settled meanwhile is a replay, one moved otherwise (sent, held) is not taken again.
    /// </summary>
    private static (NodeRecord? Next, WorkPerformed Result) Still(
        NodeRecord record, string invocation, string node, Func<NodeAttempt, (NodeRecord? Next, WorkPerformed Result)> change) => record.Invoked(invocation) switch
    {
        null => (null, new(record.Unit, node, PerformedWhat.NotTaken, NotAttempt)),
        { State: AttemptStates.Settled } => (null, new(record.Unit, node, PerformedWhat.Replay)),
        { State: AttemptStates.Unknown } attempt => change(attempt),
        _ => (null, new(record.Unit, node, PerformedWhat.NotTaken, Moved)),
    };

    /// <summary>A failure of the call itself, not the loop ending: the attempt's outcome is unknown, never a second blind dispatch.</summary>
    private static bool Unanswered(Exception error) =>
        error is not OperationCanceledException && error is DriverException or IOException or TimeoutException or InvalidOperationException;

    /// <summary>
    /// The units whose records name any of <paramref name="invocations"/>: what a pass reads the tips of before it hears
    /// their answers, so a completion is judged on the tip the pass reads. One read of the records; none for no invocation.
    /// </summary>
    public IReadOnlySet<string> UnitsOf(IReadOnlyCollection<string> invocations)
    {
        var units = new HashSet<string>(StringComparer.Ordinal);
        if (invocations.Count == 0) return units;
        foreach (var record in records.All().Records)
        {
            if (invocations.Any(invocation => record.Invoked(invocation) is not null)) units.Add(record.Unit);
        }

        return units;
    }

    private bool SentHere(string invocation)
    {
        lock (_sentHere) return _sentHere.Contains(invocation);
    }

    internal static NodeStatus StatusOf(NodeRecord record, string node) => record.Statuses.GetValueOrDefault(node) ?? NodeLifecycle.Start(record.Unit, node);

    /// <summary>The record with a move's status kept and what it cleared added to the history.</summary>
    internal static NodeRecord Kept(NodeRecord record, NodeMoved moved, string why, DateTimeOffset now) => !moved.Moved ? record : record with
    {
        Statuses = With(record.Statuses, moved.Status),
        History = moved.Cleared is { } cleared ? [.. record.History, new NodeHistory(now, why, cleared)] : record.History,
    };

    internal static Dictionary<string, NodeStatus> With(IReadOnlyDictionary<string, NodeStatus> statuses, NodeStatus status) =>
        new(statuses, StringComparer.Ordinal) { [status.Node] = status };

    internal static NodeRecord With(NodeRecord record, NodeAttempt was, NodeAttempt now) => record with
    {
        Attempts = [.. record.Attempts.Select(attempt => string.Equals(attempt.Call.Invocation, was.Call.Invocation, StringComparison.Ordinal) ? now : attempt)],
    };

    internal static bool Same(NodeStatus one, NodeStatus other) =>
        string.Equals(NodeRecordFiles.StatusText(one), NodeRecordFiles.StatusText(other), StringComparison.Ordinal);

    internal static TimeSpan Timeout(NodeDeclaration node) => node.TimeoutSeconds is { } seconds ? TimeSpan.FromSeconds(seconds) : DefaultTimeout;

    /// <summary>
    /// Whether an attempt is sent to its kind's port rather than a plugin's process: its sender names a kind whose unit is started through this
    /// journal, wired or not.
    /// </summary>
    internal static bool OnPort(NodeAttempt attempt) => NodeKit.Of(attempt.Plugin) is IJournalled;

    /// <summary>The step agents' port for a plugin's attempt they hold, or whose node declares a goal; null where no loop wires them.</summary>
    private IStepAgents? AgentsFor(NodeAttempt attempt, NodeDeclaration? node) =>
        !OnPort(attempt) && Doors.Door<IStepAgents>(StepAgents.Door) is { } agents && (node?.Goal is not null || agents.Holds(attempt.Call.Invocation))
            ? agents
            : null;

    /// <summary>The generation an attempt on a port records: the port has one, the driver's own.</summary>
    private const int PortGeneration = 1;

    /// <summary>
    /// The person's stop of a check that is running: the pass that applied it tells the executor,
    /// which ends its job as at its bound. Each named node that is stopped and still waits on an attempt sent here is told.
    /// </summary>
    public void StopChecks(string unit, IReadOnlyCollection<string> nodes)
    {
        if (records.Read(unit) is not { } record) return;
        foreach (var attempt in record.Attempts.Where(each => (OnPort(each) || AgentsFor(each, null) is not null) && nodes.Contains(each.Node)
                     && each.State is AttemptStates.Sent or AttemptStates.Recorded or AttemptStates.Unknown))
        {
            if (StatusOf(record, attempt.Node).State == NodeStates.Stopped) (PortOf(attempt.Plugin) ?? AgentsFor(attempt, null))?.Stop(attempt.Call.Invocation);
        }
    }

    internal static string Random(string prefix, int bytes) => prefix + Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(bytes));
}
