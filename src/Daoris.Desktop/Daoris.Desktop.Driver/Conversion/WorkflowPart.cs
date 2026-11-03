using System.Text.Json;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>The workflow part's plan as its review and its receipt read it: the plan, and the file it was read from.</summary>
public sealed record WorkflowPartPlan(ConversionPlan Plan, DriverConfig Config);

/// <summary>What the workflow part keeps in its receipt entry beside its operations: the plan it goes on from without planning again, and its <i>before</i>.</summary>
internal sealed record WorkflowBody(
    ConversionChoices Choices, IReadOnlyList<ConvertSave> Saves, IReadOnlyList<ConvertUse> Uses, string? DefaultWorkflow, IReadOnlyList<ConvertPolicy> Policies,
    IReadOnlyList<ConvertRemoval> Removals, IReadOnlyList<string> Held, ConversionBefore? Before);

/// <summary>
/// The conversion's workflow part (WORKFLOWCLEAN1 1e, a part of the one door since ONEWAY1ae; the cleanup design §4.2-§4.5): its census and plan are
/// <see cref="WorkflowLegacy"/>'s and <see cref="ConversionPlans"/>'s; its effects the workflow saves, each idempotent by its version's digest, then
/// one edit of <c>driver.json</c> that writes the choices, the default workflow, the line policies and the removals, judged again inside it; its
/// Undo puts the keys back in one change. It clears nothing.
/// </summary>
public sealed class WorkflowPart(ConvertDoors doors) : IConversionPart
{
    private const string Configuration = "configuration";
    private static readonly JsonSerializerOptions Format = new(JsonSerializerDefaults.Web);

    public string Name => ConversionParts.Workflows;

    public async Task<PartCensus> CensusAsync(CancellationToken ct)
    {
        var world = await doors.World(ct).ConfigureAwait(false);
        return CensusOf(WorkflowLegacy.Census(world, new ConversionReceipts(doors.Home).HeldPlaces()));
    }

    /// <summary>The census's items counted: the old versions, the places on Current, the runs on an old engine and the leftover rules.</summary>
    public static PartCensus CensusOf(LegacyCensus census) =>
        new(ConversionParts.Workflows, census.Versions.Count + census.Places.Count + census.Runs.Count + census.Leftovers.Count, census.Ended);

    public async Task<PartPlan> PlanAsync(ConversionChoices choices, CancellationToken ct)
    {
        var world = await doors.World(ct).ConfigureAwait(false);
        var plan = ConversionPlans.Plan(world, choices, new ConversionReceipts(doors.Home).HeldPlaces());
        return new(Name, plan.Digest, plan.Empty, CensusOf(plan.Census), ConvertCommand.PartLines(plan), []) { Detail = new WorkflowPartPlan(plan, world.Config) };
    }

    public PartReceipt Begin(PartPlan plan)
    {
        var planned = (WorkflowPartPlan)plan.Detail!;
        return Entry(Begin(planned.Plan, planned.Config));
    }

    public PartReceipt Settle(PartReceipt entry) => Entry(Settle(View(entry)));

    public (PartReceipt Entry, string? Refused) Convert(PartReceipt entry, Action<PartReceipt> keep)
    {
        var (receipt, refused) = Continue(View(entry), each => keep(Entry(each)));
        return (Entry(receipt), refused);
    }

    public PartReceipt Clear(PartReceipt entry, Action<PartReceipt> keep) => entry;

    public PartUndo UndoPlan(PartReceipt entry)
    {
        var receipt = View(entry);
        if (Moved(doors.Config(), receipt) is { } moved) return new($"moved: {moved}", []);
        var lines = new List<string>();
        foreach (var removal in receipt.Removals.Where(each => receipt.Before is { } before && (before.Landings.ContainsKey(Key(each.Place, each.IsWorkspace)) || before.Access.ContainsKey(Key(each.Place, each.IsWorkspace)) || before.SessionChecks.ContainsKey(Key(each.Place, each.IsWorkspace)))))
        {
            lines.Add($"`{removal.Place}`: its rules come back ({string.Join(", ", removal.Keys)})");
        }

        foreach (var use in receipt.Uses.Where(each => each.Kind is null)) lines.Add($"`{use.Place}`: its old choice comes back");
        if (receipt.DefaultWorkflow is { } wrote) lines.Add($"the home default `{wrote}` is taken away");
        if (receipt.Policies.Count > 0) lines.Add($"{receipt.Policies.Count} line polic{(receipt.Policies.Count == 1 ? "y" : "ies")} go back into their rules");
        lines.Add("the workflow versions it saved stay saved, unchosen.");
        return new(null, lines) { Said = "its rules, access entries and choices are back; the versions it saved stay saved, unchosen" };
    }

