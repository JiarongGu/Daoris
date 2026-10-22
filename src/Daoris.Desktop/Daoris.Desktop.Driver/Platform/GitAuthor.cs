namespace Daoris.Driver;

/// <summary>
/// The name and email a commit carries (GITAUTH1c, D198 point 5; the git-auth design §3.6): a commit made in a session's tree, or by
/// Daoris in the repository (a squash's commit), carries the <c>commitsAs</c> of the identity its repository's <c>origin</c> resolves
/// to, only where the repository's own configuration (its local and worktree scopes) names no <c>user.name</c> and no
/// <c>user.email</c>. A repository that names its author keeps it; an identity with no <c>commitsAs</c>, <c>own</c>, or no identity
/// leaves git's own answer. Handed in the command scope of the process serving that repository, so nothing is written into it.
/// </summary>
/// <remarks>
/// Read at the start, so a repository that names an author during a session is honoured from its next start; and a process serves one
/// repository, so a commit it makes in another it may write in carries the same author (§3.6's stated limits). A home where no identity
/// commits as anyone starts no git here.
/// </remarks>
public static class GitAuthor
{
    /// <summary>The author a commit in <paramref name="root"/> carries by §3.6, or null for git's own answer.</summary>
    public static async Task<(string Name, string Email)?> OfAsync(string? home, string root, CancellationToken ct)
    {
        if (home is null) return null;
        var identities = GitIdentities.Read(home);
        if (!identities.Any(each => each.CommitsName is not null && each.CommitsEmail is not null)) return null;

        var (code, scoped, _) = await WorkingTree.GitAsync(root, ["config", "--show-scope", "--get-regexp", @"^user\.(name|email)$"], ct).ConfigureAwait(false);
        if (code == 0 && Names(scoped)) return null;

        var address = await GitRemotes.AddressOf(root, "origin", ct: ct).ConfigureAwait(false);
        if (address is null || GitIdentityActs.GitAddressHost(address) is not { } host) return null;
        var way = TreeRefs.ReachOf(address) == "ssh" ? GitIdentity.Ssh : GitIdentity.Https;
        var (repository, workspace) = GitFolders.RepositoryOf(home, root);
        var serves = GitIdentityUse.Resolve(identities, GitIdentityUse.Read(home), repository, workspace, ["origin"], host, way);
        return serves.Identity is { CommitsName: { } name, CommitsEmail: { } email } ? (name, email) : null;
    }

    /// <summary>Whether <c>git config --show-scope</c>'s lines name an author in the repository's own scopes.</summary>
    public static bool Names(string scoped) =>
        scoped.Split('\n', StringSplitOptions.RemoveEmptyEntries).Any(line => line.StartsWith("local\t", StringComparison.Ordinal) || line.StartsWith("worktree\t", StringComparison.Ordinal));

    /// <summary>Hand the author to a start's command scope (§4.1's <c>user.name</c> and <c>user.email</c>).</summary>
    public static void Hand(IDictionary<string, string?> environment, (string Name, string Email)? author)
    {
        if (author is { } who) GitRouting.Scope(environment, [("user.name", who.Name), ("user.email", who.Email)]);
    }

    /// <summary>The words that hand the author to one git command (<c>-c user.name=… -c user.email=…</c>), none where git's own answers.</summary>
    public static IReadOnlyList<string> Arguments((string Name, string Email)? author) =>
        author is { } who ? ["-c", "user.name=" + who.Name, "-c", "user.email=" + who.Email] : [];
}
