using Daoris.Knowledge;
using Daoris.Knowledge.Mcp;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;

namespace Daoris.Service.Tests;

/// <summary>
/// PART10a: the tools the connector's host advertises, read through the SDK's own listing of what the host registers. The
/// names are the wire, which the driver's connector and the family rehearsal call by name, so moving a tool to a class of
/// its own changes none of them, and none is lost or offered twice.
/// </summary>
public sealed class McpToolRegistrationTests : McpToolsFixture
{
    /// <summary>Every name the host advertised before the tools left one class, in ordinal order.</summary>
    private static readonly string[] Advertised =
    [
        "accept_propose", "agent_propose", "agent_settings_propose", "ask_propose", "browser_propose", "clear_propose",
        "delete_propose", "go_ahead_ask", "go_propose", "hand_propose", "helper_answer", "helper_ask", "knowledge_convergence",
        "knowledge_get", "knowledge_refresh", "knowledge_repositories", "knowledge_search", "opinion_answer", "opinion_give",
        "pause_propose", "permission_propose", "plugin_propose", "quest_list", "quest_publish", "quest_respond", "registry",
        "repository_propose", "review_ready", "review_serve", "session_propose", "setting_propose", "sync_propose",
        "workflow_propose",
    ];

    private static ServiceProvider Host(Action<IServiceCollection>? services = null)
    {
        var collection = new ServiceCollection();
        services?.Invoke(collection);
        collection.AddMcpServer().WithTools<KnowledgeTools>();
        return collection.BuildServiceProvider();
    }

    [Fact]
    public void The_host_advertises_every_tool_once_by_its_name()
    {
        using var host = Host();

        var names = host.GetServices<McpServerTool>().Select(tool => tool.ProtocolTool.Name).Order(StringComparer.Ordinal);

        Assert.Equal(Advertised, names);
    }

    /// <summary>
    /// The SDK builds a tool's class for each call from the host's services, so a class asking for one nobody registers
    /// fails only when an agent calls it. The host's required services are the fixture's here; the rest are optional.
    /// </summary>
    [Fact]
    public void Every_tool_class_is_built_from_the_hosts_services()
    {
        using var host = Host(services => services
            .AddSingleton(_service)
            .AddSingleton(_quests)
            .AddSingleton(new QuestExchange(_service, _quests, files: _files))
            .AddSingleton(new AmbientWorkspace(Path.Combine(_root, "family", "Asker"))));

        foreach (var type in new[] { typeof(KnowledgeTools) })
        {
            Assert.IsType(type, ActivatorUtilities.CreateInstance(host, type));
        }
    }
}
