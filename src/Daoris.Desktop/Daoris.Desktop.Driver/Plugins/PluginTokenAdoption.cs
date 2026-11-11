namespace Daoris.Driver;

/// <summary>
/// The git identity's API token handed to the plugin that now keeps its own, once: for each command that declares <c>adopts</c>, the
/// token of the first identity serving one of its hosts is sent to that command as its answer, and the identity keeps none after, so a
/// person who set it there need not set it again. Said in the machine log by ids alone, never the value. It ends when no identity holds
/// an API token, the kind nothing reads any more.
/// </summary>
public static class PluginTokenAdoption
{
    /// <summary>One round of a command, as <see cref="PluginCommandRun.RoundAsync"/> runs it.</summary>
    public delegate Task<CommandRound> Round(PluginEntry plugin, string command, IReadOnlyDictionary<string, string> answers, CancellationToken ct);

    public static async Task RunAsync(string home, MachineLog log, CancellationToken ct, Round? round = null)
    {
        round ??= (plugin, command, answers, token) => PluginCommandRun.RoundAsync(home, plugin, command, [], answers, CommandWire.Adoption, token);
        var identities = GitIdentities.Read(home);
        foreach (var plugin in PluginCatalog.Load(home).Plugins.Where(each => each.Contributes))
        {
            var id = plugin.Manifest.Id;
            if (plugin.Manifest.Interfaces.Commands.FirstOrDefault(each => each.Adopts is not null) is not { Adopts: { } adopts } command
                || !PluginSwitches.IsOn(home, id, PluginSwitches.Commands)) continue;
            var identity = identities.FirstOrDefault(each => Serves(adopts, each.Host) && GitTokens.Has(home, each.Id, GitTokens.Api));
            if (identity is null || GitTokens.Read(home, identity.Id, GitTokens.Api) is not { } token) continue;
            try
            {
                var done = await round(plugin, command.Name, new Dictionary<string, string> { [adopts.Answer] = token }, ct).ConfigureAwait(false);
                if (done.Ask is null && done.Exit == 0)
                {
                    GitTokens.Clear(home, identity.Id, GitTokens.Api);
                    log.Info("plugin.token.adopted", ("plugin", id), ("command", command.Name), ("identity", identity.Id));
                }
                else
                {
                    log.Warn("plugin.token.unadopted", ("plugin", id), ("command", command.Name), ("identity", identity.Id), ("exit", done.Exit),
                        ("asked", done.Ask is not null));
                }
            }
            catch (DriverException)
            {
                log.Warn("plugin.token.unadopted", ("plugin", id), ("command", command.Name), ("identity", identity.Id), ("why", "unanswered"));
            }
        }
    }

    /// <summary>Whether an identity's host is one the adoption names, or a domain inside one.</summary>
    public static bool Serves(PluginAdoption adopts, string host)
    {
        var canonical = GitHosts.Canonical(host).ToLowerInvariant();
        return adopts.Hosts.Any(each => canonical == each || canonical.EndsWith("." + each, StringComparison.Ordinal));
    }
}
