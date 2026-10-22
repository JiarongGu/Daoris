namespace Daoris.Driver;

/// <summary>
/// A repository the ssh door cannot stand in front of, held as <c>own-input</c> (GITAUTH1b, D198 point 2; the git-auth design §4.2 and
/// §5.1). Where the install's path holds one of git's shell characters the door is named by <c>GIT_SSH</c>
/// (<see cref="GitWiring.SshForm"/> answers <c>git-ssh</c>), and git takes <c>core.sshCommand</c>, at whatever scope it reads it,
/// before <c>GIT_SSH</c>: an SSH remote of a repository with one would be reached past the door and its judge. So it is held over
/// reads and writes before any act, said once in <i>What needs you</i>, and told to a session, until the setting is removed (or, with
/// GITAUTH1c, the person chooses <c>own</c> for it).
/// </summary>
/// <remarks>
/// <para><b>Found, never classed from words</b>: nothing fails, since the act would pass round the door. So it is asked before each
/// act, and a hold of its class whose cause is gone is lifted on the way, why <c>setting</c>.</para>
/// <para>The setting's value is never kept or said: it may name a key's path.</para>
/// </remarks>
public static class GitOwnInput
{
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
    /// The <c>own-input</c> hold on <paramref name="remote"/> where <see cref="FindsAsync"/> finds its cause, kept (its first time kept
    /// where it was held before); else null, with any such hold whose cause is gone lifted.
    /// </summary>
    public static async Task<GitHold?> HeldAsync(
        string home, string root, string repository, string? workspace, string remote, string address, string? git, CancellationToken ct)
    {
        if (await FindsAsync(home, root, address, ct).ConfigureAwait(false))
            return GitHolds.Hold(home, root, repository, workspace, remote, address, git, GitFailureClass.OwnInput, words: null, DateTimeOffset.UtcNow);

        GitHolds.Lift(home, root, remote, "setting", hold => hold.Class == GitFailures.Code(GitFailureClass.OwnInput));
        return null;
    }
}
