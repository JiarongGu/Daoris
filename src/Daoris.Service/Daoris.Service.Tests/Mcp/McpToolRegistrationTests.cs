using System.Reflection;
using Daoris.Knowledge;
using Daoris.Knowledge.Mcp;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;

namespace Daoris.Service.Tests;

/// <summary>The tools the connector's host advertises, through the SDK's own listing: the names are the wire, called by name, so none is lost or offered twice.</summary>
public sealed class McpToolRegistrationTests : McpToolsFixture
{
    private static readonly string[] Advertised =
    [
        "brainstorm_give", "brainstorm_synthesize", "channel_post",
        "channel_read", "check_run", "checkout_read", "go_ahead_ask", "handback_read", "handover_propose",
        "helper_answer", "helper_ask",
        "knowledge_convergence",
        "knowledge_get", "knowledge_refresh", "knowledge_repositories", "knowledge_search", "line_read", "memory_end", "memory_get", "memory_propose",
        "memory_search", "memory_write", "opinion_answer", "opinion_give",
        "partner_answer", "permission_propose", "quest_list", "quest_publish", "quest_respond", "question_ask", "registry",
        "review_ready", "review_serve", "system_plan", "team_plan",
    ];

    /// <summary>A host registered as <c>Program.cs</c> registers its tools, over whatever services the test hands it.</summary>
    private static ServiceProvider Host(Action<IServiceCollection>? services = null)
    {
        var collection = new ServiceCollection();
        services?.Invoke(collection);
        collection.AddMcpServer().WithKnowledgeTools();
        return collection.BuildServiceProvider();
    }

    [Fact]
    public void The_host_advertises_every_tool_once_by_its_name()
    {
        using var host = Host();

        var names = host.GetRequiredService<IOptions<McpServerOptions>>().Value.ToolCollection!
            .Select(tool => tool.ProtocolTool.Name).Order(StringComparer.Ordinal);

        Assert.Equal(Advertised, names);
    }

    /// <summary>
    /// A brainstorm's pass is denied every tool of the connector but its result, by name, from the driver's
    /// <c>BrainstormPermissions.ConnectorTools</c>: a tool the connector gains is denied there in the same change.
    /// </summary>
    [Fact]
    public void The_driver_denies_a_brainstorm_pass_every_tool_this_host_advertises()
    {
        var source = File.ReadAllText(SourceFiles.Driver("BrainstormPermissions.cs"));

        var list = System.Text.RegularExpressions.Regex.Match(source, @"ConnectorTools\s*=\s*\[(?<names>[^\]]*)\]").Groups["names"].Value;
        var named = System.Text.RegularExpressions.Regex.Matches(list, "\"([a-z_]+)\"").Select(match => match.Groups[1].Value);

        Assert.Equal(Advertised, named.Order(StringComparer.Ordinal));
    }

    /// <summary>
    /// The list is the connector's whole: a class of tools left off it is never offered, and nothing else finds one, since
    /// the host registers the list and does not scan.
    /// </summary>
    [Fact]
    public void The_list_is_every_tool_class_the_connector_has()
    {
        var marked = typeof(KnowledgeServer).Assembly.GetTypes()
            .Where(type => type.GetCustomAttribute<McpServerToolTypeAttribute>() is not null)
            .Select(type => type.Name)
            .Order(StringComparer.Ordinal);

        Assert.Equal(marked, KnowledgeServer.ToolTypes.Select(type => type.Name).Order(StringComparer.Ordinal));
        Assert.Equal(
            [
                "BrainstormTools", "ChannelTools", "CheckTools", "CheckoutTools", "GoAheadTools", "HandOverTools", "HandbackTools",
                "HelperTools", "KnowledgeSearchTools",
                "LineTools", "MemoryTools", "OpinionTools", "PartnerTools", "QuestTools", "QuestionTools",
                "ReviewTools", "RuleProposalTools", "SystemPlanTools", "TeamTools",
            ],
            KnowledgeServer.ToolTypes.Select(type => type.Name).Order(StringComparer.Ordinal));
    }

    /// <summary>
    /// The SDK builds a tool's class for each call from the host's services, so a class asking for one nobody registers
    /// fails only when an agent calls it. The host's required services are the fixture's here; the rest are optional, and
    /// the workspace scope is the registration's own.
    /// </summary>
    [Fact]
    public async Task Every_tool_class_is_built_from_the_hosts_services()
    {
        var exchange = new QuestExchange(_service, _quests, files: _files);
        var teams = new TeamDesk(
            await TeamStore.OpenAsync(_connection), new AskDesk(_service, await AskStore.OpenAsync(_connection), exchange, _files),
            (_, _) => Task.FromResult(TeamPathPolicy.None));
        using var host = Host(services => services
            .AddSingleton(_service)
            .AddSingleton(_quests)
            .AddSingleton(teams)
            .AddSingleton(exchange)
            .AddSingleton(new AmbientWorkspace(Path.Combine(_root, "family", "Asker"))));

        foreach (var type in KnowledgeServer.ToolTypes)
        {
            Assert.IsType(type, ActivatorUtilities.CreateInstance(host, type));
        }
    }
}