    public void Undo(PartReceipt entry)
    {
        var receipt = View(entry);
        doors.Change(config => Restored(config, receipt));
    }

    // ---- The receipt entry ------------------------------------------------------------------------------------------------------------------------------

    /// <summary>The workflow part of a home's receipt, read as the workflow part's own; null where the receipt holds none.</summary>
    public static ConversionReceipt? View(HomeReceipt receipt) =>
        receipt.Of(ConversionParts.Workflows) is { } entry ? View(entry) with { At = receipt.At, UndoneAt = receipt.UndoneAt } : null;

    private static ConversionReceipt View(PartReceipt entry)
    {
        var body = entry.Body?.Deserialize<WorkflowBody>(Format)
            ?? throw new DriverException($"the workflow part of the conversion `{entry.Digest[..Math.Min(12, entry.Digest.Length)]}` keeps no plan: it is read by hand.");
        return new(entry.Digest, default, body.Choices, body.Saves, body.Uses, body.DefaultWorkflow, body.Policies, body.Removals, body.Held, entry.Operations)
        {
            Before = body.Before,
        };
    }

    private static PartReceipt Entry(ConversionReceipt receipt) => new PartReceipt(ConversionParts.Workflows, receipt.Digest, ConversionOutcomes.Pending, receipt.Operations)
    {
        Body = JsonSerializer.SerializeToNode(
            new WorkflowBody(receipt.Choices, receipt.Saves, receipt.Uses, receipt.DefaultWorkflow, receipt.Policies, receipt.Removals, receipt.Held, receipt.Before), Format) as JsonObject,
        Said = receipt.Held.Count == 0 ? null : $"{receipt.Held.Count} place{(receipt.Held.Count == 1 ? "" : "s")} keep their rules until their open unit ends.",
    }.Derived();

    /// <summary>A receipt written before parts, read as a home's receipt of one workflow part with no build (a stored shape is never changed in place).</summary>
    public static HomeReceipt Wrapped(ConversionReceipt old) => new(old.Digest, old.At, old.Choices, [Entry(old)]) { UndoneAt = old.UndoneAt };

    // ---- Applied (the cleanup design §4.4) ------------------------------------------------------------------------------------------------------------

    private static ConversionReceipt Begin(ConversionPlan plan, DriverConfig config)
    {
        var operations = plan.Saves.Select(each => new ConversionOp($"save:{each.Id}", "workflow-save", $"{(each.Mode == "append" ? "a new version of" : "a new workflow")} `{each.Id}`: {each.Said}", ConversionOutcomes.Pending)).ToList();
        operations.Add(new(Configuration, Configuration, "the choices, the home default, the line policies and the removed rules, in one change", ConversionOutcomes.Pending));
        var before = new ConversionBefore(config.DefaultWorkflow, [], [], [], []);
        foreach (var removal in plan.Removals)
        {
            var key = Key(removal.Place, removal.IsWorkspace);
            if (removal.Keys.Contains("landings") && config.Landings.TryGetValue(removal.Place, out var own)) before.Landings[key] = own;
            if (removal.Keys.Contains("workspaceLandings") && config.WorkspaceLandings.TryGetValue(removal.Place, out var shared)) before.Landings[key] = shared;
            if (removal.Keys.Any(each => each.EndsWith("access.landing", StringComparison.Ordinal)))
            {
                var level = removal.IsWorkspace ? config.WorkspaceAccess.GetValueOrDefault(removal.Place) : config.Access.GetValueOrDefault(removal.Place);
                if (level?.Pauses.GetValueOrDefault(AccessKey.Landing) is { } said) before.Access[key] = said;
            }

            if (removal.Keys.Contains("sessionChecks") && config.SessionChecks.TryGetValue(removal.Place, out var checks)) before.SessionChecks[key] = checks;
        }

        foreach (var use in plan.Uses)
        {
            var key = Key(use.Place, use.IsWorkspace);
            if (before.Choices.ContainsKey(key)) continue;
            var held = use.IsWorkspace ? config.WorkspaceWorkflows.GetValueOrDefault(use.Place) : config.Workflows.GetValueOrDefault(use.Place);
            before.Choices[key] = ConversionReceipts.Wire(held, use.IsWorkspace);
        }

        return new(plan.Digest, DateTimeOffset.UtcNow, plan.Choices, plan.Saves, plan.Uses, plan.DefaultWorkflow, plan.Policies, plan.Removals, plan.Held, operations) { Before = before };
    }

