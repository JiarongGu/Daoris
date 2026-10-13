using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>
/// One of Daoris's own plugins the install offers (PLUG9 d, D103), read as the catalogue reads a plugin,
/// its placeholders as written.
/// </summary>
/// <param name="Problem">Why it could not be installed as it stands, in the catalogue's words; null when sound.</param>
/// <param name="Needs">What it needs on the machine: its README's own requirement lines.</param>
/// <param name="Installed">Whether a plugin of this id is installed here already.</param>
public sealed partial record PluginOffer(
    string Id, string Folder, PluginManifest Manifest, string? Problem, IReadOnlyList<string> Needs, bool Installed)
{
    /// <summary>The heading in an offer's README whose bullets say what it needs. Twin: the CLI's <c>NEEDS_HEADING</c>.</summary>
    public const string NeedsHeading = "## What it needs";

    /// <summary>
    /// What a plugin needs on the machine, in its README's words: the bullets under <see cref="NeedsHeading"/>,
    /// a wrapped bullet joined into one line. Emphasis is dropped, since neither door draws it; code is kept.
    /// </summary>
    public static IReadOnlyList<string> NeedsIn(string readme)
    {
        var lines = readme.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var start = Array.FindIndex(lines, line => line.TrimEnd() == NeedsHeading);
        if (start < 0) return [];

        var needs = new List<string>();
        foreach (var raw in lines.Skip(start + 1))
        {
            if (raw.StartsWith('#')) break;
            var line = raw.Trim();
            if (line.Length == 0) continue;
            if (line.StartsWith("- ", StringComparison.Ordinal) || line.StartsWith("* ", StringComparison.Ordinal)) needs.Add(line[2..].Trim());
            else if (needs.Count > 0 && char.IsWhiteSpace(raw[0])) needs[^1] += $" {line}";
        }

        return [.. needs.Select(need => Bold().Replace(need, "$1"))];
    }

    [GeneratedRegex(@"\*\*([^*]+)\*\*")]
    private static partial Regex Bold();
}
