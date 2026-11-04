using System.Text.Json;
using Daoris.Contracts;
using Daoris.Contracts.Quests;
using Daoris.Contracts.Sessions;
using static Daoris.Driver.RefusalWords;

namespace Daoris.Driver;

/// <summary>
/// The doors for what the service keeps beside a session (the service's session store routes): the record of the finished tree a session ran
/// in, the memory the session changed, the delete of its record, and the ageing of the finished trees' records. A class over the client,
/// reached as its <see cref="ServiceClient.Store"/> and by the client's own doors of the same names.
/// </summary>
public sealed class SessionStoreDoors
{
    private readonly ServiceClient _service;

    internal SessionStoreDoors(ServiceClient service) => _service = service;

    /// <summary>
    /// History's age applied to the finished trees' records no session names (KEEPSIZE1b, <c>POST /api/trees/record/age</c>): each kept at or
    /// before <paramref name="before"/> goes; how many. A service with no such door answers none.
    /// </summary>
    internal async Task<int> AgeWorkRecordsAsync(DateTimeOffset before, CancellationToken ct = default)
    {
        var sent = await _service.SendAsync(new AgeWorkRecords(before.UtcDateTime), ct).ConfigureAwait(false);
        if (sent.Reply.Ok) return sent.Answer?.Cleared ?? 0;
        if (sent.Reply.Status == 404) return 0;
        throw new DriverException($"the service refused ageing the finished trees' records: {ErrorWords(sent.Reply.Payload) ?? sent.Reply.Payload}");
    }

    /// <summary>
    /// The record of the finished tree a session ran in (<c>GET /api/sessions/{id}/record</c>), as the service answers it; null where none is
    /// kept or the service has no such door.
    /// </summary>
    public async Task<string?> RecordAsync(string session, CancellationToken ct = default)
    {
        var answer = (await _service.SendAsync(new ReadSessionRecord(session), ct).ConfigureAwait(false)).Reply;
        if (answer.Ok) return answer.Payload;
        if (answer.Status == 404) return null;
        throw new DriverException($"the service refused reading session `{session}`'s record: {answer.Payload}");
    }

    /// <summary>
    /// What the line ending at <paramref name="session"/> read of memory, each entry in one line as the service rechecked it now
    /// (<c>POST /api/sessions/{id}/memory/read</c>, KNOWCENTRAL2f); none where the service has no such door or answers none.
    /// </summary>
    public async Task<IReadOnlyList<string>> LineMemoryAsync(string session, CancellationToken ct = default)
    {
        var sent = await _service.SendAsync(new ReadLineMemory(session), ct).ConfigureAwait(false);
        if (sent.Reply.Status == 404) return [];
        if (!sent.Reply.Ok) throw new DriverException($"the service refused reading the memory session `{session}`'s line read: {sent.Reply.Payload}");
        return [.. (sent.Answer?.Shown ?? []).Select(item => item.Line).OfType<string>().Where(line => line.Trim().Length > 0)];
    }

    /// <summary>
    /// A session's memory rows as the service answers them for its page (KNOWCENTRAL2e; the memory coordinator design §1, §4): its writes, ends
    /// and proposals, an end's reason without the row's token. Null from a host with no door.
    /// </summary>
    public async Task<string?> MemoryAsync(string session, CancellationToken ct = default)
    {
        var answer = (await _service.SendAsync(new ReadSessionMemory(session), ct).ConfigureAwait(false)).Reply;
        if (answer.Ok) return answer.Payload;
        if (answer.Status == 404) return null;
        throw new DriverException($"the service refused reading session `{session}`'s memory: {answer.Payload}");
    }

    /// <summary>
    /// Every session with a memory change waiting on the person (KNOWCENTRAL2e), as the service reads them: ends and replacements awaiting, a
    /// stopped session's staged rows. None from a host with no door.
    /// </summary>
    public async Task<IReadOnlyList<JsonElement>> MemoryWaitsAsync(CancellationToken ct = default)
    {
        var answer = (await _service.SendAsync(new ListMemoryWaits(), ct).ConfigureAwait(false)).Reply;
        if (answer.Status == 404) return [];
        if (!answer.Ok) throw new DriverException($"the service refused reading the memory waits: {answer.Payload}");
        using var document = JsonDocument.Parse(answer.Payload);
        return document.RootElement.TryGetProperty("waits", out var waits) && waits.ValueKind == JsonValueKind.Array
            ? [.. waits.EnumerateArray().Select(each => each.Clone())]
            : [];
    }

    /// <summary>
    /// The person's decision on a session's memory (KNOWCENTRAL2e): an awaiting end approved or declined by its row where
    /// <paramref name="op"/> names one, else the ended session's staged changes kept or dropped. A refusal is an answer, in the service's words.
    /// </summary>
    public async Task<(bool Ok, string Message)> DecideMemoryAsync(string session, string? op, bool yes, CancellationToken ct = default)
    {
        var sent = op is { Length: > 0 }
            ? await _service.SendAsync(new DecideMemory(op, yes), ct).ConfigureAwait(false)
            : await _service.SendAsync(new KeepSessionMemory(session, yes), ct).ConfigureAwait(false);
        var said = sent.Reply.Ok ? sent.Answer?.Message : ErrorWords(sent.Reply.Payload);
        return (sent.Reply.Ok, said ?? sent.Reply.Payload);
    }

    /// <summary>
    /// The ledger's judgement of deleting a session's record (SESSUX1f, D126 §5.4), deleting nothing: whether it would, and if not, its
    /// sentence, its word and the facts the word names.
    /// </summary>
    /// <exception cref="DriverException">A host older than the door, which answers no judgement.</exception>
    public Task<SessionDeleteAnswer> JudgeDeleteAsync(string id, CancellationToken ct = default) =>
        DeleteAnsweredAsync(new JudgeSessionDelete(id), ct);

    /// <summary>
    /// Delete a session's record (SESSUX1f): the ledger judges it again as it deletes, so a record that moved since is refused as it now
    /// stands. A refusal is an answer, not an exception.
    /// </summary>
    /// <exception cref="DriverException">A host older than the door, which answers no judgement.</exception>
    public Task<SessionDeleteAnswer> DeleteAsync(string id, CancellationToken ct = default) =>
        DeleteAnsweredAsync(new DeleteSession(id), ct);

    private async Task<SessionDeleteAnswer> DeleteAnsweredAsync<TAnswer>(IRoute<TAnswer> route, CancellationToken ct) where TAnswer : class
    {
        var sent = await _service.SendAsync(route, ct).ConfigureAwait(false);
        SessionDeletionResponse? said;
        try
        {
            said = JsonSerializer.Deserialize(sent.Reply.Payload, ContractJson.Options.GetTypeInfo(typeof(SessionDeletionResponse))) as SessionDeletionResponse;
        }
        catch (JsonException)
        {
            said = null;
        }

        // A refusal carries its word; a yes is a success that carries none. Anything else is a host older than the door.
        var refusal = said?.Refusal;
        var taken = sent.Reply.Ok && said is not null && refusal is null;
        if (!taken && refusal is null)
        {
            throw new DriverException(
                $"the service at {_service.BaseUrl} has no session delete door ({sent.Reply.Status}) — is it older than this driver?");
        }

        return new SessionDeleteAnswer(taken, (taken ? (sent.Answer as DeletedResponse)?.Message : said?.Error) ?? "")
        {
            Refusal = refusal,
            Quest = said?.Quest,
            Ask = said?.Ask,
            Origin = said?.Origin,
            Workspace = said?.Workspace,
        };
    }
}
