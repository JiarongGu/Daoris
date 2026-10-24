using System.Security.Cryptography;
using System.Text;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// STEWARDROLE1a-i (D209 §6.1, the steward design §10 row 1a-i's <c>HolderHarnessTests</c>): the programs a role's key is handed to are
/// Daoris's choice, not a file's or a variable's. The holder's tab process is Daoris's managed, pinned <c>claude-code</c> by its absolute
/// path, with no shell between; a binary whose hash is not the one its verified install recorded, a pin with no record, or no pin, is
/// refused <see cref="HolderHarness.Refused"/> with nothing started, and never PATH's <c>claude</c> instead; the environment is the one
/// Daoris composes, holding the configuration directory, the updater off, the key and the install's own <c>PATH</c>, and none of
/// Daoris's own variables; a tools file's <c>pwsh</c> and a PATH's <c>claude</c> and <c>powershell</c> are never started; and where a
/// role's tab is a shell, it is the system's own Windows PowerShell.
/// </summary>
/// <remarks>
/// 🔴 No real agent binary runs, and no window or console starts: the managed install is a file of bytes nobody runs, and the panel's
/// factory is <see cref="HolderTerminalTests.Terminals"/>' stand-in, which records each launch and starts nothing.
/// </remarks>
public sealed class HolderHarnessTests : Bridge
{
    private const string Key = "5e17e1a15e17e1a15e17e1a15e17e1a15e17e1a15e17e1a15e17e1a15e17e1a1";

    private const string Pin = "2.1.300";

    private static readonly byte[] Bytes = Encoding.UTF8.GetBytes("MZ-not-really-a-binary");

    private string Managed => HarnessSettings.ManagedHome(Home, HolderTerminal.Harness, Pin);

    private string Binary => Path.Combine(Managed, "bin", "claude.exe");

    /// <summary>A pin, and its maker's channel install with the hash it verified, as <c>ClaudeReleaseDownloads</c> leaves them.</summary>
    private void Pinned(bool recorded = true, byte[]? bytes = null)
    {
        File.WriteAllText(DaorisHome.Entry(Home, HomeEntry.Harnesses), "{\"versions\":{\"claude-code\":\"" + Pin + "\"}}");
        Directory.CreateDirectory(Path.GetDirectoryName(Binary)!);
        File.WriteAllBytes(Binary, bytes ?? Bytes);
        if (recorded) VerifiedBinary.Write(Managed, "bin/claude.exe", Convert.ToHexStringLower(SHA256.HashData(Bytes)));
    }

    /// <summary>A tools file's <c>pwsh</c> and a PATH's <c>powershell</c>, as the panel's factory would offer them: never the holder's.</summary>
    private static readonly TerminalShell ToolsPwsh = new(TerminalShells.Pwsh, @"C:\planted\tools\pwsh.exe", ["-NoLogo"]);

    private static readonly TerminalShell PathPowerShell = new(TerminalShells.WindowsPowerShell, @"C:\planted\path\powershell.exe", ["-NoLogo"]);

    private static HolderOpen Open() => new("h1", Key, [Roles.Steward], DateTimeOffset.Parse("2026-10-24T14:02:00Z"), []);

    [Fact]
    public async Task The_tab_s_process_is_the_managed_binary_by_its_absolute_path_with_no_shell_between()
    {
        Pinned();
        var terminals = new HolderTerminalTests.Terminals(ToolsPwsh, PathPowerShell);
        using var module = new TerminalModule(Bus, terminals);

        var opened = await module.OpenHolderAsync(Home, Open());

        Assert.Null(opened.Why);
        var launch = Assert.Single(terminals.Launched);
        Assert.Equal(Binary, launch.Shell.Path);
        Assert.True(Path.IsPathFullyQualified(launch.Shell.Path));
        Assert.Equal(HolderTerminal.Harness, launch.Shell.Id);
        Assert.Equal(HolderTerminal.SessionArguments, launch.Shell.Arguments);
        Assert.True(launch.Whole);
        Assert.DoesNotContain(launch.Shell.Arguments, each => each is "-NoProfile" or "-Command" or "-c" or "/c");
        Assert.NotEqual(ToolsPwsh.Path, launch.Shell.Path);
        Assert.NotEqual(PathPowerShell.Path, launch.Shell.Path);
        Assert.Equal(Binary, opened.Process!.Image);
    }

    public static TheoryData<string> Refusals => ["no pin", "no record", "other bytes"];

    [Theory]
    [MemberData(nameof(Refusals))]
    public async Task A_harness_that_is_not_Daoris_s_verified_pin_is_refused_with_nothing_started(string which)
    {
        switch (which)
        {
            case "no pin":
                break;
            case "no record":
                Pinned(recorded: false);
                break;
            default:
                Pinned(bytes: Encoding.UTF8.GetBytes("MZ-a-different-binary"));
                break;
        }

        var terminals = new HolderTerminalTests.Terminals(ToolsPwsh, PathPowerShell);
        using var module = new TerminalModule(Bus, terminals);

        var opened = await module.OpenHolderAsync(Home, Open());

        Assert.Equal((HolderHarness.Refused, (string?)null), (opened.Why, opened.Tab));
        Assert.Empty(terminals.Launched);
        Assert.False(Directory.Exists(HolderTerminal.Folder(Home, "h1")));
    }

