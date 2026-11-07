namespace Daoris.Driver;

/// <summary>
/// The part of a line before a carry-on, as its record says it ran: the harness it opened on, that harness's maker
/// where this build knows it, and the models its door reported (its <c>ran</c>), none where it reported none.
/// </summary>
public sealed record EarlierMaker(string Adapter, string? Maker, IReadOnlyList<string> Models);
