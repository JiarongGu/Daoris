namespace Daoris.Driver;

/// <summary>
/// dsh's spawn over the protocol door (ACP3/D53): <c>dsh --profile acp</c>, the automation surface its maker kept, with its stdin open
/// for the driver's frames. 🔴 Its wire carries no posture (0.1.6-alpha.2 answers <c>session/new</c> with no <c>modes</c>), so the posture
/// is its own variable read at boot, set by the recipe: <c>workspace-write</c> is D37 in its vocabulary, stated even where it is also its
/// default, since a posture that happens to match somebody else's default is not one Daoris has set. It holds no conversation.
/// </summary>
internal sealed class DshSpawn : ISpawn
{
    private static readonly string[] Program = ["dsh"];

    public ProcessRecipe Start(SessionTarget target, IReadOnlyList<string>? command) =>
        ProcessRecipe.Of(Resolve(command), ["--profile", DshAdapter.Profile]) with
        {
            Input = true,
            Variables = new Dictionary<string, string> { [DshAdapter.PermissionVariable] = DshAdapter.Posture },
        };

    public ProcessRecipe Chat(ChatTarget target, IReadOnlyList<string>? command) =>
        throw new DriverException(
            "the `dsh` adapter cannot hold a conversation — it spawns an agent that takes its "
            + "target once and runs to completion. Chat with an adapter that declares `interactive`.");

    private static IReadOnlyList<string> Resolve(IReadOnlyList<string>? command) => command is { Count: > 0 } ? command : Program;
}