    private static string Key(string place, bool workspace) => (workspace ? "workspace:" : "repository:") + place.ToLowerInvariant();

    /// <summary>A restart's read of the entry: the uncertain operations settled by their postconditions, never replayed blind.</summary>
    private ConversionReceipt Settle(ConversionReceipt receipt)
    {
        foreach (var operation in receipt.Operations.Where(each => each.Outcome == ConversionOutcomes.Uncertain).ToList())
        {
            var done = operation.Id == Configuration ? Posted(doors.Config(), receipt) : operation.Kind == "workflow-save" && SaveHeld(doors.Home, receipt.Saves.First(each => $"save:{each.Id}" == operation.Id));
            receipt = receipt.With(operation.Id, each => each with { Outcome = done ? ConversionOutcomes.Done : ConversionOutcomes.Pending, Note = null });
        }

        return receipt;
    }

    private static bool SaveHeld(string home, ConvertSave save) =>
        WorkflowPorts.Store(home).Load(save.Id) is { Read: { Problem: null, Versions: [.., { Digest: { } digest }] } } && digest == save.Digest;

    private (ConversionReceipt Receipt, string? Refused) Continue(ConversionReceipt receipt, Action<ConversionReceipt> keep)
    {
        foreach (var save in receipt.Saves)
        {
            var id = $"save:{save.Id}";
            if (receipt.Of(id) is not { Outcome: ConversionOutcomes.Pending }) continue;
            receipt = SaveOne(receipt, save, keep);
        }

        string? refused = null;
        var saving = receipt.Operations.Where(each => each.Kind == "workflow-save").ToList();
        if (receipt.Of(Configuration) is { Outcome: ConversionOutcomes.Pending })
        {
            if (saving.Any(each => each.Outcome == ConversionOutcomes.Failed))
            {
                receipt = Kept(keep, receipt.With(Configuration, each => each with { Outcome = ConversionOutcomes.Failed, Note = "a workflow it chooses was not saved, so the configuration was not applied." }));
            }
            else if (saving.All(each => each.Outcome == ConversionOutcomes.Done))
            {
                (receipt, refused) = Configure(receipt, keep);
            }
        }

        return (receipt, refused);
    }

    private static ConversionReceipt Kept(Action<ConversionReceipt> keep, ConversionReceipt receipt)
    {
        keep(receipt);
        return receipt;
    }

    private ConversionReceipt SaveOne(ConversionReceipt receipt, ConvertSave save, Action<ConversionReceipt> keep)
    {
        var id = $"save:{save.Id}";
        if (SaveHeld(doors.Home, save)) return Kept(keep, receipt.With(id, each => each with { Outcome = ConversionOutcomes.Done, Note = null }));
        var store = WorkflowPorts.Store(doors.Home);
        var found = store.Load(save.Id);
        if (save.Mode == "new" && found is not null)
        {
            return Kept(keep, receipt.With(id, each => each with { Outcome = ConversionOutcomes.Failed, Note = $"`{save.Id}` is a workflow here already that is not this one: plan it again." }));
        }

        if (save.Mode == "append" && !(found?.Read is { Problem: null, Versions: [.., var newest] } && newest.Version == save.Base))
        {
            return Kept(keep, receipt.With(id, each => each with { Outcome = ConversionOutcomes.Failed, Note = $"`{save.Id}` moved since it was planned: plan it again." }));
        }

        receipt = Kept(keep, receipt.With(id, each => each with { Outcome = ConversionOutcomes.Uncertain, Note = null }));
        try
        {
            var base_ = found?.Read.Versions[^1];
            var planned = WorkflowOperations.PlanGraph(doors.Home, found, save.Id, base_, save.Name, "screen", PlannedWorkflows.At, save.Graph, save.From);
            store.Write(save.Id, planned.Plan.Result!);
            return Kept(keep, receipt.With(id, each => each with { Outcome = ConversionOutcomes.Done, Note = null }));
        }
        catch (ChoiceWords refused)
        {
            return Kept(keep, receipt.With(id, each => each with { Outcome = ConversionOutcomes.Failed, Note = refused.Message }));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // The write's answer is lost: it may have landed, and the file found says.
            return Kept(keep, receipt.With(id, each => each with { Note = error.Message }));
        }
    }

