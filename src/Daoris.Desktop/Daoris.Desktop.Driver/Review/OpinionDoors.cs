using System.Text.Json;
using static Daoris.Driver.ServiceClient;

namespace Daoris.Driver;

/// <summary>
/// The local host's opinion doors, as the driver asks them (XAGENT1c–XAGENT1e): a pass's ask, an opinion's read and its findings'
/// hand: a class over the service's client, reached as its <see cref="ServiceClient.Opinions"/> (PART8).
/// </summary>
public sealed class OpinionDoors
{
    private readonly ServiceClient _service;

    internal OpinionDoors(ServiceClient service) => _service = service;

    /// <summary>
    /// Ask the local host for a pass (XAGENT1c, D155 point 11): it keeps the opinion and opens its reviewer's record in one
    /// transaction, a chat on no quest naming the opinion, in its own copy, as its account. A refusal is an answer, not an
    /// exception, and so is a host older than the door.
    /// </summary>
    public async Task<OpinionAsked> AskAsync(OpinionAskBody body, CancellationToken ct = default)
    {
        const string Door = "/api/opinions";
        var (ok, status, payload, root) = await _service.PostJsonAsync(Door, body.Json(), ct).ConfigureAwait(false);
        if (root is not { } answer) return new(null, null, $"the service at {_service.BaseUrl} has no `{Door}` door ({status}) — is it older than this driver?");
        if (!ok) return new(null, null, Text(answer, "error") ?? payload);

        var opinion = answer.ValueKind == JsonValueKind.Object && answer.TryGetProperty("opinion", out var kept) ? Text(kept, "id") : null;
        var session = answer.ValueKind == JsonValueKind.Object && answer.TryGetProperty("session", out var record) ? record : default;
        var id = Text(session, "id");
        if (opinion is null || id is null) return new(null, null, $"the service at {_service.BaseUrl} answered `{Door}` with no opinion and no record.");

        // The door the record opened by, for the session log (LOG1b): a reviewer's record is a chat that serves no quest.
        _service.RecordOpened(new SessionOpened(id, OpinionPass.OpenedKind, Text(session, "adapter") ?? body.Adapter, Text(session, "repository") ?? body.Candidate.Repository)
        {
            Workspace = Text(session, "workspace"),
        });
        return new(opinion, id, Text(answer, "message") ?? "");
    }

    /// <summary>
    /// The opinion under <paramref name="id"/> as the local host answers it (XAGENT1c), with where its pass stands; null where
    /// there is none, or the host is older than the door.
    /// </summary>
    /// <exception cref="DriverException">The host refused for another reason.</exception>
    public async Task<OpinionView?> ReadAsync(string id, CancellationToken ct = default)
    {
        var path = $"/api/opinions/{Uri.EscapeDataString(id)}";
        var answer = await _service.SendAsync(ServiceVerb.Get, path, null, ct).ConfigureAwait(false);
        if (answer.Status == (int)System.Net.HttpStatusCode.NotFound) return null;
        if (!answer.Ok) throw new DriverException($"{_service.BaseUrl}{path} answered {answer.Status}: {DriverHttp.ErrorOf(answer.Payload)}");
        return OpinionViews.Read(answer.Payload);
    }

    /// <summary>
    /// Hand a first pass's findings to its working session (XAGENT1c's <c>POST /api/opinions/{id}/hand</c>; design §6.3): the
    /// host keeps them on its record as a word whose <c>by</c> names the opinion, in its fixed words, once. A refusal is an
    /// answer, the host's sentence, not an exception, and so is a host older than the door.
    /// </summary>
    public async Task<(bool Handed, string Message, OpinionView? Opinion)> HandAsync(string id, CancellationToken ct = default)
    {
        var door = $"/api/opinions/{Uri.EscapeDataString(id)}/hand";
        var (ok, status, payload, root) = await _service.PostJsonAsync(door, "{}", ct).ConfigureAwait(false);
        if (root is not { } answer) return (false, $"the service at {_service.BaseUrl} has no `{door}` door ({status}) — is it older than this driver?", null);
        if (!ok) return (false, Text(answer, "error") ?? payload, null);
        var opinion = answer.ValueKind == JsonValueKind.Object && answer.TryGetProperty("opinion", out var kept) ? OpinionViews.Read(kept) : null;
        return (true, Text(answer, "message") ?? "", opinion);
    }
}
