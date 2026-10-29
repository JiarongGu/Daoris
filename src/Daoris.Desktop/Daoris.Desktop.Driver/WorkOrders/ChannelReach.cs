using System.Collections.Concurrent;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>The service's doors the channel's reach uses (SESSONE2a): a seam, so the look's rule is held by tests without a host.</summary>
public interface IChannelDoors
{
    /// <summary>
    /// The ask's channel as one entry reads it, each message with the entry's acknowledgment of it (COWORK1d), which moves nothing; null
    /// where the host holds no such ask, or no channel.
    /// </summary>
    Task<ChannelView?> ReadAsync(string ask, string entry, CancellationToken ct);

    /// <summary>
    /// The entry's acknowledgment of messages by their ids (COWORK1d, the message-urgency design §6): <see cref="ChannelAcks.Taking"/>
    /// claims each for <paramref name="session"/>'s part, one part's only; the rest raise what the entry holds, never lowering it.
    /// </summary>
    /// <returns>The messages whose acknowledgment changed: for a claim, those this part won.</returns>
    Task<IReadOnlyList<string>> AcknowledgeAsync(
        string ask, string entry, string session, string state, IReadOnlyList<string> messages, CancellationToken ct);

    /// <summary>The say door (MSG1a): the person's words kept on a parked or ended record of this machine's, for it to go on with.</summary>
    /// <param name="channel">The channel message's id (SESSONE2e): the ask holds the words as `channel`, so the door keeps no second word.</param>
    /// <param name="from">
    /// Their author where not the person (COWORK1d): <c>agent</c> for another agent's claim the person passed on, never their words.
    /// </param>
    Task<SayAnswer> SayAsync(string session, string text, string channel, string? from, CancellationToken ct);
}

/// <summary>The channel's doors through the loop's service: <c>GET /api/asks/{id}/channel</c>, <c>POST …/channel/ack</c> and the say door.</summary>
public sealed class ServiceChannelDoors(ServiceClient service) : IChannelDoors
{
    public Task<ChannelView?> ReadAsync(string ask, string entry, CancellationToken ct) => service.ChannelAsync(ask, entry, ct);

    public Task<IReadOnlyList<string>> AcknowledgeAsync(
        string ask, string entry, string session, string state, IReadOnlyList<string> messages, CancellationToken ct) =>
        service.ChannelAckAsync(ask, entry, session, state, messages, ct);

    public Task<SayAnswer> SayAsync(string session, string text, string channel, string? from, CancellationToken ct) =>
        service.SayAsync(session, text, null, ct, channel, from: from);
}

/// <summary>
/// A line of work on an ask as one look finds it (SESSONE2b): its entry, a part of it here, and that part's inbox where this loop runs
/// it and its door takes words (SESS3); none where the part is parked or ended.
/// </summary>
public sealed record ChannelPart(ChannelEntry Entry, string Session, DrivenInbox? Inbox);

