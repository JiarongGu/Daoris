using Daoris.Contracts.Asks;
using Daoris.Knowledge.Hosting;

namespace Daoris.Knowledge.Http.Routes;

/// <summary>
/// The messages to one part and a line's own channel, a local host's alone: what waits for one part a driver runs, that part's
/// acknowledgment, and the person's read of a line's own channel.
/// </summary>
public sealed class AddressedRoutes(HostWiring host) : IRouteModule
{
    private ComposedService Service => host.Service;

    public void Map(RouteMap routes)
    {
        if (host.Mode != ServiceMode.Local) return;

        routes.Map<ReadAddressed, AddressedResponse>(ReadAsync);
        routes.Map<AcknowledgeAddressed, ChannelAckResponse>(AcknowledgeAsync);
        routes.Map<ReadLineChannel, LineChannelResponse>(ReadLineAsync);
    }

    private async Task<Answer<LineChannelResponse>> ReadLineAsync(ReadLineChannel request, HttpContext http, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Session) || await Service.Channel.Line.ReadAsync(request.Session.Trim(), ct) is not { } read)
        {
            return NoSession(request.Session);
        }

        return new LineChannelResponse(read.Origin, [.. read.Messages.Select(message =>
            ChannelRoutes.ToChannelItem(message) with { Receipt = read.Receipts.GetValueOrDefault(message.Id) })]);
    }

    private async Task<Answer<AddressedResponse>> ReadAsync(ReadAddressed request, HttpContext http, CancellationToken ct) =>
        string.IsNullOrWhiteSpace(request.Session) || await Service.Channel.Parts.WaitingAsync(request.Session.Trim(), ct) is not { } waiting
            ? NoSession(request.Session)
            : new AddressedResponse([.. waiting.Select(each => ChannelRoutes.ToChannelItem(each.Message) with { Ack = each.Ack?.State, AckSession = each.Ack?.Session })]);

    private async Task<Answer<ChannelAckResponse>> AcknowledgeAsync(AcknowledgeAddressed request, HttpContext http, CancellationToken ct)
    {
        if (!ChannelAcks.Known(request.State) || string.IsNullOrWhiteSpace(request.Session))
        {
            return Refusal.BadRequest(
                "an acknowledgment names its session and a state: taking, released, sent, taken, read or partly", "bad-state");
        }

        var changed = await Service.Channel.Parts.AcknowledgeAsync(
            request.Session.Trim(), request.State!, [.. (request.Messages ?? []).OfType<string>()], DateTimeOffset.UtcNow, ct);
        return changed is null ? NoSession(request.Session) : new ChannelAckResponse(changed);
    }

    private static Refusal NoSession(string? session) =>
        Refusal.NotFound($"No session `{session}` with a channel here.", ChannelDesk.Spell(ChannelRefusal.NoSession));
}
