namespace Daoris.Driver;

/// <summary>
/// What an HTTPS act under a git identity is carried with (D198 point 4, D241 point 6; the git design §2.3): the credential door,
/// <c>git-credential-daoris</c>, which answers the identity's token for the identity's own host, and none of the person's other
/// authentication inputs. The remote helper applies it to the transport it starts, for that act alone. The base library alone: the
/// helper compiles this in.
/// </summary>
public static class GitHttps
{
    /// <summary>The empty folder the transport's <c>HOME</c> and <c>USERPROFILE</c> name, so curl finds no <c>.netrc</c> (§3.3).</summary>
    public static string EmptyHome(string home) => DaorisHome.Entry(home, HomeEntry.GitHome);

    /// <summary>The variables git or curl document as authentication, taken out of an act's environment that is not under <c>own</c>.</summary>
    public static readonly IReadOnlyList<string> Removed = ["GIT_SSL_CERT", "GIT_SSL_KEY", "GIT_SSL_CERT_PASSWORD_PROTECTED"];

    /// <summary>
    /// The canonical host of an HTTP or HTTPS address (§3.1): lower-cased, its user-info dropped, its port kept unless the scheme's
    /// default. Null for an address that does not read.
    /// </summary>
    public static string? HostOf(string? address)
    {
        if (address is null) return null;
        var text = address.StartsWith(GitRouting.Prefix, StringComparison.Ordinal) ? address[GitRouting.Prefix.Length..] : address;
        if (!Uri.TryCreate(text, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https")) return null;
        return uri.IsDefaultPort ? uri.Host.ToLowerInvariant() : $"{uri.Host.ToLowerInvariant()}:{uri.Port}";
    }

    /// <summary>Whether an address carries a password (§3.3): refused with one line under an identity.</summary>
    public static bool HasPassword(string address)
    {
        var scheme = address.IndexOf("://", StringComparison.Ordinal);
        if (scheme < 0) return false;
        var rest = address[(scheme + 3)..];
        var slash = rest.IndexOf('/');
        var authority = slash < 0 ? rest : rest[..slash];
        var at = authority.LastIndexOf('@');
        return at >= 0 && authority[..at].Contains(':');
    }

    /// <summary>The address with its user-info taken off, the rest as written.</summary>
    public static string Bare(string address)
    {
        var scheme = address.IndexOf("://", StringComparison.Ordinal);
        if (scheme < 0) return address;
        var rest = address[(scheme + 3)..];
        var slash = rest.IndexOf('/');
        var authority = slash < 0 ? rest : rest[..slash];
        var at = authority.LastIndexOf('@');
        return at < 0 ? address : address[..(scheme + 3)] + authority[(at + 1)..] + (slash < 0 ? "" : rest[slash..]);
    }

    /// <summary>The user an address names, or null.</summary>
    public static string? UserOf(string address)
    {
        var scheme = address.IndexOf("://", StringComparison.Ordinal);
        if (scheme < 0) return null;
        var rest = address[(scheme + 3)..];
        var slash = rest.IndexOf('/');
        var authority = slash < 0 ? rest : rest[..slash];
        var at = authority.LastIndexOf('@');
        if (at <= 0) return null;
        var info = authority[..at];
        return Uri.UnescapeDataString(info.Contains(':') ? info[..info.IndexOf(':')] : info);
    }

    /// <summary>
    /// Give an act's environment what an identity's HTTPS act carries (§4.3 step 3): the authentication inputs of the person's removed;
    /// <c>HOME</c> and <c>USERPROFILE</c> naming the empty folder; an empty <c>credential.helper</c> and then the credential door alone
    /// (when the act is an identity's and the door is laid), told which identity it answers for; no extra header and no cookie file.
    /// </summary>
    /// <param name="environment">The act's environment, edited.</param>
    /// <param name="home">The Daoris home, for the empty folder.</param>
    /// <param name="identityId">The identity the act is carried as, or null for an anonymous act: no credential is answered.</param>
    /// <param name="doorFolder">The folder the credential door lies in, first on the transport's <c>PATH</c> so a path with a space needs no
    /// quoting in a configuration value; null where it is not laid, and the act reads anonymously.</param>
    public static void Apply(IDictionary<string, string?> environment, string home, string? identityId, string? doorFolder)
    {
        foreach (var name in Removed) environment.Remove(name);
        environment.Remove(GitRouting.IdentityVariable);
        var empty = EmptyHome(home);
        Directory.CreateDirectory(empty);
        environment["HOME"] = empty;
        environment["USERPROFILE"] = empty;

        List<(string Key, string Value)> scope = [("credential.helper", "")];
        if (identityId is not null && doorFolder is not null)
        {
            environment[GitRouting.IdentityVariable] = identityId;
            environment["PATH"] = doorFolder + Path.PathSeparator + (environment.TryGetValue("PATH", out var path) ? path : "");
            scope.Add(("credential.helper", GitCredentialDoor.Name));
        }

        scope.AddRange([("http.extraHeader", ""), ("http.cookieFile", "")]);
        GitRouting.Scope(environment, scope);
    }
}
