using System.Text.Json;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// One part of a release's migration: it converts its own records of the home to the shape this build reads, and only
/// those. Run again over what it converted, it changes nothing, so a start that stopped mid-way runs it whole once more.
/// </summary>
public interface IHomeMigrationStep
{
    /// <summary>Its name in the migration's record, by which a later start knows it ran.</summary>
    string Name { get; }

    /// <summary>Convert the home's records, each file through <paramref name="run"/>, which keeps a copy before the first change.</summary>
    void Run(HomeMigrationRun run);
}

/// <summary>
/// What a step converts through: the home, and the one way a file is changed, a copy taken into the salvage folder before
/// the first write so the copy is always the file as the older build left it.
/// </summary>
public sealed class HomeMigrationRun(string home, string migration)
{
    private readonly List<string> _changed = [];

    private readonly SortedDictionary<string, int> _counts = new(StringComparer.Ordinal);

    public string Home => home;

    /// <summary>The files changed, relative to the Daoris folder, in the order they were.</summary>
    public IReadOnlyList<string> Changed => _changed;

    /// <summary>What a step counted of what it converted, by <c>&lt;step&gt;.&lt;what&gt;</c>: what the machine log says beside the files.</summary>
    public IReadOnlyDictionary<string, int> Counts => _counts;

    public void Count(string step, string what) => _counts[$"{step}.{what}"] = _counts.GetValueOrDefault($"{step}.{what}") + 1;

    /// <summary>Where this migration's copies go: one folder per migration, the Daoris folder's layout under it.</summary>
    public string Copies => DaorisHome.Entry(home, HomeEntry.Salvage, migration);

    /// <summary>Copy <paramref name="path"/> into the salvage folder once (<see cref="SalvageCopy"/>); a copy already there stays, holding the file as the older build left it.</summary>
    public void Keep(string path)
    {
        if (!File.Exists(path)) return;
        var target = Path.Combine(Copies, Relative(path));
        if (File.Exists(target)) return;
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        SalvageCopy.Into(path, target);
    }

    /// <summary>Write <paramref name="text"/> over <paramref name="path"/>, the file kept first.</summary>
    public void Write(string path, string text)
    {
        Keep(path);
        AtomicFile.WriteText(path, text);
        Said(path);
    }

    /// <summary>
    /// A JSON object file changed in place: <paramref name="change"/> says whether it changed anything, and only then is the file kept and
    /// written. A file that is gone or does not read as an object is left as it is.
    /// </summary>
    public void ChangeJson(string path, Func<JsonObject, bool> change)
    {
        if (!File.Exists(path)) return;
        JsonObject root;
        try
        {
            if (JsonNode.Parse(AtomicFile.ReadText(path)) is not JsonObject read) return;
            root = read;
        }
        catch (JsonException)
        {
            return;
        }

        if (change(root)) Write(path, JsonFile.Serialize(root));
    }

    /// <summary>A node's text where it is a string, else null: an older record's odd value is left as it is rather than stopping the run.</summary>
    public static string? Text(JsonNode? node) => node is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;

    /// <summary>Name a file changed in place by its owner's own writer, kept first through <see cref="Keep"/>.</summary>
    public void Said(string path)
    {
        var relative = Relative(path);
        if (!_changed.Contains(relative, StringComparer.Ordinal)) _changed.Add(relative);
    }

    private string Relative(string path)
    {
        var folder = DaorisHome.FolderOf(home).Folder;
        var relative = Path.GetRelativePath(folder, Path.GetFullPath(path));
        return (relative.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(relative) ? Path.Combine("outside", Path.GetFileName(path)) : relative)
            .Replace('\\', '/');
    }
}
