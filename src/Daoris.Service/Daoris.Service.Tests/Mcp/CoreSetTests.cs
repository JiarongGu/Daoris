using System.Text.Json;
using Daoris.Knowledge;
using Daoris.Knowledge.Mcp;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using Xunit.Abstractions;

namespace Daoris.Service.Tests;

/// <summary>
/// A session Daoris runs in a repository loads its connector's tools at its start, so it is served the
/// connector's core set and its own kind's tools alone, from the call layer's one table; Ask Daoris is served every tool. What a
/// kind is not served it is neither listed nor let call.
/// </summary>
public sealed class CoreSetTests(ITestOutputHelper output)
{
    private static readonly string[] Core =
    [
        "channel_post", "channel_read", "handback_read", "helper_ask", "knowledge_convergence", "knowledge_get", "knowledge_repositories",
        "knowledge_search", "line_read", "memory_end", "memory_get", "memory_propose", "memory_search", "memory_write", "quest_list",
        "quest_publish", "quest_respond", "registry",
    ];

    private static readonly string[] Working =
        ["go_ahead_ask", "handover_propose", "opinion_answer", "permission_propose", "question_ask", "review_ready", "review_serve"];

    /// <summary>🔴 The core set: the knowledge, memory, quests, the line, the registry, a helper's ask, the channel and the handbacks.</summary>
    [Fact]
    public void The_core_set_is_the_tools_every_repository_session_works_with()
    {
        Assert.Equal(Core, CallTable.CoreServed.Order(StringComparer.Ordinal));
        Assert.All(CallTable.CoreServed, tool => Assert.Contains(tool, ToolClassesTests.Advertised()));
    }

    /// <summary>
    /// 🔴 Each repository work's row is the core set and its own tools, less what the table refused it before (a helper's quests,
    /// memory changes and channel, an intake's memory changes and helper ask); Ask Daoris's row is every tool but the turns, the helper ask and the memory changes.
    /// </summary>
    [Theory]
    [InlineData(ParticipantWork.Build)]
    [InlineData(ParticipantWork.Design)]
    [InlineData(ParticipantWork.Chat)]
    [InlineData(ParticipantWork.Intake)]
    [InlineData(ParticipantWork.Help)]
    [InlineData(ParticipantWork.AskDaoris)]
    public void Each_repository_work_is_served_the_core_set_and_its_own_tools_alone(string work)
    {
        string[] memoryChanges = ["memory_end", "memory_write"];
        IEnumerable<string> expected = work switch
        {
            ParticipantWork.Build or ParticipantWork.Design => [.. Core, .. Working],
            ParticipantWork.Chat => [.. Core, "go_ahead_ask", "handover_propose", "opinion_answer", "permission_propose"],
            ParticipantWork.Intake => Core.Except([.. memoryChanges, "helper_ask"]),
            ParticipantWork.Help => [.. Core.Except(["channel_post", "channel_read", "memory_propose", "quest_publish", "quest_respond", .. memoryChanges]),
                "helper_answer"],
            _ => ToolClassesTests.Advertised().Except(["brainstorm_give", "brainstorm_synthesize", "helper_ask", "system_plan", .. memoryChanges]),
        };

        Assert.Equal(expected.Order(StringComparer.Ordinal), Served(new SessionWork(work)));
    }

    /// <summary>A quest's session in a system department alone is served <c>system_plan</c>, keeping a build session within the measure below.</summary>
    [Theory]
    [InlineData(ParticipantWork.Build)]
    [InlineData(ParticipantWork.Design)]
    [InlineData(ParticipantWork.Exact)]
    public void A_system_department_s_session_alone_is_served_its_plan(string work)
    {
        Assert.Equal(Core.Concat(Working).Append("system_plan").Order(StringComparer.Ordinal), Served(new SessionWork(work) { InSystem = true }));
        var refused = CallTable.Refusal(new SessionWork(work), "system_plan");
        Assert.Equal(CallTable.ForbiddenWork, refused?.Code);
        Assert.EndsWith("Nothing was planned.", refused!.Text, StringComparison.Ordinal);
    }

    /// <summary>🔴 A tool outside a kind's row is refused in words that name what the kind is served, and nothing runs.</summary>
    [Fact]
    public void A_tool_outside_a_kinds_row_is_refused_naming_the_core_set()
    {
        var refusal = CallTable.Refusal(new SessionWork(ParticipantWork.Build), "go_propose");
        var chat = CallTable.Refusal(new SessionWork(ParticipantWork.Chat), "question_ask");

        Assert.Equal(CallTable.ForbiddenWork, refusal?.Code);
        Assert.Contains("core", refusal!.Text);
        Assert.Contains("Nothing was done", refusal.Text);
        Assert.Equal(CallTable.ForbiddenWork, chat?.Code);
    }

    /// <summary>
    /// The measure (CONTEXTONE1d2): each kind's served definitions, as <c>tools/list</c> answers them, against every tool's. The core set
    /// stays within 30,000 characters and a repository kind's row within 40,000 (measured at 29,042 and a build session's at 39,856, of 85,561 for
    /// every tool); the sizes are written to the test's output and recorded in the changelog, so a description grown past the budget is
    /// seen here.
    /// </summary>
    [Fact]
    public void Each_kinds_definitions_are_measured_against_every_tools()
    {
        var sizes = Definitions();
        var core = CallTable.CoreServed.Sum(tool => sizes[tool]);
        output.WriteLine($"every tool: {sizes.Count} tools, {sizes.Values.Sum()} characters");
        output.WriteLine($"core: {CallTable.CoreServed.Count} tools, {core} characters");
        foreach (var column in CallTable.Columns)
        {
            var served = sizes.Keys.Where(tool => CallTable.Served(column, tool)).ToList();
            var size = served.Sum(tool => sizes[tool]);
            output.WriteLine($"{column}: {served.Count} tools, {size} characters");
            if (column.Work is ParticipantWork.Build or ParticipantWork.Design or ParticipantWork.Chat or ParticipantWork.Intake
                or ParticipantWork.Help)
            {
                Assert.True(size <= 40_000, $"{column} is served {size} characters of definitions");
            }
        }

        Assert.True(core <= 30_000, $"the core set is {core} characters of definitions");
    }

    private static IReadOnlyList<string> Served(SessionWork work) =>
        [.. ToolClassesTests.Advertised().Where(tool => CallTable.Served(work, tool))];

    /// <summary>Each registered tool's definition, serialized as the protocol carries it, by its length in characters.</summary>
    private static Dictionary<string, int> Definitions()
    {
        var collection = new ServiceCollection();
        collection.AddMcpServer().WithKnowledgeTools();
        using var host = collection.BuildServiceProvider();
        return host.GetRequiredService<IOptions<McpServerOptions>>().Value.ToolCollection!.ToDictionary(
            tool => tool.ProtocolTool.Name, tool => JsonSerializer.Serialize(tool.ProtocolTool, McpJsonUtilities.DefaultOptions).Length,
            StringComparer.Ordinal);
    }
}
