using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>What a proposal of Ask Daoris's is judged against: the driver's file and the machine's names, each kind's extra reading made only when needed.</summary>
public sealed class HelpProposalFacts(DriverContext context)
{
    private readonly DriverLoop _loop = context.Loop;

    /// <summary>Whether a pending proposal is a go naming a quest or an ask on Quests, judged against every one, so the service is asked for the records only then.</summary>
    public static bool JudgedAgainstRecords(IEnumerable<HelpProposal> proposals) => proposals.Any(proposal => ItemOn(proposal, "quests"));

    /// <summary>Whether a pending proposal is a go naming a session on Sessions, so the service is asked for its records only then.</summary>
    public static bool JudgedAgainstSessions(IEnumerable<HelpProposal> proposals) => proposals.Any(proposal => ItemOn(proposal, "sessions"));

    // A go naming an item on the view, read as the judge reads its view (`HelpGoProposals.Plan`); an item on any other view
    // is refused unread.
    private static bool ItemOn(HelpProposal proposal, string view) =>
        proposal.Kind == "go" && !string.IsNullOrWhiteSpace(proposal.Item)
        && string.Equals(proposal.Target?.Trim(), view, StringComparison.OrdinalIgnoreCase);

    /// <summary>The driver's file and the names the machine holds; for the kinds that need them, the roster, the quests and asks, and the sessions, asked only then.</summary>
    internal async Task<(DriverConfig Config, HelpMachineFacts Facts)> FactsAsync(
        ServiceClient service, IReadOnlyCollection<HelpProposal> proposals, CancellationToken ct)
    {
        var snapshot = await service.SnapshotAsync(ct).ConfigureAwait(false);
        var workspaces = snapshot.Repositories.Select(known => known.Workspace)
            .Append(RemoteTarget.DefaultWorkspace).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var config = DriverConfig.Load(_loop.ConfigPath);
        // the workspace the conversation that proposed them is bound to, read from its record, and only where one is
        // pending; the records are read once, for a go or a session's proposal too.
        var conversation = proposals.Select(proposal => proposal.Session).FirstOrDefault(session => !string.IsNullOrWhiteSpace(session));
        var records = conversation is null ? null : await service.SessionRecordsAsync(ct).ConfigureAwait(false);
        var facts = new HelpMachineFacts(
            [.. snapshot.Repositories.Select(known => known.Repository)], workspaces, _loop.Harnesses.Adapters.Names)
        {
            Circle = HelpCircle.OfRecord(_loop.Home, records?.FirstOrDefault(record => string.Equals(record.Id, conversation, StringComparison.OrdinalIgnoreCase))),
            Plugins = PluginCatalog.Load(_loop.Home, ProviderRegistry.Built().Names),
            Home = _loop.Home,
            Reserved = ProviderRegistry.Built().Names,
            OffersFolder = context.OffersFolder,
            Offers = PluginOffers.Load(context.OffersFolder, _loop.Home, ProviderRegistry.Built().Names),
            Registered = [.. snapshot.Repositories.Select(known => (known.Repository, (string?)known.Workspace, known.Root))],
        };

        if (JudgedAgainstRecords(proposals))
        {
            var (quests, asks) = await HelpRecordFacts.ReadAsync(service, ct).ConfigureAwait(false);
            facts = facts with { Quests = quests, Asks = asks };
        }

        if (JudgedAgainstSessions(proposals))
        {
            facts = facts with { Sessions = records ?? await service.SessionRecordsAsync(ct).ConfigureAwait(false) };
        }

        return (config, facts);
    }
}
