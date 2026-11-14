using System.Collections.Concurrent;

namespace Daoris.Driver;

/// <summary>
/// Which seeding writes each provider's conversations: Lyntai's agent layer carries the seed writers, so a provider folder's <c>Layer/</c>
/// binds its provider's when the driver loads, and a driver built without the layer binds none, so every move is handed the brief.
/// </summary>
public static class ConversationSeedings
{
    private static readonly ConcurrentDictionary<ProviderId, IConversationSeeding> Bound = new();

    /// <summary>The seeding <paramref name="provider"/> declares, or null where it declares none.</summary>
    public static IConversationSeeding? Of(ProviderId provider) => Bound.GetValueOrDefault(provider);

    /// <summary>The seeding of <paramref name="door"/>'s provider where it seeds through that door, else null.</summary>
    public static IConversationSeeding? Through(ProviderRegistry registry, DoorId door) =>
        Of(registry.ProviderOf(door)) is { } seeding && seeding.Seeds(door.Value) ? seeding : null;

    /// <summary>Binds <paramref name="provider"/>'s seeding, once, before any move asks it.</summary>
    internal static void Bind(ProviderId provider, IConversationSeeding seeding)
    {
        if (!Bound.TryAdd(provider, seeding)) throw new InvalidOperationException($"the seeding of '{provider.Value}' is bound twice.");
    }

    /// <summary>Unbinds <paramref name="provider"/>'s seeding: a test that bound a stand-in's takes it back.</summary>
    internal static void Unbind(ProviderId provider) => Bound.TryRemove(provider, out _);
}
