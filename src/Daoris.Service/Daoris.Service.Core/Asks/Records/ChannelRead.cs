namespace Daoris.Knowledge;

/// <summary>An ask's channel as one entry reads it.</summary>
/// <param name="Messages">The messages in the read, oldest first.</param>
/// <param name="Lines">Daoris's lines in the read, oldest first.</param>
/// <param name="Unread">
/// How many of the channel's items are after this entry's mark, counted before a read moved it. The person's (NOTIFY2g) counts only
/// what an agent or a teammate wrote: never their own posts on this machine, nor Daoris's lines.
/// </param>
/// <param name="Mark">The moment the entry had read up to before this read moved it; null when it never has.</param>
public sealed record ChannelRead(
    IReadOnlyList<ChannelMessage> Messages, IReadOnlyList<ChannelLine> Lines, int Unread, DateTimeOffset? Mark = null)
{
    /// <summary>Both kinds in time order, a line before a message of the same moment.</summary>
    public IReadOnlyList<IChannelItem> Items =>
        [.. Lines.Cast<IChannelItem>().Concat(Messages).OrderBy(item => item.At)];

    /// <summary>
    /// The reading entry's acknowledgment of each message, by its id (COWORK1d, the design §6): what a driver reads to hand a part only
    /// what its entry has not taken or read. Empty for the person's read and for an ask held elsewhere.
    /// </summary>
    public IReadOnlyDictionary<string, ChannelAck> Acknowledged { get; init; } = new Dictionary<string, ChannelAck>();

    /// <summary>
    /// For the person's read (COWORK1d, the design §8.4): each message's receipt for each repository it addresses, by the message's id.
    /// Empty for an entry's read and for an ask held elsewhere.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyList<ChannelReceipt>> Receipts { get; init; } =
        new Dictionary<string, IReadOnlyList<ChannelReceipt>>();

    /// <summary>For the person's read: the messages to one part each, never among <see cref="Messages"/>, which an older build reads as to everyone.</summary>
    public IReadOnlyList<ChannelMessage> Addressed { get; init; } = [];

    /// <summary>For the person's read: each of <see cref="Addressed"/>'s receipts in its part's line.</summary>
    public IReadOnlyDictionary<string, string> AddressedReceipts { get; init; } = new Dictionary<string, string>();
}
