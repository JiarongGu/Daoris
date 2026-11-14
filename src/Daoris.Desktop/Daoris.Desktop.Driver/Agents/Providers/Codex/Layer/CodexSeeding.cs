using System.Runtime.CompilerServices;
using Lyntai.Agents;

namespace Daoris.Driver;

/// <summary>
/// Codex's seeding, bound when the driver loads where it carries the layer: its protocol door loads a rollout file placed under an account's
/// <c>sessions/</c> by its id (what its carry measured on the install), so it takes a written one in (<see cref="CodexSeedWriter"/>).
/// </summary>
internal static class CodexSeeding
{
    public static AgentProviderDescriptor Provider { get; } = new()
    {
        Id = CodexAccounts.Agent,
        Doors = [LayerSeeding.Door("codex-acp", AgentDoorKinds.Acp, "a rollout copied into another account's sessions folder is loaded there (the carry's measurement)")],
    };

    public static LayerSeeding Seeding { get; } = new(Provider, (tree, at) => new CodexSeedWriter(tree, at));

    [ModuleInitializer]
    internal static void Install() => ConversationSeedings.Bind(new ProviderId(CodexAccounts.Agent), Seeding);
}
