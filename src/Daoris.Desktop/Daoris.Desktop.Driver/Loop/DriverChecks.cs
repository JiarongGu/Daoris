namespace Daoris.Driver;

/// <summary>
/// The check broker as the loop holds it: each look starts the broker's next check beside the look, never in it, so a session's turn has
/// ended while its check waits. Verdicts reach an unfinished quest's session as Daoris's word; a done quest's reds wait for the person.
/// </summary>
/// <remarks>
/// A session that asks <c>check_run</c> ends its turn <see cref="EndNoteCodes.EndedAwaitsCheck"/> (<see cref="WaitsOnOwnCheckAsync"/>);
/// a check ends reached, failed or stopped, and a stopped one is a verdict too.
/// </remarks>
internal sealed class DriverChecks
{
    private readonly LoopContext _context;
    private readonly DriverHelpers _helpers;
    private readonly CheckBroker _broker;
    private readonly Dictionary<string, string?> _folders = new(StringComparer.Ordinal);
    private readonly object _gate = new();
    private readonly CancellationTokenSource _closing = new();
    private Task _running = Task.CompletedTask;

    public DriverChecks(LoopContext context, DriverHelpers helpers)
    {
        _context = context;
        _helpers = helpers;
        _broker = new CheckBroker(context.Home, session =>
        {
            lock (_gate) return _folders.GetValueOrDefault(session);
        });
    }

    /// <summary>
    /// Whether a driven session whose turn ends now, holding its quest as a park would, waits on its own check: its agent asked one that has
    /// no verdict handed, and its last turn asked the person nothing, as a helper's wait reads it.
    /// </summary>
    internal async Task<bool> WaitsOnOwnCheckAsync(string session, DateTimeOffset since, CancellationToken ct)
    {
        var files = new CheckFiles(_context.Home);
        if (CheckWaits.Open(session, files.Asks(), files.ReadState).Count == 0) return false;
        return Exchanges.AsksPersonNothing(Exchanges.Of(await _helpers.OwnLookAsync(session, since, ct).ConfigureAwait(false), session), session);
    }

    /// <summary>One look: the hand-backs read, the asks' trees read, the next check started beside the look where none runs, the finished verdicts handed.</summary>
    internal async Task LookAsync(IEnumerable<PriorSession> sessions, CancellationToken ct)
    {
        await new DriverHandbacks(_context).LookAsync(sessions, ct).ConfigureAwait(false);
        var files = new CheckFiles(_context.Home);
        var asks = files.Asks();
        if (asks.Count == 0) return;

        foreach (var session in asks.Where(ask => !CheckStates.Finished(files.ReadState(ask.Id)?.State ?? CheckStates.Queued)).Select(ask => ask.Session).Distinct(StringComparer.Ordinal))
        {
            var tree = (await _context.Service.RecordAsync(session, ct).ConfigureAwait(false))?.Tree;
            lock (_gate) _folders[session] = tree is { Length: > 0 } ? new LinePlaces(_context.Home).Of(tree).Folder : null;
        }

        lock (_gate)
        {
            if (_running.IsCompleted) _running = Task.Run(RunAllAsync, CancellationToken.None);
        }

        await CheckHanding.HandAsync(files, SayAsync, ct, QuestStatusAsync).ConfigureAwait(false);
    }

    private async Task RunAllAsync()
    {
        try
        {
            while (await _broker.RunNextAsync(_closing.Token).ConfigureAwait(false) is { } state)
            {
                if (state.State == CheckStates.Errored)
                {
                    _context.Said.Retry("check", $"check `{state.Id}` could not be run by Daoris ({state.Error}); it is asked again in {CheckQueue.RetryAfter.TotalMinutes:0} minute.");
                }
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or DriverException or OperationCanceledException)
        {
            // The next look starts the broker again; what ran is on file as its verdict.
        }
    }

    private async Task<string?> SayAsync(string session, string text, string key, CancellationToken ct)
    {
        try
        {
            var said = await _context.Service.SayAsync(session, text, ct: ct, from: DaorisWords.From, key: key).ConfigureAwait(false);
            return said.Kept ? "handed" : null;
        }
        catch (Exception error) when (error is HttpRequestException or DriverException or System.Text.Json.JsonException)
        {
            return null;
        }
    }

    private async Task<string?> QuestStatusAsync(string session, CancellationToken ct)
    {
        var record = await _context.Service.RecordAsync(session, ct).ConfigureAwait(false);
        return (record?.Quest ?? record?.TookQuest) is { } quest
            ? (await _context.Service.FindQuestAsync(quest, ct).ConfigureAwait(false))?.Status
            : null;
    }
}
