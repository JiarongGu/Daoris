using System.ComponentModel;
using System.IO.Pipelines;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Daoris.Knowledge;
using Daoris.Knowledge.Mcp;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol.Server;

namespace Daoris.Service.Tests;

/// <summary>
/// FOUNDATION1r (the foundation design §2.3; COAGENT1b before it): the connector refuses, by the work of the session it names and
/// before the tool runs, every tool its row of the call layer's table does not serve. The census holds the table to every tool the
/// host registers and every column, so a tool added later is refused until it is classed and placed.
/// </summary>
public sealed class McpRoleFilterTests : McpToolsFixture
{
    [McpServerToolType]
    public sealed class StandInTools
    {
        public static int Ran;

        [McpServerTool(Name = "knowledge_search")]
        [Description("Stands in for the search.")]
        public static string Search()
        {
            Interlocked.Increment(ref Ran);
            return "found";
        }

        [McpServerTool(Name = "helper_ask")]
        [Description("Stands in for an ask.")]
        public static string Ask()
        {
            Interlocked.Increment(ref Ran);
            return "asked";
        }

        [McpServerTool(Name = "quest_list")]
        [Description("Stands in for the quest list.")]
        public static string List()
        {
            Interlocked.Increment(ref Ran);
            return "listed";
        }

        [McpServerTool(Name = "brainstorm_give")]
        [Description("Stands in for a generator's answer.")]
        public static string Give() => "given";

        [McpServerTool(Name = "brainstorm_synthesize")]
        [Description("Stands in for a synthesis.")]
        public static string Synthesize() => "synthesized";
    }

    private static readonly SessionWork Partner = new(ParticipantWork.Partner);

    /// <summary>
    /// 🔴 FOUNDATION1r, the census: every tool the host registers against every column the call layer places a named session in, each
    /// cell as <c>fixtures/call-table.json</c> names it, and the file names every column and no tool the host does not register.
    /// </summary>
    [Fact]
    public void Every_work_is_served_exactly_the_tools_its_row_of_the_table_names()
    {
        var advertised = ToolClassesTests.Advertised();
        var table = CallTableRows();

        Assert.Equal(CallTable.Columns.Select(column => column.ToString()).Order(StringComparer.Ordinal), table.Keys.Order(StringComparer.Ordinal));
        var differences = new List<string>();
        foreach (var column in CallTable.Columns)
        {
            var (only, allBut) = table[column.ToString()];
            differences.AddRange((only ?? allBut!).Where(name => !advertised.Contains(name)).Select(name => $"{column} names `{name}`, which is not registered"));
            foreach (var tool in advertised)
            {
                var served = only?.Contains(tool) ?? !allBut!.Contains(tool);
                if (served != CallTable.Served(column, tool)) differences.Add($"{column} × {tool}: the table says {(served ? "served" : "refused")}");
            }
        }

        Assert.True(differences.Count == 0, string.Join('\n', differences));
    }

    /// <summary>
    /// 🔴 FOUNDATION1r, failing closed: a tool the table does not class is refused to every column, and a work it does not know (a
    /// brainstorm with no turn, a work added later) is served the reads alone. A connector naming no session keeps its scope.
    /// </summary>
    [Fact]
    public void An_unclassed_tool_or_an_unknown_work_refuses()
    {
        Assert.All(CallTable.Columns, column => Assert.Equal(CallTable.UnknownTool, CallTable.Refusal(column, "a_tool_added_later")?.Code));
        Assert.Equal(CallTable.UnknownTool, CallTable.Refusal(new SessionWork(ParticipantWork.Build), null)?.Code);
        Assert.Null(CallTable.Refusal(SessionWork.Unnamed, "a_tool_added_later"));

        foreach (var unknown in new[] { new SessionWork(ParticipantWork.Brainstorm), new SessionWork("steward"), new SessionWork(ParticipantWork.Build, "g1") })
        {
            Assert.All(ToolClassesTests.Advertised(), tool => Assert.Equal(
                ToolClasses.Of(tool) == ToolClass.Read ? null : CallTable.Unplaced, CallTable.Refusal(unknown, tool)?.Code));
        }
    }

