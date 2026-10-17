using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// An installed plugin that is a copy of one of Daoris's own example plugins (same id) and is not what this build ships
/// (LANDPIN3, D119): older than the shipped version, or the same version with files the person changed. The catalogue says so;
/// the update is the person's press, never Daoris's own.
/// </summary>
/// <param name="State"><see cref="Older"/> or <see cref="Changed"/>.</param>
/// <param name="Installed">The installed copy's version, as its manifest writes it.</param>
/// <param name="Shipped">The version this build ships.</param>
/// <param name="Changes">What the shipped version brings, in the example's own `changes` line; null where it says none.</param>
public sealed record PluginShipped(string State, string Installed, string Shipped, string? Changes)
{
    /// <summary>The installed copy is an older version than the one this build ships: an update is offered.</summary>
    public const string Older = "older";

    /// <summary>The installed copy is the shipped version with files that differ: said, and never overwritten by a press of ours.</summary>
    public const string Changed = "changed";

    /// <summary>
    /// What an installed copy is against the shipped one: null where it needs no word (the same version and files, or a newer
    /// version than this build ships, which is the person's own).
    /// </summary>
    /// <param name="sameFiles">Whether the installed files are the shipped ones; asked only where the versions are equal.</param>
    public static PluginShipped? Judge(string installed, string shipped, Func<bool> sameFiles, string? changes)
    {
        var order = Compare(installed, shipped);
        if (order < 0) return new(Older, installed, shipped, changes);
        return order == 0 && !sameFiles() ? new(Changed, installed, shipped, changes) : null;
    }

    /// <summary>Which of two versions is older: dotted numbers compared number by number, a missing part as zero; -1, 0 or 1.</summary>
    public static int Compare(string left, string right)
    {
        static long[] Parts(string version) =>
            [.. version.Split('.').Select(part => long.TryParse(part.Split('-', '+')[0], out var number) ? number : 0)];
        var (a, b) = (Parts(left), Parts(right));
        for (var at = 0; at < Math.Max(a.Length, b.Length); at++)
        {
            var order = (at < a.Length ? a[at] : 0).CompareTo(at < b.Length ? b[at] : 0);
            if (order != 0) return Math.Sign(order);
        }

        return 0;
    }

    /// <summary>The `changes` line an example's manifest text carries: its trimmed string, or null where it names none or the text does not read.</summary>
    public static string? ChangesIn(string manifestText)
    {
        try
        {
            using var document = JsonDocument.Parse(manifestText, new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
            return document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("changes", out var line) && line.ValueKind == JsonValueKind.String
                && line.GetString()!.Trim() is { Length: > 0 } text
                    ? text
                    : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
