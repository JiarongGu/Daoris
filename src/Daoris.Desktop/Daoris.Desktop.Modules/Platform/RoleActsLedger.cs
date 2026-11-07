using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// The run's memory of a role's acts (D209 §3.4 steps 7 and 8, §5.1a, §5.4): an admitted act run, done and its operations moved on, and
/// the owner's marks. Every step is one hold of the standing confirmation's one lock (<see cref="StandingState"/>), never across an await.
/// </summary>
internal sealed class RoleActsLedger(StandingConfirmation owner, StandingState state)
{
    private readonly MachineLog _log = owner.Log;

    /// <summary>
    /// §3.4 step 8, under the act's gates: the act still <c>admitted</c> (no revoke and no owner's mark cancelled it), its holder's hash
    /// present and its generation live, and its target not <c>owner-set</c>, moved to <c>running</c>; null where it may run, else
    /// <c>owner-set</c> or <c>role-changed</c>.
    /// </summary>
    public string? RunAct(string id)
    {
        lock (state.Gate)
        {
            if (state.Acts.TargetOf(id) is { } target && state.OwnerHolds(target))
            {
                state.Acts.Refuse(id);
                return RoleWhy.OwnerSet;
            }

            if (state.Acts.Run(id, state.Live)) return null;
            return state.Acts.CancelledByOwner(id) ? RoleWhy.OwnerSet : SupervisorWhy.RoleChanged;
        }
    }

    /// <summary>The admitted act's target, which its gates are taken for (§5.4); null where no act has the id.</summary>
    public RoleScope? ActTarget(string id)
    {
        lock (state.Gate) return state.Acts.TargetOf(id);
    }

    /// <summary>
    /// The owner's mark (STEWARDROLE1a-ii, D209 §5.4), set inside the gates of every scope it resolved to: each repository and quest
    /// marked <c>owner-set</c> for the run, every admitted act, pending stage and binding on it cancelled in this one step; an owner's
    /// target that could not be resolved holds every quest.
    /// </summary>
    public void MarkOwner(OwnerResolved resolved)
    {
        if (resolved.Marks.Count == 0 && !resolved.Unresolved) return;
        var cancelled = 0;
        lock (state.Gate)
        {
            foreach (var scope in resolved.Marks)
            {
                state.OwnerSet.Add(scope.Key);
                cancelled += state.Acts.CancelTarget(scope.Kind, scope.Name);
            }

            if (resolved.Unresolved)
            {
                state.OwnerUnresolved = true;
                cancelled += state.Acts.CancelKind(OwnerKinds.Quest);
            }
        }

        _log.Info("role.owner-set", ("count", StandingState.Count(resolved.Marks.Count)), ("unresolved", resolved.Unresolved ? "true" : "false"));
        if (cancelled > 0) _log.Info("role.cancelled", ("count", StandingState.Count(cancelled)), ("why", RoleWhy.OwnerSet));
    }

    /// <summary>Whether the owner acted on <paramref name="target"/> this run (§5.4).</summary>
    public bool IsOwnerSet(RoleScope target)
    {
        lock (state.Gate) return state.OwnerHolds(target);
    }

    /// <summary>The act's handler returned.</summary>
    public void ActDone(string id)
    {
        lock (state.Gate) state.Acts.Done(id);
    }

    /// <summary>The act's state in memory (<see cref="RoleActStates"/>), or null where none has the id.</summary>
    public string? ActState(string id)
    {
        lock (state.Gate) return state.Acts.StateOf(id);
    }

    /// <summary>
    /// A pending operation registered for an act that ran (§5.1a): its kind, its stages in order, whether its last is a binding, and the
    /// complete operation its consumers act on. Cancelled at once where its hold ended since the act ran.
    /// </summary>
    public RoleOperation? Pend(string id, string kind, IReadOnlyList<string> stages, bool binds, object? operation)
    {
        RoleOperation? pending;
        lock (state.Gate) pending = state.Acts.Pend(id, AppointedRoles.Steward, kind, stages, binds, operation, state.Live);
        if (pending is { State: RoleOperationStates.Cancelled }) _log.Info("role.cancelled", ("role", pending.Role), ("count", "1"));
        return pending;
    }

    /// <summary>A consumer's first step (§5.1a): the live operation on <paramref name="target"/> at <paramref name="stage"/>, from memory first; null where none.</summary>
    public RoleOperation? FindOperation(string target, string stage)
    {
        lock (state.Gate) return state.Acts.Find(target, stage, state.Live);
    }

    /// <summary>
    /// A consumer's take, and after its awaits its second step, the execution cutoff (§5.1a, finding 5): the operation still at
    /// <paramref name="stage"/> and live, moved past it; null where a revoke or an owner's act cancelled it, or another took it.
    /// </summary>
    public RoleOperation? TakeOperation(string id, string stage)
    {
        lock (state.Gate) return state.Acts.Take(id, stage, state.Live);
    }

    /// <summary>The quest's live hand-over binding, read by each later start this run (§5.1a); null where none.</summary>
    public RoleOperation? Binding(string target)
    {
        lock (state.Gate) return state.Acts.Binding(target, state.Live);
    }

    /// <summary>An operation by its id, whatever its state.</summary>
    public RoleOperation? Operation(string id)
    {
        lock (state.Gate) return state.Acts.Of(id);
    }

    /// <summary>
    /// The generation of a live hold of <paramref name="kind"/>, which its holder's verb proves over (D209 §3.3), or null where none is
    /// live (not staffed by the terminal, or not started). The supervisor's listing names its own the same way.
    /// </summary>
    public string? LiveGeneration(string kind)
    {
        lock (state.Gate) return state.Holds.FirstOrDefault(each => each.Kind == kind && state.Live(each.Generation))?.Generation;
    }

    /// <summary>A session's act re-keyed to the quest its session resolved to (§5.4), before its gates' step.</summary>
    public void Retarget(string id, RoleScope target)
    {
        lock (state.Gate) state.Acts.Retarget(id, target);
    }
}
