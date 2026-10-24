using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// One act of a role's table (STEWARDROLE1a-i, D209 §3.1, §3.3): its name, its exact form, and its handler, run inside the role's door
/// once the act is admitted and past its gate. A later row adds its act behind its own look (§10's <see cref="StewardActs.Offered"/>).
/// </summary>
public interface IRoleAct
{
    /// <summary>The act's name, as <c>ROLE_ACT</c>'s <c>act</c> names it.</summary>
    string Name { get; }

    /// <summary>Whether <paramref name="form"/> is exactly the act's form (field names compared as D201's <c>IsForm</c> compares them, values exact).</summary>
    bool IsForm(JsonElement form);

    /// <summary>The thing the act acts on, as its exact form names it (a repository or a quest), which its record and its gate are keyed by.</summary>
    string Target(JsonElement form);

    /// <summary>What <see cref="Target"/> names (<see cref="OwnerKinds.Repository"/> or <see cref="OwnerKinds.Quest"/>; a session's act names its quest).</summary>
    string TargetKind => OwnerKinds.Repository;

    /// <summary>
    /// The act's conditions that read a dependency (<c>landing-pushes</c>, <c>no-tree</c>), judged again under the gates of its target and
    /// its dependencies before its handler runs (STEWARDROLE1a-ii, D209 §3.4 step 8): null where they hold, else the refusal's code.
    /// </summary>
    Task<string?> UnderGatesAsync(RoleActRun run, CancellationToken ct) => Task.FromResult<string?>(null);

    /// <summary>The act's handler, run under <see cref="PipeDoor.Role"/>; its answer is the route's.</summary>
    Task<object?> RunAsync(RoleActRun run, CancellationToken ct);
}

/// <summary>What an act's handler is handed: its admitted act's id, its role's judge (whose run's memory it registers a pending operation in), and its form.</summary>
public sealed record RoleActRun(string Id, string Role, string Act, JsonElement Form, StandingConfirmation Judge)
{
    /// <summary>What the steward's acts read and change (STEWARDROLE1b): the driver bridge's own, or a test's stand-in; null for none.</summary>
    public IStewardWorld? World { get; init; }
}

/// <summary>
/// The steward's acts (D209 §3.1, §10): its table, each row adding its act behind its own look, and the set this build offers, which an
/// appointment's <c>acts</c> keeps as its ask named it (§2.3). STEWARDROLE1b adds rows 1 to 8.
/// </summary>
public static class StewardActs
{
    /// <summary>The acts this build offers a new appointment, in §3.1's order: rows 1 to 8 (STEWARDROLE1b).</summary>
    public static IReadOnlyList<string> Offered { get; } = [.. StewardActRows.All.Select(each => each.Name)];

    /// <summary>The set-up act (§3.1 row 12), refused without the appointment's own tick.</summary>
    public const string Setup = "setup";

    /// <summary>The steward's table: §3.1 rows 1 to 8.</summary>
    public static IReadOnlyDictionary<string, IRoleAct> Table { get; } = StewardActRows.All.ToDictionary(each => each.Name, StringComparer.Ordinal);

    /// <summary>Each role's table for <c>ROLE_ACT</c>: the steward's; the supervisor's acts are its own routes, so its table here is empty.</summary>
    public static IReadOnlyDictionary<string, IRoleAct> TableOf(string role) =>
        role == Roles.Steward ? Table : new Dictionary<string, IRoleAct>(StringComparer.Ordinal);
}

/// <summary>Where an admitted act stands in the run's memory (§3.4 steps 7 and 8).</summary>
public static class RoleActStates
{
    public const string Admitted = "admitted";
    public const string Running = "running";
    public const string Done = "done";
    public const string Cancelled = "cancelled";
}

/// <summary>Where a pending operation stands (§5.1a): at a stage, bound, executed (its last stage taken), or cancelled.</summary>
public static class RoleOperationStates
{
    public const string Pending = "pending";
    public const string Bound = "bound";
    public const string Executed = "executed";
    public const string Cancelled = "cancelled";
}

/// <summary>
/// A pending operation as the run's memory holds it (§5.1a): the act it came from, its role, generation and holder, the actor it is
/// recorded as, its target, its kind, its stages in order and the one it is at, whether its last stage is a binding, its state, and the
/// complete operation its consumers act on — never what a file says.
/// </summary>
public sealed record RoleOperation(
    string Id, string Role, string Generation, string Holder, string Actor, string Target, string Kind, IReadOnlyList<string> Stages,
    int At, bool Binds, string State, object? Operation)
{
    /// <summary>What <see cref="Target"/> names, as its act's (<see cref="IRoleAct.TargetKind"/>).</summary>
    public string TargetKind { get; init; } = OwnerKinds.Repository;

    /// <summary>The stage its next consumer takes, or null where none is left.</summary>
    public string? Stage => State == RoleOperationStates.Pending && At < Stages.Count ? Stages[At] : null;
}

