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

    /// <summary>
    /// CHATFLOW1c (the chat-flow design §4.3, §4.4): a conversation that grew its tree binds its run, <c>chat-&lt;record&gt;</c>, beside the
    /// process it is told: under a steps version its first Daoris line names the version and who chose it, and the landing it is told is
    /// planned with that version's process, the one its press lands by. Under Current nothing is said. It is told no checks either way,
    /// since nothing enforces them on its tree before CHATFLOW1h.
    /// </summary>
    [Fact]
    public void A_conversation_that_grew_its_tree_binds_its_run_beside_the_process_it_is_told_and_is_told_no_checks()
    {
        WorkflowStore.Add(_home, new WorkflowVersionAdded("chat-steps", "A conversation's steps", "terminal", "2026-10-09T09:00:00Z",
            System.Text.Json.Nodes.JsonNode.Parse("""
                [{"id":"work","kind":"work","checks":"light"},
                 {"id":"landing","kind":"landing","form":"branch","accept":"you","pattern":"chat/{session}-{slug}","plugin":"none"}]
                """)), kept: []);
        var config = DriverConfig.Parse("""{"workflows":{"engine":{"default":"chat-steps"}},"sessionChecks":{"engine":"light"}}""");
        var events = new SessionEvents(DaorisHome.Entry(_home, HomeEntry.Sessions));
        using var service = new ServiceClient("http://stand-in", null, new HttpClient(new Unanswered()));
        var binds = new ChatRunBinds(_home, service, events);

        var process = binds.Opened("c1a2b3c4", "engine", "aurora", config);

        Assert.Equal(("chat-steps", 1), (WorkflowRunBindings.Read(_home, "chat-c1a2b3c4")!.Workflow, WorkflowRunBindings.Read(_home, "chat-c1a2b3c4")!.Version));
        Assert.True(process.Named);
        Assert.Equal((LandingForm.Branch, "chat/{session}-{slug}"), (process.Landing.Rule.Form, process.Landing.Rule.Pattern));
        var note = Assert.Single(events.After("c1a2b3c4", 0).Events, each => each.Kind == SessionEventKind.Note);
        Assert.Equal("This conversation's work follows `chat-steps` v1, the workflow `engine` chose.", note.Text);
        Assert.Equal(NoteCodes.ChatWorkflow.Code, Assert.Single(note.Parts!).Code);

        var current = binds.Opened("d1a2b3c4", "engine", "aurora", DriverConfig.Parse("""{"sessionChecks":{"engine":"light"}}"""));
        Assert.True(WorkflowRunBindings.Read(_home, "chat-d1a2b3c4")!.IsCurrent);
        Assert.False(current.Named);
        Assert.DoesNotContain(events.After("d1a2b3c4", 0).Events, each => each.Kind == SessionEventKind.Note);

        var told = ChatRunner.ProcessTold(CommitRules.Resolve(config, "engine", "aurora"), plan: null);
        Assert.DoesNotContain("pipeline is light", told.Words, StringComparison.Ordinal);
        Assert.DoesNotContain("checks", note.Text, StringComparison.Ordinal);
    }

    /// <summary>A service that answers nothing: the open's bind reads only local files.</summary>
    private sealed class Unanswered : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            throw new HttpRequestException("the stand-in answers nothing");
    }

    private AdapterSet Declared()
    {
        var folder = DaorisHome.Entry(_home, HomeEntry.Plugins,"acme.agent");
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

    /// <summary>
    /// 🔴 SESSCTX1b (the session-context design §3.1 item 5, Fable's F5): a new part is handed the files its line's parts kept, by
    /// name and where they lie, so its rules also allow a read of exactly those kept folders, as its own (D52 refuses every other ask).
    /// </summary>
    [Fact]
    public void A_new_part_s_rules_read_the_kept_folders_of_the_parts_its_line_continues()
    {
        var across = new AcrossReach([new("game", "/work/game")], [], [new("personal", "/srv/personal")]);

        var rules = ChatRunner.RulesFor(
            PermissionFile.Empty, "default", "engine", "/work/engine", "/data/chats/s3", across, continued: ["/data/chats/s1", "/data/chats/s2"]);

        Assert.Contains(PermissionRules.ReadRule("/data/chats/s3"), rules.Allow);
        Assert.Contains(PermissionRules.ReadRule("/data/chats/s1"), rules.Allow);
        Assert.Contains(PermissionRules.ReadRule("/data/chats/s2"), rules.Allow);
        Assert.DoesNotContain(rules.Deny, rule => rule.Contains("/data/chats", StringComparison.Ordinal));
        Assert.Equal(
            ChatRunner.RulesFor(PermissionFile.Empty, "default", "engine", "/work/engine", "/data/chats/s3", across).Allow.Count + 2,
            rules.Allow.Count);
    }

    [Fact]
    public async Task A_harness_declared_after_the_runner_was_built_is_found_through_the_roster_s_live_set()
    {
        var built = AdapterSet.Built();
        var roster = new HarnessRoster(built, DaorisHome.Entry(_home, HomeEntry.Harnesses));
        using var service = new ServiceClient("http://127.0.0.1:1", null);
        var runner = new ChatRunner(service, built, _home, new SessionProcesses(), harnesses: roster);
        var config = DriverConfig.Load(DaorisHome.Entry(_home, HomeEntry.DriverConfig));

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
        var folder = DaorisHome.Entry(_home, HomeEntry.Plugins,"acme.agent");
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
            client, live, _home, new SessionProcesses(DaorisHome.Entry(_home, HomeEntry.Sessions)),
            harnesses: new HarnessRoster(live, DaorisHome.Entry(_home, HomeEntry.Harnesses)));

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
