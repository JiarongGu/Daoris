using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// The machine's word about its plugins: which are disabled. A row, never a rename: the
/// plugin stays where it is, its data stays where it is, and both doors show it present and off.
/// </summary>
public sealed record PluginState(IReadOnlyList<string> Disabled)
{
    public const string FileName = "plugins.json";

    public static string PathIn(string home) => DaorisHome.Entry(home, HomeEntry.PluginState);

    public static PluginState Load(string home)
    {
        try
        {
            if (PluginFolders.TextIfThere(PathIn(home)) is not { } text) return new([]);

            using var document = JsonDocument.Parse(text);
            var disabled = new List<string>();
            if (document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("disabled", out var rows)
                && rows.ValueKind == JsonValueKind.Array)
            {
                foreach (var row in rows.EnumerateArray())
                {
                    if (row.ValueKind == JsonValueKind.String && row.GetString() is { Length: > 0 } id) disabled.Add(id);
                }
            }

            return new(disabled);
        }
        catch (Exception error) when (error is JsonException or IOException or UnauthorizedAccessException)
        {
            // An unreadable state file, torn or held by another process, disables nothing: the safe direction is the plugin the person
            // installed still running. An edit over it is refused rather than rewriting it whole.
            return new([]) { Problem = $"{FileName} could not be read ({error.Message})" };
        }
    }

    /// <summary>
    /// Why the file could not be read — null when it could, or when there is none. Reading it as
    /// "nothing disabled" keeps every installed plugin running, which is the safe direction for a READ;
    /// for an edit it would re-enable every plugin the person had switched off, so an edit is refused
    /// (the CLI twin refuses the same edit).
    /// </summary>
    public string? Problem { get; init; }

    public static void Disable(string home, string id)
    {
        var read = Editable(home);
        Save(home, read, [.. read.Disabled.Where(d => !Same(d, id)).Append(id)]);
    }

    public static void Enable(string home, string id)
    {
        var read = Editable(home);
        Save(home, read, [.. read.Disabled.Where(d => !Same(d, id))]);
    }

    /// <summary>A renamed plugin's row: switched off under its old id, it is off under its new one; false where the old id had none.</summary>
    public static bool Renamed(string home, string from, string to)
    {
        var read = Editable(home);
        if (!read.Disabled.Any(each => Same(each, from))) return false;
        Save(home, read, [.. read.Disabled.Where(each => !Same(each, from) && !Same(each, to)).Append(to)]);
        return true;
    }

    /// <exception cref="DriverException">The file could not be read, so an edit would rewrite it whole.</exception>
    internal static PluginState Editable(string home) =>
        Load(home) is { Problem: { } problem }
            ? throw new DriverException(
                $"{problem}, so this was not changed — writing it now would switch back on every plugin "
                + "it had switched off. Fix the file or remove it, then make the change again.")
            : Load(home);

    private static bool Same(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Written beside and renamed over, like every file Daoris owns: the edit from what the read found made onto the file as it stands
    /// (<see cref="JsonKeeping.Save"/>), so a key this build has no field for stays where it is, as written. Rebuilt from
    /// <c>disabled</c> alone, it would delete every other key the file holds.
    /// </summary>
    private static void Save(string home, PluginState read, IReadOnlyList<string> disabled) =>
        JsonKeeping.Save(PathIn(home), File.Exists(PathIn(home)), Written(read.Disabled), Written(disabled));

    /// <summary>The state's own shape: the disabled ids, sorted.</summary>
    private static string Written(IReadOnlyList<string> disabled) => JsonFile.Serialize(writer =>
    {
        writer.WriteStartObject();
        writer.WriteStartArray("disabled");
        foreach (var id in disabled.OrderBy(d => d, StringComparer.Ordinal)) writer.WriteStringValue(id);
        writer.WriteEndArray();
        writer.WriteEndObject();
    });
}
