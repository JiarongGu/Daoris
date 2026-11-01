using static Daoris.Driver.GitIdentityActs;

namespace Daoris.Driver;

/// <summary>
/// HTTPS git identities through Git Credential Manager (GITAUTH1d, D198 point 4; the git-auth design §3.4, §4.3 and §7.2): a pasted
/// token stored, a provider's browser sign-in, a test, and a removal that erases the stored sign-in. The token goes to GCM on its standard
/// input and nowhere else: it is not in an argument, a file, an answer, an event or a log line of Daoris's, and Daoris never asks GCM
/// for it (<c>get</c> is run only for a sign-in, its output drained unread).
/// </summary>
public static class GitIdentityHttps
{
    /// <summary>Add an HTTPS identity: its sign-in stored in GCM's store under Daoris's namespace (a pasted token), or got by the provider's browser sign-in.</summary>
    /// <param name="token">A personal access token, or null for the provider's browser sign-in.</param>
    /// <param name="protocol">The scheme GCM keys the credential by; <c>https</c> but for a test against a local host.</param>
    /// <exception cref="DriverException">A label in use, no user for a token, no GCM, or GCM refused.</exception>
    public static async Task<GitIdentity> AddHttpsAsync(
        string home, string host, string label, string? user, string? name, string? email, string? token, string protocol, CancellationToken ct)
    {
        var canonical = HttpsHostOf(host);
        label = LabelOf(label);
        var identities = GitIdentities.Read(home);
        if (GitIdentities.Problem(home) is { } problem) throw new DriverException($"git: {problem} (identities-unread)");
        if (identities.Any(each => string.Equals(each.Label, label, StringComparison.OrdinalIgnoreCase)))
            throw new DriverException($"git: an identity is already named {label} (label-taken)");
        var (commitsName, commitsEmail) = CommitsOf(name, email);
        user = string.IsNullOrWhiteSpace(user) ? null : user.Trim();
        if (token is not null && user is null) throw new DriverException("git: a token is stored for a user: name it with --user (user)");
        if (token is { Length: 0 }) throw new DriverException("git: the token is empty (token)");

        var manager = ManagerOf(home);
        if (token is not null) await StoreAsync(home, manager, protocol, canonical, user!, token, ct).ConfigureAwait(false);
        else await SignInAsync(home, manager, protocol, canonical, user, ct).ConfigureAwait(false);

        var identity = new GitIdentity(GitIdentities.NewId(), label, canonical, GitIdentity.Https, user, commitsName, commitsEmail, null, null,
            ReadOnly: false, Test: null, Default: !identities.Any(each => each.Host == canonical && each.Way == GitIdentity.Https && each.Default));
        GitIdentities.Change(home, all => [.. all, identity]);
        return identity;
    }

    /// <summary>A host as an HTTPS identity names it: a scheme the person typed dropped, canonical, port 443 dropped.</summary>
    public static string HttpsHostOf(string host)
    {
        var bare = host.Trim();
        var scheme = bare.IndexOf("://", StringComparison.Ordinal);
        if (scheme > 0) bare = bare[(scheme + 3)..].TrimEnd('/');
        var canonical = HostOf(bare);
        return canonical.EndsWith(":443", StringComparison.Ordinal) ? canonical[..^4] : canonical;
    }

    /// <summary>GCM's program for this home's git (§4.3); the refusal says where none is found.</summary>
    /// <exception cref="DriverException">No Git Credential Manager is found.</exception>
    public static string ManagerOf(string home)
    {
        // The git Tools resolves for this home first, then the machine's PATH, as the helper finds it on git's own.
        var path = Environment.GetEnvironmentVariable("PATH");
        if (Tools.Resolve(Tools.Read(home), home, "git").File is { } git) path = Path.GetDirectoryName(git) + Path.PathSeparator + path;
        return GitHttps.Manager(name => name == "PATH" ? path : null)
            ?? throw new DriverException("git: finds no Git Credential Manager beside this git or on this machine: Git for Windows carries one (no-gcm)");
    }

    /// <summary>
    /// Store a sign-in through GCM's <c>store</c>, in Daoris's namespace: the fields on its standard input, the token never in an argument.
    /// </summary>
    public static async Task StoreAsync(string home, string manager, string protocol, string host, string user, string token, CancellationToken ct)
    {
        var run = await ManagerAsync(home, manager, ["store"], $"protocol={protocol}\nhost={host}\nusername={user}\npassword={token}\n\n", interactive: false, drain: false, ct)
            .ConfigureAwait(false);
        if (run.Code != 0) throw new DriverException($"git: Git Credential Manager could not store the sign-in ({LastLine(run.Error)}) (gcm-store)");
    }

