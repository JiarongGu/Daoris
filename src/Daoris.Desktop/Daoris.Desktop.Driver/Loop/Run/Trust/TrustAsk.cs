namespace Daoris.Driver;

/// <summary>
/// Whether the harness uses what the repository allows it: Claude Code ignores a repository's allow-list until a person has accepted
/// its path, so a session in an untrusted tree does the work and then cannot take or close the quest it exists to serve. Asked only
/// where the connector's allowance would not reach the session, since the rules handed over at spawn are honoured in an untrusted
/// folder on both doors; trust then decides only whether the repository's own allow-list counts.
/// </summary>
internal static class TrustAsk
{
    /// <summary>The hold of a tree its account's harness has not accepted, said for a person and noted for a screen; null where it is trusted.</summary>
    public static StartRun? Untrusted(IRunWiring wiring, RunRound run, HarnessSelection chosen)
    {
        var quest = run.Quest;
        var workTree = run.State.WorkFolder;
        if (run.State.Preflight is not { } preflight
            || preflight.Home is not { TrustFile: { Length: > 0 } trustFile } home
            || wiring.HandedConnector(preflight, run.Start.Workspace, quest.To, "quest_respond"))
        {
            return null;
        }

        var trustPath = ClaudeTrust.PathFor(trustFile, chosen.ProfileHome, home.Variable);
        if (ClaudeTrust.Accepted(trustPath, workTree) != false) return null;

        // With no account home its file is the own sign-in's, which Daoris never writes: the screen offers no grant of it.
        lock (run.Untrusted) run.Untrusted.Add(new TrustHold(workTree, trustPath, Quest: quest.Id, Own: chosen.ProfileHome is null));
        return run.Hold(ClaudeTrust.Refusal(workTree, chosen.ProfileHome is null ? null : chosen.Profile));
    }
}
