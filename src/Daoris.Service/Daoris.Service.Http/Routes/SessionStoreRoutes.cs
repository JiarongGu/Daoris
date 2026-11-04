using Daoris.Contracts.Sessions;

namespace Daoris.Knowledge.Http.Routes;

/// <summary>
/// The doors a local host serves for what it keeps beside a session: a finished tree's record, the memory the session changed and the
/// person's decisions on it, and a session's delete. Machine-local, as the rows are: a shared host maps none, and a caller off this
/// machine is answered not found with no body.
/// </summary>
public sealed class SessionStoreRoutes(HostWiring host) : IRouteModule
{
    private ComposedService Service => host.Service;

    public void Map(RouteMap routes)
    {
        if (host.Mode != ServiceMode.Local) return;

        routes.Map<PostWorkRecord, WorkRecordResponse>(PostRecordAsync);
        routes.Map<AgeWorkRecords, WorkRecordAgeResponse>(async (request, http, ct) =>
            !host.MachineLocal(http) ? Answer<WorkRecordAgeResponse>.Else(StatusCodes.Status404NotFound, null)
            : request.Before is not { } before ? Refusal.BadRequest("before is required — the history age's cutoff")
            : new WorkRecordAgeResponse(await Service.Records.ForgetOrphansAsync(before, request.Workspace, ct)));
        routes.Map<ReadSessionRecord, WorkRecordShownResponse>(ReadRecordAsync);
        routes.Map<ReadSessionMemory, SessionMemoryResponse>(ReadMemoryAsync);
        routes.Map<ListMemoryWaits, MemoryWaitsResponse>(async (_, http, ct) =>
            !host.MachineLocal(http) ? Answer<MemoryWaitsResponse>.Else(StatusCodes.Status404NotFound, null)
            : new MemoryWaitsResponse([.. (await Service.Sessions.Memory.WaitsAsync(ct))
                .Select(wait => new MemoryWaitResponse(wait.Session, wait.Ends, wait.Replacements, wait.Staged, wait.Since))]));
        routes.Map<ReadLineMemory, MemoryReadResponse>(ReadLineMemoryAsync);
        routes.Map<DecideMemory, MemoryDecisionResponse>(async (request, http, ct) =>
            !host.MachineLocal(http) ? Answer<MemoryDecisionResponse>.Else(StatusCodes.Status404NotFound, null)
            : request.Approve is not { } approve ? Refusal.BadRequest("approve is required: true approves the end, false declines it")
            : Decided(await new MemoryDesk(Service.Sessions.Memory).DecideAsync(request.Id, session: null, approve, DateTimeOffset.UtcNow, ct)));
        routes.Map<KeepSessionMemory, MemoryDecisionResponse>(async (request, http, ct) =>
            !host.MachineLocal(http) ? Answer<MemoryDecisionResponse>.Else(StatusCodes.Status404NotFound, null)
            : request.Keep is not { } keep ? Refusal.BadRequest("keep is required: true keeps the staged changes, false drops them")
            : Decided(await new MemoryDesk(Service.Sessions.Memory).KeepAsync(request.Id, keep, DateTimeOffset.UtcNow, ct)));
        routes.Map<DeleteSession, DeletedResponse>(DeleteAsync);
        routes.Map<JudgeSessionDelete, SessionDeletionResponse>(async (request, _, ct) =>
        {
            var outcome = await Service.Ledger.JudgeDeleteAsync(request.Id, ct);
            return outcome.Refusal == SessionDeleteRefusal.NotFound
                ? Answer<SessionDeletionResponse>.Else(StatusCodes.Status404NotFound, SessionAnswers.ToDeletion(outcome))
                : SessionAnswers.ToDeletion(outcome);
        });
    }

    private static Answer<MemoryDecisionResponse> Decided(MemoryDecisionOutcome outcome) => outcome.Refusal switch
    {
        MemoryDecisionRefusal.None => new MemoryDecisionResponse(
            outcome.Message, outcome.Op is { } op ? SessionAnswers.ToMemoryOp(op) : null, outcome.Moved),
        MemoryDecisionRefusal.NotFound => Refusal.NotFound(outcome.Message),
        _ => Refusal.Conflict(outcome.Message),
    };

