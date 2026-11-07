namespace Daoris.Driver;

/// <summary>
/// An identity's tokens, kept beside the keys in <c>profiles/git/tokens/&lt;id&gt;/</c>: the
/// folder limited to the person's account before a byte is written (<see cref="GitPrivateFolder"/>), the tokens plaintext at rest as
/// the keys and the API keys are. <c>https</c> is the token git's HTTPS act carries, answered by the credential door alone;
/// <c>api</c> is the token a hosted service's API act is handed. A token is read only by the credential door and by the act it is
/// handed to; no answer, event, log line or page row carries it, only whether one is set. The base library alone: the doors compile it in.
/// </summary>
public static class GitTokens
{
    /// <summary>The token git's HTTPS transport takes.</summary>
    public const string Https = "https";

    /// <summary>The token a hosted service's API takes.</summary>
    public const string Api = "api";

    /// <summary>The folder of an identity's tokens.</summary>
    public static string Folder(string home, string id) => Path.Combine(DaorisHome.Entry(home, HomeEntry.GitTokens), id);

    /// <summary>Whether <paramref name="kind"/> is a token an identity keeps.</summary>
    public static bool IsKind(string? kind) => kind is Https or Api;

    /// <summary>The token of a kind, trimmed, or null where none is set or the id is not an identity's.</summary>
    public static string? Read(string home, string id, string kind)
    {
        if (!GitIdentities.IsId(id) || !IsKind(kind)) return null;
        var path = Path.Combine(Folder(home, id), kind);
        if (!File.Exists(path)) return null;
        var text = AtomicFile.ReadText(path).Trim();
        return text.Length == 0 ? null : text;
    }

    /// <summary>Whether a token of that kind is set; the only thing a page or a verb is told.</summary>
    public static bool Has(string home, string id, string kind) => Read(home, id, kind) is not null;

    /// <summary>Keep a token, the folder made limited to this account first where it is not there.</summary>
    /// <exception cref="DriverException">No such kind or id, or the token is empty or holds a line break.</exception>
    public static void Set(string home, string id, string kind, string token)
    {
        if (!GitIdentities.IsId(id)) throw new DriverException("git: that is not an identity's id (id)");
        if (!IsKind(kind)) throw new DriverException($"git: a token is {Https} or {Api} (kind)");
        token = token.Trim();
        if (token.Length == 0) throw new DriverException("git: the token is empty (token)");
        if (token.Contains('\n') || token.Contains('\r')) throw new DriverException("git: a token is one line (token)");
        var folder = Folder(home, id);
        if (!Directory.Exists(folder)) GitPrivateFolder.Make(folder, $"{id}'s token folder", "token-exists");
        AtomicFile.WriteText(Path.Combine(folder, kind), token);
    }

    /// <summary>Clear a token of one kind; nothing is said where none is set. The folder goes when it holds none.</summary>
    public static void Clear(string home, string id, string kind)
    {
        if (!GitIdentities.IsId(id) || !IsKind(kind)) return;
        var folder = Folder(home, id);
        var path = Path.Combine(folder, kind);
        if (File.Exists(path)) File.Delete(path);
        if (Directory.Exists(folder) && !Directory.EnumerateFileSystemEntries(folder).Any()) Directory.Delete(folder);
    }

    /// <summary>Remove an identity's tokens whole (<i>Remove…</i>).</summary>
    public static void Remove(string home, string id)
    {
        if (!GitIdentities.IsId(id)) return;
        var folder = Folder(home, id);
        if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
    }
}
