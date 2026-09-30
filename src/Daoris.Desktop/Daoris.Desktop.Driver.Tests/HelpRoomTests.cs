using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Driver.Tests;

/// <summary>
/// Ask Daoris's room (HELP1a, D89): a folder under the home, re-rendered at every open, that tells the
/// session what Daoris is, which screen and which terminal command does each thing, and what this
/// machine holds now — names and states, never a key. It reads; it changes nothing.
/// </summary>
public sealed class HelpRoomTests : IDisposable
{
    private readonly string _home = Path.Combine(
        Path.GetTempPath(), "daoris-help-room-" + Guid.NewGuid().ToString("N")[..8]);

    public HelpRoomTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private static readonly HelpMachine Machine = new()
    {
        Adapter = "claude-code",
        Intake = "claude-code-acp",
        Helper = "claude-code-acp",
        Cap = 4,
        Waiting = 2,
        Asks = 1,
        Repositories =
        [
            new("console-ui", "work")
            {
                Checkout = true, Drivable = true, OwnTree = true,
                Line = new Line("feature/app", LineSource.Repository),
                Landing = new Landing(new LandingRule("branch", "feature/{slug}-{quest}", Tidy: true), LandingSource.Workspace),
            },
            new("reports-db", "work")
            {
                Checkout = true, Drivable = true, Held = true, Line = new Line("main", LineSource.Checkout),
            },
            new("engine", "default") { Checkout = false },
        ],
        Agents =
        [
            new("claude-code")
            {
                Product = "Claude Code", Present = true, Version = "2.1.0", Login = "in",
                Accounts = [new("work", "out")],
            },
            new("codex") { Present = false, Login = "unknown" },
        ],
    };

    [Fact]
    public void The_room_is_under_the_home_and_says_what_the_machine_holds_now()
    {
        var room = HelpRoom.Prepare(_home, Machine);

        Assert.Equal(Path.Combine(_home, HelpRoom.Folder), room);
        var agents = File.ReadAllText(Path.Combine(room, "AGENTS.md"));
        // The circles and their repositories, each with how it is driven, its line and how its work lands.
        Assert.Contains("### Workspace `work`", agents);
        Assert.Contains("| `console-ui` | driven, in its own tree | `feature/app` (set for it) | on a branch `feature/{slug}-{quest}`, tree removed once landed (its workspace's rule) |", agents);
        Assert.Contains("| `reports-db` | driven, held | `main` (the checkout's own) | merged into the line (the default) |", agents);
        Assert.Contains("| `engine` | not driven, no checkout here | none git can name | merged into the line (the default) |", agents);
        // What drives, and what waits on the person.
        Assert.Contains("quests run on `claude-code`", agents);
        Assert.Contains("asks are answered on `claude-code-acp`", agents);
        Assert.Contains("up to 4 sessions at once", agents);
        Assert.Contains("2 sessions wait on the person", agents);
        Assert.Contains("1 ask waits for an answer", agents);
        // The agents, by the tool's own answer — and one that is not installed is said, not dropped.
        Assert.Contains("- `claude-code` (Claude Code 2.1.0): signed in; account `work` signed out", agents);
        Assert.Contains("- `codex`: not installed", agents);
        // One harness reads AGENTS.md, another CLAUDE.md (D59).
        Assert.Equal("@AGENTS.md\n", File.ReadAllText(Path.Combine(room, "CLAUDE.md")));
        Assert.DoesNotContain("\r", agents);
    }

    /// <summary>
    /// The doors (D50): every change the room speaks of is a screen and the terminal command that does the
    /// same, spelled as the CLI spells it — a helper that invented a verb would send the person to nothing.
    /// </summary>
    [Fact]
    public void The_room_names_the_screen_and_the_terminal_command_for_each_change()
    {
        var agents = HelpRoom.Render(Machine);

        foreach (var command in new[]
        {
            "daoris driver drive|undrive <repository>", "daoris driver hold|resume <repository>",
            "daoris driver trees <repository> on|off", "daoris driver line <repository> <branch>|--clear",
            "daoris driver landing <repository> merge|branch <pattern>|--clear", "daoris driver intake <agent>|off",
            "daoris driver helper <agent>|off", "daoris driver strikes <n>", "daoris driver timeout <minutes>",
            "daoris driver notify on|off", "daoris agent rules", "daoris-driver ask --workspace <name>",
            "daoris-driver trees clean",
        })
        {
            Assert.Contains(command, agents);
        }

        Assert.Contains("Settings → Workspace → Lines", agents);
        // What a landing pattern may say, as `LandingRules` reads it — the first real conversation had to guess.
        foreach (var token in new[] { "{quest}", "{session}", "{slug}", "{repository}" }) Assert.Contains(token, agents);
        Assert.Contains("Settings → Daoris's own AI", agents);
        // SETUP1b: the setup guide the person may be walked through, by the names the window gives it.
        Assert.Contains("Settings → Get started (the Daoris menu's *Set up Daoris*)", agents);
        // It reads and advises; the moves that stay the person's are named as never its own.
        Assert.Contains("You change nothing yourself", agents);
        // It proposes (HELP1c): a card the person applies, through the connector's two tools.
        Assert.Contains("`setting_propose`", agents);
        Assert.Contains("`ask_propose`", agents);
        Assert.Contains("push, merge, discard, sign in", agents);
    }

