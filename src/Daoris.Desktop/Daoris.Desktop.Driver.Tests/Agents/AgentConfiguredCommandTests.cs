using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>The command <c>driver.json</c> names for an agent is the one every agent verb runs or asks, not the declared binary.</summary>
public sealed class AgentConfiguredCommandTests : IDisposable
{
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "agent-configured", Guid.NewGuid().ToString("N")[..8]);

    public AgentConfiguredCommandTests() => Directory.CreateDirectory(_home);

    public void Dispose() => GitSeed.Discard(_home);

    [Fact]
    public void The_configured_command_is_read_from_the_homes_driver_json_and_none_is_null()
    {
        File.WriteAllText(DriverConfig.ResolvePath(_home), """{"commands":{"claude-code":["my-claude","--flag"]}}""");

        Assert.Equal(["my-claude", "--flag"], AgentsCommand.ConfiguredCommand(_home, "claude-code"));
        Assert.Null(AgentsCommand.ConfiguredCommand(_home, "codex"));
    }

    [Fact]
    public void An_unpinned_update_runs_the_configured_command_with_the_update_arguments()
    {
        var program = new TestAgent(["declared"], ["--version"], UpdateArguments: ["update"]).Program;

        Assert.Equal(["my-claude", "--flag", "update"], AgentPinCommand.UpdateWords(program, ["my-claude", "--flag"]));
        Assert.Equal(["declared", "update"], AgentPinCommand.UpdateWords(program, null));
    }
}
