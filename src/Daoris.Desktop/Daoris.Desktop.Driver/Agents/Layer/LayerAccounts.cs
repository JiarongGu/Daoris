using Lyntai.Agents;

namespace Daoris.Driver;

/// <summary>
/// The account a layer-run door is handed: the one the plan's walk picked, never a pool the layer walks.
/// It is handed under the registration's account key, its agent where one is set and else the door's own id, so the layer's refusal of
/// another provider's account never fires under either of its answers, and Daoris's records keep their own keys.
/// </summary>
internal static class LayerAccounts
{
    /// <summary>The account <paramref name="profile"/> at <paramref name="home"/>, or null for the tool's own sign-in, which is handed none.</summary>
    public static AgentAccount? Of(AgentProviderDescriptor registration, string? profile, string home) =>
        profile is { Length: > 0 } named ? new AgentAccount { Provider = registration.AccountKey, Name = named, Home = home } : null;
}
