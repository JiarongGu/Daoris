using Daoris.Driver;

namespace Daoris.Driver.Tests;

/// <summary>The room's <see cref="HelpRoomDoors"/>: each change, its screen, and the command that does the same.</summary>
public sealed class HelpRoomDoorsTests
{
    /// <summary>
    /// The doors (D50): every change the room speaks of is a screen and the terminal command that does the
    /// same, spelled as the CLI spells it — a helper that invented a verb would send the person to nothing.
    /// </summary>
    [Fact]
    public void The_room_names_the_screen_and_the_terminal_command_for_each_change()
    {
        var agents = HelpRoom.Render(HelpRoomFixture.Machine);

        foreach (var command in new[]
        {
            "daoris place drive|undrive <repository>", "daoris place hold|resume <repository>",
            "daoris place trees <repository> on|off", "daoris place line <repository> <branch>|--clear",
            "daoris place landing <repository> merge|branch <pattern>|--clear",
            // ONEWAY1a: a role's agents through the role door alone, Build's, Intake's and Ask Daoris's among them.
            "daoris role set <role> \"<agent> [<model>|-] [<effort>|-]\" …|off",
            "daoris settings notify on|off", "daoris agent rules", "daoris ask --workspace <name>",
            // BRSCOPE1a: the clean-up and bringing up to date take one workspace's checkouts with `--workspace`.
            "daoris tree clean [--workspace <name>]",
            // WSR6: bringing a repository up to date after its pull request merged; WSR7 (D112): every repository with `--all`.
            "daoris tree sync [--repository <name>] [--workspace <name>] [--all] [--yes]",
            // D107: reading and writing across, the CLI's `driver across` verbs.
            "daoris place across <repository> read on|off|--clear", "daoris place across <repository> write-to <other> [--clear]",
            // HELP9: every setting verb that changes something, and the doors Ask Daoris owes, named where they are.
            // SESSUX1b: and a stop's release, which names the session stopped.
            "daoris quest retry <quest>", "daoris quest retry <quest> --session <id>", "daoris settings cap <n>",
            // LEFT3's default and its clear left with POOL1b (D197 point 6).
            "daoris browser use daoris|edge", "daoris browser links system|daoris",
            // WORKFLOWCLEAN1 1f: the one adoption door, Ask Daoris's `setup` kind through its `place` door, its screen the place's Set up….
            "daoris place <repository>… --preset keep --adoption [--what-it-builds <kind>] --plan",
            // TOOL4e (D125 §6): the order, *Try now* and the default cool-off, the terminal's doors (on the screen since TOOL4g).
            "daoris agent account order <agent> <profile>…|--clear",
            "daoris agent account ready <agent> <profile>|--own", "daoris settings cooloff <minutes>", "daoris settings quiet <minutes>",
            // TOOL6a (D130 §16.6): how a list is used, the terminal's door (on the screen since TOOL4g); its switch and the
            // person's curve since SWITCH1d.
            "daoris agent account use <agent> [goal|order] [--keep <account>|--no-keep] [--switch smart|curve|off] "
                + "[--curve <window>=<elapsed>:<used>,…] [--no-curve]",
            // REVIEWENV1j (D154 point 3, D50): the person's review choice, the composer's, the ask page's and the gate's.
            "daoris ask … --short \"…\"", "daoris ask … --review rule|on|<environment>|off","daoris ask review <id> on|<environment>|off",
            "daoris quest review <quest> off [\"…\"]", "daoris quest review <quest> on|<environment>",
            // WORKFLOW1e (D157 point 10): which workflow work follows and a workspace's kinds, and an ask's kind and workflow.
            "daoris workflow use <id>|--clear --repository <name>|--workspace <name> [--kind <kind>|--landed]",
            "daoris workflow kind <workspace> <kind>", "daoris ask … --kind <kind> [--workflow <id>|current]",
            "daoris ask workflow <id>",
            // XAGENT1f4 (D155 point 10, design §9): the person's presses at the second opinion's gate, which Ask Daoris names and
            // proposes none of.
            "daoris session opinion ask <session> [--agent <adapter>] [--same-agent] [\"…\"]",
            "daoris session opinion show <session|opinion>", "daoris session opinion stop <opinion>",
            "daoris session opinion anyway <session> [\"…\"]", "daoris session opinion myself <session> [\"…\"]",
        })
        {
            Assert.Contains(command, agents);
        }

        Assert.Contains("Repositories → the workspace's page → Setup → Defaults → Line", agents);
        // HELP9: a parked quest's Retry is on its quest's page, which the room names beside the command (HELP10: and
        // proposes). SESSUX1b: FRAME1d made the drawer a page; the row said a drawer until then.
        Assert.Contains("Quests → the quest's page → Try again", agents);
        Assert.DoesNotContain("the quest's drawer → Try again", agents);
        // What a landing pattern may say, as `LandingRules` reads it — the first real conversation had to guess.
        foreach (var token in new[] { "{quest}", "{session}", "{slug}", "{repository}" }) Assert.Contains(token, agents);
        // COMPANYVIEW1b: the working roles' settings are in the Agents place's Roles mode, not on AI features.
        Assert.Contains("Agents → Roles → Working roles", agents);
        Assert.DoesNotContain("Settings → AI features → Actors", agents);
        Assert.DoesNotContain("Role catalogue", agents);
        // SETUP1b: the setup guide the person may be walked through, by the names the window gives it; Help's since UX7a
        // (D152), where the Daoris menu held it. UX6i2a: named Get started since UX6j (D150 §2).
        Assert.Contains("Settings → Get started (Help → *Get started*)", agents);
        Assert.DoesNotContain("Settings → Setup", agents);
        Assert.DoesNotContain("Daoris menu", agents);
        // UX6i2a: a plugin is switched, installed and updated in the Plugins place since UX6j retired Settings → Plugins.
        Assert.Contains("Plugins → the plugin's page → Turn on or Turn off", agents);
        Assert.Contains("Plugins → Daoris's own plugins → Install", agents);
        Assert.DoesNotContain("Settings → Plugins", agents);
        // It reads and advises; the moves that stay the person's are named as never its own.
        Assert.Contains("You change nothing yourself", agents);
        // It proposes (HELP1c): a card the person applies, through the connector's two tools.
        Assert.Contains("`setting_propose`", agents);
        Assert.Contains("`ask_propose`", agents);
        Assert.Contains("push, merge, discard, sign in", agents);
    }

