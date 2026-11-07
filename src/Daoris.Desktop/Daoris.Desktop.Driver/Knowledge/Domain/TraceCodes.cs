namespace Daoris.Driver;

/// <summary>The stores a trace reads, each a code the screen words and the terminal names in its sentence.</summary>
public static class TraceStores
{
    /// <summary>The service's session records.</summary>
    public const string Sessions = "sessions";

    /// <summary>The service's quests, as their operations replay.</summary>
    public const string Quests = "quests";

    /// <summary>The service's ask record.</summary>
    public const string Asks = "asks";

    /// <summary>This machine's record of a session.</summary>
    public const string Events = "events";

    /// <summary>The rules file the driver wrote under the home for a session.</summary>
    public const string Rules = "rules";

    /// <summary>This machine's <c>landings.json</c>.</summary>
    public const string Landings = "landings";

    /// <summary>This machine's <c>driver.json</c>, for the standing answers.</summary>
    public const string Config = "config";
}

// Why a link is missing, one family per kind of link, each a code the screen words where the link would
// have been. A family each, so each is a catalogue family of its own that a test holds to it (`work/trace.test.ts`).

/// <summary>Why an ask or a quest the chain names is missing.</summary>
public static class TraceLinkGaps
{
    /// <summary>Its store did not answer.</summary>
    public const string Unread = "unread";

    /// <summary>Its store holds none by the id the chain names.</summary>
    public const string NotFound = "not-found";
}

/// <summary>
/// Why a quest link carries no moves: codes of the moves' own, apart from a link's, since the moves are a part of
/// the quest's link and the page draws them from the service's door, not from the trace.
/// </summary>
public static class TraceMovesGaps
{
    /// <summary>The service's host is from before the moves' door, so its path is not kept there: no failure (trap 18).</summary>
    public const string NoDoor = "no-door";

    /// <summary>The moves' door did not answer, or answered something other than the moves.</summary>
    public const string Unread = "unread";
}

/// <summary>Why this machine's record of a session is missing.</summary>
public static class TraceEventGaps
{
    /// <summary>A session's id names no record on this machine.</summary>
    public const string NotAnId = "not-an-id";

    /// <summary>This machine holds no record of the session.</summary>
    public const string NoneHere = "none-here";

    /// <summary>The record here does not read.</summary>
    public const string Unread = "unread";
}

/// <summary>Why the rules a session is handed are missing.</summary>
public static class TraceRuleGaps
{
    /// <summary>A running session: no file, since its agent takes none (every Claude session is handed one).</summary>
    public const string NoneWhileRunning = "none-while-running";

    /// <summary>The file goes when its session ends, and a parked session's when its run does.</summary>
    public const string GoneWithRun = "gone-with-run";

    /// <summary>The file under the home does not read.</summary>
    public const string Unread = "unread";
}

/// <summary>Why no branch landing is named for a session.</summary>
public static class TraceLandingGaps
{
    /// <summary><c>landings.json</c> does not read.</summary>
    public const string Unread = "unread";

    /// <summary>No branch landing names it, and its record keeps no acceptance of its work.</summary>
    public const string None = "none";

    /// <summary>No branch landing names it, and no record of it here could say whether its work is accepted.</summary>
    public const string Unknown = "unknown";

    /// <summary>A landing into the line: its record keeps the person's acceptance, and the merge's own commit is not kept.</summary>
    public const string MergeAccepted = "merge-accepted";
}

/// <summary>Why what stood at a start is not read.</summary>
public static class TraceStoodGaps
{
    /// <summary>Its record says no moment it opened, so nothing is read against one.</summary>
    public const string NoMoment = "no-moment";
}

/// <summary>What names a link nothing holds (<see cref="TraceNamedBy"/>).</summary>
public static class TraceNamers
{
    /// <summary>A quest's sender names its ask.</summary>
    public const string Quest = "quest";

    /// <summary>An intake session's record names its ask.</summary>
    public const string Intake = "intake";

    /// <summary>A session's record names its quest.</summary>
    public const string Session = "session";

    /// <summary>A landing names its quest.</summary>
    public const string Landing = "landing";

    /// <summary>The trace itself names it.</summary>
    public const string Trace = "trace";
}

/// <summary>How a quest's done answered one requirement, as a trace reads it.</summary>
public static class TraceAnswers
{
    public const string Met = "met";
    public const string Departed = "departed";

    /// <summary>Its done carries no answer to it.</summary>
    public const string Unanswered = "unanswered";

    /// <summary>Not answered yet: a done answers it.</summary>
    public const string NotYet = "not-yet";
}

/// <summary>Whether a standing answer stood when a session started, by the moments kept and nothing else.</summary>
public static class TraceStandings
{
    /// <summary>None set now, and one cleared since is not kept.</summary>
    public const string None = "none";

    /// <summary>Set at a moment <c>driver.json</c> does not say, so whether it stood then is not known.</summary>
    public const string MomentUnknown = "moment-unknown";

    public const string Before = "before";

    /// <summary>Set after it started: what stood before is not kept, since <c>driver.json</c> keeps the latest.</summary>
    public const string After = "after";
}

/// <summary>Where a go-ahead stood when a session started, by the moments kept and nothing else.</summary>
public static class TraceGoAheadStands
{
    public const string AskedAfter = "asked-after";
    public const string Waiting = "waiting";
    public const string ApprovedBefore = "approved-before";
    public const string RefusedBefore = "refused-before";

    /// <summary>Answered after it started: whether an earlier answer stood then is not kept, since an answer replaces the one before.</summary>
    public const string AnsweredAfter = "answered-after";
}

/// <summary>Whether a session ran in the same tree as the record before it on its quest.</summary>
public static class TraceTrees
{
    public const string Same = "same";
    public const string Other = "other";

    /// <summary>Either record names no tree.</summary>
    public const string Unknown = "unknown";
}
