namespace Daoris.Driver;

/// <summary>
/// The port a <c>check</c>'s attempt is sent through by its kind, never by a plugin's name (FABRIC-PIPELINE1d; the pipeline design §4.2): the
/// heavy executor takes it, runs it on its own task, and answers the performer's reconcile from the attempt's folder.
/// </summary>
public interface ICheckPort
{
    /// <summary>Hands the attempt over and returns once it is taken.</summary>
    /// <exception cref="DriverException">It cannot be taken: its outcome is unknown until reconciled.</exception>
    Task SendAsync(string unit, NodeAttempt attempt, NodeDeclaration node, CancellationToken ct);

    /// <summary>What became of the attempt, read from its folder.</summary>
    Task<WorkReconcileAnswer> ReconcileAsync(string unit, NodeAttempt attempt, CancellationToken ct);

    /// <summary>Whether the executor has said the attempt has an answer: the only time a check sent here is asked after.</summary>
    bool Answered(string invocation);

    /// <summary>The person's stop: the attempt's job is ended, as at its bound.</summary>
    void Stop(string invocation);
}

/// <summary>A <c>check</c> as the runtime performs it: its kind's <c>perform</c> and its bound, which no author writes (the pipeline design §4.1).</summary>
public static class CheckNodes
{
    /// <summary>Reconciled: its answer is the executor's, read from the attempt's folder; never retried by itself.</summary>
    public static readonly NodePerform Perform = new(Idempotent: false, Reconcile: true, Transient: []);

    /// <summary>The declaration as performed: its kind's perform, and 7200 seconds where it declares no <c>timeoutSeconds</c>.</summary>
    public static NodeDeclaration Performed(NodeDeclaration node) =>
        node with { Perform = Perform, TimeoutSeconds = node.TimeoutSeconds ?? (int)HeavyExecutor.DefaultBound.TotalSeconds };

    /// <summary>The gates a check names (its <c>gates</c> option): null for <c>heavy</c>, else the names it lists.</summary>
    public static IReadOnlyList<string>? GatesOf(NodeDeclaration node)
    {
        if (!node.Options.TryGetValue("gates", out var text)) return null;
        try
        {
            using var document = System.Text.Json.JsonDocument.Parse(text);
            var root = document.RootElement;
            if (root.ValueKind == System.Text.Json.JsonValueKind.String) return root.GetString() == "heavy" ? null : [root.GetString()!];
            return root.ValueKind == System.Text.Json.JsonValueKind.Array
                ? [.. root.EnumerateArray().Where(each => each.ValueKind == System.Text.Json.JsonValueKind.String).Select(each => each.GetString()!)]
                : null;
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }
}

/// <summary>
/// The heavy executor behind the check's port (FABRIC-PIPELINE1d): an attempt becomes a <see cref="VerifySend"/> for its run (what to verify,
/// where, under which accepted declaration), and the executor's answer the performer's reconcile answer.
/// </summary>
/// <param name="sendOf">What the attempt verifies, read from its run's binding; null where the run names nothing to verify.</param>
public sealed class CheckExecution(HeavyExecutor executor, Func<string, NodeAttempt, NodeDeclaration, VerifySend?> sendOf) : ICheckPort
{
    public async Task SendAsync(string unit, NodeAttempt attempt, NodeDeclaration node, CancellationToken ct)
    {
        var send = sendOf(unit, attempt, node) ?? throw new DriverException($"{unit} names no repository and commit to verify");
        await executor.SendAsync(send).ConfigureAwait(false);
    }

    public async Task<WorkReconcileAnswer> ReconcileAsync(string unit, NodeAttempt attempt, CancellationToken ct) =>
        AnswerOf(await executor.ReconcileAsync(RunOf(unit), attempt.Call.Invocation).ConfigureAwait(false));

    public bool Answered(string invocation) => executor.Answered(invocation);

    public void Stop(string invocation) => executor.Stop(invocation);

    /// <summary>The run a <c>run:&lt;run&gt;</c> unit names.</summary>
    public static string RunOf(string unit) => RunUnits.Is(unit) ? unit[RunUnits.Prefix.Length..] : unit;

    /// <summary>The executor's answer as the reconcile reads it: a result as its verdict, else running, not started or unknown.</summary>
    public static WorkReconcileAnswer AnswerOf(VerifyAnswer answer) => answer.State switch
    {
        VerifyAnswers.Result when answer.Result is { } result => AnswerOf(result),
        VerifyAnswers.Running => new(ReconcileStates.Running),
        VerifyAnswers.NotStarted => new(ReconcileStates.NotStarted),
        _ => new(ReconcileStates.Unknown, new WorkOutcome(WorkStates.Unknown, WorkStates.Unknown)),
    };

    /// <summary>
    /// A result as its verdict: reached or failed as <c>done</c>, its evidence the result by its invocation (<c>gates:&lt;invocation&gt;</c>);
    /// unknown as <c>unknown</c> with its reason's code, which the performer holds for the person.
    /// </summary>
    public static WorkReconcileAnswer AnswerOf(VerifyResult result) => result.Verdict switch
    {
        VerifyVerdicts.Reached => new(ReconcileStates.Done, new WorkOutcome(WorkStates.Reached, Evidence: Evidence(result, "verified"))),
        VerifyVerdicts.Failed => new(ReconcileStates.Done, new WorkOutcome(WorkStates.Failed, HeavyExecutor.GateFailed, Evidence(result, $"failed: {result.Failed}"))),
        _ => new(ReconcileStates.Unknown, new WorkOutcome(WorkStates.Unknown, result.Code ?? WorkStates.Unknown)),
    };

    private static WorkEvidence Evidence(VerifyResult result, string said) => new($"{NodeSource.Gates}:{result.Invocation}", null, null, said);
}
