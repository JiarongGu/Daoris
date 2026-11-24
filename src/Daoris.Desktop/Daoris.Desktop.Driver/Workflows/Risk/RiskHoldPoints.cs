namespace Daoris.Driver;

/// <summary>
/// The run steps' holds as the judge's shadow reads them: each step of a run standing at a wait a gate holds today
/// (<see cref="StepWaiting.Read"/>) is an act of the census: an unchecked done (<c>pass</c> at <c>quest.holds</c>) <c>work.done</c>, a review's
/// Accept <c>work.accept</c>, a task's approval <c>task.approve</c>, a go-ahead the act its <c>act</c> option names or else
/// <c>task.approve</c>, and the recovery waits (<c>choose</c>, <c>vacant</c>, <c>held</c>, <c>left</c>) <c>work.recover</c>. A task waiting
/// on its worker's done and a plugin's actor are work, not holds.
/// </summary>
public static class RiskStepHolds
{
    public const string Domain = "step";

    /// <summary>The act a step's wait <paramref name="why"/> is, spelled <c>kind[/qualifier]</c>; null where it is work, not a hold.</summary>
    public static string? KindOf(string why, NodeDeclaration node) => why switch
    {
        PersonWaits.Pass => RiskActs.WorkDone,
        PersonWaits.Accept => RiskActs.WorkAccept,
        PersonWaits.Approval or PersonWaits.GoAhead => node.Options.GetValueOrDefault("act") is { } written && NodeOptions.Value(written) is string act && (why == PersonWaits.Approval ? Spelled(act) : Named(act))
            ? act
            : RiskActs.TaskApprove,
        PersonWaits.Choose or PersonWaits.Vacant or PersonWaits.Held or PersonWaits.Left => RiskActs.WorkRecover,
        _ => null,
    };

    /// <summary>The holds <paramref name="nodes"/> of the run <paramref name="run"/> stand at in <paramref name="record"/>, its holders as today reads them.</summary>
    public static IReadOnlyList<RiskHold> Of(
        string run, string? workspace, string? repository, bool full, IEnumerable<NodeDeclaration> nodes, NodeRecord record,
        IReadOnlyDictionary<string, StepTenure>? held = null, Func<string, string>? cast = null)
    {
        var place = new RiskPlace(workspace is { Length: > 0 } ? workspace : null, repository is { Length: > 0 } ? repository : null, full);
        var holds = new List<RiskHold>();
        foreach (var node in nodes)
        {
            var status = WorkPerformer.StatusOf(record, node.Id);
            if (StepWaiting.Read(record, node, status, held?.GetValueOrDefault(node.Id), cast) is not { } wait || KindOf(wait.Why, node) is not { } kind) continue;
            var actor = wait.Holder.Kind != StepHolders.Agent ? CastHolders.Person
                : wait.Holder.Name is { } name ? CastHolders.AgentNamed(name) : CastHolders.Agent;
            holds.Add(new RiskHold($"{Domain}:{run}/{node.Id}#{status.Attempt}:{wait.Why}", wait.Why, RiskAct.Of(kind), actor, place,
                $"run {run}, step {node.Id}, attempt {status.Attempt}", StepWaiting.Line(wait.Role, wait.Holder), TodayHeld: true)
            {
                Run = run, Step = node.Id,
            });
        }

        return holds;
    }

    /// <summary>What today did with a step's hold once it is gone: the step's state then, read from its run's record.</summary>
    public static string Released(NodeRecord? record, string node) =>
        record is null ? "released: its run is no longer governed"
        : WorkPerformer.StatusOf(record, node) is var status && status.Code is { Length: > 0 } code ? $"released: {status.State} ({code})"
        : $"released: {WorkPerformer.StatusOf(record, node).State}";