/// <summary>
/// The look's half of an ask's channel (SESSONE2b, D167 point 7; the co-working design §2.3; COWORK1d, D188, the message-urgency
/// design §2.3, §4.2, §5 and §6): what each line of work on an ask has not acknowledged reaches the part that can take it now, at its
/// level, and otherwise waits for the entry's next part, whose instruction carries it (<see cref="ChannelText.Beneath"/>).
/// </summary>
/// <remarks>
/// <para><b>What reaches what.</b> A running part takes the person's message as their words at their level (D137): urgent at its next
/// step where its door takes words then, else held for its turn's end, where it goes as its own prompt. Another session's message is
/// Daoris's words around that agent's claim (<see cref="ChannelText.Claim"/>), which its record keeps as Daoris's, never the person's:
/// urgent, it goes at the next step where the door takes words then; ordinary, and any claim on a door that takes words only at its
/// turn's end, it only rides a prompt the run sends anyway (<see cref="DrivenInbox.HoldClaim"/>), and alone at the turn's end is let
/// go for the entry's next part. A message from before, with no level, keeps its old reach, the next step. A parked or ended part takes
/// the person's message to its repository on its record, by the say door, and goes on with it as with their words (D137 §3); a message
/// to everyone, and every other session's message, waits for the entry's next part. So an agent's message never starts, reopens or
/// extends a run (D155): it reaches a run that exists, or rides one that comes for its own reasons.</para>
///
/// <para><b>By identity, one part each</b> (the design §6, traps 9 and 21). A part is handed only what its entry has not acknowledged by
/// the message's id, whatever the message's moment, and claims each on the entry (<see cref="ChannelAcks.Taking"/>) before anything
/// holds it, in the service's one statement, so of two live parts of one entry, tried newest first, one takes it. Its write and the
/// door's word acknowledge it <see cref="ChannelAcks.Sent"/> and <see cref="ChannelAcks.Taken"/>; a claim let go unsent is
/// <see cref="ChannelAcks.Released"/>. A held claim the agent read itself leaves the inbox. The entry's mark is the service's, for
/// display.</para>
///
/// <para><b>Never reported as added.</b> The person's message is kept on the ask by the channel, as the word kind <c>channel</c>
/// (SESSONE2a), so a part that takes it does not report it to the ask again (<see cref="WordsOnAsk"/> is not called).</para>
///
/// <para><b>The cost.</b> One read of the channel per part a look reaches: each part this loop runs of a line of work on an ask, or
/// its newest part parked or ended while its ask has a quest open; a claim per message a part can take. A line of work whose ask has
/// no quest open is not read: its next part, if one comes, is handed what waited.</para>
/// </remarks>
public sealed class ChannelReach
{
    /// <summary>By the part's session: the messages whose reopen the service refused, not asked again.</summary>
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, bool>> _refused = new(StringComparer.Ordinal);

    /// <summary>
    /// The parts a look reads (see the remarks' cost): for each entry on an ask, every part of it this loop runs with its inbox, newest
    /// first by when it was made; where none runs here, its newest part, parked or ended while its ask has a quest open. A teammate's
    /// record, an intake, a part another loop runs and a part whose door takes no words while it runs are none.
    /// </summary>
    public static IReadOnlyList<ChannelPart> Parts(Snapshot snapshot, SessionProcesses processes)
    {
        var lines = snapshot.LastRun.Values
            .Where(part => !part.Teammate && ChannelEntry.Of(part.Entry) is not null)
            .GroupBy(part => part.Entry!, StringComparer.Ordinal);
        var open = snapshot.Quests.Select(quest => ChannelEntry.ForQuest(quest, snapshot)?.Ask).OfType<string>().ToHashSet(StringComparer.Ordinal);

        var parts = new List<ChannelPart>();
        foreach (var line in lines)
        {
            var entry = ChannelEntry.Of(line.Key)!;
            var newestFirst = line.OrderByDescending(part => part.Created ?? DateTimeOffset.MinValue).ThenBy(part => part.Session, StringComparer.Ordinal).ToList();
            var running = newestFirst.Select(part => (part.Session, Inbox: processes.InboxOf(part.Session))).Where(part => part.Inbox is not null).ToList();
            if (running.Count > 0) parts.AddRange(running.Select(part => new ChannelPart(entry, part.Session, part.Inbox)));
            else if (!newestFirst[0].Running && open.Contains(entry.Ask)) parts.Add(new ChannelPart(entry, newestFirst[0].Session, null));
        }

        return parts;
    }

    /// <summary>
    /// What a starting part of <paramref name="entry"/> is handed from the ask's channel (design §2.3, <i>held for the next part</i>):
    /// the messages for its entry it has not acknowledged, which it acknowledges once it has started (<see cref="HandedAsync"/>). Null
    /// where nothing waits, or the channel could not be read: a start is never held by it.
    /// </summary>
    /// <exception cref="OperationCanceledException">The loop is closing.</exception>
    public static async Task<ChannelHeld?> HandAsync(IChannelDoors doors, ChannelEntry entry, string? session, CancellationToken ct)
    {
        try
        {
            if (await doors.ReadAsync(entry.Ask, entry.Key, ct).ConfigureAwait(false) is not { } read) return null;
            return new ChannelHeld(entry.Ask, entry.Key, read.ForEntry(session), session);
        }
        catch (Exception error) when (error is DriverException or HttpRequestException or JsonException or InvalidOperationException
                                          || (error is OperationCanceledException && !ct.IsCancellationRequested))
        {
            return null;
        }
    }