    /// <summary>
    /// 🔴 KNOWCENTRAL2b (the memory coordinator design §2): the table's file names the driver's four permission sources that move with it,
    /// each a list in the driver's code, so a tool placed here is placed there in the same change.
    /// </summary>
    [Fact]
    public void The_tables_file_names_the_four_permission_sources_that_move_with_it()
    {
        using var table = JsonDocument.Parse(File.ReadAllText(CallTableFile()));
        var why = table.RootElement.GetProperty("_why").GetString()!;

        foreach (var (file, owner, list) in new[]
        {
            ("OpinionPermissions.cs", "OpinionPermissions", "Offered"), ("PartnerPermissions.cs", "PartnerPermissions", "Offered"),
            ("BrainstormPermissions.cs", "BrainstormPermissions", "ConnectorTools"), ("Permissions.cs", "PermissionRules", "Defaults"),
        })
        {
            Assert.Contains($"{owner}.{list}", why);
            var source = File.ReadAllText(SourceFiles.Driver(file));
            Assert.Contains($"class {owner}", source);
            Assert.Matches($@"\b{list}\s*(\{{\s*get;\s*\}})?\s*=", source);
        }
    }

    private static string CallTableFile() =>
        Path.Combine(SourceFiles.WorkspaceFolder(), "src", "Daoris.Service", "Daoris.Service.Tests", "fixtures", "call-table.json");

    /// <summary>The table's rows as the fixture names them: a column's served tools, or every tool but those listed.</summary>
    private static Dictionary<string, (string[]? Only, string[]? AllBut)> CallTableRows()
    {
        using var file = JsonDocument.Parse(File.ReadAllText(CallTableFile()));
        string[]? Names(JsonElement row, string list) =>
            row.TryGetProperty(list, out var names) ? [.. names.EnumerateArray().Select(name => name.GetString()!)] : null;

        return file.RootElement.GetProperty("columns").EnumerateObject()
            .ToDictionary(column => column.Name, column => (Names(column.Value, "only"), Names(column.Value, "allBut")));
    }

    /// <summary>
    /// The driver's rules for a partner offer what this host allows it, by name (<c>PartnerPermissions.Offered</c>), so the two layers
    /// never disagree on what a partner may call.
    /// </summary>
    [Fact]
    public void The_drivers_partner_rules_offer_what_this_host_allows()
    {
        var source = File.ReadAllText(SourceFiles.Driver("PartnerPermissions.cs"));

        var list = System.Text.RegularExpressions.Regex.Match(source, @"Offered\s*=\s*\[(?<names>[^\]]*)\]").Groups["names"].Value;
        var named = System.Text.RegularExpressions.Regex.Matches(list, "\"([a-z_]+)\"").Select(match => match.Groups[1].Value);

        Assert.Equal(CallTable.PartnerServed.Order(StringComparer.Ordinal), named.Order(StringComparer.Ordinal));
    }

    /// <summary>
    /// 🔴 The foundation design §7 row 11: a reviewer and a brainstorm's voice are served at the server exactly what their handed rules
    /// allow (<c>OpinionPermissions.Offered</c>, <c>BrainstormPermissions.ToolOf</c>), cell for cell, on every door.
    /// </summary>
    [Fact]
    public void A_reviewer_and_a_voice_are_served_what_their_handed_rules_allow()
    {
        var advertised = ToolClassesTests.Advertised();
        var opinion = File.ReadAllText(SourceFiles.Driver("OpinionPermissions.cs"));
        var list = System.Text.RegularExpressions.Regex.Match(opinion, @"string\[\] Offered\s*=\s*\[(?<names>[^\]]*)\]").Groups["names"].Value;
        var offered = System.Text.RegularExpressions.Regex.Matches(list, "\"([a-z_]+)\"").Select(match => match.Groups[1].Value).ToHashSet();
        var brainstorm = File.ReadAllText(SourceFiles.Driver("BrainstormPermissions.cs"));

        Assert.NotEmpty(offered);
        Assert.All(advertised, tool => Assert.Equal(offered.Contains(tool), CallTable.Served(new SessionWork(ParticipantWork.Review), tool)));
        Assert.Contains("ToolOf(string turn) => turn.StartsWith('s') ? Synthesize : Give;", brainstorm);
        Assert.Equal([BrainstormBox.GiveTool], advertised.Where(tool => CallTable.Served(SessionWork.Generating, tool)));
        Assert.Equal([BrainstormBox.SynthesizeTool], advertised.Where(tool => CallTable.Served(SessionWork.Synthesizing, tool)));
    }

