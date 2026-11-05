namespace Daoris.Driver;

/// <summary>
/// Codex's protocol door's spawn (ACP3): <c>codex-acp</c>, the adapter's own bin, with its stdin open for the driver's frames. Its posture
/// is a mode on the wire, so the recipe carries no variable, and it runs no program of Claude Code's (the agent-provider design §7 row 7).
/// </summary>
internal sealed class CodexAcpSpawn : ISpawn
{
    private static readonly string[] Program = ["codex-acp"];

    public ProcessRecipe Start(SessionTarget target, IReadOnlyList<string>? command) => ProcessRecipe.Of(Resolve(command)) with { Input = true };

    public ProcessRecipe Chat(ChatTarget target, IReadOnlyList<string>? command) => ProcessRecipe.Of(Resolve(command));

    private static IReadOnlyList<string> Resolve(IReadOnlyList<string>? command) => command is { Count: > 0 } ? command : Program;
}
