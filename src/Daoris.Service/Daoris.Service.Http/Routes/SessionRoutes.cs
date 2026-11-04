using Daoris.Contracts.Sessions;

namespace Daoris.Knowledge.Http.Routes;

/// <summary>
/// The driver's session records, served by every host: the list, the two opens (a driven record for a quest, a chat) and the move of
/// the ledger's state machine. State only; the service never spawns a process, and the judgement is the shared ledger's, so this door
/// and any other cannot drift. The records answer the machine-local parts of themselves to a caller on this machine only.
/// </summary>
public sealed class SessionRoutes(HostWiring host) : IRouteModule
{
    private ComposedService Service => host.Service;

    public void Map(RouteMap routes)
    {
        routes.Map<ListSessions, SessionResponse[]>(ListAsync);
        routes.Map<OpenSession, SessionOpenResponse>(OpenAsync);
        routes.Map<OpenChat, SessionOpenResponse>(OpenChatAsync);
        routes.Map<AdvanceSession, SessionActionResponse>(AdvanceAsync);
    }

    private async Task<Answer<SessionResponse[]>> ListAsync(ListSessions request, HttpContext http, CancellationToken ct)
    {
        var sessions = await Service.Sessions.ListAsync(request.Repository, request.IncludeClosed ?? false, request.Workspace, ct);
        // Which records the ledger would delete, none at a shared deployment, which has no delete door.
        var deletable = host.Mode == ServiceMode.Local ? await Service.Ledger.DeletableAsync(sessions, ct) : new HashSet<string>();
        var answered = await Service.Ledger.WithEntriesAsync(sessions, ct);
        var local = host.MachineLocal(http);
        var views = new List<SessionResponse>();
        foreach (var session in answered)
        {
            var head = local && session.Origin is null ? (await Lines.HeadOfLineAsync(Service.Sessions, Service.Quests, session, ct))?.Id : null;
            views.Add(SessionAnswers.ToSession(session, local) with { Deletable = deletable.Contains(session.Id), Head = head });
        }

        return views.ToArray();
    }

    // The harness version and the profile are observed by the DRIVER before it spawns; the service has no binaries to look at, so it
    // records what it is told and judges none of it. An access setup that is no short map of words is the caller's mistake.
    private async Task<Answer<SessionOpenResponse>> OpenAsync(OpenSession request, HttpContext http, CancellationToken ct)
    {
        if (request.Opens is null && (string.IsNullOrWhiteSpace(request.Quest) || string.IsNullOrWhiteSpace(request.Adapter)))
        {
            return Refusal.BadRequest("quest and adapter are required");
        }

        SessionOpenOutcome outcome;
        try
        {
            outcome = await Service.Ledger.OpenAsync(
                request.Quest!, request.Adapter!, DateTimeOffset.UtcNow, request.HarnessVersion, request.Profile, request.Tree,
                request.BaseCommit, ct, request.From, request.FromWhy, request.FromBy, request.Access, actor: request.Actor,
                opens: request.Opens, moves: SessionAnswers.MovesOf(request.Moves));
        }
        catch (ArgumentException refused)
        {
            return Malformed(request.Opens, refused.Message);
        }

        if (request.Opens is not null) return await KeyedAsync(outcome, http);
        return await OpenedAsync(outcome, http, SessionOpenRefusal.QuestNotFound,
            SessionOpenRefusal.QuestNotOpen, SessionOpenRefusal.TakenElsewhere, SessionOpenRefusal.RepositoryBusy);
    }

    // A chat is a session a person entered rather than the driver planned: same record, same lock, no quest required. The process is the
    // driver's, as ever: this opens the RECORD.
    private async Task<Answer<SessionOpenResponse>> OpenChatAsync(OpenChat request, HttpContext http, CancellationToken ct)
    {
        if (request.Opens is null && string.IsNullOrWhiteSpace(request.Repository)) return Refusal.BadRequest("repository is required");

        SessionOpenOutcome outcome;
        try
        {
            outcome = await Service.Ledger.OpenChatAsync(
                request.Repository!,
                // The adapter is the harness, never a model. Silence takes the supported one.
                string.IsNullOrWhiteSpace(request.Adapter) ? "claude-code" : request.Adapter,
                DateTimeOffset.UtcNow, request.HarnessVersion, request.Profile, request.Tree, request.BaseCommit, ct,
                request.From, request.FromWhy, request.FromBy, actor: request.Actor, opens: request.Opens,
                moves: SessionAnswers.MovesOf(request.Moves));
        }
        catch (ArgumentException refused)
        {
            return Malformed(request.Opens, refused.Message);
        }

        if (request.Opens is not null) return await KeyedAsync(outcome, http);
        return await OpenedAsync(outcome, http, SessionOpenRefusal.RepositoryUnknown, SessionOpenRefusal.RepositoryBusy);
    }

