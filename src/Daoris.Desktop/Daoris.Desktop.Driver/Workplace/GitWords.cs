using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>
/// The one boundary for words that may hold a secret (GITAUTH1a, D198 point 16; the git-auth design §10): every text from git, ssh,
/// Git Credential Manager or a landing plugin passes <see cref="Redact"/> before Daoris keeps, says or answers it. Beside
/// <see cref="WiringRedact"/>, which cuts what This Daoris shows of a value it was handed; this one cuts what a child wrote.
/// </summary>
/// <remarks>
/// <para><b>What it cuts</b>, each held by a row of <c>GitWordsTests</c>: an address's user-info (a token is often carried as
/// one, <c>https://x-access-token:…@host</c>); an <c>Authorization</c> or <c>Cookie</c> header's value, as a curl trace writes
/// it; the token shapes the sensitive gate refuses, with GitHub's fine-grained <c>github_pat_</c> and GitLab's <c>glpat-</c>
/// added; and a private key block, whole.</para>
/// <para><b>Today's paths</b> through it: the fetch's failure (<c>TreeRefs</c>), which feeds the sync's and the landing's words, the
/// start's sentence and Ask Daoris's kept sync proposal; a held remote's record; a landing plugin's message as the driver says and
/// records it (<c>LandingPlugins</c>).</para>
/// </remarks>
public static partial class GitWords
{
    /// <summary>What stands where something was cut.</summary>
    public const string Cut = "[cut]";

    [GeneratedRegex(@"(?<scheme>\b[A-Za-z][A-Za-z0-9+.\-]*://)(?<info>[^\s/@'""<>]+)@")]
    private static partial Regex UserInfo();

    [GeneratedRegex(@"(?im)\b(?<name>(?:Proxy-)?Authorization|Cookie|Set-Cookie)(?<sep>\s*:\s*)[^\r\n]*")]
    private static partial Regex Header();

    [GeneratedRegex(@"\b(?:gh[pousr]_[A-Za-z0-9]{20,}|github_pat_[A-Za-z0-9_]{20,}|glpat-[A-Za-z0-9_\-]{20,})")]
    private static partial Regex Token();

    [GeneratedRegex(@"-----BEGIN [A-Z ]*PRIVATE KEY-----[\s\S]*?(?:-----END [A-Z ]*PRIVATE KEY-----|\z)")]
    private static partial Regex KeyBlock();

    /// <summary><paramref name="text"/> with every secret it may carry cut; null stays null.</summary>
    public static string? Redact(string? text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        var cut = KeyBlock().Replace(text, Cut);
        cut = Header().Replace(cut, match => $"{match.Groups["name"].Value}{match.Groups["sep"].Value}{Cut}");
        cut = UserInfo().Replace(cut, match => match.Groups["scheme"].Value);
        return Token().Replace(cut, Cut);
    }

    /// <summary>
    /// The host of a remote's address (the git-auth design §3.2), with its port where the address names one: <c>github.com</c>,
    /// <c>ssh.dev.azure.com</c>, <c>127.0.0.1:2222</c>. Null for an address that names no host (a local path, <c>file://</c>).
    /// </summary>
    public static string? HostOf(string? address)
    {
        if (string.IsNullOrWhiteSpace(address)) return null;
        var text = address.Trim();
        var scheme = text.IndexOf("://", StringComparison.Ordinal);
        if (scheme > 0)
        {
            if (text[..scheme].Equals("file", StringComparison.OrdinalIgnoreCase)) return null;
            var rest = text[(scheme + 3)..];
            var end = rest.IndexOfAny(['/', '?', '#']);
            var authority = end < 0 ? rest : rest[..end];
            var at = authority.LastIndexOf('@');
            var host = (at < 0 ? authority : authority[(at + 1)..]).ToLowerInvariant();
            return host.Length == 0 ? null : host;
        }

        // `[user@]host:path`, as TreeRefs.ReachOf reads it: a colon before any slash, after a host of more than one letter.
        if (TreeRefs.ReachOf(text) != "ssh") return null;
        var colon = text.IndexOf(':');
        var named = text[..colon];
        var user = named.LastIndexOf('@');
        return (user < 0 ? named : named[(user + 1)..]).ToLowerInvariant();
    }
}
