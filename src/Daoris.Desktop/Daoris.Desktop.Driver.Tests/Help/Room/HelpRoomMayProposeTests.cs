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
        // ASKDISMISS1: a go card's second press only takes the card away, and the room says nothing comes back of it.
        Assert.Contains("a go card reads **go there** and **dismiss** (in 中文 **前往** and **移除**)", agents);
        Assert.Contains("dismissing a go card only takes it out of the conversation: nothing comes back to you", agents);
        Assert.Contains("**应用**", agents);
        Assert.Contains("**前往**", agents);
        // HELP10: a delete's press is its own word, as its card labels it, and a bring-up-to-date card's first is the look.
        Assert.Contains("a delete card **delete** and **not now** (in 中文 **删除** and **暂不**)", agents);
        Assert.Contains("a bring-up-to-date card **look for updates** until the person has looked, then **apply**", agents);
        Assert.Contains("**查看更新**", agents);
    }

    /// <summary>
    /// LAYOUT8 (D170 point 1): a repository is set up by the press's own door, proposed by name: one quest to its own session and
    /// the rule its session needs, refused in `--plan`'s words, and never a shell or a write into the repository. The owner's
    /// helper could only name the terminal command; now it proposes the press.
    /// </summary>
    [Fact]
    public void The_room_says_how_a_set_up_is_proposed_and_what_it_never_does()
    {
        var proposes = new HelpRoomMayPropose().Render(HelpRoomFixture.Machine);

        Assert.Contains("- `setup_propose`: set a repository registered here up for every agent, as `daoris-driver setup <repository>`", proposes);
        Assert.Contains("one quest to that repository's own session", proposes);
        Assert.Contains("a rule letting its sessions run the doctrine tool", proposes);
        Assert.Contains("Its card shows what was read on its line and the quest's whole text", proposes);
        Assert.Contains("Daoris never writes into the repository", proposes);
        Assert.Contains("never run it yourself", proposes);
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
        Assert.Contains("Name a repository, a workspace\n  to look within, or none for every one with a checkout here.", proposes);
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
    /// ENTRY1d1 (D161's ENTRY1d note): a repository is moved to a workspace by name, a new workspace allowed, as the Manage
    /// drawer's *Move to workspace* moves it; adding or importing one needs a folder, which stays the person's pick, so a go
    /// opens the drawer with the workspace filled (ENTRY1d2a).
    /// </summary>
    [Fact]
    public void The_room_says_how_a_repository_is_moved_to_a_workspace()
    {
        var proposes = new HelpRoomMayPropose().Render(HelpRoomFixture.Machine);

        Assert.Contains("- `repository_propose`: move a repository registered here to a workspace, as its page's Manage →", proposes);
        Assert.Contains("or a new name, which the move starts", proposes);
        Assert.Contains("It changes one row of this machine's registry and touches no file.", proposes);
        Assert.Contains("Adding or importing a repository needs a folder, which stays the person's pick: `go_propose` opens", proposes);
        Assert.Contains("Repositories' `add` or `import` with the workspace filled, and the person picks the folder there.", proposes);
        // NEW1d-a: a go may open New conversation with a repository and words.
        Assert.Contains("or open New conversation with a repository and the person's words in the composer.", proposes);
        Assert.DoesNotContain("the person's pick in Repositories.", proposes);
    }

    /// <summary>
    /// SESSUX1h2b (D126 §5.2–§5.4, §7.3): sessions are archived or unarchived by id, or every one that ended archived, and a
    /// conversation that served no quest deleted, each judged as Sessions' own press is; a stop is no proposal, since a session's
    /// stop is its own conversation (D161's SESSUX1h1 note).
    /// </summary>
    [Fact]
    public void The_room_says_how_sessions_are_archived_and_a_conversation_deleted()
    {
        var proposes = new HelpRoomMayPropose().Render(HelpRoomFixture.Machine);

        Assert.Contains("- `delete_propose`: a quest or an ask made by mistake (a duplicate, a test), as the quest's drawer and the", proposes);
        Assert.Contains("  A session goes only where Sessions offers Delete…: a conversation that served no quest, ended,", proposes);
        Assert.Contains("- `session_propose`: archive sessions on this machine, or unarchive them, by id as Sessions lists them,", proposes);
        Assert.Contains("or `ended` to archive every session that ended, as the list's ⋯ → Archive what ended… does.", proposes);
        Assert.Contains("A session still running, in Waiting on you or in To review is never archived: archive never hides what", proposes);
        Assert.Contains("Stopping a session is never proposed: it is the person's own,\n  on the session's page, where `go_propose` takes them.", proposes);
        Assert.Contains("is archived instead (`session_propose`), and Ask Daoris's own conversations are deleted in its History.", proposes);
    }

    /// <summary>
    /// PAUSE1f-b (D132 §7.4): an ask's work or one quest's is paused or resumed by its id, the room saying what a pause stops
    /// and what it leaves (design §2.1); abandon is never proposed, since its reason is the person's answer (D37).
    /// </summary>
    [Fact]
    public void The_room_says_how_a_pause_is_proposed_and_that_abandon_is_the_persons_own()
    {
        var proposes = new HelpRoomMayPropose().Render(HelpRoomFixture.Machine);

        Assert.Contains("- `pause_propose`: pause one ask's work, or one quest's, on this machine, or resume it, naming the ask or", proposes);
        Assert.Contains("A pause stops what of the work runs here as the person's own stop and starts nothing of it", proposes);
        Assert.Contains("a session waiting on the person stays parked, a running intake keeps reading, and a\n  teammate's session is never reached.", proposes);
        Assert.Contains("Resume lifts the pause and releases its stops", proposes);
        Assert.Contains("Abandoning is never proposed: it declines quests with the person's reason, which is their answer,", proposes);
        Assert.Contains("`daoris-driver ask --abandon <id>`", proposes);
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
        // UX6e2: both are said where they are made now, the agent's page in the Agents place.
        Assert.Contains("an agent's Update or a pin to one version, as Agents → the agent's page → Ways in offers them", agents);
        Assert.Contains("an account's own model and effort, as Agents → the agent's page → Model and\n  effort sets them", agents);
        Assert.DoesNotContain("Settings → Agents", agents);
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
    /// POOL1b (D197 point 6, the account-pool design §5.3): the room words Daoris's accounts as one set, whose <c>use</c> and
    /// <c>order</c> name no workspace, and offers no default; HELP10 held here that an agent's default account, for the machine or a
    /// workspace, was proposed, beside <c>daoris agent profile default</c>.
    /// </summary>
    [Fact]
    public void The_room_says_an_agents_accounts_are_one_set_and_offers_no_default()
    {
        var agents = HelpRoom.Render(HelpRoomFixture.Machine);

        Assert.Contains("Daoris's accounts for an agent are one set that serves every workspace, in one order, so these name no workspace", agents);
        Assert.Contains("`order` puts the\n  accounts named first, in that order, the rest after, or with `clear` returns to the ids' order", agents);
        Assert.DoesNotContain("by default, for the machine or one workspace", agents);
        Assert.DoesNotContain("daoris agent profile default", agents);
        Assert.DoesNotContain("this machine's accounts", agents);
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

    /// <summary>
    /// HIST1f-b (D153, design §6.4): a clear names one quest, one ask or one workspace; its card lists and Apply clears exactly
    /// that, what runs or waits on the person is never cleared; HIST1f-d adds the `failed` door, a closed quest's failed sessions alone.
    /// </summary>
    [Fact]
    public void The_room_says_how_a_clear_is_proposed_and_what_it_never_takes()
    {
        var proposes = new HelpRoomMayPropose().Render(HelpRoomFixture.Machine);

        Assert.Contains("- `clear_propose`: clear finished history from this machine: one closed quest's work (`quest` and its id),\n", proposes);
        Assert.Contains("one ask's work with every quest it became (`ask` and its id), one closed quest's failed sessions alone, the quest\n", proposes);
        Assert.Contains("and its other sessions staying (`failed` and the quest's id), or a workspace's (`workspace` and its name)", proposes);
        Assert.Contains("Its card lists what would go and what stays and why; apply clears exactly what the card listed", proposes);
        Assert.Contains("A quest an ask asked is cleared with its ask, never alone", proposes);
        Assert.DoesNotContain("Clearing a quest's failed sessions alone is never proposed", proposes);
    }

    /// <summary>
    /// WORKFLOW1h-b (D157 point 13, design §8): a workflow is proposed as a whole version, new or on its newest, with its choice
    /// optional; a stale base is refused, what a step needs first makes a draft, and a merge with nobody is never proposed (§8.3).
    /// </summary>
    [Fact]
    public void The_room_says_how_a_workflow_is_proposed_and_what_makes_it_a_draft()
    {
        var proposes = new HelpRoomMayPropose().Render(HelpRoomFixture.Machine);

        Assert.Contains("- `workflow_propose`: a named workflow's whole version for a repository or a workspace (`scope`): a new\n", proposes);
        Assert.Contains("`choose` to make it the scope's workflow too.", proposes);
        Assert.Contains("A base that is not the newest is refused as stale.", proposes);
        Assert.Contains("makes the card a draft with no apply until it is.", proposes);
        Assert.Contains("Changes against a base, a kind and an import are not\n  proposed yet.", proposes);
        Assert.Contains("Merging with nobody is never proposed:", proposes);
    }

    /// <summary>MOD6: every kind registered is a tool the room teaches, so a kind cannot be added that the helper never hears of.</summary>
    [Fact]
    public void Every_kinds_tool_is_named_in_what_it_may_propose()
    {
        var proposes = new HelpRoomMayPropose().Render(HelpRoomFixture.Machine);

        foreach (var kind in HelpProposalKinds.All) Assert.Contains($"- `{kind.Tool}`: ", proposes);
    }
}
