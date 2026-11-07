using Daoris.Contracts.Brainstorms;

namespace Daoris.Knowledge.Http.Routes;

/// <summary>
/// The brainstorm and meeting desks a terminal reaches: the person's question written as the request the driver's look freezes into a
/// packet, the packet and document read back, the person's send, stop and clear. Kept where it is asked, carrying the asker's records
/// and this machine's accounts, so answered only to a caller on this machine and never a quest operation.
/// </summary>
public sealed class BrainstormRoutes(HostWiring host) : IRouteModule
{
    private readonly BrainstormDesk _brainstorms = BrainstormDesk.FromEnvironment();
    private readonly MeetingDesk _meetings = MeetingDesk.FromEnvironment();

    public void Map(RouteMap routes)
    {
        if (host.Mode != ServiceMode.Local) return;

        routes.Map<ListBrainstorms, BrainstormResponse[]>(ListAsync);
        routes.Map<AskBrainstorm, BrainstormActionResponse>(AskAsync);
        routes.Map<ShowBrainstorm, BrainstormShownResponse>(ShowAsync);
        routes.Map<SendBrainstorm, BrainstormActionResponse>(SendAsync);
        routes.Map<StopBrainstorm, BrainstormActionResponse>(StopAsync);
        routes.Map<ClearBrainstorm, BrainstormActionResponse>(ClearAsync);
    }

    private static Answer<T> OffMachine<T>() =>
        Refusal.NotFound("A brainstorm is kept on this machine, and answered only to a caller on this machine.");

    private Task<Answer<BrainstormResponse[]>> ListAsync(ListBrainstorms request, HttpContext http, CancellationToken ct) =>
        Task.FromResult<Answer<BrainstormResponse[]>>(
            host.MachineLocal(http) ? _brainstorms.List().Select(ToBrainstorm).ToArray() : OffMachine<BrainstormResponse[]>());

    // The one writer of a brainstorm's request, a meeting's included (`kind`); the caller's own `id` makes an ask idempotent.
    private Task<Answer<BrainstormActionResponse>> AskAsync(AskBrainstorm request, HttpContext http, CancellationToken ct)
    {
        if (!host.MachineLocal(http)) return Task.FromResult(OffMachine<BrainstormActionResponse>());
        return Task.FromResult(BrainstormKindOf(request.Kind) switch
        {
            BrainstormDesk.MeetingKind when request.Sources is { Count: > 0 } =>
                Reply(new(BrainstormRefusal.BadShape, "A meeting takes no named sources. Nothing was asked.")),
            BrainstormDesk.MeetingKind => Reply(_meetings.Ask(request.Question, request.Repository, request.Agents, DateTimeOffset.UtcNow, request.Id)),
            BrainstormDesk.BrainstormKind => Reply(
                _brainstorms.Ask(request.Question, request.Repository, request.Agents, DateTimeOffset.UtcNow, request.Id, request.Sources)),
            _ => Reply(BadKind(request.Kind)),
        });
    }

    private Task<Answer<BrainstormShownResponse>> ShowAsync(ShowBrainstorm request, HttpContext http, CancellationToken ct)
    {
        if (!host.MachineLocal(http)) return Task.FromResult(OffMachine<BrainstormShownResponse>());
        return Task.FromResult<Answer<BrainstormShownResponse>>(_brainstorms.Show(request.Id) is { } shown
            ? new BrainstormShownResponse(ToBrainstorm(shown.Brainstorm), shown.Frozen, shown.Document)
            : Refusal.NotFound(BrainstormDesk.NotFoundSentence(request.Id)));
    }

    // `kind=meeting` addresses a meeting; `round=<n>` names the round a meeting's send is for.
    private Task<Answer<BrainstormActionResponse>> SendAsync(SendBrainstorm request, HttpContext http, CancellationToken ct)
    {
        if (!host.MachineLocal(http)) return Task.FromResult(OffMachine<BrainstormActionResponse>());
        return Task.FromResult(BrainstormKindOf(request.Kind) switch
        {
            BrainstormDesk.MeetingKind => Reply(_meetings.Send(request.Id, request.Round, DateTimeOffset.UtcNow)),
            BrainstormDesk.BrainstormKind when request.Round is not null => Reply(
                new(BrainstormRefusal.BadShape, "A brainstorm has one packet to send, no rounds; a round is a meeting's.")),
            BrainstormDesk.BrainstormKind => Reply(_brainstorms.Send(request.Id, DateTimeOffset.UtcNow)),
            _ => Reply(BadKind(request.Kind)),
        });
    }

    private Task<Answer<BrainstormActionResponse>> StopAsync(StopBrainstorm request, HttpContext http, CancellationToken ct)
    {
        if (!host.MachineLocal(http)) return Task.FromResult(OffMachine<BrainstormActionResponse>());
        return Task.FromResult(BrainstormKindOf(request.Kind) switch
        {
            BrainstormDesk.MeetingKind => Reply(_meetings.Stop(request.Id, DateTimeOffset.UtcNow)),
            BrainstormDesk.BrainstormKind => Reply(_brainstorms.Stop(request.Id, DateTimeOffset.UtcNow)),
            _ => Reply(BadKind(request.Kind)),
        });
    }

    private Task<Answer<BrainstormActionResponse>> ClearAsync(ClearBrainstorm request, HttpContext http, CancellationToken ct)
    {
        if (!host.MachineLocal(http)) return Task.FromResult(OffMachine<BrainstormActionResponse>());
        return Task.FromResult(BrainstormKindOf(request.Kind) switch
        {
            BrainstormDesk.MeetingKind => Reply(_meetings.Clear(request.Id)),
            BrainstormDesk.BrainstormKind => Reply(_brainstorms.Clear(request.Id)),
            _ => Reply(BadKind(request.Kind)),
        });
    }

    // The kind a door is asked of: none is a brainstorm; anything but the two is no kind.
    private static string? BrainstormKindOf(string? kind) => string.IsNullOrWhiteSpace(kind)
        ? BrainstormDesk.BrainstormKind
        : kind.Trim().ToLowerInvariant() is var word and (BrainstormDesk.BrainstormKind or BrainstormDesk.MeetingKind) ? word : null;

    private static BrainstormOutcome BadKind(string? kind) =>
        new(BrainstormRefusal.BadShape, $"A brainstorm's kind is `brainstorm` or `meeting`; `{kind}` is neither. Nothing was done.");

    // A shape is a bad request, nothing by that name is not found, and a state it is in, a clear the disk would not finish or no home is
    // the lock's own shape, 409.
    private static Answer<BrainstormActionResponse> Reply(BrainstormOutcome outcome) => outcome.Refusal switch
    {
        BrainstormRefusal.None => new BrainstormActionResponse(outcome.Brainstorm is { } b ? ToBrainstorm(b) : null, outcome.Message),
        BrainstormRefusal.NotFound => Refusal.NotFound(outcome.Message),
        BrainstormRefusal.BadShape => Refusal.BadRequest(outcome.Message),
        _ => Refusal.Conflict(outcome.Message),
    };

    private static BrainstormResponse ToBrainstorm(BrainstormStanding b) =>
        new(b.Id, b.Question, b.Repository, b.Agents, b.At, b.State, b.Code, b.Packet, b.Sent,
            b.Kind == BrainstormDesk.MeetingKind ? b.Kind : null, b.Kind == BrainstormDesk.MeetingKind ? b.Round : null);
}
