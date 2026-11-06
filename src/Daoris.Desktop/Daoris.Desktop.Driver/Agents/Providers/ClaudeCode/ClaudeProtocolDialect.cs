namespace Daoris.Driver;

/// <summary>
/// Claude Code's protocol door's dialect: what <c>session/new</c> carries in <c>_meta.claudeCode.options</c>, which the adapter spreads into
/// the Agent SDK's options.
/// </summary>
internal sealed class ClaudeProtocolDialect : IProtocolDialect
{
    /// <summary>
    /// The rules Daoris composed (PERM1, D72) as the options' <c>settings</c>, a file read against the session's cwd (claude-agent-acp
    /// 0.79.0, <c>dist/acp-agent.js</c>, l. 5934–6045 and 6003–6010), and the harness plugins a spawn composed (HELPPLUG1b) as its
    /// <c>plugins</c>, each <c>{ type: "local", path }</c>, which the SDK passes to Claude Code as one <c>--plugin-dir</c> each
    /// (claude-agent-acp 0.91.0, l. 8115–8223; the SDK 0.3.300, <c>sdk.d.ts</c>, l. 2036–2049).
    /// </summary>
    public object? SessionMeta(string settingsFile, IReadOnlyList<string> plugins) => plugins.Count == 0
        ? new { claudeCode = new { options = new { settings = settingsFile } } }
        : new
        {
            claudeCode = new
            {
                options = new { settings = settingsFile, plugins = plugins.Select(path => new { type = "local", path }).ToArray() },
            },
        };
}
