using System.Diagnostics;
using System.Text;
using System.Text.Json;

using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A session's MCP client against the real <c>daoris plugin tools</c>, started as its harness starts the entry it is handed: the built
/// program lists the Azure DevOps example's <c>status</c> at the session's place, and a call runs it in the plugin's own process, its
/// words coming back as the tool's text. Switched off at that place, the program lists nothing and refuses the call.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Needs, Needed.Process)]
public sealed class PluginToolsProcessTests : IDisposable
{
    private const string Plugin = "daoris-azure-devops";

    private readonly string _home = Path.Combine(TestHomes.Root, "daoris-toolsproc-" + Guid.NewGuid().ToString("N")[..8]);

    public PluginToolsProcessTests()
    {
        var from = Path.Combine(WorkspaceRoot.Folder, "examples", "plugins", Plugin);
        var to = DaorisHome.Entry(_home, HomeEntry.Plugins, Plugin);
        foreach (var file in Directory.GetFiles(from))
        {
            Directory.CreateDirectory(to);
            File.Copy(file, Path.Combine(to, Path.GetFileName(file)));
        }
    }

    public void Dispose()
    {
        for (var attempt = 0; Directory.Exists(_home); attempt++)
        {
            try
            {
                Directory.Delete(_home, recursive: true);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException && attempt < 20)
            {
                Thread.Sleep(100);
            }
        }
    }

    /// <summary>The entry a session is handed, started as its harness would: the host's build in this program's place.</summary>
    private ServerEntry Entry() =>
        PluginToolsHand.Entry(_home, new PluginToolsAt("s-tools", new PluginPlace("engine", "games"), "en"), program: "dotnet")
        ?? throw new InvalidOperationException("no entry was handed");

    private async Task<IReadOnlyList<JsonElement>> SessionAsync(ServerEntry entry, params string[] requests)
    {
        var info = new ProcessStartInfo(entry.Command)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            StandardInputEncoding = new UTF8Encoding(false),
            WorkingDirectory = _home,
        };
        info.ArgumentList.Add(DriverHostTests.HostDll());
        foreach (var word in entry.Arguments) info.ArgumentList.Add(word);
        foreach (var name in info.Environment.Keys.Where(key => key.StartsWith("DAORIS_", StringComparison.OrdinalIgnoreCase)).ToList()) info.Environment.Remove(name);
        foreach (var (name, value) in entry.Environment) info.Environment[name] = value;

        using var process = Process.Start(info)!;
        var error = process.StandardError.ReadToEndAsync();
        var answers = new List<JsonElement>();
        using var bound = new CancellationTokenSource(TimeSpan.FromSeconds(90));
        foreach (var request in requests)
        {
            await process.StandardInput.WriteLineAsync(request);
            await process.StandardInput.FlushAsync();
            if (!request.Contains("\"id\"", StringComparison.Ordinal)) continue;
            var line = await process.StandardOutput.ReadLineAsync(bound.Token)
                ?? throw new InvalidOperationException($"the server ended: {await error}");
            answers.Add(JsonDocument.Parse(line).RootElement.Clone());
        }

        process.StandardInput.Close();
        await process.WaitForExitAsync(bound.Token);
        Assert.Equal(0, process.ExitCode);
        return answers;
    }

    private const string Open = """{ "jsonrpc": "2.0", "id": 1, "method": "initialize", "params": { "protocolVersion": "2025-06-18", "capabilities": {}, "clientInfo": { "name": "stand-in", "version": "0" } } }""";

    private const string Opened = """{ "jsonrpc": "2.0", "method": "notifications/initialized" }""";

    [Fact]
    public async Task A_session_s_client_lists_the_plugin_s_tool_and_its_call_runs_in_the_plugin_s_own_process()
    {
        var answers = await SessionAsync(
            Entry(), Open, Opened,
            """{ "jsonrpc": "2.0", "id": 2, "method": "tools/list" }""",
            """{ "jsonrpc": "2.0", "id": 3, "method": "tools/call", "params": { "name": "daoris-azure-devops_status", "arguments": {} } }""");

        Assert.Equal(PluginToolsServer.Name, answers[0].GetProperty("result").GetProperty("serverInfo").GetProperty("name").GetString());
        var names = answers[1].GetProperty("result").GetProperty("tools").EnumerateArray().Select(tool => tool.GetProperty("name").GetString()).ToList();
        Assert.Contains("daoris-azure-devops_status", names);
        Assert.DoesNotContain("daoris-azure-devops_login", names);
        var result = answers[2].GetProperty("result");
        Assert.False(result.GetProperty("isError").GetBoolean());
        Assert.Contains("No token is kept", result.GetProperty("content")[0].GetProperty("text").GetString());
    }

    [Fact]
    public async Task Switched_off_at_the_session_s_repository_the_program_lists_nothing_and_refuses_the_call()
    {
        // The entry is made while the tools are on, then the person switches them off before the session calls.
        var entry = Entry();
        PluginSwitches.Set(_home, Plugin, PluginSwitches.Tools, "repository:engine", on: false);

        var answers = await SessionAsync(
            entry, Open, Opened,
            """{ "jsonrpc": "2.0", "id": 2, "method": "tools/list" }""",
            """{ "jsonrpc": "2.0", "id": 3, "method": "tools/call", "params": { "name": "daoris-azure-devops_status", "arguments": {} } }""");

        Assert.Empty(answers[1].GetProperty("result").GetProperty("tools").EnumerateArray());
        Assert.Equal(-32602, answers[2].GetProperty("error").GetProperty("code").GetInt32());
    }
}
