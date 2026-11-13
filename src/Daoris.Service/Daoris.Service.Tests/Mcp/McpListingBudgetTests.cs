using System.Text.Json;
using Daoris.Knowledge.Mcp;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using Xunit.Abstractions;

namespace Daoris.Service.Tests;

/// <summary>
/// The size of the tool definitions every session is handed at its start. Every caller is listed the same tools (McpRoleFilterTests),
/// so one measure is each caller's. A number over budget needs a reason or a cut.
/// </summary>
public sealed class McpListingBudgetTests(ITestOutputHelper output)
{
    /// <summary>Characters of the listing as the wire carries it; raise it only with the reason beside it.</summary>
    internal const int ListingBudget = 42_000;

    /// <summary>One tool's definition may not exceed this many characters.</summary>
    internal const int ToolBudget = 2_600;

    /// <summary>
    /// The one tool over <see cref="ToolBudget"/>: quest_publish carries the chain, requirement, review and kind shapes an ask
    /// needs in one call, and a second tool for them would be a second way to publish.
    /// </summary>
    internal const int PublishBudget = 5_600;

    /// <summary>The tokens a model reads in JSON text, at about three characters each.</summary>
    internal static int EstimatedTokens(int characters) => (characters + 2) / 3;

    private static List<(string Name, int Characters)> Measured()
    {
        var collection = new ServiceCollection();
        collection.AddMcpServer().WithKnowledgeTools();
        using var host = collection.BuildServiceProvider();
        return [.. host.GetRequiredService<IOptions<McpServerOptions>>().Value.ToolCollection!
            .Select(tool => (tool.ProtocolTool.Name, JsonSerializer.Serialize(tool.ProtocolTool, McpJsonUtilities.DefaultOptions).Length))
            .OrderByDescending(tool => tool.Item2)];
    }

    /// <summary>🔴 The listing stays under its budget, and no one tool takes more than its share.</summary>
    [Fact]
    public void The_listing_is_within_its_budget()
    {
        var tools = Measured();
        var total = tools.Sum(tool => tool.Characters);
        output.WriteLine($"{tools.Count} tools, {total} characters, about {EstimatedTokens(total)} tokens");
        foreach (var (name, characters) in tools) output.WriteLine($"{characters,7} {name}");

        Assert.True(total <= ListingBudget, $"{total} characters is over the {ListingBudget} budget: cut or give the reason");
        var heavy = tools.Where(tool => tool.Characters > (tool.Name == "quest_publish" ? PublishBudget : ToolBudget)).Select(tool => $"{tool.Name} {tool.Characters}").ToList();
        Assert.True(heavy.Count == 0, "over " + ToolBudget + ": " + string.Join(", ", heavy));
    }
}