    /// <summary>
    /// HELP4: asked to tidy a repository's branches, the helper's first move was a shell command, refused
    /// before it ran, and it then rebuilt the repository's branches from its quests and presented the
    /// guess as the tree. The room says it has no shell and reads no checkout; that a repository's own
    /// work is routed there, as an ask or a conversation in that repository; that what it could not see
    /// is said as such; and it points at the cleanup the person wanted, which is a door of Daoris's own.
    /// HELP5: its fourth move on a real conversation was a fetch of a ticket's URL, refused the same way,
    /// so the same sentence says it has no web either.
    /// </summary>
    [Fact]
    public void The_room_says_it_has_no_shell_nor_web_and_routes_a_repositorys_own_work_there()
    {
        var agents = HelpRoom.Render(Machine);

        Assert.Contains("You have no shell", agents);
        Assert.Contains("no web fetch or search", agents);
        Assert.Contains("never try one", agents);
        Assert.Contains("a repository's own work", agents);
        Assert.Contains("`ask_propose`", agents);
        Assert.Contains("Sessions → Start a session", agents);
        Assert.Contains("say what you could not see", agents);
        Assert.Contains("Session branches", agents);
    }

    /// <summary>
    /// HELP7: tried with the real helper, it told the person to *press Apply* on a card whose button reads
    /// *go there*. The room gives each card's buttons as the card labels them, in both languages.
    /// </summary>
    [Fact]
    public void The_room_names_each_cards_own_buttons()
    {
        var agents = HelpRoom.Render(Machine);

        Assert.Contains("every card but a go reads **apply** and **not now**", agents);
        Assert.Contains("a go card reads **go there** and **not now**", agents);
        Assert.Contains("**应用**", agents);
        Assert.Contains("**前往**", agents);
    }

    /// <summary>
    /// HELP2: asked what the panel held, the helper guessed that the View menu names each view's region,
    /// which it does not — its room said nothing of the window. The room says how the window is laid out
    /// and how a view moves, by the names the window uses, and that where the views stand now arrives with
    /// the person's message.
    /// </summary>
    [Fact]
    public void The_room_says_how_the_window_is_laid_out_and_how_a_view_moves()
    {
        var agents = HelpRoom.Render(Machine);

        Assert.Contains("## The window", agents);
        foreach (var said in new[]
        {
            "the right side bar", "the panel", "the timeline, the review and the console", "Every view sits in one frame",
            "tab list", "right-click", "drag", "Reset view locations",
            "`Ctrl+B`", "`Ctrl+J`", "`Ctrl+Alt+B`", "`F1`", "`Ctrl+Alt+I`", "`Ctrl+Shift+Alt+L`", "`Ctrl+K`",
            // The person's own terminal (CONSOLE4b), and that it is theirs rather than a session's.
            "terminal: the person's own shell", "never a session's",
        })
        {
            Assert.Contains(said, agents);
        }

        // It cannot see the window: what it is told of it comes with the message, and otherwise it asks.
        Assert.Contains("You cannot see the window", agents);
    }

    [Fact]
    public void A_machine_with_nothing_registered_says_so_and_where_to_begin()
    {
        var agents = HelpRoom.Render(new HelpMachine());

        Assert.Contains("No repository is registered on this machine", agents);
        Assert.Contains("daoris connect", agents);
        Assert.Contains("no agent answers asks", agents);
    }

