using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// A person's press that proves no session's key, judged by the machine's terminal tier (<see cref="TerminalTiers"/>, read at each
/// press): under <c>act</c>, from a process the pipe places outside every agent Daoris runs, it is the person's press already confirmed,
/// run with no card on the delegate's path (<see cref="PipeDelegate.Pressed"/>) and attributed to a terminal. Under <c>ask</c>, and from
/// a process inside an agent's job or one the pipe cannot place, it is asked on the card as before: a session's process that left its
/// key off is still a session's.
/// </summary>
internal static class PipeTerminalTier
{
    /// <summary>The covered judgement under act from outside every agent; null where the press is asked as before.</summary>
    public static PipeDelegateJudgement? Judge(InstancePipe pipe, PipeConnection connection)
    {
        if (pipe.TerminalTier?.Invoke() != TerminalTiers.Act) return null;
        return pipe.StandingConfirmation.Caller.Place(connection.Client) == StandingPlace.Outside
            ? new(PipeDelegateTier.Covered, Terminal: true)
            : null;
    }
}
