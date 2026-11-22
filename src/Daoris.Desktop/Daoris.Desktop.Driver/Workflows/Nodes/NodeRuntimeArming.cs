namespace Daoris.Driver;

/// <summary>What arms the evaluation runtime beyond its choices, and what withdrawing it clears.</summary>
public sealed partial class NodeRuntime
{
    /// <summary>
    /// Withdraws what the runtime holds once the choices no longer arm it: the awaited set told nothing, the timers and the governed units
    /// cleared. Called before the plan's supervision, so a plugin the last graph awaited is not started again by the look that reads that
    /// graph's removal. Returns whether the choices disarm the runtime.
    /// </summary>
    public async Task<bool> WithdrawAsync(DriverConfig config, CancellationToken ct)
    {
        if (KeepsClosures || ArmsRuns || OwesCleanup || new OwedRunEnds(records.Home).Any) return false;
        if (!Telling.Holding && _governed is null) return true;
        await _one.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            Withdraw();
        }
        finally
        {
            _one.Release();
        }

        return true;
    }

    /// <summary>
    /// Whether a plugin is owed a cleanup of what a run created: an ended run's temporary resources stay owed until their plugin settles them,
    /// so the runtime stays armed for the ask though no run is open.
    /// </summary>
    private bool OwesCleanup
    {
        get
        {
            var ended = _graphRuns.Read().Where(run => run.Ended is not null).Select(run => run.Unit).ToHashSet(StringComparer.Ordinal);
            return _cleaner.Owed(unit => ended.Contains(unit) ? true : Ended(unit)).Count > 0;
        }
    }

    /// <summary>
    /// Whether this loop closes quests and its home keeps a closure graph a quest is bound to: it stays armed whatever the choices say, so a
    /// close in flight is reconciled and a bound quest closed by its graph. One stat; a home that never bound a quest keeps none, and is
    /// judged by its choices alone.
    /// </summary>
    private bool KeepsClosures => _closer is not null && ClosureGraphs.Any(records.Home);

    /// <summary>
    /// Disarmed: the plugins are told nothing is awaited where something is, so their processes stop, and everything cached is cleared
    /// whatever is told, a pending retry's timer included, so nothing wakes the loop and a later arming reads the units afresh. Nothing is
    /// read. Called holding <see cref="_one"/>.
    /// </summary>
    private void Withdraw()
    {
        Telling.Withdraw();
        Owing.Forget();
        (_governed, _lastRead, _choices) = (null, null, null);
        _byTold = new Dictionary<string, GovernedUnit>(StringComparer.Ordinal);
        LastQuests = null;
        QuestReads.Delete(records.Home);
    }
}