    // State conflicts wear 409, the shape the quest door teaches: a driver racing another machine for a repository slot got beaten.
    private async Task<Answer<SessionOpenResponse>> OpenedAsync(
        SessionOpenOutcome outcome, HttpContext http, SessionOpenRefusal notFound, params SessionOpenRefusal[] conflicts)
    {
        if (outcome.Refusal == SessionOpenRefusal.None)
        {
            return new SessionOpenResponse(await SessionAnswers.ToSessionAsync(Service, outcome.Session!, host.MachineLocal(http)), outcome.Message);
        }

        return outcome.Refusal == notFound ? Refusal.NotFound(outcome.Message)
            : conflicts.Contains(outcome.Refusal) ? Refusal.Conflict(outcome.Message)
            : Refusal.BadRequest(outcome.Message);
    }

    private static Answer<SessionOpenResponse> Malformed(string? key, string message) => key is null
        ? Refusal.BadRequest(message)
        : Answer<SessionOpenResponse>.Else(
            StatusCodes.Status400BadRequest, new SessionOpenResponse(null, message, key, [], Error: message, Code: "malformed"));

    // A keyed open answers its key's echo and its transfer at every status: a refusal is the same record with `error` and `code`.
    private async Task<Answer<SessionOpenResponse>> KeyedAsync(SessionOpenOutcome outcome, HttpContext http)
    {
        var ok = outcome.Refusal == SessionOpenRefusal.None;
        var answer = new SessionOpenResponse(
            outcome.Session is { } record ? await SessionAnswers.ToSessionAsync(Service, record, host.MachineLocal(http)) : null,
            outcome.Message, outcome.Opens!,
            [.. outcome.Transfer.Select(each => new OpenTransferResponse(each.Word, each.Key, each.Transfer, each.Order, each.Id))],
            outcome.Head, ok ? null : outcome.Message, ok ? null : outcome.Refusal.ToString().ToLowerInvariant());
        return ok
            ? answer
            : Answer<SessionOpenResponse>.Else(outcome.Refusal == SessionOpenRefusal.Gone ? StatusCodes.Status404NotFound : StatusCodes.Status409Conflict, answer);
    }

    private async Task<Answer<SessionActionResponse>> AdvanceAsync(AdvanceSession request, HttpContext http, CancellationToken ct)
    {
        var outcome = await Service.Ledger.AdvanceAsync(
            request.Id, request.State ?? "", request.Note, request.Evidence, request.Transcript, DateTimeOffset.UtcNow, ct,
            interrupted: request.Interrupted == true, limit: request.Limit == true,
            // The note's lines by code, kept only as what a part is; none with no note.
            noteParts: request.Note is null ? null : NoteParts.Normalize(request.NoteParts), expected: request.Expected,
            unbindAsk: request.UnbindAsk == true, expect: request.Expect?.OfType<string>().ToList(),
            turns: SessionAnswers.TurnsOf(request.Turns));

        return outcome.Refusal switch
        {
            SessionAdvanceRefusal.None => new SessionActionResponse(
                await SessionAnswers.ToSessionAsync(Service, outcome.Session!, host.MachineLocal(http)), outcome.Message),
            SessionAdvanceRefusal.NotFound => Refusal.NotFound(outcome.Message),
            // Terminal and an illegal move are conflicts with the record's current state, and so is a tree another session holds
            // when an ended record would go on. Only an unknown state name is a bad request.
            SessionAdvanceRefusal.Terminal or SessionAdvanceRefusal.InvalidMove or SessionAdvanceRefusal.Busy
                or SessionAdvanceRefusal.WordsChanged => Refusal.Conflict(outcome.Message),
            _ => Refusal.BadRequest(outcome.Message),
        };
    }
}
