namespace Daoris.Driver;

/// <summary>
/// A limit scoped to a model family (MODELROLE1k, D205 §14 row k): which family a reading or a limit's words name, and which starts a
/// family's cool-off holds. Pure; the families are the agent's own declaration (<see cref="WindowWords.Models"/>).
/// </summary>
/// <remarks>
/// A start whose model is the account default is held by every cool-off of the account: the agent chooses what it runs, and a start
/// sent into a spent family would be refused and cool the account again at every look. Only a start naming another family runs.
/// </remarks>
public static class ModelLimits
{
    /// <summary>Whether a cool-off of <paramref name="family"/> holds a start asking <paramref name="asked"/>; null asks the account default.</summary>
    public static bool Holds(string family, string? asked) => asked is null || Names(asked, family);

    /// <summary>
    /// Whether a model's id names the family as a word of it: <c>opus</c>, <c>claude-opus-4-5</c> and <c>opus[1m]</c> name <c>opus</c>;
    /// <c>opusplan</c>, a mode of its own, names none.
    /// </summary>
    public static bool Names(string model, string family) =>
        Words(model).Any(word => string.Equals(word, family, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// The family among <paramref name="families"/> that the words a limit was read with name as a word (<i>Opus</i> in <i>You've hit
    /// your Opus limit</i>): what was hit first, then the window its reset named. Null where none is named: the whole account's.
    /// </summary>
    public static string? In(LimitSeen seen, IReadOnlyList<string>? families) =>
        families is not { Count: > 0 } ? null
        : new[] { seen.Hit, seen.Window }.OfType<string>()
            .Select(said => families.FirstOrDefault(family => Names(said, family)))
            .FirstOrDefault(family => family is not null);

    /// <summary>
    /// The families whose week a reading puts at its limit (a whole 100%, its reset still ahead), each with that reset: Claude's
    /// <c>seven_day_opus</c> and <c>seven_day_sonnet</c>, as the usage history keeps them. A scope that is no declared family says nothing.
    /// </summary>
    public static IReadOnlyList<(string Family, DateTimeOffset Reset)> Spent(
        IReadOnlyList<WindowSeen> windows, IReadOnlyList<string>? families, DateTimeOffset now) =>
        families is not { Count: > 0 } ? []
        : [.. windows
            .Where(window => window.Walk is null && window.Scope is not null && window.Used is { } used && RotationWords.Whole(used) >= 100
                && window.Reset is { } reset && reset > now)
            .Select(window => (Family: families.FirstOrDefault(family => string.Equals(family, window.Scope, StringComparison.OrdinalIgnoreCase)), Reset: window.Reset!.Value))
            .Where(spent => spent.Family is not null)
            .Select(spent => (spent.Family!, spent.Reset))];

    private static IEnumerable<string> Words(string said)
    {
        var start = -1;
        for (var i = 0; i <= said.Length; i++)
        {
            var letter = i < said.Length && char.IsLetterOrDigit(said[i]);
            if (letter && start < 0) start = i;
            if (letter || start < 0) continue;
            yield return said[start..i];
            start = -1;
        }
    }
}