    /// <summary>The bytes changed between the look and the start: checked immediately before, so nothing starts.</summary>
    [Fact]
    public async Task A_binary_changed_after_the_look_is_refused_at_the_start_itself()
    {
        Pinned();
        var terminals = new HolderTerminalTests.Terminals();
        var looks = 0;
        using var module = new TerminalModule(Bus, terminals)
        {
            Harness = home =>
            {
                if (++looks == 2) File.WriteAllText(Binary, "MZ-changed-meanwhile");
                return HolderHarness.Find(home);
            },
        };

        var opened = await module.OpenHolderAsync(Home, Open());

        Assert.Equal(HolderHarness.Refused, opened.Why);
        Assert.Empty(terminals.Launched);
    }

    [Fact]
    public void The_environment_is_composed_whole_and_holds_none_of_Daoris_s_own()
    {
        var own = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["SystemRoot"] = @"C:\Windows", ["USERPROFILE"] = @"C:\Users\owner", ["TEMP"] = @"C:\Users\owner\AppData\Local\Temp",
            ["PATH"] = @"C:\planted\path;C:\Users\owner\AppData\Roaming\npm", ["NODE_OPTIONS"] = "--require C:\\planted\\hook.js",
            ["DAORIS_PLANTED"] = "1", ["CLAUDE_CONFIG_DIR"] = @"C:\Users\owner\.claude", ["DOTNET_STARTUP_HOOKS"] = @"C:\planted\hook.dll",
        };
        var app = Path.Combine(Home, "install", "app");

        var composed = HolderTerminal.Environment(Home, Key, app, name => own.GetValueOrDefault(name));

        Assert.Equal(HolderTerminal.Config(Home), composed[HolderTerminal.ConfigVariable]);
        Assert.Equal("1", composed["DISABLE_AUTOUPDATER"]);
        Assert.Equal(Key, composed[HolderKey.Variable]);
        Assert.Equal(
            $@"{app};C:\Windows\System32;C:\Windows;C:\Windows\System32\Wbem;C:\Windows\System32\WindowsPowerShell\v1.0", composed["PATH"]);
        Assert.Equal(@"C:\Users\owner", composed["USERPROFILE"]);
        Assert.False(composed.ContainsKey("NODE_OPTIONS"));
        Assert.False(composed.ContainsKey("DOTNET_STARTUP_HOOKS"));
        Assert.False(composed.ContainsKey("DAORIS_PLANTED"));
        Assert.DoesNotContain(composed.Values, value => value.Contains("planted", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(composed.Values, value => value.Contains(@"\npm", StringComparison.OrdinalIgnoreCase));
        Assert.All(composed.Keys, name => Assert.True(
            HolderTerminal.SystemVariables.Contains(name, StringComparer.OrdinalIgnoreCase)
            || name is "PATH" or "DAORIS_HOME" or "CLAUDE_CONFIG_DIR" or "DISABLE_AUTOUPDATER" or HolderKey.Variable,
            $"{name} is not one Daoris composes"));
    }

    [Fact]
    public async Task The_launch_carries_the_composed_environment_and_never_this_process_s()
    {
        Pinned();
        var terminals = new HolderTerminalTests.Terminals(ToolsPwsh, PathPowerShell);
        using var module = new TerminalModule(Bus, terminals) { App = Path.Combine(Home, "install", "app") };

        await module.OpenHolderAsync(Home, Open());

        var launch = Assert.Single(terminals.Launched);
        Assert.True(launch.Whole);
        Assert.Equal(HolderTerminal.Environment(Home, Key, Path.Combine(Home, "install", "app")), launch.Environment);
        Assert.StartsWith(Path.Combine(Home, "install", "app") + ";", launch.Environment["PATH"], StringComparison.Ordinal);
    }

    /// <summary>D209 §6.1: wherever a role's tab is a shell, the system's own Windows PowerShell, never the tools file's nor PATH's.</summary>
    [Fact]
    public void Where_a_role_s_tab_is_a_shell_it_is_the_system_s_Windows_PowerShell()
    {
        var shell = HolderTerminal.SystemShell(name => name == "SystemRoot" ? @"D:\Windows" : null);
        Assert.Equal((TerminalShells.WindowsPowerShell, @"D:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe"), (shell.Id, shell.Path));
        Assert.Equal(["-NoLogo", "-NoProfile"], shell.Arguments);
        Assert.NotEqual(PathPowerShell.Path, shell.Path);
        Assert.NotEqual(ToolsPwsh.Path, shell.Path);
    }
}
