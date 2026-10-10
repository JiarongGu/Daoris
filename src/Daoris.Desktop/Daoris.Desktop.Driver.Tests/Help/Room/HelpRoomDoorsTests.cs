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
            "daoris driver drive|undrive <repository>", "daoris driver hold|resume <repository>",
            "daoris driver trees <repository> on|off", "daoris driver line <repository> <branch>|--clear",
            "daoris driver landing <repository> merge|branch <pattern>|--clear", "daoris driver intake <agent>|off",
            "daoris driver helper <agent>|off", "daoris driver strikes <n>", "daoris driver timeout <minutes>",
            "daoris driver notify on|off", "daoris agent rules", "daoris-driver ask --workspace <name>",
            // BRSCOPE1a: the clean-up and bringing up to date take one workspace's checkouts with `--workspace`.
            "daoris-driver trees clean [--workspace <name>]",
            // WSR6: bringing a repository up to date after its pull request merged; WSR7 (D112): every repository with `--all`.
            "daoris-driver trees sync [--repository <name>] [--workspace <name>] [--all] [--yes]",
            // D107: reading and writing across, the CLI's `driver across` verbs.
            "daoris driver across <repository> read on|off|--clear", "daoris driver across <repository> write-to <other> [--clear]",
            // HELP9: every `daoris driver` verb that changes something, and the doors Ask Daoris owes, named where they are.
            // SESSUX1b: and a stop's release, which names the session stopped.
            "daoris driver retry <quest>", "daoris driver retry <quest> --session <id>", "daoris driver cap <n>",
            "daoris driver adapter <agent>",
            // LEFT3: the screen's clear has its terminal door, which Ask Daoris still owes.
            "daoris agent profile default <agent> <profile>|--clear [--workspace <name>]",
            "daoris browser use daoris|edge", "daoris browser links system|daoris",
            // LAYOUT7: the set-up press, which Ask Daoris owes a kind until LAYOUT8.
            "daoris-driver setup <repository> [--plan]",
            // TOOL4e (D125 §6): the order, *Try now* and the default cool-off, the terminal's doors (on the screen since TOOL4g).
            "daoris agent profile order <agent> <profile>…|--clear [--workspace <name>]",
            "daoris agent profile ready <agent> <profile>|--own", "daoris driver cooloff <minutes>",
            // TOOL6a (D130 §16.6): how a list is used, the terminal's door (on the screen since TOOL4g).
            "daoris agent profile use <agent> [goal|order] [--keep <account>|--no-keep] [--early on|off] [--near <percent>] "
                + "[--workspace <name>]",
            // REVIEWENV1j (D154 point 3, D50): the person's review choice, the composer's, the ask page's and the gate's.
            "daoris-driver ask … --review rule|on|<environment>|off", "daoris-driver ask --set-review <id> on|<environment>|off",
            "daoris-driver quest review <quest> off [\"…\"]", "daoris-driver quest review <quest> on|<environment>",
            // WORKFLOW1e (D157 point 10): which workflow work follows and a workspace's kinds, and an ask's kind and workflow.
            "daoris driver workflow use <id>|current|--clear --repository <name>|--workspace <name> [--kind <kind>]",
            "daoris driver workflow kind <workspace> <kind>", "daoris-driver ask … --kind <kind> [--workflow <id>|current]",
            "daoris-driver ask --set-workflow <id>",
            // XAGENT1f4 (D155 point 10, design §9): the person's presses at the second opinion's gate, which Ask Daoris names and
            // proposes none of.
            "daoris-driver opinion ask <session> [--reviewer <adapter>] [--same-agent] [\"…\"]",
            "daoris-driver opinion show <session|opinion>", "daoris-driver opinion stop <opinion>",
            "daoris-driver opinion anyway <session> [\"…\"]", "daoris-driver opinion myself <session> [\"…\"]",
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
        Assert.Contains("Settings → AI features", agents);
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
    /// WORKFLOW1c4 (D157 point 12; the workflow design §6.1, §7): the room named none of the places how work moves is drawn, so
    /// asked how a repository's work lands, or where a quest's work stands, the helper could not send the person there. Current
    /// is a repository's and a workspace's *Workflow* tab, with `daoris driver workflow show` its twin (WORKFLOW1a, WORKFLOW1b);
    /// a run is the session's *Workflow* view, the *Workflow:* line in a quest's and an ask's head, and What needs you's
    /// *Workflow* door, with `daoris-driver workflow run` its twin (WORKFLOW1c, WORKFLOW1c2). Both only read, so Ask Daoris
    /// proposes neither; changing a workflow is WORKFLOW1h's proposal, and today a step opens the Setup row that sets it.
    /// </summary>
    [Fact]
    public void The_room_names_where_how_work_moves_is_drawn_and_where_a_run_stands_with_their_terminal_twins()
    {
        var current = HelpRoomDoors.Doors.Single(door => door.Terminal.StartsWith("`daoris driver workflow show --repository", StringComparison.Ordinal));
        Assert.Equal("`daoris driver workflow show --repository <name>|--workspace <name>`", current.Terminal);
        Assert.Equal("Repositories → the repository's page → Workflow; a workspace's, Repositories → the workspace's page → Workflow",
            current.Screen);
        foreach (var said in new[]
        {
            "who takes part (agent alone, agent + you, you, automatic)", "where each was set", "what Daoris cannot do of it yet",
            "a step opens the Setup row that sets it", "Ask Daoris never proposes it",
        })
        {
            Assert.Contains(said, current.To);
        }

        var run = HelpRoomDoors.Doors.Single(door => door.Terminal.Contains("daoris-driver workflow run", StringComparison.Ordinal));
        Assert.Equal("`daoris-driver workflow run --session|--quest|--ask <id>`", run.Terminal);
        foreach (var said in new[]
        {
            "Sessions → the session's *Workflow* view", "beside Timeline and Review", "View → Workflow",
            "*Workflow:* in a quest's page's head and an ask's", "*Workflow* on a row of What needs you",
        })
        {
            Assert.Contains(said, run.Screen);
        }

        foreach (var said in new[] { "each step's state", "the step the run stands at", "Ask Daoris never proposes it", "its owner's" })
        {
            Assert.Contains(said, run.To);
        }

        // In the order a person meets them: how work moves beside the rules it is drawn from, before a named workflow; where a
        // piece of work stands after starting one, before answering what waits on the person.
        var order = HelpRoomDoors.Doors.Select(door => door.To).ToList();
        int At(string to) => order.FindIndex(each => each.StartsWith(to, StringComparison.Ordinal));
        Assert.Equal(At("declare which other agent reads") + 1, order.IndexOf(current.To));
        Assert.Equal(order.IndexOf(current.To) + 1, At("name a workflow"));
        Assert.True(order.IndexOf(run.To) > At("start a task") && order.IndexOf(run.To) < At("answer what waits on the person"));

        // WORKFLOW1d's row keeps the named workflows' verbs, its `show` the one that names a saved workflow, so the two shows
        // are not read as one.
        var named = HelpRoomDoors.Doors.Single(door => door.To.StartsWith("name a workflow", StringComparison.Ordinal));
        Assert.Contains("`daoris driver workflow list`, `daoris driver workflow show <id>[@<version>]`", named.Terminal);
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

        var keep = HelpRoomDoors.Doors.Single(door => door.Terminal.Contains("daoris-driver workflow keep", StringComparison.Ordinal));
        Assert.Equal("`daoris-driver workflow keep <session> [\"…\"]`", keep.Terminal);
        Assert.Equal("(no screen yet)", keep.Screen);
        Assert.Contains("Ask Daoris never proposes it", keep.To);
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

        var ask = HelpRoomDoors.Doors.Single(door => door.Terminal.Contains("daoris-driver opinion ask", StringComparison.Ordinal));
        var answer = HelpRoomDoors.Doors.Single(door => door.Terminal.Contains("daoris-driver opinion anyway", StringComparison.Ordinal));
        Assert.Contains("daoris-driver opinion show", ask.Terminal);
        Assert.Contains("daoris-driver opinion stop", ask.Terminal);
        Assert.Contains("daoris-driver opinion myself", answer.Terminal);
        foreach (var door in new[] { ask, answer }) Assert.Contains("Ask Daoris never proposes", door.To);
        Assert.Contains("*Go on anyway…*", answer.Screen);
        Assert.Contains("*I looked myself…*", answer.Screen);
    }
}
