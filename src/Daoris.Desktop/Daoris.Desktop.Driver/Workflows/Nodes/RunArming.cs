namespace Daoris.Driver;

/// <summary>What keeps the evaluation runtime armed for the runs bound to graph versions.</summary>
internal static class RunArming
{
    /// <summary>
    /// Whether a bound run arms the runtime: one not ended, one its chain asked a pass for, one failed at a step an observed fact may still
    /// settle, and every one at the first look. A run whose version no longer reads arms nothing: it is held whole, and said.
    /// </summary>
    public static bool Arms(IEnumerable<GraphRun> runs, bool restarted, Func<string, bool> returned, Func<string, NodeRecord?> record) =>
        runs.Any(run => run.Version.Readable && (run.Ended is null || restarted || returned(run.Run) || FailedHeldRuns.Arms(run, record))
            // A conversation's run is owed a pass only once a press submitted a candidate, and again at the next one.
            && (!ChatRuns.IsChatRun(run.Run) || (run.Candidate?.Newest is { } candidate
                && !string.Equals(candidate.IdIn(run.Run), run.Ended?.Quest, StringComparison.Ordinal)))
            || run.Version.Readable && run.Candidate?.Newest is { } next && !string.Equals(next.IdIn(run.Run), run.Ended?.Quest, StringComparison.Ordinal));

    /// <summary>
    /// Whether a plugin is owed a cleanup of what a run created: an ended run's temporary resources stay owed until their plugin settles them,
    /// so the runtime stays armed for the ask though no run is open.
    /// </summary>
    public static bool OwesCleanup(IEnumerable<GraphRun> runs, ResourceCleaner cleaner, Func<string, bool?> ended)
    {
        var over = runs.Where(run => run.Ended is not null).Select(run => run.Unit).ToHashSet(StringComparer.Ordinal);
        return cleaner.Owed(unit => over.Contains(unit) ? true : ended(unit)).Count > 0;
    }
}
