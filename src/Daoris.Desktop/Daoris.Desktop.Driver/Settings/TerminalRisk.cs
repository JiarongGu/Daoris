namespace Daoris.Driver;

/// <summary>One journal line of an act a terminal makes: its key, the hold's release or its open, the actor, the subject, what it did, whether it held, and when.</summary>
public sealed record RiskLine(string Key, bool Release, string Actor, string Subject, string Today, bool Held, DateTimeOffset At);

/// <summary>
/// What an act made below the judge asks of the risk policy, which Workflows keeps: the verdict on a kind of the census at its place, and
/// its journal line. <c>agent.version</c> (<see cref="AgentVersionAct"/>) and <c>priority.set</c> (<see cref="PriorityCommand"/>) read it.
/// Workflows installs it when the driver loads.
/// </summary>
public interface ITerminalRisk
{
    RiskVerdict Verdict(string home, string kind, string? workspace, string actor, string subject);

    void Journal(string home, string kind, string? workspace, RiskVerdict verdict, RiskLine line);
}

/// <summary>The Workflows operation; unwired, it reads no judge, so an act from a session waits on the owner.</summary>
public static class TerminalRisk
{
    private static ITerminalRisk? _current;

    public static ITerminalRisk Current
    {
        get => _current ?? Unwired;
        set => _current = value;
    }

    private static ITerminalRisk Unwired { get; } = new UnwiredRisk();

    private sealed class UnwiredRisk : ITerminalRisk
    {
        public RiskVerdict Verdict(string home, string kind, string? workspace, string actor, string subject) =>
            new(kind, null, RiskVerdict.OwnerOutcome, RiskClasses.Owner, null, RiskVerdict.Unreadable, "unwired",
                "no judge of the risk policy is wired in this process, so it reads at the owner");

        public void Journal(string home, string kind, string? workspace, RiskVerdict verdict, RiskLine line)
        {
        }
    }
}
