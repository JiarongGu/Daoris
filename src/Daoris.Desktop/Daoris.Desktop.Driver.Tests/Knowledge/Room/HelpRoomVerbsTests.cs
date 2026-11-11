using Daoris.Driver;

namespace Daoris.Driver.Tests;

/// <summary>
/// The room's <see cref="HelpRoomVerbs"/>: under <i>What you may do</i>, each verb Ask Daoris presses with the rule its route's view
/// judges it by, and no tool, card or button of a proposal.
/// </summary>
public sealed class HelpRoomVerbsTests
{
    private static string Verbs => new HelpRoomVerbs().Render(HelpRoomFixture.Machine);

    /// <summary>The verbs sit under <i>What you may do</i>, which names them alone: no proposal section, tool or card of one.</summary>
    [Fact]
    public void What_you_may_do_names_the_verbs_alone()
    {
        var room = HelpRoom.Render(HelpRoomFixture.Machine);

        Assert.Contains("### The verbs you press\n", room);
        Assert.True(room.IndexOf("## What you may do", StringComparison.Ordinal) < room.IndexOf("### The verbs you press", StringComparison.Ordinal));
        Assert.DoesNotContain("## What you may propose", room);
        Assert.DoesNotContain("_propose`", room);
        Assert.DoesNotContain("never proposed", room);
        Assert.DoesNotContain("**go there**", room);
        Assert.DoesNotContain("Apply and Not now", room);
    }

    /// <summary>A repository with no rooms is offered them through its own session; the room drafts nothing itself.</summary>
    [Fact]
    public void The_room_asks_for_rooms_and_classes_for_a_repository_with_none()
    {
        Assert.Contains("declares no rooms (no lane with `builds`) is offered them", Verbs);
        Assert.Contains("is offered them by `daoris ask` at its workspace", Verbs);
        Assert.Contains("by the doctrine's `rooms-and-classes` knowledge, on its own branch", Verbs);
        Assert.Contains("a set-up's adoption asks the same", Verbs);
    }

    /// <summary>Each verb is said as pressed, as its screen does it, with the rule its route's view judges it by.</summary>
    [Theory]
    [InlineData("- Every setting is pressed, its card the press's own sentence and command", "`daoris place review|opinion|helpers|commits <repository> …`")]
    [InlineData("- Something to start is asked by pressing", "`--review rule|on|<environment>|off`")]
    [InlineData("- A place's set-up is applied by pressing", "a rule letting its sessions run the doctrine tool")]
    [InlineData("- Repositories are brought up to date after a pull request merged by pressing", "row that moves. Daoris never pushes.")]
    [InlineData("- A repository registered here is moved to a workspace by pressing:", "one the registry would refuse is answered to you at once and never shown.")]
    [InlineData("- A quest or an ask made by mistake (a duplicate, a test) is deleted by pressing:", "an ask goes with every quest it became, or not at all")]
    [InlineData("- Sessions are archived or unarchived by pressing: `daoris sessions archive <id>…` and", "Only a done or failed session is archived; one working, pending or needing you never is")]
    [InlineData("- One ask's work, or one quest's, is paused on this machine or resumed by pressing:", "Resume lifts the pause and releases its stops")]
    [InlineData("- Finished history is cleared from this machine by pressing", "A quest an ask asked is cleared with its ask, never alone")]
    [InlineData("- A workflow's new version is saved by pressing,", "press `daoris workflow apply <id> drafts/<id>.json --base <version>`")]
    [InlineData("- Daoris's browser's settings are pressed, as Settings → Browser sets them", "Each but `links` holds from the browser's next start.")]
    [InlineData("- An agent's Update or a pin to one version is pressed, as Agents → the agent's page → Ways in offers them", "exact release, like 2.1.300, never `latest`")]
    [InlineData("- How Daoris's accounts for an agent are used, ordered and ended early are pressed, and name no workspace", "`daoris agent account order <agent> …`")]
    [InlineData("- An account's own model and effort are pressed", "`max` is for one conversation, never an account's default")]
    public void Each_verb_is_said_as_pressed_with_its_rule(string verb, string rule)
    {
        Assert.Contains(verb, Verbs);
        Assert.Contains(rule, Verbs);
    }

    /// <summary>
    /// What stays the person's own press is said so, and a link takes them where it is made: a departure's yes, a landed branch's
    /// hand-off, a session's stop, an abandon's reason, and the folder an added repository is picked from.
    /// </summary>
    [Fact]
    public void The_person_s_own_presses_are_reached_by_a_link()
    {
        Assert.Contains("- Never pressed for the person: accepting a quest's departure, and handing a landed branch to a landing plugin.", Verbs);
        Assert.Contains("on the quest's page and in the review, where a link takes them.", Verbs);
        Assert.Contains("Stopping a session is never pressed for the person: it is their own, on the session's page, where a link takes them.", Verbs);
        Assert.Contains("Abandoning is never pressed for the person: it declines quests with the person's reason, which is their answer,", Verbs);
        Assert.Contains("Adding or importing a repository needs a folder, which stays the person's pick: a link opens", Verbs);
        Assert.Contains("Repositories' `add` or `import` with the workspace filled, and the person picks the folder there.", Verbs);
        Assert.Contains("Daoris never writes into a repository", Verbs);
    }

    /// <summary>The room offers no default account, for the machine or a workspace, and no <c>daoris agent account default</c>.</summary>
    [Fact]
    public void The_room_offers_no_default_account()
    {
        var room = HelpRoom.Render(HelpRoomFixture.Machine);

        Assert.DoesNotContain("by default, for the machine or one workspace", room);
        Assert.DoesNotContain("daoris agent account default", room);
        Assert.DoesNotContain("Settings → Agents", room);
        foreach (var command in new[]
        {
            "daoris agent update <agent>", "daoris agent pin <agent> <version>",
            "daoris agent settings <agent> --account <name> model <model> effort <effort>",
            "daoris quest delete <id>", "daoris ask --delete <id>",
        })
        {
            Assert.Contains(command, room);
        }
    }
}
