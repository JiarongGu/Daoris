namespace Daoris.Driver;

/// <summary>
/// One MCP server a session is handed on `session/new` (ACP4) — a local program the agent spawns.
/// </summary>
/// <remarks>
/// <b>This is how a driven session gets a voice without anything being written into its repository.</b>
/// The pipe door depends on the repository's own `.mcp.json` wiring the knowledge tools; the protocol
/// carries them itself, which is both simpler and the only shape compatible with `reaching-in` for a
/// repository that has not wired one.
/// </remarks>
/// <param name="Name">What the agent calls it — tools arrive as `mcp__&lt;name&gt;__&lt;tool&gt;`.</param>
/// <param name="Command">The program, resolved by this side before it is named.</param>
/// <param name="Arguments">Its arguments, as given.</param>
/// <param name="Environment">
/// What the server needs to find the same store this driver is using. Passed through rather than
/// invented: a scratch run overrides these, and a session writing to the machine's real store because
/// the overrides did not travel is the failure that would be hardest to see.
/// </param>
public sealed record AcpMcpServer(
    string Name,
    string Command,
    IReadOnlyList<string> Arguments,
    IReadOnlyDictionary<string, string> Environment);
