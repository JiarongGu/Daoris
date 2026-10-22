using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>A judge's answer for one remote act (the git-auth design §6.5): whether it may pass, its code, and the one line a refusal says.</summary>
/// <param name="Allowed">Whether the act may pass.</param>
/// <param name="Code">Why: <c>read</c>, <c>granted</c>, <c>no-push-off</c>, or <c>denied</c>.</param>
/// <param name="Line">The one line a refusal writes where git prints it; null where the act passes.</param>
public sealed record GitVerdict(bool Allowed, string Code, string? Line);

/// <summary>
/// The one judge of git access (GITAUTH1b, D198 points 8, 11 and 13; the git-auth design §6.1, §6.2, §6.5 and §6.7), asked by both
/// doors at the remote command. Until the person can set git access (GITAUTH1f) it answers Daoris's default, door by door, which is
/// today's behaviour: <b>Read</b> is allowed; <b>Write</b> is allowed to the landing alone, by the grant the driver writes for its
/// plugin's push on the person's yes (<see cref="GitGrants"/>), and refused to every other process, a session on any harness among
/// them; <b>Dangerous</b> stays the harness's rules' (<c>no-push</c>'s force, delete and tag forms) until the transport judge.
/// </summary>
/// <remarks>
/// <para><b>A machine whose person switched <c>no-push</c> off</b> keeps today's: its sessions push, so Write is allowed there. GITAUTH1f
/// reads it once as this machine's <c>machineGitAccess.write</c> <i>allow</i> (§6.8); until then the judge reads the switch itself.</para>
/// <para><b>Refusing a Codex session's push</b>, which nothing refused before, is a narrowing D72 lets Daoris make without the person
/// (§6.2). So is refusing a push by a process that is not a session and holds no grant (a plugin's trial or tool run, a hook).</para>
/// <para>The base library alone: the doors compile it in (<c>Daoris.Desktop.GitDoor</c>).</para>
/// </remarks>
public static class GitAccessRules
{
    /// <summary>The shipped default whose switch the judge reads (<c>Permissions.Defaults</c>' id).</summary>
    public const string NoPush = "no-push";

    /// <summary>The line a refused push says, where git prints it.</summary>
    public const string DeniedLine =
        "daoris: Daoris's git access refuses a push from here: a session's accepted work is pushed by its landing, on the person's yes.";

    /// <summary>The answer for an act of <paramref name="act"/>'s class.</summary>
    /// <param name="act">Read or Write, from the remote command.</param>
    /// <param name="pushesHere">Whether the person switched <c>no-push</c> off on this machine (<see cref="PushesHere"/>).</param>
    /// <param name="granted">Whether the act carried a grant it took (<see cref="GitGrants.Take"/>).</param>
    public static GitVerdict Judge(GitAct act, bool pushesHere, bool granted) => act switch
    {
        GitAct.Read => new(true, "read", null),
        _ when granted => new(true, "granted", null),
        _ when pushesHere => new(true, "no-push-off", null),
        _ => new(false, "denied", DeniedLine),
    };

    /// <summary>
    /// Whether the person switched <c>no-push</c> off in the home's <c>permissions.json</c> (<c>defaultsOff</c>). A file that is absent or
    /// unreadable is the default's: on.
    /// </summary>
    public static bool PushesHere(string home)
    {
        try
        {
            var path = DaorisHome.Entry(home, HomeEntry.Permissions);
            if (!File.Exists(path)) return false;
            return JsonNode.Parse(AtomicFile.ReadText(path)) is JsonObject root
                && root["defaultsOff"] is JsonArray off
                && off.Any(each => each is JsonValue value && value.TryGetValue<string>(out var id) && id == NoPush);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException or DriverException)
        {
            return false;
        }
    }

    /// <summary>The class of an ssh remote command's program (§4.2): read, write, or null for one the door does not take.</summary>
    public static GitAct? ClassOf(string program, string? lfsOperation = null) => program switch
    {
        "git-upload-pack" => GitAct.Read,
        "git-receive-pack" => GitAct.Write,
        "git-lfs-authenticate" when lfsOperation == "download" => GitAct.Read,
        "git-lfs-authenticate" when lfsOperation == "upload" => GitAct.Write,
        _ => null,
    };
}

