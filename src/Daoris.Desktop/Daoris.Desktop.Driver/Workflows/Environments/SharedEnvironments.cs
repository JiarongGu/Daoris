namespace Daoris.Driver;

/// <summary>
/// What runs an environment's instances on this machine. The one seam to the machine's processes: the real host (the
/// repository's own start command run in a tree of the version, with the instance's ports in its environment) is a later slice.
/// </summary>
public interface IEnvironmentProcesses
{
    /// <summary>
    /// Runs <paramref name="instance"/> and returns once it answers; where one of its id already runs at another version, replaces it.
    /// Throws, or honours <paramref name="cancel"/>, where it does not come up in time.
    /// </summary>
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

    /// <summary>The instance did not come up in time; it and every lease on it are gone (<see cref="EnvironmentLeased.Said"/>).</summary>
    StartFailed,
}

/// <summary>A lease asked, as the session is told.</summary>
public sealed record EnvironmentLeased(EnvironmentOutcome Outcome, EnvironmentInstance? Instance, EnvironmentRefusal? Refusal, string? Said);

/// <summary>
/// The machine's running environments as sessions use them: one <see cref="EnvironmentLedger"/> kept behind one gate, and the
/// processes it asks for. The gate is held for the ledger's judgement only, never across a process: a start runs outside it, under a
/// deadline, and a session that leases an instance still starting waits on that start, not on the gate. A start that fails (a move of
/// a branch's instance included, which may have stopped the old process) drops the instance and every lease on it, so the next lease
/// starts it again. A sweep stops each expired instance on its own and says the ones that would not stop.
/// </summary>
public sealed class SharedEnvironments(IEnvironmentProcesses processes, TimeProvider clock, PortPool pool, int capacity, TimeSpan? startDeadline = null)
{
    /// <summary>How long a start may take where the caller names none.</summary>
    public static readonly TimeSpan DefaultStartDeadline = TimeSpan.FromMinutes(5);

    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly Dictionary<string, Task> starting = [];
    private readonly TimeSpan deadline = startDeadline ?? DefaultStartDeadline;
    private EnvironmentLedger ledger = EnvironmentLedger.Empty(pool);

    /// <summary>How many instances run: what the machine's admission counts.</summary>
    public int Running => ledger.Count;

    /// <summary>The sessions that lease <paramref name="instanceId"/>, in the order they took it.</summary>
    public IReadOnlyList<string> Holders(string instanceId) => ledger.Holders(instanceId);

    /// <summary>
    /// Leases <paramref name="use"/>'s instance, running it first where it is new or has moved to a new version. Where the admission is
    /// full, an unheld instance in its grace is stopped to make room.
    /// </summary>
    public async Task<EnvironmentLeased> LeaseAsync(EnvironmentUse use, CancellationToken cancel)
    {
        EnvironmentLeaseAnswer answer;
        Task? start;
        await gate.WaitAsync(cancel).ConfigureAwait(false);
        try
        {
            answer = ledger.Lease(use, clock.GetUtcNow(), capacity);
            if (answer.Refusal is { } refusal) return new(EnvironmentOutcome.Refused, null, refusal, null);
            ledger = answer.Ledger;
            var id = answer.Instance!.Id;
            starting.TryGetValue(id, out var running);
            var instance = answer.Instance;
            start = answer.Starts ? Task.Run(() => Start(instance, use.Spec, running), CancellationToken.None) : running;
            if (answer.Starts) starting[id] = start!;
        }
        finally
        {
            gate.Release();
        }

        foreach (var evicted in answer.Evicted) await StopQuietlyAsync(evicted, cancel).ConfigureAwait(false);
        if (start is null) return new(EnvironmentOutcome.Leased, answer.Instance, null, null);
        try
        {
            await start.WaitAsync(cancel).ConfigureAwait(false);
        }
        catch (Exception failed) when (failed is not OperationCanceledException || !cancel.IsCancellationRequested)
        {
            await DropAsync(answer.Instance!.Id, start, cancel).ConfigureAwait(false);
            return new(EnvironmentOutcome.StartFailed, null, null, failed is OperationCanceledException ? $"it did not come up in {deadline}" : failed.Message);
        }

        return new(EnvironmentOutcome.Leased, answer.Instance, null, null);
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

    /// <summary>Stops every instance no lease holds whose grace has ended, each on its own, and says the ones that would not stop.</summary>
    /// <returns>One sentence for each instance whose stop failed; empty where all stopped.</returns>
    public async Task<IReadOnlyList<string>> SweepAsync(CancellationToken cancel)
    {
        EnvironmentSweep swept;
        await gate.WaitAsync(cancel).ConfigureAwait(false);
        try
        {
            swept = ledger.Sweep(clock.GetUtcNow());
            ledger = swept.Ledger;
        }
        finally
        {
            gate.Release();
        }

        var failures = new List<string>();
        foreach (var instance in swept.Stopped)
        {
            if (await StopQuietlyAsync(instance, cancel).ConfigureAwait(false) is { } said) failures.Add(said);
        }

        return failures;
    }

    /// <summary>Runs the start under the deadline, after one still running of the same id has ended, however it ended.</summary>
    private async Task Start(EnvironmentInstance instance, EnvironmentSpec spec, Task? earlier)
    {
        if (earlier is not null)
        {
            try
            {
                await earlier.ConfigureAwait(false);
            }
            catch (Exception)
            {
                // the earlier start's own caller is told how it ended; this one starts over whatever it left
            }
        }

        using var limit = new CancellationTokenSource(deadline);
        await processes.StartAsync(instance, spec, limit.Token).ConfigureAwait(false);
    }

    private async Task DropAsync(string instanceId, Task failed, CancellationToken cancel)
    {
        await gate.WaitAsync(cancel).ConfigureAwait(false);
        try
        {
            if (!starting.TryGetValue(instanceId, out var current) || !ReferenceEquals(current, failed)) return;
            starting.Remove(instanceId);
            ledger = ledger.Drop(instanceId);
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<string?> StopQuietlyAsync(EnvironmentInstance instance, CancellationToken cancel)
    {
        try
        {
            await processes.StopAsync(instance, cancel).ConfigureAwait(false);
            return null;
        }
        catch (Exception failed) when (failed is not OperationCanceledException)
        {
            return $"{instance.Id}: {failed.Message}";
        }
    }
}
