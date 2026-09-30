using Daoris.Driver;

namespace Daoris.Driver.Tests;

/// <summary>The room's <see cref="HelpRoomMayPropose"/>: each kind's tool with its route's rule, and each card's buttons.</summary>
public sealed class HelpRoomMayProposeTests
{
    /// <summary>
    /// HELP7: tried with the real helper, it told the person to *press Apply* on a card whose button reads
    /// *go there*. The room gives each card's buttons as the card labels them, in both languages.
    /// </summary>
    [Fact]
    public void The_room_names_each_cards_own_buttons()
    {
        var agents = HelpRoom.Render(HelpRoomFixture.Machine);

        Assert.Contains("every card but a go reads **apply** and **not now**", agents);
        Assert.Contains("a go card reads **go there** and **not now**", agents);
        Assert.Contains("**应用**", agents);
        Assert.Contains("**前往**", agents);
    }

    /// <summary>
    /// HELP6: every door built since HELP1c is a proposal too — an agent's update or pin, a delete of a
    /// record made by mistake, an account's model and effort, and a screen to open — each named with its
    /// tool and the rule its route judges it by, so the helper does not propose what would be refused.
    /// </summary>
    [Fact]
    public void The_room_names_every_kind_it_may_propose_and_the_rule_each_is_judged_by()
    {
        var agents = HelpRoom.Render(HelpRoomFixture.Machine);

        foreach (var tool in new[] { "`agent_propose`", "`delete_propose`", "`agent_settings_propose`", "`go_propose`" })
        {
            Assert.Contains(tool, agents);
        }

        // A delete: only what nobody has started on, and never a taken, done or declined quest.
        Assert.Contains("Never propose deleting a taken, done or declined quest", agents);
        Assert.Contains("the route refuses it, and declining it with the reason is the way instead", agents);
        Assert.Contains("an ask goes with every quest it became, or not at all", agents);
        // An agent: Update where the Agents screen offers it, a pin to one exact release.
        Assert.Contains("a pin names one exact release, like 2.1.300, never `latest`", agents);
        // An account: the tool's own values, and `max` never an account's default.
        Assert.Contains("`max` is for one conversation, never an account's default", agents);
        // The doors table carries the terminal twins of the new kinds (D50).
        foreach (var command in new[]
        {
            "daoris agent update <agent>", "daoris agent pin <agent> <version>",
            "daoris agent settings <agent> --account <name> model <model> effort <effort>",
            "daoris-driver quest delete <id>", "daoris-driver ask --delete <id>",
        })
        {
            Assert.Contains(command, agents);
        }
    }

    /// <summary>
    /// HELP9: a setting's doors are every `daoris driver` verb, reading and writing across among them (D107), each
    /// form said as the terminal spells it, and a write-to named as the person's standing say-so; since HELP10
    /// `retry` too, of a quest the driver parked.
    /// </summary>
    [Fact]
    public void The_room_says_how_a_setting_proposes_reading_and_writing_across()
    {
        var proposes = new HelpRoomMayPropose().Render(HelpRoomFixture.Machine);

        Assert.Contains("every door below that `daoris driver` spells.", proposes);
        Assert.DoesNotContain("but `retry`", proposes);
        Assert.Contains("`across` takes `read on|off|--clear` for a repository or a whole workspace", proposes);
        Assert.Contains("`write-to <other> [--clear]` for a repository", proposes);
        Assert.Contains("the person's standing say-so for writing across", proposes);
    }

    /// <summary>
    /// HELP10: which account an agent runs as by default, for the machine or a workspace, is an agent proposal, named
    /// with the rule it is judged by — an account the room lists — beside the command that does the same.
    /// </summary>
    [Fact]
    public void The_room_says_an_agents_default_account_is_proposed()
    {
        var agents = HelpRoom.Render(HelpRoomFixture.Machine);

        Assert.Contains("or which of its accounts it runs as by default, for the machine or one workspace", agents);
        Assert.Contains("the account one the room lists under that agent", agents);
        Assert.Contains("`daoris agent profile default <agent> <profile> [--workspace <name>]`", agents);
    }

    /// <summary>MOD6: every kind registered is a tool the room teaches, so a kind cannot be added that the helper never hears of.</summary>
    [Fact]
    public void Every_kinds_tool_is_named_in_what_it_may_propose()
    {
        var proposes = new HelpRoomMayPropose().Render(HelpRoomFixture.Machine);

        foreach (var kind in HelpProposalKinds.All) Assert.Contains($"- `{kind.Tool}`: ", proposes);
    }
}