/// <summary>
/// An address as the judge and the grants compare it (§6.5, §6.7): its host, lower-cased, and its path with no leading or trailing
/// slash and no <c>.git</c>, so <c>git@github.com:org/engine.git</c>, <c>ssh://git@github.com/org/engine</c> and
/// <c>https://github.com/org/engine.git</c> are one place. Never its user-info, port or scheme.
/// </summary>
public static class GitAddressKeys
{
    /// <summary>The key of a whole address (a URL, or scp's <c>[user@]host:path</c>); null for a local path or one with no host.</summary>
    public static string? Of(string? address)
    {
        if (string.IsNullOrWhiteSpace(address)) return null;
        var text = address.Trim();
        if (text.StartsWith(GitRouting.Prefix, StringComparison.Ordinal)) text = text[GitRouting.Prefix.Length..];

        var scheme = text.IndexOf("://", StringComparison.Ordinal);
        if (scheme > 0)
        {
            var rest = text[(scheme + 3)..];
            var slash = rest.IndexOf('/');
            var authority = slash < 0 ? rest : rest[..slash];
            var path = slash < 0 ? "" : rest[slash..];
            return Of(HostOf(authority), path);
        }

        // scp's form: a colon before any slash, and not a drive letter.
        var colon = text.IndexOf(':');
        var first = text.IndexOf('/');
        if (colon > 1 && (first < 0 || colon < first)) return Of(text[..colon], text[(colon + 1)..]);
        return null;
    }

    /// <summary>The key of the host and path an ssh door is handed (<c>[user@]host</c>, and the command's path).</summary>
    public static string? Of(string host, string path)
    {
        var bare = HostOf(host);
        if (bare.Length == 0) return null;
        var trimmed = path.Trim().Trim('/');
        if (trimmed.StartsWith("~/", StringComparison.Ordinal)) trimmed = trimmed[2..];
        if (trimmed.EndsWith(".git", StringComparison.OrdinalIgnoreCase)) trimmed = trimmed[..^4];
        return bare.ToLowerInvariant() + "/" + trimmed.Trim('/');
    }

    private static string HostOf(string authority)
    {
        var at = authority.LastIndexOf('@');
        var host = at < 0 ? authority : authority[(at + 1)..];
        if (host.StartsWith('['))
        {
            var close = host.IndexOf(']');
            return close < 0 ? host : host[..(close + 1)];
        }

        var port = host.IndexOf(':');
        return port < 0 ? host : host[..port];
    }
}

/// <summary>
/// The landing's grant (GITAUTH1b, D198 point 13; the git-auth design §6.7): written by the driver for a plugin's push on the person's
/// yes, handed to that one plugin process in <see cref="GitRouting.GrantVariable"/>, and taken by the door for one write connection to
/// the address it names, once, within its bound. Kept in the home's <c>run/git/grants/</c>, one file per grant, holding the address's
/// key and its bound and nothing else.
/// </summary>
/// <remarks>
/// A grant file can be written by any process of the account, as every local record can (§10): grants are bound to their act and their
/// minutes, and the doors govern an agent's ordinary git, not an agent set on getting round them. The transport judge (GITAUTH1g) binds
/// a grant to its ref and new commit as well.
/// </remarks>
public static class GitGrants
{
    /// <summary>How long a grant stands: a held call's bound (<c>HeldPermissions.Bound</c>).</summary>
    public static readonly TimeSpan Bound = TimeSpan.FromMinutes(15);

    /// <summary>Write a grant for one write connection to <paramref name="address"/>; its id, or null where the address has no key.</summary>
    public static string? Write(string home, string address, DateTimeOffset now)
    {
        if (GitAddressKeys.Of(address) is not { } key) return null;
        var id = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16));
        var folder = DaorisHome.Entry(home, HomeEntry.GitGrants);
        Directory.CreateDirectory(folder);
        var record = new JsonObject { ["address"] = key, ["until"] = (now + Bound).ToString("O") };
        var path = Path.Combine(folder, id + ".json");
        File.WriteAllText(path + ".writing", record.ToJsonString());
        File.Move(path + ".writing", path);
        return id;
    }

    /// <summary>
    /// Take the grant <paramref name="id"/> names for a write to <paramref name="key"/>: true once, where it is there, names that key
    /// and stands at <paramref name="now"/>. A grant for another address is left for its own act.
    /// </summary>
    public static bool Take(string home, string? id, string? key, DateTimeOffset now)
    {
        if (id is null || key is null || id.Length != 32 || !id.All(char.IsAsciiHexDigitLower)) return false;
        try
        {
            var path = Path.Combine(DaorisHome.Entry(home, HomeEntry.GitGrants), id + ".json");
            if (!File.Exists(path)) return false;
            if (JsonNode.Parse(AtomicFile.ReadText(path)) is not JsonObject record) return false;
            if (!string.Equals((string?)record["address"], key, StringComparison.OrdinalIgnoreCase)) return false;

            // Taken by its move: of two acts carrying it, one moves it and the other finds it gone.
            var taken = path + ".taken";
            File.Move(path, taken);
            File.Delete(taken);
            return DateTimeOffset.TryParse((string?)record["until"], out var until) && now <= until;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException or DriverException)
        {
            return false;
        }
    }

    /// <summary>Drop a grant its process did not take, once the process ends.</summary>
    public static void Drop(string home, string? id)
    {
        if (id is null || id.Length != 32 || !id.All(char.IsAsciiHexDigitLower)) return;
        try
        {
            File.Delete(Path.Combine(DaorisHome.Entry(home, HomeEntry.GitGrants), id + ".json"));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or DriverException)
        {
        }
    }
}
