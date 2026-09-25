using System.Text.Json;
using static Daoris.Knowledge.JsonFields;

namespace Daoris.Knowledge;

/// <summary>
/// Where a workspace's remote is and how this machine speaks to it — machine-local configuration,
/// never per-repository (D47 §9, D48 §5): one file under the Daoris home, with the environment
/// overriding.
/// </summary>
/// <remarks>
/// <para>The file is the home's `remotes.json` (D63) — a MAP, `{ "aurora": { "url": "...", "key": "dk_..." } }` —
/// because one shared deployment serves one workspace (D48 §5), and a machine may hold repositories
/// from several circles. The workspace NAME keys the map; whether a given repository may feed at all
/// stays its own manifest's `remote` declaration: the manifest says MAY, the machine says WHERE.</para>
///
/// <para>It is not tracked by any repository, which is what `sensitive-info` requires of a credential;
/// the OS secret store is held as D47's open question 3. `daoris remote list|add|remove` is the
/// surface over it (D50) — and hand-editing keeps working, because the file is the truth.</para>
/// </remarks>
public sealed record RemoteConfig(string Url, string Key)
{
    public const string UrlVariable = "DAORIS_REMOTE_URL";
    public const string KeyVariable = "DAORIS_REMOTE_KEY";

    /// <summary>Which workspace the environment pair serves. Absent is <see cref="Workspaces.Default"/>.</summary>
    public const string WorkspaceVariable = "DAORIS_REMOTE_WORKSPACE";

    public const string PathVariable = "DAORIS_REMOTE_CONFIG";

    /// <summary>
    /// The map's conventional home — under the Daoris home (D63), and null where there is none: a
    /// machine with no home has no remotes, which is the documented default anyway.
    /// </summary>
    public static string? DefaultPath => DaorisHome.File("remotes.json");

    /// <summary>This machine's remotes, by workspace. Absence is the default and it is silent (D21).</summary>
    public static IReadOnlyDictionary<string, RemoteConfig> Load() => Load(
        Environment.GetEnvironmentVariable,
        Environment.GetEnvironmentVariable(PathVariable) ?? DefaultPath);

    /// <summary>
    /// The testable shape: the same judgement over injected surroundings.
    /// </summary>
    /// <remarks>
    /// Either environment variable present means the environment IS the answer — for the WHOLE
    /// MACHINE, not one entry of it, and a half-set pair is no remote at all. Never a mix of an env
    /// URL with the file's key, which would quietly aim one machine's key at another's host; and never
    /// a merge, which would let a real map leak into a process that thought it had named its only
    /// remote. <see cref="WorkspaceVariable"/> names which circle the pair serves.
    /// </remarks>
    public static IReadOnlyDictionary<string, RemoteConfig> Load(Func<string, string?> environment, string? path)
    {
        var map = new Dictionary<string, RemoteConfig>(StringComparer.OrdinalIgnoreCase);
        var url = environment(UrlVariable);
        var key = environment(KeyVariable);

        if (!string.IsNullOrWhiteSpace(url) || !string.IsNullOrWhiteSpace(key))
        {
            if (!string.IsNullOrWhiteSpace(url) && !string.IsNullOrWhiteSpace(key))
            {
                map[Workspaces.Normalize(environment(WorkspaceVariable))] = new(url.TrimEnd('/'), key);
            }

            return map;
        }

        if (!File.Exists(path)) return map;

        // A file that will not parse is a file that names no remote. The sync loop, not this reader, is
        // where "you configured a remote and it does not work" gets said out loud.
        if (ParseObject(File.ReadAllText(path)) is not { } root) return map;

        foreach (var entry in root.EnumerateObject())
        {
            if (entry.Value.ValueKind != JsonValueKind.Object) continue;
            var entryUrl = Text(entry.Value, "url");
            var entryKey = Text(entry.Value, "key");
            // An entry missing half its pair is one workspace with no remote, never a machine with
            // none: a typo in one circle must not silently unwire the others.
            if (string.IsNullOrWhiteSpace(entryUrl) || string.IsNullOrWhiteSpace(entryKey)) continue;

            map[Workspaces.Normalize(entry.Name)] = new(entryUrl.TrimEnd('/'), entryKey);
        }

        return map;
    }
}
