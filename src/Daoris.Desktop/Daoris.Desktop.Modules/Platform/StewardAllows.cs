using Daoris.Driver;
using Shenora.Core.Ipc;

namespace Daoris.Desktop;

/// <summary>
/// A steward's act outside its grants, first offered to the allow supervisor that answers its target's place. The request waits on the
/// pipe as one pending entry (<see cref="PendingAllow"/>) under the pipe's gate and is settled once, by the first of: the supervisor's
/// allow, which admits the act as the steward's own; its leave, ten minutes from the raise, or the person's overrule of a held
/// <c>land</c>, each of which falls to the person's card; and the terminal's withdrawal, which ends the reservation under it. Only that
/// settlement runs the act or asks the card, outside the gate, so either happens once.
/// </summary>
public sealed class StewardAllows(InstancePipe pipe)
{
    /// <summary>How long a steward's request waits for the supervisor before it falls to the person's card.</summary>
    public static readonly TimeSpan Ten = TimeSpan.FromMinutes(10);

    /// <summary>The wait, ten minutes unless a test shortens it.</summary>
    public TimeSpan Limit { get; set; } = Ten;

    /// <summary>The window a <c>land</c> is held for in place of the allowing role's, where a test names one.</summary>
    public TimeSpan? Window { get; set; }

    /// <summary>Where an act's target is decided (<see cref="StewardPlaces"/>); null where nothing reads it, and the person's card is asked.</summary>
    public Func<RoleScope, CancellationToken, Task<RolePlace?>>? Places { get; set; }

    private PipeState State => pipe.State;

    /// <summary>
    /// A steward's press that asks, offered to the allow supervisor of its target's place, or asked of the person at once where its target
    /// does not read, no supervisor answers there or the supervisor's holder is the steward's own.
    /// </summary>
    internal async Task RaiseAsync(PipeReserved reserved, Func<Task> card, IpcRequest original, RoleJudgement asked, PipeClientProcess? client)
    {
        if (!await TryRaiseAsync(reserved, card, original, asked, client).ConfigureAwait(false)) await card().ConfigureAwait(false);
    }

    private async Task<bool> TryRaiseAsync(PipeReserved reserved, Func<Task> card, IpcRequest original, RoleJudgement asked, PipeClientProcess? client)
    {
        var judge = pipe.StandingConfirmation;
        RolePlace? place;
        try
        {
            place = await PlaceAsync(original, asked, reserved.Reading.Token).ConfigureAwait(false);
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            place = null;
        }

        if (place is not { } where || judge.AdmitStewardAllow(where, asked.Holder).Why is not null) return false;
        var entry = new PendingAllow(Guid.NewGuid().ToString("N")[..12], reserved, original, asked, where, pipe.Clock.GetUtcNow(), card, client);
        lock (State.Gate)
        {
            // Ended meanwhile by a withdrawal or a close: nothing more is asked.
            if (!State.Reserved.TryGetValue(original.Id, out var held) || !ReferenceEquals(held, reserved)) return true;
            State.StewardAsks[entry.Id] = entry;
        }

        pipe.Log.Info("allow.raised", ("role", asked.Role ?? ""), ("act", asked.Act ?? ""));
        _ = ExpireAsync(entry);
        return true;
    }

    private async Task<RolePlace?> PlaceAsync(IpcRequest original, RoleJudgement asked, CancellationToken ct)
    {
        if (Places is not { } places || RoleActForm.Read(original.Payload) is not { } form) return null;
        if (!pipe.StandingConfirmation.TableOf(asked.Role ?? "").TryGetValue(form.Act, out var row)) return null;
        return await places(new RoleScope(row.TargetKind, row.Target(form.Form)), ct).ConfigureAwait(false);
    }

    /// <summary>The requests waiting for a supervisor now, oldest first; one the pipe no longer holds is forgotten.</summary>
    public IReadOnlyList<PendingAllow> Waiting()
    {
        lock (State.Gate)
        {
            foreach (var gone in State.StewardAsks.Values.Where(each => !Live(each)).ToList()) State.StewardAsks.Remove(gone.Id);
            return [.. State.StewardAsks.Values.Where(each => each.State == PendingAllowState.Pending).OrderBy(each => each.Raised)];
        }
    }

    /// <summary>A request still held, waiting or an allowed <c>land</c> in its window; null where it was settled.</summary>
    public PendingAllow? Find(string ask)
    {
        lock (State.Gate)
        {
            return State.StewardAsks.TryGetValue(ask, out var entry) && Live(entry) ? entry : null;
        }
    }

