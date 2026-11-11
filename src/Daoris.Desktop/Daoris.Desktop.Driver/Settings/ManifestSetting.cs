using System.Globalization;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// How many files and how much time a folder's manifest read spends before the place is too large to follow: <c>manifest.files</c>
/// and <c>manifest.minutes</c> in <c>driver.json</c>, the one setting the read's <see cref="ManifestBounds"/> are taken from. Each is
/// written only when set, so the list tells the default from a choice.
/// </summary>
/// <param name="Files">The most files a manifest lists, or null for the default.</param>
/// <param name="Minutes">The most minutes a manifest's read takes, or null for the default.</param>
public sealed record ManifestSetting(int? Files = null, int? Minutes = null)
{
    private static readonly MachineSetting Declared = MachineSettings.Of("manifest")!;

    /// <summary>Its field in <c>driver.json</c>.</summary>
    public const string File = "manifest";

    /// <summary>The fewest files it takes: less would call an ordinary folder too large.</summary>
    public const int FilesLeast = 1_000;

    /// <summary>The most files it takes.</summary>
    public const int FilesMost = 10_000_000;

    /// <summary>The fewest minutes it takes, as the list declares it.</summary>
    public static int MinutesLeast => Declared.Least!.Value;

    /// <summary>The most minutes it takes, as the list declares it.</summary>
    public static int MinutesMost => Declared.Most!.Value;

    /// <summary>The refusal every door says for a count of files out of its bounds.</summary>
    public static readonly string FilesRefusal = string.Create(CultureInfo.InvariantCulture,
        $"a folder's manifest is bounded by a whole number of files from {FilesLeast} to {FilesMost} — e.g. `daoris settings manifest files 100000`.");

    /// <summary>The refusal every door says for minutes out of their bounds.</summary>
    public static readonly string MinutesRefusal = string.Create(CultureInfo.InvariantCulture,
        $"a folder's manifest is bounded by a whole number of minutes from {MinutesLeast} to {MinutesMost} — e.g. `daoris settings manifest minutes 3`.");

    /// <summary>The refusal of a press naming both bounds or neither: one is set at a time.</summary>
    public const string OneRefusal = "a folder's manifest takes one bound at a time: `daoris settings manifest files <n>` or `daoris settings manifest minutes <n>`.";

    /// <summary>Nothing set: both bounds are the default.</summary>
    public static ManifestSetting Unset { get; } = new();

    /// <summary>Whether a count of files is one the setting takes.</summary>
    public static bool TakesFiles(long files) => files is >= FilesLeast and <= FilesMost;

    /// <summary>Whether a count of minutes is one the setting takes.</summary>
    public static bool TakesMinutes(long minutes) => minutes >= MinutesLeast && minutes <= MinutesMost;

    /// <summary>The bounds a manifest read takes: each as set, or the default.</summary>
    public ManifestBounds Bounds => new(Files ?? ManifestBounds.Default.Files, Minutes is { } minutes ? TimeSpan.FromMinutes(minutes) : ManifestBounds.Default.Time);

    /// <summary>The setting of a parsed <c>driver.json</c>: each bound a whole number in its bounds, or the default.</summary>
    public static ManifestSetting Read(JsonElement root) =>
        root.TryGetProperty(File, out var node) && node.ValueKind == JsonValueKind.Object
            ? new(Whole(node, "files") is { } files && TakesFiles(files) ? (int)files : null, Whole(node, "minutes") is { } minutes && TakesMinutes(minutes) ? (int)minutes : null)
            : Unset;

    /// <summary>Write the setting's object, or nothing where neither bound is set.</summary>
    public void Write(Utf8JsonWriter writer)
    {
        if (this == Unset) return;
        writer.WriteStartObject(File);
        if (Files is { } files) writer.WriteNumber("files", files);
        if (Minutes is { } minutes) writer.WriteNumber("minutes", minutes);
        writer.WriteEndObject();
    }

    private static long? Whole(JsonElement node, string name) =>
        node.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var whole) ? whole : null;
}
