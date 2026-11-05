namespace Daoris.Driver;

/// <summary>
/// Claude Code's protocol door's spawn (ACP2): <c>claude-agent-acp</c> with its stdin open for the driver's frames. It runs the Agent SDK,
/// which runs Claude Code's own program, so it takes Claude Code's seam and the pipe door's switch for tools at the start (HELP5).
/// </summary>
internal sealed class ClaudeAcpSpawn : ISpawn
{
    private static readonly string[] Program = ["claude-agent-acp"];

    public ProcessRecipe Start(SessionTarget target, IReadOnlyList<string>? command) => ProcessRecipe.Of(Resolve(command)) with { Input = true };

    /// <summary>A conversation's stdin is the driver's frames on this door; the shell opens it.</summary>
    public ProcessRecipe Chat(ChatTarget target, IReadOnlyList<string>? command) => ProcessRecipe.Of(Resolve(command));

    /// <summary>The Agent SDK runs <c>claude</c> with this process's environment, which is how the switch reaches it, and the pin's too (AGT2).</summary>
    public IReadOnlyDictionary<string, string> ToolsUpFront => ClaudeCodeSpawn.LoadToolsUpFront;

    public bool ClaudeSeam => true;

    public SpawnChoiceVariables ChoiceVariables => ClaudeCodeSpawn.ChoiceVariablesOfClaude;

    private static IReadOnlyList<string> Resolve(IReadOnlyList<string>? command) => command is { Count: > 0 } ? command : Program;
}