    /// <summary>
    /// It reads the family and nothing else: over the protocol door an unlisted tool is refused by
    /// construction (D52), which is the point. Proposing arrives with HELP1c.
    /// </summary>
    [Fact]
    public void The_room_allows_reading_the_family_and_nothing_that_writes()
    {
        var room = HelpRoom.Prepare(_home, Machine);

        using var settings = JsonDocument.Parse(File.ReadAllText(Path.Combine(room, ".claude", "settings.json")));
        var allowed = settings.RootElement.GetProperty("permissions").GetProperty("allow")
            .EnumerateArray().Select(e => e.GetString()!).ToList();
        Assert.Equal(HelpRoom.Allowed, allowed);
        Assert.Contains("mcp__daoris-knowledge__registry", allowed);
        Assert.Contains("mcp__daoris-knowledge__knowledge_search", allowed);
        Assert.DoesNotContain("mcp__daoris-knowledge__quest_publish", allowed);
        // It proposes, and the person applies (HELP1c) — never PERM2's rule proposal, whose narrowing applies itself.
        Assert.Contains("mcp__daoris-knowledge__setting_propose", allowed);
        Assert.Contains("mcp__daoris-knowledge__ask_propose", allowed);
        // HELP6: the four further kinds, each a card the person applies.
        Assert.Contains("mcp__daoris-knowledge__agent_propose", allowed);
        Assert.Contains("mcp__daoris-knowledge__delete_propose", allowed);
        Assert.Contains("mcp__daoris-knowledge__agent_settings_propose", allowed);
        Assert.Contains("mcp__daoris-knowledge__go_propose", allowed);
        // PLUG9: adding a landed plugin, or switching one, a card the person applies like the rest.
        Assert.Contains("mcp__daoris-knowledge__plugin_propose", allowed);
        Assert.DoesNotContain("mcp__daoris-knowledge__permission_propose", allowed);
        Assert.DoesNotContain(allowed, rule => rule.StartsWith("Bash", StringComparison.Ordinal)
            || rule.StartsWith("Edit", StringComparison.Ordinal) || rule.StartsWith("Write", StringComparison.Ordinal));
    }

    /// <summary>
    /// What it is handed at spawn: the room's allows and a read of its conversation's files — never the
    /// person's own allows, which are for work in a repository — and every deny the person wrote.
    /// </summary>
    [Fact]
    public void Its_rules_are_the_rooms_allows_and_the_persons_denies()
    {
        var file = PermissionFile.Empty with
        {
            Machine = new RuleLists(["Bash(npm test:*)"], ["Bash(git commit:*)"], ["WebFetch(domain:secrets.example)"]),
        };

        var rules = HelpRoom.Rules(file, Path.Combine(_home, "chats", "s1"));

        Assert.Equal([.. HelpRoom.Allowed, PermissionRules.ReadRule(Path.Combine(_home, "chats", "s1"))], rules.Allow);
        Assert.Empty(rules.Ask);
        Assert.Contains("WebFetch(domain:secrets.example)", rules.Deny);
        Assert.DoesNotContain("Bash(npm test:*)", rules.Allow);
    }

