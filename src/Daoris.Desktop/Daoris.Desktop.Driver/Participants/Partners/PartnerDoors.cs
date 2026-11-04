using System.Text.Json;
using Daoris.Contracts.Participants;
using static Daoris.Driver.Json;

namespace Daoris.Driver;

/// <summary>
/// The local host's partner door, as the driver asks it (COAGENT1b, D199 point 2; <c>POST /api/partners</c>): a class over the
/// service's client, reached as its <c>ServiceClient.Partners</c> (<see cref="PartnerDoorsOfClient"/>), beside <see cref="HelperDoors"/>.
/// </summary>
public sealed class PartnerDoors
{
    /// <summary>The kind the session log names a partner's record by (LOG1b).</summary>
    public const string OpenedKind = "partner";

    private readonly ServiceClient _service;

    internal PartnerDoors(ServiceClient service) => _service = service;

    /// <summary>
    /// Open a partner's record for <paramref name="piece"/> (the co-agent design §2.3 step 5): a chat on no quest that serves
    /// <paramref name="lead"/> and carries the piece, in <paramref name="tree"/>, the clone the driver made, as <paramref name="adapter"/> and
    /// its account. Idempotent by the piece: a record already open for it is answered. A refusal is an answer, the host's sentence, and so
    /// is a host older than the door.
    /// </summary>
    public async Task<HelperOpened> OpenAsync(
        string lead, string piece, string adapter, string tree, string? account, string? harnessVersion, string? baseCommit,
        CancellationToken ct = default)
    {
        var route = new OpenPartner(lead, piece, adapter, tree, account, harnessVersion, baseCommit);
        var door = RouteRequest.Of(route).Path;
        var (ok, status, payload, root) = await _service.SendJsonAsync(route, ct).ConfigureAwait(false);
        if (root is not { } answer) return new(null, $"the service at {_service.BaseUrl} has no `{door}` door ({status}) — is it older than this driver?");
        if (!ok) return new(null, Text(answer, "error") ?? payload);

        var session = answer.ValueKind == JsonValueKind.Object && answer.TryGetProperty("session", out var record) ? record : default;
        var id = Text(session, "id");
        if (id is null) return new(null, $"the service at {_service.BaseUrl} answered `{door}` with no record.");

        _service.RecordOpened(new SessionOpened(id, OpenedKind, Text(session, "adapter") ?? adapter, Text(session, "repository") ?? "")
        {
            Workspace = Text(session, "workspace"),
        });
        return new(id, Text(answer, "message") ?? "");
    }

    /// <summary>
    /// Hand a piece's answer (<c>POST /api/partners/{partner}/hand</c>; the design §2.3 step 9): the host keeps <paramref name="text"/>
    /// under the piece's key on the newest record of the lead's line, which it names. A refusal is an answer, the host's sentence, and
    /// so is a host older than the door; a hand with no answer, or one that does not read, is uncertain (<see cref="SolicitedHand.Read"/>).
    /// </summary>
    public async Task<SolicitedAnswer> HandAsync(string partner, string text, CancellationToken ct = default)
    {
        // The hand reads a settled answer, a key its line settled (FOUNDATION1 row p).
        var route = new HandPartner(partner, text, true);
        var door = RouteRequest.Of(route).Path;

        bool ok;
        int status;
        string payload;
        JsonElement? root;
        try
        {
            (ok, status, payload, root) = await _service.SendJsonAsync(route, ct).ConfigureAwait(false);
        }
        catch (Exception lost) when (lost is HttpRequestException || (lost is OperationCanceledException && !ct.IsCancellationRequested))
        {
            return SolicitedHand.Lost(door, _service.BaseUrl, lost.Message);
        }

        // Settled before a word (row p): the key's word left a record of the lead's line, so nothing was kept and there is no word.
        return SolicitedHand.Read(ok, status, root, payload, door, _service.BaseUrl);
    }
}

/// <summary>
/// The client's partner door, as the participants' own member of it (FOUNDATION1 mod-iv, the design §14.2: the door classes are their owners',
/// over the service's client, which names none of them): each read hands a door over the client, which holds no state of its own.
/// </summary>
public static class PartnerDoorsOfClient
{
    extension(ServiceClient client)
    {
        /// <summary>The local host's partner door (COAGENT1b), over this client.</summary>
        public PartnerDoors Partners => new(client);
    }
}
