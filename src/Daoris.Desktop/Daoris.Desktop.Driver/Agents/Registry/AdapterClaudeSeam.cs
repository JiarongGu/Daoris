namespace Daoris.Driver;

/// <summary>
/// Whether a start on a door is pointed at the <c>claude</c> Claude Code's pin chose (ACP2; the agent-provider design §7 row 7): a contract
/// door's spawn says so of its own program, so Codex's and dsh's doors take none of it. Any other protocol adapter, a plugin's, keeps it as
/// every protocol door had it: a plugin may run <c>claude-agent-acp</c>, and its program is unmeasured.
/// </summary>
internal static class AdapterClaudeSeam
{
    public static bool TakesClaudeSeam(this ISessionAdapter adapter) =>
        adapter is LegacyDoor door ? door.ClaudeSeam : adapter.Abilities().Transport == Transport.Protocol;
}
