using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>A check door whose every check is green: a test of what comes after a graph's <c>check</c> step passes it on its next pass.</summary>
internal sealed class GreenChecks : ICheckPort
{
    public Task SendAsync(string unit, NodeAttempt attempt, NodeDeclaration node, CancellationToken ct) => Task.CompletedTask;

    public Task<WorkReconcileAnswer> ReconcileAsync(string unit, NodeAttempt attempt, CancellationToken ct) =>
        Task.FromResult(new WorkReconcileAnswer(ReconcileStates.Done, new WorkOutcome(WorkStates.Reached)));

    public bool Answered(string invocation) => true;

    public void Supersede(string invocation, string why) => Stop(invocation);

    public void Stop(string invocation)
    {
    }
}
