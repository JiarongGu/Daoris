using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// The steward's own record of this run: what each act did, for the window's line and for the conditions
/// that read the steward's own acts (resume's and Try again's <c>not-own</c>). Memory alone, a restart forgets it, read and
/// written under the standing confirmation's one lock (<see cref="StandingState"/>).
/// </summary>
internal sealed class StewardRecord(StandingConfirmation owner, StandingState state)
{
    private readonly MachineLog _log = owner.Log;
    private readonly TimeProvider _clock = owner.Clock;

    /// <summary>A steward's act that ran, recorded as the steward's with the moment it ran: the line's entry and its conditions' memory.</summary>
    public void StewardDid(string id, string act, string? repository = null, string? quest = null, string? session = null)
    {
        lock (state.Gate) state.StewardDone.Add(new StandingState.StewardDoneAct(id, act, repository, quest, session, _clock.GetUtcNow()));
        _log.Info("role.act", ("role", AppointedRoles.Steward), ("act", act), ("outcome", "done"));
        // The act closes every open request on the desk that asked for it, by its act and what it acted on.
        owner.Desk.Answered(id, act, repository, quest, session);
    }

    /// <summary>
    /// Whether the steward's own hold of <paramref name="repository"/> this run still stands (the <c>not-own</c> rule):
    /// its last hold or resume there a hold, and nothing of the owner's on it since.
    /// </summary>
    public bool StewardHolds(string repository)
    {
        lock (state.Gate) return state.Latest(repository, StewardActNames.Hold, StewardActNames.Resume)?.Act == StewardActNames.Hold
                                 && !state.OwnerHolds(new RoleScope(OwnerKinds.Repository, repository));
    }

    /// <summary>Whether the steward drove <paramref name="repository"/> this run and nothing of the owner's touched it since.</summary>
    public bool StewardDrove(string repository)
    {
        lock (state.Gate) return state.Latest(repository, StewardActNames.Drive, StewardActNames.Undrive)?.Act == StewardActNames.Drive
                                 && !state.OwnerHolds(new RoleScope(OwnerKinds.Repository, repository));
    }

    /// <summary>
    /// The session of the steward's own stop of <paramref name="quest"/> this run that nothing has touched since (the
    /// <c>not-own</c> rule): its last stop or Try again there a stop, and nothing of the owner's on it since; else null.
    /// </summary>
    public string? StewardStopOf(string quest)
    {
        lock (state.Gate)
        {
            var last = state.StewardDone.LastOrDefault(each => each.Quest is { } named && string.Equals(named, quest, StringComparison.OrdinalIgnoreCase)
                                                               && each.Act is StewardActNames.Stop or StewardActNames.Retry);
            return last is { Act: StewardActNames.Stop } && !state.OwnerHolds(new RoleScope(OwnerKinds.Quest, quest)) ? last.Session : null;
        }
    }

    /// <summary>Whether the steward's last pause or resume of <paramref name="quest"/> this run is a pause, and nothing of the owner's on it since.</summary>
    public bool StewardPaused(string quest)
    {
        lock (state.Gate)
        {
            var last = state.StewardDone.LastOrDefault(each => each.Quest is { } named && string.Equals(named, quest, StringComparison.OrdinalIgnoreCase)
                                                               && each.Act is StewardActNames.Pause or StewardActNames.Unpause);
            return last is { Act: StewardActNames.Pause } && !state.OwnerHolds(new RoleScope(OwnerKinds.Quest, quest));
        }
    }

    /// <summary>
    /// Whether a steward's act caused a start of <paramref name="quest"/> in <paramref name="repository"/>: the repository driven by
    /// the steward this run and not the owner's since, or a live pending operation of the steward's on the quest.
    /// </summary>
    public bool StewardCaused(string repository, string quest)
    {
        lock (state.Gate)
        {
            if (state.OwnerHolds(new RoleScope(OwnerKinds.Quest, quest))) return false;
            return state.Acts.Pending(quest, state.Live)
                   || (state.Latest(repository, StewardActNames.Drive, StewardActNames.Undrive)?.Act == StewardActNames.Drive
                       && !state.OwnerHolds(new RoleScope(OwnerKinds.Repository, repository)));
        }
    }

    /// <summary>Whether this run's memory holds the steward's release <paramref name="act"/> of <paramref name="quest"/> at <c>released</c>, live.</summary>
    public bool StewardReleased(string quest, string act)
    {
        lock (state.Gate)
        {
            return state.Acts.Of(act) is { Stage: StewardStages.Released } operation
                   && string.Equals(operation.Target, quest, StringComparison.OrdinalIgnoreCase) && state.Live(operation.Generation)
                   && !state.OwnerHolds(new RoleScope(OwnerKinds.Quest, quest));
        }
    }

    /// <summary>The start's take of the steward's release: moved past <c>released</c> in one step, or null where it is cancelled.</summary>
    public RoleOperation? TakeRelease(string quest, string act)
    {
        RoleOperation? taken;
        lock (state.Gate)
        {
            taken = state.Acts.Of(act) is { } operation && string.Equals(operation.Target, quest, StringComparison.OrdinalIgnoreCase)
                    && !state.OwnerHolds(new RoleScope(OwnerKinds.Quest, quest))
                ? state.Acts.Take(act, StewardStages.Released, state.Live)
                : null;
        }

        _log.Info(taken is null ? "role.cancelled" : "role.act", ("role", AppointedRoles.Steward), ("act", StewardActNames.Retry),
            ("outcome", taken is null ? "withdrawn" : "started"));
        return taken;
    }

    /// <summary>A start a steward's act caused held for the owner (<c>held.checkout</c>): every pending operation on the quest ended.</summary>
    public void HeldForOwner(string quest)
    {
        int cancelled;
        lock (state.Gate) cancelled = state.Acts.CancelTarget(OwnerKinds.Quest, quest);
        _log.Info("role.cancelled", ("role", AppointedRoles.Steward), ("count", StandingState.Count(cancelled)), ("why", "held.checkout"));
    }

    /// <summary>The steward's acts this run, newest first, as the window's line reads them: each standing while the owner has not acted on its target since.</summary>
    public IReadOnlyList<StewardActNow> StewardActsNow()
    {
        lock (state.Gate)
        {
            var acts = new List<StewardActNow>();
            for (var at = state.StewardDone.Count - 1; at >= 0; at--)
            {
                var done = state.StewardDone[at];
                var target = done.Quest is { } quest ? new RoleScope(OwnerKinds.Quest, quest) : new RoleScope(OwnerKinds.Repository, done.Repository ?? "");
                var standing = !state.OwnerHolds(target);
                // A stop waits on a Try again while no later Try again of the steward's on its quest followed it and it still stands.
                var waiting = done.Act == StewardActNames.Stop && standing
                              && !state.StewardDone.Skip(at + 1).Any(later => later.Act == StewardActNames.Retry
                                                                              && string.Equals(later.Quest, done.Quest, StringComparison.OrdinalIgnoreCase));
                acts.Add(new StewardActNow(done.Id, done.Act, done.Repository, done.Quest, done.Session, done.At, standing, waiting, state.AllowedActs.ContainsKey(done.Id)));
            }

            return acts;
        }
    }
}
