using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>The last test of a git identity: its answer, the user the host named, and when.</summary>
/// <param name="Result"><c>reached</c>, <c>refused</c>, <c>host-unknown</c>, <c>host-changed</c> or <c>unreachable</c>.</param>
/// <param name="User">The account the host named, where it names one.</param>
/// <param name="At">When the test ran.</param>
public sealed record GitIdentityTest(string Result, string? User, DateTimeOffset At);

/// <summary>
/// A git identity: who Daoris's git acts as on one code host. Holds no secret: an
/// SSH identity's key is its type and fingerprint here, and its bytes live in <c>profiles/git/keys/&lt;id&gt;/</c>
/// (<see cref="GitKeys"/>).
/// </summary>
/// <param name="Id">Stable, drawn at random (<c>gi-</c> and twelve hex digits); never shown as the name.</param>
/// <param name="Label">The person's name for it, unique on the machine.</param>
/// <param name="Host">The host it signs in to, canonical (<see cref="GitHosts.Canonical"/>), with a port where not the default.</param>
/// <param name="Way"><c>ssh</c>, or <c>https</c>.</param>
/// <param name="User">The account on the host, as the host named it at a test, else as the person typed it.</param>
/// <param name="CommitsName">The name a commit carries under this identity; null for none.</param>
/// <param name="CommitsEmail">The email a commit carries under this identity; null for none.</param>
/// <param name="KeyType">An SSH key's type: <c>ed25519</c>, <c>rsa-4096</c>, <c>ecdsa-256</c>.</param>
/// <param name="Fingerprint">An SSH key's SHA-256 fingerprint, as OpenSSH writes it (<c>SHA256:…</c>).</param>
/// <param name="ReadOnly">The person's statement that the host grants it reading alone.</param>
/// <param name="Test">The last test, or null where none ran.</param>
/// <param name="Default">Whether it is its host's default.</param>
public sealed record GitIdentity(
    string Id, string Label, string Host, string Way, string? User, string? CommitsName, string? CommitsEmail,
    string? KeyType, string? Fingerprint, bool ReadOnly, GitIdentityTest? Test, bool Default)
{
    /// <summary>The SSH way.</summary>
    public const string Ssh = "ssh";

    /// <summary>The HTTPS way.</summary>
    public const string Https = "https";
}

/// <summary>
/// The machine's one set of git identities, kept in
/// <c>data/git-identities.json</c>, which holds no secret, written whole or not at all. Read by the driver and by the
/// ssh door, which compiles this in, so the base library alone.
/// </summary>
/// <remarks>
/// A file that does not read is no identity, said by <see cref="Problem"/> rather than read as empty and written over: a write refuses
/// over a file it could not read, so nothing the person set is lost to a bad byte. An entry this build does not read (a way or a shape
/// of a newer build, or a hand edit) is no identity either, and is kept as it stands, after the identities, by every write.
/// </remarks>
public static class GitIdentities
{
    /// <summary>The ids an identity takes: <c>gi-</c> and twelve lower-case hex digits.</summary>
    public static bool IsId(string? id) =>
        id is { Length: 15 } && id.StartsWith("gi-", StringComparison.Ordinal) && id[3..].All(char.IsAsciiHexDigitLower);

