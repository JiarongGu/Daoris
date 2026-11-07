namespace Daoris.Driver;

/// <summary>
/// One account cooling: until when, and what said so. Names only — never the agent's words, never a
/// key, never who signed in; the session id points at the record, which holds the words.
/// </summary>
/// <param name="Agent">Whose accounts these are: the account's owner, so a door and its tool share one cool-off.</param>
/// <param name="Account">The profile's name, or null for the tool's own configuration home (kept as <c>""</c>).</param>
/// <param name="Until">When the account is offered again.</param>
/// <param name="Stated">Whether the agent named that time; false is Daoris's default standing in.</param>
/// <param name="Window">The window the reset named (<i>session</i>, <i>weekly</i>), or null.</param>
/// <param name="Seen">When the limit is read.</param>
/// <param name="Session">The session the limit refused, or null.</param>
/// <param name="AssumedZone">Whether the machine's zone stood in for the one the agent named.</param>
/// <param name="NotBelieved">Whether the agent named a date more than 8 days off, so the default stood in for it.</param>
/// <param name="Model">
/// The model family whose limit this is (<c>opus</c>, <c>sonnet</c>), kept as account plus model, so a start asking another family
/// still runs on the account (<see cref="ModelLimits"/>); null for the whole account's, which holds every start.
/// </param>
public sealed record CoolingEntry(
    string Agent, string? Account, DateTimeOffset Until, bool Stated, string? Window, DateTimeOffset Seen, string? Session,
    bool AssumedZone = false, bool NotBelieved = false, string? Model = null);
