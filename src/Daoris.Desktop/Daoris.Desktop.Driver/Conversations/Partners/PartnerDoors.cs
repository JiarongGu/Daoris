using System.Text.Json;
using static Daoris.Driver.Json;

namespace Daoris.Driver;

/// <summary>
/// The local host's partner door, as the driver asks it (COAGENT1b, D199 point 2; <c>POST /api/partners</c>): a class over the
/// service's client, reached as its <see cref="ServiceClient.Partners"/>, beside <see cref="HelperDoors"/>.
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
}