    /// <summary>
    /// The runtime's hold point: every run it governs read now, through the same tenures and casts <i>What needs you</i> reads, and journaled
    /// in shadow. Wired at the runtime's pass (<see cref="NodeRuntime.Risk"/>); it decides nothing.
    /// </summary>
    internal static void Shadow(RiskShadow shadow, IEnumerable<GovernedUnit> governed, NodeRecordFiles records, DriverConfig config)
    {
        var holds = new List<RiskHold>();
        var units = new Dictionary<string, string>(StringComparer.Ordinal);
        RiskPolicy policy;
        try
        {
            policy = RiskPolicy.Of(config);
            var tenures = new StepTenureFiles(records.Home, records.HoldsLease);
            var casts = new PlaceCastFiles(records.Home).Book();
            foreach (var each in governed)
            {
                if (each.Scope is null || records.Read(each.Unit.Id) is not { } record) continue;
                units[each.Run] = each.Unit.Id;
                var holding = tenures.Holding(each.Unit.Id, record.Statuses.ToDictionary(pair => pair.Key, pair => pair.Value.Attempt, StringComparer.Ordinal));
                var owner = each.Unit.Owner;
                var cast = CastReading.At(casts, config.PlaceRisk, owner.Workspace, owner.Repository);
                var full = RiskPlace.Of(config, owner.Workspace, owner.Repository).Full;
                holds.AddRange(Of(each.Run, owner.Workspace, owner.Repository, full, each.Graph.Nodes, record, holding, cast));
            }
        }
        catch (Exception fault) when (fault is not OperationCanceledException)
        {
            // The shadow decides nothing: a hold point it cannot read is passed over, never a reason to fail the pass.
            return;
        }

        shadow.Look(Domain, policy, holds, key =>
        {
            var run = key[(Domain.Length + 1)..key.IndexOf('/', StringComparison.Ordinal)];
            var node = key[(key.IndexOf('/', StringComparison.Ordinal) + 1)..key.IndexOf('#', StringComparison.Ordinal)];
            return Released(units.TryGetValue(run, out var unit) ? records.Read(unit) : null, node);
        });
    }

    /// <summary>The task's `act` option where it is no act of the census (an unknown kind or qualifier, a case mismatch, not a string); null where it names none or a good one.</summary>
    public static string? BadAct(NodeDeclaration node) =>
        node.Kind == NodeKinds.Task && node.Options.TryGetValue("act", out var written) && !(NodeOptions.Value(written) is string act && Spelled(act)) ? written : null;

    private static bool Spelled(string act) =>
        Named(act) && RiskAct.Of(act) is var named && named.ToString() == act
        && (named.Qualifier is null ? !act.Contains('/', StringComparison.Ordinal) : named.Kind == RiskActs.ReleaseStart && named.Qualifier.All(c => c is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '-' or '.' or '_'));

    private static bool Named(string act)
    {
        try
        {
            _ = RiskAct.Of(act);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}

/// <summary>
/// Each changed <c>daoris.gates.json</c> owed acceptance is a <c>command.change</c> at its repository, judged on each changed unit's digest
/// (<see cref="DeclarationVerdicts"/>): where every unit passes it is accepted as it lands (<see cref="IntegrationOwed.Passed"/>).
/// </summary>
public static class RiskDeclarationHolds
{
    public const string Domain = "declaration";

    /// <summary>What a declaration's hold did once it is no longer owed.</summary>
    public const string Released = "released: accepted, or the line moved past it";

    public static IReadOnlyList<RiskHold> Of(IReadOnlyList<DeclarationOwed> owed, IReadOnlyList<DeclarationOwed> passed, DriverConfig config)
    {
        RiskHold Hold(DeclarationOwed each, bool held) =>
            new($"{Domain}:{each.Repository}@{each.Blob}", "declaration", DeclarationVerdicts.Act, $"line:{each.Line}",
                RiskPlace.Of(config, each.Workspace, each.Repository), $"{each.Repository}'s daoris.gates.json at {each.Blob[..Math.Min(12, each.Blob.Length)]}{Units(each)}",
                held ? "Waiting on the yes its verdict names." : "Accepted as it lands: the risk policy passes it.", held);

        var automatic = passed.Select(each => $"{Domain}:{each.Repository}@{each.Blob}").ToHashSet(StringComparer.Ordinal);
        return [.. owed.Where(each => !automatic.Contains($"{Domain}:{each.Repository}@{each.Blob}")).Select(each => Hold(each, true)), .. passed.Select(each => Hold(each, false))];
    }

    private static string Units(DeclarationOwed each) =>
        each.Verdicts.Where(unit => unit.Unit.Length > 0).Select(unit => $"{DeclarationUnits.Said(unit.Unit)} {unit.Digest[..Math.Min(12, unit.Digest.Length)]}").ToList() is { Count: > 0 } named
            ? $" ({string.Join(", ", named)})"
            : "";

    /// <summary>The look's hold point: each declaration owed or taken journaled with its verdict.</summary>
    internal static void Shadow(RiskShadow shadow, IntegrationOwed owed, DriverConfig config)
    {
        try
        {
            shadow.Look(Domain, RiskPolicy.Of(config), Of(owed.Under(config), owed.Passed(config), config), _ => Released);
        }
        catch (Exception fault) when (fault is not OperationCanceledException)
        {
            // The journal decides nothing: its fault never keeps a declaration from its acceptance.
        }
    }
}
