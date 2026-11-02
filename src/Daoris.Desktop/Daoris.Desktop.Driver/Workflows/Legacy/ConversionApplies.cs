using System.Security.Cryptography;
using System.Text;

namespace Daoris.Driver;

/// <summary>What an apply reads and writes: the conversion's world now, the one edit of <c>driver.json</c>, and the Current units open right now.</summary>
/// <param name="World">The world as it stands now: the file, the registry, the workflows and the bound runs with their units' state.</param>
/// <param name="OpenCurrent">
/// Inside the edit, under the file's lock: the places (<see cref="WorkflowLegacy.Held"/>'s keys) with a Current unit open now, read from the run
/// files as they are then, so a start that bound after the plan is seen.
/// </param>
public sealed record ConvertDoors(
    string Home, Func<CancellationToken, Task<ConvertWorld>> World, Action<Func<DriverConfig, DriverConfig>> Change, Func<DriverConfig> Config,
    Func<IReadOnlySet<string>> OpenCurrent);

/// <summary>What an apply or an undo came to: whether it is done, a code where it was refused, the sentence, and each operation as the receipt keeps it.</summary>
public sealed record ConvertResult(bool Done, string? Refused, string Message, string Digest, IReadOnlyList<ConversionOp> Operations, ConversionPlan? Plan = null);

/// <summary>The codes a conversion's press is refused by, each with nothing written.</summary>
public static class ConversionRefusals
{
    /// <summary>The plan the person read no longer holds; the answer carries the plan now.</summary>
    public const string Moved = "moved";

    public const string Digest = "digest";
    public const string Receipt = "receipt";
    public const string Choices = "choices";

    /// <summary>The plan writes nothing: what is left is kept, held or counted.</summary>
    public const string Empty = "empty";

    /// <summary>No conversion stands to undo, or a later one does.</summary>
    public const string NoneStands = "none-stands";
}

/// <summary>
/// The conversion applied and undone (WORKFLOWCLEAN1 1e; the cleanup design §4.4, §4.5): the plan again, refused <c>moved</c> where its digest
/// differs; a receipt written before the first effect; the workflow saves, each idempotent by its version's digest; then one edit of
/// <c>driver.json</c> that writes the choices, the default workflow, the line policies and the removals, judged again inside it.
/// </summary>
public static class ConversionApplies
{
    private const string Configuration = "configuration";

    /// <summary>The conversion plan <paramref name="digest"/> names, applied. A digest that holds a receipt goes on from it.</summary>
    public static async Task<ConvertResult> ApplyAsync(ConvertDoors doors, ConversionChoices choices, string digest, CancellationToken ct = default)
    {
        if (!PlaceDigests.Is(digest)) return Refuse(digest, ConversionRefusals.Digest, "name the plan's digest, as its plan says it: 64 lower-case hex.");
        if (choices.Problem is { } problem) return Refuse(digest, ConversionRefusals.Choices, problem);
        var receipts = new ConversionReceipts(doors.Home);
        ConversionReceipt? receipt;
        try
        {
            receipt = receipts.Read(digest);
        }
        catch (DriverException unread)
        {
            return Refuse(digest, ConversionRefusals.Receipt, unread.Message);
        }

        if (receipt is null || receipt.Undone)
        {
            var world = await doors.World(ct).ConfigureAwait(false);
            var plan = ConversionPlans.Plan(world, choices, receipts.HeldPlaces());
            if (plan.Digest != digest)
            {
                return new(false, ConversionRefusals.Moved,
                    $"the plan you read no longer holds: a rule, a workflow or a run moved since, so nothing was written. Plan it again (now `{plan.Digest[..12]}`).", digest, [], plan);
            }

            if (plan.Empty) return new(false, ConversionRefusals.Empty, "the plan writes nothing: what it lists is kept as it is, so there is nothing to apply.", digest, [], plan);
            receipt = Begin(plan, world.Config);
            receipts.Write(receipt);
        }
        else
        {
            receipt = Settle(doors, receipts, receipt);
        }

        return Continue(doors, receipts, receipt);
    }

    private static ConvertResult Refuse(string digest, string code, string message) => new(false, code, message, digest, []);

