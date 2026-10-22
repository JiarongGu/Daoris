using System.ComponentModel;
using System.IO.Pipelines;
using System.Text;
using System.Text.Json.Nodes;
using Daoris.Knowledge;
using Daoris.Knowledge.Mcp;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;

namespace Daoris.Service.Tests;

/// <summary>
/// COAGENT1b (D199 point 4; the co-agent design §7.4): the connector refuses, by the session's record and before the tool runs, every
/// tool outside a partner's allow-list, and the quest tools to any record that serves another. The census holds the partner's two lists
/// to every tool the host advertises, so a tool added later is classified when it is added.
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
    }

    private static readonly SessionRole Partner = new("lead1", "pc000001");

    private static readonly SessionRole Helper = new("lead1", null);

    /// <summary>🔴 The census: the partner's allowed and refused lists name every advertised tool, each once, and nothing else.</summary>
    [Fact]
    public void Every_advertised_tool_is_allowed_or_refused_to_a_partner_once()
    {
        var collection = new ServiceCollection();
        collection.AddMcpServer().WithKnowledgeTools();
        using var host = collection.BuildServiceProvider();
        var advertised = host.GetRequiredService<IOptions<McpServerOptions>>().Value.ToolCollection!
            .Select(tool => tool.ProtocolTool.Name).Order(StringComparer.Ordinal).ToList();

        Assert.Empty(ConnectorRoles.PartnerAllowed.Intersect(ConnectorRoles.PartnerRefused));
        Assert.Equal(advertised, ConnectorRoles.PartnerAllowed.Concat(ConnectorRoles.PartnerRefused).Order(StringComparer.Ordinal));
        Assert.All(ConnectorRoles.ServingRefused, name => Assert.Contains(name, ConnectorRoles.PartnerRefused));
    }

    /// <summary>A partner reads, reads its lead's line and answers; it publishes, asks, proposes and posts nothing.</summary>
    [Theory]
    [InlineData("knowledge_search", true)]
    [InlineData("line_read", true)]
    [InlineData("partner_answer", true)]
    [InlineData("quest_publish", false)]
    [InlineData("quest_respond", false)]
    [InlineData("quest_list", false)]
    [InlineData("helper_ask", false)]
    [InlineData("go_ahead_ask", false)]
    [InlineData("channel_post", false)]
    [InlineData("permission_propose", false)]
    [InlineData("a_tool_added_later", false)]
    public void A_partner_is_held_to_its_allow_list(string tool, bool allowed)
    {
        var refusal = ConnectorRoles.Refusal(tool, Partner);

        Assert.Equal(allowed, refusal is null);
        if (!allowed) Assert.Contains("not a partner's to call", refusal);
    }

    /// <summary>A helper keeps its tools but the quest tools; a working session, or a connector naming none, is refused nothing.</summary>
    [Fact]
    public void A_record_that_serves_publishes_and_takes_no_quest_and_others_are_refused_nothing()
    {
        Assert.Contains("publishes, takes and closes no quest", ConnectorRoles.Refusal("quest_publish", Helper));
        Assert.Contains("publishes, takes and closes no quest", ConnectorRoles.Refusal("quest_respond", Helper));
        Assert.Null(ConnectorRoles.Refusal("helper_answer", Helper));
        Assert.Null(ConnectorRoles.Refusal("quest_list", Helper));
        Assert.All(ConnectorRoles.PartnerRefused, tool => Assert.Null(ConnectorRoles.Refusal(tool, SessionRole.None)));
    }

    /// <summary>The role is the record's: a partner's, a helper's (its piece dropped without a lead is none), and nobody's.</summary>
    [Fact]
    public async Task The_role_is_read_from_the_record_the_connector_names()
    {
        var sessions = await SessionStore.OpenAsync(_connection);
        var ledger = new SessionLedger(_quests, sessions, _service, await AskStore.OpenAsync(_connection));
        var lead = await sessions.CreateAsync("q1", "Owner", "claude-code", DateTimeOffset.UtcNow, tree: Path.Combine(_root, "trees", "lead"));
        var partner = await sessions.CreateAsync(null, "Owner", "codex-acp", DateTimeOffset.UtcNow, kind: SessionKind.Chat, serves: lead.Id, piece: "pc000001");
        var helper = await sessions.CreateAsync(null, "Owner", "codex-acp", DateTimeOffset.UtcNow, kind: SessionKind.Chat, serves: lead.Id);

        Assert.Equal(new SessionRole(lead.Id, "pc000001"), await new SessionRoles(ledger, new IntakeScope(null, partner.Id)).ReadAsync(default));
        Assert.Equal(new SessionRole(lead.Id, null), await new SessionRoles(ledger, new IntakeScope(null, helper.Id)).ReadAsync(default));
        Assert.Equal(SessionRole.None, await new SessionRoles(ledger, new IntakeScope(null, lead.Id)).ReadAsync(default));
        Assert.Equal(SessionRole.None, await new SessionRoles(ledger, new IntakeScope(null, "nobody")).ReadAsync(default));
        Assert.Equal(SessionRole.None, await new SessionRoles(ledger, new IntakeScope(null, null)).ReadAsync(default));
    }

    /// <summary>🔴 Through the server: a partner's refused call is answered with its sentence and never reaches its tool.</summary>
    [Fact]
    public async Task A_refused_call_never_reaches_its_tool()
    {
        var before = StandInTools.Ran;

        var answers = await CallAsync(_ => Task.FromResult(Partner), "helper_ask", "knowledge_search");

        Assert.Contains("not a partner's to call", answers[0]);
        Assert.Equal("found", answers[1]);
        Assert.Equal(before + 1, StandInTools.Ran);
    }

    /// <summary>A record that cannot be read lets the reading tools answer and refuses the rest, since nothing says they are allowed.</summary>
    [Fact]
    public async Task An_unread_record_lets_only_the_reading_tools_answer()
    {
        var answers = await CallAsync(_ => throw new InvalidOperationException("the store is gone"), "helper_ask", "knowledge_search");

        Assert.Contains("could not read this session's record", answers[0]);
        Assert.Equal("found", answers[1]);
    }

    /// <summary>The host's own composition judges every call, so what the tests above hold is what a session's connector does.</summary>
    [Fact]
    public void The_host_judges_every_call_by_the_sessions_role()
    {
        var at = new DirectoryInfo(AppContext.BaseDirectory);
        while (at is not null && !File.Exists(Path.Combine(at.FullName, "daoris.json"))) at = at.Parent;
        var program = File.ReadAllText(Path.Combine(at!.FullName, "src", "Daoris.Service", "Daoris.Service.Mcp", "Program.cs"));

        Assert.Contains(".RefuseOutsideRole(new SessionRoles(composed.Ledger, intake).ReadAsync)", program);
    }

    private static async Task<List<string>> CallAsync(Func<CancellationToken, Task<SessionRole>> role, params string[] tools)
    {
        var toServer = new Pipe();
        var fromServer = new Pipe();
        var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());
        builder.Services
            .AddMcpServer(options => options.ServerInfo = new() { Name = "example-knowledge", Version = "0.0.1" })
            .WithStreamServerTransport(toServer.Reader.AsStream(), fromServer.Writer.AsStream())
            .WithTools(toolTypes: [typeof(StandInTools)])
            .RefuseOutsideRole(role);

        var answers = new List<string>();
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
                answers.Add(string.Join("\n", answer["result"]!["content"]!.AsArray().Select(block => (string)block!["text"]!)));
            }

            await toServer.Writer.CompleteAsync();
            await host.StopAsync();
        }

        return answers;
    }
}
