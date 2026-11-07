namespace Daoris.Driver;

/// <summary>
/// Whose take holds a quest that sits <see cref="StartVerdict.TakenElsewhere"/>, as facts: the page says the sentence in the
/// reader's language from them, never by reading the driver's English.
/// </summary>
/// <param name="Last">The session this machine last ran on the quest, which is not carried on over the take.</param>
/// <param name="Machine">The machine that took it, as a teammate's record names it; null where no record of theirs came, and for a take made here.</param>
/// <param name="Session">That teammate's session, as <c>origin/id</c>, where a record names one; null otherwise.</param>
/// <param name="Here">Taken on this machine after <paramref name="Last"/> ended, which no record here marks: a chat's or work outside Daoris.</param>
/// <param name="Reopened">The take is lost to the person's reopen of the work the step grew from, not to another machine.</param>
public sealed record TakenBy(string Last, string? Machine = null, string? Session = null, bool Here = false, bool Reopened = false);
