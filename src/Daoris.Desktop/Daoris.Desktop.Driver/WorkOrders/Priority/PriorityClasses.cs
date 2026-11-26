namespace Daoris.Driver;

/// <summary>
/// The four classes a quest or an ask holds: words a wait line can say, never a number to inflate. Their order is
/// Daoris's, the same in every queue: urgent first, low last.
/// </summary>
public static class PriorityClasses
{
    public const string Urgent = "urgent";

    public const string High = "high";

    /// <summary>The class of whatever has none set, itself or through its ask.</summary>
    public const string Normal = "normal";

    public const string Low = "low";

    /// <summary>Every class, first to go first.</summary>
    public static IReadOnlyList<string> All { get; } = [Urgent, High, Normal, Low];

    /// <summary>The class's place in <see cref="All"/>; a word that is none ranks as <see cref="Normal"/>.</summary>
    public static int Rank(string? @class)
    {
        var at = All.ToList().IndexOf(@class ?? Normal);
        return at >= 0 ? at : All.ToList().IndexOf(Normal);
    }

    /// <summary>The class a word names, trimmed and in any case, or null where it names none.</summary>
    public static string? Parse(string? word) =>
        word?.Trim().ToLowerInvariant() is { } said && All.Contains(said) ? said : null;
}