/// <summary>
/// The run's memory of a role's acts (STEWARDROLE1a-i, D209 §3.4 steps 7 and 8, §5.1a): every admitted act by its id, every pending
/// operation by its stages, and every hand-over binding. It is the judge's own state, every method called under the judge's lock
/// (<see cref="StandingConfirmation"/> holds it), so an admission, a gate's step, a consumer's take and a revoke's cancel never
/// interleave. Nothing here is written to disk; a restart forgets it all.
/// </summary>
internal sealed class RoleActsMemory
{
    private sealed record Admitted(string Id, string Role, string Act, string Generation, string Holder, string Target, string Kind)
    {
        public string State { get; set; } = RoleActStates.Admitted;

        /// <summary>That the owner's mark cancelled it (§5.4), so its refusal is <c>owner-set</c>.</summary>
        public bool ByOwner { get; set; }
    }

    private readonly Dictionary<string, Admitted> _acts = new(StringComparer.Ordinal);
    private readonly Dictionary<string, RoleOperation> _operations = new(StringComparer.Ordinal);

    /// <summary>Step 7: the act registered as <c>admitted</c>, its id drawn here, never the verb's.</summary>
    public string Admit(string role, string act, string generation, string holder, string target, string kind = OwnerKinds.Repository)
    {
        var id = "a" + Guid.NewGuid().ToString("N")[..15];
        _acts[id] = new Admitted(id, role, act, generation, holder, target, kind);
        return id;
    }

    /// <summary>The act's target, or null where no act has the id.</summary>
    public RoleScope? TargetOf(string id) => _acts.TryGetValue(id, out var act) ? new RoleScope(act.Kind, act.Target) : null;

    /// <summary>
    /// A session's act re-keyed to the quest its session resolved to (STEWARDROLE1b, D209 §5.4: a session resolved to its quest), so an
    /// owner's mark on the quest cancels it as it cancels the quest's own acts.
    /// </summary>
    public void Retarget(string id, RoleScope target)
    {
        if (_acts.TryGetValue(id, out var act)) _acts[id] = new Admitted(act.Id, act.Role, act.Act, act.Generation, act.Holder, target.Name, target.Kind)
        {
            State = act.State, ByOwner = act.ByOwner,
        };
    }

    /// <summary>Whether a live pending operation of any stage acts on <paramref name="target"/> (§3.1a: a start a steward's act caused).</summary>
    public bool Pending(string target, Func<string, bool> live) =>
        _operations.Values.Any(each => each.State is RoleOperationStates.Pending or RoleOperationStates.Bound
                                       && string.Equals(each.Target, target, StringComparison.OrdinalIgnoreCase) && live(each.Generation));

    /// <summary>An admitted act refused at its gates' step by the owner's mark: cancelled, never run.</summary>
    public void Refuse(string id)
    {
        if (_acts.TryGetValue(id, out var act) && act.State == RoleActStates.Admitted)
        {
            act.State = RoleActStates.Cancelled;
            act.ByOwner = true;
        }
    }

    /// <summary>Whether the owner's mark cancelled the act.</summary>
    public bool CancelledByOwner(string id) => _acts.TryGetValue(id, out var act) && act.ByOwner;

    /// <summary>The act's state, or null where no act has the id.</summary>
    public string? StateOf(string id) => _acts.TryGetValue(id, out var act) ? act.State : null;

    /// <summary>The act's generation, or null.</summary>
    public string? GenerationOf(string id) => _acts.TryGetValue(id, out var act) ? act.Generation : null;

    /// <summary>Step 8: an act still <c>admitted</c> and live moved to <c>running</c>; false where it was cancelled or is not admitted.</summary>
    public bool Run(string id, Func<string, bool> live)
    {
        if (!_acts.TryGetValue(id, out var act) || act.State != RoleActStates.Admitted) return false;
        if (!live(act.Generation))
        {
            act.State = RoleActStates.Cancelled;
            return false;
        }

        act.State = RoleActStates.Running;
        return true;
    }

    /// <summary>The handler returned; an act refused before its gates' step never ran, and is cancelled.</summary>
    public void Done(string id)
    {
        if (!_acts.TryGetValue(id, out var act)) return;
        if (act.State == RoleActStates.Running) act.State = RoleActStates.Done;
        else if (act.State == RoleActStates.Admitted) act.State = RoleActStates.Cancelled;
    }