    /// <summary>Whether <paramref name="config"/> already holds what the conversion's edit writes (the settled postcondition).</summary>
    private static bool Posted(DriverConfig config, ConversionReceipt receipt)
    {
        if (receipt.DefaultWorkflow is { } wrote && config.DefaultWorkflow != wrote) return false;
        foreach (var use in receipt.Uses.Where(each => each.Kind is null))
        {
            var held = use.IsWorkspace ? config.WorkspaceWorkflows.GetValueOrDefault(use.Place) : config.Workflows.GetValueOrDefault(use.Place);
            if (held?.Default != use.Workflow) return false;
        }

        foreach (var policy in receipt.Policies)
        {
            if (!(policy.IsWorkspace ? config.WorkspaceLinePolicies : config.LinePolicies).ContainsKey(policy.Place)) return false;
        }

        return receipt.Removals.All(each => Gone(config, each) || receipt.Held.Contains(Key(each.Place, each.IsWorkspace)));
    }

    private static bool Gone(DriverConfig config, ConvertRemoval removal) =>
        !(removal.Keys.Contains("landings") && config.Landings.ContainsKey(removal.Place))
        && !(removal.Keys.Contains("workspaceLandings") && config.WorkspaceLandings.ContainsKey(removal.Place))
        && !(removal.Keys.Contains("sessionChecks") && config.SessionChecks.ContainsKey(removal.Place))
        && !(removal.Keys.Any(each => each.EndsWith("access.landing", StringComparison.Ordinal))
            && (removal.IsWorkspace ? config.WorkspaceAccess : config.Access).GetValueOrDefault(removal.Place)?.Pauses.ContainsKey(AccessKey.Landing) == true);

    private (ConversionReceipt Receipt, string? Refused) Configure(ConversionReceipt receipt, Action<ConversionReceipt> keep)
    {
        receipt = Kept(keep, receipt.With(Configuration, each => each with { Outcome = ConversionOutcomes.Uncertain, Note = null }));
        IReadOnlyList<string> held = [];
        try
        {
            doors.Change(config =>
            {
                var (next, kept) = Edited(config, receipt, doors.OpenCurrent());
                held = kept;
                return next;
            });
        }
        catch (DriverException refused)
        {
            var moved = refused.Message.StartsWith("moved", StringComparison.Ordinal);
            return (Kept(keep, receipt.With(Configuration, each => each with { Outcome = ConversionOutcomes.Failed, Note = refused.Message })),
                moved ? ConversionRefusals.Moved : "refused");
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return (Kept(keep, receipt.With(Configuration, each => each with { Note = error.Message })), null);
        }

        // The places the judge left their rules at: what a later plan offers to remove.
        var kept2 = receipt.Removals.Select(each => Key(each.Place, each.IsWorkspace)).Where(held.Contains).Concat(receipt.Held).Distinct().ToList();
        receipt = Kept(keep, (receipt with { Held = kept2 }).With(Configuration, each => each with { Outcome = ConversionOutcomes.Done, Note = null }));
        return (receipt, null);
    }

