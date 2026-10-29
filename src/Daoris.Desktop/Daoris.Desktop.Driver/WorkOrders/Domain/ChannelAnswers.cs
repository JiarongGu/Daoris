namespace Daoris.Driver;

/// <summary>
/// What the person's <i>Send to its session</i> did (COWORK1d, the message-urgency design §4.2, §8.4): sent, to which part, or why not by
/// a code the page words: <c>no-part</c> (no part of that line of work on this machine), <c>gone</c> (the channel no longer holds it),
/// <c>held</c> (another part of the entry holds it now), or the say door's own refusal. Declared here, beside the answer the page reads,
/// since the loop's <c>ChannelReach</c> makes it and no module may name the loop (the code-structure design §5).
/// </summary>
public sealed record ChannelForwarded(bool Sent, string? Code, string? Session);

/// <summary>
/// One item of an ask's channel as the page reads it (SESSONE2c): a message's words, or one of Daoris's coded lines. COWORK1d: a message's
/// level (null for one from before) and what it reached in each repository it addresses.
/// </summary>
public sealed record ChannelItemAnswer(
    string Kind, DateTimeOffset At, string? Id, bool Person, string? Session, string? Repository, string? Agent, string? Entry, string? To,
    string? Re, bool Question, bool Open, string? Text, string? Code, string? Quest, string? Name, string? From, bool? Urgent = null,
    IReadOnlyList<ChannelReceiptView>? Receipts = null);

/// <summary>The person's <i>Send to its session</i> (COWORK1d): sent, to which session, or the code that says why not.</summary>
public sealed record ChannelForwardAnswer(bool Sent, string? Code, string? Session);

/// <summary>An ask's channel as the person reads it: its items in time order, how many are past their mark, and the mark.</summary>
public sealed record ChannelAnswer(string Ask, string Entry, IReadOnlyList<ChannelItemAnswer> Items, int Unread, DateTimeOffset? Mark);

/// <summary>The person's post: the message kept, or the service's code where it refused, which the page words (<see cref="ChannelPosted"/>).</summary>
public sealed record ChannelPostAnswer(bool Posted, string? Code, ChannelItemAnswer? Message);

/// <summary>The asks held elsewhere as the page reads them (<c>ASKS_ELSEWHERE</c>).</summary>
public sealed record ElsewhereAnswer(IReadOnlyList<ElsewhereAskAnswer> Asks);

/// <summary>
/// What the channel's doors answer the page (SESSONE2c, D167, D50): the shell's <c>CHANNEL_READ</c>, <c>CHANNEL_POST</c> and
/// <c>CHANNEL_MARK</c> return these, so the page reads what the service says field for field. A session and an entry are ids and
/// keys, and a message's words are as written, cleaned of paths by the service (D167 point 8): never a path of this machine.
/// </summary>
public static class ChannelAnswers
{
    /// <summary>The channel as the page reads it.</summary>
    public static ChannelAnswer Read(ChannelView view) =>
        new(view.Ask, view.Entry, [.. view.Items.Select(Item)], view.Unread, view.Mark);

    /// <summary>A channel nothing is on: an ask the host does not hold, or a host from before the channel.</summary>
    public static ChannelAnswer Empty(string ask) => new(ask.TrimStart('#'), "person", [], 0, null);

    /// <summary>The person's post as the page reads it.</summary>
    public static ChannelPostAnswer Posted(ChannelPosted posted) =>
        new(posted.Ok, posted.Code, posted.Message is { } message ? Item(message) : null);

    /// <summary>The person's <i>Send to its session</i> as the page reads it.</summary>
    public static ChannelForwardAnswer Forwarded(ChannelForwarded forwarded) => new(forwarded.Sent, forwarded.Code, forwarded.Session);

    private static ChannelItemAnswer Item(ChannelItemView item) => new(
        item.Kind, item.At, item.Id, item.Person, item.Session, item.Repository, item.Agent, item.Entry, item.To, item.Re,
        item.Question, item.Open, item.Text, item.Code, item.Quest, item.Name, item.From, item.Urgent, item.Receipts);
}