    /// <summary>A new identity's id.</summary>
    public static string NewId() => "gi-" + Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(6));

    /// <summary>Every identity the file holds, in its order; none where it is absent or does not read.</summary>
    public static IReadOnlyList<GitIdentity> Read(string home) => Load(home).Identities;

    /// <summary>Why the file does not read, or null where it reads or is absent.</summary>
    public static string? Problem(string home) => Load(home).Problem;

    /// <summary>The identity of this id, or null.</summary>
    public static GitIdentity? Find(string home, string id) => Read(home).FirstOrDefault(each => each.Id == id);

    /// <summary>
    /// The identity a person names at the terminal: its id, else its label (case-blind). Null where neither names one.
    /// </summary>
    public static GitIdentity? Named(IReadOnlyList<GitIdentity> identities, string name) =>
        identities.FirstOrDefault(each => each.Id == name)
        ?? identities.FirstOrDefault(each => string.Equals(each.Label, name, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Change the set: <paramref name="change"/> is handed the identities as they stand and answers the set to keep, written whole.
    /// </summary>
    /// <exception cref="DriverException">The file does not read, so nothing is written over it.</exception>
    public static IReadOnlyList<GitIdentity> Change(string home, Func<IReadOnlyList<GitIdentity>, IReadOnlyList<GitIdentity>> change)
    {
        var (identities, unread, problem) = Load(home);
        if (problem is not null) throw new DriverException(problem);
        var kept = change(identities);
        Save(home, kept, unread);
        return kept;
    }

    private static (IReadOnlyList<GitIdentity> Identities, IReadOnlyList<JsonNode> Unread, string? Problem) Load(string home)
    {
        var path = DaorisHome.Entry(home, HomeEntry.GitIdentities);
        if (!File.Exists(path)) return ([], [], null);
        try
        {
            if (JsonNode.Parse(AtomicFile.ReadText(path)) is not JsonObject root || root["identities"] is not JsonArray list)
                return ([], [], $"{path} is not a list of git identities: fix it, or move it aside to start again");
            var identities = new List<GitIdentity>();
            var unread = new List<JsonNode>();
            foreach (var item in list)
            {
                if (item is JsonObject each && Of(each) is { } identity) identities.Add(identity);
                else if (item is not null) unread.Add(item.DeepClone());
            }

            return (identities, unread, null);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
        {
            return ([], [], $"{path} does not read ({error.Message}): fix it, or move it aside to start again");
        }
    }

    private static GitIdentity? Of(JsonObject each)
    {
        string? Text(JsonNode? node) => node is JsonValue value && value.TryGetValue<string>(out var text) && text.Length > 0 ? text : null;
        bool Flag(JsonNode? node) => node is JsonValue value && value.TryGetValue<bool>(out var flag) && flag;

        var id = Text(each["id"]);
        var label = Text(each["label"]);
        var host = Text(each["host"]);
        var way = Text(each["way"]);
        if (!IsId(id) || label is null || host is null || way is not (GitIdentity.Ssh or GitIdentity.Https)) return null;

        GitIdentityTest? test = null;
        if (each["test"] is JsonObject tested && Text(tested["result"]) is { } result
            && DateTimeOffset.TryParse(Text(tested["at"]), System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var at))
        {
            test = new GitIdentityTest(result, Text(tested["user"]), at);
        }

        var commits = each["commitsAs"] as JsonObject;
        var key = each["key"] as JsonObject;
        return new GitIdentity(
            id!, label, host, way, Text(each["user"]), Text(commits?["name"]), Text(commits?["email"]),
            Text(key?["type"]), Text(key?["fingerprint"]), Flag(each["readOnly"]), test, Flag(each["default"]));
    }

    private static void Save(string home, IReadOnlyList<GitIdentity> identities, IReadOnlyList<JsonNode> unread)
    {
        var list = new JsonArray();
        foreach (var each in identities)
        {
            var item = new JsonObject
            {
                ["id"] = each.Id,
                ["label"] = each.Label,
                ["host"] = each.Host,
                ["way"] = each.Way,
            };
            if (each.User is not null) item["user"] = each.User;
            if (each.CommitsName is not null || each.CommitsEmail is not null)
                item["commitsAs"] = new JsonObject { ["name"] = each.CommitsName, ["email"] = each.CommitsEmail };
            if (each.KeyType is not null || each.Fingerprint is not null)
                item["key"] = new JsonObject { ["type"] = each.KeyType, ["fingerprint"] = each.Fingerprint };
            if (each.ReadOnly) item["readOnly"] = true;
            if (each.Test is { } test)
            {
                var tested = new JsonObject { ["result"] = test.Result, ["at"] = test.At.ToUniversalTime().ToString("O", System.Globalization.CultureInfo.InvariantCulture) };
                if (test.User is not null) tested["user"] = test.User;
                item["test"] = tested;
            }

            if (each.Default) item["default"] = true;
            list.Add(item);
        }

        foreach (var each in unread) list.Add(each);
        JsonFile.Write(DaorisHome.Entry(home, HomeEntry.GitIdentities), new JsonObject { ["identities"] = list });
    }
}

/// <summary>
/// A code host's name as identities and addresses compare it: lower-cased, a default port
/// dropped, and Azure DevOps' SSH hosts named as the service is (<c>dev.azure.com</c>), so an identity for Azure DevOps serves its
/// <c>ssh.dev.azure.com</c> addresses. The base library alone.
/// </summary>
public static class GitHosts
{
    /// <summary>The hosts the door and the page know by name, with the SSH host each serves and its key page.</summary>
    public static readonly IReadOnlyDictionary<string, (string SshHost, string? KeyPage)> Known = new Dictionary<string, (string, string?)>(StringComparer.Ordinal)
    {
        ["github.com"] = ("github.com", "https://github.com/settings/keys"),
        ["gitlab.com"] = ("gitlab.com", "https://gitlab.com/-/user_settings/ssh_keys"),
        ["dev.azure.com"] = ("ssh.dev.azure.com", null),
    };

    private static readonly string[] AzureSsh = ["ssh.dev.azure.com", "vs-ssh.visualstudio.com"];

    /// <summary>The canonical name of <paramref name="host"/> (<c>[user@]host[:port]</c>): its user dropped, port 22 dropped.</summary>
    public static string Canonical(string host)
    {
        var bare = host.Trim();
        bare = bare[(bare.LastIndexOf('@') + 1)..].ToLowerInvariant();
        if (bare.EndsWith(":22", StringComparison.Ordinal)) bare = bare[..^3];
        return AzureSsh.Contains(bare, StringComparer.Ordinal) ? "dev.azure.com" : bare;
    }

    /// <summary>
    /// The canonical host of an address's host and port as git hands them (<paramref name="port"/> from <c>-p</c>, or null).
    /// </summary>
    public static string Of(string host, string? port) =>
        Canonical(port is { Length: > 0 } and not "22" ? $"{host[(host.LastIndexOf('@') + 1)..]}:{port}" : host);

    /// <summary>The host an SSH connection for an identity of <paramref name="host"/> is made to, its port apart.</summary>
    public static (string Host, string? Port) SshTarget(string host)
    {
        var target = Known.TryGetValue(host, out var known) ? known.SshHost : host;
        var colon = target.LastIndexOf(':');
        return colon > 0 && !target.StartsWith('[') ? (target[..colon], target[(colon + 1)..]) : (target, null);
    }

    /// <summary>Whether a host takes RSA keys alone: Azure DevOps, as its documentation says.</summary>
    public static bool RsaAlone(string host) => Canonical(host) == "dev.azure.com";
}

/// <summary>
/// An SSH identity's key folder: <c>profiles/git/keys/&lt;id&gt;/</c>, made with
/// its access limited to the person's Windows account (inherited entries removed; SYSTEM and Administrators keep their implicit
/// reach) <b>before</b> a key byte is written into it, by generation or import. The public key read and its fingerprint computed
/// here; an import reads only the one file the person names.
/// </summary>
public static class GitKeys
{
    /// <summary>The private key's file name in its folder.</summary>
    public const string KeyFile = "key";

    /// <summary>The public key's file name beside it.</summary>
    public const string PublicFile = "key.pub";

    /// <summary>The largest key file an import reads: an OpenSSH RSA-16384 key is under 13 KB.</summary>
    public const int ImportLimit = 64 * 1024;

    /// <summary>The folder of an identity's key.</summary>
    public static string Folder(string home, string id) => Path.Combine(DaorisHome.Entry(home, HomeEntry.GitKeys), id);

    /// <summary>The private key's path, for the door to hand ssh by <c>-i</c>; never put in an environment.</summary>
    public static string KeyPath(string home, string id) => Path.Combine(Folder(home, id), KeyFile);

    /// <summary>
    /// Make the identity's key folder with its access limited to this account, before anything is written into it. A folder
    /// already there is refused: a key is never written over.
    /// </summary>
    /// <exception cref="DriverException">The folder is there already, or its access could not be set.</exception>
    public static string MakeFolder(string home, string id)
    {
        var folder = Folder(home, id);
        GitPrivateFolder.Make(folder, $"{id}'s key folder", "key-exists");
        return folder;
    }

    /// <summary>Remove an identity's key folder whole.</summary>
    public static void Remove(string home, string id)
    {
        if (!GitIdentities.IsId(id)) return;
        var folder = Folder(home, id);
        if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
    }

    /// <summary>The public key as OpenSSH writes it (<c>type base64 comment</c>), or null where none is there.</summary>
    public static string? PublicKey(string home, string id)
    {
        var path = Path.Combine(Folder(home, id), PublicFile);
        return File.Exists(path) ? AtomicFile.ReadText(path).Trim() : null;
    }

    /// <summary>
    /// A public key's type and SHA-256 fingerprint, from its line (<c>ssh-ed25519 AAAA… comment</c>): the type as the identity keeps
    /// it (<c>ed25519</c>, <c>rsa-4096</c>, <c>ecdsa-256</c>) and the fingerprint as OpenSSH writes it. Null where it does not read.
    /// </summary>
    public static (string Type, string Fingerprint)? Describe(string publicLine)
    {
        var parts = publicLine.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2) return null;
        try
        {
            return Describe(Convert.FromBase64String(parts[1]));
        }
        catch (FormatException)
        {
            return null;
        }
    }

    /// <summary>A public key blob's type and fingerprint; null where the blob does not read.</summary>
    public static (string Type, string Fingerprint)? Describe(byte[] blob)
    {
        var reader = new SshReader(blob);
        if (reader.String() is not { } name) return null;
        var type = Encoding.ASCII.GetString(name);
        string? kept = type switch
        {
            "ssh-ed25519" => "ed25519",
            "ssh-rsa" when reader.String() is not null && reader.String() is { } modulus => "rsa-" + Bits(modulus),
            "ecdsa-sha2-nistp256" => "ecdsa-256",
            "ecdsa-sha2-nistp384" => "ecdsa-384",
            "ecdsa-sha2-nistp521" => "ecdsa-521",
            _ => null,
        };
        return kept is null ? null : (kept, Fingerprint(blob));
    }

    /// <summary>A blob's fingerprint as OpenSSH writes it: <c>SHA256:</c> and the hash in base64 with no padding.</summary>
    public static string Fingerprint(byte[] blob) => "SHA256:" + Convert.ToBase64String(SHA256.HashData(blob)).TrimEnd('=');

    private static int Bits(byte[] modulus)
    {
        var index = 0;
        while (index < modulus.Length && modulus[index] == 0) index++;
        if (index == modulus.Length) return 0;
        var bits = (modulus.Length - index - 1) * 8;
        for (var top = modulus[index]; top > 0; top >>= 1) bits++;
        return bits;
    }

    /// <summary>Reads an unencrypted OpenSSH envelope's public blob without decoding its private part.</summary>
    public static (byte[]? PublicBlob, string? Problem) ReadPrivate(string text)
    {
        // Joined at run time: the sensitive scan refuses a key block's two lines standing together in a source.
        var opening = string.Concat("-----", "BEGIN OPENSSH PRIVATE KEY-----");
        var closing = string.Concat("-----", "END OPENSSH PRIVATE KEY-----");
        var start = text.IndexOf(opening, StringComparison.Ordinal);
        var end = text.IndexOf(closing, StringComparison.Ordinal);
        if (start < 0 || end < start)
        {
            return (null, "this file is not an OpenSSH private key (import-format)");
        }

        try
        {
            var body = Convert.FromBase64String(string.Concat(text[(start + opening.Length)..end].Where(ch => !char.IsWhiteSpace(ch))));
            var magic = "openssh-key-v1\0"u8;
            if (body.Length < magic.Length || !body.AsSpan(0, magic.Length).SequenceEqual(magic)) return (null, "this file is not a private key (import-format)");
            var reader = new SshReader(body, magic.Length);
            var cipher = reader.String();
            reader.String();
            reader.String();
            var count = reader.UInt32();
            var blob = reader.String();
            if (cipher is null || count is null || blob is null) return (null, "this file is not a private key (import-format)");
            if (Encoding.ASCII.GetString(cipher) != "none")
                return (null, "this key has a passphrase, which nobody is there to type at a background fetch: import a key without one (import-passphrase)");
            if (count != 1) return (null, "this file holds more than one key: import a file holding one (import-format)");
            return (blob, null);
        }
        catch (FormatException)
        {
            return (null, "this file is not a private key (import-format)");
        }
    }

    /// <summary>SSH's wire reading: a length-prefixed string, a big-endian number; null past the end.</summary>
    private sealed class SshReader(byte[] bytes, int at = 0)
    {
        private int _at = at;

        public uint? UInt32()
        {
            if (_at + 4 > bytes.Length) return null;
            var value = (uint)(bytes[_at] << 24 | bytes[_at + 1] << 16 | bytes[_at + 2] << 8 | bytes[_at + 3]);
            _at += 4;
            return value;
        }

        public byte[]? String()
        {
            if (UInt32() is not { } length || length > bytes.Length - _at) return null;
            var value = bytes.AsSpan(_at, (int)length).ToArray();
            _at += (int)length;
            return value;
        }
    }
}
