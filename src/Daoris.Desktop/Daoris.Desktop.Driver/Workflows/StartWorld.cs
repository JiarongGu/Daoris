namespace Daoris.Driver;

/// <summary>What binding a run reads and says through the loop's service: its quests, its asks, and the machine log's line.</summary>
internal sealed class StartWorld(ServiceClient service) : IWorkflowStartWorld
{
    public Task<IReadOnlyList<QuestView>> EveryQuestAsync(CancellationToken ct) => service.EveryQuestAsync(ct);

    public Task<AskView?> FindAskAsync(string id, CancellationToken ct) => service.FindAskAsync(id, ct);

    public void Chosen(LandingLine line) => service.LandingSaid(line);

    public async Task<string?> RemoteHostAsync(string repository, CancellationToken ct)
    {
        var row = (await service.RegistryAsync(ct).ConfigureAwait(false))
            .FirstOrDefault(each => string.Equals(each.Repository, repository, StringComparison.OrdinalIgnoreCase));
        return row?.Root is { Length: > 0 } root && Directory.Exists(root)
            ? GitWords.HostOf(await GitRemotes.AddressOf(root, "origin", ct: ct).ConfigureAwait(false))
            : null;
    }
}
