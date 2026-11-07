namespace Daoris.Driver;

/// <summary>
/// A door's go-ahead: an act whose class says <c>ask</c> is held at the door for the person's answer,
/// but only while Daoris runs. The go-ahead is shown in <i>What needs you</i> by an instance (the desktop, or a headless driver), so with
/// none holding the home nothing would show it, and the door refuses at once rather than wait out the bound.
/// </summary>
/// <remarks>
/// <para>A grant a late allow kept for the same act is taken first, running or not: the person already answered it.</para>
/// <para>The base library alone: the doors compile it in, with the home's lease it reads.</para>
/// </remarks>
public static class GitDoorAsks
{
    /// <summary>The line a door says where it would ask and Daoris is not running.</summary>
    public const string NotRunningLine =
        "daoris: this git act waits on the person's go-ahead, and Daoris is not running to ask for it: start Daoris, then try again.";

    /// <summary>Whether an instance of Daoris holds <paramref name="home"/>, so a go-ahead raised there is shown to the person.</summary>
    public static bool Running(string home) => HomeLease.HeldBy(home) is { IsInstance: true };

    /// <summary>
    /// Hold the act for its go-ahead of class <paramref name="cls"/>: allowed by a kept grant or the person's allow; refused at once with
    /// <see cref="NotRunningLine"/> where Daoris is not running, or with the refusal's line where the person refused or did not answer in time.
    /// </summary>
    public static GitVerdict Hold(
        string home, string? repository, string? workspace, string? remote, string? host, string cls, GitAct act, string? key,
        Func<string, string?> variable)
    {
        var session = variable(GitLog.SessionVariable);
        bool allowed;
        if (GitAsks.TakeGrant(home, session, key, cls, DateTimeOffset.UtcNow)) allowed = true;
        else if (!Running(home)) return new(false, "not-running", NotRunningLine);
        else allowed = GitAsks.Hold(home, repository, workspace, remote, host, cls, session, key, () => DateTimeOffset.UtcNow, Thread.Sleep);

        GitLog.Asked(home, variable, allowed, act, remote, host);
        return allowed ? new(true, "asked", null) : new(false, "ask-refused", GitAccessRules.RefusedLine);
    }
}