    // A store a newer build stamped is a 409, RECORDS_NEWER, so the driver keeps the folder awaiting its record and says why.
    private static Refusal RecordsNewer(NewerWorkRecordsException newer) => Refusal.Conflict(newer.Message, "RECORDS_NEWER");

    private async Task<Answer<WorkRecordResponse>> PostRecordAsync(PostWorkRecord request, HttpContext http, CancellationToken ct)
    {
        if (!host.MachineLocal(http)) return Answer<WorkRecordResponse>.Else(StatusCodes.Status404NotFound, null);
        if (SessionAnswers.PostOf(request) is not { } post)
        {
            return Refusal.BadRequest(
                "workspace, repository, folder, tree, moved, branch, hold, tip and left are required — the manifest's facts of a finished tree");
        }

        try
        {
            var (record, kept) = await Service.Records.PostAsync(post, DateTimeOffset.UtcNow, ct, SessionAnswers.TurnsOf(request.Turns));
            return new WorkRecordResponse(kept, record.At, [.. record.Sessions.Select(SessionAnswers.ToRecordSession)]);
        }
        catch (NewerWorkRecordsException newer)
        {
            return RecordsNewer(newer);
        }
    }

    private async Task<Answer<WorkRecordShownResponse>> ReadRecordAsync(ReadSessionRecord request, HttpContext http, CancellationToken ct)
    {
        if (!host.MachineLocal(http)) return Answer<WorkRecordShownResponse>.Else(StatusCodes.Status404NotFound, null);

        try
        {
            return await Service.Records.FindBySessionAsync(request.Id, ct) is { } record
                ? SessionAnswers.ToShown(record)
                : Refusal.NotFound($"no finished tree's record names session `{request.Id}`");
        }
        catch (NewerWorkRecordsException newer)
        {
            return RecordsNewer(newer);
        }
    }

    private async Task<Answer<SessionMemoryResponse>> ReadMemoryAsync(ReadSessionMemory request, HttpContext http, CancellationToken ct)
    {
        if (!host.MachineLocal(http)) return Answer<SessionMemoryResponse>.Else(StatusCodes.Status404NotFound, null);

        await new MemoryProposals(Service.Sessions.Memory, Service.Quests, engines: Service.Memory).SettleAsync(DateTimeOffset.UtcNow, ct);
        return new SessionMemoryResponse(
            [.. (await new MemoryDesk(Service.Sessions.Memory).PageAsync(request.Id, ct)).Select(SessionAnswers.ToMemoryOp)]);
    }

    // What the line ending at a session read, each entry read again and its cited records rechecked; another repository's entry is read
    // through its view, asking the session's workspace first.
    private async Task<Answer<MemoryReadResponse>> ReadLineMemoryAsync(ReadLineMemory request, HttpContext http, CancellationToken ct)
    {
        if (!host.MachineLocal(http)) return Answer<MemoryReadResponse>.Else(StatusCodes.Status404NotFound, null);

        var reads = await Service.Sessions.Memory.ReadsOfLineAsync(request.Id, ct);
        var record = await Service.Sessions.FindAsync(request.Id, ct);
        var shown = await new MemoryPresenter(Service.Sessions.Memory, Service.Memory, Service.Service)
        {
            Own = record?.Repository,
            Check = new MemoryWorkspaceCheck(Service.Service, record?.Workspace),
        }.PresentAsync(reads, ct);
        return new MemoryReadResponse([.. shown.Select(each => new MemoryShownResponse(
            each.Reference.ToString(), each.Read, each.Standing, each.Entry?.Revision, each.Checked, each.Why, each.Line))]);
    }

    // The ledger judges the record's half and removes it; a refusal is its sentence with its word and facts, so the driver reads the word.
    // Live, another machine's, a record something names: a conflict with the record as it stands.
    private async Task<Answer<DeletedResponse>> DeleteAsync(DeleteSession request, HttpContext http, CancellationToken ct)
    {
        var outcome = await Service.Ledger.DeleteAsync(request.Id, ct);
        return outcome.Refusal switch
        {
            SessionDeleteRefusal.None => new DeletedResponse(request.Id, outcome.Message),
            SessionDeleteRefusal.NotFound => Answer<DeletedResponse>.Else(StatusCodes.Status404NotFound, SessionAnswers.ToDeletion(outcome)),
            _ => Answer<DeletedResponse>.Else(StatusCodes.Status409Conflict, SessionAnswers.ToDeletion(outcome)),
        };
    }
}
