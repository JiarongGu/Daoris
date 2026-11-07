using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>A judge's answer for one remote act (the git-auth design §6.5): whether it may pass, its code, and the one line a refusal says.</summary>
/// <param name="Allowed">Whether the act may pass.</param>
/// <param name="Code">Why: <c>read</c>, <c>granted</c>, <c>access</c>, <c>asked</c>, or a refusal's code (<c>denied</c>, <c>read-only</c>, ...).</param>
/// <param name="Line">The one line a refusal writes where git prints it; null where the act passes.</param>
/// <param name="Ask">Whether the act must be asked of the person at the door (GITAUTH1f, §6.6): not allowed yet, and not refused.</param>
public sealed record GitVerdict(bool Allowed, string Code, string? Line, bool Ask = false);

/// <summary>
/// The one judge of git access (GITAUTH1b, D198 points 8, 11 and 13; the git-auth design §6.1, §6.2, §6.5 and §6.7), asked by both
/// doors at the remote command. Until the person can set git access (GITAUTH1f) it answers Daoris's default, door by door, which is
/// today's behaviour: <b>Read</b> is allowed; <b>Write</b> is allowed to the push act alone, by the grant the driver writes for that
/// act's git start on the person's press (<see cref="GitGrants"/>), and refused to every other process, a session on any harness and a
/// plugin among them; <b>Dangerous</b>, each update of a push judged by its kind, is judged at the doors for every agent whatever its harness
/// (D233 choice 3, D241 point 3), refused unless the person's git access allows it (<see cref="GitPushJudge"/>).
/// </summary>
/// <remarks>
/// <para><b>Git access alone</b> says whether a session pushes: a home whose person had switched <c>no-push</c> off was converted once into
/// this machine's Write <i>allow</i> by the home's migration (<c>GitPushStep</c>).</para>
/// <para><b>An identity read only on its host</b> refuses every Write through it, a grant's included, at both doors and at the landing.</para>
/// <para><b>Refusing a Codex session's push</b>, which nothing refused before, is a narrowing D72 lets Daoris make without the person
/// (§6.2). So is refusing a push by a process that is not a session and holds no grant (a plugin's trial or tool run, a hook).</para>
/// <para>The base library alone: the doors compile it in (<c>Daoris.Desktop.GitDoor</c>).</para>
/// </remarks>
public static class GitAccessRules
{
    /// <summary>The line a refused push says, where git prints it.</summary>
    public const string DeniedLine =
        "daoris: Daoris's git access refuses a push from here: a session's accepted work is pushed by its landing, on the person's yes.";

    /// <summary>The answer for an act of <paramref name="act"/>'s class under Daoris's default (<see cref="GitAccessKeys.AsToday"/>).</summary>
    /// <param name="act">Read or Write, from the remote command.</param>
    /// <param name="granted">Whether the act carried a grant it took (<see cref="GitGrants.Take"/>).</param>
    public static GitVerdict Judge(GitAct act, bool granted) => act switch
    {
        GitAct.Read => new(true, "read", null),
        _ when granted => new(true, "granted", null),
        _ => new(false, "denied", DeniedLine),
    };

    /// <summary>The line a refusal says where the identity serving the act is read only on its host.</summary>
    public static string ReadOnlyLine(string label) =>
        $"daoris: the git identity {label} is read only on its host, so Daoris refuses a push through it: point this remote at another identity, or clear the mark in Daoris's settings.";

    /// <summary>The line a refusal says where the person's git access denies the act.</summary>
    public static string AccessDeniedLine(GitAct act) =>
        $"daoris: this repository's git access denies {(act == GitAct.Read ? "reading from" : "writing to")} a remote: change it in Daoris's settings.";

    /// <summary>The line a refusal says where the person refused the act's go-ahead.</summary>
    public const string RefusedLine = "daoris: the git go-ahead for this act was refused, or was not answered in time.";

