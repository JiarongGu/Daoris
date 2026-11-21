namespace Daoris.Driver;

/// <summary>
/// A check red past its rounds (<see cref="CheckRounds.Bound"/>) as the risk policy decides it: a left attempt's recovery, the act
/// <c>work.recover</c> at the run's place. Where the judge passes it Daoris gives the run's check up as its own stop with what it tried, so
/// its run ends as gave up, one <c>risk.judged</c> line kept and the pass said; where it gates or classes it <c>owner</c>, or no policy is
/// read, the failure stays kept for its holder.
/// </summary>
internal static class SpentChecks
{
    public static RiskVerdict? Verdict(DriverConfig? config, string? workspace, string? repository) =>
        RiskStepGates.At(config, workspace, repository)?.Act(RiskAct.Of(RiskActs.WorkRecover));

    public static bool Passes(DriverConfig? config, string? workspace, string? repository) =>
        Verdict(config, workspace, repository) is { Outcome: RiskVerdict.Pass };

    public static string Words(string node, string? code, string? found, int reds) =>
        NodeRunKeeping.FailedWords(node, code, found)
        + $"\n\nThis is its red round {reds}, past the {CheckRounds.Bound} Daoris sends back on its own, and no risk is declared for its recovery "
        + "here, so Daoris gives the run's check up with what was tried: the run ends as given up, and nobody is asked.";

    public static string Tried(int reds, string? found) =>
        $"its {reds} red rounds are spent, past the {CheckRounds.Bound} Daoris sends back on its own, and no risk is declared for its recovery here. "
        + $"Tried: each red sent back to the work's session as its next turn, {reds} times"
        + (found?.Trim().ReplaceLineEndings("\n").Split('\n')[0] is { Length: > 0 } last ? $"; the last red: {last}" : "");

    public static bool Decide(
        NodeRecordFiles records, string run, string node, int reds, DriverConfig? config, string? repository, string? workspace, string? found, DateTimeOffset now,
        List<string> said)
    {
        if (Verdict(config, workspace, repository) is not { Outcome: RiskVerdict.Pass } verdict) return false;
        var words = Tried(reds, found);
        var stopped = RunNodeWords.Word(records, run, node, NodeWordActs.Stop, words, () => now, settle: SettleBy.Observation);
        if (!stopped.Done)
        {
            said.Add($"run #{run}: step `{node}` is past its red rounds and passed by the risk policy, and Daoris's stop is refused ({stopped.Refused}).");
            return false;
        }

        var place = RiskPlace.Of(config, workspace, repository);
        RiskJournal.Append(records.Home, new RiskJudged(now, RiskJudged.Released, $"spent:{run}/{node}#{reds}", PersonWaits.Left, verdict.Kind, verdict.Qualifier,
            RiskPolicy.Key, place.Workspace, place.Repository, place.Mode, run, node, $"run {run}, step {node}, red round {reds}", verdict.Outcome, verdict.Class,
            verdict.By, verdict.Source, verdict.Revision, verdict.Reason, PolicyPasses.Today, false));
        said.Add($"run #{run}: step `{node}`: Daoris gave it up: {words}. Its run ends there.");
        return true;
    }
}