    /// <summary>
    /// The machine the room describes is read from what the driver already holds — its file, the
    /// registry, the lines and the roster — so the room and the screens cannot disagree.
    /// </summary>
    [Fact]
    public void The_machine_is_described_from_the_drivers_own_answers()
    {
        var config = DriverConfig.Empty with
        {
            Drivable = ["console-ui", "reports-db"], Holds = ["reports-db"], Trees = ["console-ui"],
            Cap = 4, Adapter = "claude-code", IntakeAdapter = "claude-code-acp", HelperAdapter = "claude-code-acp",
            WorkspaceLandings = new Dictionary<string, LandingRule>(StringComparer.OrdinalIgnoreCase)
            {
                ["work"] = new("branch", "feature/{slug}-{quest}", Tidy: true),
            },
        };
        var snapshot = new Snapshot(
            [],
            [
                new RepoView("console-ui", Adopted: false, Root: "/work/console-ui", Workspace: "work"),
                new RepoView("reports-db", Adopted: false, Root: "/work/reports-db", Workspace: "work"),
                new RepoView("engine", Adopted: true, Root: null),
            ],
            [
                new SessionView("s1", "console-ui", "awaiting-person"),
                new SessionView("s2", "reports-db", "working"),
            ]);
        IReadOnlyList<RepositoryLine> lines =
        [
            new("console-ui", "work", "feature/app", LineSource.Repository),
            new("reports-db", "work", "main", LineSource.Checkout),
            new("engine", "default", null, LineSource.None),
        ];
        IReadOnlyList<HarnessReport> roster =
        [
            new("claude-code", true, "2.1.0", null, null, null,
                [new ProfileReport("work", "/profiles/work", LoginState.Out, Key: "sk-…")], LoginState.In),
            new("codex", false, null, "not found", null, null, []),
        ];

        IReadOnlyList<AskView> standing =
        [
            new("a1", "work", "cap the chunk budget", "Proposed", "declarations"),
            new("a2", "work", "stream the tiles", "Published", "named") { Quests = ["q1"] },
        ];
        var machine = HelpRoom.Describe(
            config, snapshot, lines, roster, adapter => adapter == "claude-code" ? "Claude Code" : null, asks: 1, standing);

        var report = Assert.Single(machine.Repositories, repository => repository.Name == "console-ui");
        Assert.True(report.Drivable && report.OwnTree && report.Checkout && !report.Held);
        Assert.Equal("feature/app", report.Line.Branch);
        Assert.Equal(LandingSource.Workspace, report.Landing.Source);
        Assert.True(Assert.Single(machine.Repositories, repository => repository.Name == "reports-db").Held);
        Assert.False(Assert.Single(machine.Repositories, repository => repository.Name == "engine").Checkout);
        Assert.Equal(1, machine.Waiting);
        Assert.Equal(1, machine.Asks);
        // The asks by id (HELP6), with the quests each became.
        Assert.Equal(["a1", "a2"], machine.OpenAsks.Select(ask => ask.Id));
        Assert.Equal(["q1"], machine.OpenAsks[1].Quests);
        Assert.Equal(("claude-code", "claude-code-acp", "claude-code-acp", 4), (machine.Adapter, machine.Intake, machine.Helper, machine.Cap));
        var agent = Assert.Single(machine.Agents, agent => agent.Name == "claude-code");
        Assert.Equal(("Claude Code", "in"), (agent.Product, agent.Login));
        Assert.Equal([new HelpAccount("work", "out")], agent.Accounts);
        // A key is an account's handle and never its value (AGT3), and the room carries neither.
        Assert.DoesNotContain("sk-", HelpRoom.Render(machine));
        Assert.DoesNotContain("/profiles/work", HelpRoom.Render(machine));
        Assert.DoesNotContain("/work/console-ui", HelpRoom.Render(machine));
    }

