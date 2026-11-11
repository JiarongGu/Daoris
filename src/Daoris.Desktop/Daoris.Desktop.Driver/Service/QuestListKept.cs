using System.Text.Json.Nodes;
using Daoris.Contracts.Quests;

namespace Daoris.Driver;

/// <summary>
/// The service's widest quest list (every quest, closed ones and every workspace's included) kept by its change since the mark it last
/// read (<c>GET /api/quests/changes</c>), as <see cref="KeptList{T}"/> keeps one: every reader of every quest reads it (the planners'
/// views, a trace's text, the slice), and one quest's lookup reads one record of it. Reached as <see cref="ServiceClient.QuestList"/>.
/// </summary>
public sealed class QuestListKept : KeptList<QuestResponse>
{
    private readonly ServiceClient _service;

    internal QuestListKept(ServiceClient service) => _service = service;

    protected override Task<JsonObject> AskAsync(string? since, CancellationToken ct) =>
        AskAsync(_service, new ListQuestChanges(since), new ListQuests(IncludeClosed: true), ct);
}
