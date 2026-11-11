namespace Daoris.Driver;

/// <summary>
/// Claude Code's protocol door's dialect: what <c>session/new</c> carries in <c>_meta.claudeCode.options</c>, which the adapter spreads into
/// the Agent SDK's options.
/// </summary>
internal sealed class ClaudeProtocolDialect : IProtocolDialect
{
    /// <summary>
    /// The rules Daoris composed as the options' <c>settings</c>, a file read against the session's cwd (claude-agent-acp
    /// 0.79.0, <c>dist/acp-agent.js</c>, l. 5934–6045 and 6003–6010), and the harness plugins a spawn composed as its
    /// <c>plugins</c>, each <c>{ type: "local", path }</c>, which the SDK passes to Claude Code as one <c>--plugin-dir</c> each
    /// (claude-agent-acp 0.91.0, l. 8115–8223; the SDK 0.3.300, <c>sdk.d.ts</c>, l. 2036–2049).
    /// </summary>
    public object? SessionMeta(string settingsFile, IReadOnlyList<string> plugins, OwnTools? own)
    {
        var options = new Dictionary<string, object?> { ["settings"] = settingsFile };
        if (plugins.Count > 0) options["plugins"] = Plugins(plugins);
        return Own(options, own);
    }

    /// <summary>
    /// The options with <c>settingSources: ["user"]</c> and <c>strictMcpConfig: true</c>, which the adapter spreads over its own list
    /// as the holder's terminal's flags are on the pipe door: the folder's settings, hooks and servers are not the session's.
    /// </summary>
    public object? SourcesAloneMeta(string settingsFile, IReadOnlyList<string> plugins, OwnTools? own) => Own(
        new Dictionary<string, object?>
        {
            ["settings"] = settingsFile, ["settingSources"] = new[] { "user" }, ["strictMcpConfig"] = true, ["plugins"] = Plugins(plugins),
        },
        own);

    private static object[] Plugins(IReadOnlyList<string> plugins) => [.. plugins.Select(path => new { type = "local", path })];

    private static object Own(Dictionary<string, object?> options, OwnTools? own)
    {
        // The client's options are spread after the adapter's own, so this preset replaces its own whole; a null extra argument is a bare flag.
        if (own is not null && ClaudeOwnMemory.Text(own) is { } memory)
        {
            options["systemPrompt"] = new
            {
                type = "preset",
                preset = "claude_code",
                append = memory,
            };
        }

        if (own is not null && ClaudeOwnChrome.On(own))
        {
            options["extraArgs"] = new Dictionary<string, string?> { ["chrome"] = null };
        }

        return new { claudeCode = new { options } };
    }
}
