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
            .Concat(adapters.Select(each => each.Value.Toolchain?.Owner(each.Key) ?? each.Key))
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
    /// The toolchain of an agent whose accounts a door runs as, where this set carries no door of it (CODEXACCT1): its own
    /// binary, sign-in, status question and account seam, so a door onto it signs its accounts in and asks them as the
    /// agent itself does, the way <c>claude-code-acp</c> reaches <c>claude-code</c>'s (AGT7). Null for an agent this set
    /// carries a door of, which answers for itself, and for one nobody declared.
    /// </summary>
    /// <remarks>
    /// Not a door: it is no adapter, the roster lists it nowhere, and no session starts on it (D23: an adapter arrives
    /// deliberately). A plugin's door that names the same agent reads the same holder.
    /// </remarks>
    public HarnessToolchain? Holder(string agent) =>
        !adapters.ContainsKey(agent) && holders is not null && holders.TryGetValue(agent, out var held) ? held : null;

    /// <summary>The plugin a harness came from (D64), or null for one this build carries.</summary>
    public string? DeclaredBy(string name) =>
        adapters.TryGetValue(name, out var adapter) && adapter is IPluginAdapter declared
            ? declared.Plugin
            : null;

    /// <summary>
    /// This set plus every harness the catalogue's contributing plugins declare (D64 §3). The
    /// catalogue has already refused any name this set carries, so nothing here can be replaced —
    /// a plugin adds, and the built-in set is exactly what it was.
    /// </summary>
    public AdapterSet WithPlugins(IAgentPlugins catalog)
    {
        var joined = new Dictionary<string, ISessionAdapter>(adapters, StringComparer.OrdinalIgnoreCase);
        foreach (var adapter in catalog.Adapters)
        {
            joined.TryAdd(adapter.Name, adapter);
        }

        return new AdapterSet(joined, holders, [.. (declared ?? []).Concat(catalog.DeclaredAgents).Distinct(StringComparer.OrdinalIgnoreCase)]);
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
