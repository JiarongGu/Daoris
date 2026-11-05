using System.Collections.Concurrent;

namespace Daoris.Driver;

/// <summary>
/// What every registration declared of a spawn's environment, by provider (the agent-provider design §4.6, §7 row 9): each door's home
/// variable, its pool's key and its doors' account agents' keys, its programs' pinned variables and the variables its program is measured
/// to read. A door's spawn inherits none of another provider's (s-vi): a key handed only to its own provider's spawn (D172 point 3).
/// </summary>
/// <remarks>
/// Kept for the process, as the registration list is, since a door's spawn is made apart from the registry that registered it; the
/// built set is in it before any registry is made. Registering refuses a declared name of Daoris's own (<see cref="Spawning.Daoris"/>).
/// </remarks>
internal static class RegisteredVariables
{
    private static readonly ConcurrentDictionary<string, IReadOnlyList<string>> ByProvider = new(StringComparer.OrdinalIgnoreCase);

    private static readonly Lazy<IReadOnlyDictionary<string, IReadOnlyList<string>>> Built = new(() =>
        Providers.Built().ToDictionary(provider => provider.Id.Value, provider => Declared(provider, null), StringComparer.OrdinalIgnoreCase));

    /// <summary>Records what each of the registry's providers declares, refusing one that declares a name of Daoris's own.</summary>
    public static void Register(ProviderRegistry registry)
    {
        var declared = registry.Providers.ToDictionary(provider => provider.Id.Value, provider => Declared(provider, registry), StringComparer.OrdinalIgnoreCase);
        foreach (var (id, names) in declared)
        {
            if (names.FirstOrDefault(Spawning.Daoris) is { } daoris)
            {
                throw new DriverException(
                    $"provider '{id}' declares '{daoris}', a variable Daoris sets on every spawn itself — a provider's variables are its own, never Daoris's.");
            }
        }

        foreach (var (id, names) in declared) ByProvider[id] = names;
    }

    /// <summary>
    /// The variables another provider registers that <paramref name="provider"/> neither declares nor is measured to read: what its spawn
    /// drops of the environment it inherits, before the account's are bound.
    /// </summary>
    public static IReadOnlyCollection<string> Others(Provider provider)
    {
        var mine = Declared(provider, null).Concat(ByProvider.GetValueOrDefault(provider.Id.Value) ?? []).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return
        [
            .. Built.Value.Concat(ByProvider)
                .Where(entry => !Ids.Same(entry.Key, provider.Id.Value))
                .SelectMany(entry => entry.Value)
                .Where(name => !mine.Contains(name))
                .Distinct(StringComparer.OrdinalIgnoreCase),
        ];
    }

    /// <summary>What <paramref name="provider"/> declares; its doors' account agents' keys read through <paramref name="registry"/> where one is given.</summary>
    internal static IReadOnlyList<string> Declared(Provider provider, ProviderRegistry? registry) =>
    [
        .. provider.Doors.Select(door => door.Home.Variable)
            .Append(provider.Accounts.IsKnown ? provider.Accounts.Required.Facts.KeyVariable : null)
            .Concat(provider.Doors.Select(door => door.Lent.Accounts.KeyVariable))
            .Concat(registry is null ? Enumerable.Empty<string?>() : provider.Doors.Select(door => registry.AccountAgentOf(door.Id)?.Accounts.KeyVariable))
            .Concat(Pinned(provider.Program))
            .Concat(provider.Doors.SelectMany(door => Pinned(door.Program)))
            .Concat(provider.Vocabulary.IsKnown ? provider.Vocabulary.Required.Variables : Array.Empty<string>())
            .OfType<string>()
            .Where(name => name.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.Ordinal),
    ];

    private static IEnumerable<string> Pinned(Known<ToolSpec> program) =>
        program.IsKnown && program.Required.PinnedEnvironment is { } pinned ? pinned.Keys : [];
}
