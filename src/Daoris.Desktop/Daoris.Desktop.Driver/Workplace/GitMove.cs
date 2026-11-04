namespace Daoris.Driver;

/// <summary>
/// No ambient credentials (GITAUTH1e, D198 point 17; the git-auth design §6.8). The move needs the git doors: where the install's
/// <c>app/bin/</c> holds them, a remote no git identity serves is reached with no credential of the person's. An SSH remote is held as
/// <c>no-identity</c> before the act, an HTTPS one reads anonymously and is held as <c>no-identity</c> where the host refuses it. Where the
/// doors are not laid, the home's git keeps the credentials git finds on the machine (D233 choice 7). Nothing is pre-applied; the person
/// sets an identity up or chooses their own sign-ins, which lifts the hold.
/// </summary>
public static class GitMove
{
    /// <summary>Whether this home has moved: both doors lie in the install's <c>app/bin/</c>, so an act can be carried under an identity.</summary>
    public static bool Moved(string home) =>
        GitWiring.DoorPath(home, GitWiring.SshDoor) is not null && GitWiring.DoorPath(home, GitWiring.RemoteHelper) is not null;

    /// <summary>A refusal where nothing serves the remote, once the home has moved, read as <c>no-identity</c> (§5.1); other classes stand.</summary>
    public static GitFailureClass Reclass(string home, string repository, string? workspace, string remote, string address, GitFailureClass failure)
    {
        if (failure != GitFailureClass.Refused || !Moved(home)) return failure;
        return GitOwnInput.ServesNothing(home, repository, workspace, remote, address) ? GitFailureClass.NoIdentity : failure;
    }
}
