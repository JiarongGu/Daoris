namespace Daoris.Driver;

/// <summary>
/// How long one listing of the sessions took, stage by stage, into the machine log as <c>session.list</c>: the terminal's
/// <c>daoris session list</c> and the window's <c>SESSION_GROUPS</c> each write one line per look, so the stage that made a list take a
/// minute on an installed machine is read from the log, not guessed. Numbers and the door, never a word of a session.
/// </summary>
/// <remarks>
/// Each stage as <c>&lt;stage&gt;Ms</c>, in the order it ran: the look's own (<c>recordsMs</c>, <c>questsMs</c>, <c>verdictsMs</c>,
/// <c>marksMs</c>, <c>waitingMs</c>, <c>treesMs</c>), the trees as the facts' parts judged them where a Daoris answers (<c>judgedMs</c>),
/// a terminal alone's run waits, passages and its git walks of the trees of rows needing you (<c>runWaitsMs</c>, <c>passagesMs</c>,
/// <c>walksMs</c>, <c>walkedRowsMs</c>; <c>trees</c> counts the walks), and the listing's (<c>rowsMs</c>, <c>entriesMs</c>,
/// <c>openingsMs</c>, the events files' read, <c>linesMs</c>). Over <see cref="SlowMs"/> the line is a warning with <c>slow</c> true.
/// </remarks>
public static class SessionListLog
{
    /// <summary>The machine log's event.</summary>
    public const string Event = "session.list";

    /// <summary>Where a listing is called slow: the list is the window's first screen, so three seconds is a wait the person sees.</summary>
    public const long SlowMs = 3000;

    /// <summary>Write one line for a listing of <paramref name="sessions"/> records that took <paramref name="took"/> by <paramref name="door"/>.</summary>
    public static void Said(MachineLog? log, TimeSpan took, int sessions, string door, LookTimes times)
    {
        var ms = (long)took.TotalMilliseconds;
        var slow = ms > SlowMs;
        List<(string, object?)> data = [("ms", ms), ("slow", slow), ("sessions", sessions), ("door", door)];
        data.AddRange(times.Steps.Select(step => ($"{step.Step}Ms", (object?)step.Ms)));
        data.Add(("trees", times.Trees));
        if (slow) log?.Warn(Event, [.. data]);
        else log?.Info(Event, [.. data]);
    }
}