    /// <summary>
    /// What its started part was handed, acknowledged once the part works (COWORK1d, the design §6, trap 22): each message its packet
    /// carried whole <see cref="ChannelAcks.Taken"/>, and each it cut or left out among the older ones
    /// <see cref="ChannelAcks.Partly"/>, which a new part is handed again and the agent reads whole with <c>channel_read</c>. A miss is
    /// the next part's to be handed again, never a failure of the start.
    /// </summary>
    public static async Task HandedAsync(IChannelDoors doors, ChannelHeld? held, CancellationToken ct)
    {
        if (held is not { Messages.Count: > 0, Session: { } session }) return;
        var (shown, left) = ChannelText.Bounded(held.Messages);
        List<string> whole = [.. shown.Where(message => (message.Text?.Length ?? 0) <= ChannelText.MessageLimit).Select(message => message.Id!)];
        List<string> partly = [.. left.Concat(shown.Where(message => (message.Text?.Length ?? 0) > ChannelText.MessageLimit)).Select(message => message.Id!)];
        try
        {
            if (whole.Count > 0) await doors.AcknowledgeAsync(held.Ask, held.Entry, session, ChannelAcks.Taken, whole, ct).ConfigureAwait(false);
            if (partly.Count > 0) await doors.AcknowledgeAsync(held.Ask, held.Entry, session, ChannelAcks.Partly, partly, ct).ConfigureAwait(false);
        }
        catch (Exception error) when (error is DriverException or HttpRequestException or JsonException or InvalidOperationException
                                          or ObjectDisposedException or OperationCanceledException)
        {
            // Unacknowledged, the entry's next part is handed the same messages again, which is no harm to a new conversation. Never
            // thrown, since the run's working moment calls it unawaited.
        }
    }

    /// <summary>
    /// One look's reach over <paramref name="parts"/>: the lines it says, one for each message that reached a part, each classed as it
    /// is made (NOTIFY2h2): a channel not read the next look's, a message's reach what the ask's channel says.
    /// </summary>
    /// <param name="said">The look's classing; none classes and keeps nothing.</param>
    /// <exception cref="OperationCanceledException">The loop is closing.</exception>
    public async Task<IReadOnlyList<string>> LookAsync(IChannelDoors doors, IReadOnlyList<ChannelPart> parts, CancellationToken ct, LookSaid? said = null)
    {
        var lines = new LookLines(said ?? LookSaid.None, "channel");
        foreach (var part in parts)
        {
            try
            {
                await LookAtAsync(doors, part, lines, ct).ConfigureAwait(false);
            }
            catch (Exception error) when (error is DriverException or HttpRequestException or JsonException or InvalidOperationException
                                              || (error is OperationCanceledException && !ct.IsCancellationRequested))
            {
                lines.Retry($"channel  ask #{part.Entry.Ask}'s channel was not read for `{part.Entry.Repository}` at this look, and the next tries again: {error.Message}");
            }
        }

        // A part no look reads any more has ended its run here, or left the ask's open work: what it refused is forgotten.
        foreach (var session in _refused.Keys)
        {
            if (!parts.Any(part => string.Equals(part.Session, session, StringComparison.Ordinal))) _refused.TryRemove(session, out _);
        }

        return lines.Said;
    }

    private async Task LookAtAsync(IChannelDoors doors, ChannelPart part, LookLines lines, CancellationToken ct)
    {
        if (await doors.ReadAsync(part.Entry.Ask, part.Entry.Key, ct).ConfigureAwait(false) is not { } read) return;

        // A claim held for the turn's end that the agent read itself with `channel_read` leaves the inbox: never handed twice.
        if (part.Inbox is { } holding)
        {
            var readAlready = read.Items.Where(item => item.IsMessage && item.Ack == ChannelAcks.Read).Select(item => item.Id!).ToHashSet(StringComparer.Ordinal);
            if (readAlready.Count > 0) holding.DropClaims(readAlready);
        }

        var refused = _refused.GetOrAdd(part.Session, _ => new ConcurrentDictionary<string, bool>(StringComparer.Ordinal));
        foreach (var message in read.ForEntry(part.Session))
        {
            if (refused.ContainsKey(message.Id!)) continue;
            await ReachAsync(doors, part, message, refused, lines, ct).ConfigureAwait(false);
        }
    }

