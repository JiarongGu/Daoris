using Daoris.Driver;

namespace Daoris.Driver.Tests;

/// <summary>
/// The room's <see cref="HelpRoomTerminalActs"/> (D233 point 1): each act the person runs at a terminal that no proposal takes, named
/// with its commands, so the helper names the command rather than calling a missing card a limit of its own.
/// </summary>
public sealed class HelpRoomTerminalActsTests
{
    [Fact]
    public void The_room_names_each_terminal_act_with_its_commands()
    {
        foreach (var machine in new[] { HelpRoomFixture.Machine, new HelpMachine() })
        {
            var agents = HelpRoom.Render(machine);

            Assert.Contains("## Done at a terminal", agents);
            Assert.DoesNotContain("still owes", agents);
            foreach (var act in HelpRoomTerminalActs.All)
            {
                Assert.Contains($"- {act.Act}: ", agents);
                foreach (var command in act.Terminal) Assert.Contains($"`{command}`", agents);
            }
        }
    }

    /// <summary>
    /// The section's place: after the doors table, so an act is read beside the screens and commands that exist; and what the helper
    /// may do sends it there when it has no card to propose.
    /// </summary>
    [Fact]
    public void The_section_follows_the_doors_and_what_it_may_do_points_at_it()
    {
        var agents = HelpRoom.Render(HelpRoomFixture.Machine);

        Assert.True(
            agents.IndexOf("## The doors", StringComparison.Ordinal) < agents.IndexOf("## Done at a terminal", StringComparison.Ordinal),
            "the terminal's acts are read after the doors.");
        Assert.Contains("*Done at a terminal*", agents[..agents.IndexOf("## The doors", StringComparison.Ordinal)]);
    }

    /// <summary>
    /// ONEWAY1ai-ii: each command is a command of the one tree, so the helper never sends the person to a verb that is not there: it
    /// is spelled <c>daoris …</c> and the tree runs its words, neither refusing them as an old form nor answering with its usage.
    /// </summary>
    [Fact]
    public void Each_command_is_one_the_tree_runs()
    {
        foreach (var command in HelpRoomTerminalActs.All.SelectMany(act => act.Terminal))
        {
            Assert.StartsWith("daoris ", command);
            var route = CommandTree.Route(command["daoris ".Length..].Split(' '));
            Assert.True(route.Kind == TreeRouteKind.Run, $"`{command}` is no command the tree runs: {route.Kind} {route.Said}");
        }
    }

    [Fact]
    public void Each_act_says_what_it_is_and_at_least_one_command()
    {
        Assert.NotEmpty(HelpRoomTerminalActs.All);
        Assert.Equal(HelpRoomTerminalActs.All.Count, HelpRoomTerminalActs.All.Select(act => act.Act).Distinct(StringComparer.Ordinal).Count());
        foreach (var act in HelpRoomTerminalActs.All)
        {
            Assert.False(string.IsNullOrWhiteSpace(act.Act));
            Assert.NotEmpty(act.Terminal);
        }
    }
}
