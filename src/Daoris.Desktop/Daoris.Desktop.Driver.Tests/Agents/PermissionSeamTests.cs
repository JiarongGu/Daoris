using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// How the rules reach a session (PERM1, D72): the harness's own way of taking a settings file at spawn,
/// in each door's vocabulary — and nothing handed to a harness a Claude Code rule means nothing to.
/// </summary>
/// <remarks>
/// <b>Keyless.</b> What these hold is what Daoris hands over. Whether the harness honours it is the real
/// session's to show (`tools/acp-trust-probe.mjs` measures the protocol door in an untrusted folder).
/// </remarks>
public sealed class PermissionSeamTests
{
    private static SessionTarget Target(string root) => new(
        "abc123", "Expose a streaming budget", "World streaming needs a per-frame cap.",
        "game", "engine", root, "http://localhost:5177");

    private static readonly string File = Path.Combine(Path.GetTempPath(), "daoris-home", "spawn", "s1.settings.json");

    /// <summary>
    /// `--settings &lt;file&gt;` — *"load additional settings from"* (`claude --help`, 2.1.280, HELP3's
    /// evidence): the command-line tier, which the harness merges with the person's and the repository's
    /// own, `deny` winning.
    /// </summary>
    [Fact]
    public void The_pipe_door_takes_the_rules_as_its_settings_flag()
    {
        var adapter = AdapterSet.Built().Resolve("claude-code");
        var info = adapter.Prepare(Target(Path.GetTempPath()), command: null);

        Assert.True(adapter.TakesSettings);
        adapter.HandSettings(info, File);

        var arguments = info.ArgumentList.ToList();
        var at = arguments.IndexOf("--settings");
        Assert.True(at >= 0, string.Join(' ', arguments));
        Assert.Equal(File, arguments[at + 1]);
        // The posture is unchanged by it: the rules are a scope, not a mode.
        Assert.Contains("acceptEdits", arguments);
    }

    /// <summary>A conversation is the same harness on the same door, and takes the same flag.</summary>
    [Fact]
    public void A_pipe_door_conversation_takes_them_too()
    {
        var adapter = AdapterSet.Built().Resolve("claude-code");
        var info = adapter.PrepareChat(new ChatTarget("engine", Path.GetTempPath(), "http://localhost:5177"), command: null);

        adapter.HandSettings(info, File);

        Assert.Contains("--settings", info.ArgumentList);
    }

    /// <summary>
    /// 🔴 Read from the adapter's own source at 0.79.0 (`dist/acp-agent.js` l. 5934–6045):
    /// `_meta.claudeCode.options` is spread into the Agent SDK's options, and a `settings` string is a
    /// file read against the session's cwd — an absolute path resolving to itself. Never a
    /// command-line argument on this door: the protocol carries it.
    /// </summary>
    [Fact]
    public void The_protocol_door_takes_them_on_session_new()
    {
        var adapter = AdapterSet.Built().Resolve("claude-code-acp");
        var info = adapter.Prepare(Target(Path.GetTempPath()), command: null);

        Assert.True(adapter.TakesSettings);
        adapter.HandSettings(info, File);
        Assert.DoesNotContain("--settings", info.ArgumentList);

        var meta = JsonSerializer.SerializeToElement(adapter.AcpSessionMeta(File));
        Assert.Equal(File, meta.GetProperty("claudeCode").GetProperty("options").GetProperty("settings").GetString());
    }

    /// <summary>
    /// A Claude Code rule means nothing to Codex's approval policy or dsh's permission mode, and a
    /// stand-in is no harness at all: they are handed nothing, and silence preserves how each spawned
    /// before (the rule every adapter default follows).
    /// </summary>
    [Theory]
    [InlineData("stub")]
    [InlineData("acp-stub")]
    [InlineData("codex-acp")]
    [InlineData("dsh")]
    public void Every_other_harness_is_handed_nothing(string name)
    {
        var adapter = AdapterSet.Built().Resolve(name);
        var info = adapter.Prepare(Target(Path.GetTempPath()), command: ["node", "agent.mjs"]);
        var before = info.ArgumentList.ToList();

        Assert.False(adapter.TakesSettings);
        adapter.HandSettings(info, File);

        Assert.Equal(before, info.ArgumentList);
        Assert.Null(adapter.AcpSessionMeta(File));
    }
}
