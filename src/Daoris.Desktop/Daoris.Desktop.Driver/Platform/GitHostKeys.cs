using System.Text.Json;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// The SSH host-key fingerprints the public hosts publish (GITAUTH1c; the git-auth design §3.5), which a test compares a host's offered
/// key with before the person trusts it. Read from the tool resources' list (<c>resources.json</c>'s <c>hostKeys</c>, D121 §3), the
/// install's copy in <c>app/</c> first, else the one beside the driver, so the list updates without a release; an older build's
/// reader passes the key over. Each value is from its host's own documentation, read 2026-10-23.
/// </summary>
public static class GitHostKeys
{
    /// <summary>The list's key in <c>resources.json</c>.</summary>
    public const string Key = "hostKeys";

    /// <summary>The fingerprints a host publishes, or null where the list names none for it.</summary>
    public static IReadOnlyList<string>? Published(string host, string? home = null, string? baseDirectory = null)
    {
        var canonical = GitHosts.Canonical(host);
        foreach (var path in Lists(home ?? DaorisHome.Resolve(), baseDirectory ?? AppContext.BaseDirectory))
        {
            if (Read(path) is { } table) return table.TryGetValue(canonical, out var known) ? known : null;
        }

        return null;
    }

    /// <summary>The <c>hostKeys</c> table of a list's text, by canonical host; null where the list holds none.</summary>
    public static IReadOnlyDictionary<string, IReadOnlyList<string>>? Parse(string text)
    {
        try
        {
            if (JsonNode.Parse(text) is not JsonObject root || root[Key] is not JsonObject hosts) return null;
            var table = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
            foreach (var (host, value) in hosts)
            {
                if (value is not JsonArray list) continue;
                var prints = list.Select(each => each is JsonValue print && print.TryGetValue<string>(out var said) ? said : null)
                    .Where(each => each is not null && each.StartsWith("SHA256:", StringComparison.Ordinal)).Select(each => each!).ToList();
                if (prints.Count > 0) table[GitHosts.Canonical(host)] = prints;
            }

            return table;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static IEnumerable<string> Lists(string? home, string baseDirectory)
    {
        if (home is not null) yield return Path.Combine([DaorisHome.FolderOf(Path.GetFullPath(home)).Folder, .. ToolResources.Layout]);
        yield return Path.Combine(baseDirectory, ToolResources.FileName);
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<string>>? Read(string path)
    {
        try
        {
            return File.Exists(path) ? Parse(AtomicFile.ReadText(path)) : null;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}

/// <summary>
/// <c>driver.json</c> changed in place for a key <see cref="DriverConfig"/> does not model (GITAUTH1c's pointers): under the file's
/// own lock (<see cref="KeepLock"/>, CONFIGLOCK1), read as it stands, changed, and written whole, every other key kept as written.
/// </summary>
public static class DriverConfigFile
{
    /// <summary>Change the home's <c>driver.json</c>.</summary>
    /// <exception cref="DriverException">It does not read as an object, so nothing is written over it.</exception>
    public static void Change(string home, Action<JsonObject> change)
    {
        var path = Path.GetFullPath(DaorisHome.Entry(home, HomeEntry.DriverConfig));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var held = KeepLock.Take(path);
        JsonObject root;
        try
        {
            root = File.Exists(path)
                ? JsonNode.Parse(AtomicFile.ReadText(path)) as JsonObject ?? throw new DriverException($"git: {path} is not an object, so nothing was written (config-unread)")
                : new JsonObject();
        }
        catch (JsonException error)
        {
            throw new DriverException($"git: {path} does not read ({error.Message}), so nothing was written (config-unread)");
        }

        change(root);
        AtomicFile.WriteText(path, JsonFile.Serialize(root));
    }
}
