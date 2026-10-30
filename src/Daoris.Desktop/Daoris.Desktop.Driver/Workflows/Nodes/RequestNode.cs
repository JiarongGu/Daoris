namespace Daoris.Driver;

/// <summary>What a request node publishes: the quest's key and words, and who asks whom.</summary>
public sealed record RequestAsk(string Key, string From, string To, string Title, string Body);

/// <summary>The exchange's answer to a published request: the quest made (or held already under the key), or the refusal's code and words.</summary>
public sealed record RequestPublished(bool Ok, string? Code, string Message, QuestView? Quest);

/// <summary>The one door a request node publishes through, as a port so a node is judged over a stand-in. <see cref="ServiceClient"/> is the host's.</summary>
public interface IRequestPort
{
    /// <exception cref="HttpRequestException">The host did not answer: the publish is unknown, and the same key is sent again.</exception>
    Task<RequestPublished> PublishAsync(RequestAsk ask, CancellationToken ct);
}

/// <summary>
/// Daoris's own <c>request</c> node (INTEGRATE1p; the integration design §17.5): a waited node whose start publishes a quest to another
/// repository under the request's key, <c>&lt;run&gt;/&lt;node&gt;/&lt;n&gt;</c>, and whose state is read from this machine's projection of that quest.
/// </summary>
/// <remarks>
/// <para><b>The key is written first.</b> Step 1 is one locked edit that starts the node and keeps its attempt with the key; step 2 publishes; step 3
/// keeps the answer. A quest the projection already holds under the key is never published again, and a publish whose answer was lost is sent once
/// more under the same key, which the exchange answers with the quest it made: no duplicate.</para>
/// <para><b>Reached, failed, waiting.</b> The quest's done reaches it (its taking under <c>until: taken</c>); its decline fails it with
/// <see cref="RequestOptions.DeclinedCode"/>. A quest not in the projection is unknown, never passed. Nothing of the receiving repository's files is read.</para>
/// </remarks>
public sealed class RequestNode(NodeRecordFiles records, Func<IRequestPort?> port, Func<DateTimeOffset> clock)
{
    private readonly List<string> _said = [];

    /// <summary>The sender a request's attempt names.</summary>
    public const string Sender = NodeKinds.Request;

    /// <summary>The key a request's quest is published under: the run, the node and the node's attempt.</summary>
    public static string KeyOf(string run, string node, int attempt) => $"{run}/{node}/{attempt}";

    /// <summary>What the node said since it was last asked.</summary>
    public IReadOnlyList<string> TakeSaid()
    {
        var said = _said.ToList();
        _said.Clear();
        return said;
    }

    /// <summary>Whether an attempt is a request's whose quest the record has not finished with.</summary>
    internal static bool Waiting(NodeRecord record, NodeAttempt attempt) =>
        attempt.Plugin == Sender
        && (attempt.State is AttemptStates.Recorded or AttemptStates.Unknown
            || record.Statuses.GetValueOrDefault(attempt.Node) is { } status && status.Unreconciled == attempt.Attempt);

    private static QuestView? Find(IReadOnlyDictionary<string, QuestView> quests, string key) =>
        quests.Values.FirstOrDefault(quest => string.Equals(quest.Request, key, StringComparison.Ordinal));

