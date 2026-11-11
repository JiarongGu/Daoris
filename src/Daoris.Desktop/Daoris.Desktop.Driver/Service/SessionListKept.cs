using System.Text.Json.Nodes;
using Daoris.Contracts.Sessions;

namespace Daoris.Driver;

/// <summary>The service's widest session list kept by its change since its last mark (<c>GET /api/sessions/changes</c>), as
/// <see cref="KeptList{T}"/> keeps one; reached as <see cref="SessionListDoors.Kept"/>.</summary>
public sealed class SessionListKept : KeptList<SessionResponse>
{
    private readonly ServiceClient _service;

    internal SessionListKept(ServiceClient service) => _service = service;

    protected override Task<JsonObject> AskAsync(string? since, CancellationToken ct) =>
        AskAsync(_service, new ListSessionChanges(since), new ListSessions(IncludeClosed: true), ct);
}
