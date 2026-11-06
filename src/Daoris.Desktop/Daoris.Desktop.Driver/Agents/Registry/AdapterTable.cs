namespace Daoris.Driver;

/// <summary>
/// The doors a registry was made over, by name, as the old seam's readers still ask them (<see cref="ISessionAdapter"/>), with the
/// agents a door runs as that carry no door of their own here (CODEXACCT1) and every agent an installed plugin's harness names,
/// enabled or not (POOLSTRAY1). The registry holds it and <see cref="ProviderRegistration"/> reads it to make the providers; it ends
/// with <see cref="ISessionAdapter"/>, when the registry is made from <see cref="Providers"/> and the plugins' doors alone.
/// </summary>
internal sealed class AdapterTable(
    IReadOnlyDictionary<string, ISessionAdapter> adapters, IReadOnlyDictionary<string, HarnessToolchain>? holders = null,
    IReadOnlyCollection<string>? declared = null)
{
    /// <summary>Every door this build has, in a stable order.</summary>
    public IReadOnlyList<string> Names => [.. adapters.Keys.OrderBy(k => k, StringComparer.Ordinal)];

    /// <summary>
    /// The agents whose accounts Daoris keeps (POOLSTRAY1, D197): each name run as a harness, each agent a door runs as, and each an
    /// installed plugin's harness names, a disabled one's too. A folder under the account homes naming none of these is no agent.
    /// </summary>
    public IReadOnlyList<string> Agents =>
    [
        .. Reserved
            .Concat(adapters.Select(each => each.Value.Toolchain?.AccountOf is { Length: > 0 } owner ? owner : each.Key))
            .Concat(declared ?? [])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(k => k, StringComparer.Ordinal),
    ];

    /// <summary>Every name run as a harness, in a stable order: each door and each agent a door runs as with no door here (<see cref="Holder"/>).</summary>
    public IReadOnlyList<string> Reserved =>
    [
        .. adapters.Keys.Concat(holders?.Keys ?? [])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(k => k, StringComparer.Ordinal),
    ];

    /// <summary>The toolchain of an agent whose accounts a door runs as, with no door here; null for a door's name and an agent nobody declared.</summary>
    public HarnessToolchain? Holder(string agent) =>
        !adapters.ContainsKey(agent) && holders is not null && holders.TryGetValue(agent, out var held) ? held : null;

    public ISessionAdapter Resolve(string name)
    {
        if (adapters.TryGetValue(name, out var adapter)) return adapter;

        throw new DriverException(
            $"unknown adapter '{name}' — one of: {string.Join(", ", adapters.Keys.OrderBy(k => k, StringComparer.Ordinal))}. "
            + "An adapter is added deliberately, never guessed.");
    }

    /// <summary>This table plus <paramref name="more"/> doors and the agents they declare; a name already carried is kept as it is.</summary>
    public AdapterTable Joined(IEnumerable<ISessionAdapter> more, IEnumerable<string> declaredAgents)
    {
        var joined = new Dictionary<string, ISessionAdapter>(adapters, StringComparer.OrdinalIgnoreCase);
        foreach (var adapter in more) joined.TryAdd(adapter.Name, adapter);

        return new AdapterTable(joined, holders, [.. (declared ?? []).Concat(declaredAgents).Distinct(StringComparer.OrdinalIgnoreCase)]);
    }
}
