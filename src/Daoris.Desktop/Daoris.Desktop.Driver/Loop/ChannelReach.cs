using System.Collections.Concurrent;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>The service's doors the channel's reach uses (SESSONE2a): a seam, so the look's rule is held by tests without a host.</summary>
public interface IChannelDoors
{
    /// <summary>The ask's channel as one entry reads it, which moves no mark; null where the host holds no such ask, or no channel.</summary>
    Task<ChannelView?> ReadAsync(string ask, string entry, CancellationToken ct);

    /// <summary>Move an entry's read mark to <paramref name="through"/>; a mark never moves back.</summary>
    Task MarkAsync(string ask, string entry, DateTimeOffset through, CancellationToken ct);

    /// <summary>The say door (MSG1a): the person's words kept on a parked or ended record of this machine's, for it to go on with.</summary>
    /// <param name="channel">The channel message's id (SESSONE2e): the ask holds the words as `channel`, so the door keeps no second word.</param>
    Task<SayAnswer> SayAsync(string session, string text, string channel, CancellationToken ct);
}

/// <summary>The channel's doors through the loop's service: <c>GET /api/asks/{id}/channel</c>, <c>POST …/channel/read</c> and the say door.</summary>
public sealed class ServiceChannelDoors(ServiceClient service) : IChannelDoors
{
    public Task<ChannelView?> ReadAsync(string ask, string entry, CancellationToken ct) => service.ChannelAsync(ask, entry, ct);

    public Task MarkAsync(string ask, string entry, DateTimeOffset through, CancellationToken ct) =>
        service.ChannelReadAsync(ask, entry, through, ct);

    public Task<SayAnswer> SayAsync(string session, string text, string channel, CancellationToken ct) =>
        service.SayAsync(session, text, null, ct, channel);
}

/// <summary>
/// A line of work on an ask as one look finds it (SESSONE2b): its entry, its newest part here, and that part's inbox where this
/// loop runs it and its door takes words (SESS3); none where the part is parked or ended.
/// </summary>
public sealed record ChannelPart(ChannelEntry Entry, string Session, DrivenInbox? Inbox);

/// <summary>
/// The look's half of an ask's channel (SESSONE2b, D167 point 7; the co-working design §2.3): what is new for each line of work on
/// an ask, past its entry's read mark, reaches the part that can take it now, and otherwise waits for the entry's next part, whose
/// instruction carries it (<see cref="ChannelText.Beneath"/>).
/// </summary>
/// <remarks>
/// <para><b>What reaches what.</b> A running part on the next-step door (D136) takes every message at its next step: the person's as
/// their words, another session's in Daoris's words as that agent's claim (<see cref="ChannelText.Claim"/>), which its record keeps
/// as Daoris's, never the person's. On a door that takes words only when its turn ends, the person's message is held for the turn's
/// end, as their words are (D137 §2); another session's is not, since words held there are prompted after the turn and would extend
/// the run. A parked or ended part takes the person's message to its repository on its record, by the say door, and goes on with it
/// as with their words (D137 §3); a message to everyone, and every other session's message, waits for the entry's next part. So an
/// agent's message never starts, reopens or extends a run (D155): it reaches a run that exists or comes for its own reasons.</para>
///
/// <para><b>Never reported as added.</b> The person's message is kept on the ask by the channel, as the word kind <c>channel</c>
/// (SESSONE2a), so a part that takes it does not report it to the ask again (<see cref="WordsOnAsk"/> is not called).</para>
///
/// <para><b>The mark.</b> One moment per entry (SESSONE2a), so it moves through the messages that reached a part, in order, and stops
/// before the first one held for the next part: that part is handed everything past it. A message past a held one that reached the
/// part is remembered here, by the part's session, so it is not handed to the same part twice; a new part is a new conversation, and
/// is handed it again. A reopen the service refused is remembered too, and not asked again.</para>
///
/// <para><b>The cost.</b> One read of the channel per line of work on an ask whose newest part here this loop runs, or is parked or
/// ended while its ask has a quest open, at each look; a mark's move only when one moved. A line of work whose ask has no quest open
/// is not read: its next part, if one comes, is handed what waited.</para>
/// </remarks>
public sealed class ChannelReach
{
    /// <summary>By the part's session: each message that reached it (true), or whose reopen the service refused (false).</summary>
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, bool>> _reached = new(StringComparer.Ordinal);