    private static ConversionReceipt Begin(ConversionPlan plan, DriverConfig config)
    {
        var operations = plan.Saves.Select(each => new ConversionOp($"save:{each.Id}", "workflow-save", $"{(each.Mode == "append" ? "a new version of" : "a new workflow")} `{each.Id}`: {each.Said}", PlaceOutcomes.Pending)).ToList();
        operations.Add(new(Configuration, Configuration, "the choices, the home default, the line policies and the removed rules, in one change", PlaceOutcomes.Pending));
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

    /// <summary>A restart's read of a receipt: the uncertain operations settled by their postconditions, never replayed blind.</summary>
    public static ConversionReceipt Settle(ConvertDoors doors, ConversionReceipts receipts, ConversionReceipt receipt)
    {
        foreach (var operation in receipt.Operations.Where(each => each.Outcome == PlaceOutcomes.Uncertain).ToList())
        {
            var done = operation.Id == Configuration ? Posted(doors.Config(), receipt) : operation.Kind == "workflow-save" && SaveHeld(doors.Home, receipt.Saves.First(each => $"save:{each.Id}" == operation.Id));
            receipt = receipt.With(operation.Id, each => each with { Outcome = done ? PlaceOutcomes.Done : PlaceOutcomes.Pending, Note = null });
        }

        receipts.Write(receipt);
        return receipt;
    }

    /// <summary>A restart's read of every conversion receipt: each uncertain operation settled by its postcondition, nothing written but the receipt.</summary>
    public static IReadOnlyList<ConversionReceipt> SettleAll(ConvertDoors doors)
    {
        var receipts = new ConversionReceipts(doors.Home);
        return [.. receipts.All().Select(each => each.Undone || each.Operations.All(op => op.Outcome != PlaceOutcomes.Uncertain) ? each : Settle(doors, receipts, each))];
    }

    private static bool SaveHeld(string home, ConvertSave save) =>
        WorkflowPorts.Store(home).Load(save.Id) is { Read: { Problem: null, Versions: [.., { Digest: { } digest }] } } && digest == save.Digest;

    private static ConvertResult Continue(ConvertDoors doors, ConversionReceipts receipts, ConversionReceipt receipt)
    {
        foreach (var save in receipt.Saves)
        {
            var id = $"save:{save.Id}";
            if (receipt.Of(id) is not { Outcome: PlaceOutcomes.Pending }) continue;
            receipt = SaveOne(doors, receipts, receipt, save);
        }

        string? refused = null;
        var saving = receipt.Operations.Where(each => each.Kind == "workflow-save").ToList();
        if (receipt.Of(Configuration) is { Outcome: PlaceOutcomes.Pending })
        {
            if (saving.Any(each => each.Outcome == PlaceOutcomes.Failed))
            {
                receipt = Write(receipts, receipt.With(Configuration, each => each with { Outcome = PlaceOutcomes.Failed, Note = "a workflow it chooses was not saved, so the configuration was not applied." }));
            }
            else if (saving.All(each => each.Outcome == PlaceOutcomes.Done))
            {
                (receipt, refused) = Configure(doors, receipts, receipt);
            }
        }

        var done = receipt.Applied;
        var held = receipt.Held.Count == 0 ? "" : $" {receipt.Held.Count} place{(receipt.Held.Count == 1 ? "" : "s")} keep their rules until their open unit ends.";
        return new(done, refused, done ? $"the conversion is applied.{held}" : "the conversion is not done: " + string.Join("; ", receipt.Operations.Where(each => each.Outcome != PlaceOutcomes.Done).Select(Said)),
            receipt.Digest, receipt.Operations);
    }

    private static string Said(ConversionOp operation) => $"{operation.Said} ({operation.Outcome}{(operation.Note is { } note ? $": {note}" : "")})";

    private static ConversionReceipt Write(ConversionReceipts receipts, ConversionReceipt receipt)
    {
        receipts.Write(receipt);
        return receipt;
    }

    private static ConversionReceipt SaveOne(ConvertDoors doors, ConversionReceipts receipts, ConversionReceipt receipt, ConvertSave save)
    {
        var id = $"save:{save.Id}";
        if (SaveHeld(doors.Home, save)) return Write(receipts, receipt.With(id, each => each with { Outcome = PlaceOutcomes.Done, Note = null }));
        var store = WorkflowPorts.Store(doors.Home);
        var found = store.Load(save.Id);
        if (save.Mode == "new" && found is not null)
        {
            return Write(receipts, receipt.With(id, each => each with { Outcome = PlaceOutcomes.Failed, Note = $"`{save.Id}` is a workflow here already that is not this one: plan it again." }));
        }

        if (save.Mode == "append" && !(found?.Read is { Problem: null, Versions: [.., var newest] } && newest.Version == save.Base))
        {
            return Write(receipts, receipt.With(id, each => each with { Outcome = PlaceOutcomes.Failed, Note = $"`{save.Id}` moved since it was planned: plan it again." }));
        }

        receipt = Write(receipts, receipt.With(id, each => each with { Outcome = PlaceOutcomes.Uncertain, Note = null }));
        try
        {
            var base_ = found?.Read.Versions[^1];
            var planned = WorkflowOperations.PlanGraph(doors.Home, found, save.Id, base_, save.Name, "screen", PlannedWorkflows.At, save.Graph, save.From);
            store.Write(save.Id, planned.Plan.Result!);
            return Write(receipts, receipt.With(id, each => each with { Outcome = PlaceOutcomes.Done, Note = null }));
        }
        catch (ChoiceWords refused)
        {
            return Write(receipts, receipt.With(id, each => each with { Outcome = PlaceOutcomes.Failed, Note = refused.Message }));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // The write's answer is lost: it may have landed, and the file found says.
            return Write(receipts, receipt.With(id, each => each with { Note = error.Message }));
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

    private static (ConversionReceipt Receipt, string? Refused) Configure(ConvertDoors doors, ConversionReceipts receipts, ConversionReceipt receipt)
    {
        receipt = Write(receipts, receipt.With(Configuration, each => each with { Outcome = PlaceOutcomes.Uncertain, Note = null }));
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
            return (Write(receipts, receipt.With(Configuration, each => each with { Outcome = PlaceOutcomes.Failed, Note = refused.Message })),
                moved ? ConversionRefusals.Moved : "refused");
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return (Write(receipts, receipt.With(Configuration, each => each with { Note = error.Message })), null);
        }

        // The places the judge left their rules at: what a later plan offers to remove.
        var kept2 = receipt.Removals.Select(each => Key(each.Place, each.IsWorkspace)).Where(held.Contains).Concat(receipt.Held).Distinct().ToList();
        receipt = Write(receipts, (receipt with { Held = kept2 }).With(Configuration, each => each with { Outcome = PlaceOutcomes.Done, Note = null }));
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

    // ---- Undo (the cleanup design §4.5) -------------------------------------------------------------------------------------------------------------------

    /// <summary>What <i>Undo</i> would put back, or why it is not offered: the latest conversion, done and not undone, its keys still as it wrote them.</summary>
    public static (ConversionReceipt? Receipt, string? Refusal, string? Digest, IReadOnlyList<string> Lines) UndoPlan(string home, DriverConfig config)
    {
        var receipts = new ConversionReceipts(home);
        var latest = receipts.Latest();
        if (latest is null || latest.Undone || !latest.Applied)
        {
            return (null, latest is null ? "no conversion has been applied here, so there is nothing to undo." : latest.Undone ? "the latest conversion was undone already." : "the latest conversion is not done: go on with it first.", null, []);
        }

        if (Moved(config, latest) is { } moved) return (latest, $"moved: {moved}", null, []);
        var lines = new List<string>();
        foreach (var removal in latest.Removals.Where(each => latest.Before is { } before && (before.Landings.ContainsKey(Key(each.Place, each.IsWorkspace)) || before.Access.ContainsKey(Key(each.Place, each.IsWorkspace)) || before.SessionChecks.ContainsKey(Key(each.Place, each.IsWorkspace)))))
        {
            lines.Add($"`{removal.Place}`: its rules come back ({string.Join(", ", removal.Keys)})");
        }

        foreach (var use in latest.Uses.Where(each => each.Kind is null)) lines.Add($"`{use.Place}`: its old choice comes back");
        if (latest.DefaultWorkflow is { } wrote) lines.Add($"the home default `{wrote}` is taken away");
        if (latest.Policies.Count > 0) lines.Add($"{latest.Policies.Count} line polic{(latest.Policies.Count == 1 ? "y" : "ies")} go back into their rules");
        lines.Add("the workflow versions it saved stay saved, unchosen.");
        var digest = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes("undo\n" + latest.Digest + "\n" + string.Join('\n', lines))));
        return (latest, null, digest, lines);
    }

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

    /// <summary>The undo pressed: the keys put back in one change, and the receipt marked undone. Refused <c>moved</c> where anything was moved since.</summary>
    public static ConvertResult Undo(ConvertDoors doors, string digest)
    {
        if (!PlaceDigests.Is(digest)) return Refuse(digest, ConversionRefusals.Digest, "name the undo plan's digest, as its plan says it: 64 lower-case hex.");
        var plan = UndoPlan(doors.Home, doors.Config());
        if (plan.Receipt is null || plan.Refusal is not null)
        {
            return Refuse(digest, plan.Refusal?.StartsWith("moved", StringComparison.Ordinal) == true ? ConversionRefusals.Moved : ConversionRefusals.NoneStands, plan.Refusal ?? "nothing stands to undo.");
        }

        if (plan.Digest != digest) return Refuse(digest, ConversionRefusals.Moved, $"the undo you read no longer holds, so nothing was written. Plan it again (now `{plan.Digest![..12]}`).");
        var receipts = new ConversionReceipts(doors.Home);
        var receipt = plan.Receipt;
        try
        {
            doors.Change(config => Restored(config, receipt));
        }
        catch (DriverException refused)
        {
            return Refuse(digest, ConversionRefusals.Moved, refused.Message);
        }

        receipts.Write(receipt with { UndoneAt = DateTimeOffset.UtcNow });
        return new(true, null, "the conversion is undone: its rules, access entries and choices are back; the versions it saved stay saved, unchosen.", digest, receipt.Operations);
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
