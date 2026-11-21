namespace Daoris.Driver;

/// <summary>
/// The judge deciding a run step's own hold: a review's Accept (<c>work.accept</c>) and a task's approval (<c>task.approve</c>, or the
/// destination its <c>act</c> option names), read through <see cref="RiskStepHolds.KindOf"/>. Where the judge passes the act at the run's
/// place the step reaches by the policy with the verdict's rule on its words, and no one is asked; where it gates the step waits on its own
/// role as the place casts it, and where it classes the act <c>owner</c> the owner role holds it from its start (<see cref="Owned"/>).
/// </summary>
public static class RiskStepGates
{
    /// <summary>The judge at a place as <paramref name="config"/> holds its policy and access, or null where no config is read.</summary>
    public static RiskStepJudge? At(DriverConfig? config, string? workspace, string? repository)
    {
        if (config is null) return null;
        var policy = RiskPolicy.Of(config);
        var place = RiskPlace.Of(config, workspace, repository);
        return new(act => RiskGate.Judge(policy, act, "", place, ""), config.PlaceRisk.At(workspace, repository).Marked(PlaceMarks.ProductionReleases)) { Place = place };
    }

    /// <summary>The pass's reads of <paramref name="governed"/> with the judge at its place.</summary>
    internal static NodeGitReads Reads(NodeGitReads? reads, DriverConfig? config, GovernedUnit governed) =>
        (reads ?? new NodeGitReads()) with { Risk = At(config, governed.Unit.Owner.Workspace, governed.Unit.Owner.Repository) };

    /// <summary>The verdict on the hold <paramref name="node"/> stands at; the owner role's step and a marked place's approval are the owner's.</summary>
    public static RiskVerdict? Verdict(NodeDeclaration node, string? why, RiskStepJudge? judge)
    {
        if (judge is null || why is not (PersonWaits.Accept or PersonWaits.Approval) || RiskStepHolds.KindOf(why, node) is not { } kind) return null;
        var verdict = judge.Act(RiskAct.Of(kind));
        if (RiskStepHolds.BadAct(node) is { } bad) return Owners(verdict, $"its `act` option {bad} names no act of the census, which no policy row can class");
        var role = StepRoles.Of(node);
        if (verdict.Outcome == RiskVerdict.OwnerOutcome) return verdict;
        if (role == StepRoles.Owner) return Owners(verdict, $"the step's role is `{StepRoles.Owner}`, which is the owner's own");
        return judge.Marked && why == PersonWaits.Approval && role == StepRoles.Approver
            ? Owners(verdict, $"its place is marked `{PlaceMarks.ProductionReleases}`, which keeps its approver's approval the owner's")
            : verdict;
    }

    private static RiskVerdict Owners(RiskVerdict verdict, string reason) =>
        verdict with { Outcome = RiskVerdict.OwnerOutcome, Class = RiskClasses.Owner, By = null, Reason = reason };

    /// <summary>The step reached by the policy where the judge passes its hold <paramref name="why"/>, with <paramref name="outcome"/>; null where it waits.</summary>
    public static NodeObservation? Passed(NodeReading reading, string why, string? outcome = null)
    {
        if (Verdict(reading.Node, why, reading.Git?.Risk) is not { Outcome: RiskVerdict.Pass } verdict) return null;
        return NodeObserving.Seen(reading, NodeStates.Reached, RiskVerdict.Passes) with
        {
            Source = NodeSource.Policy, Words = verdict.Passed,
            Acceptance = reading.Binding with
            {
                Outcome = outcome,
                Evidence = new NodePort(NodeObserving.EvidenceSchema(reading.Node.Kind), $"{RiskPolicy.Key}#{reading.Unit.Id}", reading.Binding.SubjectRevision, reading.Binding.Id),
            },
        };
    }

    /// <summary>Whether the judge classes the hold <paramref name="node"/> stands at the owner's, so the owner role holds it from its start.</summary>
    public static bool Owned(NodeDeclaration node, NodeStatus status, RiskStepJudge? judge) =>
        Verdict(node, status.State == NodeStates.Active && NodeKit.Of(node.Kind) is IWaitsOnRole waits ? waits.Waits(node, status) : null, judge)
            is { Outcome: RiskVerdict.OwnerOutcome };
}

/// <summary>The judge at a run's place, and whether the place carries the production-releases mark.</summary>
public sealed record RiskStepJudge(Func<RiskAct, RiskVerdict> Act, bool Marked)
{
    public RiskPlace? Place { get; init; }
}
