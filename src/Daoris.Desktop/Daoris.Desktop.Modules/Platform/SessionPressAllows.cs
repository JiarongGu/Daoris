using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// A working session's press that asks, first offered to the allow supervisor that answers its session's place. The request waits on the
/// pipe as one pending entry (<see cref="PendingPress"/>) under the pipe's gate and is settled once, by the first of: the supervisor's
/// allow, which runs the press as the session's own; its leave or ten minutes from the raise, which fall to the person's card; and the
/// terminal's withdrawal, which ends the reservation under it. Only that settlement runs the press or asks the card, outside the gate.
/// </summary>
public sealed class SessionPressAllows(InstancePipe pipe)
{
    /// <summary>The act of the allow kind that allows a working session's press.</summary>
    public const string Act = "once";

    /// <summary>The wait, ten minutes unless a test shortens it.</summary>
    public TimeSpan Limit { get; set; } = StewardAllows.Ten;

    private PipeState State => pipe.State;

    /// <summary>The press offered to the allow supervisor of its session's place, or asked of the person at once where none answers there.</summary>
    internal async Task RaiseAsync(PipeReserved reserved, Func<Task> card, string session, string repository)
    {
        var judge = pipe.StandingConfirmation;
        var place = judge.Placed(repository);
        if (judge.Admit(SupervisorKinds.Allow, place, Act).Why is not null)
        {
            await card().ConfigureAwait(false);
            return;
        }

        var route = $"{reserved.Request.Module}.{reserved.Request.Type}";
        var entry = new PendingPress(Guid.NewGuid().ToString("N")[..12], reserved, session, route, place, pipe.Clock.GetUtcNow(), card, reserved.Connection.Client);
        lock (State.Gate)
        {
            // Ended meanwhile by a withdrawal or a close: nothing more is asked.
            if (!State.Reserved.TryGetValue(reserved.Request.Id!, out var held) || !ReferenceEquals(held, reserved)) return;
            State.SessionAsks[entry.Id] = entry;
        }

        pipe.Log.Info("allow.raised", ("session", session), ("route", route));
        _ = ExpireAsync(entry);
    }

    /// <summary>The requests waiting for a supervisor now, oldest first; one the pipe no longer holds is forgotten.</summary>
    public IReadOnlyList<PendingPress> Waiting()
    {
        lock (State.Gate)
        {
            foreach (var gone in State.SessionAsks.Values.Where(each => !Live(each)).ToList()) State.SessionAsks.Remove(gone.Id);
            return [.. State.SessionAsks.Values.OrderBy(each => each.Raised)];
        }
    }

    /// <summary>A request still waiting; null where something settled it.</summary>
    public PendingPress? Find(string press)
    {
        lock (State.Gate)
        {
            return State.SessionAsks.TryGetValue(press, out var entry) && Live(entry) ? entry : null;
        }
    }

    private bool Live(PendingPress entry) =>
        State.Reserved.TryGetValue(entry.Reserved.Request.Id!, out var held) && ReferenceEquals(held, entry.Reserved);

    /// <summary>The supervisor's allow: settled once, then the press run as the session's own. Null where kept, else why not.</summary>
    public string? Allow(string press, Action<PendingPress>? kept = null)
    {
        PendingPress entry;
        lock (State.Gate)
        {
            if (!State.SessionAsks.TryGetValue(press, out entry!) || !Live(entry)) return SupervisorWhy.Answered;
            State.Reserved.Remove(entry.Reserved.Request.Id!);
            State.SessionAsks.Remove(press);
        }

        entry.Clock.Cancel();
        kept?.Invoke(entry);
        var request = entry.Reserved.Request;
        pipe.Delegate.Pressed(new PipeDelegateJudgement(PipeDelegateTier.Covered, Session: entry.Session, Own: true), request, entry.Reserved.Line.Task);
        _ = pipe.RunAsync(request, request.Id!, entry.Reserved.Binding, entry.Reserved.Line, client: entry.Client, worker: entry.Session);
        return null;
    }

    /// <summary>The supervisor's leave: the request falls to the person's card. False where something settled it first.</summary>
    public bool Leave(string press) => Fall(press);

    private bool Fall(string press)
    {
        PendingPress entry;
        lock (State.Gate)
        {
            if (!State.SessionAsks.TryGetValue(press, out entry!) || !Live(entry)) return false;
            State.SessionAsks.Remove(press);
        }

        entry.Clock.Cancel();
        _ = entry.Card();
        return true;
    }

    private async Task ExpireAsync(PendingPress entry)
    {
        try
        {
            await Task.Delay(Limit, pipe.Clock, entry.Clock.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (Fall(entry.Id)) pipe.Log.Info("supervisor.dropped", ("kind", SupervisorKinds.Allow), ("why", "expired"));
    }

    /// <summary>The pipe closes: every wait stops.</summary>
    internal void Close()
    {
        PendingPress[] every;
        lock (State.Gate)
        {
            every = [.. State.SessionAsks.Values];
            State.SessionAsks.Clear();
        }

        foreach (var entry in every) entry.Clock.Cancel();
    }
}
