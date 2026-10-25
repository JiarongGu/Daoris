using System.Text.Json;
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
        const string Door = "/api/partners";
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteString("lead", lead);
            writer.WriteString("piece", piece);
            writer.WriteString("adapter", adapter);
            writer.WriteString("tree", tree);
            if (account is not null) writer.WriteString("account", account);
            if (harnessVersion is not null) writer.WriteString("harnessVersion", harnessVersion);
            if (baseCommit is not null) writer.WriteString("baseCommit", baseCommit);
            writer.WriteEndObject();
        }

        var (ok, status, payload, root) = await _service.PostJsonAsync(Door, System.Text.Encoding.UTF8.GetString(stream.ToArray()), ct).ConfigureAwait(false);
        if (root is not { } answer) return new(null, $"the service at {_service.BaseUrl} has no `{Door}` door ({status}) — is it older than this driver?");
        if (!ok) return new(null, Text(answer, "error") ?? payload);

        var session = answer.ValueKind == JsonValueKind.Object && answer.TryGetProperty("session", out var record) ? record : default;
        var id = Text(session, "id");
        if (id is null) return new(null, $"the service at {_service.BaseUrl} answered `{Door}` with no record.");

        _service.RecordOpened(new SessionOpened(id, OpenedKind, Text(session, "adapter") ?? adapter, Text(session, "repository") ?? "")
        {
            Workspace = Text(session, "workspace"),
        });
        return new(id, Text(answer, "message") ?? "");
    }

    /// <summary>
    /// Hand a piece's answer (<c>POST /api/partners/{partner}/hand</c>; the design §2.3 step 9): the host keeps <paramref name="text"/>
    /// under the piece's key on the newest record of the lead's line, which it names. A refusal is an answer, the host's sentence, and
    /// so is a host older than the door.
    /// </summary>
    public async Task<(bool Handed, string Message, string? Word, string? To)> HandAsync(string partner, string text, CancellationToken ct = default)
    {
        var door = $"/api/partners/{Uri.EscapeDataString(partner)}/hand";
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteString("text", text);
            writer.WriteEndObject();
        }

        var (ok, status, payload, root) = await _service.PostJsonAsync(door, System.Text.Encoding.UTF8.GetString(stream.ToArray()), ct).ConfigureAwait(false);
        if (root is not { } answer) return (false, $"the service at {_service.BaseUrl} has no `{door}` door ({status}) — is it older than this driver?", null, null);
        if (!ok) return (false, Text(answer, "error") ?? payload, null, null);

        var word = answer.ValueKind == JsonValueKind.Object && answer.TryGetProperty("said", out var said) ? Text(said, "id") : null;
        var to = answer.ValueKind == JsonValueKind.Object && answer.TryGetProperty("session", out var session) ? Text(session, "id") : null;
        return (true, Text(answer, "message") ?? "", word, to);
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
