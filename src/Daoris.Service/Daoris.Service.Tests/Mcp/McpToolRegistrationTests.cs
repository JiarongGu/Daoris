using System.Reflection;
using Daoris.Knowledge;
using Daoris.Knowledge.Mcp;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
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
        "accept_propose", "agent_propose", "agent_settings_propose", "ask_propose", "brainstorm_give", "brainstorm_synthesize", "browser_propose", "channel_post",
        "channel_read", "clear_propose", "delete_propose", "go_ahead_ask", "go_propose", "hand_propose", "helper_answer", "helper_ask",
        "housekeeping_clean_up", "housekeeping_discard_branch", "housekeeping_inspect", "knowledge_convergence",
        "knowledge_get", "knowledge_refresh", "knowledge_repositories", "knowledge_search", "opinion_answer", "opinion_give",
        "pause_propose", "permission_propose", "plugin_propose", "quest_list", "quest_publish", "quest_respond", "registry",
        "repository_propose", "review_ready", "review_serve", "session_propose", "setting_propose", "setup_propose", "sync_propose",
        "workflow_propose",
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
    /// BRAINSTORM1a2 (D176 point 2): a brainstorm's pass is denied every tool of the connector but its result, by name, from the
    /// driver's <c>BrainstormPermissions.ConnectorTools</c>. A tool the connector gains is denied there in the same change, or a pass
    /// could be offered it. The merge tool's rule for the driver's file runs this suite when it changes.
    /// </summary>
    [Fact]
    public void The_driver_denies_a_brainstorm_pass_every_tool_this_host_advertises()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "daoris.json"))) root = root.Parent;
        var source = File.ReadAllText(Path.Combine(
            root!.FullName, "src", "Daoris.Desktop", "Daoris.Desktop.Driver", "Brainstorms", "Domain", "BrainstormPermissions.cs"));

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
                "BrainstormTools", "ChannelTools", "GoAheadTools", "HelpAcceptTool", "HelpAccountTool", "HelpAgentTool", "HelpAskTool", "HelpBrowserTool", "HelpClearTool",
                "HelpDeleteTool", "HelpGoTool", "HelpHandTool", "HelpPauseTool", "HelpPluginTool", "HelpRepositoryTool", "HelpSessionTool",
                "HelpSettingTool", "HelpSetupTool", "HelpSyncTool", "HelpWorkflowTool", "HelperTools", "HousekeepingTools", "KnowledgeSearchTools",
                "OpinionTools", "QuestTools",
                "ReviewTools", "RuleProposalTools",
            ],
            KnowledgeServer.ToolTypes.Select(type => type.Name).Order(StringComparer.Ordinal));
    }

    /// <summary>
    /// The SDK builds a tool's class for each call from the host's services, so a class asking for one nobody registers
    /// fails only when an agent calls it. The host's required services are the fixture's here; the rest are optional, and
    /// the workspace scope is the registration's own.
    /// </summary>
    [Fact]
    public void Every_tool_class_is_built_from_the_hosts_services()
    {
        using var host = Host(services => services
            .AddSingleton(_service)
            .AddSingleton(_quests)
            .AddSingleton(new QuestExchange(_service, _quests, files: _files))
            .AddSingleton(new AmbientWorkspace(Path.Combine(_root, "family", "Asker"))));

        foreach (var type in KnowledgeServer.ToolTypes)
        {
            Assert.IsType(type, ActivatorUtilities.CreateInstance(host, type));
        }
    }
}
