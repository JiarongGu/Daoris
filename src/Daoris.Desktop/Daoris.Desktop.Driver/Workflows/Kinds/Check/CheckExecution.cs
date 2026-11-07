namespace Daoris.Driver;

/// <summary>
/// The heavy executor behind the check's port: an attempt becomes a <see cref="VerifySend"/> for its run (what to verify,
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
