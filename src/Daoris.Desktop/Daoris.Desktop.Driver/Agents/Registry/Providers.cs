namespace Daoris.Driver;

/// <summary>
/// The one registration list (AGENTPROVIDER1 row b; the agent-provider design §10, §11): the providers this build carries, written on
/// the contract, each a line naming its registration and nothing else. A provider added later is one line here and its own folder
/// under <c>Agents/Providers/</c>; the census holds this file to naming registrations and never an id, a type's vocabulary or a
/// branch on either (<c>ProviderCensusTests</c>).
/// </summary>
internal static class Providers
{
    private static readonly Lazy<IReadOnlyList<Provider>> Registered = new(() => [StubProvider.Registration()]);

    /// <summary>Every provider registered, each with its doors; the shim presents their doors to today's readers.</summary>
    public static IReadOnlyList<Provider> Built() => Registered.Value;

    /// <summary>
    /// Whether a name is a stand-in's, a provider's id or one of its doors', which runs no model (the identity's flag): the one
    /// answer the roles' verbs and the brainstorm's provenance read. An unknown name is no stand-in.
    /// </summary>
    public static bool IsStandIn(string? name) =>
        name is { Length: > 0 }
        && Built().Any(provider => provider.Identity.StandIn
            && (Ids.Same(provider.Id.Value, name) || provider.Doors.Any(door => Ids.Same(door.Id.Value, name))));
}
