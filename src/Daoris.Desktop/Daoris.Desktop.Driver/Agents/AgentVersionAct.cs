namespace Daoris.Driver;

/// <summary>
/// A move of an agent's managed version as the risk policy reads it: <c>daoris agent pin</c> and <c>update</c>, and the Agents
/// place's <c>HARNESS_ACTION</c> <c>pin</c> and <c>update</c>, are the act <c>agent.version</c> at the pin's place, the workspace it names or
/// else the machine. A session's terminal takes the move where the judge's verdict passes it, and the verdict is one <c>risk.judged</c> line
/// in the home's journal naming the act; where it gates on a role or the owner the move waits on that holder, the verdict said with its rule,
/// and the terminal does nothing (alone, no card can hold it; the running Daoris puts a proved session's press on the holder's card,
/// <c>PipeDelegate</c>). A person's own terminal is not judged here: its word is the owner's.
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

    /// <summary>
    /// The verdict on a version move at <paramref name="workspace"/>'s place (the machine's where none is named) under the policy
    /// <paramref name="config"/> holds; a <c>driver.json</c> that does not read is <paramref name="fault"/>, which reads the act at the owner.
    /// </summary>
    public static (RiskVerdict Verdict, RiskPlace Place) Judge(DriverConfig? config, string? workspace, string actor, string subject, string? fault = null)
    {
        var named = workspace?.Trim() is { Length: > 0 } trimmed ? trimmed : null;
        var act = new RiskAct(RiskActs.AgentVersion, null);
        if (config is null)
        {
            var place = new RiskPlace(named, null, Full: false);
            var policy = new RiskPolicy(new Dictionary<string, IReadOnlyList<RiskRow>>(), PlaceRiskBook.Unwritten, RiskPolicy.SeedRevision)
            {
                Fault = fault ?? "driver.json does not read",
            };
            return (RiskGate.Judge(policy, act, actor, place, subject), place);
        }

        var at = RiskPlace.Of(config, named, null);
        return (RiskGate.Judge(RiskPolicy.Of(config), act, actor, at, subject), at);
    }

    /// <summary>The verdict at <paramref name="home"/>'s <c>driver.json</c>, read now.</summary>
    public static (RiskVerdict Verdict, RiskPlace Place) JudgeAt(string home, string? workspace, string actor, string subject)
    {
        DriverConfig config;
        try
        {
            config = DriverConfig.Load(DriverConfig.ResolvePath(home));
        }
        catch (DriverException unread)
        {
            return Judge(null, workspace, actor, subject, unread.Message);
        }

        return Judge(config, workspace, actor, subject);
    }

    /// <summary>The verdict journaled once: a pass as the move's release, a gate as where it would hold, never released here.</summary>
    public static void Journal(string home, RiskVerdict verdict, RiskPlace place, string actor, string subject, DateTimeOffset at)
    {
        var passed = verdict.Outcome == RiskVerdict.Pass;
        var key = $"{Domain}:{actor}/{subject}#{at.UtcTicks}";
        RiskJournal.Append(home, new RiskJudged(
            at, passed ? RiskJudged.Released : RiskJudged.Opened, key, RiskActs.AgentVersion, verdict.Kind, verdict.Qualifier, actor, place.Workspace,
            place.Repository, place.Mode, null, null, subject, verdict.Outcome, verdict.Class, verdict.By, verdict.Source, verdict.Revision, verdict.Reason,
            passed ? Passed : Waits, !passed));
    }

    /// <summary>Whom a gated verdict waits on, as its words name them.</summary>
    public static string Holder(RiskVerdict verdict) => verdict.Outcome == RiskVerdict.Gate ? $"`{verdict.By}`" : "the owner";

    /// <summary>The words a session's terminal says where the policy passes the move.</summary>
    public static string PassSaid(RiskVerdict verdict, RiskPlace place) =>
        $"this terminal is a session's, and the risk policy passes `{RiskActs.AgentVersion}` at {Where(place)}: {verdict.Passed}.";

    /// <summary>The words a session's terminal says where the policy gates the move: whom it waits on, and the verdict with its rule.</summary>
    public static string WaitSaid(RiskVerdict verdict, RiskPlace place) =>
        $"this terminal is a session's, and the risk policy classes `{RiskActs.AgentVersion}` `{verdict.Class}` at {Where(place)}: it waits on "
        + $"{Holder(verdict)}'s yes — {verdict.Why}. Nothing was done here: make it in Daoris's window, or from the owner's own terminal.";

    private static string Where(RiskPlace place) => place.Workspace is { } workspace ? $"the `{workspace}` workspace" : "this machine";

    /// <summary>
    /// From a session's terminal (<c>DAORIS_SESSION_ID</c>): the move judged, journaled and said; true where it waits on a holder and nothing
    /// may be done here. A terminal that is no session's is the person's own, and is not judged.
    /// </summary>
    internal static async Task<bool> WaitsHereAsync(AgentsWorld world, string verb, string agent, string? workspace, string? version)
    {
        if (world.Instance.Environment(DriverChoicesCommand.SessionVariable) is not { Length: > 0 } session) return false;
        var home = HarnessSettings.HomeOf(AgentsCommand.WiringPath(world));
        var actor = PipeSessionActor(session);
        var subject = $"{verb} {agent}{(version is { } v ? $" {v}" : "")}{(workspace is { Length: > 0 } w ? $" --workspace {w}" : "")}";
        var (verdict, place) = JudgeAt(home, workspace, actor, subject);
        Journal(home, verdict, place, actor, subject, world.Now());
        if (verdict.Outcome == RiskVerdict.Pass)
        {
            await world.Instance.Out.WriteLineAsync($"daoris: {PassSaid(verdict, place)}").ConfigureAwait(false);
            return false;
        }

        await world.Instance.Error.WriteLineAsync($"daoris: {WaitSaid(verdict, place)}").ConfigureAwait(false);
        return true;
    }

    /// <summary>A session as a verdict's actor: <c>session &lt;id&gt;</c>, as a session's own press is logged.</summary>
    public static string PipeSessionActor(string session) => $"session {session}";
}
