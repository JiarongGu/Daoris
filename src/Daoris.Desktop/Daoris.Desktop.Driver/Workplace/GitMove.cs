namespace Daoris.Driver;

/// <summary>
/// No ambient credentials (GITAUTH1e, D198 point 17; the git-auth design §6.8): a remote no git identity serves is reached with no
/// credential of the person's. An SSH remote is held as <c>no-identity</c> before the act, an HTTPS one reads anonymously and is held as
/// <c>no-identity</c> where the host refuses it. Nothing is pre-applied; the person sets an identity up or chooses their own sign-ins,
/// which lifts the hold.
/// </summary>
public static class GitMove
{
    /// <summary>A refusal where nothing serves the remote, read as <c>no-identity</c> (§5.1); other classes stand.</summary>
    public static GitFailureClass Reclass(string home, string repository, string? workspace, string remote, string address, GitFailureClass failure)
    {
        if (failure != GitFailureClass.Refused) return failure;
        return GitOwnInput.ServesNothing(home, repository, workspace, remote, address) ? GitFailureClass.NoIdentity : failure;
    }
}
