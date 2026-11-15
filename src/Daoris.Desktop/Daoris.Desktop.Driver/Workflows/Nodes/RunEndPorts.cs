using Daoris.Contracts.Quests;

namespace Daoris.Driver;

/// <summary>The doors a run's end is written through, as its loop wires them (<see cref="RunSessionEnds"/>).</summary>
/// <param name="Home">The home its once-said reasons are kept by.</param>
public sealed record RunEndPorts(string Home)
{
    /// <summary>The write of its quests' Done and its sessions' end; null where no service door is wired.</summary>
    public Func<RunFinish, CancellationToken, Task<RunEndResponse?>>? Finish { get; init; }

    /// <summary>This machine's session records, read at each write to find those waiting on the run; null answered where they do not read.</summary>
    public Func<CancellationToken, Task<IReadOnlyList<SessionRecord>?>>? Sessions { get; init; }

    /// <summary>The machine log, by event and sentence.</summary>
    public Action<string, string>? Log { get; init; }

    /// <summary>Where an end the service could not take is kept until it does; null keeps none.</summary>
    public OwedRunEnds? Owed { get; init; }
}
