namespace Daoris.Driver;

/// <summary>
/// A repository the doors cannot stand in front of, held as <c>own-input</c> (GITAUTH1b and GITAUTH1c, D198 points 2 and 3; the git-auth
/// design §3.3, §4.2 and §5.1), in two cases. <b>Over SSH</b>, where the install's path holds one of git's shell characters the door is
/// named by <c>GIT_SSH</c> (<see cref="GitWiring.SshForm"/> answers <c>git-ssh</c>), and git takes <c>core.sshCommand</c>, at whatever
/// scope it reads it, before <c>GIT_SSH</c>: an SSH remote of a repository with one would be reached past the door and its judge.
/// <b>Over HTTPS</b>, a client certificate or key named in configuration (<c>http.sslCert</c>, <c>http.sslKey</c>), which git gives no
/// way to clear, would be handed with the act of a git identity of Daoris's. Either is held over reads and writes before any act, said
/// once in <i>What needs you</i>, and told to a session, until the setting is removed or the person chooses <c>own</c> for the remote.
/// </summary>
/// <remarks>
/// <para><b>Found, never classed from words</b>: nothing fails, since the act would pass round the door. So it is asked before each
/// act, and a hold of its class whose cause is gone is lifted on the way, why <c>setting</c>.</para>
/// <para>A setting's value is never read, kept or said: it may name a key's path. The certificate's hold keeps the key's name as its
/// words, the SSH command's none.</para>
/// </remarks>
public static class GitOwnInput
{
    /// <summary>The SSH command's setting.</summary>
    public const string SshCommand = "core.sshCommand";

    /// <summary>
    /// Whether an act at <paramref name="address"/> from the checkout at <paramref name="root"/> would pass round the ssh door: the door
    /// named by <c>GIT_SSH</c> for <paramref name="home"/>, an SSH address, and a <c>core.sshCommand</c> git reads there.
    /// </summary>
    public static async Task<bool> FindsAsync(string home, string root, string address, CancellationToken ct)
    {
        if (GitWiring.SshForm(home) != "git-ssh" || TreeRefs.ReachOf(address) != "ssh") return false;
        var (code, said, _) = await WorkingTree.GitAsync(root, ["config", "--get", "core.sshCommand"], ct).ConfigureAwait(false);
        return code == 0 && said.Trim().Length > 0;
    }

    /// <summary>
    /// The client certificate or key an HTTPS act at <paramref name="address"/> from <paramref name="root"/> would carry, by its name as
    /// git documents it (<c>http.sslCert</c>, or <c>http.&lt;url&gt;.sslKey</c> for one scoped to an address this one starts with);
    /// null where none is named for it. Names alone are asked of git, never a value.
    /// </summary>
    public static async Task<string?> CertificateAsync(string root, string address, CancellationToken ct)
    {
        if (TreeRefs.ReachOf(address) is not ("https" or "http")) return null;
        var (code, said, _) = await WorkingTree.GitAsync(
            root, ["config", "--name-only", "--get-regexp", @"^http\.(.+\.)?ssl(cert|key)$"], ct).ConfigureAwait(false);
        if (code != 0) return null;
        foreach (var line in said.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var name = line.EndsWith("sslcert", StringComparison.OrdinalIgnoreCase) ? "sslCert" : "sslKey";
            // git names a scoped key `http.<url>.sslcert`, its url as written.
            var last = line.LastIndexOf('.');
            var scope = last >= "http.".Length ? line["http.".Length..last] : "";
            if (scope.Length > 0 && !address.StartsWith(scope.TrimEnd('/'), StringComparison.OrdinalIgnoreCase)) continue;
            return scope.Length == 0 ? $"http.{name}" : $"http.{scope}.{name}";
        }

        return null;
    }

    /// <summary>
    /// The <c>own-input</c> hold on <paramref name="remote"/> where either case finds its cause, kept (its first time kept where it was
    /// held before); else null, with any such hold whose cause is gone lifted.
    /// </summary>
    public static async Task<GitHold?> HeldAsync(
        string home, string root, string repository, string? workspace, string remote, string address, string? git, CancellationToken ct)
    {
        var serves = Serves(home, repository, workspace, remote, address);
        // GITAUTH1c (§3.3): where the person chose their own sign-ins for this remote, its own settings are theirs to run.
        if (serves != GitServeKind.Own)
        {
            if (await FindsAsync(home, root, address, ct).ConfigureAwait(false))
                return GitHolds.Hold(home, root, repository, workspace, remote, address, git, GitFailureClass.OwnInput, words: null, DateTimeOffset.UtcNow);
            // The certificate binds only where an HTTPS identity of Daoris's serves (GITAUTH1d): else today's credentials carry it, as before.
            if (serves == GitServeKind.Identity && await CertificateAsync(root, address, ct).ConfigureAwait(false) is { } certificate)
                return GitHolds.Hold(home, root, repository, workspace, remote, address, git, GitFailureClass.OwnInput, certificate, DateTimeOffset.UtcNow);
        }

        GitHolds.Lift(home, root, remote, "setting", hold => hold.Class == GitFailures.Code(GitFailureClass.OwnInput));

        // GITAUTH1e (§6.8): once the home has moved, an SSH remote nothing serves has no key to reach it with, so it is held before it
        // connects; an HTTPS one reads anonymously and is classed where the host refuses it (GitMove.Reclass). A serving choice lifts it.
        var nothing = GitMoved.Moved(home) && ServesNothing(home, repository, workspace, remote, address);
        if (nothing && TreeRefs.ReachOf(address) == "ssh")
            return GitHolds.Hold(home, root, repository, workspace, remote, address, git, GitFailureClass.NoIdentity, words: null, DateTimeOffset.UtcNow);
        if (!nothing) GitHolds.Lift(home, root, remote, "identity", hold => hold.Class == GitFailures.Code(GitFailureClass.NoIdentity));
        return null;
    }

    /// <summary>Whether nothing serves <paramref name="remote"/> at <paramref name="address"/> (§3.2) and the address names a host.</summary>
    public static bool ServesNothing(string home, string repository, string? workspace, string remote, string address) =>
        GitIdentityActs.GitAddressHost(address) is not null && Serves(home, repository, workspace, remote, address) == GitServeKind.None;

    /// <summary>Whether <paramref name="remote"/>'s SSH address resolves to <c>own</c> (§3.2): then nothing of the person's is held from it.</summary>
    public static bool ChoseOwn(string home, string repository, string? workspace, string remote, string address) =>
        Serves(home, repository, workspace, remote, address) == GitServeKind.Own;

    /// <summary>What serves <paramref name="remote"/>'s act at <paramref name="address"/> (§3.2), by the address's way; none for a local path.</summary>
    private static GitServeKind Serves(string home, string repository, string? workspace, string remote, string address) =>
        GitIdentityActs.GitAddressHost(address) is { } host
            ? GitIdentityUse.Resolve(GitIdentities.Read(home), GitIdentityUse.Read(home), repository, workspace, [remote], host,
                TreeRefs.ReachOf(address) is "https" or "http" ? GitIdentity.Https : GitIdentity.Ssh).Kind
            : GitServeKind.None;
}
