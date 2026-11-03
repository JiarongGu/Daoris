using System.Text.Json;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// Whether a home has moved off ambient credentials (GITAUTH1e, D198 point 17; the git-auth design §6.8), read by the doors from
/// <c>driver.json</c> alone; <see cref="GitMove"/> marks it and classes what follows.
/// </summary>
public static class GitMoved
{
    /// <summary>The key in <c>driver.json</c>; it holds no secret.</summary>
    public const string Key = "gitMoved";

    /// <summary>Whether this home has moved: a remote nothing serves is then reached with no credential of the person's.</summary>
    public static bool Moved(string home)
    {
        // transitional: git-moved-key
        try
        {
            var path = DaorisHome.Entry(home, HomeEntry.DriverConfig);
            return File.Exists(path) && JsonNode.Parse(AtomicFile.ReadText(path)) is JsonObject root && root[Key] is JsonObject;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException or DriverException)
        {
            return false;
        }
    }

    /// <summary>The line the ssh door refuses with where nothing serves a host once the home has moved.</summary>
    public static string SshLine(string host) =>
        $"daoris-git-ssh: {host} has no git identity on this machine, so Daoris's git has no key to reach it with: set one up in Settings → Git, or choose your own git sign-ins for it.";
}
