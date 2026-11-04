using Daoris.Contracts.Asks;
using Daoris.Knowledge.Hosting;

namespace Daoris.Knowledge.Http.Routes;

/// <summary>The messages to one part, a local host's alone: what waits for one part a driver runs, and that part's acknowledgment.</summary>
public sealed class AddressedRoutes(HostWiring host) : IRouteModule
{
    private ComposedService Service => host.Service;

    public void Map(RouteMap routes)
    {
        if (host.Mode != ServiceMode.Local) return;

        routes.Map<ReadAddressed, AddressedResponse>(ReadAsync);
        routes.Map<AcknowledgeAddressed, ChannelAckResponse>(AcknowledgeAsync);
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
