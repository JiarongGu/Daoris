using Daoris.Knowledge.Mcp;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;

namespace Daoris.Service.Tests;

/// <summary>
/// FABRIC-C2a (D173's FABRIC-C2a note, point 3): one class for every tool the host registers, so a tool added later is refused to
/// a child until it is classed.
/// </summary>
public sealed class ToolClassesTests
{
    internal static IReadOnlyList<string> Advertised()
    {
        var collection = new ServiceCollection();
        collection.AddMcpServer().WithKnowledgeTools();
        using var host = collection.BuildServiceProvider();
        return [.. host.GetRequiredService<IOptions<McpServerOptions>>().Value.ToolCollection!
            .Select(tool => tool.ProtocolTool.Name).Order(StringComparer.Ordinal)];
    }

    /// <summary>🔴 The census: every registered tool has a class, and the table names no tool the host does not register.</summary>
    [Fact]
    public void Every_registered_tool_has_a_class_and_the_table_names_no_other()
    {
        var advertised = Advertised();

        Assert.All(advertised, name => Assert.NotNull(ToolClasses.Of(name)));
        Assert.All(ToolClasses.Named.Keys, name => Assert.Contains(name, advertised));
        Assert.Null(ToolClasses.Of("a_tool_added_later"));
        Assert.Null(ToolClasses.Of(null));
    }

    /// <summary>The note's table: its reads and the two since that change nothing, its one exception, and everything else restricted.</summary>
    [Fact]
    public void The_table_is_the_note_s()
    {
        Assert.Equal(
            ["handback_read", "knowledge_convergence", "knowledge_get", "knowledge_repositories", "knowledge_search", "line_read", "quest_list", "registry"],
            Advertised().Where(name => ToolClasses.Of(name) == ToolClass.Read));
        Assert.Equal(["quest_publish"], Advertised().Where(name => ToolClasses.Of(name) == ToolClass.Publish));
        Assert.All(
            new[]
            {
                "helper_ask", "channel_post", "quest_respond", "go_ahead_ask", "opinion_give", "permission_propose", "channel_read",
                "helper_answer", "opinion_answer", "review_serve", "review_ready", "knowledge_refresh", "checkout_read", "housekeeping_inspect",
            }.Concat(Advertised().Where(name => name.EndsWith("_propose", StringComparison.Ordinal))),
            name => Assert.Equal(ToolClass.Restricted, ToolClasses.Of(name)));
    }
}
