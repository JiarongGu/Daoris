namespace Daoris.Driver;

/// <summary>
/// Claude Code's protocol door's spawn: <c>claude-agent-acp</c> with its stdin open for the driver's frames. It runs the Agent SDK,
/// which runs Claude Code's own program, so it takes Claude Code's seam and the pipe door's switch for tools at the start.
/// </summary>
internal sealed class ClaudeAcpSpawn : ISpawn
{
    private static readonly string[] Program = ["claude-agent-acp"];

    public ProcessRecipe Start(SpawnRequest request) => ProcessRecipe.Of(Resolve(request.Command)) with { Input = true };

    /// <summary>A conversation's stdin is the driver's frames on this door; the shell opens it.</summary>
    public ProcessRecipe Chat(SpawnRequest request) => ProcessRecipe.Of(Resolve(request.Command));

    /// <summary>The Agent SDK runs <c>claude</c> with this process's environment, which is how the switch reaches it, and the pin's too.</summary>
    public IReadOnlyDictionary<string, string> ToolsUpFront => ClaudeCodeSpawn.LoadToolsUpFront;

    public bool ClaudeSeam => true;

    public void PointAtClaude(System.Diagnostics.ProcessStartInfo info, string managedClaude) => ClaudeCodeSpawn.PointAt(info, managedClaude);

    public SpawnChoiceVariables ChoiceVariables => ClaudeCodeSpawn.ChoiceVariablesOfClaude;

    private static IReadOnlyList<string> Resolve(IReadOnlyList<string>? command) => command is { Count: > 0 } ? command : Program;
}
