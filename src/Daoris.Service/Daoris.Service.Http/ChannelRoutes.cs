using Daoris.Knowledge.Hosting;

namespace Daoris.Knowledge.Http;

/// <summary>
/// The channel's answers as the page and the driver read them, and the doors of the messages to one part: what waits for one part a
/// driver runs, and that part's acknowledgment. The driver's call is how it says it reads those messages; one that never asks is handed
/// none, and they wait for the part's own read.
/// </summary>
public static class ChannelRoutes
{
    /// <summary>
    /// One message or line of an ask's channel (SESSONE2a): a message's poster is named by its session, repository and agent, and the
    /// person's by <c>person</c>; Daoris's lines are coded and carry no sentence. COWORK1d: its level, and the reader's acknowledgment of
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
            Part: m.Part, Channel: m.Channel, Receipt: read?.AddressedReceipts.GetValueOrDefault(m.Id)),
        ChannelLine l => new ChannelItemResponse("line", l.At, From: l.From, To: l.To, Code: l.Code, Quest: l.Quest, Name: l.Name),
        _ => throw new InvalidOperationException("an item of the channel is a message or a line"),
    };

    public static ChannelResponse ToChannel(string ask, string entry, ChannelRead read) =>
        new(ask.TrimStart('#'), entry, [.. read.Items.Select(item => ToChannelItem(item, read))], read.Unread, read.Mark)
        {
            Addressed = read.Addressed.Count == 0 ? null : [.. read.Addressed.Select(item => ToChannelItem(item, read))],
        };

    /// <summary>The two doors of the messages to one part, local mode's alone.</summary>
    public static void MapChannelParts(this WebApplication app)
    {
        app.MapGet("/api/channel/addressed", async (ComposedService s, string? session, CancellationToken ct) =>
            string.IsNullOrWhiteSpace(session) || await s.Channel.Parts.WaitingAsync(session.Trim(), ct) is not { } waiting
                ? Results.NotFound(new ErrorResponse($"No session `{session}` with a channel here.", ChannelDesk.Spell(ChannelRefusal.NoSession)))
                : Results.Ok(new AddressedResponse([.. waiting.Select(each => ToChannelItem(each.Message) with { Ack = each.Ack?.State, AckSession = each.Ack?.Session })])));

        app.MapPost("/api/channel/addressed/ack", async (ComposedService s, AddressedAckRequest body, CancellationToken ct) =>
        {
            if (!ChannelAcks.Known(body.State) || string.IsNullOrWhiteSpace(body.Session))
            {
                return Results.BadRequest(new ErrorResponse(
                    "an acknowledgment names its session and a state: taking, released, sent, taken, read or partly", "bad-state"));
            }

            var changed = await s.Channel.Parts.AcknowledgeAsync(
                body.Session.Trim(), body.State!, [.. (body.Messages ?? []).OfType<string>()], DateTimeOffset.UtcNow, ct);
            return changed is null
                ? Results.NotFound(new ErrorResponse($"No session `{body.Session}` with a channel here.", ChannelDesk.Spell(ChannelRefusal.NoSession)))
                : Results.Ok(new ChannelAckResponse(changed));
        });
    }
}

/// <summary>What waits for one part a driver runs: the messages to it and on its line's own channel, each with its line's <c>ack</c>.</summary>
public sealed record AddressedResponse(IReadOnlyList<ChannelItemResponse> Messages);

/// <summary>One part's acknowledgment of the messages to it by their ids, as an entry's of an ask's: <c>taking</c>, <c>released</c>, <c>sent</c>, <c>taken</c>, <c>read</c>.</summary>
public sealed record AddressedAckRequest(string? Session, string? State, IReadOnlyList<string>? Messages);
