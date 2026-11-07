using Daoris.Contracts.Asks;
using Daoris.Knowledge.Hosting;

namespace Daoris.Knowledge.Http;

/// <summary>
/// The channel's answers as the page and the driver read them.
/// </summary>
public static class ChannelRoutes
{
    /// <summary>
    /// One message or line of an ask's channel: a message's poster is named by its session, repository and agent, and the
    /// person's by <c>person</c>; Daoris's lines are coded and carry no sentence. Its level, and the reader's acknowledgment of
    /// it or, for the person, what it reached in each repository it addresses; a message to one part, its part, channel and receipt.
    /// </summary>
    public static ChannelItemResponse ToChannelItem(IChannelItem item, ChannelRead? read = null) => item switch
    {
        ChannelMessage m => new ChannelItemResponse(
            "message", m.At, Id: m.Id, Person: m.ByPerson, Session: m.Session, Repository: m.Repository, Agent: m.Agent, Entry: m.Entry,
            To: m.To, Re: m.Re, Question: m.Question, Open: m.Open, Text: m.Text, Origin: m.Origin, Urgent: m.Urgent,
            Ack: read?.Acknowledged.GetValueOrDefault(m.Id)?.State, AckSession: read?.Acknowledged.GetValueOrDefault(m.Id)?.Session,
            Receipts: read?.Receipts.GetValueOrDefault(m.Id) is { } receipts
                ? [.. receipts.Select(r => new ChannelReceiptResponse(r.Repository, r.State))]
                : null,
            Part: m.Part, Channel: m.Channel, Receipt: read?.AddressedReceipts.GetValueOrDefault(m.Id), Outside: m.Outside ? true : null),
        ChannelLine l => new ChannelItemResponse("line", l.At, From: l.From, To: l.To, Code: l.Code, Quest: l.Quest, Name: l.Name),
        _ => throw new InvalidOperationException("an item of the channel is a message or a line"),
    };

    public static ChannelResponse ToChannel(string ask, string entry, ChannelRead read) =>
        new(ask.TrimStart('#'), entry, [.. read.Items.Select(item => ToChannelItem(item, read))], read.Unread, read.Mark)
        {
            Addressed = read.Addressed.Count == 0 ? null : [.. read.Addressed.Select(item => ToChannelItem(item, read))],
        };
}