    /// <summary>🔴 Through the server: a voice's own result tool reaches it, the other turn's and every other tool do not.</summary>
    [Fact]
    public async Task A_voices_result_tool_is_served_through_the_server()
    {
        var generator = await CallAsync(_ => Task.FromResult(SessionWork.Generating), "brainstorm_give", "brainstorm_synthesize", "knowledge_search");
        var synthesizer = await CallAsync(_ => Task.FromResult(SessionWork.Synthesizing), "brainstorm_synthesize", "brainstorm_give");

        Assert.Equal(("given", null), generator[0]);
        Assert.Equal(CallTable.ForbiddenWork, generator[1].Code);
        Assert.Equal(CallTable.ForbiddenWork, generator[2].Code);
        Assert.Equal(("synthesized", null), synthesizer[0]);
        Assert.Equal(CallTable.ForbiddenWork, synthesizer[1].Code);
    }

    /// <summary>
    /// 🔴 The foundation design §7 row 10: a stand-in caller naming a session this machine keeps no record of is served the reads and
    /// refused every mutation, unplaced; before, nothing was refused it.
    /// </summary>
    [Fact]
    public async Task A_named_caller_with_no_record_is_served_reads_and_refused_mutations()
    {
        var ledger = await LedgerAsync();
        var before = StandInTools.Ran;

        var answers = await CallAsync(new SessionWorks(ledger, new IntakeScope(null, "s-stand-in")).ReadAsync,
            "knowledge_search", "quest_list", "helper_ask", "brainstorm_give");

        Assert.Equal(("found", null), answers[0]);
        Assert.Equal(("listed", null), answers[1]);
        Assert.Equal(CallTable.Unplaced, answers[2].Code);
        Assert.Contains("serves this session the reads alone", answers[2].Text);
        Assert.Equal(CallTable.Unplaced, answers[3].Code);
        Assert.Equal(before + 2, StandInTools.Ran);
    }

    /// <summary>
    /// Each refusal says why in the words its caller read before the table, where it was refused before: by the filter (a partner, a
    /// record that serves, a brainstorm pass) or by the box that judged by the work alone (the channel, Ask Daoris's tools, a turn).
    /// </summary>
    [Theory]
    [InlineData("partner", null, "quest_list", "not a partner's to call")]
    [InlineData("help", null, "quest_publish", "publishes, takes and closes no quest")]
    [InlineData("help", null, "channel_post", "has no channel")]
    [InlineData("build", null, "settings_cap", "is Ask Daoris's alone")]
    [InlineData("build", null, "brainstorm_give", "serves no brainstorm turn Daoris started")]
    [InlineData("brainstorm", "generator", "quest_list", "not a brainstorm pass's to call")]
    [InlineData("brainstorm", "generator", "brainstorm_synthesize", "answer through `brainstorm_give`")]
    [InlineData("brainstorm", "synthesizer", "brainstorm_give", "answer through `brainstorm_synthesize`")]
    public void Each_refusal_says_why_in_the_words_it_was_said_before(string work, string? turn, string tool, string words)
    {
        var refusal = CallTable.Refusal(new SessionWork(work, turn), tool);

        Assert.Equal(CallTable.ForbiddenWork, refusal?.Code);
        Assert.Contains(words, refusal!.Text);
    }

