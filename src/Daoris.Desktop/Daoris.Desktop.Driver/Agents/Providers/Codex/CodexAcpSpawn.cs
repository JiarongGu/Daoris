namespace Daoris.Driver;

/// <summary>
/// Codex's protocol door's spawn: <c>codex-acp</c>, the adapter's own bin, with its stdin open for the driver's frames. Its posture
/// is a mode on the wire, so the recipe carries no variable, and it runs no program of Claude Code's.
/// </summary>
internal sealed class CodexAcpSpawn : ISpawn
{
    private static readonly string[] Program = ["codex-acp"];

    public ProcessRecipe Start(SpawnRequest request) => ProcessRecipe.Of(Resolve(request.Command)) with { Input = true };

    public ProcessRecipe Chat(SpawnRequest request) => ProcessRecipe.Of(Resolve(request.Command));

    /// <summary>What the person's own Codex home hands the session, as the adapter's session configuration (<see cref="CodexOwnHome"/>).</summary>
    public void HandOwn(System.Diagnostics.ProcessStartInfo info, OwnTools own)
    {
        if (CodexOwnHome.Config(own) is { } config) info.Environment[CodexOwnHome.ConfigVariable] = config.ToJsonString();
    }

    public IReadOnlyList<ServerEntry> OwnServers(OwnTools own) => CodexOwnServers.Read(own);

    private static IReadOnlyList<string> Resolve(IReadOnlyList<string>? command) => command is { Count: > 0 } ? command : Program;
}
