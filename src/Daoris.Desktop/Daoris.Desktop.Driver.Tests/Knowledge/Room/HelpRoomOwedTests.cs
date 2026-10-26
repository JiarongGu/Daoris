using Daoris.Driver;

namespace Daoris.Driver.Tests;

/// <summary>
/// The room's <see cref="HelpRoomOwed"/> (HELPOWED1, D170 point 2): each act a person can start that Ask Daoris cannot propose
/// yet, named with its terminal command, and said to be a door Daoris still owes rather than a limit of the helper's.
/// </summary>
public sealed class HelpRoomOwedTests
{
    /// <summary>
    /// D170: asked to set a registered repository up, the helper said it had no shell and no door to propose it, as if that
    /// were the design. The room now lists each door owed, its command, and that Daoris owes it, so the helper says so.
    /// </summary>
    [Fact]
    public void The_room_names_each_door_owed_with_its_command_and_says_daoris_still_owes_it()
    {
        foreach (var machine in new[] { HelpRoomFixture.Machine, new HelpMachine() })
        {
            var agents = HelpRoom.Render(machine);

            Assert.Contains("## Doors Daoris still owes", agents);
            Assert.Contains("Daoris still owes", agents);
            Assert.Contains("not a limit of yours", agents);
            foreach (var owed in HelpRoomOwed.All)
            {
                Assert.Contains($"- {owed.Act}: ", agents);
                foreach (var command in owed.Terminal) Assert.Contains($"`{command}`", agents);
            }
        }
    }

    /// <summary>
    /// The door the owner met (D170 point 1), setting a repository up, is LAYOUT8's `setup` kind's now, so it is owed no more;
    /// registering one from its line, WSSETUP7's, still is, by its headless verb.
    /// </summary>
    [Fact]
    public void A_door_built_leaves_the_list_and_registering_a_repository_is_still_owed()
    {
        var agents = HelpRoom.Render(new HelpMachine());
        var owed = agents[agents.IndexOf("## Doors Daoris still owes", StringComparison.Ordinal)..];

        Assert.DoesNotContain("`daoris-driver setup <repository> [--plan]`", owed);
        Assert.Contains("`daoris-driver register [--repository <name>]`", owed);
        Assert.Contains(HelpRoomOwed.Register, HelpRoomOwed.All);
    }

    /// <summary>
    /// The section's place: after the doors table, so a door owed is read beside the screens and commands that exist; and
    /// what the helper may do sends it there when it has no door to propose.
    /// </summary>
    [Fact]
    public void The_section_follows_the_doors_and_what_it_may_do_points_at_it()
    {
        var agents = HelpRoom.Render(HelpRoomFixture.Machine);

        Assert.True(
            agents.IndexOf("## The doors", StringComparison.Ordinal) < agents.IndexOf("## Doors Daoris still owes", StringComparison.Ordinal),
            "the doors owed are read after the doors.");
        Assert.Contains("*Doors Daoris still owes*", agents[..agents.IndexOf("## The doors", StringComparison.Ordinal)]);
    }

    /// <summary>
    /// Each command is spelled as its own usage spells it, so the helper never sends the person to a verb that is not there:
    /// the headless host's by <see cref="DriverCommand.Usage"/>, the CLI's by its command table.
    /// </summary>
    [Fact]
    public void Each_command_owed_is_spelled_as_its_usage_spells_it()
    {
        string Cli(string file) => File.ReadAllText(Path.Combine(HelpProposalKindsTests.RepositoryRoot(), "src", "Daoris.Cli", "src", "cli", file));
        var usages = new (string Prefix, string Usage)[]
        {
            ("daoris-driver ", DriverCommand.Usage),
            ("daoris driver ", Cli("driver.ts")),
            ("daoris agent ", Cli("agent.ts")),
            ("daoris tool ", Cli("tool.ts")),
        };

        foreach (var command in HelpRoomOwed.All.SelectMany(owed => owed.Terminal))
        {
            var (prefix, usage) = usages.SingleOrDefault(each => command.StartsWith(each.Prefix, StringComparison.Ordinal));
            Assert.True(prefix is not null, $"`{command}` is no command a usage this test reads spells.");
            Assert.True(usage.Contains(command[prefix.Length..], StringComparison.Ordinal), $"its usage no longer spells `{command}`.");
        }
    }

    /// <summary>Each row says what the act is, at least one command, and why the door is owed, in a sentence.</summary>
    [Fact]
    public void Each_door_owed_says_its_act_its_command_and_why()
    {
        Assert.NotEmpty(HelpRoomOwed.All);
        Assert.Equal(HelpRoomOwed.All.Count, HelpRoomOwed.All.Select(owed => owed.Act).Distinct(StringComparer.Ordinal).Count());
        foreach (var owed in HelpRoomOwed.All)
        {
            Assert.False(string.IsNullOrWhiteSpace(owed.Act));
            Assert.NotEmpty(owed.Terminal);
            Assert.True(owed.Reason.Length >= 40 && owed.Reason.EndsWith('.'), $"a reason is a sentence that says why: `{owed.Reason}`.");
        }
    }
}
