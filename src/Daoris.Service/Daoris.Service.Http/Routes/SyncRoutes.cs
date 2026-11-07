using Daoris.Contracts.Sync;

namespace Daoris.Knowledge.Http.Routes;

/// <summary>
/// A local host's sync doors: one pass for one circle, where it stands, a machine's retirement at the remote, and where this
/// machine's claim on a quest stands, which is how its driver knows a session to stop. A shared host is what they sync with and has none.
/// </summary>
public sealed class SyncRoutes(HostWiring host) : IRouteModule
{
    private ComposedService Service => host.Service;

    public void Map(RouteMap routes)
    {
        if (host.Mode != ServiceMode.Local) return;

        routes.Map<SyncCircle, SyncResponse>(PassAsync);
        routes.Map<ReadSyncStanding, SyncStandingResponse>(StandingAsync);
        routes.Map<RetireSyncMachine, MachineRetiredResponse>(RetireAsync);
        routes.Map<ReadClaim, QuestClaimResponse>(ClaimAsync);
    }

    // The quest half is the same pass a take on a shared quest runs as it claims.
    private async Task<Answer<SyncResponse>> PassAsync(SyncCircle request, HttpContext http, CancellationToken ct)
    {
        var circle = Workspaces.Normalize(request.Workspace);
        if (Service.Remotes?.For(circle) is not { } remote)
        {
            return new SyncResponse(circle, Service.Quests.Machine, Wired: false, 0, [], [], [], 0, 0, null, 0);
        }

        var quests = await QuestSync.RunAsync(Service.Quests, Service.Service, remote, circle, ct);
        var sessions = await SessionSync.RunAsync(Service.Sessions, Service.Quests, Service.Service, remote, circle, ct);
        // An ask's channel travels on the same pass; a remote without it is told once and the channel stays here.
        var channel = await ChannelSync.RunAsync(Service.Messages, Service.Sessions, Service.Service, remote, circle, ct);
        // The team's code maps come down on the same pass: pulling one needs no git, so it is the host's to do, and the host is
        // what answers the page for a repository with no checkout here.
        var maps = await CodeMapSync.RunAsync(Service.Service, remote, circle, ct);
        return new SyncResponse(
            circle, Service.Quests.Machine, Wired: true, quests.Pushed,
            quests.Conflicts.Select(c => new QuestConflictNote(c.Quest, (c.Attempted ?? QuestStatus.Open).ToString())).ToList(),
            quests.Refused.Select(r => new QuestPushRefusalWire(r.Quest, r.Reason)).ToList(),
            quests.Behind,
            sessions.Pushed,
            sessions.Fetched,
            quests.Problem ?? sessions.Problem ?? maps.Problem ?? channel.Problem,
            maps.Fetched);
    }

    // Read from the store, so it answers without reaching the remote: what the status bar polls and `daoris remote sync status` prints.
    private async Task<Answer<SyncStandingResponse>> StandingAsync(ReadSyncStanding request, HttpContext http, CancellationToken ct)
    {
        var circle = Workspaces.Normalize(request.Workspace);
        if (Service.Remotes?.For(circle) is null) return new SyncStandingResponse(circle, Wired: false, 0, [], [], null, null, null);

        var standing = await QuestSync.StandingAsync(Service.Quests, Service.Service, circle, ct);
        return new SyncStandingResponse(
            circle, Wired: true, standing.Ahead, standing.Behind, standing.Conflicts,
            standing.Synced, standing.Tried, standing.Problem,
            // The machines the remote last said had not declared, as the last pass heard them; none where it heard none.
            Service.Quests.ClosureOf(circle)?.Undeclared);
    }

    // Asked at the remote, and what it answers kept as a pass keeps it. A wall or a remote without the door is said in the remote's
    // words, and nothing here changes.
    private async Task<Answer<MachineRetiredResponse>> RetireAsync(RetireSyncMachine request, HttpContext http, CancellationToken ct)
    {
        var circle = Workspaces.Normalize(request.Workspace);
        if (Service.Remotes?.For(circle) is not { } remote)
        {
            return Refusal.NotFound($"`{circle}` has no remote on this machine, so no machine of it is counted anywhere.");
        }

        try
        {
            var answered = await QuestSync.RetireMachineAsync(Service.Quests, remote, circle, request.Machine, ct);
            return new MachineRetiredResponse(
                circle, request.Machine, answered.Retired, answered.Closure.Undeclared,
                answered.Retired
                    ? $"`{circle}`'s remote no longer counts machine `{request.Machine}`; a push from it counts it again."
                    : $"`{circle}`'s remote has heard nothing from machine `{request.Machine}`, so it counts nothing to retire.");
        }
        catch (RemoteException wall)
        {
            return Answer<MachineRetiredResponse>.Else(
                StatusCodes.Status502BadGateway,
                new ErrorResponse(wall.Status == 404
                    ? $"`{circle}`'s remote is older than retiring a machine, so nothing was retired ({wall.Message})."
                    : $"`{circle}`'s remote did not retire machine `{request.Machine}`: {wall.Message}."));
        }
    }

    private async Task<Answer<QuestClaimResponse>> ClaimAsync(ReadClaim request, HttpContext http, CancellationToken ct)
    {
        var id = request.Id.TrimStart('#');
        var takes = await Service.Quests.ReadTakesAsync(id, ct);
        return new QuestClaimResponse(
            id, (await Service.Quests.ClaimAsync(id, ct)).ToString().ToLowerInvariant(),
            // A take lost to the person's reopen, which the driver's stop says as that.
            await Service.Quests.LostToTheReopenAsync(id, ct) ? "source-reopened" : null,
            // The take it holds the quest by, where the person's bring-forward made it, which the driver carries a
            // stood-down session on over.
            takes.BroughtForwardBy(Service.Quests.Machine) is { } forward ? QuestAnswers.ToForward(forward) : null,
            // When the take it stands taken by is made, which a submission, a reopen or a go-on never moves as they move `updated`.
            takes.Take?.At);
    }
}
