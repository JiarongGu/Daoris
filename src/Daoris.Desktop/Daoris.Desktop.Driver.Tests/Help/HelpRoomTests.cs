using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Driver.Tests;

/// <summary>
/// Ask Daoris's room (HELP1a, D89): a folder under the home, re-rendered at every open, that tells the
/// session what Daoris is, which screen and which terminal command does each thing, and what this
/// machine holds now — names and states, never a key. It reads; it changes nothing.
/// </summary>
/// <remarks>
/// What the room is and what it allows. Each section's own sentences are held beside it under
/// <c>Help/Room/</c>, and the room whole by <see cref="HelpRoomGoldenTests"/> (MOD6).
/// </remarks>
public sealed class HelpRoomTests : IDisposable
{
    private readonly string _home = Path.Combine(
        Path.GetTempPath(), "daoris-help-room-" + Guid.NewGuid().ToString("N")[..8]);

    public HelpRoomTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private static HelpMachine Machine => HelpRoomFixture.Machine;

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
        // WSR5b: handing a landed branch to its plugin, a card the person applies like the rest.
        Assert.Contains("mcp__daoris-knowledge__hand_propose", allowed);
        // ENTRY1d1: moving a registered repository to a workspace, by name, a card the person applies like the rest.
        Assert.Contains("mcp__daoris-knowledge__repository_propose", allowed);
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
    /// D107: with reading across on, what it is handed adds, for each checkout it may read, a read and the two
    /// read-only git commands by exact prefix — no other command, and nothing that writes. HELP4's refusal of
    /// a shell stands: in the agent's asking mode anything else is asked, and refused.
    /// </summary>
    [Fact]
    public void Its_rules_read_each_checkout_it_may_read_and_run_no_other_command()
    {
        var kept = Path.Combine(_home, "chats", "s1");

        var rules = HelpRoom.Rules(PermissionFile.Empty, kept, [new("console-ui", "work", "/work/console-ui")]);

        Assert.Equal(
            [
                .. HelpRoom.Allowed, PermissionRules.ReadRule(kept),
                "Read(//work/console-ui/**)", "Bash(git -C /work/console-ui status:*)", "Bash(git -C /work/console-ui branch --list:*)",
            ],
            rules.Allow);
        Assert.DoesNotContain(rules.Allow, rule => rule.StartsWith("Edit", StringComparison.Ordinal) || rule.StartsWith("Write", StringComparison.Ordinal));
        Assert.Equal(2, rules.Allow.Count(rule => rule.StartsWith("Bash", StringComparison.Ordinal)));
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
        Assert.Equal([new HelpWaitingSession("s1", "console-ui")], machine.Waiting);
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
        // A root is named only where the helper may read it (D107), once, in the reading list.
        var room = HelpRoom.Render(machine);
        Assert.Single(room.Split('\n'), line => line.Contains("/work/console-ui", StringComparison.Ordinal));
        Assert.Contains("- `console-ui` (workspace `work`) — `/work/console-ui`", room);
        var unread = HelpRoom.Describe(
            config.WithWorkspaceReadAcross("work", false), snapshot, lines, roster, _ => null, asks: 1, standing);
        Assert.DoesNotContain("/work/console-ui", HelpRoom.Render(unread));
    }

    /// <summary>
    /// KNOWUSE1b: each repository's standing answer is in the room, the person's words as this machine keeps them, so the
    /// helper can say what a repository's sessions are handed and propose changing it; a workspace with none says nothing.
    /// </summary>
    [Fact]
    public void The_room_carries_each_repositorys_standing_answer_in_the_persons_words()
    {
        var machine = HelpRoomFixture.Machine with
        {
            Repositories =
            [
                .. HelpRoomFixture.Machine.Repositories.Select(repository => repository.Name == "console-ui"
                    ? repository with { Standing = "dev writes allowed; prod only on a yes" }
                    : repository),
            ],
        };

        var room = HelpRoom.Render(machine);

        Assert.Contains("Standing answers in `work`, each handed to every session in its repository beneath its quest", room);
        Assert.Contains("- `console-ui`: \"dev writes allowed; prod only on a yes\"", room);
        Assert.DoesNotContain("Standing answers in `default`", room);
        Assert.DoesNotContain("Standing answers", HelpRoom.Render(HelpRoomFixture.Machine));

        var described = HelpRoom.Describe(
            DriverConfig.Empty.WithStanding("Engine", "dev only", DateTimeOffset.UnixEpoch),
            new Snapshot([], [new RepoView("engine", Adopted: true, Root: null)], []), [], [], _ => null, asks: 0, []);
        Assert.Equal("dev only", Assert.Single(described.Repositories).Standing);
    }

    /// <summary>A twin (`twins.md`): the service's `SessionLedger.HelpRepository` and the page's spell it too.</summary>
    [Fact]
    public void Its_sessions_are_recorded_in_a_repository_no_folder_can_be_called()
    {
        Assert.Equal("daoris:help", HelpRoom.Repository);
    }

    /// <summary>
    /// MOD6: every section is a class in a file of its own under <c>Help/Room/</c>, named after it, so a new
    /// section is a file and one registry line, and no section hides inside another's file.
    /// </summary>
    [Fact]
    public void Every_section_is_a_file_of_its_own()
    {
        var folder = Path.Combine(HelpProposalKindsTests.RepositoryRoot(), "src", "Daoris.Desktop", "Daoris.Desktop.Driver", "Help", "Room");

        Assert.Equal(
            HelpRoomSections.All.Select(section => $"{section.GetType().Name}.cs").Order(StringComparer.Ordinal),
            Directory.GetFiles(folder, "*.cs").Select(Path.GetFileName).Order(StringComparer.Ordinal));
        foreach (var section in HelpRoomSections.All)
        {
            Assert.Contains($"class {section.GetType().Name} : IHelpRoomSection", File.ReadAllText(Path.Combine(folder, $"{section.GetType().Name}.cs")));
        }
    }
}
