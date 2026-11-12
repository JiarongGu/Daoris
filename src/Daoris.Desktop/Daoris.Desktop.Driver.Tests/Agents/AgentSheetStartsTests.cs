using System.Diagnostics;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The agent sheet starts no process it has read before: <c>daoris agent list</c> and the page's roster answer from what is read,
/// the binary's version kept under the home with the file it is read of (<c>reads.json</c>) and asked again only when that
/// file changes, and an account's sign-in asked only at a press. Every start is counted through the start gate
/// (<see cref="ProcessStarts.Within"/>) and made for real, over a stand-in <c>claude.cmd</c> in the managed layout.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Needs, Needed.Process)]
[Trait(Category.Needs, Needed.Git)]
public sealed class AgentSheetStartsTests : IDisposable
{
    private const string Version = "--version";

    private readonly string _root = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "agent-sheet-starts", Guid.NewGuid().ToString("N")[..8]);

    public AgentSheetStartsTests()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Settings)!);
        File.WriteAllText(Settings, """
            {"versions":{"claude-code":"1.2.3","claude-code-acp":"0.0.0-none","codex":"0.0.0-none","codex-acp":"0.0.0-none","dsh":"0.0.0-none"}}
            """);
        var account = HarnessSettings.ProfileHome(Home, "claude-code", "account-1");
        Directory.CreateDirectory(account);
        File.WriteAllText(Path.Combine(account, "who.json"), """{"loggedIn":true,"email":"someone@example.invalid"}""");
        Stand("2.1.0");
    }

    public void Dispose() => GitSeed.Discard(_root);

    private string Home => Path.Combine(_root, "daoris");

    private string Settings => DaorisHome.Entry(Home, HomeEntry.Harnesses);

    private string Binary => Path.Combine(DaorisHome.Entry(Home, HomeEntry.Toolchain), "claude-code", "1.2.3", "node_modules", ".bin", "claude.cmd");

    /// <summary>The stand-in, answering <paramref name="version"/> and each account's sign-in from its home's <c>who.json</c>.</summary>
    private void Stand(string version)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Binary)!);
        File.WriteAllText(Binary,
            "@echo off\r\nif \"%~1\"==\"--version\" (\r\n  echo " + version + " ^(Claude Code^)\r\n  exit /b 0\r\n)\r\n"
            + "if exist \"%CLAUDE_CONFIG_DIR%\\who.json\" (\r\n  type \"%CLAUDE_CONFIG_DIR%\\who.json\"\r\n) else (\r\n  echo {\"loggedIn\": false}\r\n)\r\n");
    }

    /// <summary>What <paramref name="read"/> answers, and each start it makes, by its file's name and its arguments.</summary>
    private static async Task<(T Said, List<string> Starts)> Counted<T>(Func<Task<T>> read)
    {
        var starts = new List<string>();
        T said;
        using (ProcessStarts.Within(info =>
        {
            lock (starts) starts.Add(string.Join(' ', [Path.GetFileName(info.FileName), .. info.ArgumentList]));
            return Process.Start(info);
        }))
        {
            said = await read();
        }

        return (said, starts);
    }

    private async Task<string> ListAsync(params string[] words)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var world = new AgentsWorld
        {
            Instance = new InstanceWorld
            {
                Home = Home,
                Environment = variable => variable == DaorisHome.Variable ? Home : null,
                Out = output,
                Error = error,
                In = new StringReader(""),
            },
            Folder = _root,
            UserHome = Path.Combine(_root, "user"),
        };
        var exit = await AgentsCommand.RunAsync(["list", .. words], world, CancellationToken.None);
        Assert.True(exit == 0, $"exit {exit}: {error}");
        return output.ToString();
    }

    /// <summary>The page's roster as a window that has just started reads it: a roster of its own over the home.</summary>
    private async Task<string?> PageAsync(HarnessRoster? roster = null)
    {
        var reports = await (roster ?? new HarnessRoster(ProviderRegistry.Built(), Settings)).RosterAsync(DriverConfig.Empty);
        return reports.Single(report => report.Adapter == "claude-code").Version;
    }

    [Fact]
    public async Task A_warm_list_starts_no_process_and_a_changed_binary_is_read_once()
    {
        if (!OperatingSystem.IsWindows()) return;

        // Cold: the binary is asked its version, and no account its sign-in, which only a press asks.
        var cold = await Counted(() => ListAsync());
        Assert.Equal([$"claude.cmd {Version}"], cold.Starts);
        Assert.Contains("2.1.0 (Claude Code)", cold.Said, StringComparison.Ordinal);

        var warm = await Counted(() => ListAsync());
        Assert.Empty(warm.Starts);
        Assert.Contains("2.1.0 (Claude Code)", warm.Said, StringComparison.Ordinal);

        // The same size, written again: its time is what moved.
        Stand("2.2.0");
        var changed = await Counted(() => ListAsync());
        Assert.Equal([$"claude.cmd {Version}"], changed.Starts);
        Assert.Contains("2.2.0 (Claude Code)", changed.Said, StringComparison.Ordinal);
        Assert.Empty((await Counted(() => ListAsync())).Starts);

        // The terminal's press asks the binary and each account anew; what it read is then said with no start.
        var again = await Counted(() => ListAsync("--again"));
        Assert.Equal(2, again.Starts.Count);
        Assert.Contains($"claude.cmd {Version}", again.Starts);
        var after = await Counted(() => ListAsync());
        Assert.Empty(after.Starts);
        Assert.Matches(@"account-1\s+in\b", after.Said);
    }

    [Fact]
    public async Task A_warm_page_read_starts_no_process_after_a_restart_and_a_changed_binary_is_read_once()
    {
        if (!OperatingSystem.IsWindows()) return;

        // What the terminal read, the window reads: one store of the binary's readings.
        Assert.Equal([$"claude.cmd {Version}"], (await Counted(() => ListAsync())).Starts);
        var first = await Counted(() => PageAsync());
        Assert.Empty(first.Starts);
        Assert.Equal("2.1.0 (Claude Code)", first.Said);

        var running = new HarnessRoster(ProviderRegistry.Built(), Settings);
        Assert.Empty((await Counted(() => PageAsync(running))).Starts);

        // Changed under a running window: its next read asks once, and every read after, here or after a restart, none.
        Stand("2.2.0");
        var changed = await Counted(() => PageAsync(running));
        Assert.Equal([$"claude.cmd {Version}"], changed.Starts);
        Assert.Equal("2.2.0 (Claude Code)", changed.Said);
        Assert.Empty((await Counted(() => PageAsync(running))).Starts);
        Assert.Empty((await Counted(() => PageAsync())).Starts);
        Assert.Empty((await Counted(() => ListAsync())).Starts);
    }
}
