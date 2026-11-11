using System.Text.Json;
using Daoris.Contracts.Participants;
using static Daoris.Driver.Json;

namespace Daoris.Driver;

/// <summary>
/// The local host's door for a supervisor's turn's record (<c>POST /api/supervise</c>): a chat on no quest marked with the attempt it
/// decides for, reached as the client's <c>Supervising</c>.
/// </summary>
public sealed class SuperviseDoors
{
    /// <summary>What a supervisor's turn's record is opened as, for the session log.</summary>
    public const string OpenedKind = "supervise";

    private readonly ServiceClient _service;

    internal SuperviseDoors(ServiceClient service) => _service = service;

    /// <summary>
    /// Opens the record for <paramref name="attempt"/> of <paramref name="role"/>, at the subject's place, serving the session it waits on;
    /// a refusal is the host's sentence, and so is a host older than the door. A <paramref name="machine"/> subject's names no place.
    /// </summary>
    public async Task<HelperOpened> OpenAsync(
        SupervisorAttempt attempt, string role, string repository, string? workspace, string adapter, string tree, string? account,
        string? doorVersion, CancellationToken ct = default, bool machine = false)
    {
        var route = new OpenSupervise(
            attempt.Id, role, machine ? "" : repository, machine ? null : workspace, attempt.Subject.Session, adapter, tree, account, doorVersion,
            machine ? true : null);
        var door = RouteRequest.Of(route).Path;
        var (ok, status, payload, root) = await _service.SendJsonAsync(route, ct).ConfigureAwait(false);
        if (root is not { } answer) return new(null, $"the service at {_service.BaseUrl} has no `{door}` door ({status}) — is it older than this driver?");
        if (!ok) return new(null, Text(answer, "error") ?? payload);
        var session = answer.ValueKind == JsonValueKind.Object && answer.TryGetProperty("session", out var record) ? record : default;
        if (Text(session, "id") is not { } id) return new(null, $"the service at {_service.BaseUrl} answered `{door}` with no record.");
        _service.RecordOpened(new SessionOpened(id, OpenedKind, Text(session, "door") ?? adapter, Text(session, "repository") ?? repository)
        {
            Workspace = Text(session, "workspace"),
        });
        return new(id, Text(answer, "message") ?? "");
    }
}

public static class SuperviseDoorsOfClient
{
    extension(ServiceClient client)
    {
        /// <summary>The local host's door for a supervisor's turn's record, over this client.</summary>
        public SuperviseDoors Supervising => new(client);
    }
}
