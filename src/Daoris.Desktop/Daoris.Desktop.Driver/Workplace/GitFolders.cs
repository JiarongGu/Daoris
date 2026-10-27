using System.Text.Json;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// Which repository a git act's working folder belongs to, from the files alone (GITAUTH1c; the git-auth design §4.2 step 1): the
/// door is started by git in the act's own folder, so a <c>git -C</c> into another repository resolves for itself, and it reads no
/// variable to know it. A session's tree is named by the layout Daoris chose (<c>work/trees/&lt;workspace&gt;/&lt;repository&gt;/&lt;name&gt;</c>);
/// a checkout by <c>run/git/checkouts.json</c>, the registry's roots as the driver last read them (<see cref="Remember"/>). A folder in
/// neither is a repository Daoris does not know, which resolves by the machine's steps alone (§3.2).
/// </summary>
/// <remarks>The base library alone: the door compiles this in.</remarks>
public static class GitFolders
{
    /// <summary>The repository and workspace <paramref name="folder"/> belongs to, or nulls where Daoris knows it as none.</summary>
    public static (string? Repository, string? Workspace) RepositoryOf(string home, string folder)
    {
        string full;
        try
        {
            full = Path.GetFullPath(folder);
        }
        catch (Exception error) when (error is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return (null, null);
        }

        var trees = Path.GetFullPath(DaorisHome.Entry(home, HomeEntry.Trees));
        if (full.StartsWith(trees + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            var parts = Path.GetRelativePath(trees, full).Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return parts.Length >= 3 ? (parts[1], parts[0]) : (null, null);
        }

        (string Root, string Repository, string Workspace)? found = null;
        foreach (var each in Read(home))
        {
            var root = Path.TrimEndingDirectorySeparator(each.Root);
            var inside = string.Equals(full, root, StringComparison.OrdinalIgnoreCase)
                || full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
            if (inside && (found is null || root.Length > found.Value.Root.Length)) found = (root, each.Repository, each.Workspace);
        }

        return found is { } hit ? (hit.Repository, hit.Workspace) : (null, null);
    }

    /// <summary>The checkouts the driver last remembered: each root with its repository and workspace.</summary>
    public static IReadOnlyList<(string Root, string Repository, string Workspace)> Read(string home)
    {
        try
        {
            var path = DaorisHome.Entry(home, HomeEntry.GitCheckouts);
            if (!File.Exists(path) || JsonNode.Parse(AtomicFile.ReadText(path)) is not JsonObject root || root["checkouts"] is not JsonArray list) return [];
            var kept = new List<(string, string, string)>();
            foreach (var item in list)
            {
                if (item is JsonObject each && (string?)each["root"] is { Length: > 0 } at && (string?)each["repository"] is { Length: > 0 } repository)
                    kept.Add((at, repository, (string?)each["workspace"] ?? ""));
            }

            return kept;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException or DriverException)
        {
            return [];
        }
    }

    /// <summary>
    /// Keep the registry's checkouts for the doors (a machine-local record, written whole where it changed): each root with its
    /// repository and workspace.
    /// </summary>
    public static void Remember(string home, IEnumerable<(string Root, string Repository, string Workspace)> checkouts)
    {
        var list = new JsonArray();
        foreach (var (root, repository, workspace) in checkouts.OrderBy(each => each.Repository, StringComparer.Ordinal).ThenBy(each => each.Root, StringComparer.Ordinal))
        {
            list.Add(new JsonObject { ["root"] = Path.GetFullPath(root), ["repository"] = repository, ["workspace"] = workspace });
        }

        try
        {
            var path = DaorisHome.Entry(home, HomeEntry.GitCheckouts);
            var text = JsonFile.Serialize(new JsonObject { ["checkouts"] = list });
            if (File.Exists(path) && AtomicFile.ReadText(path) == text) return;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            AtomicFile.WriteText(path, text);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or DriverException)
        {
            // Kept again at the next read of the registry.
        }
    }
}
