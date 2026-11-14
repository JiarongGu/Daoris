using System.Runtime.CompilerServices;
using Lyntai.Agents;

namespace Daoris.Driver;

/// <summary>
/// Claude Code's seeding, bound when the driver loads where it carries the layer: both its doors resume a conversation file placed in an
/// account's project folder (what its carry measured on the install), so both take a written one in (<see cref="ClaudeSeedWriter"/>).
/// </summary>
internal static class ClaudeSeeding
{
    private const string Evidence = "a conversation file copied into another account's project folder is resumed there (the carry's measurement)";

    public static AgentProviderDescriptor Provider { get; } = new()
    {
        Id = ClaudeCodeProvider.Id,
        Doors = [LayerSeeding.Door("claude-code", AgentDoorKinds.Cli, Evidence), LayerSeeding.Door("claude-code-acp", AgentDoorKinds.Acp, Evidence)],
    };

    public static LayerSeeding Seeding { get; } = new(Provider, (tree, at) => new ClaudeSeedWriter(tree, at));

    [ModuleInitializer]
    internal static void Install() => ConversationSeedings.Bind(new ProviderId(ClaudeCodeProvider.Id), Seeding);
}
