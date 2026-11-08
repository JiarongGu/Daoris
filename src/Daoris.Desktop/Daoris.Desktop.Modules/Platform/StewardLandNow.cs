namespace Daoris.Desktop;

/// <summary>
/// A <c>land</c> of the steward's that a supervisor allowed and that waits its window as the window reads it: the request's id, which
/// <i>Overrule</i> names, the act, what it acts on (one of the three) and when it lands.
/// </summary>
public sealed record StewardLandNow(string Ask, string Act, string? Repository, string? Quest, string? Session, DateTimeOffset LandsAt);