    /// <summary>One message to one part, at its level (see the remarks); a reopen refused is remembered and not asked again.</summary>
    private static async Task ReachAsync(
        IChannelDoors doors, ChannelPart part, ChannelItemView message, ConcurrentDictionary<string, bool> refused, LookLines lines,
        CancellationToken ct)
    {
        var id = message.Id!;
        // A message from before says no level and keeps its old reach, the next step where the door takes words then.
        var level = message.Urgent == false ? MessageLevel.Ordinary : MessageLevel.Urgent;
        if (part.Inbox is { } inbox)
        {
            // 🔴 Trap 3: a part whose turn is over, or whose inbox closed, takes no claim; the person's words it may still hold.
            if (!message.Person && !inbox.TakesClaims) return;

            // 🔴 Trap 21: claimed on the entry by its id before anything holds it, so one part of the entry takes it.
            var claimed = await doors.AcknowledgeAsync(part.Entry.Ask, part.Entry.Key, part.Session, ChannelAcks.Taking, [id], ct).ConfigureAwait(false);
            if (!claimed.Contains(id, StringComparer.Ordinal)) return;

            var acknowledge = Acknowledger(doors, part, id);
            var words = new ChatMessage(message.Person ? message.Text! : ChannelText.Claim(message), [])
            {
                Channel = message, Level = level, Acknowledge = acknowledge, Went = () => acknowledge(ChannelAcks.Sent),
            };
            var reach = inbox.ReachAt(level);
            if (!(message.Person ? inbox.Hold(words) : inbox.HoldClaim(words)))
            {
                await doors.AcknowledgeAsync(part.Entry.Ask, part.Entry.Key, part.Session, ChannelAcks.Released, [id], ct).ConfigureAwait(false);
                return;
            }

            var when = reach == DrivenReach.NextStep ? "at its next step" : "when its turn ends";
            lines.Progress(message.Person
                ? $"channel  the person's message `{id}` on ask #{part.Entry.Ask} reaches session {part.Session} {when}, as their words"
                : level == MessageLevel.Urgent && reach == DrivenReach.NextStep
                    ? $"channel  message `{id}` on ask #{part.Entry.Ask} reaches session {part.Session} at its next step, as `{message.Repository}`'s claim"
                    : $"channel  message `{id}` on ask #{part.Entry.Ask} rides what session {part.Session} is told anyway when its turn ends, "
                      + $"else waits for `{part.Entry.Repository}`'s next part, as `{message.Repository}`'s claim");
            return;
        }

        // Only the person's words reopen a part, and only those addressed to its repository (design §2.3's third row).
        if (!message.Person || message.To is null) return;

        var said = await doors.SayAsync(part.Session, message.Text!, id, null, ct).ConfigureAwait(false);
        if (said.Kept)
        {
            // Kept on the part's own record, whose queue carries it from here (COWORK1c): sent, as far as the channel goes.
            await doors.AcknowledgeAsync(part.Entry.Ask, part.Entry.Key, part.Session, ChannelAcks.Sent, [id], ct).ConfigureAwait(false);
        }
        else
        {
            refused[id] = true;
        }

        // Refused, the message waits for the entry's next part, which the ask's channel says: not asked again (NOTIFY2h2).
        lines.Progress(said.Kept
            ? $"channel  the person's message `{id}` on ask #{part.Entry.Ask} was kept on session {part.Session}'s record, which goes on with it"
            : $"channel  the person's message `{id}` on ask #{part.Entry.Ask} waits for `{part.Entry.Repository}`'s next part: {said.Message}");
    }