    /// <summary>
    /// Erase a sign-in through GCM's <c>erase</c> (§7.1 <i>Remove…</i>): the entry in Daoris's namespace for this host and user, nothing
    /// of the person's own.
    /// </summary>
    public static async Task EraseAsync(string home, string protocol, string host, string? user, CancellationToken ct)
    {
        string manager;
        try
        {
            manager = ManagerOf(home);
        }
        catch (DriverException)
        {
            return;
        }

        var fields = $"protocol={protocol}\nhost={host}\n" + (user is null ? "" : $"username={user}\n") + "\n";
        var run = await ManagerAsync(home, manager, ["erase"], fields, interactive: false, drain: false, ct).ConfigureAwait(false);
        if (run.Code != 0) throw new DriverException($"git: Git Credential Manager could not erase the sign-in ({LastLine(run.Error)}) (gcm-erase)");
    }

    /// <summary>
    /// A provider's browser sign-in (§7.2): GitHub through GCM's <c>github login --browser</c>, which stores what it gets; any other host
    /// through GCM's own <c>get</c>, run interactive, whose answer is drained unread (the credential it prints is never kept).
    /// </summary>
    private static async Task SignInAsync(string home, string manager, string protocol, string host, string? user, CancellationToken ct)
    {
        GitToolRun run;
        if (host == "github.com")
        {
            List<string> arguments = ["github", "login", "--browser", "--url", GitHttps.UrlOf(host, protocol == "https")];
            if (user is not null) arguments.AddRange(["--username", user]);
            run = await ManagerAsync(home, manager, arguments, null, interactive: true, drain: false, ct).ConfigureAwait(false);
        }
        else
        {
            run = await ManagerAsync(home, manager, ["get"], $"protocol={protocol}\nhost={host}\n" + (user is null ? "" : $"username={user}\n") + "\n",
                interactive: true, drain: true, ct).ConfigureAwait(false);
        }

        if (run.Code != 0) throw new DriverException($"git: the sign-in did not finish ({LastLine(GitWords.Redact(run.Error))}) (signin)");
    }

    /// <summary>
    /// Test an HTTPS identity (§7.2 step 6): a read of <paramref name="address"/>, a repository on its host, as the identity's act is
    /// carried, nothing prompting. The answer is recorded on the identity.
    /// </summary>
    /// <exception cref="DriverException">No such identity, not an HTTPS one, no address on its host, or no git.</exception>
    public static async Task<GitTestAnswer> TestHttpsAsync(string home, string name, string address, CancellationToken ct)
    {
        var identity = Find(home, name);
        if (identity.Way != GitIdentity.Https) throw new DriverException($"git: {identity.Label} is not an HTTPS identity (way)");
        if (GitHttps.HostOf(address) != identity.Host)
            throw new DriverException($"git: test with a repository on {identity.Host}: --repo <address> (repo)");
        if (GitHttps.HasPassword(address)) throw new DriverException("git: the address carries a password: leave it out (repo)");

        var git = Tools.Resolve(Tools.Read(home), home, "git").File ?? throw new DriverException("git: finds no git (no-git)");
        var manager = TryManager(home);
        var run = await RunWithAsync(git, ["ls-remote", "--heads", GitHttps.Bare(address)], home, null, drain: false, info =>
        {
            // A test reads directly: the helper's routing would carry the act twice.
            GitRouting.Unroute(info.Environment);
            GitHttps.Apply(info.Environment, home, identity.User ?? GitHttps.UserOf(address), manager);
        }, home).WaitAsync(TestBound, ct).ConfigureAwait(false);
        GitSshFiles.Used(home, identity.Id);
        var words = GitWords.Redact(run.Error + "\n" + run.Output) ?? "";
        var failure = run.Code == 0 ? (GitFailureClass?)null : GitFailures.Class(words, GitAct.Read);
        var result = run.Code == 0 ? "reached" : failure switch
        {
            GitFailureClass.Refused => "refused",
            GitFailureClass.OwnInput => "refused",
            _ => "unreachable",
        };
        return Record(home, identity, new GitTestAnswer(result, result == "reached" ? identity.User : null, [], LastLine(words)));
    }

    private static string? TryManager(string home)
    {
        try
        {
            return ManagerOf(home);
        }
        catch (DriverException)
        {
            return null;
        }
    }

    private static Task<GitToolRun> ManagerAsync(
        string home, string manager, IReadOnlyList<string> arguments, string? input, bool interactive, bool drain, CancellationToken ct) =>
        RunWithAsync(manager, arguments, home, input, drain, info => GitHttps.ManagerEnvironment(info.Environment, interactive), home)
            .WaitAsync(interactive ? TimeSpan.FromMinutes(10) : TestBound, ct);

    /// <summary>Remove an identity and erase its stored sign-in (HTTPS) or delete its key (SSH): <see cref="Remove"/> and GCM's <c>erase</c>.</summary>
    public static async Task<IReadOnlyList<string>> RemoveAsync(string home, string name, CancellationToken ct)
    {
        var identity = Find(home, name);
        var pointing = Remove(home, name);
        if (identity.Way == GitIdentity.Https)
        {
            // An address of the host reached over http is keyed by its scheme: both entries of Daoris's namespace go.
            await EraseAsync(home, "https", identity.Host, identity.User, ct).ConfigureAwait(false);
            await EraseAsync(home, "http", identity.Host, identity.User, ct).ConfigureAwait(false);
        }

        return pointing;
    }

}
