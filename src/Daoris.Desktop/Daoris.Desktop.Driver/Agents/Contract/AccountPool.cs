namespace Daoris.Driver;

/// <summary>
/// What an account key resolves to (the agent-provider design §2.2, §5; C3-2.2): the key and every provider whose account key it is,
/// with the doors of those providers. No first provider is selected, since a key is shared only by plugins naming one id, and a
/// reader of an account fact asks the provider or door it serves, whose facts are its own.
/// </summary>
public sealed record AccountPool
{
    public AccountPool(AccountKey key, IReadOnlyList<ProviderId> providers, IReadOnlyList<DoorId> doors)
    {
        if (providers.Count == 0) throw new ArgumentException("a pool holds at least the provider whose key it is", nameof(providers));
        Key = key;
        Providers = providers;
        Doors = doors;
    }

    public AccountKey Key { get; }

    /// <summary>Every provider holding the key, in the registry's order.</summary>
    public IReadOnlyList<ProviderId> Providers { get; }

    /// <summary>The doors of those providers; none for a pool of unavailable descriptors.</summary>
    public IReadOnlyList<DoorId> Doors { get; }
}
