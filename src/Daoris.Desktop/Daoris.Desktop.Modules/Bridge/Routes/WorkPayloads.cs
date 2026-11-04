namespace Daoris.Desktop;

// A work route names exactly one of an ask and a quest. A terminal's verbs send <c>Said</c>, read as the page's flags are.

/// <summary>The abandon's list for an ask or a quest.</summary>
public sealed record WorkPlanRead(string? Ask, string? Quest, bool Said = false);

/// <summary>The abandon; <c>Pieces</c> are the keys the list held.</summary>
public sealed record WorkAbandonPress(string? Ask, string? Quest, string? Reason, IReadOnlyList<string>? Pieces, bool Said = false);

/// <summary>A pause or a resume of an ask's or a quest's work.</summary>
public sealed record WorkPausePress(string? Ask, string? Quest, bool Said = false);