    /// <summary>
    /// The lines of work a look reads (see the remarks' cost): for each entry on an ask, its newest part here by when it was made,
    /// with its inbox where this loop runs it. A teammate's record, an intake, a part another loop runs and a part whose door takes
    /// no words while it runs are none.
    /// </summary>
    public static IReadOnlyList<ChannelPart> Parts(Snapshot snapshot, SessionProcesses processes)
    {
        var newest = snapshot.LastRun.Values
            .Where(part => !part.Teammate && ChannelEntry.Of(part.Entry) is not null)
            .GroupBy(part => part.Entry!, StringComparer.Ordinal)
            .Select(line => line.OrderByDescending(part => part.Created ?? DateTimeOffset.MinValue).ThenBy(part => part.Session, StringComparer.Ordinal).First())
            .ToList();
        var open = snapshot.Quests.Select(quest => ChannelEntry.ForQuest(quest, snapshot)?.Ask).OfType<string>().ToHashSet(StringComparer.Ordinal);

        var parts = new List<ChannelPart>();
        foreach (var part in newest)
        {
            var entry = ChannelEntry.Of(part.Entry)!;
            if (processes.InboxOf(part.Session) is { } inbox) parts.Add(new ChannelPart(entry, part.Session, inbox));
            else if (!part.Running && open.Contains(entry.Ask)) parts.Add(new ChannelPart(entry, part.Session, null));
        }

        return parts;
    }

    /// <summary>
    /// What a starting part of <paramref name="entry"/> is handed from the ask's channel (design §2.3, <i>held for the next part</i>):
    /// the messages for its entry past its mark, with the newest item read, which its mark moves to once the part has started
    /// (<see cref="HandedAsync"/>). Null where nothing waits, or the channel could not be read: a start is never held by it, and
    /// its mark stays for the next part.
    /// </summary>
    /// <exception cref="OperationCanceledException">The loop is closing.</exception>
    public static async Task<ChannelHeld?> HandAsync(IChannelDoors doors, ChannelEntry entry, CancellationToken ct)
    {
        try
        {
            if (await doors.ReadAsync(entry.Ask, entry.Key, ct).ConfigureAwait(false) is not { } read) return null;
            return new ChannelHeld(entry.Ask, entry.Key, read.ForEntry(), read.Newest);
        }
        catch (Exception error) when (error is DriverException or HttpRequestException or JsonException or InvalidOperationException
                                          || (error is OperationCanceledException && !ct.IsCancellationRequested))
        {
            return null;
        }
    }

    /// <summary>
    /// The entry's mark moved to what its started part was handed (design §2.3: <i>the mark moves when the part starts</i>), to the
    /// newest item read, a line included, so a message that arrived since is handed at the part's next step. A miss is the next
    /// part's to be handed again, never a failure of the start.
    /// </summary>
    public static async Task HandedAsync(IChannelDoors doors, ChannelHeld? held, CancellationToken ct)
    {
        if (held?.Through is not { } through) return;
        try
        {
            await doors.MarkAsync(held.Ask, held.Entry, through, ct).ConfigureAwait(false);
        }
        catch (Exception error) when (error is DriverException or HttpRequestException or JsonException or InvalidOperationException
                                          or ObjectDisposedException or OperationCanceledException)
        {
            // The mark stays: the entry's next part is handed the same messages again, which is no harm to a new conversation. Never
            // thrown, since the run's working moment calls it unawaited.
        }
    }