    /// <summary>The start the evaluation proposed for a request of <paramref name="run"/>: its attempt and key kept, then the quest published and the answer kept.</summary>
    public async Task<WorkPerformed> StartAsync(
        NodeDeclaration node, WireStart start, NodeStatus evaluated, string run, string from, IReadOnlyDictionary<string, QuestView> quests, CancellationToken ct)
    {
        if (start.Move.Binding is not { } binding) return new("", start.Node, PerformedWhat.Refused, NodeRefusal.NoBinding);
        var unit = binding.Unit;
        if (node.Kind != NodeKinds.Request || port() is null || RequestOptions.Problem(node) is not null) return new(unit, node.Id, PerformedWhat.Refused, WorkPerformer.NotDeclared);
        var now = clock();
        var (refused, started) = records.Edit<(NodeAttempt? Attempt, string? Refused)>(unit, record =>
        {
            var held = WorkPerformer.StatusOf(record, node.Id);
            if (!WorkPerformer.Same(held, evaluated)) return (null, (null, WorkPerformer.Moved));
            var moved = NodeLifecycle.Apply(node, held, start.Move);
            if (!moved.Moved) return (null, (null, moved.Refused));
            var key = KeyOf(run, node.Id, moved.Status.Attempt);
            var call = new WorkPerformCall(unit, node.Kind, node.Version, WorkPerformer.Random("i-", 12), key, key, [], now);
            var attempt = new NodeAttempt(node.Id, moved.Status.Attempt, Sender, call, AttemptStates.Recorded) { RecordedAt = now };
            var fired = record.Fired.Concat(start.Firings.Select(firing => firing.Key)).Distinct(StringComparer.Ordinal).ToList();
            return (WorkPerformer.Kept(record, moved, "start", now) with { Attempts = [.. record.Attempts, attempt], Fired = fired }, (attempt, null));
        });
        if (refused is not null) return new(unit, node.Id, PerformedWhat.Refused, refused);
        if (started.Attempt is not { } recorded) return new(unit, node.Id, PerformedWhat.Refused, started.Refused);
        return await AnswerAsync(unit, recorded, node, from, quests, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Every request attempt of a run the record has not finished with, judged on this look's projection: its quest found by its key and read onto
    /// the node, or, where it is not there and the attempt never had its answer, published under the same key.
    /// </summary>
    public async Task<IReadOnlyList<WorkPerformed>> ReconcileAsync(
        IEnumerable<GovernedUnit> governed, IReadOnlyDictionary<string, QuestView> quests, CancellationToken ct)
    {
        var done = new List<WorkPerformed>();
        foreach (var each in governed.Where(each => each.Scope is not null))
        {
            if (records.Read(each.Unit.Id) is not { } record) continue;
            foreach (var attempt in record.Attempts.Where(attempt => Waiting(record, attempt)).ToList())
            {
                if (!each.Declared.TryGetValue(attempt.Node, out var node) || node.Kind != NodeKinds.Request) continue;
                done.Add(await AnswerAsync(each.Unit.Id, attempt, node, each.Watched.Repository, quests, ct).ConfigureAwait(false));
            }
        }

        return done;
    }

    private async Task<WorkPerformed> AnswerAsync(
        string unit, NodeAttempt attempt, NodeDeclaration node, string from, IReadOnlyDictionary<string, QuestView> quests, CancellationToken ct)
    {
        var key = attempt.Call.IdempotencyKey;
        var quest = Find(quests, key);
        RequestPublished? refused = null;
        if (quest is null && attempt.State is AttemptStates.Recorded or AttemptStates.Unknown)
        {
            try
            {
                var answer = await port()!.PublishAsync(new RequestAsk(key, from, NodeOptions.Text(node, "to")!, NodeOptions.Text(node, "title")!, NodeOptions.Text(node, "body")!), ct)
                    .ConfigureAwait(false);
                if (answer.Ok) quest = answer.Quest; else refused = answer;
            }
            catch (Exception error) when (!ct.IsCancellationRequested && error is HttpRequestException or IOException or TimeoutException or DriverException)
            {
                return Unknown(unit, attempt, node);
            }
        }

        var now = clock();
        var (failed, result) = records.Edit(unit, record =>
        {
            if (record.Invoked(attempt.Call.Invocation) is not { } current || !Waiting(record, current)) return (null, new WorkPerformed(unit, node.Id, PerformedWhat.NotTaken, WorkPerformer.Moved));
            return Read(record, current, node, quest, refused, now);
        });
        return failed is null ? result! : new(unit, node.Id, PerformedWhat.Unknown, failed);
    }

    /// <summary>The quest as the projection holds it, read onto the attempt and its node; nothing but a refusal or the quest's own state moves the node.</summary>
    private (NodeRecord? Next, WorkPerformed Result) Read(NodeRecord record, NodeAttempt attempt, NodeDeclaration node, QuestView? quest, RequestPublished? refused, DateTimeOffset now)
    {
        var unit = record.Unit;
        var status = WorkPerformer.StatusOf(record, node.Id);
        var on = status.Attempt == attempt.Attempt && status.State is NodeStates.Active or NodeStates.Unknown;
        var taken = NodeOptions.Text(node, "until") == RequestOptions.Taken;

        string? outcome = null, code = null;
        if (refused is not null)
        {
            outcome = WorkStates.Failed;
            code = refused.Code ?? "refused";
            _said.Add($"work  {unit}: its request `{node.Id}` was not made: {refused.Message}");
        }
        else if (quest is not null)
        {
            if (quest.Status == "Declined")
            {
                outcome = WorkStates.Failed;
                code = RequestOptions.DeclinedCode;
                _said.Add($"work  {unit}: quest #{quest.Id} to `{quest.To}` was declined, so its request `{node.Id}` failed.");
            }
            else if (quest.Status == "Done" || taken && quest.Status == "Taken") outcome = WorkStates.Reached;
        }

        if (quest is null && refused is null) return (null, new(unit, node.Id, PerformedWhat.Running));
        if (outcome is null)
        {
            // Open or taken, and waited on: the attempt has its answer, and an unknown node is active again.
            var adopted = attempt with { State = AttemptStates.Settled, Code = null };
            var next = record;
            if (on && status.State == NodeStates.Unknown)
            {
                next = WorkPerformer.Kept(next, NodeLifecycle.Apply(node, status, new NodeMove(NodeStates.Active, NodeCause.Daoris) { Attempt = attempt.Attempt }), "active", now);
            }

            if (adopted == attempt && ReferenceEquals(next, record)) return (null, new(unit, node.Id, PerformedWhat.Running));
            return (WorkPerformer.With(next, attempt, adopted), new(unit, node.Id, attempt.State == AttemptStates.Settled ? PerformedWhat.Running : PerformedWhat.Sent));
        }

        var settled = attempt with { State = AttemptStates.Settled, Outcome = outcome, Code = code };
        if (!on)
        {
            var stale = WorkPerformer.With(record, attempt, settled);
            return (stale with { Statuses = WorkPerformer.With(stale.Statuses, NodeLifecycle.Reconciled(status, attempt.Attempt)) }, new(unit, node.Id, PerformedWhat.Stale));
        }

        var move = outcome == WorkStates.Reached
            ? new NodeMove(NodeStates.Reached, NodeCause.Authority)
            {
                Source = NodeSource.Commitment, Attempt = attempt.Attempt,
                Acceptance = status.Binding is { } binding
                    ? binding with { Evidence = new NodePort(node.Evidence ?? node.Kind, "quest:" + quest!.Id, binding.SubjectRevision, null) }
                    : null,
            }
            : new NodeMove(NodeStates.Failed, NodeCause.Authority) { Source = NodeSource.Commitment, Attempt = attempt.Attempt, Code = code };
        var moved = NodeLifecycle.Apply(node, status, move);
        if (!moved.Moved) return (WorkPerformer.With(record, attempt, settled), new(unit, node.Id, PerformedWhat.Refused, moved.Refused));
        return (WorkPerformer.With(WorkPerformer.Kept(record, moved, outcome, now), attempt, settled), new(unit, node.Id, PerformedWhat.Applied, outcome));
    }

    /// <summary>The host did not answer: the attempt and its node are unknown, and the next look sends the same key again.</summary>
    private WorkPerformed Unknown(string unit, NodeAttempt attempt, NodeDeclaration node)
    {
        var now = clock();
        var (unsent, _) = records.Edit<bool>(unit, record =>
        {
            if (record.Invoked(attempt.Call.Invocation) is not { State: AttemptStates.Recorded or AttemptStates.Unknown } current) return (null, false);
            var status = WorkPerformer.StatusOf(record, node.Id);
            if (status.State == NodeStates.Active && status.Attempt == current.Attempt)
            {
                record = WorkPerformer.Kept(record, NodeLifecycle.Apply(node, status, new NodeMove(NodeStates.Unknown, NodeCause.Daoris) { Attempt = current.Attempt }), "unknown", now);
            }

            return (WorkPerformer.With(record, current, current with { State = AttemptStates.Unknown }), true);
        });
        return new(unit, node.Id, PerformedWhat.Unknown, unsent ?? PerformedWhat.Unanswered);
    }
}