    /// <summary>
    /// UX6e2 and HELPSETUP1 (D150 §3.1): a change is named where it is made now. An agent's accounts, ways in and model are
    /// on its page in the Agents place, and a repository's own driving, line, landing, session language, standing answer,
    /// reach and rules on its Setup; a workspace's default keeps its Settings home until its own page takes it (UX6g).
    /// </summary>
    [Fact]
    public void Each_change_is_named_on_its_home_an_agents_on_its_page_and_a_repositorys_on_its_setup()
    {
        var screens = HelpRoomDoors.Doors.ToDictionary(door => door.To, door => door.Screen);
        string Screen(string to) => screens.Single(each => each.Key.StartsWith(to, StringComparison.Ordinal)).Value;

        Assert.DoesNotContain(HelpRoomDoors.Doors, door => door.Screen.Contains("Settings → Agents", StringComparison.Ordinal));
        Assert.Equal("Agents → the agent's page → Accounts", Screen("sign an agent in"));
        Assert.Equal("Agents → the agent's page → Ways in → Update, Pin a version", Screen("update an agent"));
        Assert.Equal("Agents → the agent's page → Model and effort", Screen("set an account's own model"));
        Assert.Equal("Agents → the agent's page → the account's Try now", Screen("end an account's cool-off"));
        Assert.StartsWith("Agents → the agent's page → What it may do", Screen("allow, ask or deny"));

        Assert.Equal("Repositories → the repository's page → Setup → Driving", Screen("drive a repository"));
        Assert.Equal("Repositories → the repository's page → Setup → Driving", Screen("hold one"));
        Assert.Equal("Repositories → the repository's page → Setup → Driving", Screen("give its sessions their own tree"));
        Assert.Equal(
            "Repositories → the repository's page → Setup → Line and landing; a workspace's, Repositories → the workspace's page → Setup → Defaults → Line",
            Screen("set the line"));
        Assert.Equal(
            "Repositories → the repository's page → Setup → Line and landing; a workspace's, Repositories → the workspace's page → Setup → Defaults → How work lands",
            Screen("set how accepted work lands"));
        Assert.Equal(
            "Repositories → the repository's page → Setup → Reach; a workspace's reading, Repositories → the workspace's page → Setup → Defaults → Read by agents outside",
            Screen("let agents read"));
        Assert.Equal("Repositories → the repository's page → Setup → Sessions", Screen("keep a standing answer"));
        Assert.Equal(
            "Repositories → the repository's page → Setup → Sessions; a workspace's, Repositories → the workspace's page → Setup → Defaults → Session language",
            Screen("set the language"));
        Assert.Contains("a repository's, Repositories → the repository's page → Setup → Reach", Screen("allow, ask or deny"));
        // UX6g2a: a workspace's session branches and updates are on its Branches tab, its rules on Setup → Remote and reach.
        Assert.Equal("Repositories → the workspace's page → Branches → Updates", Screen("bring a repository up to date"));
        Assert.Equal("Repositories → the workspace's page → Branches → Session branches", Screen("clean up session branches"));
        Assert.EndsWith("a workspace's, Repositories → the workspace's page → Setup → Remote and reach", Screen("allow, ask or deny"));
        Assert.DoesNotContain(HelpRoomDoors.Doors, door => door.Screen.Contains("Settings → Workspace", StringComparison.Ordinal)
            || door.Screen.Contains("Settings → Permissions", StringComparison.Ordinal));
    }

