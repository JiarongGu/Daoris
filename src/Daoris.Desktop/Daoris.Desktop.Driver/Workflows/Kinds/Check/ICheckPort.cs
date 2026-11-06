namespace Daoris.Driver;

/// <summary>
/// The port a <c>check</c>'s attempt is sent through by its kind, never by a plugin's name (FABRIC-PIPELINE1d; the pipeline design §4.2): the
/// heavy executor takes it, runs it on its own task, and answers the performer's reconcile from the attempt's folder.
/// </summary>
public interface ICheckPort : INodeDoor
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
