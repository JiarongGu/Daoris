using System.Runtime.CompilerServices;
using Lyntai.Agents;

namespace Daoris.Driver;

/// <summary>
/// dsh's seeding, bound when the driver loads where it carries the layer: its protocol door resumes (<c>session/resume</c>) a session log
/// placed under an account's <c>DSH_HOME</c> by its id and tree, which it loads (<see cref="DshSeedWriter"/>).
/// </summary>
internal static class DshSeeding
{
    public static AgentProviderDescriptor Provider { get; } = new()
    {
        Id = "dsh",
        Doors = [LayerSeeding.Door("dsh", AgentDoorKinds.Acp, "a session log placed under its home is resumed there by id (the writer's measurement)")],
    };

    public static LayerSeeding Seeding { get; } = new(Provider, (tree, at) => new DshSeedWriter(tree, at), DshSeedWriter.Physical);

    [ModuleInitializer]
    internal static void Install() => ConversationSeedings.Bind(new ProviderId("dsh"), Seeding);
}
