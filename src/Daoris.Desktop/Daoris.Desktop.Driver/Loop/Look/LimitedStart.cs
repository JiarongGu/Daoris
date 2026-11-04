namespace Daoris.Driver;

/// <summary>
/// A start of a quest whose last part an account's limit ended: it says once that it waits and once that it went on, as the line's own
/// news, coded <see cref="LookSaid.LimitWaits"/> and keyed by that part.
/// </summary>
internal static class LimitedStart
{
    /// <summary>The line said where a line a limit cut off went on: a carry-on is a new part handed the brief.</summary>
    public const string WentOnLine = "work an account's limit cut off went on in a new session, handed Daoris's brief.";

    /// <summary>The line said where a line a limit cut off resumed its own conversation on its own account.</summary>
    public const string ResumedLine = "work an account's limit cut off resumed in its own conversation, its account ready again.";

    /// <summary>Null for any other start. A start the person's words make is theirs, never said back, so it is none.</summary>
    public static LookNews? LineOf(StartBegun started) =>
        started is { Quest: { } quest, Start: Consideration { GoesOn: false, Resumes: { Limit: true } prior } }
            ? new LookNews(LookSaid.LimitWaits, prior.Session, prior.Session, quest, started.Workspace)
            : null;
}
