using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// AGENTPROVIDER1 s-vii (the agent-provider design §7 row 10, O3-F9, O4-F4): which agent a door is, the one reading the usage count and
/// PICKCARRY1's carry share. A move between one agent's two doors is one agent, so a picked conversation moved between them on its picked
/// account keeps its pick; two providers sharing a pool are two agents though their account key is one, so a pick is never carried between them.
/// </summary>
public sealed class AgentOfDoorTests : IDisposable
{
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "agent-of-door", Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        if (Directory.Exists(_home)) GitSeed.Discard(_home);
    }

    private AdapterSet WithPlugin(string id, string harnesses)
    {
        var folder = DaorisHome.Entry(_home, HomeEntry.Plugins, id);
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, PluginCatalog.ManifestName), $$"""{ "id": "{{id}}", "harnesses": [ {{harnesses}} ] }""");
        return AdapterSet.Built().WithPlugins(PluginCatalog.Load(_home, AdapterSet.Built().Names));
    }

    /// <summary>
    /// 🔴 Old: PICKCARRY1 compared the record's door with the new part's, so <c>claude-code</c> to <c>claude-code-acp</c> on one account
    /// dropped the pick. New: both doors are agent <c>claude-code</c>, so the pick is kept; Codex's door is its holder's agent.
    /// </summary>
    [Fact]
    public void One_agent_s_two_doors_are_one_agent_and_another_agent_is_not()
    {
        var agents = new AgentOfDoor(AdapterSet.Built().Registry);

        Assert.True(agents.Same("claude-code", "claude-code-acp"));
        Assert.True(agents.Same("stub", "acp-stub"));
        Assert.False(agents.Same("claude-code", "codex-acp"));
        Assert.Equal(("claude-code", "codex", "p-gone"), (agents.Of("claude-code-acp"), agents.Of("codex-acp"), agents.Of("p-gone")));
        Assert.False(string.Equals("claude-code", "claude-code-acp", StringComparison.OrdinalIgnoreCase), "the old carry's identity comparison");
    }

    /// <summary>
    /// Two plugins' doors naming <c>claude-code-acp</c> share its pool (<c>OwnerOf</c> equal) and are each a provider of their own: never one
    /// agent, so a pick is not carried between them, as PICKCARRY1 holds today; a door joined to <c>claude-code</c> is that agent's.
    /// </summary>
    [Fact]
    public void Two_providers_sharing_a_pool_are_two_agents_and_a_joined_door_is_its_provider_s()
    {
        var adapters = WithPlugin("p.pool", """
            { "name": "p-a", "command": ["agent"], "accountOf": "claude-code-acp" },
            { "name": "p-b", "command": ["agent"], "accountOf": "claude-code-acp" },
            { "name": "p-joined", "command": ["agent"], "accountOf": "claude-code" }
            """);
        var registry = adapters.Registry;
        var agents = new AgentOfDoor(registry);

        Assert.Equal(registry.OwnerOf(new DoorId("p-a")), registry.OwnerOf(new DoorId("p-b")));
        Assert.False(agents.Same("p-a", "p-b"));
        Assert.False(agents.Same("p-a", "claude-code-acp"));
        Assert.True(agents.Same("p-joined", "claude-code-acp"));
    }
}
