namespace Daoris.Driver;

/// <summary>
/// The end of an ask's delivery run: judged once its rounds are kept, over when nothing waits and no node of its graph can move. It has no quest
/// to name its end by, so the end names the ask, and a run ended is delivered for good: it is never governed again.
/// </summary>
internal static class AskRunEnds
{
    public static void End(NodeRecordFiles records, Func<DateTimeOffset> clock, GraphRuns graphRuns, GovernedUnit governed, RunScope scope, List<string> said)
    {
        var unit = governed.Unit.Id;
        if (records.Read(unit) is not { Ended: null } record || !NodeRunKeeping.Over(governed, record)) return;
        RunEnd? ended = null;
        var (refused, kept) = records.Edit<bool>(unit, current =>
            NodeRunKeeping.Over(governed, current) ? (current with { Ended = ended = RunOutcomes.Of(governed.Graph, current, scope.Ask!, clock()) }, true) : (null, false));
        if (refused is not null || kept != true || ended is null) return;
        graphRuns.Ended(scope.Run.Run, ended);
        var at = ended.Node is { } step ? $" at `{step}`" : "";
        said.Add($"work  {unit}: the delivery of ask #{ended.Quest} ended, {ended.Outcome}{at}.");
    }
}
