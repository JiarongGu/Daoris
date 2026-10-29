namespace Daoris.Driver;

/// <summary>
/// This process's run of the desktop store (FOUNDATION1f-i, D212; the foundation design §13.4's <c>store_runs</c>, §19.1): a run id drawn once
/// at the process's start and the kind of process it is, owned here, below every family's store, so the bindings and the move record write
/// under one run per process and a move's claim names the run its executor's process is.
/// </summary>
/// <remarks>
/// The kind is <see cref="Verb"/> until a composition root names its process: the shell's window names <see cref="Shell"/> at its start and the
/// headless loop <see cref="Host"/> once its verb is read; every terminal verb stays a verb. Row z-iii kept these on the bindings' store, the
/// first family to write a run; the move store, the second, reads the same pair, so <c>store_runs</c> holds the process once.
/// </remarks>
public static class StoreProcess
{
    /// <summary>The shell: the window's process, its modules and the driver library in one.</summary>
    public const string Shell = "shell";

    /// <summary>The headless loop, <c>daoris-driver drive</c>, the instance's or a <c>--share</c> loop beside it.</summary>
    public const string Host = "host";

    /// <summary>A terminal verb, or any process no root has named.</summary>
    public const string Verb = "verb";

    /// <summary>This process's run, drawn once: what every open of the store here is recorded under, and every claim names.</summary>
    public static string Run { get; } = Guid.NewGuid().ToString("N");

    /// <summary>What this process is, for its run: <see cref="Verb"/> until a composition root says it is a shell or a host.</summary>
    public static string Kind { get; set; } = Verb;

    /// <summary>The options a family opens the store with for one use: this process's run and kind, and the reading of the start's confirmation handed.</summary>
    public static DesktopStoreOptions Options(Func<StoreSettled>? settled) => new() { Kind = Kind, Run = Run, Settled = settled };
}
