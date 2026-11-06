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

    /// <summary>The profile dsh boots for the automation surface, its ACP server automation-only by its maker's own choice.</summary>
    public const string Profile = "acp";

    /// <summary>dsh's permission posture, an environment variable it reads at boot, feeding its sandbox and its approval policy.</summary>
    public const string PermissionVariable = "DSH_PERMISSION_MODE";

    /// <summary>
    /// D37 in dsh's vocabulary: writes inside the workspace proceed, anything past the sandbox asks, which over ACP is refused by
    /// construction (D52). Never <c>danger-full-access</c>, which is approvals off.
    /// </summary>
    public const string Posture = "workspace-write";

    public ProcessRecipe Start(SpawnRequest request) =>
        ProcessRecipe.Of(Resolve(request.Command), ["--profile", Profile]) with
        {
            Input = true,
            Variables = new Dictionary<string, string> { [PermissionVariable] = Posture },
        };

    public ProcessRecipe Chat(SpawnRequest request) =>
        throw new DriverException(
            "the `dsh` adapter cannot hold a conversation — it spawns an agent that takes its "
            + "target once and runs to completion. Chat with an adapter that declares `interactive`.");

    private static IReadOnlyList<string> Resolve(IReadOnlyList<string>? command) => command is { Count: > 0 } ? command : Program;
}
