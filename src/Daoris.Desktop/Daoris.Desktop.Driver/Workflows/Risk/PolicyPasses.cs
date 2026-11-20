using System.Collections.Concurrent;

namespace Daoris.Driver;

/// <summary>
/// Every step a pass reached by the risk policy (<see cref="NodeSource.Policy"/>) is journaled once as a <c>risk.judged</c> release, its rule and
/// revision with it, since the pass reaches it inside its rounds where the shadow never sees it wait. A run whose passed step held a person's
/// tenure (a wait opened before the policy decided it) is told so in one line, once.
/// </summary>
internal static class PolicyPasses
{
    public const string Today = "passed by the risk policy";

    private static readonly ConcurrentDictionary<string, HashSet<string>> Journaled = new(StringComparer.OrdinalIgnoreCase);

    public static void Look(NodeRecordFiles records, IEnumerable<GovernedUnit> governed, DriverConfig config, DateTimeOffset now, List<string> said)
    {
        var policy = RiskPolicy.Of(config);
        var seen = Journaled.GetOrAdd(records.Home, home => [.. RiskJournal.Read(home).Select(line => line.Key)]);
        var tenures = new StepTenureFiles(records.Home, records.HoldsLease);
        foreach (var each in governed)
        {
            if (records.Read(each.Unit.Id) is not { } record) continue;
            var place = RiskPlace.Of(config, each.Unit.Owner.Workspace, each.Unit.Owner.Repository);
            var told = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var node in each.Graph.Nodes)
            {
                var status = WorkPerformer.StatusOf(record, node.Id);
                var why = node.Kind == NodeKinds.Review ? PersonWaits.Accept : PersonWaits.Approval;
                if (status is not { State: NodeStates.Reached, By: NodeSource.Policy } || RiskStepHolds.KindOf(why, node) is not { } kind) continue;
                var key = $"{RiskStepHolds.Domain}:{each.Run}/{node.Id}#{status.Attempt}:{why}";
                lock (seen)
                {
                    if (!seen.Add(key)) continue;
                }

                var verdict = RiskGate.Judge(policy, RiskAct.Of(kind), "", place, "");
                RiskJournal.Append(records.Home, new RiskJudged(now, RiskJudged.Released, key, why, verdict.Kind, verdict.Qualifier, RiskPolicy.Key, place.Workspace,
                    place.Repository, place.Mode, each.Run, node.Id, $"run {each.Run}, step {node.Id}, attempt {status.Attempt}", verdict.Outcome, verdict.Class,
                    verdict.By, verdict.Source, verdict.Revision, verdict.Reason, Today, false));
                if (tenures.Current(each.Unit.Id, node.Id, status.Attempt)?.Holder == CastHolders.Person) told.Add(kind);
            }

            if (told.Count > 0)
            {
                said.Add($"work  {each.Unit.Id}: #{each.Run} {Today}: no rule gates {string.Join(", ", told.Select(kind => $"`{kind}`"))} here, so the wait it held "
                    + "for you ends.");
            }
        }
    }
}
