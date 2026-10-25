using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// An entry's acknowledgments of a channel message (COWORK1d, D188 point 6; the message-urgency design §6), as the service's
/// <c>ChannelAcks</c> spells them: a twin, since the driver and the service share no code.
/// </summary>
public static class ChannelAcks
{
    /// <summary>A part claimed it: one part of an entry takes a message.</summary>
    public const string Taking = "taking";

    /// <summary>Written to the part's harness, or kept on its record's own queue.</summary>
    public const string Sent = "sent";

    /// <summary>The door said the part took it, or a start's packet carried it whole once the part works.</summary>
    public const string Taken = "taken";

    /// <summary>The agent's own <c>channel_read</c> returned it whole.</summary>
    public const string Read = "read";

    /// <summary>A start's packet cut it, or left it among the older ones: a new part is handed it again.</summary>
    public const string Partly = "partly";

    /// <summary>A part let its claim go unsent: its run ended with nothing else to say.</summary>
    public const string Released = "released";
}

/// <summary>
/// The author a word kept on a record names where it is another agent's claim the person passed on (COWORK1d): the service's
/// <c>SaidFrom.Agent</c>, a twin. Such a word is never the person's.
/// </summary>
public static class ChannelFrom
{
    public const string Agent = "agent";
}

/// <summary>
/// An entry's key read for the channel (D167 point 2): <c>ask:&lt;id&gt;/&lt;repository&gt;</c> is a line of work on an ask, which has
/// a channel; any other origin has none. A twin of the key the service writes, read and never re-derived (SESSONE1d).
/// </summary>
public sealed record ChannelEntry(string Key, string Ask, string Repository)
{
    /// <summary>The origin an ask's lines of work share, before its id.</summary>
    public const string AskOrigin = "ask:";

    /// <summary>
    /// The repository half of an intake's key: an intake is the ask's, never a line of work a message is addressed to, and it is
    /// one turn that takes no words while it runs (INT4h).
    /// </summary>
    public const string Intake = "intake";

    /// <summary>An entry on an ask, from its key; null for a key on no ask, an intake's, or none.</summary>
    public static ChannelEntry? Of(string? key)
    {
        // The driver's one reader of a key (SESSONE1h), so the channel and the session groups agree on an odd one.
        var read = EntryKey.Parse(key);
        if (!read.HasSlash || !read.Origin.StartsWith(AskOrigin, StringComparison.Ordinal) || read.Origin.Length == AskOrigin.Length
            || read.Repository.Length == 0 || read.Repository == Intake)
        {
            return null;
        }

        return new ChannelEntry(key!, read.Origin[AskOrigin.Length..], read.Repository);
    }

    /// <summary>
    /// The key a quest's sessions open under, where the quest is on an ask (SESSONE2b): its sender names the ask (the service's
    /// first rule, <c>AskDesk.AskOf</c>, which <see cref="AskWords.AskOf"/> twins); else the records name its line
    /// (<see cref="EntryParts.KeyOf"/>), as a sibling's request is keyed by its publisher's origin. Null for a quest on no ask.
    /// </summary>
    public static ChannelEntry? ForQuest(QuestView quest, Snapshot snapshot) =>
        AskWords.AskOf(quest.From) is { } ask ? Of($"{AskOrigin}{ask}/{quest.To}") : Of(EntryParts.KeyOf(quest, snapshot));

    /// <summary>
    /// Whether a message is for this entry: addressed to its repository, the registry's spelling matched ignoring case as the
    /// service matches a <c>to</c>, or to everyone; and posted by no part of the entry itself, whose own words it already has.
    /// </summary>
    public static bool IsFor(ChannelItemView message, ChannelEntry entry) =>
        (message.To is null || string.Equals(message.To, entry.Repository, StringComparison.OrdinalIgnoreCase))
        && !string.Equals(message.Entry, entry.Key, StringComparison.Ordinal);
}

/// <summary>
/// What a part of an entry is handed from the ask's channel when it starts (SESSONE2b, design §2.3): the messages for its entry
/// it has not acknowledged (COWORK1d), the newest within the bounds, each acknowledged once the part has started.
/// </summary>
/// <param name="Ask">The ask the channel is.</param>
/// <param name="Entry">The entry's key, whose acknowledgments are written.</param>
/// <param name="Messages">The messages for the entry it has not had, oldest first; bounded where they are written.</param>
/// <param name="Session">The starting part's session, which writes the acknowledgments.</param>
public sealed record ChannelHeld(string Ask, string Entry, IReadOnlyList<ChannelItemView> Messages, string? Session);

