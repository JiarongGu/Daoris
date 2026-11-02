namespace Daoris.Driver;

/// <summary>
/// The move off ambient credentials (GITAUTH1e, D198 point 17; the git-auth design §6.8). Until a home has moved, a remote no git identity
/// serves keeps the credentials git finds on the machine. At the first start of the build that wires the move the home is marked
/// (<see cref="GitMoved"/>), and from then on a remote nothing serves is reached with no credential of the person's: an SSH remote is
/// held as <c>no-identity</c> before the act, an HTTPS one reads anonymously and is held as <c>no-identity</c> where the host refuses it.
/// Nothing is pre-applied; the person sets an identity up or chooses their own sign-ins, which lifts the hold.
/// </summary>
public static class GitMove
{
    /// <summary>Whether this home has moved.</summary>
    public static bool Moved(string home) => GitMoved.Moved(home);

    /// <summary>Mark the home as moved if it is not; true when this call marked it. A file that does not read is left as it is.</summary>
    public static bool Ensure(string home, DateTimeOffset now)
    {
        try
        {
            if (Moved(home)) return false;
            DriverConfigFile.Change(home, root =>
            {
                if (root[GitMoved.Key] is not System.Text.Json.Nodes.JsonObject) root[GitMoved.Key] = new System.Text.Json.Nodes.JsonObject { ["at"] = Moments.Write(now) };
            });
            return true;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or DriverException)
        {
            return false;
        }
    }

    /// <summary>A refusal where nothing serves the remote, once the home has moved, read as <c>no-identity</c> (§5.1); other classes stand.</summary>
    public static GitFailureClass Reclass(string home, string repository, string? workspace, string remote, string address, GitFailureClass failure)
    {
        if (failure != GitFailureClass.Refused || !Moved(home)) return failure;
        return GitOwnInput.ServesNothing(home, repository, workspace, remote, address) ? GitFailureClass.NoIdentity : failure;
    }
}