    /// <summary>
    /// WORKFLOW1h-b (D157 point 13, design §8.4): a named workflow's whole version and its choice are Ask Daoris's `workflow` kind's
    /// `new`, `edit` and `use` doors, which the person applies; an import and a kind are WORKFLOW1h2's, which the rows say too.
    /// </summary>
    [Fact]
    public void The_room_says_ask_daoris_proposes_a_workflow_s_version_and_its_choice()
    {
        var named = HelpRoomDoors.Doors.Single(door => door.Terminal.StartsWith("`daoris workflow new <id>", StringComparison.Ordinal));
        Assert.Equal(("workflow", "new"), named.Ask?.Through);
        Assert.EndsWith("(Ask Daoris proposes it, a whole version, new or after the newest, which you apply; not an import yet)", named.To);

        var chosen = HelpRoomDoors.Doors.Single(door => door.Terminal.StartsWith("`daoris workflow use <id>", StringComparison.Ordinal));
        Assert.Equal(("workflow", "use"), chosen.Ask?.Through);
        Assert.EndsWith("(Ask Daoris proposes it, the choice with a version it proposes, as a repository's or a workspace's default; not a "
            + "kind yet)", chosen.To);
    }

    /// <summary>
    /// WORKFLOWCLEAN1 1i (D227): the room teaches one way. The conversion has its door, which opens the page and never proposes the
    /// apply; the old landing rule is a place still on Current's, which converting retires; a workflow is made from a default or
    /// another saved one, never edited, and `current` is chosen for no development route.
    /// </summary>
    [Fact]
    public void The_room_teaches_the_converted_way_and_names_the_conversion()
    {
        var convert = HelpRoomDoors.Doors.Single(door => door.Terminal.Contains("daoris convert --plan", StringComparison.Ordinal));
        Assert.Contains("Convert…", convert.Screen);
        Assert.Contains("Review conversion…", convert.Screen);
        Assert.IsType<HelpDoorAsk.Never>(convert.Ask);
        Assert.Contains("it opens the page and names the plan, and the apply is your press", convert.To);

        var landing = HelpRoomDoors.Doors.Single(door => door.To.StartsWith("set how accepted work lands", StringComparison.Ordinal));
        Assert.Contains("only a place still on Current", landing.To);
        Assert.Contains("converting retires", landing.To);

        var show = HelpRoomDoors.Doors.Single(door => door.Terminal.StartsWith("`daoris workflow show --repository", StringComparison.Ordinal));
        Assert.DoesNotContain("read from its rules", show.To);

        var created = HelpRoomDoors.Doors.Single(door => door.Terminal.StartsWith("`daoris workflow new <id>", StringComparison.Ordinal));
        Assert.Contains("--from <default>|<id>[@<version>]", created.Terminal);
        Assert.DoesNotContain("current:", created.Terminal);
        Assert.DoesNotContain("workflow edit", created.Terminal);

        var chosen = HelpRoomDoors.Doors.Single(door => door.Terminal.StartsWith("`daoris workflow use <id>", StringComparison.Ordinal));
        Assert.DoesNotContain("|current|", chosen.Terminal);
        Assert.Contains("no development route's choice", chosen.To);
    }

