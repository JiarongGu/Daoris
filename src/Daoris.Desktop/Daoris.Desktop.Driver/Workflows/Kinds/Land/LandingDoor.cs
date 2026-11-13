namespace Daoris.Driver;

/// <summary>The door a run's <c>land</c> is performed through, once per node and tip, landing off the pass through <see cref="LandPerformer.PerformAsync"/>
/// under the tree's hold and the delivery's key as every landing does, what it came to kept for the press that waits (<see cref="LandOutcomes"/>).</summary>
/// <param name="lander">Builds the performer for one landing; it lands as the review's Accept lands it, authorized by the review's word.</param>
public sealed class LandingDoor(Func<LandPerformer> lander, Action<string>? log = null, Func<bool>? runs = null, Func<LandStart, Task<string?>>? asks = null) : ILandingDoor, IDoorRuns
{
    private readonly NodeDoorRuns _runs = new(runs);

    NodeDoorRuns IDoorRuns.Runs => _runs;

    /// <inheritdoc/>
    public bool Start(LandStart start) => _runs.Start(start, _ => asks is not null && AskRunKey.IsBranchUnit(start.Branch) ? asks(start) : lander().PerformAsync(start), error =>
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
