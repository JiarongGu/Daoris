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

        Assert.Contains("every other card reads **apply** and **not now**", agents);
        Assert.Contains("a go card reads **go there** and **not now**", agents);
        Assert.Contains("**应用**", agents);
        Assert.Contains("**前往**", agents);
        // HELP10: a delete's press is its own word, as its card labels it, and a bring-up-to-date card's first is the look.
        Assert.Contains("a delete card **delete** and **not now** (in 中文 **删除** and **暂不**)", agents);
        Assert.Contains("a bring-up-to-date card **look for updates** until the person has looked, then **apply**", agents);
        Assert.Contains("**查看更新**", agents);
    }

    /// <summary>
    /// HELP10: WSR6's *Bring up to date* (D109) is proposed, and the room says its card's two presses: the look, which is
    /// the person's and fetches, and the Apply, on the rows the look listed only.
    /// </summary>
    [Fact]
    public void The_room_says_how_bringing_repositories_up_to_date_is_proposed()
    {
        var proposes = new HelpRoomMayPropose().Render(HelpRoomFixture.Machine);

        Assert.Contains("- `sync_propose`: bring repositories up to date after a pull request merged", proposes);
        Assert.Contains("Its card asks the person to look first, which fetches each line as them", proposes);
        Assert.Contains("apply acts on those rows only. Daoris never pushes.", proposes);
    }

    /// <summary>
    /// DRIFT1d2 (D133 §4): the person's yes to a done's departure is proposed only when they ask for it, of a quest
    /// <c>quest_list</c> shows held, and the room says its card's press as the card labels it.
    /// </summary>
    [Fact]
    public void The_room_says_how_a_yes_to_a_departure_is_proposed()
    {
        var proposes = new HelpRoomMayPropose().Render(HelpRoomFixture.Machine);

        Assert.Contains("- `accept_propose`: the person's yes to a quest a done's departure holds", proposes);
        Assert.Contains("only when the person asks to accept it", proposes);
        Assert.Contains("an accept card **accept** and **not now** (in 中文 **采纳** and **暂不**)", proposes);
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
        Assert.Contains("`daoris agent profile default <agent> <profile>|--clear [--workspace <name>]`", agents);
    }

    /// <summary>HELP10: the browser's settings, each named as the terminal spells it, and when each holds.</summary>
    [Fact]
    public void The_room_says_how_the_browsers_settings_are_proposed()
    {
        var proposes = new HelpRoomMayPropose().Render(HelpRoomFixture.Machine);

        Assert.Contains("- `browser_propose`: Daoris's browser's settings, as Settings → Browser sets them", proposes);
        Assert.Contains("`use` `daoris` or `edge`", proposes);
        Assert.Contains("a `favorite` to `add` (its address, and a title if wanted) or `remove` (one the list below keeps)", proposes);
        Assert.Contains("Each but `links` holds from the browser's next start.", proposes);
    }

    /// <summary>MOD6: every kind registered is a tool the room teaches, so a kind cannot be added that the helper never hears of.</summary>
    [Fact]
    public void Every_kinds_tool_is_named_in_what_it_may_propose()
    {
        var proposes = new HelpRoomMayPropose().Render(HelpRoomFixture.Machine);

        foreach (var kind in HelpProposalKinds.All) Assert.Contains($"- `{kind.Tool}`: ", proposes);
    }
}
