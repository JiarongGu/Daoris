using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// The machine's word about its plugins: which are disabled. A row, never a rename (D64 §5) — the
/// plugin stays where it is, its data stays where it is, and both doors show it present and off.
/// </summary>
public sealed record PluginState(IReadOnlyList<string> Disabled)
{
    public const string FileName = "plugins.json";

    public static string PathIn(string home) => DaorisHome.Entry(home, HomeEntry.PluginState);

    public static PluginState Load(string home)
    {
        if (PluginFolders.TextIfThere(PathIn(home)) is not { } text) return new([]);

        try
        {
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
        catch (JsonException error)
        {
            // An unreadable state file disables nothing: the safe direction is the plugin the person
            // installed still running. An edit over it is refused rather than rewriting it whole.
            return new([]) { Problem = $"{FileName} could not be read ({error.Message})" };
        }
    }

    /// <summary>
    /// Why the file could not be read — null when it could, or when there was none. Reading it as
    /// "nothing disabled" keeps every installed plugin running, which is the safe direction for a READ;
    /// for an edit it would re-enable every plugin the person had switched off, so an edit is refused
    /// (REV3, and the CLI twin refuses the same edit).
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

    /// <exception cref="DriverException">The file could not be read, so an edit would rewrite it whole.</exception>
    private static PluginState Editable(string home) =>
        Load(home) is { Problem: { } problem }
            ? throw new DriverException(
                $"{problem}, so this was not changed — writing it now would switch back on every plugin "
                + "it had switched off. Fix the file or remove it, then make the change again.")
            : Load(home);

    private static bool Same(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Written beside and renamed over, like every file Daoris owns: the edit from what was read made onto the file as it stands
    /// (KEEPALL1, <see cref="JsonKeeping.Save"/>), so a key this build has no field for stays where it is, as written. Rebuilt from
    /// <c>disabled</c> alone, it deleted every other key the file held. The CLI's <c>writePluginState</c> does the same.
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