    /// <summary>
    /// The person's <i>Send to its session</i> (COWORK1d, the message-urgency design §4.2, §8.4): a message of the ask's channel, sent to
    /// the newest part here of <paramref name="repository"/>'s line of work on the ask. Because the person pressed, it may make a turn
    /// happen: to a running part it goes at the next step where the door takes words then, else as its turn's end's own prompt, a claim
    /// held only to ride going now as one; to a parked or ended part it is kept on its record by the say door, which may reopen it.
    /// Another agent's message stays that agent's claim, marked as passed on by the person (<c>from: agent</c>), never their words.
    /// </summary>
    public static async Task<ChannelForwarded> ForwardAsync(
        IChannelDoors doors, IReadOnlyCollection<PriorSession> records, SessionProcesses processes, string ask, string repository,
        string messageId, CancellationToken ct)
    {
        if (ChannelEntry.Of($"{ChannelEntry.AskOrigin}{ask.TrimStart('#')}/{repository}") is not { } entry
            || records.Where(part => !part.Teammate && string.Equals(part.Entry, entry.Key, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(part => part.Created ?? DateTimeOffset.MinValue).FirstOrDefault() is not { } newest)
        {
            return new ChannelForwarded(false, "no-part", null);
        }

        if (await doors.ReadAsync(entry.Ask, entry.Key, ct).ConfigureAwait(false) is not { } read
            || read.Items.FirstOrDefault(item => item.IsMessage && item.Id == messageId) is not { } message)
        {
            return new ChannelForwarded(false, "gone", newest.Session);
        }

        var part = new ChannelPart(entry, newest.Session, processes.InboxOf(newest.Session));
        var text = message.Person ? message.Text! : ChannelText.Claim(message, passedOn: true);
        if (part.Inbox is { } inbox)
        {
            // A claim this part holds only to ride goes now as a turn of its own: the person is between.
            if (inbox.PassOn(messageId)) return new ChannelForwarded(true, null, part.Session);

            var claimed = await doors.AcknowledgeAsync(entry.Ask, entry.Key, part.Session, ChannelAcks.Taking, [messageId], ct).ConfigureAwait(false);
            if (!claimed.Contains(messageId, StringComparer.Ordinal) && message.AckSession != part.Session)
            {
                return new ChannelForwarded(false, "held", part.Session);
            }

            var acknowledge = Acknowledger(doors, part, messageId);
            var held = inbox.Hold(new ChatMessage(text, [])
            {
                Channel = message, Level = MessageLevel.Urgent, Acknowledge = acknowledge, Went = () => acknowledge(ChannelAcks.Sent),
            });
            if (held) return new ChannelForwarded(true, null, part.Session);
            await doors.AcknowledgeAsync(entry.Ask, entry.Key, part.Session, ChannelAcks.Released, [messageId], ct).ConfigureAwait(false);
        }

        var said = await doors.SayAsync(part.Session, text, messageId, message.Person ? null : ChannelFrom.Agent, ct).ConfigureAwait(false);
        if (!said.Kept) return new ChannelForwarded(false, said.Refusal ?? "refused", part.Session);

        await doors.AcknowledgeAsync(entry.Ask, entry.Key, part.Session, ChannelAcks.Sent, [messageId], ct).ConfigureAwait(false);
        return new ChannelForwarded(true, null, part.Session);
    }

    /// <summary>
    /// What tells the entry a held message reached a state, from the inbox's lock or the door's thread: written beside the run, never
    /// awaited by it, one after another in the order told, and a miss costs the receipt, never the words.
    /// </summary>
    private static Action<string> Acknowledger(IChannelDoors doors, ChannelPart part, string id)
    {
        var gate = new object();
        var chain = Task.CompletedTask;
        return state =>
        {
            lock (gate)
            {
                chain = chain.ContinueWith(_ => AcknowledgeAsync(state), CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default)
                    .Unwrap();
            }
        };

        async Task AcknowledgeAsync(string state)
        {
            try
            {
                await doors.AcknowledgeAsync(part.Entry.Ask, part.Entry.Key, part.Session, state, [id], CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception error) when (error is DriverException or HttpRequestException or JsonException or InvalidOperationException
                                              or ObjectDisposedException or OperationCanceledException)
            {
                // The words went either way; the receipt waits for what the next look or read says.
            }
        }
    }
}
