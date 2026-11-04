namespace Daoris.Driver;

/// <summary>
/// The starts one look reserves and begins at once: sessions in different repositories, or in trees of their own, run together. Each is
/// reserved first, its quest or ask, the tree it goes back into and its place under the cap, so a look that runs before its record
/// opens neither plans it again nor gives its place. The look then waits for each until it has opened its record or come to nothing,
/// so a hold, a refusal and an error before the spawn are still this look's to say; but under the watch only until something else
/// wants the loop: a start still preparing (git under a scan, an account's probe, a plugin slow to answer) runs on beside the look,
/// reserved, and what it comes to joins a later look's report. The rest of each run is the running set's.
/// </summary>
internal sealed class BegunStarts(IStartsWiring wiring, List<string> events, Dictionary<string, string> goingOn)
{
    private readonly List<(StartBegun Started, LookStages.StageSpan Span, StartReservation Reserved, Task<StartCame> Came)> _beginning = [];

    /// <summary>
    /// One start reserved, then begun. The planner holds a reserved quest, ask and tree, so a reservation refused here is a start
    /// outside the look's plan reaching the same work first: said, and nothing begun twice.
    /// </summary>
    public void Begin(StartBegun started, StartReservation reserved, Func<List<TrustHold>, Action, Task<StartRun>> start)
    {
        if (!wiring.Runs.TryReserve(reserved))
        {
            events.Add(wiring.Lines.Progress(
                "starting", $"starting  {started.Where}: a start of this machine's is already opening its session, so none is begun beside it."));
            if (started.Quest is { } quest && goingOn.TryGetValue(quest, out var session)) wiring.Runs.WentOn(session);
            return;
        }

        var span = wiring.Stages.Begin("start", started.Quest is { } named ? $"#{named}" : $"ask#{started.Ask}");
        _beginning.Add((started, span, reserved, BeginAsync(started, span, reserved, start)));
    }

    /// <summary>
    /// The starts, each to its record open or to what it came to. Under the watch only until something else wants the loop; each
    /// still preparing then runs on beside the look, reserved, and what it comes to is carried to a later look's report. A start that
    /// threw before its record opened fails the look.
    /// </summary>
    public async Task<IReadOnlyList<StartCame>> SettledAsync(CancellationToken ct)
    {
        if (_beginning.Count == 0) return [];
        var all = Task.WhenAll(_beginning.Select(each => each.Came));
        if (wiring.Wanted is not { } wanted) return await all.ConfigureAwait(false);

        using (var waiting = CancellationTokenSource.CreateLinkedTokenSource(ct))
        {
            var first = await Task.WhenAny(all, wanted(waiting.Token)).ConfigureAwait(false);
            await waiting.CancelAsync().ConfigureAwait(false);
            if (first == all) return await all.ConfigureAwait(false);
        }

        // Partitioned once: a start that settles after this is carried, never said twice.
        var settled = new List<Task<StartCame>>();
        foreach (var each in _beginning)
        {
            if (each.Came.IsCompleted)
            {
                settled.Add(each.Came);
                continue;
            }

            each.Span.Aside();
            wiring.Runs.Carry(each.Started, each.Came);
        }

        var came = new List<StartCame>();
        foreach (var task in settled) came.Add(await task.ConfigureAwait(false));
        return came;
    }

    /// <summary>
    /// Each start's preparation, from its holds to its record open or to what it came to, timed with its quest or ask. Its reservation
    /// is let go the moment its record opens, when the ledger holds it, or once it came to nothing. Run as its span's own work, so its
    /// git waiting for a place is said as its queue; the look that holds the span is not.
    /// </summary>
    private async Task<StartCame> BeginAsync(
        StartBegun started, LookStages.StageSpan span, StartReservation reserved, Func<List<TrustHold>, Action, Task<StartRun>> start)
    {
        var own = new List<TrustHold>();
        try
        {
            var came = await span.RunAsync(() => wiring.Runs.StartAsync(opened => start(own, () =>
            {
                wiring.Runs.Release(reserved);
                opened();
            }))).ConfigureAwait(false);
            return new StartCame(started, came, own);
        }
        finally
        {
            wiring.Runs.Release(reserved);
            span.Dispose();
        }
    }
}
