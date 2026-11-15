namespace Daoris.Driver;

/// <summary>
/// Why a run's ready <c>check</c> stays ready at a look, by a code: each exit of its start (<see cref="CheckNode.StartAsync"/>) that starts
/// nothing, said in the machine log once per change of reason (<see cref="NodeWaits"/>).
/// </summary>
public static class CheckStartWaits
{
    /// <summary>The machine log's event a wait is written under.</summary>
    public const string Event = "check.waits";

    /// <summary>The graph does not send it: it is not among the governed unit's dispatched nodes.</summary>
    public const string NotSent = "not-sent";

    /// <summary>No heavy executor is wired in this loop as the check's door.</summary>
    public const string NoDoor = "no-door";

    /// <summary>It failed at the commit it binds, and the work has not answered that red yet (<see cref="CheckRounds.Answered"/>).</summary>
    public const string RedHere = "red-here";

    /// <summary>It failed at the commit it binds, and the sessions did not read, so whether the work answered is not known.</summary>
    public const string SessionsUnread = "sessions-unread";

    /// <summary>The wait as the machine log's sentence: the run, its node, the reason's code and its words.</summary>
    public static string Sentence(string run, string node, string reason) => $"run {run}: `{node}` waits ({reason}): {Words(reason)}";

    private static string Words(string reason) => reason switch
    {
        NotSent => "its graph does not send it, so nothing starts it.",
        NoDoor => "no heavy executor is wired in this loop, so nothing runs its gates.",
        RedHere => "it failed at this commit, and the work has not answered that red: it is asked again at the work's next commit, or once its session waits on the run's next step.",
        SessionsUnread => "it failed at this commit, and the sessions did not read, so whether the work answered that red is not known.",
        _ => reason,
    };
}