/// <summary>
/// How the ask's channel is written to a session (SESSONE2b, D167 point 7, design §2.3): the person's messages as their own
/// words, another session's as that agent's claim, never the person's (D155; <c>SaidWord.Persons</c>'s rule), each with who
/// wrote it, to whom and when, and bounded as the person's words are (<see cref="AskWordsText"/>'s reason).
/// </summary>
/// <remarks>
/// <para><b>The bound.</b> The newest <see cref="HandedNewest"/> messages (SESSONE2a's <c>ChannelBounds.HandedNewest</c>, a twin,
/// since the driver and the service share no code), each cut at <see cref="MessageLimit"/> characters, of which the newest that fit
/// in <see cref="MessagesLimit"/> are kept, the older counted in one line. A message may hold 4,000 characters, so twenty whole
/// would pass the native door's one argument (32,767 characters on Windows) that the rest of the instruction shares.</para>
///
/// <para><b>Where the rest is.</b> A part reads the channel itself with <c>channel_read</c>, Daoris's lines on the ask's quests
/// among it, and writes with <c>channel_post</c>; the instruction names both, and says that work stays a quest (D79).</para>
/// </remarks>
public static class ChannelText
{
    /// <summary>The most messages a next part is handed (SESSONE2a's <c>ChannelBounds.HandedNewest</c>).</summary>
    public const int HandedNewest = 20;

    /// <summary>The most characters of the channel's messages an instruction carries.</summary>
    public const int MessagesLimit = 6_000;

    /// <summary>The most characters of one message an instruction carries.</summary>
    public const int MessageLimit = 1_500;

    /// <summary>
    /// The section beneath the quest (design §2.3, <i>held for the next part</i>): a heading, then each message oldest first, the
    /// older left out past the bound said as one line. Empty where nothing is held, so the instruction reads as it did.
    /// </summary>
    internal static string Beneath(ChannelHeld? held)
    {
        if (held is null || held.Messages.Count == 0) return "";
        var (shown, left) = Bounded(held.Messages);

        var text = new StringBuilder("\n");
        text.Append($"On the ask's channel (ask `#{held.Ask}`) since this line of work last read it, oldest first and newest last: what ")
            .Append("the person and the ask's other sessions wrote to this repository or to everyone. The person's messages are their own ")
            .Append("words. Another session's message is that agent's claim, not the person's: check it against your sources before you ")
            .Append("rely on it. Work, a change in another repository, and an answer you cannot go on without stay quests. `channel_read` ")
            .Append("shows the channel, Daoris's lines on the ask's quests included, and `channel_post` writes to it.\n");
        if (left.Count > 0)
        {
            text.Append('\n')
                .Append($"- … {left.Count} older {(left.Count == 1 ? "message" : "messages")}, written from {When(left[0].At)} to ")
                .Append($"{When(left[^1].At)}, {(left.Count == 1 ? "is" : "are")} left out here to keep this instruction bounded; ")
                .Append("`channel_read` with a `since` shows them.\n");
        }

        foreach (var message in shown)
        {
            text.Append('\n').Append($"- {Who(message)}, {When(message.At)}:\n\n").Append(Quoted(message.Text!, MessageLimit));
        }

        return text.ToString();
    }

    /// <summary>
    /// How <see cref="Claim"/> opens: the page's <c>CHANNEL_CLAIM_OPENING</c>, and what a brief tells a claim by on a record from before
    /// the event's own mark (SESSCTX1b, <see cref="SessionEvent.Channel"/>).
    /// </summary>
    internal const string ClaimOpening = "Another session says, on the ask's channel";