    /// <summary>Under the gate: whether the pipe still holds the entry's reservation, which a withdrawal and a close take.</summary>
    private bool Live(PendingAllow entry) =>
        State.Reserved.TryGetValue(entry.Original.Id, out var held) && ReferenceEquals(held, entry.Reserved);

    /// <summary>
    /// The supervisor's allow: settled once, then the act admitted as the steward's own (the owner's marks read first) and run. A
    /// <c>land</c> is held for <paramref name="window"/> first, still the pending entry. Null where kept, else why it was not.
    /// </summary>
    public async Task<string?> AllowAsync(string ask, string decision, TimeSpan window)
    {
        PendingAllow entry;
        var hold = false;
        lock (State.Gate)
        {
            if (!State.StewardAsks.TryGetValue(ask, out entry!) || !Live(entry) || entry.State != PendingAllowState.Pending) return SupervisorWhy.Answered;
            window = Window ?? window;
            hold = entry.Asked.Act == StewardActNames.Land && window > TimeSpan.Zero;
            if (hold)
            {
                entry.State = PendingAllowState.Holding;
                entry.Decision = decision;
                entry.LandsAt = pipe.Clock.GetUtcNow() + window;
            }
            else
            {
                State.Reserved.Remove(entry.Original.Id);
                State.StewardAsks.Remove(ask);
            }
        }

        entry.Clock.Cancel();
        if (!hold)
        {
            RunAllowed(entry, decision);
            return null;
        }

        entry.Clock = new CancellationTokenSource();
        _ = HoldAsync(entry, window);
        return null;
    }

    /// <summary>The supervisor's leave: the request falls to the person's card. False where it was settled already.</summary>
    public bool Leave(string ask) => Fall(ask, PendingAllowState.Pending);

    /// <summary>The person's overrule of an allowed <c>land</c> held for its window: the person's card follows. False where it was settled already.</summary>
    public bool Overrule(string ask) => Fall(ask, PendingAllowState.Holding);

    private bool Fall(string ask, PendingAllowState from)
    {
        PendingAllow entry;
        lock (State.Gate)
        {
            if (!State.StewardAsks.TryGetValue(ask, out entry!) || !Live(entry) || entry.State != from) return false;
            State.StewardAsks.Remove(ask);
        }

        entry.Clock.Cancel();
        _ = entry.Card();
        return true;
    }

    private async Task ExpireAsync(PendingAllow entry)
    {
        try
        {
            await Task.Delay(Limit, pipe.Clock, entry.Clock.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        // The ten minutes close the subject for the supervisor before the card is reserved: a later answer is dropped.
        if (Fall(entry.Id, PendingAllowState.Pending)) pipe.Log.Info("supervisor.dropped", ("kind", SupervisorKinds.Allow), ("why", "expired"));
    }

    private async Task HoldAsync(PendingAllow entry, TimeSpan window)
    {
        try
        {
            await Task.Delay(window, pipe.Clock, entry.Clock.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        lock (State.Gate)
        {
            if (!State.StewardAsks.TryGetValue(entry.Id, out var held) || !ReferenceEquals(held, entry) || !Live(entry) || entry.State != PendingAllowState.Holding) return;
            State.Reserved.Remove(entry.Original.Id);
            State.StewardAsks.Remove(entry.Id);
        }

        RunAllowed(entry, entry.Decision ?? "");
    }

    // Outside the gate, the entry's reservation claimed: admitted as the steward's own, or answered with the refusal.
    private void RunAllowed(PendingAllow entry, string decision)
    {
        var judged = pipe.StandingConfirmation.AdmitAllowedAct(entry.Asked, entry.Original.Payload, decision);
        if (!judged.Covered)
        {
            pipe.Asking.EndReserved(entry.Reserved, pipe.Refuse(entry.Original, judged.Why ?? InstancePipe.Unclassed, PipeAdmission.RoleRefusal(judged.Why)));
            return;
        }

        _ = pipe.RunAsync(entry.Original, entry.Original.Id, entry.Reserved.Binding, entry.Reserved.Line, role: judged, client: entry.Client);
    }

    /// <summary>The pipe closes: every wait stops.</summary>
    internal void Close()
    {
        PendingAllow[] every;
        lock (State.Gate)
        {
            every = [.. State.StewardAsks.Values];
            State.StewardAsks.Clear();
        }

        foreach (var entry in every) entry.Clock.Cancel();
    }
}
