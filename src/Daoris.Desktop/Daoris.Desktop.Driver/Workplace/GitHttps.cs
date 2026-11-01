namespace Daoris.Driver;

/// <summary>
/// What an HTTPS act under a git identity is carried with (GITAUTH1d, D198 point 4; the git-auth design §3.3, §3.4 and §4.3): Git
/// Credential Manager in Daoris's own namespace, and none of the person's other authentication inputs. The remote helper applies it to
/// the transport it starts, for that act alone; the terminal's and the window's acts that store, test or erase a sign-in apply the same
/// to GCM's own process. The base library alone: the helper compiles this in.
/// </summary>
/// <remarks>
/// <para><b>Daoris never reads the token.</b> It is stored through GCM (<c>store</c>, its fields on GCM's standard input), handed to git
/// by GCM alone, and erased through GCM; no file, answer, event or log line of Daoris's holds it.</para>
/// <para>GCM is the one beside the resolved git where it has one, else the system's. The helper reaches it as <c>manager</c> with its
/// folder first on the transport's <c>PATH</c>, so a path with a space needs no quoting inside a configuration value.</para>
/// </remarks>
public static class GitHttps
{
    /// <summary>GCM's namespace for every credential Daoris stores: its entries never meet the person's own (<c>git:</c>).</summary>
    public const string Namespace = "daoris";

    /// <summary>GCM's credential store: the Windows Credential Manager, by name.</summary>
    public const string Store = "wincredman";

    /// <summary>The empty folder the transport's <c>HOME</c> and <c>USERPROFILE</c> name, so curl finds no <c>.netrc</c> (§3.3).</summary>
    public static string EmptyHome(string home) => DaorisHome.Entry(home, HomeEntry.GitHome);

    /// <summary>The variables git, curl or GCM document as authentication, taken out of an act's environment that is not under <c>own</c>.</summary>
    public static readonly IReadOnlyList<string> Removed = ["GIT_SSL_CERT", "GIT_SSL_KEY", "GIT_SSL_CERT_PASSWORD_PROTECTED"];

    /// <summary>The GCM program git would run for the person: beside the git on <paramref name="variable"/>'s PATH, else on that PATH; null where none.</summary>
    public static string? Manager(Func<string, string?> variable)
    {
        var path = variable("PATH");
        string[] names = OperatingSystem.IsWindows() ? ["git-credential-manager.exe"] : ["git-credential-manager"];
        if (GitDoors.Find("git", path) is { } git)
        {
            // Git for Windows lays GCM in mingw64/bin, which git's own cmd/ and mingw64/bin both sit beside.
            var folder = Path.GetDirectoryName(git);
            var parent = folder is null ? null : Path.GetDirectoryName(folder);
            foreach (var up in new[] { folder, parent, parent is null ? null : Path.GetDirectoryName(parent) })
            {
                if (up is null) continue;
                foreach (var candidate in new[] { up, Path.Combine(up, "mingw64", "bin"), Path.Combine(up, "libexec", "git-core") })
                {
                    foreach (var name in names)
                    {
                        if (File.Exists(Path.Combine(candidate, name))) return Path.Combine(candidate, name);
                    }
                }
            }
        }

        return GitDoors.Find("git-credential-manager", path);
    }

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

    /// <summary>The scheme and authority GCM keys a credential by, with no path (<c>https://github.com</c>).</summary>
    public static string UrlOf(string host, bool secure = true) => (secure ? "https://" : "http://") + host;

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
    /// <c>HOME</c> and <c>USERPROFILE</c> naming the empty folder; an empty <c>credential.helper</c> and then GCM alone (when one is
    /// found); GCM's namespace and store; no prompt and no trace; the identity's user; no extra header and no cookie file.
    /// </summary>
    /// <param name="environment">The act's environment, edited.</param>
    /// <param name="home">The Daoris home, for the empty folder.</param>
    /// <param name="user">The identity's user, or null for GCM's own choice of account.</param>
    /// <param name="manager">GCM's program, or null where none was found: the act then reads anonymously.</param>
    public static void Apply(IDictionary<string, string?> environment, string home, string? user, string? manager)
    {
        foreach (var name in Removed) environment.Remove(name);
        foreach (var name in environment.Keys.Where(each => each.StartsWith("GCM_", StringComparison.OrdinalIgnoreCase)).ToList()) environment.Remove(name);
        var empty = EmptyHome(home);
        Directory.CreateDirectory(empty);
        environment["HOME"] = empty;
        environment["USERPROFILE"] = empty;
        environment["GCM_INTERACTIVE"] = "never";

        List<(string Key, string Value)> scope = [("credential.helper", "")];
        if (manager is not null)
        {
            environment["GCM_NAMESPACE"] = Namespace;
            environment["GCM_CREDENTIAL_STORE"] = Store;
            var folder = Path.GetDirectoryName(manager)!;
            environment["PATH"] = folder + Path.PathSeparator + (environment.TryGetValue("PATH", out var path) ? path : "");
            scope.AddRange(
            [
                ("credential.helper", "manager"),
                ("credential.namespace", Namespace),
                ("credential.credentialStore", Store),
                ("credential.interactive", "never"),
                ("credential.trace", "false"),
                ("credential.traceSecrets", "false"),
            ]);
            if (user is not null) scope.Add(("credential.username", user));
        }

        scope.AddRange([("http.extraHeader", ""), ("http.cookieFile", "")]);
        GitRouting.Scope(environment, scope);
    }

    /// <summary>
    /// The environment GCM's own program runs in for an identity's store, erase or sign-in: GCM's namespace and store, the prompt
    /// variables as given, and none of the inherited <c>GCM_*</c>.
    /// </summary>
    public static void ManagerEnvironment(IDictionary<string, string?> environment, bool interactive)
    {
        foreach (var name in environment.Keys.Where(each => each.StartsWith("GCM_", StringComparison.OrdinalIgnoreCase)).ToList()) environment.Remove(name);
        environment["GCM_NAMESPACE"] = Namespace;
        environment["GCM_CREDENTIAL_STORE"] = Store;
        environment["GCM_INTERACTIVE"] = interactive ? "auto" : "never";
        environment["GIT_TERMINAL_PROMPT"] = "0";
    }
}