    /// <summary>
    /// A pending operation registered for an act that ran (§5.1a): at its first stage where its generation is live, else cancelled at
    /// once. The operation's id is its act's.
    /// </summary>
    public RoleOperation? Pend(string id, string actor, string kind, IReadOnlyList<string> stages, bool binds, object? operation, Func<string, bool> live)
    {
        if (!_acts.TryGetValue(id, out var act) || stages.Count == 0) return null;
        var state = live(act.Generation) ? RoleOperationStates.Pending : RoleOperationStates.Cancelled;
        var pending = new RoleOperation(id, act.Role, act.Generation, act.Holder, actor, act.Target, kind, stages, 0, binds, state, operation)
        {
            TargetKind = act.Kind,
        };
        _operations[id] = pending;
        return pending;
    }

    /// <summary>A consumer's first step: the live operation on <paramref name="target"/> at <paramref name="stage"/>, memory first by target and stage.</summary>
    public RoleOperation? Find(string target, string stage, Func<string, bool> live) =>
        _operations.Values.FirstOrDefault(each => string.Equals(each.Target, target, StringComparison.OrdinalIgnoreCase)
                                                  && each.Stage == stage && live(each.Generation));

    /// <summary>
    /// A consumer's take (and, after its awaits, its second step, the execution cutoff): the operation still at <paramref name="stage"/>,
    /// its generation live, moved past it; its last stage taken makes it a binding where it binds, else executed. False otherwise.
    /// </summary>
    public RoleOperation? Take(string id, string stage, Func<string, bool> live)
    {
        if (!_operations.TryGetValue(id, out var operation) || operation.Stage != stage) return null;
        if (!live(operation.Generation))
        {
            _operations[id] = operation with { State = RoleOperationStates.Cancelled };
            return null;
        }

        var next = operation.At + 1;
        var taken = next < operation.Stages.Count
            ? operation with { At = next }
            : operation with { At = next, State = operation.Binds ? RoleOperationStates.Bound : RoleOperationStates.Executed };
        _operations[id] = taken;
        return taken;
    }

    /// <summary>The live binding on <paramref name="target"/>, read by each later start this run; null where none.</summary>
    public RoleOperation? Binding(string target, Func<string, bool> live) =>
        _operations.Values.FirstOrDefault(each => each.State == RoleOperationStates.Bound
                                                  && string.Equals(each.Target, target, StringComparison.OrdinalIgnoreCase) && live(each.Generation));

    /// <summary>The operation by its id, whatever its state.</summary>
    public RoleOperation? Of(string id) => _operations.GetValueOrDefault(id);

    /// <summary>
    /// A revoke's part of its mutation step (§3.4): every act of <paramref name="generations"/> still <c>admitted</c>, every stage of its
    /// pending operations not yet executed, and every binding, cancelled. Answers what it cancelled, for the log.
    /// </summary>
    public int Cancel(IReadOnlySet<string> generations)
    {
        var cancelled = 0;
        foreach (var act in _acts.Values.Where(each => each.State == RoleActStates.Admitted && generations.Contains(each.Generation)))
        {
            act.State = RoleActStates.Cancelled;
            cancelled++;
        }

        foreach (var operation in _operations.Values.ToList())
        {
            if (!generations.Contains(operation.Generation)) continue;
            if (operation.State is not (RoleOperationStates.Pending or RoleOperationStates.Bound)) continue;
            _operations[operation.Id] = operation with { State = RoleOperationStates.Cancelled };
            cancelled++;
        }

        return cancelled;
    }

    /// <summary>
    /// The owner's act on a target (§5.4, STEWARDROLE1a-ii's mark): every admitted act, pending stage and binding on it cancelled, each act
    /// remembered as the owner's so it is refused <c>owner-set</c>. Answers what it cancelled, for the log.
    /// </summary>
    public int CancelTarget(string kind, string target) =>
        Cancel(kind, each => string.Equals(each, target, StringComparison.OrdinalIgnoreCase));

    /// <summary>Every act and operation on a target of <paramref name="kind"/> cancelled: an owner's target that could not be resolved.</summary>
    public int CancelKind(string kind) => Cancel(kind, _ => true);

    private int Cancel(string kind, Func<string, bool> target)
    {
        var cancelled = 0;
        foreach (var act in _acts.Values.Where(each => each.State == RoleActStates.Admitted && each.Kind == kind && target(each.Target)))
        {
            act.State = RoleActStates.Cancelled;
            act.ByOwner = true;
            cancelled++;
        }

        foreach (var operation in _operations.Values.ToList())
        {
            if (operation.TargetKind != kind || !target(operation.Target)) continue;
            if (operation.State is not (RoleOperationStates.Pending or RoleOperationStates.Bound)) continue;
            _operations[operation.Id] = operation with { State = RoleOperationStates.Cancelled };
            cancelled++;
        }

        return cancelled;
    }
}
