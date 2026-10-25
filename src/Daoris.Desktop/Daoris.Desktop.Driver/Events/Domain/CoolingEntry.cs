namespace Daoris.Driver;

/// <summary>
/// One account cooling (TOOL4d, D125 §2.3): until when, and what said so. Names only — never the agent's words, never a
/// key, never who signed in; the session id points at the record, which holds the words.
/// </summary>
/// <param name="Agent">Whose accounts these are: the account's owner (AGT7), so a door and its tool share one cool-off.</param>
/// <param name="Account">The profile's name, or null for the tool's own configuration home (kept as <c>""</c>).</param>
/// <param name="Until">When the account is offered again.</param>
/// <param name="Stated">Whether the agent named that time; false is Daoris's default standing in.</param>
/// <param name="Window">The window the reset named (<i>session</i>, <i>weekly</i>), or null.</param>
/// <param name="Seen">When the limit was read.</param>
/// <param name="Session">The session the limit refused, or null.</param>
/// <param name="AssumedZone">Whether the machine's zone stood in for the one the agent named.</param>
/// <param name="NotBelieved">Whether the agent named a date more than 8 days off, so the default stood in for it.</param>
public sealed record CoolingEntry(
    string Agent, string? Account, DateTimeOffset Until, bool Stated, string? Window, DateTimeOffset Seen, string? Session,
    bool AssumedZone = false, bool NotBelieved = false);
