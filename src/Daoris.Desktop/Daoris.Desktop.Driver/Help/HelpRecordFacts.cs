namespace Daoris.Driver;

/// <summary>A quest as the service answered it, for the go proposal that names one.</summary>
/// <param name="Status">`Open`, `Taken`, `Done` or `Declined`, as the service spells it.</param>
/// <param name="Workspace">The circle it is held in, as the service answered it, the default where a host answers none.</param>
public sealed record HelpQuestFacts(string Id, string Title, string To, string Status, bool Deletable, string Workspace = RemoteTarget.DefaultWorkspace);

/// <summary>An ask as the service answered it, with the quests it became.</summary>
public sealed record HelpAskFacts(string Id, string Sentence, string Workspace, string State, IReadOnlyList<string> Quests, bool Deletable);

/// <summary>Every quest and ask the service holds, closed ones included, as a go naming one is judged against them.</summary>
public static class HelpRecordFacts
{
    public static async Task<(IReadOnlyList<HelpQuestFacts> Quests, IReadOnlyList<HelpAskFacts> Asks)> ReadAsync(ServiceClient service, CancellationToken ct)
    {
        var quests = await service.EveryQuestAsync(ct).ConfigureAwait(false);
        var asks = await service.EveryAskAsync(ct).ConfigureAwait(false);
        return (
            [.. quests.Select(quest => new HelpQuestFacts(quest.Id, quest.Title, quest.To, quest.Status, quest.Deletable, quest.Workspace))],
            [.. asks.Select(ask => new HelpAskFacts(ask.Id, ask.Sentence, ask.Workspace, ask.State, ask.Quests, ask.Deletable))]);
    }
}
