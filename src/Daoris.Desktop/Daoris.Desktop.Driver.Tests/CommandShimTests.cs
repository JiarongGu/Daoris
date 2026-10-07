using System.Diagnostics;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// 🔴 <b>An agent npm put on PATH is found, and started</b> (USE1f). A global npm install of the Claude
/// Code ACP adapter puts <c>claude-agent-acp.cmd</c> in the Node folder, with an extensionless POSIX
/// script beside it. The CLI found it; the desktop held every driven start with "not installed",
/// because it started the bare name, and Windows starts only an <c>.exe</c> by a bare name (the
/// extensionless script it would find first is no program at all). The probe and both doors' spawns
/// now resolve the name the way installers already did: to what Windows can START, by PATHEXT.
/// </summary>
/// <remarks>
/// One resolver, <see cref="CommandPresence.Resolve"/>, the plugin door's. Windows only, because the
/// failure is Windows': elsewhere a bare name starts as it always did.
/// </remarks>
[Collection(ProcessPath.Name)]
[Trait(Category.Name, Category.Process)]
public sealed class CommandShimTests : IDisposable
{
    // Scratch in the repository's own gitignored `_fixtures/`, never OS temp.
    private readonly string _folder = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "command-shim", Guid.NewGuid().ToString("N")[..8]);

    private static readonly HarnessToolchain Plain = new(["x"], ["--version"]);

    public CommandShimTests() => Directory.CreateDirectory(_folder);

    public void Dispose()
    {
        if (Directory.Exists(_folder)) Directory.Delete(_folder, recursive: true);
    }

    [Fact]
    public void A_name_on_path_only_as_a_cmd_shim_resolves_to_the_shim_not_the_script_beside_it()
    {
        if (!OperatingSystem.IsWindows()) return;
        NpmShim("use1f-agent");

        // Case aside: the extension is spelled as PATHEXT spells it, and Windows' names ignore case.
        Assert.Equal(
            Path.Combine(_folder, "use1f-agent.cmd"),
            CommandPresence.Resolve("use1f-agent", _folder, startable: true), ignoreCase: true);
    }

    [Fact]
    public void An_exe_beside_a_cmd_shim_wins_in_PATHEXT_order()
    {
        if (!OperatingSystem.IsWindows()) return;
        NpmShim("use1f-agent");
        File.WriteAllText(Path.Combine(_folder, "use1f-agent.exe"), "");

        Assert.Equal(
            Path.Combine(_folder, "use1f-agent.exe"),
            CommandPresence.Resolve("use1f-agent", _folder, startable: true), ignoreCase: true);
    }

    [Fact]
    public void A_name_that_is_nowhere_resolves_to_nothing_and_a_spawn_keeps_its_name()
    {
        if (!OperatingSystem.IsWindows()) return;
        var info = new ProcessStartInfo("use1f-nowhere");
        info.Environment["PATH"] = _folder;

        HarnessProbe.Apply(info, Plain, profileHome: null);

        Assert.Null(CommandPresence.Resolve("use1f-nowhere", _folder, startable: true));
        Assert.Equal("use1f-nowhere", info.FileName);
    }

    /// <summary>
    /// Both doors, as the driver prepares them: the adapter's process, then the one line both take,
    /// where the pin is applied too — resolved against the spawn's own PATH.
    /// </summary>
    [Fact]
    public void Both_doors_spawn_an_agent_on_path_as_its_cmd_shim()
    {
        if (!OperatingSystem.IsWindows()) return;
        NpmShim("claude-agent-acp");
        NpmShim("claude");
        var acp = AdapterSet.Built().Resolve("claude-code-acp");
        var pipe = AdapterSet.Built().Resolve("claude-code");

        var driven = acp.Prepare(Target(), command: null);
        var chat = pipe.PrepareChat(new ChatTarget("Game", _folder, "http://localhost:5177"), command: null);
        foreach (var (info, adapter) in new[] { (driven, acp), (chat, pipe) })
        {
            info.Environment["PATH"] = _folder;
            HarnessProbe.Apply(info, adapter.Toolchain!, profileHome: null);
        }

        Assert.Equal(Path.Combine(_folder, "claude-agent-acp.cmd"), driven.FileName, ignoreCase: true);
        Assert.Equal(Path.Combine(_folder, "claude.cmd"), chat.FileName, ignoreCase: true);
    }

    /// <summary>
    /// 🔴 A shim is run by <c>cmd.exe</c>, which parses its arguments again and stops at a line break.
    /// The pipe door's prompt is an argument with line breaks in it, so on a shim it is refused in a
    /// sentence rather than cut short without anyone knowing.
    /// </summary>
    [Fact]
    public void A_prompt_a_shim_s_shell_would_reinterpret_is_refused_rather_than_cut_short()
    {
        if (!OperatingSystem.IsWindows()) return;
        NpmShim("claude");
        var pipe = AdapterSet.Built().Resolve("claude-code");
        var info = pipe.Prepare(Target(), command: null);
        info.Environment["PATH"] = _folder;

        var error = Assert.Throws<DriverException>(() => HarnessProbe.Apply(info, pipe.Toolchain!, profileHome: null));

        Assert.Contains("cannot be passed to a Windows command shim safely", error.Message);
        Assert.Contains("claude.cmd", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\n", error.Message);
    }

    /// <summary>
    /// The probe decides "present", and a start is held on its answer. A shim on PATH answers its
    /// version; a name that is nowhere is still absent, in the probe's own sentence.
    /// </summary>
    [Fact]
    public async Task An_agent_on_path_as_a_cmd_shim_probes_as_present_and_one_that_is_nowhere_as_absent()
    {
        if (!OperatingSystem.IsWindows()) return;
        File.WriteAllText(Path.Combine(_folder, "use1f-probe.cmd"), "@echo use1f-probe 0.84.0\r\n");
        File.WriteAllText(Path.Combine(_folder, "use1f-probe"), "#!/bin/sh\necho not this one\n");
        var saved = Environment.GetEnvironmentVariable("PATH");
        // Prepended, so every other test's tools stay where they were.
        Environment.SetEnvironmentVariable("PATH", _folder + Path.PathSeparator + saved);
        try
        {
            var present = await HarnessProbe.ProbeAsync(
                "use1f-probe", new HarnessToolchain(["use1f-probe"], ["--version"]), command: null,
                new HarnessSettings(), _folder);
            var absent = await HarnessProbe.ProbeAsync(
                "use1f-nowhere", new HarnessToolchain(["use1f-nowhere"], ["--version"]), command: null,
                new HarnessSettings(), _folder);

            Assert.True(present.Present, present.Problem);
            Assert.Equal("use1f-probe 0.84.0", present.Version);
            Assert.False(absent.Present);
            Assert.Contains("PATH", absent.Problem);
        }
        finally
        {
            Environment.SetEnvironmentVariable("PATH", saved);
        }
    }

    /// <summary>What npm lays down for a global bin on Windows: a <c>.cmd</c>, and a POSIX script beside it.</summary>
    private void NpmShim(string name)
    {
        File.WriteAllText(Path.Combine(_folder, name + ".cmd"), "@echo shim %*\r\n");
        File.WriteAllText(Path.Combine(_folder, name), "#!/bin/sh\necho shim \"$@\"\n");
    }

    private SessionTarget Target() => new(
        QuestId: "abc123",
        Title: "Fix the flaky gate",
        Body: "It fails one run in five;\nthe log is attached.",
        Asker: "Platform",
        Repository: "Game",
        Root: _folder,
        ServiceUrl: "http://localhost:5177");
}