    /// <summary>
    /// The answer for an act under the value its class resolved to (GITAUTH1f, §6.2, §6.5): <c>allow</c> passes, <c>deny</c> refuses,
    /// <c>ask</c> is asked at the door, and <see cref="GitAccessKeys.AsToday"/> (Write, set by none) is <see cref="Judge(GitAct, bool)"/>.
    /// A grant (the landing's, or a late answer's) passes a Write whatever the value but <c>deny</c>. An identity read only on its host makes
    /// Write refuse whatever the access and the grant.
    /// </summary>
    /// <param name="readOnlyIdentity">The label of the identity serving the act where the person marked it read only on its host; else null.</param>
    public static GitVerdict Judge(GitAct act, string value, bool granted, string? readOnlyIdentity = null)
    {
        if (act == GitAct.Write && readOnlyIdentity is not null) return new(false, "read-only", ReadOnlyLine(readOnlyIdentity));
        return value switch
        {
            GitAccessKeys.Deny => new(false, "access-denied", AccessDeniedLine(act)),
            GitAccessKeys.Allow => new(true, "access", null),
            GitAccessKeys.Ask when granted => new(true, "granted", null),
            GitAccessKeys.Ask => new(false, "ask", null, Ask: true),
            _ => Judge(act, granted),
        };
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
/// The push act's grant (GITAUTH1b, D198 point 13; the git-auth design §6.7): written by the driver for the act's push on the person's
/// press, handed to that act's own git start in <see cref="GitRouting.GrantVariable"/>, and taken by the door for one write connection to
/// the address it names, once, within its bound. Kept in the home's <c>run/git/grants/</c>, one file per grant, holding the address's
/// key and its bound and nothing else.
/// </summary>
/// <remarks>
/// A grant file can be written by any process of the account, as every local record can (§10): grants are bound to their act and their
/// minutes, and the doors govern an agent's ordinary git, not an agent set on getting round them. A grant carries the refs of its own act
/// (<see cref="GitGrantRef"/>) and nothing else.
/// </remarks>
public static class GitGrants
{
    /// <summary>How long a grant stands: a held call's bound (<c>HeldPermissions.Bound</c>).</summary>
    public static readonly TimeSpan Bound = TimeSpan.FromMinutes(15);

    /// <summary>
    /// Write a grant for one push to <paramref name="address"/> that carries exactly <paramref name="refs"/>; its id, or null where the
    /// address has no key or no ref is named.
    /// </summary>
    public static string? Write(string home, string address, IReadOnlyList<GitGrantRef> refs, DateTimeOffset now)
    {
        if (GitAddressKeys.Of(address) is not { } key || refs.Count == 0) return null;
        var id = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16));
        var folder = DaorisHome.Entry(home, HomeEntry.GitGrants);
        Directory.CreateDirectory(folder);
        var record = new JsonObject
        {
            ["address"] = key,
            ["until"] = (now + Bound).ToString("O"),
            ["refs"] = new JsonArray(refs.Select(each => (JsonNode)new JsonObject { ["ref"] = each.Ref, ["new"] = each.New, ["old"] = each.Old }).ToArray()),
        };
        var path = Path.Combine(folder, id + ".json");
        File.WriteAllText(path + ".writing", record.ToJsonString());
        File.Move(path + ".writing", path);
        return id;
    }

    /// <summary>
    /// The refs the grant <paramref name="id"/> carries for a push to <paramref name="key"/>, read and left where it is: null where it is not
    /// there, names another address, has passed its bound or carries no ref. A grant for another address is no grant here.
    /// </summary>
    public static IReadOnlyList<GitGrantRef>? Read(string home, string? id, string? key, DateTimeOffset now)
    {
        if (id is null || key is null || id.Length != 32 || !id.All(char.IsAsciiHexDigitLower)) return null;
        try
        {
            var path = Path.Combine(DaorisHome.Entry(home, HomeEntry.GitGrants), id + ".json");
            if (!File.Exists(path) || JsonNode.Parse(AtomicFile.ReadText(path)) is not JsonObject record) return null;
            if (!string.Equals((string?)record["address"], key, StringComparison.OrdinalIgnoreCase)) return null;
            if (!DateTimeOffset.TryParse((string?)record["until"], out var until) || now > until) return null;
            var refs = (record["refs"] as JsonArray ?? new JsonArray())
                .OfType<JsonObject>()
                .Select(each => new GitGrantRef((string?)each["ref"] ?? "", (string?)each["new"] ?? "", (string?)each["old"]))
                .Where(each => each.Ref.Length > 0 && each.New.Length > 0)
                .ToList();
            return refs.Count == 0 ? null : refs;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException or DriverException)
        {
            return null;
        }
    }

    /// <summary>
    /// Take the grant <paramref name="id"/> names for a push to <paramref name="key"/>: true once, where it is there, names that key
    /// and stands at <paramref name="now"/>. Which refs it carries is <see cref="Read"/>'s, judged by <see cref="GitPushJudge"/> first.
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
