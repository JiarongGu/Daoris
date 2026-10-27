namespace Daoris.Driver;

/// <summary>
/// The coded machine-log lines of git's doors and grants (GITAUTH1b, D198 point 16; the git-auth design §11): <c>git.refused</c>,
/// <c>git.granted</c>, <c>git.grant.used</c> and <c>git.wired</c>, beside GITAUTH1a's <c>git.held</c> and <c>git.lifted</c>
/// (<c>GitHolds</c>). Never a secret, an address's user-info or path, or a machine path: a remote is its name and its host.
/// </summary>
/// <remarks>
/// The doors compile this in (<c>Daoris.Desktop.GitDoor</c>) and write into the driver's file of the day, as every process of that
/// source does (<see cref="MachineLog"/>'s turn per file); each line names its <c>door</c>, who acted: <c>landing</c> where the
/// process carries the landing's grant, <c>session</c> where it is a session's (<c>DAORIS_SESSION_ID</c>), else <c>daoris</c>.
/// The base library alone, as everything the doors compile in.
/// </remarks>
public static class GitLog
{
    /// <summary>The variable a session's processes carry its id in (<c>IntakeRoom.SessionVariable</c>'s twin).</summary>
    public const string SessionVariable = "DAORIS_SESSION_ID";

    /// <summary>The machine log's source the doors write as: the driver's, whose git they are.</summary>
    public const string Source = "driver";

    /// <summary>Who acted, from a door's environment: <c>landing</c>, <c>session</c> or <c>daoris</c>.</summary>
    public static string DoorOf(Func<string, string?> variable) =>
        variable(GitRouting.GrantVariable) is { Length: > 0 } ? "landing"
        : variable(SessionVariable) is { Length: > 0 } ? "session"
        : "daoris";

    /// <summary>A door's refusal (§11 <c>git.refused</c>): its code, the act's class, the remote and host where known.</summary>
    public static void Refused(
        string? home, Func<string, string?> variable, string code, GitAct? act, string? remote, string? host) =>
        Write(home, "git.refused", ("repository", variable("DAORIS_REPOSITORY")), ("remote", remote), ("host", host),
            ("class", act is { } known ? ClassName(known) : null), ("code", code), ("door", DoorOf(variable)),
            ("session", variable(SessionVariable)));

    /// <summary>A grant written (§11 <c>git.granted</c>): the landing's, for its plugin's push to <paramref name="remote"/>.</summary>
    public static void Granted(string home, string repository, string remote, string? session, string by) =>
        Write(home, "git.granted", ("repository", repository), ("remote", remote), ("class", ClassName(GitAct.Write)),
            ("session", session), ("by", by), ("door", "landing"));

    /// <summary>A grant taken by a door's write (§11 <c>git.grant.used</c>).</summary>
    public static void GrantUsed(string home, Func<string, string?> variable, string? remote, string? host) =>
        Write(home, "git.grant.used", ("repository", variable("DAORIS_REPOSITORY")), ("remote", remote), ("host", host),
            ("class", ClassName(GitAct.Write)), ("door", DoorOf(variable)), ("session", variable(SessionVariable)));

    /// <summary>
    /// How the driver's git is wired at its start (§11 <c>git.wired</c>): which git Tools runs (its way, and its version where Tools
    /// records one, never its path: asking git would be one more start on every launch), how the ssh door is named (<c>command</c>,
    /// <c>git-ssh</c>, or null where none is laid out) and whether the remote helper is. Identities, pointers and GCM join with
    /// GITAUTH1c and 1d.
    /// </summary>
    public static void Wired(string home, string? way, string? version, string? sshForm, bool helper) =>
        Write(home, "git.wired", ("way", way), ("version", version), ("ssh", sshForm), ("helper", helper),
            ("identities", GitIdentities.Read(home).Count), ("pointers", GitIdentityUse.Read(home).Count));

    /// <summary>A class's name, as the judge and the log say it.</summary>
    public static string ClassName(GitAct act) => act == GitAct.Write ? "write" : "read";

    private static void Write(string? home, string @event, params (string Key, object? Value)[] data)
    {
        if (string.IsNullOrEmpty(home)) return;
        using var log = new MachineLog(home, Source);
        log.Info(@event, data);
    }
}