    /// <summary>
    /// HELP6: every door built since HELP1c is a proposal too — an agent's update or pin, a delete of a
    /// record made by mistake, an account's model and effort, and a screen to open — each named with its
    /// tool and the rule its route judges it by, so the helper does not propose what would be refused.
    /// </summary>
    [Fact]
    public void The_room_names_every_kind_it_may_propose_and_the_rule_each_is_judged_by()
    {
        var agents = HelpRoom.Render(Machine);

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
    /// HELP6: the places `go_propose` may name are listed from the driver's own table, the one it judges a
    /// go by — so the room and the judge cannot disagree about which screens exist.
    /// </summary>
    [Fact]
    public void The_room_lists_every_place_a_go_may_name()
    {
        var agents = HelpRoom.Render(Machine);

        Assert.Contains("## Where you may take the person", agents);
        foreach (var (id, _) in HelpPlaces.Views) Assert.Contains($"`{id}`", agents);
        foreach (var (id, name) in HelpPlaces.Domains) Assert.Contains($"`{id}` ({name})", agents);
        foreach (var (_, id, name) in HelpPlaces.Parts) Assert.Contains($"`{id}` ({name})", agents);
        Assert.Contains("changes nothing", agents);
    }

    /// <summary>HELP6: the asks not closed are listed by id, so a delete of one made by mistake can name it.</summary>
    [Fact]
    public void The_room_lists_the_asks_by_id()
    {
        var agents = HelpRoom.Render(Machine with
        {
            OpenAsks = [new HelpAsk("a1b2c3d4", "fix the chunk streamer's stall", "work", "Published", ["q1a2b3c4"])],
        });

        Assert.Contains("- `#a1b2c3d4` at `work`: “fix the chunk streamer's stall” (Published; quests `#q1a2b3c4`)", agents);
    }

    /// <summary>
    /// HELP8: the plugins that can land work here are named, so a landing rule the helper proposes names
    /// one the route takes (D100); with none, it says so and that installing one is the person's.
    /// </summary>
    [Fact]
    public void The_room_names_the_plugins_that_can_land_work_here()
    {
        var some = HelpRoom.Render(Machine with { LandingPlugins = ["example.github-pull-request", "example.lands"] });
        var none = HelpRoom.Render(Machine);

        Assert.Contains("`--plugin <id>`", some);
        Assert.Contains("Plugins that can land work here: `example.github-pull-request`, `example.lands`.", some);
        Assert.Contains("No plugin that lands work is installed here", none);
        Assert.Contains("`daoris plugin add <folder>`", none);
    }

    [Fact]
    public void The_room_is_told_which_installed_plugins_land_work()
    {
        var home = Path.Combine(Path.GetTempPath(), "daoris-help-plugins-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            void Install(string id, string points)
            {
                var folder = Path.Combine(home, PluginCatalog.Folder, id);
                Directory.CreateDirectory(folder);
                File.WriteAllText(Path.Combine(folder, PluginCatalog.ManifestName),
                    $$"""{ "id": "{{id}}", "hooks": { "command": ["node", "${plugin}/h.mjs"], "points": [{{points}}] } }""");
            }

            Install("example.lands", "\"work/land\"");
            Install("example.off", "\"work/land\"");
            Install("example.quiet", "\"session/ended\"");
            PluginState.Disable(home, "example.off");

            Assert.Equal(["example.lands"], HelpRoom.LandingPluginsOf(PluginCatalog.Load(home)));
        }
        finally
        {
            try { Directory.Delete(home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }

    /// <summary>
    /// PLUG9: a plugin runs on this machine as the person, so making one is work. The room sends it to the
    /// repository that holds plugins as an ask, naming every point a plugin may speak on, says the helper
    /// never writes one and never proposes adding one that has not landed, and leaves where plugins live
    /// to the person when no repository says it holds them.
    /// </summary>
    [Fact]
    public void The_room_says_a_plugin_is_made_as_an_ask_and_only_a_landed_one_is_added()
    {
        var agents = HelpRoom.Render(Machine);

        Assert.Contains("## Making a plugin", agents);
        Assert.Contains("never write one yourself", agents);
        Assert.Contains("as an ask (`ask_propose`) at the workspace of the repository that holds plugins", agents);
        foreach (var point in HookPoints.All) Assert.Contains($"`{point}`", agents);
        Assert.Contains("makes it with its tests", agents);
        Assert.Contains("the person decides where plugins live (a repository of their own, connected like any other)", agents);
        Assert.Contains("never propose adding one that has not landed", agents);
        Assert.Contains("`plugin_propose`", agents);
        // The doors (D50): the terminal twins of what a plugin card applies.
        Assert.Contains("`daoris plugin add <folder>`, `daoris plugin enable|disable <id>`", agents);
    }

    /// <summary>PLUG9: the plugins installed here, by id and state, so a switch names one the catalogue holds.</summary>
    [Fact]
    public void The_room_lists_the_plugins_installed_here()
    {
        var some = HelpRoom.Render(Machine with
        {
            Plugins =
            [
                new HelpPlugin("example.lands", Enabled: true, ["work/land"]),
                new HelpPlugin("example.off", Enabled: false, []),
                new HelpPlugin("example.broken", Enabled: true, []) { Problem = "`apiVersion` must be an integer." },
            ],
        });

        Assert.Contains("- Plugins: `example.lands` (on, speaks on `work/land`), `example.off` (off), "
            + "`example.broken` (on, contributes nothing: `apiVersion` must be an integer.)", some);
        Assert.Contains("- Plugins: none installed.", HelpRoom.Render(Machine));
    }

    [Fact]
    public void The_plugins_are_described_from_the_catalogue()
    {
        var home = Path.Combine(Path.GetTempPath(), "daoris-help-installed-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            var folder = Path.Combine(home, PluginCatalog.Folder, "example.lands");
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, PluginCatalog.ManifestName),
                """{ "id": "example.lands", "hooks": { "command": ["node", "${plugin}/h.mjs"], "points": ["work/land"] } }""");
            PluginState.Disable(home, "example.lands");

            var machine = HelpRoom.Describe(
                DriverConfig.Empty, new Snapshot([], [], []), [], [], _ => null, asks: 0, plugins: PluginCatalog.Load(home));

            var plugin = Assert.Single(machine.Plugins);
            Assert.Equal(("example.lands", false, null), (plugin.Id, plugin.Enabled, plugin.Problem));
            Assert.Equal(["work/land"], plugin.Points);
        }
        finally
        {
            try { Directory.Delete(home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }

    /// <summary>A twin (`twins.md`): the service's `SessionLedger.HelpRepository` and the page's spell it too.</summary>
    [Fact]
    public void Its_sessions_are_recorded_in_a_repository_no_folder_can_be_called()
    {
        Assert.Equal("daoris:help", HelpRoom.Repository);
    }
}
