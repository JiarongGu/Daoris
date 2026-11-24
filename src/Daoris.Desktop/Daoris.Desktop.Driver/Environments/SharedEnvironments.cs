namespace Daoris.Driver;

/// <summary>
/// What runs an environment's instances on this machine. The one seam to the machine's processes: the real host (the
/// repository's own start command run in a tree of the version, with the instance's ports in its environment) is a later slice.
/// </summary>
public interface IEnvironmentProcesses
{
    /// <summary>Runs <paramref name="instance"/> and returns once it answers; where one of its id already runs at another version, replaces it. Throws where it does not come up.</summary>
    Task StartAsync(EnvironmentInstance instance, EnvironmentSpec spec, CancellationToken cancel);

    /// <summary>Stops <paramref name="instance"/>.</summary>
    Task StopAsync(EnvironmentInstance instance, CancellationToken cancel);
}

/// <summary>How a lease asked ended.</summary>
public enum EnvironmentOutcome
{
    /// <summary>The session holds a lease on a running instance.</summary>
    Leased,

    /// <summary>The machine would not run another instance, or had no ports for it (<see cref="EnvironmentLeased.Refusal"/>).</summary>
    Refused,

    /// <summary>The instance did not come up; nothing is kept (<see cref="EnvironmentLeased.Said"/>).</summary>
    StartFailed,
}

/// <summary>A lease asked, as the session is told.</summary>
public sealed record EnvironmentLeased(EnvironmentOutcome Outcome, EnvironmentInstance? Instance, EnvironmentRefusal? Refusal, string? Said);

/// <summary>
/// The machine's running environments as sessions use them: one <see cref="EnvironmentLedger"/> kept behind one gate, and the
/// processes it asks for. A lease starts an instance only where the ledger says it must run, and keeps no trace of one that did not come
/// up; a sweep takes the stopped instances out of the ledger first and then stops them, so a stop that fails leaves a process the ledger
/// no longer names, which the host's own reconciliation (a later slice) finds by the mark it gave the process.
/// </summary>
public sealed class SharedEnvironments(IEnvironmentProcesses processes, TimeProvider clock, PortPool pool, int capacity)
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private EnvironmentLedger ledger = EnvironmentLedger.Empty(pool);

    /// <summary>How many instances run: what the machine's admission counts.</summary>
    public int Running => ledger.Count;

    /// <summary>The sessions that lease <paramref name="instanceId"/>, in the order they took it.</summary>
    public IReadOnlyList<string> Holders(string instanceId) => ledger.Holders(instanceId);

    /// <summary>Leases <paramref name="use"/>'s instance, running it first where it is new or has moved to a new version.</summary>
    public async Task<EnvironmentLeased> LeaseAsync(EnvironmentUse use, CancellationToken cancel)
    {
        await gate.WaitAsync(cancel).ConfigureAwait(false);
        try
        {
            var answer = ledger.Lease(use, clock.GetUtcNow(), capacity);
            if (answer.Refusal is { } refusal) return new(EnvironmentOutcome.Refused, null, refusal, null);
            if (answer.Starts)
            {
                try
                {
                    await processes.StartAsync(answer.Instance!, use.Spec, cancel).ConfigureAwait(false);
                }
                catch (Exception failed) when (failed is not OperationCanceledException)
                {
                    return new(EnvironmentOutcome.StartFailed, null, null, failed.Message);
                }
            }

            ledger = answer.Ledger;
            return new(EnvironmentOutcome.Leased, answer.Instance, null, null);
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>Lets go every lease <paramref name="session"/> holds; what it is last to hold waits out its grace.</summary>
    public async Task ReleaseAsync(string session, CancellationToken cancel)
    {
        await gate.WaitAsync(cancel).ConfigureAwait(false);
        try
        {
            ledger = ledger.Release(session, clock.GetUtcNow());
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>Stops every instance no lease holds whose grace has ended.</summary>
    public async Task SweepAsync(CancellationToken cancel)
    {
        await gate.WaitAsync(cancel).ConfigureAwait(false);
        try
        {
            var swept = ledger.Sweep(clock.GetUtcNow());
            ledger = swept.Ledger;
            foreach (var instance in swept.Stopped) await processes.StopAsync(instance, cancel).ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }
}
