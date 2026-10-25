namespace Daoris.Driver;

/// <summary>
/// The names of Ask Daoris's access mode as <c>driver.json</c>'s <c>helpMode</c> keeps them (HELPMODE1a1, D183 point 1): the settings'
/// own reading of the key, which Help's <see cref="HelpModes"/> answers through, so the key reads and is spelled the same wherever a
/// mode is named.
/// </summary>
public static class HelpModeNames
{
    /// <summary>It reads, advises and proposes, and the person applies (D89): the default.</summary>
    public const string Restricted = "restricted";

    /// <summary>It acts on this machine itself, in its harness's mode that asks nothing (D183 point 1).</summary>
    public const string Full = "full";

    /// <summary>The modes, in the order a person is offered them.</summary>
    public static IReadOnlyList<string> All { get; } = [Restricted, Full];

    /// <summary>The mode <paramref name="value"/> names, trimmed and in any case, or null for none.</summary>
    public static string? Named(string? value) =>
        All.FirstOrDefault(mode => string.Equals(mode, value?.Trim(), StringComparison.OrdinalIgnoreCase));
}