    /// <summary>
    /// 🔴 FOUNDATION1r: a brainstorm's scope is placed first, by the turn named on its connector and never by a record, as its generator
    /// or its synthesizer. A connector naming no whole turn is read by its record, and a session no record keeps is unplaced.
    /// </summary>
    [Fact]
    public async Task A_brainstorm_pass_is_placed_by_its_turn_first_as_generator_or_synthesizer()
    {
        var ledger = await LedgerAsync();
        BrainstormScope Turn(string turn) => new("b1", turn, Path.Combine(_root, "answer.json"), Path.Combine(_root, "room"));

        Assert.Equal(SessionWork.Generating, await new SessionWorks(ledger, new IntakeScope(null, "brainstorm-b1-g1"), Turn("g1")).ReadAsync(default));
        Assert.Equal(SessionWork.Synthesizing, await new SessionWorks(ledger, new IntakeScope(null, "brainstorm-b1-s1"), Turn("s1r")).ReadAsync(default));
        Assert.Equal(SessionWork.Unplaced, await new SessionWorks(ledger, new IntakeScope(null, "brainstorm-b1-g1"), new BrainstormScope(null, null, null, null))
            .ReadAsync(default));
    }

    /// <summary>
    /// 🔴 FOUNDATION1r: the connector's session is its record's work, read by the one rule (<see cref="ParticipantWork.Of"/>); a session
    /// with no record is unplaced, and a connector that names none is a person's own agent's.
    /// </summary>
    [Fact]
    public async Task The_work_is_read_from_the_record_the_connector_names()
    {
        var sessions = await SessionStore.OpenAsync(_connection);
        var ledger = new SessionLedger(_quests, sessions, _service, await AskStore.OpenAsync(_connection));
        var lead = await sessions.CreateAsync("q1", "Owner", "claude-code", DateTimeOffset.UtcNow, tree: Path.Combine(_root, "trees", "lead"));
        var partner = await sessions.CreateAsync(null, "Owner", "codex-acp", DateTimeOffset.UtcNow, kind: SessionKind.Chat, serves: lead.Id, piece: "pc000001");
        var helper = await sessions.CreateAsync(null, "Owner", "codex-acp", DateTimeOffset.UtcNow, kind: SessionKind.Chat, serves: lead.Id);
        var chat = await sessions.CreateAsync(null, "Owner", "codex-acp", DateTimeOffset.UtcNow, kind: SessionKind.Chat);

        Task<SessionWork> Read(string? session) => new SessionWorks(ledger, new IntakeScope(null, session)).ReadAsync(default);

        Assert.Equal(new SessionWork(ParticipantWork.Partner), await Read(partner.Id));
        Assert.Equal(new SessionWork(ParticipantWork.Help), await Read(helper.Id));
        Assert.Equal(new SessionWork(ParticipantWork.Build), await Read(lead.Id));
        Assert.Equal(new SessionWork(ParticipantWork.Chat), await Read(chat.Id));
        Assert.Equal(SessionWork.Unplaced, await Read("nobody"));
        Assert.Equal(SessionWork.Unnamed, await Read(null));
    }

    private async Task<SessionLedger> LedgerAsync()
    {
        var sessions = await SessionStore.OpenAsync(_connection);
        return new SessionLedger(_quests, sessions, _service, await AskStore.OpenAsync(_connection));
    }

    /// <summary>🔴 Through the server: a partner's refused call is answered with its sentence and never reaches its tool.</summary>
    [Fact]
    public async Task A_refused_call_never_reaches_its_tool()
    {
        var before = StandInTools.Ran;

        var answers = await CallAsync(_ => Task.FromResult(Partner), "helper_ask", "knowledge_search");

        Assert.Contains("not a partner's to call", answers[0].Text);
        Assert.Equal(CallTable.ForbiddenWork, answers[0].Code);
        Assert.Equal(("found", null), answers[1]);
        Assert.Equal(before + 1, StandInTools.Ran);
    }

