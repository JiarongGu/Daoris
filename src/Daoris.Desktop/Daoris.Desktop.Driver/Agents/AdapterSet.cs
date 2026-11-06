using System.Collections.ObjectModel;

namespace Daoris.Driver;

/// <summary>
/// The adapters that exist, by name. D23 one layer up: `claude-code` first and supported, `codex`
/// second and explicit — and an unknown name is an error naming what exists, never a silent fallback,
/// because a driver that quietly spawned a different harness than the person configured is the same
/// failure as a repository that asked for one layout and received another.
/// </summary>
/// <param name="holders">
/// The agents whose accounts a door runs as (<see cref="HarnessToolchain.AccountOf"/>) where this set carries no door of that
/// agent, each by its toolchain (CODEXACCT1): see <see cref="Holder"/>. None by default, so a set a test builds asks nobody.
/// </param>
/// <param name="declared">
/// Every agent an installed plugin's harness names, enabled or not (POOLSTRAY1): see <see cref="Agents"/>. None by default.
/// </param>
public sealed class AdapterSet(
    IReadOnlyDictionary<string, ISessionAdapter> adapters, IReadOnlyDictionary<string, HarnessToolchain>? holders = null,
    IReadOnlyCollection<string>? declared = null)
{
    /// <summary>Every adapter this build has, in a stable order — what a roster enumerates.</summary>
    public IReadOnlyList<string> Names => [.. adapters.Keys.OrderBy(k => k, StringComparer.Ordinal)];

    /// <summary>
    /// The agents whose accounts Daoris keeps (POOLSTRAY1, D197): each name this set runs as a harness, each agent a door runs as,
    /// and each an installed plugin's harness names, a disabled one's too, so turning a plugin off loses none of its wiring. A
    /// folder under the account homes naming none of these is no agent, and the fold, every reader and the page pass it over
    /// (<see cref="HarnessSettings.AccountsOf"/>).
    /// </summary>
    public IReadOnlyList<string> Agents =>
    [
        .. Reserved
            .Concat(adapters.Select(each => each.Value.Toolchain?.AccountOf is { Length: > 0 } owner ? owner : each.Key))
            .Concat(declared ?? [])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(k => k, StringComparer.Ordinal),
    ];

    /// <summary>
    /// Every name this set runs as a harness, in a stable order (PLUGINRESERVE1): each door it carries and each agent a door
    /// runs as with no door here (<see cref="Holder"/>). Of the built set, what no plugin may declare
    /// (<see cref="PluginCatalog.Reserved"/>): a plugin's harness named for a holder would be a door of that agent, and
    /// <see cref="Holder"/> would stop answering for its accounts. Twin: the CLI's <c>reservedHarnesses()</c>.
    /// </summary>
    public IReadOnlyList<string> Reserved =>
    [
        .. adapters.Keys.Concat(holders?.Keys ?? [])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(k => k, StringComparer.Ordinal),
    ];

    /// <summary>
    /// The toolchain of an agent whose accounts a door runs as, where this set carries no door of it (CODEXACCT1), for the registry
    /// made over this set to hold (<see cref="ProviderRegistry.ToolchainOf"/>, the one reader by name). Null for a door's name and for
    /// an agent nobody declared. Not a door: no session starts on it (D23).
    /// </summary>
    internal HarnessToolchain? Holder(string agent) =>
        !adapters.ContainsKey(agent) && holders is not null && holders.TryGetValue(agent, out var held) ? held : null;

    /// <summary>
    /// The providers and doors this set carries, made once (<see cref="ProviderRegistry.Of"/>): the one reader of whose accounts an
    /// agent runs as is its <see cref="ProviderRegistry.OwnerOfAgent"/>, a door's account key, a provider's own, else the name itself;
    /// of an agent's toolchain, <see cref="ProviderRegistry.ToolchainOf"/>; of the plugin a door came from, <see cref="ProviderRegistry.DeclaredBy"/>.
    /// </summary>
    public ProviderRegistry Registry => LazyInitializer.EnsureInitialized(ref _registry, () => ProviderRegistry.Of(this))!;

    private ProviderRegistry? _registry;

    /// <summary>
    /// This set plus <paramref name="more"/> doors and the agents they declare. A name this set carries is kept as it is, so nothing here can
    /// be replaced: a door joins, and the set it joins is exactly what it was. Who offers the doors is the caller's (<c>WithPlugins</c>).
    /// </summary>
    public AdapterSet Joined(IEnumerable<ISessionAdapter> more, IEnumerable<string> declaredAgents)
    {
        var joined = new Dictionary<string, ISessionAdapter>(adapters, StringComparer.OrdinalIgnoreCase);
        foreach (var adapter in more)
        {
            joined.TryAdd(adapter.Name, adapter);
        }

        return new AdapterSet(joined, holders, [.. (declared ?? []).Concat(declaredAgents).Distinct(StringComparer.OrdinalIgnoreCase)]);
    }

    public ISessionAdapter Resolve(string name)
    {
        if (adapters.TryGetValue(name, out var adapter)) return adapter;

        throw new DriverException(
            $"unknown adapter '{name}' — one of: {string.Join(", ", adapters.Keys.OrderBy(k => k, StringComparer.Ordinal))}. "
            + "An adapter is added deliberately, never guessed.");
    }

    public static AdapterSet Built() => new(new Dictionary<string, ISessionAdapter>(
        // Every door this build carries is written on the contract and arrives through the shim, once, from the registration list
        // (AGENTPROVIDER1 rows b and d), so a provider added there touches no adapter file: the stand-ins, which gate-verify the
        // mechanism with no model in it (D46 §8), Claude Code's two doors, Codex's and dsh's.
        LegacyDoor.Over(Providers.Built()), StringComparer.OrdinalIgnoreCase),
        new Dictionary<string, HarnessToolchain>(StringComparer.OrdinalIgnoreCase)
    {
        // The agent `codex-acp` runs as, with no door of its own here (CODEXACCT1): its sign-in and status question.
        [CodexAccounts.Agent] = CodexAccounts.Toolchain,
    });
}