    /// <summary>
    /// WORKFLOW1c4 (D157 point 12; the workflow design §6.1, §7): the room named none of the places how work moves is drawn, so
    /// asked how a repository's work lands, or where a quest's work stands, the helper could not send the person there. Current
    /// is a repository's and a workspace's *Workflow* tab, with `daoris workflow show` its twin (WORKFLOW1a, WORKFLOW1b);
    /// a run is the session's *Workflow* view, the *Workflow:* line in a quest's and an ask's head, and What needs you's
    /// *Workflow* door, with `daoris workflow run` its twin (WORKFLOW1c, WORKFLOW1c2). Both only read, so Ask Daoris
    /// proposes neither; changing a workflow is WORKFLOW1h's proposal, and today a step opens the Setup row that sets it.
    /// </summary>
    [Fact]
    public void The_room_names_where_how_work_moves_is_drawn_and_where_a_run_stands_with_their_terminal_twins()
    {
        var current = HelpRoomDoors.Doors.Single(door => door.Terminal.StartsWith("`daoris workflow show --repository", StringComparison.Ordinal));
        Assert.Equal("`daoris workflow show --repository <name>|--workspace <name>`", current.Terminal);
        Assert.Equal("Repositories → the repository's page → Workflow; a workspace's, Repositories → the workspace's page → Workflow",
            current.Screen);
        foreach (var said in new[]
        {
            "who takes part (agent alone, agent + you, you, automatic)", "where each was set", "what Daoris cannot do of it yet",
            "or for Current the Setup row that sets it", "Ask Daoris never proposes it",
        })
        {
            Assert.Contains(said, current.To);
        }

        var run = HelpRoomDoors.Doors.Single(door => door.Terminal.Contains("daoris workflow run", StringComparison.Ordinal));
        Assert.Equal("`daoris workflow run --session|--quest|--ask <id>`, `daoris workflow run --line <repository>`", run.Terminal);
        Assert.Contains("a repository's line, its newest verifications", run.To);
        foreach (var said in new[]
        {
            "Sessions → the session's *Workflow* view", "beside Timeline and Review", "View → Workflow",
            "*Workflow:* in a quest's page's head and an ask's", "*Workflow* on a row of What needs you",
            "a repository's *Verifications* on its Workflow tab",
        })
        {
            Assert.Contains(said, run.Screen);
        }

        foreach (var said in new[] { "each step's state", "the step the run stands at", "Ask Daoris never proposes it", "its owner's" })
        {
            Assert.Contains(said, run.To);
        }

        // In the order a person meets them: how work moves beside the rules it is drawn from, before a named workflow; where a
        // piece of work stands after starting one, before saying something to a session, a park's answer among them (ONEWAY1k).
        var order = HelpRoomDoors.Doors.Select(door => door.To).ToList();
        int At(string to) => order.FindIndex(each => each.StartsWith(to, StringComparison.Ordinal));
        Assert.Equal(At("declare which other agent reads") + 1, order.IndexOf(current.To));
        Assert.Equal(order.IndexOf(current.To) + 1, At("name a workflow"));
        Assert.True(order.IndexOf(run.To) > At("start a task") && order.IndexOf(run.To) < At("say something to a session"));

        // WORKFLOW1d's row keeps the named workflows' verbs, its `show` the one that names a saved workflow, so the two shows
        // are not read as one.
        var named = HelpRoomDoors.Doors.Single(door => door.To.StartsWith("name a workflow", StringComparison.Ordinal));
        Assert.Contains("`daoris workflow list`, `daoris workflow show <id>[@<version>]`", named.Terminal);
        Assert.DoesNotContain("workflow list|show`", named.Terminal);
    }

