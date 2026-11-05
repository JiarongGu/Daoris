using static Daoris.Driver.GitIdentityActs;

namespace Daoris.Driver;

/// <summary>
/// HTTPS git identities (D198 point 4, D241 point 6; the git design §2.3): an identity kept with its token in
/// <c>profiles/git/tokens/&lt;id&gt;/</c> (<see cref="GitTokens"/>), which the credential door answers to git for the identity's own host,
/// and a test. The token is in no argument, answer, event or log line of Daoris's; Daoris reads it only to answer the door.
/// </summary>
public static class GitIdentityHttps
{
    /// <summary>Add an HTTPS identity: its token kept beside the keys, and the identity written once the token is.</summary>
    /// <exception cref="DriverException">A label in use, no user or token, or the folder could not be made.</exception>
    public static GitIdentity AddHttps(string home, string host, string label, string? user, string? name, string? email, string? token)
    {
        var canonical = HttpsHostOf(host);
        label = LabelOf(label);
        var identities = GitIdentities.Read(home);
        if (GitIdentities.Problem(home) is { } problem) throw new DriverException($"git: {problem} (identities-unread)");
        if (identities.Any(each => string.Equals(each.Label, label, StringComparison.OrdinalIgnoreCase)))
            throw new DriverException($"git: an identity is already named {label} (label-taken)");
        var (commitsName, commitsEmail) = CommitsOf(name, email);
        user = string.IsNullOrWhiteSpace(user) ? null : user.Trim();
        if (user is null) throw new DriverException("git: an HTTPS identity signs in as a user: name it with --user (user)");
        if (string.IsNullOrWhiteSpace(token)) throw new DriverException("git: an HTTPS identity needs its token (token)");

        var identity = new GitIdentity(GitIdentities.NewId(), label, canonical, GitIdentity.Https, user, commitsName, commitsEmail, null, null,
            ReadOnly: false, Test: null, Default: !identities.Any(each => each.Host == canonical && each.Way == GitIdentity.Https && each.Default));
        GitTokens.Set(home, identity.Id, GitTokens.Https, token);
        try
        {
            GitIdentities.Change(home, all => [.. all, identity]);
        }
        catch
        {
            GitTokens.Remove(home, identity.Id);
            throw;
        }

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
        var door = GitWiring.DoorPath(home, GitCredentialDoor.Exe) is { } laid ? Path.GetDirectoryName(laid) : null;
        var run = await RunWithAsync(git, ["ls-remote", "--heads", GitHttps.Bare(address)], home, null, drain: false, info =>
        {
            // A test reads directly: the helper's routing would carry the act twice.
            GitRouting.Unroute(info.Environment);
            GitHttps.Apply(info.Environment, home, identity.Id, door);
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
}
