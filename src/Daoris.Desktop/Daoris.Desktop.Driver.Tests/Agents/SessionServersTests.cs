using System.Diagnostics;
using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// LAUNCHCHAIN1: a server a session is handed starts as one node, its package's own script, never through <c>npx</c> or a
/// <c>cmd</c>; the package is resolved once into the toolchain, and the harness's file names the same direct command. An agent npm
/// installed starts the same way, as its node and script, never its <c>.cmd</c> shim.
/// </summary>
public sealed class SessionServersTests : IDisposable
{
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "session-servers", Guid.NewGuid().ToString("N")[..8], "a home");

    public SessionServersTests() => Directory.CreateDirectory(_home);

    public void Dispose() => GitSeed.Discard(Path.GetDirectoryName(_home)!);

    private static readonly AcpMcpServer Knowledge =
        new("daoris-knowledge", "daoris-knowledge.exe", [], new Dictionary<string, string>());

    private static AcpMcpServer Npx(params string[] arguments) => new("browser", "npx", arguments, new Dictionary<string, string>());

    /// <summary>
    /// A package laid in the toolchain as an install leaves it: its manifest naming <paramref name="bin"/> as <paramref name="script"/>,
    /// and the script, whose text is <paramref name="body"/>. The script's whole path.
    /// </summary>
    internal static string Lay(
        string home, string package, string version, string script = "cli.js", string body = "", string? bin = null)
    {
        var folder = Path.Combine([ServerPackages.Folder(home, package, version), "node_modules", .. package.Split('/')]);
        Directory.CreateDirectory(folder);
        var manifest = new Dictionary<string, object>
        {
            ["name"] = package,
            ["version"] = version,
            ["bin"] = new Dictionary<string, string> { [bin ?? NpxCommand.Unscoped(package)] = script },
        };
        File.WriteAllText(Path.Combine(folder, "package.json"), JsonSerializer.Serialize(manifest));
        var path = Path.Combine([folder, .. script.Split('/')]);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, body);
        return path;
    }

    /// <summary>No word of a start is a shell or <c>npx</c>.</summary>
    private static void NoWrapper(string command, IEnumerable<string> arguments)
    {
        foreach (var word in arguments.Prepend(command))
        {
            var name = Path.GetFileNameWithoutExtension(word);
            Assert.False(name is "cmd" or "npx" or "npx-cli", $"`{word}` wraps the server");
            Assert.DoesNotContain("npx", word, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task A_sessions_npx_server_is_handed_as_node_and_its_packages_own_script()
    {
        var script = Lay(_home, "@playwright/mcp", "0.0.82", bin: "playwright-mcp");

        var (handed, notice, drives, _) = await SessionServers.HandNotedAsync(
            _home, [Knowledge, Npx("-y", "@playwright/mcp@0.0.82", "--output-dir", "a folder")], browser: null, log: null,
            CancellationToken.None);

        Assert.Null(notice);
        Assert.False(drives);
        Assert.Equal(Knowledge, handed[0]);
        var browser = handed[1];
        Assert.Equal("browser", browser.Name);
        Assert.True(Path.IsPathFullyQualified(browser.Command));
        Assert.Equal("node", Path.GetFileNameWithoutExtension(browser.Command), ignoreCase: true);
        Assert.Equal([script, "--output-dir", "a folder"], browser.Arguments);
        NoWrapper(browser.Command, browser.Arguments);
    }

    [Fact]
    public async Task A_server_that_drives_Daoris_browser_starts_under_the_keeper_and_another_does_not()
    {
        var script = Lay(_home, "@playwright/mcp", "0.0.82", bin: "playwright-mcp");
        var other = Npx("-y", "@playwright/mcp@0.0.82", "--output-dir", "a folder") with { Name = "other" };

        var (handed, _, drives, _) = await SessionServers.HandNotedAsync(
            _home, [Npx("-y", "@playwright/mcp@0.0.82", "--cdp-endpoint", InAppBrowserServers.Placeholder), other],
            new InAppBrowserServersTests.LazyBrowser("http://127.0.0.1:9"), log: null, CancellationToken.None);

        Assert.True(drives);
        Assert.Equal(
            [ServerKeeper.Install(_home), ServerKeeper.CeilingMb.ToString(), ServerKeeper.EveryMs.ToString(), script, "--cdp-endpoint", "http://127.0.0.1:9"],
            handed[0].Arguments);
        Assert.Equal([script, "--output-dir", "a folder"], handed[1].Arguments);
    }

    [Fact]
    public async Task The_pipe_doors_file_names_the_direct_command_so_the_harness_starts_one_node()
    {
        var script = Lay(_home, "@playwright/mcp", "0.0.82");
        var (handed, _, _, _) = await SessionServers.HandNotedAsync(
            _home, [Npx("-y", "@playwright/mcp@0.0.82")], browser: null, log: null, CancellationToken.None);

        var file = SpawnServers.Write(_home, "s1", handed)!;

        using var written = JsonDocument.Parse(File.ReadAllText(file));
        var server = written.RootElement.GetProperty("mcpServers").GetProperty("browser");
        var command = server.GetProperty("command").GetString()!;
        string[] arguments = [.. server.GetProperty("args").EnumerateArray().Select(each => each.GetString()!)];
        Assert.Equal("node", Path.GetFileNameWithoutExtension(command), ignoreCase: true);
        Assert.Equal([script], arguments);
        NoWrapper(command, arguments);
    }

    [Fact]
    public async Task A_server_withheld_for_want_of_Daoris_browser_resolves_nothing()
    {
        var (handed, notice, drives, drivers) = await SessionServers.HandNotedAsync(
            _home, [Npx("-y", "@playwright/mcp@0.0.82", "--cdp-endpoint", InAppBrowserServers.Placeholder)], browser: null, log: null,
            CancellationToken.None);

        Assert.Empty(handed);
        Assert.NotNull(notice);
        Assert.False(drives);
        Assert.Empty(drivers);
        Assert.False(Directory.Exists(DaorisHome.Entry(_home, HomeEntry.Toolchain)));
    }

    [Fact]
    public async Task A_tag_or_no_version_is_answered_by_the_newest_version_installed_without_asking_npm()
    {
        Lay(_home, "@playwright/mcp", "0.0.9");
        var newest = Lay(_home, "@playwright/mcp", "0.0.82");

        foreach (var written in new[] { "@playwright/mcp@latest", "@playwright/mcp" })
        {
            var (handed, _, _, _) = await SessionServers.HandNotedAsync(
                _home, [Npx("-y", written)], browser: null, log: null, CancellationToken.None);
            Assert.Equal([newest], Assert.Single(handed).Arguments);
        }
    }

    [Theory]
    [InlineData(new[] { "-y", "@playwright/mcp@0.0.82", "--cdp-endpoint", "x" }, "@playwright/mcp", "0.0.82", null, new[] { "--cdp-endpoint", "x" })]
    [InlineData(new[] { "--yes", "server" }, "server", null, null, new string[0])]
    [InlineData(new[] { "-p", "@a/tools@1.0.0", "tool", "--flag" }, "@a/tools", "1.0.0", "tool", new[] { "--flag" })]
    [InlineData(new[] { "--package=@a/tools", "tool" }, "@a/tools", null, "tool", new string[0])]
    [InlineData(new[] { "-y", "--", "server@^2", "-y" }, "server", "^2", null, new[] { "-y" })]
    public void An_npx_command_is_read_as_npm_reads_it(string[] arguments, string package, string? version, string? bin, string[] rest)
    {
        var (read, refusal) = NpxCommand.Read(@"C:\tools\npx.cmd", arguments);

        Assert.Null(refusal);
        Assert.Equal(new NpxCommand(package, version, bin, rest), read! with { Arguments = rest });
        Assert.Equal(rest, read.Arguments);
    }

    [Theory]
    [InlineData("-c", "server")]
    [InlineData("--no", "server")]
    [InlineData("-y")]
    [InlineData("../climbs")]
    [InlineData("server@1 2")]
    public void An_npx_command_this_cannot_read_is_refused_rather_than_guessed(params string[] arguments)
    {
        var (read, refusal) = NpxCommand.Read("npx", arguments);

        Assert.Null(read);
        Assert.NotNull(refusal);
    }

    [Fact]
    public void A_command_that_is_no_npx_is_not_read_as_one()
    {
        Assert.Equal((null, null), NpxCommand.Read("node", ["-y", "server"]));
        Assert.Equal((null, null), NpxCommand.Read(@"C:\tools\npxtra.exe", ["server"]));
    }

    [Theory]
    [InlineData("""{ "bin": "cli.js" }""", "@a/server", null, "cli.js")]
    [InlineData("""{ "bin": { "one": "./dist/one.js" } }""", "@a/server", null, "dist/one.js")]
    [InlineData("""{ "bin": { "a": "cli.js", "b": "cli.js" } }""", "@a/server", null, "cli.js")]
    [InlineData("""{ "bin": { "other": "o.js", "server": "s.js" } }""", "@a/server", null, "s.js")]
    [InlineData("""{ "bin": { "other": "o.js", "named": "n.js" } }""", "@a/server", null, null)]
    [InlineData("""{ "bin": { "other": "o.js", "named": "n.js" } }""", "@a/server", "named", "n.js")]
    [InlineData("""{ "bin": { "server": "../out.js" } }""", "@a/server", null, null)]
    [InlineData("""{ "name": "@a/server" }""", "@a/server", null, null)]
    [InlineData("not json", "@a/server", null, null)]
    public void A_packages_script_is_picked_as_npm_picks_it(string manifest, string package, string? bin, string? script)
    {
        Assert.Equal(script, PackageBin.Of(manifest, package, bin));
    }

    /// <summary>
    /// The protocol door's adapter, pinned by npm into the toolchain: its start is the node and the script the shim would have run, with
    /// its arguments as they were, so one that <c>cmd</c> would read again starts as written.
    /// </summary>
    [Fact]
    public void A_pinned_adapter_starts_as_its_node_and_script_never_its_shim()
    {
        if (!OperatingSystem.IsWindows()) return;
        var adapter = new ClaudeAcpAdapter();
        var toolchain = adapter.Toolchain!;
        var prefix = HarnessSettings.ManagedHome(_home, adapter.Name, "0.91.0");
        var package = Path.Combine([prefix, "node_modules", .. toolchain.Package!.Split('/')]);
        Directory.CreateDirectory(Path.Combine(package, "dist"));
        File.WriteAllText(Path.Combine(package, "package.json"), """{ "bin": { "claude-agent-acp": "dist/index.js" } }""");
        File.WriteAllText(Path.Combine(package, "dist", "index.js"), "");
        var shims = Path.Combine(prefix, "node_modules", ".bin");
        Directory.CreateDirectory(shims);
        File.WriteAllText(Path.Combine(shims, "claude-agent-acp.cmd"), "@echo off\r\n");
        var shim = HarnessSettings.ManagedBinary(_home, adapter.Name, "0.91.0", toolchain.Binary)!;
        Assert.EndsWith(".cmd", shim, StringComparison.OrdinalIgnoreCase);

        var info = new ProcessStartInfo("claude-agent-acp") { UseShellExecute = false, CreateNoWindow = true };
        info.ArgumentList.Add("a \"quoted\" & piped word");
        Shell.Equip(info, toolchain, profileHome: null, binary: shim);

        Assert.Equal("node", Path.GetFileNameWithoutExtension(info.FileName), ignoreCase: true);
        Assert.True(Path.IsPathFullyQualified(info.FileName));
        Assert.Equal([Path.Combine(package, "dist", "index.js"), "a \"quoted\" & piped word"], info.ArgumentList);
    }
}
