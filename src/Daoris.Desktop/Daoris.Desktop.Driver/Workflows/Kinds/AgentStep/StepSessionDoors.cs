using System.Text.Json;
using Daoris.Contracts.Participants;
using static Daoris.Driver.Json;

namespace Daoris.Driver;

/// <summary>
/// The local host's door for the record of a step's session (<c>POST /api/step-sessions</c>): a chat on no quest at the run's place, with
/// no tree of its own, marked with the attempt it performs, reached as the client's <c>StepSessions</c>.
/// </summary>
public sealed class StepSessionDoors
{
    /// <summary>What a step's session's record is opened as, for the session log.</summary>
    public const string OpenedKind = "step";

    private readonly ServiceClient _service;

    internal StepSessionDoors(ServiceClient service) => _service = service;

    /// <summary>Opens the record for <paramref name="ask"/>'s attempt at its run's place; a refusal is the host's sentence, and so is a host older than the door.</summary>
    public async Task<HelperOpened> OpenAsync(StepAgentAsk ask, string adapter, string? account, string? doorVersion, CancellationToken ct = default)
    {
        var route = new OpenStepSession(ask.Invocation, ask.Repository, ask.Workspace, adapter, account, doorVersion);
        var door = RouteRequest.Of(route).Path;
        var (ok, status, payload, root) = await _service.SendJsonAsync(route, ct).ConfigureAwait(false);
        if (root is not { } answer) return new(null, $"the service at {_service.BaseUrl} has no `{door}` door ({status}) — is it older than this driver?");
        if (!ok) return new(null, Text(answer, "error") ?? payload);
        var session = answer.ValueKind == JsonValueKind.Object && answer.TryGetProperty("session", out var record) ? record : default;
        if (Text(session, "id") is not { } id) return new(null, $"the service at {_service.BaseUrl} answered `{door}` with no record.");
        _service.RecordOpened(new SessionOpened(id, OpenedKind, Text(session, "door") ?? adapter, Text(session, "repository") ?? ask.Repository)
        {
            Workspace = Text(session, "workspace"),
        });
        return new(id, Text(answer, "message") ?? "");
    }
}

public static class StepSessionDoorsOfClient
{
    extension(ServiceClient client)
    {
        /// <summary>The local host's door for a step's session's record, over this client.</summary>
        public StepSessionDoors StepSessions => new(client);
    }
}
