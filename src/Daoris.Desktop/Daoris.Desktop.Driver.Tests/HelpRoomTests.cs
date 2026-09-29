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
        // It reads and advises; the moves that stay the person's are named as never its own.
        Assert.Contains("You change nothing yourself", agents);
        // It proposes (HELP1c): a card the person applies, through the connector's two tools.
        Assert.Contains("`setting_propose`", agents);
        Assert.Contains("`ask_propose`", agents);
        Assert.Contains("push, merge, discard, sign in", agents);
    }

    /// <summary>
    /// HELP2: asked what the panel held, the helper guessed that the View menu names each view's region,
    /// which it does not — its room said nothing of the window. The room says how the window is laid out
    /// and how a view moves, by the names the window uses, and that where the views stand now arrives with
    /// the person's message.
    /// </summary>
    /// <summary>
    /// HELP4: asked to tidy a repository's branches, the helper's first move was a shell command, refused
    /// before it ran, and it then rebuilt the repository's branches from its quests and presented the
    /// guess as the tree. The room says it has no shell and reads no checkout; that a repository's own
    /// work is routed there, as an ask or a conversation in that repository; that what it could not see
    /// is said as such; and it points at the cleanup the person wanted, which is a door of Daoris's own.
    /// </summary>
    [Fact]
    public void The_room_says_it_has_no_shell_and_routes_a_repositorys_own_work_there()
    {
        var agents = HelpRoom.Render(Machine);

        Assert.Contains("You have no shell", agents);
        Assert.Contains("never try one", agents);
        Assert.Contains("a repository's own work", agents);
        Assert.Contains("`ask_propose`", agents);
        Assert.Contains("Sessions → Start a session", agents);
        Assert.Contains("say what you could not see", agents);
        Assert.Contains("Session branches", agents);
    }

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

        var machine = HelpRoom.Describe(config, snapshot, lines, roster, adapter => adapter == "claude-code" ? "Claude Code" : null, asks: 1);

        var report = Assert.Single(machine.Repositories, repository => repository.Name == "console-ui");
        Assert.True(report.Drivable && report.OwnTree && report.Checkout && !report.Held);
        Assert.Equal("feature/app", report.Line.Branch);
        Assert.Equal(LandingSource.Workspace, report.Landing.Source);
        Assert.True(Assert.Single(machine.Repositories, repository => repository.Name == "reports-db").Held);
        Assert.False(Assert.Single(machine.Repositories, repository => repository.Name == "engine").Checkout);
        Assert.Equal(1, machine.Waiting);
        Assert.Equal(1, machine.Asks);
        Assert.Equal(("claude-code", "claude-code-acp", "claude-code-acp", 4), (machine.Adapter, machine.Intake, machine.Helper, machine.Cap));
        var agent = Assert.Single(machine.Agents, agent => agent.Name == "claude-code");
        Assert.Equal(("Claude Code", "in"), (agent.Product, agent.Login));
        Assert.Equal([new HelpAccount("work", "out")], agent.Accounts);
        // A key is an account's handle and never its value (AGT3), and the room carries neither.
        Assert.DoesNotContain("sk-", HelpRoom.Render(machine));
        Assert.DoesNotContain("/profiles/work", HelpRoom.Render(machine));
        Assert.DoesNotContain("/work/console-ui", HelpRoom.Render(machine));
    }

    /// <summary>A twin (`twins.md`): the service's `SessionLedger.HelpRepository` and the page's spell it too.</summary>
    [Fact]
    public void Its_sessions_are_recorded_in_a_repository_no_folder_can_be_called()
    {
        Assert.Equal("daoris:help", HelpRoom.Repository);
    }
}