    /// <summary>
    /// 🔴 A record that cannot be read lets the reads answer, `quest_list` among them, and refuses the rest, since nothing says they are
    /// allowed (the foundation design §7 row 10).
    /// </summary>
    [Fact]
    public async Task An_unread_record_lets_only_the_reading_tools_answer()
    {
        var answers = await CallAsync(_ => throw new InvalidOperationException("the store is gone"), "helper_ask", "knowledge_search", "quest_list");

        Assert.Contains("could not read this session's record", answers[0].Text);
        Assert.Equal(CallTable.Unread, answers[0].Code);
        Assert.Equal(("found", null), answers[1]);
        Assert.Equal(("listed", null), answers[2]);
    }

    /// <summary>The host's own composition judges every call, so what the tests above hold is what a session's connector does.</summary>
    [Fact]
    public void The_host_judges_every_call_by_the_sessions_role()
    {
        var at = new DirectoryInfo(AppContext.BaseDirectory);
        while (at is not null && !File.Exists(Path.Combine(at.FullName, "daoris.json"))) at = at.Parent;
        var program = File.ReadAllText(Path.Combine(at!.FullName, "src", "Daoris.Service", "Daoris.Service.Mcp", "Program.cs"));

        Assert.Contains(".RefuseOutsideRole(new SessionWorks(composed.Ledger, intake, BrainstormScope.FromEnvironment()).ReadAsync)", program);
    }

    /// <summary>Each tool called through the server in turn: what it answered, and the refusal's code where it was refused.</summary>
    private static async Task<List<(string Text, string? Code)>> CallAsync(Func<CancellationToken, Task<SessionWork>> role, params string[] tools)
    {
        var toServer = new Pipe();
        var fromServer = new Pipe();
        var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());
        builder.Services
            .AddMcpServer(options => options.ServerInfo = new() { Name = "example-knowledge", Version = "0.0.1" })
            .WithStreamServerTransport(toServer.Reader.AsStream(), fromServer.Writer.AsStream())
            .WithTools(toolTypes: [typeof(StandInTools)])
            .RefuseOutsideRole(role);

        var answers = new List<(string Text, string? Code)>();
        using (var host = builder.Build())
        {
            await host.StartAsync();
            using var reader = new StreamReader(fromServer.Reader.AsStream());

            async Task<JsonNode> Send(JsonObject message)
            {
                await toServer.Writer.WriteAsync(Encoding.UTF8.GetBytes(message.ToJsonString() + "\n"));
                var line = await reader.ReadLineAsync().WaitAsync(Poll.Patience);
                return JsonNode.Parse(line!)!;
            }

            await Send(new JsonObject
            {
                ["jsonrpc"] = "2.0", ["id"] = 0, ["method"] = "initialize",
                ["params"] = new JsonObject
                {
                    ["protocolVersion"] = "2025-06-18", ["capabilities"] = new JsonObject(),
                    ["clientInfo"] = new JsonObject { ["name"] = "test", ["version"] = "0" },
                },
            });
            await toServer.Writer.WriteAsync(Encoding.UTF8.GetBytes("""{"jsonrpc":"2.0","method":"notifications/initialized"}""" + "\n"));
            var id = 1;
            foreach (var tool in tools)
            {
                var answer = await Send(new JsonObject
                {
                    ["jsonrpc"] = "2.0", ["id"] = id++, ["method"] = "tools/call", ["params"] = new JsonObject { ["name"] = tool },
                });
                var result = answer["result"]!;
                answers.Add((string.Join("\n", result["content"]!.AsArray().Select(block => (string)block!["text"]!)),
                    (string?)result["_meta"]?[RoleFilter.RefusalKey]));
            }

            await toServer.Writer.CompleteAsync();
            await host.StopAsync();
        }

        return answers;
    }
}
