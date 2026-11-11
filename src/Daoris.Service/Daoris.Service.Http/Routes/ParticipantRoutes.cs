using Daoris.Contracts.Participants;

namespace Daoris.Knowledge.Http.Routes;

/// <summary>
/// The helper and partner records a local host keeps for the driver's sessions, and the return of their answers to the session each
/// serves: the driver opens the record (a chat that serves another session, in a clone of its own) and hands the answer over as a word.
/// Answered only to a caller on this machine.
/// </summary>
public sealed class ParticipantRoutes(HostWiring host) : IRouteModule
{
    private ComposedService Service => host.Service;

    public void Map(RouteMap routes)
    {
        if (host.Mode != ServiceMode.Local) return;

        routes.Map<OpenHelper, HelperActionResponse>(OpenHelperAsync);
        routes.Map<HandHelper, HelperActionResponse>(HandHelperAsync);
        routes.Map<OpenPartner, HelperActionResponse>(OpenPartnerAsync);
        routes.Map<HandPartner, HelperActionResponse>(HandPartnerAsync);
        routes.Map<OpenSupervise, HelperActionResponse>(OpenSuperviseAsync);
        routes.Map<OpenStepSession, HelperActionResponse>(OpenStepSessionAsync);
    }

    private async Task<Answer<HelperActionResponse>> OpenStepSessionAsync(OpenStepSession request, HttpContext http, CancellationToken ct) =>
        host.MachineLocal(http)
            ? await ReplyAsync(
                await Service.StepSessions.OpenAsync(
                    new StepSessionOpen(request.Performs ?? "", request.Repository ?? "", request.Workspace, request.Door ?? "")
                    {
                        Account = request.Account, DoorVersion = request.DoorVersion,
                    },
                    DateTimeOffset.UtcNow, ct),
                http)
            : OffMachine;

    private async Task<Answer<HelperActionResponse>> OpenSuperviseAsync(OpenSupervise request, HttpContext http, CancellationToken ct) =>
        host.MachineLocal(http)
            ? await ReplyAsync(
                await Service.Supervising.OpenAsync(
                    new SuperviseOpen(
                        request.Decides ?? "", request.Role ?? "", request.Repository ?? "", request.Workspace, request.Serves,
                        request.Door ?? "", request.Tree ?? "")
                    {
                        Account = request.Account, DoorVersion = request.DoorVersion, Machine = request.Machine == true,
                    },
                    DateTimeOffset.UtcNow, ct),
                http)
            : OffMachine;

    private async Task<Answer<HelperActionResponse>> OpenHelperAsync(OpenHelper request, HttpContext http, CancellationToken ct) =>
        host.MachineLocal(http)
            ? await ReplyAsync(
                await Service.Helpers.OpenAsync(
                    new HelperOpen(request.Asker ?? "", request.Door ?? "", request.Tree ?? "")
                    {
                        Account = request.Account, DoorVersion = request.DoorVersion, BaseCommit = request.BaseCommit,
                    },
                    DateTimeOffset.UtcNow, ct),
                http)
            : OffMachine;

    private async Task<Answer<HelperActionResponse>> HandHelperAsync(HandHelper request, HttpContext http, CancellationToken ct) =>
        host.MachineLocal(http)
            ? await ReplyAsync(await Service.Helpers.HandAsync(
                request.Helper, request.Text ?? "", DateTimeOffset.UtcNow, ct, running: request.Running == true,
                level: string.IsNullOrWhiteSpace(request.Level) ? null : request.Level.Trim(), settled: request.Settled == true), http)
            : OffMachine;

    private async Task<Answer<HelperActionResponse>> OpenPartnerAsync(OpenPartner request, HttpContext http, CancellationToken ct) =>
        host.MachineLocal(http)
            ? await ReplyAsync(
                await Service.Partners.OpenAsync(
                    new PartnerOpen(request.Lead ?? "", request.Piece ?? "", request.Door ?? "", request.Tree ?? "")
                    {
                        Account = request.Account, DoorVersion = request.DoorVersion, BaseCommit = request.BaseCommit,
                    },
                    DateTimeOffset.UtcNow, ct),
                http)
            : OffMachine;

    private async Task<Answer<HelperActionResponse>> HandPartnerAsync(HandPartner request, HttpContext http, CancellationToken ct) =>
        host.MachineLocal(http)
            ? await ReplyAsync(await Service.Partners.HandAsync(request.Partner, request.Text ?? "", DateTimeOffset.UtcNow, ct, settled: request.Settled == true), http)
            : OffMachine;

    // A second opinion's door says the same to a caller off this machine.
    private static Answer<HelperActionResponse> OffMachine =>
        Refusal.NotFound("A second opinion is kept on this machine, and answered only to a caller on this machine.");

    // A shape is a bad request, nothing by that name is not found, and a state it is in is the lock's own shape, 409.
    private async Task<Answer<HelperActionResponse>> ReplyAsync(HelperOutcome outcome, HttpContext http) =>
        outcome.Refusal switch
        {
            HelperRefusal.None => new HelperActionResponse(
                outcome.Message,
                outcome.Session is { } session ? await SessionAnswers.ToSessionAsync(Service, session, host.MachineLocal(http)) : null,
                outcome.Word is { } word ? SessionAnswers.ToSaid(word) : null,
                outcome.SettledOn,
                outcome.Waiting ? true : null),
            HelperRefusal.NotFound or HelperRefusal.Shared => Refusal.NotFound(outcome.Message),
            HelperRefusal.BadShape => Refusal.BadRequest(outcome.Message),
            _ => Refusal.Conflict(outcome.Message),
        };
}
