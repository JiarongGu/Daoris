using System.Text.Json.Nodes;
using Daoris.Contracts.Sessions;

namespace Daoris.Driver;

/// <summary>
/// The service's widest session list kept by its change since the mark it last read (<c>GET /api/sessions/changes</c>), as
/// <see cref="KeptList{T}"/> keeps one. Its text is what a reader of the list's answer parses (<see cref="SessionRecords.Parse"/>): the
/// one read for a reader that needs records beside the one it names (a line's other parts, a quest's earlier sessions, every tree).
/// Reached as <see cref="SessionListDoors.Kept"/>.
/// </summary>
public sealed class SessionListKept : KeptList<SessionResponse>
{
    private readonly ServiceClient _service;

    internal SessionListKept(ServiceClient service) => _service = service;

    protected override Task<JsonObject> AskAsync(string? since, CancellationToken ct) =>
        AskAsync(_service, new ListSessionChanges(since), new ListSessions(IncludeClosed: true), ct);
}
