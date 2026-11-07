using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A pass that cannot start says why. A request naming <c>claude,codex</c> (the agents' common names, which are no
/// adapter's id on this machine) ends <c>no-agent</c> with each pass <c>unavailable (not-started)</c>; the pass record keeps
/// the walk's code and its sentence, the machine log writes it once, and <c>brainstorm show</c> reads it.
/// </summary>
/// <remarks>Nothing here starts a process: the real turn runs as far as its walk, which finds no adapter of the name.</remarks>
public sealed class BrainstormStartTests : IDisposable
{
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "brainstormstart1-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly Dictionary<string, string> _variables = new(StringComparer.Ordinal);

    public BrainstormStartTests() => Directory.CreateDirectory(_home);

    public void Dispose() => GitSeed.Discard(_home);

    private string? Variable(string name) => _variables.GetValueOrDefault(name);

    /// <summary>A connector the driver finds by a file look, so the turn reaches its walk; nothing starts it.</summary>
    private void OwnConnector()
    {
        var host = Path.Combine(_home, "knowledge-host");
        File.WriteAllText(host, "");
        _variables[KnowledgeConnector.PathVariable] = host;
        _variables[DaorisHome.Variable] = _home;
    }

    private async Task<(BrainstormFiles Files, string Id)> AskedAsync(params string[] agents)
    {
        OwnConnector();
        var driver = new Daoris.Driver.Driver(
            new ServiceClient("http://127.0.0.1:9", null), DriverConfig.Empty with { Actors = StaffedRoles.On("stub"), Cap = 3 }, ProviderRegistry.Built(), _home)
        {
            Variable = Variable,
        };
        var files = new BrainstormFiles(_home);
        var id = files.WriteRequest("Why does the daily report drop its last day?", null, agents, DateTimeOffset.UtcNow);
        var snapshot = new Snapshot([], [], []);
        await driver.BrainstormsAsync(snapshot, 0, [], CancellationToken.None);
        files.WriteSent(id, files.ReadState(id)!.Packet!, DateTimeOffset.UtcNow);
        await driver.BrainstormsAsync(snapshot, 0, [], CancellationToken.None);
        await driver.Running.SettledAsync().WaitAsync(Poll.Patience);
        return (files, id);
    }

    /// <summary>
    /// 🔴 The install's request, <c>--agents claude,codex</c>: each pass is <c>unavailable</c> and its record says why, in the walk's own
    /// sentence, which names the adapters this machine has and no path or account; and <c>brainstorm show</c> reads it.
    /// </summary>
    [Fact]
    public async Task A_pass_naming_no_adapter_of_this_machine_records_why_and_show_says_it()
    {
        var (files, id) = await AskedAsync("claude", "codex");

        Assert.Equal(BrainstormStates.NoAgent, files.ReadState(id)!.Code);
        foreach (var (pass, agent) in new[] { ("g1", "claude"), ("g2", "codex") })
        {
            var record = JsonNode.Parse(File.ReadAllText(files.PathOf(id, "passes", $"{pass}.json")))!;
            Assert.Equal("unavailable", record["state"]!.GetValue<string>());
            Assert.Equal(["not-started"], record["codes"]!.AsArray().Select(code => code!.GetValue<string>()));
            var why = record["why"]!.GetValue<string>();
            Assert.Contains($"unknown adapter '{agent}'", why);
            Assert.Contains("claude-code", why);
            Assert.DoesNotContain(_home, why);
        }

        var shown = new StringWriter();
        Assert.Equal(0, BrainstormCommand.Run(["show", id], _home, shown, DateTimeOffset.UtcNow));
        var text = shown.ToString();
        Assert.Contains("`claude`", text);
        Assert.Contains("not-started", text);
        Assert.Contains("unknown adapter 'claude'", text);
        Assert.Contains("unknown adapter 'codex'", text);
    }

    /// <summary>A record without a reason reads as having none, and show says the code alone.</summary>
    [Fact]
    public async Task A_pass_record_without_a_reason_reads_as_before()
    {
        var (files, id) = await AskedAsync("claude");
        var path = files.PathOf(id, "passes", "g1.json");
        var record = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        record.Remove("why");
        File.WriteAllText(path, record.ToJsonString());

        Assert.Null(files.ReadPasses(id).Single().Why);
        var shown = new StringWriter();
        Assert.Equal(0, BrainstormCommand.Run(["show", id], _home, shown, DateTimeOffset.UtcNow));
        Assert.Contains("not-started", shown.ToString());
    }
}
