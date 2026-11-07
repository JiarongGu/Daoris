using Daoris.Contracts.Review;
using Daoris.Contracts.Sessions;

namespace Daoris.Knowledge.Http.Routes;

/// <summary>
/// The second opinions a local host serves the driver, answered only to a caller on this machine: the candidate is this machine's
/// commits until they land, and the reviewer names an account. What the reviewer says and what the working session answers come
/// through each one's own connector, and no door here answers a dispute for the person.
/// </summary>
public sealed class OpinionRoutes(HostWiring host) : IRouteModule
{
    private ComposedService Service => host.Service;

    public void Map(RouteMap routes)
    {
        if (host.Mode != ServiceMode.Local) return;

        routes.Map<AskOpinion, OpinionActionResponse>(AskAsync);
        routes.Map<ListOpinions, OpinionResponse[]>(ListAsync);
        routes.Map<ReadOpinion, OpinionResponse>(ReadAsync);
        routes.Map<HandOpinion, OpinionActionResponse>(HandAsync);
    }

    private async Task<Answer<OpinionActionResponse>> AskAsync(AskOpinion request, HttpContext http, CancellationToken ct)
    {
        if (!host.MachineLocal(http)) return OffMachine;

        // A part left out arrives blank, refused by the desk naming which; `minutes` left out is 0, which is no bound.
        var candidate = request.Candidate;
        var reviewer = request.Reviewer;
        var ask = new OpinionAsk(
            request.Occasion ?? "", request.Pass ?? "", request.Working ?? "",
            new OpinionCandidate(
                candidate?.Repository ?? "", candidate?.Base ?? "", candidate?.Tip ?? "", [.. (candidate?.Commits ?? []).Select(commit => commit ?? "")]),
            new OpinionReviewer(reviewer?.Adapter ?? "", reviewer?.Label ?? "")
            {
                Product = reviewer?.Product, Maker = reviewer?.Maker, Account = reviewer?.Account,
            },
            request.Posture ?? "", request.Minutes ?? 0, request.Tree ?? "")
        {
            Rechecks = request.Rechecks,
            Families = [.. (request.Families ?? []).Select(family => family ?? "")],
            DoorVersion = request.DoorVersion,
        };
        return await ReplyAsync(await Service.Opinions.AskAsync(ask, DateTimeOffset.UtcNow, ct), http, ct);
    }

    private async Task<Answer<OpinionResponse[]>> ListAsync(ListOpinions request, HttpContext http, CancellationToken ct)
    {
        if (!host.MachineLocal(http)) return Refusal.NotFound(OffMachineSentence);

        var listed = await Service.Opinions.ListAsync(request.Working, request.Session, request.Repository, DateTimeOffset.UtcNow, ct);
        return listed.Select(OpinionAnswers.ToOpinion).ToArray();
    }

    private async Task<Answer<OpinionResponse>> ReadAsync(ReadOpinion request, HttpContext http, CancellationToken ct)
    {
        if (!host.MachineLocal(http)) return Refusal.NotFound(OffMachineSentence);

        return await Service.Opinions.ReadAsync(request.Id, DateTimeOffset.UtcNow, ct) is { } standing
            ? OpinionAnswers.ToOpinion(standing)
            : Refusal.NotFound($"There is no second opinion `{request.Id.Trim()}` on this machine.");
    }

    private async Task<Answer<OpinionActionResponse>> HandAsync(HandOpinion request, HttpContext http, CancellationToken ct) =>
        host.MachineLocal(http)
            ? await ReplyAsync(await Service.Opinions.HandAsync(request.Id, DateTimeOffset.UtcNow, ct, request.To, settled: request.Settled == true), http, ct)
            : OffMachine;

    private const string OffMachineSentence = "A second opinion is kept on this machine, and answered only to a caller on this machine.";

    private static Answer<OpinionActionResponse> OffMachine => Refusal.NotFound(OffMachineSentence);

    // A shape is a bad request, nothing by that name is not found, and a bound or a state the opinion is in is the lock's own shape,
    // 409. Done, the opinion as it now stands with the desk's sentence.
    private async Task<Answer<OpinionActionResponse>> ReplyAsync(OpinionOutcome outcome, HttpContext http, CancellationToken ct)
    {
        if (outcome.Refusal != OpinionRefusal.None)
        {
            return outcome.Refusal switch
            {
                OpinionRefusal.NotFound or OpinionRefusal.Shared => Refusal.NotFound(outcome.Message),
                OpinionRefusal.BadShape => Refusal.BadRequest(outcome.Message),
                _ => Refusal.Conflict(outcome.Message),
            };
        }

        var standing = (await Service.Opinions.ReadAsync(outcome.Opinion!.Id, DateTimeOffset.UtcNow, ct))!;
        return new OpinionActionResponse(
            OpinionAnswers.ToOpinion(standing), outcome.Message,
            outcome.Session is { } session ? await SessionAnswers.ToSessionAsync(Service, session, host.MachineLocal(http)) : null,
            outcome.Word is { } word ? SessionAnswers.ToSaid(word) : null,
            outcome.SettledOn, outcome.Waiting ? true : null);
    }
}
