using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The agent card's doors (AGENTPROVIDER1 row s-ix o1 to o3): the page's next start speaks for the account's own door, and says one
/// line for each other door onto the agent's accounts whose answer to *does a start run in its own home* differs, so it never says of
/// a door what is another's. The walk's own question, a door's own toolchain, is unchanged (<c>provider-owner.json</c>'s
/// <c>disagrees</c> cells name these doors).
/// </summary>
public sealed class AgentCardDoorsTests : IDisposable
{
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "agent-card", Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        if (Directory.Exists(_home)) Directory.Delete(_home, recursive: true);
    }

    private HarnessRoster Roster(string id, string harnesses)
    {
        var folder = DaorisHome.Entry(_home, HomeEntry.Plugins, id);
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, PluginCatalog.ManifestName), $$"""{ "id": "{{id}}", "harnesses": [ {{harnesses}} ] }""");
        var adapters = AdapterSet.Built().WithPlugins(PluginCatalog.Load(_home, AdapterSet.Built().Names));
        return new HarnessRoster(adapters, DaorisHome.Entry(_home, HomeEntry.Harnesses)) { Clock = () => DateTimeOffset.Parse("2026-11-06T12:00:00Z", System.Globalization.CultureInfo.InvariantCulture) };
    }

    [Fact]
    public void A_door_joined_to_claude_code_with_no_home_variable_starts_in_its_own_home_where_the_card_owes_an_account()
    {
        var roster = Roster("p.claude", """{ "name": "p-claude-bare", "command": ["agent"], "accountOf": "claude-code" }""");

        var next = roster.Next("claude-code");

        Assert.Equal(NextReason.NoAccount, next.Reason);
        Assert.Equal([new DoorStart("p-claude-bare", true)], next.Doors);
    }

    [Fact]
    public void A_door_joined_to_dsh_that_declares_its_own_home_is_said_apart_from_dsh_s_card()
    {
        var roster = Roster("p.dsh", """{ "name": "p-dsh", "command": ["agent"], "accountOf": "dsh", "profileVariable": "P_DSH_HOME", "ownHome": true }""");

        var next = roster.Next("dsh");

        Assert.Equal(NextReason.NoAccount, next.Reason);
        Assert.Equal([new DoorStart("p-dsh", true)], next.Doors);
    }

    [Fact]
    public void A_door_with_its_own_variable_beside_the_account_s_own_door_that_has_none_is_said_to_owe_an_account()
    {
        var roster = Roster("p.acp", """
            { "name": "p-a", "command": ["agent"], "accountOf": "claude-code-acp" },
            { "name": "p-b", "command": ["agent"], "accountOf": "claude-code-acp", "profileVariable": "B_HOME" }
            """);

        var next = roster.Next("claude-code-acp");

        Assert.NotEqual(NextReason.NoAccount, next.Reason);
        Assert.Equal([new DoorStart("p-b", false)], next.Doors);
    }

    [Fact]
    public void No_built_agent_has_a_door_that_answers_differently()
    {
        var roster = new HarnessRoster(AdapterSet.Built(), DaorisHome.Entry(_home, HomeEntry.Harnesses));

        Assert.All(new[] { "claude-code", "codex", "dsh" }, agent => Assert.Empty(roster.Next(agent).Doors));
    }
}
