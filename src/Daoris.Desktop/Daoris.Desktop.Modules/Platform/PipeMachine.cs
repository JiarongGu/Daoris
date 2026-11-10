using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// What a rule's view is judged against as the machine holds it now: the driver's file and the names beside it (the registered
/// repositories with their checkouts, the plugins, the git identities, the quests the last look parked or held).
/// </summary>
public sealed record PipeMachine(DriverConfig Config, HelpMachineFacts Facts)
{
    /// <summary>The machine as the loop reads it; throws where the service cannot be read, which the pipe takes as a read that failed.</summary>
    public static async Task<PipeMachine> ReadAsync(DriverLoop loop, ServiceClient service, CancellationToken cancellationToken)
    {
        var snapshot = await service.SnapshotAsync(cancellationToken).ConfigureAwait(false);
        var workspaces = snapshot.Repositories.Select(known => known.Workspace)
            .Append(RemoteTarget.DefaultWorkspace).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var facts = new HelpMachineFacts([.. snapshot.Repositories.Select(known => known.Repository)], workspaces, loop.Harnesses.Adapters.Names)
        {
            Plugins = PluginCatalog.Load(loop.Home, ProviderRegistry.Built().Names),
            GitIdentities = [.. GitIdentities.Read(loop.Home).Select(each => new HelpGitIdentity(each.Id, each.Label, each.Host, each.Way))],
            Parked = loop.Parked.Latest,
            Held = HeldQuest.From(loop.Look.Latest),
            Registered = [.. snapshot.Repositories.Select(known => (known.Repository, (string?)known.Workspace, known.Root))],
        };
        return new PipeMachine(DriverConfig.Load(loop.ConfigPath), facts);
    }
}
