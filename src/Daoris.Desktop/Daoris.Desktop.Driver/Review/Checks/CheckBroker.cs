namespace Daoris.Driver;

/// <summary>How a check is run: the heavy run's own door, <see cref="HeavyRun.RunAsync"/>, which a test stands in for.</summary>
public delegate Task<int> BrokeredRun(HeavyRunAsk ask, HeavyRunWorld world, TextWriter output, CancellationToken ct);

/// <summary>
/// The check broker: takes the next ask under the machine's admission and runs it as a heavy run in its session's tree, one at a time,
/// writing the verdict with the end of its log. A worker's turn ends while its check waits here (<see cref="EndNoteCodes.EndedAwaitsCheck"/>),
/// so the wait spends no tokens; <see cref="CheckHanding"/> carries the verdict back.
/// </summary>
/// <remarks>
/// <b>One at a time is this broker's own queue</b>, set at one so a build or a test is never doubled on this machine; the admission's capacity
/// (<see cref="MachineAdmission.CapacityVariable"/>) is the machine's and still holds the terminal's and the merge tool's runs beside it.
/// A run whose state says it runs but which this broker does not hold ended with a restart, and is queued again.
/// </remarks>
public sealed class CheckBroker(string home, Func<string, string?> folderOf, BrokeredRun? run = null)
{
    /// <summary>The most of a log's end a verdict keeps, in characters.</summary>
    public const int TailBound = 4000;

    private readonly CheckFiles _files = new(home);
    private readonly BrokeredRun _run = run ?? HeavyRun.RunAsync;
    private string? _holding;

    /// <summary>The ask this broker runs now, or null.</summary>
    public string? Holding => _holding;

    /// <summary>Run the next ask to its verdict; null where none waits, one runs, or another Daoris process on this home holds the seat.</summary>
    public async Task<CheckState?> RunNextAsync(CancellationToken ct)
    {
        using var seat = _files.TakeSeat();
        return seat is null ? null : await RunSeatedAsync(ct).ConfigureAwait(false);
    }

    private async Task<CheckState?> RunSeatedAsync(CancellationToken ct)
    {
        var asks = _files.Asks();
        foreach (var ask in asks.Where(each => _files.ReadState(each.Id) is { State: CheckStates.Running } && each.Id != _holding))
        {
            _files.WriteState(new CheckState(ask.Id, CheckStates.Queued, DateTimeOffset.UtcNow));
        }

        if (CheckQueue.Next(asks, _files.ReadState) is not { } next) return null;
        if (folderOf(next.Session) is not { Length: > 0 } folder || !Directory.Exists(folder))
        {
            return Keep(new CheckState(next.Id, CheckStates.Errored, DateTimeOffset.UtcNow) { Error = "its session has no tree on this machine to run in." });
        }

        var tip = await TipAsync(folder, ct).ConfigureAwait(false);
        _holding = next.Id;
        _files.WriteState(new CheckState(next.Id, CheckStates.Running, DateTimeOffset.UtcNow) { Tip = tip });
        try
        {
            int exit;
            await using (var log = _files.OpenLogWriter(next.Id))
            {
                var world = new HeavyRunWorld(home, folder, System.Environment.GetEnvironmentVariable) { Log = _files.PathOf(next.Id, "gate.log") };
                exit = await _run(new HeavyRunAsk(next.Gate)
                {
                    Command = next.Command,
                    Observed = next.By == CheckAsk.ByDaoris && next.Gate is not null && next.Command is null,
                }, world, log, ct).ConfigureAwait(false);
            }

            return Keep(new CheckState(next.Id, exit == 0 ? CheckStates.Reached : exit == 2 ? CheckStates.Errored : CheckStates.Failed, DateTimeOffset.UtcNow)
            {
                Exit = exit, Tail = TailOf(next.Id), Tip = tip, Error = exit == 2 ? TailOf(next.Id) : null,
            });
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return Keep(new CheckState(next.Id, CheckStates.Stopped, DateTimeOffset.UtcNow) { Tail = TailOf(next.Id), Tip = tip });
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or DriverException)
        {
            return Keep(new CheckState(next.Id, CheckStates.Errored, DateTimeOffset.UtcNow) { Error = error.Message, Tip = tip });
        }
        finally
        {
            _holding = null;
        }
    }

    private CheckState Keep(CheckState state)
    {
        _files.WriteState(state);
        return state;
    }

    private string? TailOf(string id)
    {
        if (_files.ReadLog(id) is not { } text) return null;
        if (text.Length <= TailBound) return text;
        var cut = text[^TailBound..];
        var line = cut.IndexOf('\n');
        return line >= 0 && line + 1 < cut.Length ? cut[(line + 1)..] : cut;
    }

    private static async Task<string?> TipAsync(string folder, CancellationToken ct)
    {
        var (code, tip, _) = await WorkingTree.GitAsync(folder, ["--no-optional-locks", "rev-parse", "HEAD"], ct).ConfigureAwait(false);
        return code == 0 && tip.Trim() is { Length: > 0 } read ? read : null;
    }
}
