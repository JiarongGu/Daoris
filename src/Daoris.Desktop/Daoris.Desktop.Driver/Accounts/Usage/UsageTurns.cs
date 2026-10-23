using System.Runtime.CompilerServices;

namespace Daoris.Driver;

/// <summary>
/// Whose a session's turns are (USAGECAP1a2, D179 point 2), as its start chose: the account's owner (AGT7) and the account, the
/// door it runs through, and what that door's token report means.
/// </summary>
/// <param name="Agent">The account's owner, or null for an agent that declares no accounts.</param>
/// <param name="Account">
/// The account's folder name, <see cref="AccountWindows.Own"/> for the tool's own sign-in as its readings name it, or null where
/// the agent has no accounts.
/// </param>
/// <param name="Door">The adapter the session runs through.</param>
/// <param name="Meaning">
/// What its turns' counts mean: the door's own <see cref="HarnessToolchain.TurnReport"/>, or <see cref="UsageHistory.MeaningUnknown"/>
/// where it declares none.
/// </param>
public sealed record TurnBinding(string? Agent, string? Account, string Door, string Meaning)
{
    public string? Actor { get; init; }
    /// <summary>A session's binding as its start chose the account: <paramref name="profile"/>, or null for the tool's own.</summary>
    public static TurnBinding Of(ISessionAdapter adapter, string? profile)
    {
        var owner = adapter.Toolchain?.Owner(adapter.Name);
        return new(
            owner, owner is null ? null : profile ?? AccountWindows.Own, adapter.Name,
            adapter.Toolchain?.TurnReport ?? UsageHistory.MeaningUnknown);
    }
}

/// <summary>One turn as its session's record ended it (USAGECAP1a2), for <see cref="UsageHistory.Turned"/>.</summary>
/// <param name="Seq">The turn's event's sequence in its session's record: with the session, the key against a turn heard twice.</param>
/// <param name="Started">When the session took the turn's ask, or null where this process did not see it.</param>
/// <param name="Ended">When the turn's end was recorded.</param>
/// <param name="Tokens">Its four counts as the door reported them, or null where it reported none.</param>
/// <param name="Models">The models its door said, or null where it said none.</param>
/// <param name="Cancelled">Whether the turn was stopped.</param>
public sealed record TurnSeen(
    string Session, long Seq, DateTimeOffset? Started, DateTimeOffset Ended, TurnTokens? Tokens, TurnModels? Models, bool Cancelled);

/// <summary>
/// The usage history's watch on the sessions' records (USAGECAP1a2, D179 point 2): each turn a record ends is kept in the turns'
/// log, under the account its start chose (<see cref="Bound"/>), from the session's ask to the turn's end.
/// </summary>
/// <remarks>
/// <para><b>One watch per record</b>, however many of the loop's per-look instances share it: a turn is heard once and kept
/// once, and its session and event sequence are the key besides (<see cref="UsageHistory.Turned"/>).</para>
/// <para><b>The ask</b> is the person's or the driver's words where the session took them, as the machine log times a turn
/// (<see cref="SessionLog"/>): the words shown the moment they are said (<see cref="SessionEvent.Reaches"/>) are no ask, and a
/// turn already in flight keeps its start.</para>
/// <para><b>A session no start bound</b> (one begun before this process, or by a door that binds none) is kept with no account,
/// never guessed at.</para>
/// <para><b>Held in memory</b> for the process's life: each home's newest <see cref="Remembered"/> bindings and asks. A restart
/// binds the sessions it starts again.</para>
/// </remarks>
public static class UsageTurns
{
    /// <summary>How many sessions' bindings, and asks in flight, each home's watch remembers.</summary>
    private const int Remembered = 4096;

    private static readonly ConditionalWeakTable<SessionEvents, Watch> Watches = new();

    private static readonly object Gate = new();

    // The account each session's start chose, by home and session: the newest last, the oldest dropped past the bound.
    private static readonly Dictionary<string, (Dictionary<string, TurnBinding> Sessions, Queue<string> Order)> Bindings =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Watch a sessions' record for its turns, once whoever asks (the loop's context, the chat runner): the record handed back.
    /// </summary>
    /// <param name="clock">When a turn is written; the system's but in a test. The first watch's stands.</param>
    public static SessionEvents Watched(string home, SessionEvents events, Func<DateTimeOffset>? clock = null)
    {
        _ = Watches.GetValue(events, each => new Watch(home, each, clock ?? (() => DateTimeOffset.UtcNow)));
        return events;
    }

    /// <summary>
    /// A session's account as its start chose it, where the account is chosen (<see cref="Driver.Said"/>, the chat runner's
    /// start): its turns are kept under it from now on. A later start of the same session (an account moved, a conversation
    /// going on) replaces it.
    /// </summary>
    public static void Bound(string home, SessionEvents events, string session, TurnBinding binding)
    {
        Watched(home, events);
        lock (Gate)
        {
            if (!Bindings.TryGetValue(KeyOf(home), out var held))
            {
                held = (new Dictionary<string, TurnBinding>(StringComparer.Ordinal), new Queue<string>());
                Bindings[KeyOf(home)] = held;
            }

            if (held.Sessions.TryAdd(session, binding)) held.Order.Enqueue(session);
            else held.Sessions[session] = binding;
            while (held.Order.Count > Remembered) held.Sessions.Remove(held.Order.Dequeue());
        }
    }

    public static void ActorBound(string home, SessionEvents events, string session, string actor)
    {
        if (BindingOf(home, session) is { } binding) Bound(home, events, session, binding with { Actor = actor });
    }

    private static TurnBinding? BindingOf(string home, string session)
    {
        lock (Gate)
        {
            return Bindings.TryGetValue(KeyOf(home), out var held) && held.Sessions.TryGetValue(session, out var binding) ? binding : null;
        }
    }

    private static string KeyOf(string home) => Path.GetFullPath(home);

    /// <summary>One record's watch: each session's ask in flight, and each turn's end written.</summary>
    private sealed class Watch
    {
        private readonly string _home;
        private readonly Func<DateTimeOffset> _clock;
        private readonly Dictionary<string, DateTimeOffset> _asked = new(StringComparer.Ordinal);

        public Watch(string home, SessionEvents events, Func<DateTimeOffset> clock)
        {
            _home = home;
            _clock = clock;
            events.Evented += Heard;
        }

        private void Heard(string session, SessionEvent e)
        {
            switch (e.Kind)
            {
                case SessionEventKind.User when e.Reaches is null:
                    lock (_asked)
                    {
                        if (_asked.TryAdd(session, e.At) && _asked.Count > Remembered)
                        {
                            _asked.Remove(_asked.MinBy(pair => pair.Value).Key);
                        }
                    }

                    return;
                case SessionEventKind.Turn:
                    DateTimeOffset? started;
                    lock (_asked) started = _asked.Remove(session, out var asked) ? asked : null;
                    UsageHistory.Turned(
                        _home, BindingOf(_home, session),
                        new TurnSeen(session, e.Seq, started, e.At, e.Tokens, e.Models, e.StopReason == "cancelled"), _clock());
                    return;
            }
        }
    }
}
