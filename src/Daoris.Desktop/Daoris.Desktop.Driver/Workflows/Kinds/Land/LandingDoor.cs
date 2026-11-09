namespace Daoris.Driver;

/// <summary>
/// The door a run's <c>land</c> is performed through: the runtime starts it, once per node and
/// tip, and it lands the run's work off the pass, from the node's bound intent, through <see cref="LandPerformer.PerformAsync"/> over the session
/// trees, so the landing runs under the tree's hold and the delivery's key as every landing does. A press and a pass at once land one delivery.
/// What each landing came to is kept (<see cref="LandOutcomes"/>), for the press that waits to answer from.
/// </summary>
/// <param name="lander">Builds the performer for one landing; it lands as the review's Accept lands it, authorized by the review's word.</param>
/// <param name="log">Told a landing that could not be tried; the machine log's line.</param>
/// <param name="runs">Whether a landing may begin now: not while an update drains; null always may.</param>
public sealed class LandingDoor(Func<LandPerformer> lander, Action<string>? log = null, Func<bool>? runs = null) : ILandingDoor, IDoorRuns
{
    private readonly NodeDoorRuns _runs = new(runs);

    NodeDoorRuns IDoorRuns.Runs => _runs;

    /// <inheritdoc/>
    public bool Start(LandStart start) => _runs.Start(start, _ => lander().PerformAsync(start), error =>
    {
        if (error is not (DriverException or HttpRequestException or System.Text.Json.JsonException or Microsoft.Data.Sqlite.SqliteException or IOException
            or UnauthorizedAccessException))
        {
            return false;
        }

        log?.Invoke($"landing  run {start.Run}: its `land` could not be tried: {error.Message.TrimEnd().TrimEnd('.')}. It is tried again at a later pass.");
        return true;
    });

    /// <summary>Completes once every landing started has ended; a test's wait, and a closing loop's.</summary>
    public Task IdleAsync() => _runs.IdleAsync();
}
