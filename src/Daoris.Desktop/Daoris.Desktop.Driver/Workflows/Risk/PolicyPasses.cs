using System.Collections.Concurrent;

namespace Daoris.Driver;

/// <summary>Where a second opinion's gate is read: its chain or chat run, the work's place, and the run and <c>opinion</c> step a process binds.</summary>
internal sealed record PolicyPassAt(string Subject, string? Workspace, string? Repository, string? Run, string? Step)
{
    public static PolicyPassAt Of(string subject, string? workspace, string? repository, string? chatRun, IWorkplaceProcess? process) =>
        new(subject, workspace, repository, chatRun ?? process?.BoundRun,
            (process as WorkflowProcess)?.Graph?.Nodes.FirstOrDefault(node => node.Kind == NodeKinds.Opinion)?.Id);
}

/// <summary>
/// Every step a pass reached by the risk policy (<see cref="NodeSource.Policy"/>) is journaled once as a <c>risk.judged</c> release, its rule and
/// revision with it, since the pass reaches it inside its rounds where the shadow never sees it wait; an <c>opinion</c> step its gate's pass let
/// go is one of them. A run whose passed step held a person's tenure (a wait opened before the policy decided it) is told so in one line, with
/// that step's first line, so once across restarts too. Only a pass line counts as journaled, and only once appended: the shadow keys a step's
/// open and release alike, and an append that fails is tried again at the next read. Every verdict here is <see cref="RiskStepGates.Verdict(RiskStepJudge?, string, NodeDeclaration?)"/>'s.
/// </summary>
internal static class PolicyPasses
{
    public const string Today = "passed by the risk policy";

    private static readonly ConcurrentDictionary<string, HashSet<string>> Journaled = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>A dispute the policy passed (<c>work.accept</c>, at the opinion's gate) journaled once, as a step's pass is; a failed append throws.</summary>
    internal static void Gate(string home, DriverConfig config, PolicyPassAt at, OpinionGateState gate, DateTimeOffset now)
    {
        if (gate is not { State: OpinionGateStates.Answered, Risk: { Outcome: RiskVerdict.Pass } verdict, Person.Said: OpinionPersonSaid.Policy }) return;
        var key = $"opinion:{at.Subject}@{gate.Tip}/{gate.Answered}";
        var place = RiskPlace.Of(config, at.Workspace, at.Repository);
        if (Once(home, key, () => new RiskJudged(now, RiskJudged.Released, key, gate.Answered ?? "", verdict.Kind, verdict.Qualifier, RiskPolicy.Key, place.Workspace,
                place.Repository, place.Mode, at.Run, at.Step, $"second opinion on {at.Subject} at {gate.Tip}", verdict.Outcome, verdict.Class, verdict.By, verdict.Source,
                verdict.Revision, verdict.Reason, Today, false)) && at.Run is not null && at.Step is not null)
        {
            lock (OpinionPassed(home)) OpinionPassed(home).Add($"{at.Run}/{at.Step}");
            StepBusFacts.Teed(home, () => StepBusFacts.PolicyPassed(at.Run, at.Step, gate.Tip));
        }
    }

    private static readonly ConcurrentDictionary<string, HashSet<string>> OpinionSteps = new(StringComparer.OrdinalIgnoreCase);

    private static HashSet<string> OpinionPassed(string home) => OpinionSteps.GetOrAdd(home, path =>
        new HashSet<string>(RiskJournal.Read(path).Where(line => line.Today == Today && line.Key.StartsWith("opinion:", StringComparison.Ordinal)
            && line is { Run: not null, Step: not null }).Select(line => $"{line.Run}/{line.Step}"), StringComparer.Ordinal));

    /// <summary>The act the policy let a reached step go on by: its own pass, or an <c>opinion</c> step's gate pass; else null.</summary>
    internal static string? PassedKind(NodeDeclaration node, NodeStatus? status, bool gatePassed) => status switch
    {
        { State: NodeStates.Reached } when node.Kind == NodeKinds.Opinion => gatePassed ? RiskActs.WorkAccept : null,
        { State: NodeStates.Reached, By: NodeSource.Policy } => RiskStepHolds.KindOf(PassWhy(node), node),
        _ => null,
    };

    private static string PassWhy(NodeDeclaration node) => node.Kind switch
    {
        NodeKinds.Review or NodeKinds.Opinion => PersonWaits.Accept,
        NodeKinds.QuestHolds => PersonWaits.Pass,
        _ => PersonWaits.Approval,
    };

    private static bool Once(string home, string key, Func<RiskJudged> line)
    {
        var seen = Journaled.GetOrAdd(home, path => [.. RiskJournal.Read(path).Where(each => each.Today == Today).Select(each => each.Key)]);
        lock (seen)
        {
            if (seen.Contains(key)) return false;
            RiskJournal.Append(home, line());
            seen.Add(key);
            return true;
        }
    }

    public static void Look(NodeRecordFiles records, IEnumerable<GovernedUnit> governed, DriverConfig config, DateTimeOffset now, List<string> said)
    {
        var tenures = new StepTenureFiles(records.Home, records.HoldsLease);
        foreach (var each in governed)
        {
            if (records.Read(each.Unit.Id) is not { } record) continue;
            var place = RiskPlace.Of(config, each.Unit.Owner.Workspace, each.Unit.Owner.Repository);
            var judge = RiskStepGates.At(config, each.Unit.Owner.Workspace, each.Unit.Owner.Repository);
            var told = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var node in each.Graph.Nodes)
            {
                var status = WorkPerformer.StatusOf(record, node.Id);
                bool passed;
                lock (OpinionPassed(records.Home)) passed = OpinionPassed(records.Home).Contains($"{each.Run}/{node.Id}");
                // An opinion step its gate's pass let go is a step the policy passed too: its own line once, and its person told once with it.
                var why = PassWhy(node);
                if (PassedKind(node, status, passed) is not { } kind || RiskStepGates.Verdict(judge, kind, node) is not { } verdict) continue;
                var key = $"{RiskStepHolds.Domain}:{each.Run}/{node.Id}#{status.Attempt}:{why}";
                if (!Once(records.Home, key, () => new RiskJudged(now, RiskJudged.Released, key, why, verdict.Kind, verdict.Qualifier, RiskPolicy.Key, place.Workspace,
                        place.Repository, place.Mode, each.Run, node.Id, $"run {each.Run}, step {node.Id}, attempt {status.Attempt}", verdict.Outcome, verdict.Class,
                        verdict.By, verdict.Source, verdict.Revision, verdict.Reason, Today, false)))
                {
                    continue;
                }

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
