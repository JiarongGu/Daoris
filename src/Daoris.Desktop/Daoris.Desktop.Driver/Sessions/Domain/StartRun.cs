namespace Daoris.Driver;

/// <summary>What one start came to (DEV3): a quest's session, or an ask's intake.</summary>
/// <param name="Line">The console line a person reads, which the report carries.</param>
/// <param name="Opened">Whether a session record was opened for it — what a look counts as progress.</param>
/// <param name="Ended">What it ended as and whose decision that was, when it ended here; null for a hold, a refusal or a park.</param>
/// <param name="Held">
/// The hold's own sentence when it was held before a record opened, which the report carries as the quest's verdict, and a
/// wait the start is among as its sentence: a quest's and an ask's intake's alike (UX6d1).
/// </param>
/// <param name="Cooling">The account's cool-off, when that is what held it (TOOL4d): what the report's waits are gathered from.</param>
internal sealed record StartRun(string Line, bool Opened, SessionEnded? Ended = null, string? Held = null, CoolingEntry? Cooling = null)
{
    /// <summary>The accounts a held start passed because they are not signed in (TOOL6g), or null.</summary>
    public SignedOutAccounts? SignedOut { get; init; }

    /// <summary>
    /// Its line's class as the pass that made it judged it (NOTIFY2h2): a meeting's, a brainstorm's or a helper's, said where the look
    /// drains it. Null for a session's start, whose end the watch announces, or the person's own where <see cref="Ended"/> says so.
    /// </summary>
    public LookClassed? Look { get; init; }
}
