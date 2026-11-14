namespace Daoris.Driver;

/// <summary>
/// Where a heavy run runs: the home its slot is under, the folder it runs from and its environment, read through a seam; and, for a
/// terminal's run in the running Daoris (<see cref="HeavyTerminalRun"/>), the terminal it is for and the environment and handles its
/// gates are started with.
/// </summary>
/// <param name="Home">The home whose <c>locks/</c> holds the slot.</param>
/// <param name="Directory">The folder the run starts in; its checkout is the nearest above holding a <c>.git</c>.</param>
/// <param name="Variable">A variable of the run's environment, or null.</param>
public sealed record HeavyRunWorld(string Home, string Directory, Func<string, string?> Variable)
{
    /// <summary>How the run waits for the slot.</summary>
    public HeavyWaiting Waiting { get; init; } = HeavyWaiting.Default;

    /// <summary>How the run waits for the machine's admission.</summary>
    public AdmissionWaiting Admission { get; init; } = AdmissionWaiting.Default;

    /// <summary>Whom the slot names as holding or waiting: this process, unless the run is a terminal's in the running Daoris.</summary>
    public HeavyAsker Asker { get; init; } = HeavyAsker.ThisProcess();

    /// <summary>The whole environment the gates start in, with the run's own variables over it; null for this process's.</summary>
    public IReadOnlyDictionary<string, string>? Environment { get; init; }

    /// <summary>The standard handles the gates are handed; null for this process's own.</summary>
    public TerminalHandles? Streams { get; init; }

    /// <summary>A separate file for the gate's output, copied into the run's writer after each step.</summary>
    internal string? Log { get; init; }

    /// <summary>How each gate is started: as this process's own unless the world names an environment or handles.</summary>
    internal StepOptions? Steps => Environment is null && Streams is null && Log is null ? null : new StepOptions(Environment, Log) { Streams = Streams };

    /// <summary>This process's: its folder and its environment.</summary>
    public static HeavyRunWorld OfThisProcess(string home) => new(home, System.Environment.CurrentDirectory, System.Environment.GetEnvironmentVariable);
}
