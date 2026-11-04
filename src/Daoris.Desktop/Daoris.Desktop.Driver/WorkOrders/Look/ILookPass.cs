namespace Daoris.Driver;

/// <summary>
/// One pass of the driver's look, registered with <see cref="LookPassAttribute"/> and built once per driver with its wiring: a
/// public constructor taking <see cref="ILookWiring"/>, or a wiring interface of its module's own that extends it.
/// </summary>
/// <remarks>
/// A pass decides for itself whether this look runs it, and times its own work under its name in <see cref="ILookWiring.Stages"/>,
/// so a pass with nothing to do leaves no span, and work it leaves beside the look is timed there, on its own task.
/// </remarks>
internal interface ILookPass
{
    Task RunAsync(LookRound round, CancellationToken ct);
}