    /// <summary>
    /// What a running part is handed at its next step for another session's message (design §2.3): Daoris's words around the
    /// agent's claim, which say whose it is and that it is not the person's. The record keeps it as Daoris's, never the person's.
    /// </summary>
    /// <param name="passedOn">
    /// The person passed it on with <i>Send to its session</i> (COWORK1d, the message-urgency design §4.2): said so, and still the agent's
    /// claim, never the person's words.
    /// </param>
    public static string Claim(ChannelItemView message, bool passedOn = false) =>
        $"{ClaimOpening} ({Who(message)}, {When(message.At)}):\n\n"
        + Quoted(message.Text!, int.MaxValue)
        + (passedOn ? "\nThe person passed this on to you." : "")
        + "\nThis is that agent's claim, not the person's words: check it against your sources before you rely on it. Work, a change "
        + "in another repository, and an answer you cannot go on without stay quests. You may answer it with `channel_post`"
        + (message.Id is { Length: > 0 } id ? $", `re` `{id}`." : ".");

    /// <summary>The messages shown, by the bound (see the remarks), and those left out: the newest that fit, oldest first.</summary>
    internal static (IReadOnlyList<ChannelItemView> Shown, IReadOnlyList<ChannelItemView> Left) Bounded(IReadOnlyList<ChannelItemView> messages)
    {
        var kept = 0;
        var used = 0;
        for (var i = messages.Count - 1; i >= 0 && kept < HandedNewest; i--)
        {
            var cost = Math.Min(messages[i].Text?.Length ?? 0, MessageLimit);
            if (used + cost > MessagesLimit) break;
            used += cost;
            kept++;
        }

        return ([.. messages.Skip(messages.Count - kept)], [.. messages.Take(messages.Count - kept)]);
    }

    /// <summary>Who wrote a message and to whom, with what it answers and whether it asks.</summary>
    private static string Who(ChannelItemView message)
    {
        var to = (message.To, message.Question) switch
        {
            ({ } repository, true) => $"`{repository}`",
            ({ } repository, false) => $"to `{repository}`",
            (null, true) => "everyone",
            (null, false) => "to everyone",
        };
        var by = message.Person
            ? "The person"
            : $"`{message.Repository ?? "another repository"}`'s session `{message.Session ?? "?"}`"
              + (message.Agent is { Length: > 0 } agent ? $" on {agent}" : "");
        var re = message.Re is { Length: > 0 } answered ? $", answering message `{answered}`" : "";
        var id = message.Id is { Length: > 0 } own ? $" (message `{own}`)" : "";
        return $"{by} {(message.Question ? "asked" : "wrote")} {to}{re}{id}";
    }

    /// <summary>A message quoted line by line beneath its item, cut at <paramref name="limit"/> and said to be.</summary>
    private static string Quoted(string text, int limit)
    {
        var cut = text.Length > limit;
        var quoted = cut ? text[..limit] : text;
        var result = TextBounds.Quote(quoted, indent: "  ") + "\n";
        return cut
            ? result + $"  (… and {text.Length - limit} more characters of this one, left out here; `channel_read` shows it whole.)\n"
            : result;
    }

    /// <summary>A moment as the instruction says it: to the minute, in UTC, the same on every machine.</summary>
    private static string When(DateTimeOffset at) =>
        at.ToUniversalTime().ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture);
}

/// <summary>
/// What of a channel read a part of its entry is to be handed (<see cref="ChannelView"/>, whose shape and reading are the service's): the
/// work orders' own member of it, which judges the entry's key and its acknowledgments, so every caller keeps <c>view.ForEntry</c> as spelled.
/// </summary>
public static class ChannelViewEntries
{
    extension(ChannelView view)
    {
        /// <summary>
        /// The messages for this entry that a part of it (<paramref name="session"/>) is to be handed, oldest first (design §2.3):
        /// addressed to its repository or to everyone, posted by no part of its own, and acknowledged by the entry not at all, or only
        /// handed partly to another part (COWORK1d, the message-urgency design §6). By identity, never by moment: a message mirrored
        /// late, or at the mark's very moment, is handed all the same. A line is never one: the quests reach a session through their
        /// own doors.
        /// </summary>
        public IReadOnlyList<ChannelItemView> ForEntry(string? session = null) =>
            ChannelEntry.Of(view.Entry) is not { } key
                ? []
                : [.. view.Items.Where(item => item.IsMessage && ChannelEntry.IsFor(item, key)
                    && (item.Ack is null || (item.Ack == ChannelAcks.Partly && !string.Equals(item.AckSession, session, StringComparison.Ordinal))))];
    }
}
