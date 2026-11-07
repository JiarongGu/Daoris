namespace Daoris.Driver;

/// <summary>
/// No ambient credentials. The move needs the git doors: where the install's
/// <c>app/bin/</c> holds them, a remote no git identity serves is reached with no credential of the person's. An SSH remote is held as
/// <c>no-identity</c> before the act, an HTTPS one reads anonymously and is held as <c>no-identity</c> where the host refuses it. A moved
/// install reaches a host with its identity's own key or token, or with none, never with a credential the machine holds. A home with no
/// install beside it (a run from source) is not moved and keeps the credentials git finds on the machine. Nothing is
/// pre-applied; the person sets an identity up or chooses their own sign-ins, which lifts the hold.
/// </summary>
public static class GitMove
{
    /// <summary>The three doors the publish always lays in the install's <c>app/bin/</c>, in the order the words name them.</summary>
    public static readonly string[] Doors = [GitWiring.SshDoor, GitWiring.RemoteHelper, GitCredentialDoor.Exe];

    /// <summary>The doors the install beside <paramref name="home"/> lacks: all three where there is no install, none where it is whole.</summary>
    public static string[] Missing(string home) => [.. Doors.Where(door => GitWiring.DoorPath(home, door) is null)];

    /// <summary>Whether this home has moved: all three doors lie in the install's <c>app/bin/</c>, so an act can be carried under an identity.</summary>
    public static bool Moved(string home) => Missing(home).Length == 0;

    /// <summary>A refusal where nothing serves the remote, once the home has moved, read as <c>no-identity</c>; other classes stand.</summary>
    public static GitFailureClass Reclass(string home, string repository, string? workspace, string remote, string address, GitFailureClass failure)
    {
        if (failure != GitFailureClass.Refused || !Moved(home)) return failure;
        return GitOwnInput.ServesNothing(home, repository, workspace, remote, address) ? GitFailureClass.NoIdentity : failure;
    }
}