    /// <summary>One look's reach over <paramref name="parts"/>: the lines it says, one for each message that reached a part.</summary>
    /// <exception cref="OperationCanceledException">The loop is closing.</exception>
    public async Task<IReadOnlyList<string>> LookAsync(IChannelDoors doors, IReadOnlyList<ChannelPart> parts, CancellationToken ct)
    {
        var lines = new List<string>();
        foreach (var part in parts)
        {
            try
            {
                await LookAtAsync(doors, part, lines, ct).ConfigureAwait(false);
            }
            catch (Exception error) when (error is DriverException or HttpRequestException or JsonException or InvalidOperationException
                                              || (error is OperationCanceledException && !ct.IsCancellationRequested))
            {
                lines.Add($"channel  ask #{part.Entry.Ask}'s channel was not read for `{part.Entry.Repository}` at this look, and the next tries again: {error.Message}");
            }
        }

        // A part no look reads any more has ended its run here, or left the ask's open work: what reached it is forgotten.
        foreach (var session in _reached.Keys)
        {
            if (!parts.Any(part => string.Equals(part.Session, session, StringComparison.Ordinal))) _reached.TryRemove(session, out _);
        }

        return lines;
    }

    private async Task LookAtAsync(IChannelDoors doors, ChannelPart part, List<string> lines, CancellationToken ct)
    {
        if (await doors.ReadAsync(part.Entry.Ask, part.Entry.Key, ct).ConfigureAwait(false) is not { } read) return;

        var reached = _reached.GetOrAdd(part.Session, _ => new ConcurrentDictionary<string, bool>(StringComparer.Ordinal));
        DateTimeOffset? through = null;
        var holding = false;
        foreach (var message in read.ForEntry())
        {
            var took = reached.TryGetValue(message.Id!, out var before)
                ? before
                : await ReachAsync(doors, part, message, reached, lines, ct).ConfigureAwait(false);
            if (!took) holding = true;
            else if (!holding) through = message.At;
        }

        if (through is { } at) await doors.MarkAsync(part.Entry.Ask, part.Entry.Key, at, ct).ConfigureAwait(false);
    }

    /// <summary>Whether one message reached the part now (see the remarks); a reopen refused is remembered as not reached.</summary>
    private static async Task<bool> ReachAsync(
        IChannelDoors doors, ChannelPart part, ChannelItemView message, ConcurrentDictionary<string, bool> reached, List<string> lines,
        CancellationToken ct)
    {
        var id = message.Id!;
        if (part.Inbox is { } inbox)
        {
            var took = message.Person
                ? inbox.Hold(new ChatMessage(message.Text!, []) { Channel = message })
                : inbox.HoldDuringTurn(new ChatMessage(ChannelText.Claim(message), []) { Channel = message });
            if (!took) return false;

            reached[id] = true;
            var when = inbox.Reach == DrivenReach.TurnEnd ? "when its turn ends" : "at its next step";
            lines.Add(message.Person
                ? $"channel  the person's message `{id}` on ask #{part.Entry.Ask} reaches session {part.Session} {when}, as their words"
                : $"channel  message `{id}` on ask #{part.Entry.Ask} reaches session {part.Session} {when}, as `{message.Repository}`'s claim");
            return true;
        }

        // Only the person's words reopen a part, and only those addressed to its repository (design §2.3's third row).
        if (!message.Person || message.To is null) return false;

        var said = await doors.SayAsync(part.Session, message.Text!, id, ct).ConfigureAwait(false);
        reached[id] = said.Kept;
        lines.Add(said.Kept
            ? $"channel  the person's message `{id}` on ask #{part.Entry.Ask} was kept on session {part.Session}'s record, which goes on with it"
            : $"channel  the person's message `{id}` on ask #{part.Entry.Ask} waits for `{part.Entry.Repository}`'s next part: {said.Message}");
        return said.Kept;
    }
}