    /// <summary>
    /// The edit itself, judged over the file as the edit reads it (the cleanup design §13 point 4): each rule to be removed still reads as the plan
    /// read it (else <c>moved</c>, naming it), and a place with a Current unit open now keeps its rules. Idempotent: what is already written is written again.
    /// </summary>
    private static (DriverConfig Config, IReadOnlyList<string> Held) Edited(DriverConfig config, ConversionReceipt receipt, IReadOnlySet<string> open)
    {
        var held = new List<string>();
        var next = config;
        if (receipt.DefaultWorkflow is { } wrote && next.DefaultWorkflow is null) next = next.WithDefaultWorkflow(wrote);
        foreach (var use in receipt.Uses)
        {
            next = use.Workflow == WorkflowSelection.Current
                ? PinnedToCurrent(next, use.Place, use.IsWorkspace)
                : WorkflowSelection.Apply(next, new WorkflowUse(use.Workflow, use.IsWorkspace ? null : use.Place, use.IsWorkspace ? use.Place : null, use.Kind));
        }

        foreach (var removal in receipt.Removals)
        {
            var key = Key(removal.Place, removal.IsWorkspace);
            if (open.Contains(key))
            {
                held.Add(key);
                continue;
            }

            if (receipt.Before is { } before && !Gone(config, removal) && !Reads(config, removal, before, key))
            {
                throw new DriverException($"moved: `{removal.Place}`'s rules changed since the plan was read, so nothing was written. Plan it again.");
            }

            next = Removed(next, removal);
            var policy = receipt.Policies.FirstOrDefault(each => each.Place == removal.Place && each.IsWorkspace == removal.IsWorkspace);
            if (policy is not null) next = removal.IsWorkspace ? next.WithWorkspaceLinePolicy(removal.Place, policy.Policy) : next.WithLinePolicy(removal.Place, policy.Policy);
        }

        return (next, held);
    }

    /// <summary>
    /// A place's default choice written as <c>current</c>, which no door writes any more for a development route: the conversion keeps a place with a
    /// work cycle on Current this way once the home default would take it, its kinds and landed route as they were.
    /// </summary>
    public static DriverConfig PinnedToCurrent(DriverConfig config, string place, bool workspace)
    {
        var map = workspace ? config.WorkspaceWorkflows : config.Workflows;
        var held = map.GetValueOrDefault(place);
        var pinned = With(map, place, new WorkflowChoice(WorkflowSelection.Current, held?.Kinds ?? []) { Landed = held?.Landed });
        return workspace ? config with { WorkspaceWorkflows = pinned } : config with { Workflows = pinned };
    }

    private static bool Reads(DriverConfig config, ConvertRemoval removal, ConversionBefore before, string key)
    {
        if (removal.Keys.Contains("landings") && before.Landings.TryGetValue(key, out var own) && config.Landings.GetValueOrDefault(removal.Place) != own) return false;
        if (removal.Keys.Contains("workspaceLandings") && before.Landings.TryGetValue(key, out var shared) && config.WorkspaceLandings.GetValueOrDefault(removal.Place) != shared) return false;
        if (removal.Keys.Contains("sessionChecks") && before.SessionChecks.TryGetValue(key, out var checks) && config.SessionChecks.GetValueOrDefault(removal.Place) != checks) return false;
        return true;
    }

    private static DriverConfig Removed(DriverConfig config, ConvertRemoval removal)
    {
        var next = config;
        if (removal.Keys.Contains("landings")) next = next with { Landings = Without(next.Landings, removal.Place) };
        if (removal.Keys.Contains("workspaceLandings")) next = next with { WorkspaceLandings = Without(next.WorkspaceLandings, removal.Place) };
        if (removal.Keys.Contains("sessionChecks")) next = next with { SessionChecks = Without(next.SessionChecks, removal.Place) };
        if (removal.Keys.Any(each => each.EndsWith("access.landing", StringComparison.Ordinal)))
        {
            var map = removal.IsWorkspace ? next.WorkspaceAccess : next.Access;
            if (map.GetValueOrDefault(removal.Place) is { } level)
            {
                var pauses = level.Pauses.Where(pair => pair.Key != AccessKey.Landing).ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
                var rewritten = pauses.Count == 0 && !level.Full ? Without(map, removal.Place) : With(map, removal.Place, new AccessSetting { Full = level.Full, Pauses = pauses });
                next = removal.IsWorkspace ? next with { WorkspaceAccess = rewritten } : next with { Access = rewritten };
            }
        }

        return next;
    }

