namespace Daoris.Driver;

/// <summary>
/// A stand-in's spawn (AGENTPROVIDER1 row b): the command the machine's configuration names, run in the repository root with the
/// target in the environment. The protocol door keeps stdin open
/// for a driven session, because the driver writes frames into it (D53); a conversation's is open on both. The protocol stand-in takes
/// Claude Code's seam, as every protocol door did before the seam became a door's own (s-v takes it off Codex's and dsh's alone).
/// </summary>
internal sealed class StandInSpawn(string id, string example, bool protocol) : ISpawn
{
    public ProcessRecipe Start(SpawnRequest request) => ProcessRecipe.Of(Command(request.Command)) with { Input = protocol };

    public ProcessRecipe Chat(SpawnRequest request) => ProcessRecipe.Of(Command(request.Command));

    public bool ClaudeSeam => protocol;

    public void PointAtClaude(System.Diagnostics.ProcessStartInfo info, string managedClaude)
    {
        if (protocol) ClaudeCodeSpawn.PointAt(info, managedClaude);
    }

    private IReadOnlyList<string> Command(IReadOnlyList<string>? command) =>
        command is { Count: > 0 }
            ? command
            : throw new DriverException(
                $"the {id} adapter needs a command — name one in driver.json: "
                + "{ \"commands\": { \"" + id + "\": [\"node\", \"path/to/" + example + "\"] } }");
}
