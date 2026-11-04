namespace Daoris.Knowledge;

/// <summary>A unit as a press names it: its kind and its quest's or ask's id.</summary>
public sealed record HistoryUnitRef(HistoryUnitKind Kind, string Id)
{
    /// <summary>The kind's word on the wire: <c>quest</c>, <c>ask</c> or <c>failed</c>.</summary>
    public static string Spell(HistoryUnitKind kind) => kind.ToString().ToLowerInvariant();

    /// <summary>The reverse of <see cref="Spell"/>: null for a word that names no kind of unit.</summary>
    public static HistoryUnitKind? Parse(string? spelled) =>
        Enum.GetValues<HistoryUnitKind>()
            .Where(kind => string.Equals(Spell(kind), spelled?.Trim(), StringComparison.OrdinalIgnoreCase))
            .Select(kind => (HistoryUnitKind?)kind)
            .FirstOrDefault();
}