    private static IReadOnlyDictionary<string, T> Without<T>(IReadOnlyDictionary<string, T> map, string key) =>
        map.Where(pair => !string.Equals(pair.Key, key, StringComparison.OrdinalIgnoreCase)).ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);

    private static IReadOnlyDictionary<string, T> With<T>(IReadOnlyDictionary<string, T> map, string key, T value)
    {
        var next = new Dictionary<string, T>(map, StringComparer.OrdinalIgnoreCase);
        var spelled = next.Keys.FirstOrDefault(each => string.Equals(each, key, StringComparison.OrdinalIgnoreCase)) ?? key;
        next[spelled] = value;
        return next;
    }

    // ---- Undone (the cleanup design §4.5) -----------------------------------------------------------------------------------------------------------

    /// <summary>Why the keys the conversion wrote no longer read as it wrote them, or null.</summary>
    private static string? Moved(DriverConfig config, ConversionReceipt receipt)
    {
        if (receipt.DefaultWorkflow is { } wrote && config.DefaultWorkflow is { } now && now != wrote) return $"the home default is `{now}` now, not `{wrote}`.";
        foreach (var use in receipt.Uses.Where(each => each.Kind is null))
        {
            var held = use.IsWorkspace ? config.WorkspaceWorkflows.GetValueOrDefault(use.Place) : config.Workflows.GetValueOrDefault(use.Place);
            if (held?.Default != use.Workflow) return $"`{use.Place}` follows `{held?.Default ?? "nothing"}` now, not `{use.Workflow ?? "the home default"}`.";
        }

        foreach (var removal in receipt.Removals)
        {
            if (!Gone(config, removal) && !receipt.Held.Contains(Key(removal.Place, removal.IsWorkspace))) return $"`{removal.Place}` has rules again since the conversion.";
        }

        foreach (var policy in receipt.Policies)
        {
            var map = policy.IsWorkspace ? config.WorkspaceLinePolicies : config.LinePolicies;
            if (!map.TryGetValue(policy.Place, out var written) || written != policy.Policy) return $"`{policy.Place}`'s line policy changed since the conversion.";
        }

        return null;
    }

    private static DriverConfig Restored(DriverConfig config, ConversionReceipt receipt)
    {
        if (Moved(config, receipt) is { } moved) throw new DriverException($"moved: {moved}");
        var next = config;
        var before = receipt.Before!;
        if (receipt.DefaultWorkflow is not null) next = next.WithDefaultWorkflow(before.DefaultWorkflow);
        foreach (var use in receipt.Uses)
        {
            var key = Key(use.Place, use.IsWorkspace);
            if (!before.Choices.TryGetValue(key, out var wire)) continue;
            var old = ConversionReceipts.Unwire(wire, use.IsWorkspace);
            var map = use.IsWorkspace ? next.WorkspaceWorkflows : next.Workflows;
            var rewritten = old is null ? Without(map, use.Place) : With(map, use.Place, old);
            next = use.IsWorkspace ? next with { WorkspaceWorkflows = rewritten } : next with { Workflows = rewritten };
        }

        foreach (var policy in receipt.Policies)
        {
            next = policy.IsWorkspace ? next.WithWorkspaceLinePolicy(policy.Place, null) : next.WithLinePolicy(policy.Place, null);
        }

        foreach (var removal in receipt.Removals)
        {
            var key = Key(removal.Place, removal.IsWorkspace);
            if (before.Landings.TryGetValue(key, out var rule))
            {
                next = removal.IsWorkspace ? next with { WorkspaceLandings = With(next.WorkspaceLandings, removal.Place, rule) } : next with { Landings = With(next.Landings, removal.Place, rule) };
            }

            if (before.SessionChecks.TryGetValue(key, out var checks)) next = next with { SessionChecks = With(next.SessionChecks, removal.Place, checks) };
            if (before.Access.TryGetValue(key, out var said))
            {
                var map = removal.IsWorkspace ? next.WorkspaceAccess : next.Access;
                var level = map.GetValueOrDefault(removal.Place);
                var pauses = new Dictionary<string, string>(level?.Pauses ?? new Dictionary<string, string>(), StringComparer.Ordinal) { [AccessKey.Landing] = said };
                var rewritten = With(map, removal.Place, new AccessSetting { Full = level?.Full ?? false, Pauses = pauses });
                next = removal.IsWorkspace ? next with { WorkspaceAccess = rewritten } : next with { Access = rewritten };
            }
        }

        return next;
    }
}
