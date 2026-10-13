using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A conversation resolves its harness through the roster's LIVE set (D64): the driver's tick hands
/// the roster the build's adapters plus whatever the plugins declare, and a runner built before a
/// plugin arrived must still find the harness it declared.
/// </summary>
[Trait(Category.Name, Category.Process)]
public sealed class ChatRunnerTests : IDisposable
{
    private readonly string _home = Path.Combine(
        Path.GetTempPath(), "daoris-chat-" + Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        if (Directory.Exists(_home)) Directory.Delete(_home, recursive: true);
    }

    private AdapterSet Declared()
    {
        var folder = Path.Combine(_home, "plugins", "acme.agent");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "plugin.json"),
            """{ "id": "acme.agent", "harnesses": [ { "name": "acme-agent", "command": ["acme"] } ] }""");
        return AdapterSet.Built().WithPlugins(PluginCatalog.Load(_home, AdapterSet.Built().Names));
    }

    /// <summary>
    /// D107: a conversation in a repository is handed what a driven session there is — the person's union, a
    /// read of its kept files, and what it may reach across — with no refusal on its own tree or files.
    /// </summary>
    [Fact]
    public void A_conversations_rules_are_a_driven_sessions_with_its_kept_files_and_what_it_may_reach_across()
    {
        var file = PermissionFile.Empty with { Machine = new RuleLists(["Bash(make:*)"], [], []) };
        var across = new AcrossReach([new("game", "/work/game")], [], [new("personal", "/srv/personal")]);

        var rules = ChatRunner.RulesFor(file, "default", "engine", "/work/engine", "/data/chats/s1", across);

        Assert.Contains("Bash(make:*)", rules.Allow);
        Assert.Contains(PermissionRules.ReadRule("/data/chats/s1"), rules.Allow);
        Assert.Contains("Read(//work/game/**)", rules.Allow);
        Assert.Contains("Edit(//work/game/**)", rules.Deny);
        Assert.Contains("Read(//srv/personal/**)", rules.Deny);
        Assert.DoesNotContain(rules.Deny, rule => rule.Contains("/work/engine", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_harness_declared_after_the_runner_was_built_is_found_through_the_roster_s_live_set()
    {
        var built = AdapterSet.Built();
        var roster = new HarnessRoster(built, Path.Combine(_home, "harnesses.json"));
        using var service = new ServiceClient("http://127.0.0.1:1", null);
        var runner = new ChatRunner(service, built, _home, new SessionProcesses(), harnesses: roster);
        var config = DriverConfig.Load(Path.Combine(_home, "driver.json"));

        // Before the tick handed the roster the plugins: the driver's own refusal, naming what exists.
        var unknown = await Assert.ThrowsAsync<DriverException>(() =>
            runner.StartAsync("engine", "acme-agent", config));
        Assert.Contains("unknown adapter 'acme-agent'", unknown.Message);

        // After: the harness resolves, and the conversation gets as far as asking the service —
        // which is nowhere here, so THAT is the failure, and it is not the adapter's.
        roster.Use(Declared());
        await Assert.ThrowsAnyAsync<HttpRequestException>(() =>
            runner.StartAsync("engine", "acme-agent", config));
    }

    /// <summary>
    /// CHATSERVERS1: a plugin that declares an agent and a server hands the server to a conversation, as the driver loop hands
    /// it to a driven session. The runner read the catalogue reserving the roster's live set, which already holds the plugin's
    /// own agent, so the plugin was refused as one this build already carries and its servers were withheld. Nothing starts:
    /// the harness's program does not exist, so the start fails after its servers are handed, and the tree is no checkout.
    /// </summary>
    [Fact]
    public async Task A_plugin_that_also_declares_an_agent_hands_a_conversation_its_servers()
    {
        var folder = Path.Combine(_home, "plugins", "acme.agent");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "plugin.json"), """
            { "id": "acme.agent",
              "harnesses": [ { "name": "acme-agent", "command": ["acme"] } ],
              "servers": [ { "name": "tickets", "command": ["node", "tickets.mjs"] } ] }
            """);
        var talk = new PipeTalk(Path.Combine(_home, "no-such-harness.exe"));
        var own = new AdapterSet(new Dictionary<string, ISessionAdapter>(StringComparer.OrdinalIgnoreCase) { ["talk"] = talk });
        // The live set, as the driver's tick hands the roster it: this set and the plugin's agent.
        var live = own.WithPlugins(PluginCatalog.Load(_home, own.Names));
        Assert.Equal("acme.agent", live.DeclaredBy("acme-agent"));
        var ledger = new ChatLedger().Register("engine", Path.Combine(_home, "engine"));
        using var client = ledger.Client();
        using var runner = new ChatRunner(
            client, live, _home, new SessionProcesses(Path.Combine(_home, "sessions")),
            harnesses: new HarnessRoster(live, Path.Combine(_home, "harnesses.json")));

        var start = await runner.StartAsync("engine", "talk", DriverConfig.Empty);

        Assert.Null(start.SessionId);
        Assert.Equal(["starting", "failed"], ledger.Moves("c1"));
        Assert.Equal(["tickets"], talk.Handed);
    }

    /// <summary>A conversation's adapter on the pipe door whose program does not exist, keeping the servers it is handed.</summary>
    private sealed class PipeTalk(string program) : ISessionAdapter
    {
        public List<string> Handed { get; } = [];

        public string Name => "talk";

        public bool Interactive => true;

        public System.Diagnostics.ProcessStartInfo Prepare(SessionTarget target, IReadOnlyList<string>? command) =>
            throw new InvalidOperationException("this test drives no session");

        public System.Diagnostics.ProcessStartInfo PrepareChat(ChatTarget target, IReadOnlyList<string>? command) =>
            new(program) { UseShellExecute = false };

        // Read as it is handed: a start that fails removes the file after.
        public void HandServers(System.Diagnostics.ProcessStartInfo info, string configFile)
        {
            using var file = System.Text.Json.JsonDocument.Parse(File.ReadAllText(configFile));
            Handed.AddRange(file.RootElement.GetProperty("mcpServers").EnumerateObject().Select(server => server.Name));
        }
    }
}
