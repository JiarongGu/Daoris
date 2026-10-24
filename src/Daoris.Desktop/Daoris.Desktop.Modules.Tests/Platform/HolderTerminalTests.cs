using Daoris.Driver;
using Shenora.Core.Events;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// SUPERVISOR1a-ii (D208 point 2; the supervisor design §2.2 and §11 row 1a-ii; the owner's direction of 2026-10-24): the holder's
/// terminal is a tab of Daoris's own terminal panel, never a console window of the system's. The panel opens the owner's shell with no
/// profile, in the role's emptied folder outside every repository, the key in that one shell's environment alone; its first lines say what
/// it is and how to start the session, and none holds the key; the page is told the tab; and nothing opens where no PowerShell is here.
/// </summary>
/// <remarks>
/// 🔴 A stand-in terminal factory throughout: no case starts a shell, a console or a window (the owner's direction, after a Process case
/// opened a console window that took the keyboard). The panel's real pseudo-console is <c>TerminalModuleTests</c>' own.
/// </remarks>
public sealed class HolderTerminalTests : Bridge
{
    private const string Sentinel = "5e17e1a15e17e1a15e17e1a15e17e1a15e17e1a15e17e1a15e17e1a15e17e1a1";

    private sealed class Shell : ITerminal
    {
        public void Write(string data)
        {
        }

        public void Resize(int columns, int rows)
        {
        }

        public void Dispose()
        {
        }
    }

    /// <summary>The panel's factory as a stand-in: it records each launch and starts nothing.</summary>
    private sealed class Terminals(params TerminalShell[] shells) : ITerminalFactory
    {
        public List<TerminalLaunch> Launched { get; } = [];

        public IReadOnlyList<TerminalShell> Shells() => shells;

        public ITerminal Start(TerminalLaunch launch, Action<string> output, Action<int> exited)
        {
            lock (Launched) Launched.Add(launch);
            return new Shell();
        }
    }

    private static readonly TerminalShell Pwsh = new(TerminalShells.Pwsh, @"C:\Program Files\PowerShell\7\pwsh.exe", ["-NoLogo"]);
    private static readonly System.Text.Json.JsonSerializerOptions Relaxed = new() { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    private static readonly TerminalShell Cmd = new(TerminalShells.Cmd, @"C:\Windows\System32\cmd.exe", []);

    [Fact]
    public async Task The_appointment_opens_a_tab_of_the_panel_with_no_profile_in_its_emptied_folder_and_the_key_in_its_environment()
    {
        var folder = HolderTerminal.Folder(Home, Roles.Supervisor);
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, ".mcp.json"), """{"mcpServers":{"planted":{"command":"node"}}}""");
        File.WriteAllText(Path.Combine(folder, "CLAUDE.md"), "planted");
        var said = new List<EventMessage>();
        using var output = Bus.Subscribe("DAORIS", "TERMINAL_OUTPUT", message => { lock (said) said.Add(message); return Task.CompletedTask; });
        using var opened = Bus.Subscribe("DAORIS", "TERMINAL_OPENED", message => { lock (said) said.Add(message); return Task.CompletedTask; });
        var terminals = new Terminals(Cmd, Pwsh);
        using var module = new TerminalModule(Bus, terminals);

        Assert.True(await module.OpenHolderAsync(Home, Roles.Supervisor, Sentinel, DateTimeOffset.Parse("2026-10-24T14:02:00Z")));

        var launch = Assert.Single(terminals.Launched);
        Assert.Equal(TerminalShells.Pwsh, launch.Shell.Id);
        Assert.Equal(["-NoLogo", "-NoProfile"], launch.Shell.Arguments);
        Assert.Equal(folder, launch.Directory);
        Assert.Empty(Directory.EnumerateFileSystemEntries(folder));
        Assert.StartsWith(DaorisHome.Entry(Home, HomeEntry.RoleRooms, Roles.Supervisor), folder, StringComparison.Ordinal);
        Assert.Equal(Sentinel, launch.Environment[HolderKey.Variable]);

        // The tab's first lines, and the page told of the tab: none holds the key.
        string Told()
        {
            lock (said) return string.Join('\n', said.Select(each => System.Text.Json.JsonSerializer.Serialize(each.Payload, Relaxed)));
        }

        // The output batch and the tab's word arrive apart: both, before anything is read.
        await UntilAsync(() => Told().Contains("start only the supervisor's session here", StringComparison.Ordinal)
                               && Told().Contains(HolderTerminal.Title, StringComparison.Ordinal));
        var told = Told();
        Assert.DoesNotContain(Sentinel, told, StringComparison.Ordinal);
        Assert.Contains("start only the supervisor's session here", told, StringComparison.Ordinal);
        Assert.Contains("never print the environment", told, StringComparison.Ordinal);
        Assert.Contains("--setting-sources user --strict-mcp-config", told, StringComparison.Ordinal);
        Assert.Contains(HolderTerminal.Title, told, StringComparison.Ordinal);
    }

    [Fact]
    public async Task No_PowerShell_here_opens_nothing()
    {
        var terminals = new Terminals(Cmd);
        using var module = new TerminalModule(Bus, terminals);
        Assert.False(await module.OpenHolderAsync(Home, Roles.Supervisor, Sentinel, DateTimeOffset.UtcNow));
        Assert.Empty(terminals.Launched);
    }

    /// <summary>The panel's environment block never inherits the key: only the holder's own launch adds it.</summary>
    [Fact]
    public void A_tab_of_the_panel_never_inherits_the_key()
    {
        var inherited = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { [HolderKey.Variable] = Sentinel, ["PATH"] = @"C:\x" };
        Assert.DoesNotContain(Sentinel, Block(new Dictionary<string, string>(), inherited), StringComparison.Ordinal);
        Assert.Contains($"{HolderKey.Variable}={Sentinel}",
            Block(new Dictionary<string, string> { [HolderKey.Variable] = Sentinel }, new Dictionary<string, string>()), StringComparison.Ordinal);
    }

    private static string Block(IReadOnlyDictionary<string, string> added, IReadOnlyDictionary<string, string> inherited) =>
        (string)typeof(PseudoConsoleTerminals).Assembly.GetType("Daoris.Driver.PseudoConsole")!
            .GetMethod("EnvironmentBlock", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!
            .Invoke(null, [added, inherited])!;

    private static async Task UntilAsync(Func<bool> condition)
    {
        var until = DateTime.UtcNow + Poll.Patience;
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < until, "the panel told nothing");
            await Task.Delay(10);
        }
    }
}