    /// <summary>
    /// WORKFLOW1f (D157 points 10 and 11, the workflow design §4.4–§4.5): the choice's row says the gate follows the version each
    /// run bound, never that no gate reads it; and the person's <i>Keep</i> is named, yours alone, Ask Daoris never proposing it.
    /// </summary>
    [Fact]
    public void The_choice_s_row_says_the_gate_follows_a_runs_version_and_the_keep_is_named()
    {
        var room = HelpRoom.Render(HelpRoomFixture.Machine);
        Assert.DoesNotContain("no gate reads", room, StringComparison.Ordinal);
        Assert.DoesNotContain("still lands as Current says", room, StringComparison.Ordinal);

        var choose = HelpRoomDoors.Doors.Single(door => door.To.StartsWith("choose which workflow", StringComparison.Ordinal));
        Assert.Contains("each run binds the newest version at its first start, and how it lands, your look and its second opinion follow that version", choose.To);

        var keep = HelpRoomDoors.Doors.Single(door => door.Terminal.Contains("daoris workflow keep", StringComparison.Ordinal));
        Assert.Equal("`daoris workflow keep <session> [\"…\"]`", keep.Terminal);
        Assert.Equal("(no screen yet)", keep.Screen);
        Assert.Equal(new HelpDoorAsk.Never("it is yours alone"), keep.Ask);
        Assert.Contains("nothing switches by itself", keep.To);
    }

