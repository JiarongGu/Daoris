namespace Daoris.Driver;

/// <summary>
/// One stage of the driver's run of a driven session, registered with <see cref="RunStageAttribute"/> and built once per driver with
/// its wiring: a public constructor taking <see cref="IRunWiring"/>.
/// </summary>
internal interface IRunStage
{
    /// <summary>The run's end where this stage holds the start, which no stage after it sees; null goes on to the next.</summary>
    ValueTask<StartRun?> RunAsync(RunRound run, CancellationToken ct);
}
