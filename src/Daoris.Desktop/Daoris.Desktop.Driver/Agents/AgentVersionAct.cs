namespace Daoris.Driver;

/// <summary>
/// A move of an agent's managed version as the risk policy reads it: <c>daoris agent pin</c> and <c>update</c>, and the Agents
/// place's <c>HARNESS_ACTION</c> <c>pin</c> and <c>update</c>, are the act <c>agent.version</c> at the pin's place, the workspace it names or
/// else the machine. A session's terminal takes the move where the judge's verdict passes it, and the verdict is one <c>risk.judged</c> line
/// in the home's journal naming the act; where it gates on a role or the owner the move waits on that holder, the verdict said with its rule,
/// and the terminal does nothing (alone, no card can hold it; the running Daoris puts a proved session's press on the holder's card,
/// <c>PipeDelegate</c>). A person's own terminal is not judged here: its word is the owner's. The judge is Workflows', reached through
/// <see cref="IAgentVersionRisk"/>, so this speaks only the settings' vocabulary.
/// </summary>
public static class AgentVersionAct
{
    /// <summary>The journal's key domain for a version move.</summary>
    public const string Domain = "agent";

    /// <summary>What a version move the policy passed did, on its <c>risk.judged</c> line.</summary>
    public const string Passed = "moved: passed by the risk policy";

    /// <summary>What a version move the policy gated did at a terminal that cannot hold it.</summary>
    public const string Waits = "nothing done here: it waits on its holder's yes";

    /// <summary>Whether <paramref name="action"/>, an Agents place action, moves a version.</summary>
    public static bool Moves(string? action) => action is "pin" or "update";

    /// <summary>The verdict on a version move at <paramref name="workspace"/>'s place (the machine's where none is named), read at <paramref name="home"/> now and journaled once.</summary>
    public static RiskVerdict Judged(string home, string? workspace, string actor, string subject, DateTimeOffset at) =>
        AgentVersionRisk.Current.Judged(home, workspace?.Trim() is { Length: > 0 } named ? named : null, actor, subject, at);

    /// <summary>Whom a gated verdict waits on, as its words name them.</summary>
    public static string Holder(RiskVerdict verdict) => verdict.Outcome == RiskVerdict.Gate ? $"`{verdict.By}`" : "the owner";

    /// <summary>The words a session's terminal says where the policy passes the move.</summary>
    public static string PassSaid(RiskVerdict verdict, string? workspace) =>
        $"this terminal is a session's, and the risk policy passes `{RiskActs.AgentVersion}` at {Where(workspace)}: {verdict.Passed}.";

    /// <summary>The words a session's terminal says where the policy gates the move: whom it waits on, and the verdict with its rule.</summary>
    public static string WaitSaid(RiskVerdict verdict, string? workspace) =>
        $"this terminal is a session's, and the risk policy classes `{RiskActs.AgentVersion}` `{verdict.Class}` at {Where(workspace)}: it waits on "
        + $"{Holder(verdict)}'s yes — {verdict.Why}. Nothing was done here: make it in Daoris's window, or from the owner's own terminal.";

    private static string Where(string? workspace) => workspace?.Trim() is { Length: > 0 } named ? $"the `{named}` workspace" : "this machine";

    /// <summary>
    /// From a session's terminal (<c>DAORIS_SESSION_ID</c>): the move judged, journaled and said; true where it waits on a holder and nothing
    /// may be done here. A terminal that is no session's is the person's own, and is not judged.
    /// </summary>
    internal static async Task<bool> WaitsHereAsync(AgentsWorld world, string verb, string agent, string? workspace, string? version)
    {
        if (world.Instance.Environment(DriverChoicesCommand.SessionVariable) is not { Length: > 0 } session) return false;
        var home = HarnessSettings.HomeOf(AgentsCommand.WiringPath(world));
        var subject = $"{verb} {agent}{(version is { } v ? $" {v}" : "")}{(workspace is { Length: > 0 } w ? $" --workspace {w}" : "")}";
        var verdict = Judged(home, workspace, PipeSessionActor(session), subject, world.Now());
        if (verdict.Outcome == RiskVerdict.Pass)
        {
            await world.Instance.Out.WriteLineAsync($"daoris: {PassSaid(verdict, workspace)}").ConfigureAwait(false);
            return false;
        }

        await world.Instance.Error.WriteLineAsync($"daoris: {WaitSaid(verdict, workspace)}").ConfigureAwait(false);
        return true;
    }

    /// <summary>A session as a verdict's actor: <c>session &lt;id&gt;</c>, as a session's own press is logged.</summary>
    public static string PipeSessionActor(string session) => $"session {session}";
}

/// <summary>What a version move asks of the risk policy, which Workflows keeps: the verdict at its place, journaled once. Workflows installs it when the driver loads.</summary>
public interface IAgentVersionRisk
{
    RiskVerdict Judged(string home, string? workspace, string actor, string subject, DateTimeOffset at);
}

/// <summary>The Workflows operation; unwired, it reads no judge, so a version move from a session waits on the owner.</summary>
public static class AgentVersionRisk
{
    private static IAgentVersionRisk? _current;

    public static IAgentVersionRisk Current
    {
        get => _current ?? Unwired;
        set => _current = value;
    }

    private static IAgentVersionRisk Unwired { get; } = new UnwiredRisk();

    private sealed class UnwiredRisk : IAgentVersionRisk
    {
        public RiskVerdict Judged(string home, string? workspace, string actor, string subject, DateTimeOffset at) =>
            new(RiskActs.AgentVersion, null, RiskVerdict.OwnerOutcome, RiskClasses.Owner, null, RiskVerdict.Unreadable, "unwired",
                "no judge of the risk policy is wired in this process, so it reads at the owner");
    }
}
