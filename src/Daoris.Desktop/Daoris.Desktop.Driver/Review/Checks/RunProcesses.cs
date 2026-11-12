namespace Daoris.Driver;

/// <summary>A process left running at a hand-back: its id and name, and whether the run started it.</summary>
public sealed record LeftProcess(int Pid, string Name, bool Owned);

/// <summary>One process a run started, known by its id and the creation time that tells it from a later process reusing the id.</summary>
public sealed record OwnedProcess(string Run, int Pid, long Created);

/// <summary>
/// The processes each run started, kept by run, process id and creation time, and stopped at the run's end (the job-held processes by purpose are <see cref="OwnedProcesses"/>'s). An id whose creation time moved
/// is another process, so it is never stopped; a process the run did not start is named at the hand-back and never stopped.
/// </summary>
/// <param name="createdOf">The creation time (UTC ticks) of the process now holding an id, or null where none runs.</param>
/// <param name="kill">Stop one process and what it started.</param>
public sealed class RunProcesses(Func<int, long?> createdOf, Action<int> kill)
{
    private readonly List<OwnedProcess> _owned = [];
    private readonly object _gate = new();

    public void Track(string run, int pid, long created)
    {
        lock (_gate)
        {
            if (!_owned.Any(each => each.Run == run && each.Pid == pid && Same(each.Created, created))) _owned.Add(new OwnedProcess(run, pid, created));
        }
    }

    /// <summary>The processes <paramref name="run"/> started that are still the same process now.</summary>
    public IReadOnlyList<OwnedProcess> Standing(string run)
    {
        lock (_gate) return [.. _owned.Where(each => each.Run == run && createdOf(each.Pid) is { } created && Same(created, each.Created))];
    }

    /// <summary>Stop what <paramref name="run"/> started and forget it; the processes stopped.</summary>
    public IReadOnlyList<OwnedProcess> Stop(string run)
    {
        lock (_gate)
        {
            var standing = _owned.Where(each => each.Run == run && createdOf(each.Pid) is { } created && Same(created, each.Created)).ToList();
            foreach (var each in standing) kill(each.Pid);
            _owned.RemoveAll(each => each.Run == run);
            return standing;
        }
    }

    /// <summary>What <paramref name="found"/> running in the run's reach is, each marked as the run's own or not.</summary>
    public IReadOnlyList<LeftProcess> Left(string run, IEnumerable<(int Pid, string Name)> found)
    {
        var standing = Standing(run).Select(each => each.Pid).ToHashSet();
        return [.. found.Select(each => new LeftProcess(each.Pid, each.Name, standing.Contains(each.Pid)))];
    }

    private static bool Same(long a, long b) => Math.Abs(a - b) < TimeSpan.TicksPerSecond;
}
