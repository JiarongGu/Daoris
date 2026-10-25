using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The ports the instance declares and the subdomains above it answer (FOUNDATION1 mod-vi): how a verb's own command reads its lease. Each
/// subdomain that owns a command installs its answer when the driver's assembly loads, so every verb holds exactly what it always did; a
/// verb nobody answers is unclassed and holds the lease.
/// </summary>
public sealed class InstancePortsTests
{
    [Fact]
    public void The_verbs_whose_commands_a_higher_subdomain_owns_are_answered_when_the_assembly_loads()
    {
        Assert.Equal(["access", "agent", "checks", "git", "heavy", "plugin", "work", "workflow"], VerbLeases.Answered.Keys.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void The_agents_answer_is_its_commands_reading_of_what_changes_a_home_what_its_verbs_are_and_what_acts_on_a_wiring_files_home()
    {
        var reading = VerbLeases.Answered["agent"];

        Assert.Same(AgentsCommand.Verbs, reading.Verbs);
        Assert.True(reading.Holds(["rules", "allow", "WebFetch"]));
        Assert.False(reading.Holds(["rules"]));
        string? Named(string name) => name == "DAORIS_HARNESS_CONFIG" ? "wiring.json" : null;
        Assert.True(reading.Isolated!(["settings", "claude-code"], Named));
        Assert.False(reading.Isolated!(["settings", "claude-code"], _ => null));
        Assert.False(reading.Isolated!(["rules", "allow", "WebFetch"], Named));
    }

    [Fact]
    public void The_participants_answer_is_the_access_commands_reading_of_a_change_and_names_no_second_word()
    {
        var reading = VerbLeases.Answered["access"];

        Assert.True(reading.Holds(["engine", "full"]));
        Assert.True(reading.Holds(["engine", "--clear"]));
        Assert.False(reading.Holds(["engine"]));
        Assert.False(reading.Holds([]));
        Assert.Null(reading.Verbs);
        Assert.Null(reading.Isolated);
    }

    [Fact]
    public void The_plugins_answer_is_its_commands_reading_of_a_short_change_and_its_verbs()
    {
        var reading = VerbLeases.Answered["plugin"];

        Assert.Same(PluginCommand.Verbs, reading.Verbs);
        Assert.True(reading.Holds(["add", "D:/work/acme.gate"]));
        Assert.True(reading.Holds(["update", "acme.gate", "--yes"]));
        Assert.False(reading.Holds(["update", "acme.gate"]));
        Assert.False(reading.Holds([]));
        Assert.Null(reading.Isolated);
    }

    [Fact]
    public void The_reviews_answers_are_the_heavy_and_checks_commands_readings_of_a_change()
    {
        var heavy = VerbLeases.Answered["heavy"];
        var checks = VerbLeases.Answered["checks"];

        Assert.True(heavy.Holds(["engine", "build=x"]));
        Assert.True(heavy.Holds(["engine", "--clear"]));
        Assert.False(heavy.Holds([]));
        Assert.False(heavy.Holds(["engine"]));
        Assert.True(checks.Holds(["engine", "light"]));
        Assert.True(checks.Holds(["engine", "--clear"]));
        Assert.False(checks.Holds([]));
        Assert.False(checks.Holds(["engine"]));
        Assert.All(new[] { heavy, checks }, reading =>
        {
            Assert.Null(reading.Verbs);
            Assert.Null(reading.Isolated);
        });
    }

    [Fact]
    public void The_workflows_answers_are_the_words_of_a_nodes_door_and_a_held_attempts_resolutions()
    {
        var workflow = VerbLeases.Answered["workflow"];
        var work = VerbLeases.Answered["work"];

        Assert.True(workflow.Holds(["keep", "s-1"]));
        Assert.True(workflow.Holds([NodeWordActs.Skip, "q7", "go", "nobody raises it"]));
        Assert.True(workflow.Holds([NodeWordActs.Stop, "q7", "go"]));
        Assert.False(workflow.Holds(["run", "s-1"]));
        Assert.False(workflow.Holds([]));

        Assert.True(work.Holds(["recovery", "drop"]));
        Assert.All(HeldResolutions.All, resolution => Assert.True(work.Holds(["held", "q7", resolution])));
        Assert.False(work.Holds(["held", "q7"]));
        Assert.False(work.Holds(["state"]));
        Assert.False(work.Holds([]));
        Assert.All(new[] { workflow, work }, reading =>
        {
            Assert.Null(reading.Verbs);
            Assert.Null(reading.Isolated);
        });
    }

    [Fact]
    public void The_workplaces_answer_is_the_git_commands_reading_of_what_changes_an_identity_or_a_default()
    {
        var git = VerbLeases.Answered["git"];

        Assert.True(git.Holds(["identity", "add-ssh", "github.com", "--label", "work"]));
        Assert.True(git.Holds(["default", "github.com", "work"]));
        Assert.True(git.Holds(["use", "engine", "own"]));
        Assert.False(git.Holds(["identity", "list"]));
        Assert.False(git.Holds(["identity", "public-key", "work"]));
        Assert.False(git.Holds(["branches"]));
        Assert.Null(git.Verbs);
        Assert.Null(git.Isolated);
    }
}
