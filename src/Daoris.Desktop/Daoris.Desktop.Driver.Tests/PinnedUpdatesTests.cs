using System.Diagnostics;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// 🔴 <b>A pin stays the version pinned</b> (AGT2). Measured 2026-09-23 on a pinned Claude Code
/// 2.1.270 with no login and no model: its own <c>claude doctor</c> reported <i>Auto-updates:
/// enabled</i>, channel <i>latest</i>, and called itself an <i>npm-global</i> install, which a copy in
/// Daoris's own folder is not. With <c>DISABLE_UPDATES=1</c> it reported updates disabled, refused
/// <c>claude update</c> outright, and stayed 2.1.270.
/// </summary>
/// <remarks>
/// So a pinned binary runs with the tool's own switch, on every spawn Daoris makes of it: a session
/// over either door, and the probe that asks its version and login state. An UNPINNED binary is the
/// machine's, and its updates are the machine's business — it gains nothing (D48 §2a, byte for byte).
/// </remarks>
public sealed class PinnedUpdatesTests : IDisposable
{
    private readonly string _home = Path.Combine(
        Path.GetTempPath(), "daoris-pinned-tests", Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        if (Directory.Exists(_home)) Directory.Delete(_home, recursive: true);
    }

    private static HarnessToolchain ClaudeCode => AdapterSet.Built().Resolve("claude-code").Toolchain!;

    [Fact]
    public void Claude_code_declares_the_switch_it_was_measured_with()
    {
        Assert.Equal("1", ClaudeCode.PinnedEnvironment!["DISABLE_UPDATES"]);
    }

    /// <summary>The pipe door: a spawn of the managed binary runs with updates off.</summary>
    [Fact]
    public void A_pinned_spawn_runs_with_the_tool_s_updates_off()
    {
        var info = new ProcessStartInfo("claude");

        HarnessProbe.Apply(info, ClaudeCode, profileHome: null, binary: "C:/somewhere/toolchain/claude-code/2.1.270/claude.cmd");

        Assert.Equal("1", info.Environment["DISABLE_UPDATES"]);
    }

    /// <summary>🔴 The additive rule: a machine's own binary gains no variable because pins exist.</summary>
    [Fact]
    public void An_unpinned_spawn_is_left_as_the_machine_has_it()
    {
        var info = new ProcessStartInfo("claude");
        info.Environment.Remove("DISABLE_UPDATES");

        HarnessProbe.Apply(info, ClaudeCode, profileHome: null, binary: null);

        Assert.False(info.Environment.ContainsKey("DISABLE_UPDATES"));
    }

    /// <summary>
    /// The protocol door: the Agent SDK spawns the managed <c>claude</c> with the adapter's own
    /// environment, so the switch travels on the adapter's spawn — and only when a managed
    /// <c>claude</c> was pointed at.
    /// </summary>
    [Fact]
    public void The_protocol_door_s_managed_claude_runs_with_updates_off()
    {
        var acp = AdapterSet.Built().Resolve("claude-code-acp").Toolchain!;
        var pointed = new ProcessStartInfo("claude-agent-acp");
        var unpointed = new ProcessStartInfo("claude-agent-acp");
        unpointed.Environment.Remove("DISABLE_UPDATES");

        HarnessProbe.Apply(pointed, acp, profileHome: null, binary: null,
            claudeExecutable: "C:/somewhere/toolchain/claude-code/2.1.270/claude.cmd");
        HarnessProbe.Apply(unpointed, acp, profileHome: null, binary: null, claudeExecutable: null);

        Assert.Equal("1", pointed.Environment["DISABLE_UPDATES"]);
        Assert.False(unpointed.Environment.ContainsKey("DISABLE_UPDATES"));
    }

    /// <summary>
    /// The probe asks a pinned binary the same way a session runs it — rule 4 decides every question
    /// about that binary — so asking its version is not the moment it moves.
    /// </summary>
    [Fact]
    public async Task A_pinned_binary_is_probed_with_its_updates_off_and_an_unpinned_one_is_not()
    {
        Directory.CreateDirectory(_home);
        var script = Path.Combine(_home, "says-its-updates.mjs");
        File.WriteAllText(script, """
            if (process.argv[2] === '--version') { console.log('updates:' + (process.env.DISABLE_UPDATES ?? 'on')); process.exit(0); }
            """);
        var bin = Path.Combine(HarnessSettings.ManagedHome(_home, "fake", "1.2.3"), "node_modules", ".bin");
        Directory.CreateDirectory(bin);
        var windows = OperatingSystem.IsWindows();
        var shim = Path.Combine(bin, windows ? "pinned.cmd" : "pinned");
        File.WriteAllText(shim, windows
            ? $"@node \"{script}\" %*\r\n"
            : $"#!/bin/sh\nexec node \"{script}\" \"$@\"\n");
        if (!windows) File.SetUnixFileMode(shim, UnixFileMode.UserRead | UnixFileMode.UserExecute);

        var toolchain = new HarnessToolchain(
            Binary: ["pinned"], VersionArguments: ["--version"],
            PinnedEnvironment: new Dictionary<string, string> { ["DISABLE_UPDATES"] = "1" });
        var pinned = new HarnessSettings(Versions: new Dictionary<string, string> { ["fake"] = "1.2.3" });

        var managed = await HarnessProbe.ProbeAsync("fake", toolchain, command: null, pinned, _home);
        var machine = await HarnessProbe.ProbeAsync(
            "fake", toolchain, command: ["node", script], new HarnessSettings(), _home);

        Assert.Equal("updates:1", managed.Version);
        Assert.Equal("updates:on", machine.Version);
    }
}
