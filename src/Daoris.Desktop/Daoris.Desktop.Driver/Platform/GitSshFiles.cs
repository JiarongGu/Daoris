using System.Text;

namespace Daoris.Driver;

/// <summary>
/// Daoris's own ssh configuration and known hosts (GITAUTH1c, D198 point 4; the git-auth design §3.5 and §4.2):
/// <c>profiles/git/ssh_config</c>, which the ssh door hands its ssh by <c>-F</c> so the person's own configuration is never read, and
/// <c>profiles/git/known_hosts</c>, the only host keys an identity's connection trusts. A host's key enters the file only at a
/// person's press (D73), never on first use in the background: ssh is told <c>StrictHostKeyChecking yes</c> and
/// <c>UpdateHostKeys no</c>, so ssh itself writes nothing there.
/// </summary>
/// <remarks>The paths and the door's arguments are the base library alone: the door compiles this in.</remarks>
public static class GitSshFiles
{
    /// <summary>The ssh configuration's path.</summary>
    public static string Config(string home) => DaorisHome.Entry(home, HomeEntry.GitSshConfig);

    /// <summary>The known hosts' path.</summary>
    public static string KnownHosts(string home) => DaorisHome.Entry(home, HomeEntry.GitKnownHosts);

    /// <summary>A path as ssh reads it on the command line and in its configuration: forward slashes.</summary>
    public static string Slashed(string path) => path.Replace('\\', '/');

    /// <summary>A path inside an <c>-o</c> option, quoted where it holds a space (ssh splits an option's value as a config line).</summary>
    public static string Option(string path) => Slashed(path) is var slashed && slashed.Contains(' ') ? $"\"{slashed}\"" : Slashed(path);

    /// <summary>The configuration's text: every option the door also gives, so a test's own ssh reads the same.</summary>
    public static string ConfigText(string home) =>
        "# Written by Daoris (GITAUTH1c, D198). Read by Daoris's ssh door with -F, so your own ssh configuration is never read.\n"
        + "# Rewritten by Daoris: a change here is not kept.\n"
        + "Host *\n"
        + "\tBatchMode yes\n"
        + "\tIdentitiesOnly yes\n"
        + "\tIdentityAgent none\n"
        + "\tForwardAgent no\n"
        + "\tStrictHostKeyChecking yes\n"
        + "\tUpdateHostKeys no\n"
        + "\tGlobalKnownHostsFile none\n"
        + $"\tUserKnownHostsFile {Option(KnownHosts(home))}\n";

    /// <summary>Write the configuration where it is not as Daoris writes it, and make the known hosts' file where it is absent.</summary>
    public static void Ensure(string home)
    {
        var config = Config(home);
        Directory.CreateDirectory(Path.GetDirectoryName(config)!);
        var text = ConfigText(home);
        if (!File.Exists(config) || AtomicFile.ReadText(config) != text) AtomicFile.WriteText(config, text);
        var known = KnownHosts(home);
        if (!File.Exists(known)) AtomicFile.TryCreateText(known, "");
    }

    /// <summary>
    /// The options the door hands an OpenSSH ssh for an identity's act (§4.2 step 3), before git's own <c>-p</c>, <c>-4</c>, <c>-6</c>
    /// and <c>SendEnv</c>: <c>BatchMode</c> first, Daoris's configuration, the identity's key alone, no agent, Daoris's known hosts
    /// checked strictly, no forwarding.
    /// </summary>
    public static IReadOnlyList<string> IdentityOptions(string home, string id) =>
    [
        "-o", "BatchMode=yes",
        "-F", Slashed(Config(home)),
        "-i", Slashed(GitKeys.KeyPath(home, id)),
        "-o", "IdentitiesOnly=yes",
        "-o", "IdentityAgent=none",
        "-o", "StrictHostKeyChecking=yes",
        "-o", "UserKnownHostsFile=" + Option(KnownHosts(home)),
        "-o", "ForwardAgent=no",
    ];

    /// <summary>The host as a known-hosts line names it: bare on port 22, else <c>[host]:port</c>.</summary>
    public static string KnownName(string host, string? port) =>
        port is { Length: > 0 } and not "22" ? $"[{host}]:{port}" : host;

    /// <summary>
    /// The keys the known hosts hold for a host (its lines' <c>type base64</c>), by its known name. Hashed names are never written
    /// here, so a name is compared as written.
    /// </summary>
    public static IReadOnlyList<string> Known(string home, string name)
    {
        var path = KnownHosts(home);
        if (!File.Exists(path)) return [];
        var found = new List<string>();
        foreach (var line in AtomicFile.ReadText(path).Split('\n'))
        {
            var parts = line.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 3 || parts[0].StartsWith('#')) continue;
            if (parts[0].Split(',').Contains(name, StringComparer.OrdinalIgnoreCase)) found.Add(parts[1] + " " + parts[2]);
        }

        return found;
    }

    /// <summary>
    /// Trust one key for a host (§3.5, at a person's press): its line appended, any other key of that host's name taken out, so a
    /// changed host is trusted again as the one key the person compared.
    /// </summary>
    public static void Trust(string home, string name, string key)
    {
        Ensure(home);
        var path = KnownHosts(home);
        var kept = new StringBuilder();
        foreach (var line in AtomicFile.ReadText(path).Split('\n'))
        {
            var parts = line.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) continue;
            if (parts.Length >= 3 && parts[0].Split(',').Contains(name, StringComparer.OrdinalIgnoreCase)) continue;
            kept.Append(line.TrimEnd('\r')).Append('\n');
        }

        kept.Append(name).Append(' ').Append(key).Append('\n');
        AtomicFile.WriteText(path, kept.ToString());
    }

    /// <summary>Touch <c>run/git/used/&lt;id&gt;</c>, which the page reads as <i>last used</i> (§4.2 step 6).</summary>
    public static void Used(string home, string id)
    {
        if (!GitIdentities.IsId(id)) return;
        try
        {
            var folder = DaorisHome.Entry(home, HomeEntry.GitUsed);
            Directory.CreateDirectory(folder);
            var path = Path.Combine(folder, id);
            if (File.Exists(path)) File.SetLastWriteTimeUtc(path, DateTime.UtcNow);
            else File.WriteAllText(path, "");
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // Last used is a note, never a reason an act fails.
        }
    }

    /// <summary>When an identity was last used, or null.</summary>
    public static DateTimeOffset? LastUsed(string home, string id)
    {
        var path = Path.Combine(DaorisHome.Entry(home, HomeEntry.GitUsed), id);
        return GitIdentities.IsId(id) && File.Exists(path) ? new DateTimeOffset(File.GetLastWriteTimeUtc(path), TimeSpan.Zero) : null;
    }
}