    /// <summary>
    /// XAGENT1f4 (D155 points 9 and 10, design §8.5, §9): the room's second-opinion row says what the gate does, in the sentences
    /// every other door says (<see cref="OpinionRules.Gate"/>, held to the twins' table), never that nothing reads the rule; and the
    /// person's presses at the gate are named with what each does, each said to be one Ask Daoris never proposes (D110, D156).
    /// </summary>
    [Fact]
    public void The_second_opinion_s_rows_say_what_the_gate_does_and_name_the_person_s_presses()
    {
        var room = HelpRoom.Render(HelpRoomFixture.Machine);
        var rule = HelpRoomDoors.Doors.Single(door => door.To.StartsWith("declare which other agent reads", StringComparison.Ordinal));

        // Every sentence any door may say after a rule's own: a rule reading at landing and each step, and letting it run.
        foreach (var sentence in OpinionRules.Gate(new OpinionRule(["codex-acp"], [OpinionRules.Landing, OpinionRules.Steps], Verify: true)))
        {
            Assert.Contains(sentence, rule.To);
        }

        Assert.DoesNotContain("declared only", rule.To, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("nothing reads it yet", room, StringComparison.Ordinal);

        var ask = HelpRoomDoors.Doors.Single(door => door.Terminal.Contains("daoris session opinion ask", StringComparison.Ordinal));
        var answer = HelpRoomDoors.Doors.Single(door => door.Terminal.Contains("daoris session opinion anyway", StringComparison.Ordinal));
        Assert.Contains("daoris session opinion show", ask.Terminal);
        Assert.Contains("daoris session opinion stop", ask.Terminal);
        Assert.Contains("daoris session opinion myself", answer.Terminal);
        foreach (var door in new[] { ask, answer }) Assert.IsType<HelpDoorAsk.Never>(door.Ask);
        Assert.Contains("*Go on anyway…*", answer.Screen);
        Assert.Contains("*I looked myself…*", answer.Screen);
    }

    /// <summary>
    /// DOORS1a (D163 point 3; D164's amendment points 1-3): the room names the helpers rule and the commit rule, each with its
    /// Setup row and the terminal's twin as <c>daoris place helpers|commits</c> spells it, said to be Ask Daoris's to propose;
    /// and the landing's row names the ticket's key and the squash beside its other switches.
    /// </summary>
    [Fact]
    public void The_room_names_the_helpers_and_commit_rules_and_the_landings_key_and_squash()
    {
        var helpers = HelpRoomDoors.Doors.Single(door => door.Terminal.Contains("daoris place helpers", StringComparison.Ordinal));
        var commits = HelpRoomDoors.Doors.Single(door => door.Terminal.Contains("daoris place commits", StringComparison.Ordinal));
        var landing = HelpRoomDoors.Doors.Single(door => door.Terminal.Contains("daoris place landing", StringComparison.Ordinal));

        Assert.Contains("`daoris place helpers <repository> --mode ask|on|off [--agents <adapter,adapter>]`", helpers.Terminal);
        Assert.Contains("`daoris place helpers <repository> --clear`", helpers.Terminal);
        Assert.Contains("Repositories → the repository's page → Setup → Sessions → Helpers", helpers.Screen);
        Assert.Contains("Repositories → the workspace's page → Setup → Defaults → Helpers", helpers.Screen);
        Assert.Contains("`daoris place commits <repository> --mode as-you-go|ask|at-end [--convention \"…\"|--no-convention]`", commits.Terminal);
        Assert.Contains("`daoris place commits <repository> --clear`", commits.Terminal);
        Assert.Contains("Repositories → the repository's page → Setup → Line and landing → Commits", commits.Screen);
        Assert.Contains("Repositories → the workspace's page → Setup → Defaults → Commits", commits.Screen);
        foreach (var door in new[] { helpers, commits })
        {
            Assert.Contains("`--workspace <name>` for a whole workspace", door.Terminal);
            Assert.IsType<HelpDoorAsk.Proposes>(door.Ask);
        }

        Assert.Contains("from the repository's own instructions", commits.To);
        Assert.Contains("`{ticket}`", landing.To + landing.Terminal);
        Assert.Contains("`--key <shape>|--no-key`", landing.Terminal);
        Assert.Contains("`--squash|--no-squash`", landing.Terminal);
    }

    /// <summary>
    /// SESSUX1h2b (D126 §7.3): the room names Sessions' archive, archive what ended and unarchive, and a conversation's delete,
    /// each with its press and the terminal's twin as <c>daoris session list</c> spells it, beside a quest's or an ask's delete.
    /// </summary>
    [Fact]
    public void The_rooms_rows_name_a_sessions_archive_and_a_conversations_delete_with_their_twins()
    {
        var order = HelpRoomDoors.Doors.Select(door => door.To).ToList();
        var archive = HelpRoomDoors.Doors.Single(door => door.Terminal.Contains("daoris session archive", StringComparison.Ordinal));
        var delete = HelpRoomDoors.Doors.Single(door => door.Terminal.Contains("daoris session delete", StringComparison.Ordinal));

        Assert.Equal("`daoris session archive <id>… | --ended [--yes]`, `daoris session unarchive <id>…`", archive.Terminal);
        Assert.Equal("Sessions → a session's ⋯ → Archive or Unarchive; the list's ⋯ → Archive what ended…, and Show archived", archive.Screen);
        Assert.Contains("a session running, waiting on you or to review is never archived", archive.To);
        Assert.Equal("`daoris session delete <id>`", delete.Terminal);
        Assert.Equal("Sessions → a session's ⋯ → Delete…", delete.Screen);
        Assert.StartsWith("delete a conversation that served no quest", delete.To);
        Assert.Equal(order.IndexOf("delete a quest or an ask made by mistake") + 1, order.IndexOf(archive.To));
        Assert.Equal(order.IndexOf(archive.To) + 1, order.IndexOf(delete.To));
    }

    /// <summary>
    /// PAUSE1f-b (D132 §7.1, §7.4): pause and resume are pressed on the ask's and the quest's pages and on a session, which the
    /// `pause` kind proposes; abandon is on the pages alone, and marked as the person's own.
    /// </summary>
    [Fact]
    public void The_rooms_rows_name_where_pause_resume_and_abandon_are_pressed()
    {
        var pause = HelpRoomDoors.Doors.Single(door => door.Terminal.Contains("daoris ask pause|--resume", StringComparison.Ordinal));
        var abandon = HelpRoomDoors.Doors.Single(door => door.Terminal.Contains("daoris ask abandon", StringComparison.Ordinal));

        Assert.Equal(
            "Quests → the ask's page or the quest's page → Pause…, Resume; Sessions → the session's header or its row's ⋯ → "
            + "Pause quest…, Pause ask…, Resume quest, Resume ask",
            pause.Screen);
        Assert.Equal("Quests → the ask's page or the quest's page → Abandon…", abandon.Screen);
        Assert.Equal(new HelpDoorAsk.Never("the reason is your answer"), abandon.Ask);
    }

    /// <summary>
    /// HIST1f-b (design §6.1, §6.2, §6.4): the room names where finished history is cleared, the workspace's reading first, and
    /// the terminal's history verbs as <c>daoris</c>'s usage spells them, after a conversation's delete.
    /// </summary>
    [Fact]
    public void The_rooms_row_names_where_finished_history_is_cleared_with_its_twins()
    {
        var order = HelpRoomDoors.Doors.Select(door => door.To).ToList();
        var clear = HelpRoomDoors.Doors.Single(door => door.Terminal.Contains("daoris history clear", StringComparison.Ordinal));
        var delete = HelpRoomDoors.Doors.Single(door => door.Terminal.Contains("daoris session delete", StringComparison.Ordinal));

        Assert.StartsWith("clear finished history from this machine: a closed quest's work, an ask's with every quest it became", clear.To);
        Assert.Equal(
            "Quests → the quest's page or the ask's page → its header's ⋯ → Clear from this machine…, and the quest's Clear failed "
            + "sessions…; Repositories → the workspace's page → Details → Kept on this machine → Clear history…",
            clear.Screen);
        Assert.Equal(
            "`daoris history [--workspace <name>]`, `daoris history clear --workspace <name> [--yes]`, "
            + "`daoris quest clear <id> [--failed] [--yes]`, `daoris ask clear <id> [--yes]`",
            clear.Terminal);
        Assert.Equal(order.IndexOf(delete.To) + 1, order.IndexOf(clear.To));
    }

    /// <summary>
    /// ASKSCOPE1e (D158's ASKSCOPE1d note point 1): the history's terminal door names its workspace flags as
    /// <c>daoris ask-daoris list</c>'s usage spells them, so a helper asked for another workspace's conversation sends the
    /// person to a list that holds it.
    /// </summary>
    [Fact]
    public void The_rooms_history_row_names_the_list_s_workspace_flags()
    {
        var history = HelpRoomDoors.Doors.Single(door => door.Terminal.Contains("daoris ask-daoris list", StringComparison.Ordinal));

        Assert.Equal(
            "`daoris ask-daoris list [--workspace <name> | --all] [--search \"…\"]`, `daoris session say <id> \"…\"`, "
            + "`daoris ask-daoris rename|pin|unpin|delete <id>`",
            history.Terminal);
        Assert.Contains("[--workspace <name> | --all]", DriverCommand.Usage);
        Assert.Contains("[--workspace <name> | --all]", HelpCommand.Usage);
    }
}
