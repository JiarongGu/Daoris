using Daoris.Contracts.Sessions;

namespace Daoris.Knowledge.Http.Routes;

/// <summary>
/// The doors a local host serves the driver that runs a session's process: the records it opens for the conversations it starts
/// itself (an ask's intake, Ask Daoris's), and what it learned about a start that the service cannot look at. The process is the
/// driver's, as ever; only a record of this machine's is kept here.
/// </summary>
public sealed class SessionKeepRoutes(HostWiring host) : IRouteModule
{
    private ComposedService Service => host.Service;

    public void Map(RouteMap routes)
    {
        if (host.Mode != ServiceMode.Local) return;

        routes.Map<OpenIntake, SessionActionResponse>(OpenIntakeAsync);
        routes.Map<OpenHelp, SessionActionResponse>(OpenHelpAsync);
        routes.Map<KeepHelpMode, SessionActionResponse>(KeepHelpModeAsync);
        routes.Map<KeepAccess, SessionActionResponse>(KeepAccessAsync);
        routes.Map<KeepActor, SessionActionResponse>(KeepActorAsync);
        routes.Map<KeepResolved, SessionActionResponse>(KeepResolvedAsync);
    }

    private async Task<Answer<SessionActionResponse>> OpenIntakeAsync(OpenIntake request, HttpContext http, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Ask) || string.IsNullOrWhiteSpace(request.Room))
        {
            return Refusal.BadRequest("ask and room are required — which ask, and where its intake runs");
        }

        var outcome = await Service.Ledger.OpenIntakeAsync(
            request.Ask,
            // The adapter is the harness, never a model. Silence takes the supported one.
            string.IsNullOrWhiteSpace(request.Adapter) ? "claude-code" : request.Adapter,
            request.Room, DateTimeOffset.UtcNow, request.HarnessVersion, request.Profile, ct, actor: request.Actor);

        return await OpenedAsync(
            outcome, http, SessionOpenRefusal.AskNotFound, SessionOpenRefusal.AskAnswered, SessionOpenRefusal.RepositoryBusy);
    }

    private async Task<Answer<SessionActionResponse>> OpenHelpAsync(OpenHelp request, HttpContext http, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Room)) return Refusal.BadRequest("room is required — where Ask Daoris's conversation runs");

        // The mode its start got, which the ledger knows the spelling of; one it does not know is the caller's mistake.
        SessionOpenOutcome outcome;
        try
        {
            outcome = await Service.Ledger.OpenHelpAsync(
                string.IsNullOrWhiteSpace(request.Adapter) ? "claude-code" : request.Adapter,
                request.Room, DateTimeOffset.UtcNow, request.HarnessVersion, request.Profile, request.Workspace,
                string.IsNullOrWhiteSpace(request.Mode) ? null : request.Mode, ct, actor: request.Actor);
        }
        catch (ArgumentException refused)
        {
            return Refusal.BadRequest(refused.Message);
        }

        return await OpenedAsync(outcome, http, SessionOpenRefusal.WorkspaceUnknown, SessionOpenRefusal.RepositoryBusy);
    }

    // An open's outcome: the record, or the refusal by the status its kind wears. Busy and answered are state conflicts.
    private async Task<Answer<SessionActionResponse>> OpenedAsync(
        SessionOpenOutcome outcome, HttpContext http, SessionOpenRefusal notFound, params SessionOpenRefusal[] conflicts)
    {
        if (outcome.Refusal == SessionOpenRefusal.None)
        {
            return new SessionActionResponse(
                await SessionAnswers.ToSessionAsync(Service, outcome.Session!, host.MachineLocal(http)), outcome.Message);
        }

        return outcome.Refusal == notFound ? Refusal.NotFound(outcome.Message)
            : conflicts.Contains(outcome.Refusal) ? Refusal.Conflict(outcome.Message)
            : Refusal.BadRequest(outcome.Message);
    }

    private async Task<Answer<SessionActionResponse>> KeepHelpModeAsync(KeepHelpMode request, HttpContext http, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Mode)) return Refusal.BadRequest("mode is required — the access mode Ask Daoris goes on under");

        Session? kept;
        try
        {
            kept = await Service.Ledger.KeepHelpModeAsync(request.Id, request.Mode, ct);
        }
        catch (ArgumentException refused)
        {
            return Refusal.BadRequest(refused.Message);
        }

        return kept is null
            ? Refusal.NotFound($"no Ask Daoris conversation of this machine's is `{request.Id}`")
            : new SessionActionResponse(
                await SessionAnswers.ToSessionAsync(Service, kept, host.MachineLocal(http)), $"Ask Daoris `{request.Id}` goes on {kept.HelpMode}.");
    }

    private async Task<Answer<SessionActionResponse>> KeepAccessAsync(KeepAccess request, HttpContext http, CancellationToken ct)
    {
        if (request.Access is not { Count: > 0 })
        {
            return Refusal.BadRequest("access is required — the setup the session goes on under, each pause with its value");
        }

        Session? kept;
        try
        {
            kept = await Service.Ledger.KeepAccessAsync(request.Id, request.Access, ct);
        }
        catch (ArgumentException refused)
        {
            return Refusal.BadRequest(refused.Message);
        }

        return kept is null
            ? Refusal.NotFound($"no driven session of this machine's is `{request.Id}`")
            : new SessionActionResponse(
                await SessionAnswers.ToSessionAsync(Service, kept, host.MachineLocal(http)), $"Session `{request.Id}` goes on under the setup it was told.");
    }

    private async Task<Answer<SessionActionResponse>> KeepActorAsync(KeepActor request, HttpContext http, CancellationToken ct)
    {
        if (!host.MachineLocal(http)) return Answer<SessionActionResponse>.Else(StatusCodes.Status404NotFound, null);

        try
        {
            var kept = await Service.Ledger.KeepActorAsync(request.Id, request.Choice, request.Asked, request.Override, request.Ran, request.Chosen, ct);
            return kept is null
                ? Refusal.NotFound("no session of this machine's")
                : new SessionActionResponse(await SessionAnswers.ToSessionAsync(Service, kept, true), "Actor provenance kept.");
        }
        catch (ArgumentException refused)
        {
            return Refusal.BadRequest(refused.Message);
        }
    }

    private async Task<Answer<SessionActionResponse>> KeepResolvedAsync(KeepResolved request, HttpContext http, CancellationToken ct)
    {
        if (request.Resolved is not { Count: > 0 })
        {
            return Refusal.BadRequest("resolved is required — the downloaded versions the start was handed, agent|tool/<name>/<version>");
        }

        Session? kept;
        try
        {
            kept = await Service.Ledger.KeepResolvedAsync(request.Id, request.Resolved, ct);
        }
        catch (ArgumentException refused)
        {
            return Refusal.BadRequest(refused.Message);
        }

        return kept is null
            ? Refusal.NotFound($"no session of this machine's is `{request.Id}`")
            : new SessionActionResponse(
                await SessionAnswers.ToSessionAsync(Service, kept, host.MachineLocal(http)), $"Session `{request.Id}` keeps the versions its start was handed.");
    }
}
