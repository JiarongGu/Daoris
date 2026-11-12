using Daoris.Desktop.Driver.Tests;
using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Driver.Tests;

/// <summary>
/// Ask Daoris's room rendered whole, against its golden text:
/// three machines between them take every branch a section has. A section moved,
/// reordered or reworded shows here as the whole room's difference, byte for byte.
/// </summary>
/// <remarks>
/// The golden files under <c>Help/golden/help-room/</c> are the room as the helper reads it. A change to what the
/// room says changes its golden file in the same commit, so review reads the room as the helper will; the
/// section tests beside this one say why each sentence is there.
/// </remarks>
public sealed class HelpRoomGoldenTests
{
    /// <summary>Every section speaking: offers with no landing plugin installed, each plugin state, each line and landing source.</summary>
    internal static readonly HelpMachine Full = new()
    {
        Adapter = "claude-code",
        Intake = "claude-code-acp",
        Helper = "claude-code-acp",
        Cap = 4,
        // A session waiting on the person, by id and repository.
        Waiting = [new("s9c0d1e2", "console-ui")],
        // Past the cap, so the golden shows the "and N more" too.
        Sessions = new(1, 1, 2, 1, 12, [.. Enumerable.Range(1, 10).Select(n => new HelpEndedSession($"s{n}e0f1a2", n % 2 == 0 ? "engine" : "console-ui"))]),
        Asks = 2,
        // One unit ended with its cleanup waiting on a tree a session holds and a folder kept, one cleaned.
        Work =
        [
            new WorkMilestone("u-held", "console-ui", "daoris/s1c2d3e4", "s1c2d3e4",
                new MilestoneEnd("ended", EndBy.Merged, "feature/app", "a1b2c3d4e5f6", new DateTimeOffset(2026, 10, 17, 9, 0, 0, TimeSpan.Zero), "every commit of it is on `feature/app`"),
                [new("u-held", InventoryKind.Line, "feature/app", InventoryRole.Result, InventoryDisposition.Kept, "the line holds it")],
                new MilestoneCleanup("pending", [new("u-held", InventoryKind.Tree, "s1c2d3e4", InventoryRole.Temporary, InventoryDisposition.Blocked, "a session still holds this tree")], []),
                [new("u-held", InventoryKind.MovedAside, "s1c2d3e4-20261017", InventoryRole.Recovery, InventoryDisposition.Retained, "kept until 2026-10-24 09:00 UTC")], []),
            new WorkMilestone("u-clean", "engine", "daoris/s5f6a7b8", "s5f6a7b8",
                new MilestoneEnd("ended", EndBy.Merged, "main", "b2c3d4e5f6a1", new DateTimeOffset(2026, 10, 16, 9, 0, 0, TimeSpan.Zero), "every commit of it is on `main`"),
                [new("u-clean", InventoryKind.Line, "main", InventoryRole.Result, InventoryDisposition.Kept, "the line holds it")],
                new MilestoneCleanup("completed", [], [new("u-clean", InventoryKind.SessionBranch, "daoris/s5f6a7b8", InventoryRole.Temporary, InventoryDisposition.Removed, "went 2026-10-16 11:00 UTC")]),
                [], []),
        ],
        WorkUnread = ["`reports-db` has no checkout here, so its recovery refs were not read"],
        // Two lines waiting on what they asked of others, one source not read.
        Waits = new HelpWaits(
            [
                new("s1c2d3e4", "console-ui", "default", "helper h7 · codex-acp · asked 16:29 · asked · delivery kept"),
                new("s5f6a7b8", "engine", "default", "#q9 of core · asked · delivery kept"),
            ],
            ["piece"]),
        Repositories =
        [
            new("console-ui", "work")
            {
                Checkout = true, Drivable = true, OwnTree = true,
                Line = new Line("feature/app", LineSource.Repository),
                // A workspace's required review rule, its one environment local.
                Review = new ResolvedReview(
                    new ReviewRule([new("dev", "local", "README.md", "http://localhost:4200")], Required: true), ReviewSource.Workspace),
                // A workspace's required second opinion, another maker's agent first.
                Opinion = new ResolvedOpinion(new OpinionRule(["codex-acp", "dsh"], ["landing"], Required: true), OpinionSource.Workspace),
                // A named workflow chosen for it, with a kind of its own mapped.
                Workflow = new HelpWorkflowFollowed("docs-to-pr", WorkflowLevels.Repository)
                {
                    Name = "Documentation to a pull request", Version = 2, Kinds = [("feature", "current")],
                    // It lands as its version's `land` step says, not by a landing rule.
                    LandMethod = "pull-request", LandPattern = "docs/{slug}",
                },
            },
            new("reports-db", "work")
            {
                Checkout = true, Drivable = true, Held = true, Line = new Line("main", LineSource.Checkout),
                // Its workspace's choice, which is not saved here.
                Workflow = new HelpWorkflowFollowed("release-flow", WorkflowLevels.Workspace) { Problem = "not saved here" },
            },
            new("engine", "default")
            {
                Checkout = false,
                // None here, whatever its workspace says.
                Review = new ResolvedReview(ReviewRule.None, ReviewSource.Repository),
                // No second opinion here, whatever its workspace says.
                Opinion = new ResolvedOpinion(OpinionRule.None, OpinionSource.Repository),
            },
            new("tools", "default")
            {
                Checkout = true, Drivable = true, Line = new Line("develop", LineSource.Workspace),
            },
        ],
        // The kinds a workspace declares, one mapped and one left to the default.
        WorkflowKinds =
        [
            new HelpWorkflowKind("work", "docs", "Documentation", ["docs/**", "**/*.md"], "docs-to-pr"),
            new HelpWorkflowKind("work", "feature", "Feature", [], null),
        ],
        // Reading across: `reports-db` switched off, the rest on, one path that git needs quoted.
        Reads = [new("console-ui", "work", "/work/console-ui"), new("tools", "default", "/src/my tools")],
        Agents =
        [
            new("claude-code")
            {
                Product = "Claude Code", Present = true, Version = "2.1.0",
                Accounts = [new("work", "out"), new("play", "unknown")],
            },
            new("codex") { Present = false },
            new("dsh") { Present = true, Accounts = [new("home", "in")] },
        ],
        // A role under a set policy with its week, one whose policy is stale, one with none, and one scoped to a workspace.
        Roles =
        [
            new HelpRole("build", "Build", ["build", "design"], HelpRole.MachineScope, null, 7)
            {
                Choices = ["claude-code sonnet high", "claude-code opus high"],
                PolicyState = HelpRole.Set, PolicyRevision = 3,
                Policy = "Spread by weight (3:1); on a limit: next by weight; at most 3 moves a day",
                Next = "choice 1 (claude-code sonnet high), by weight, 3 of the last 4 were choice 2",
                Week = "last 7 days: 14 starts (claude-code sonnet high ×10, claude-code opus high ×4), moved: limit ×1, transient ×1",
            },
            new HelpRole("intake", "Intake", ["intake"], HelpRole.MachineScope, null, 0)
            {
                Choices = ["codex-acp"], PolicyState = HelpRole.Stale, PolicyRevision = 2,
                Why = "its choices changed since it was set",
            },
            new HelpRole("helper", "Helper", ["help"], HelpRole.MachineScope, null, 0) { ChoiceState = "off" },
            new HelpRole("steward", "Steward", ["steward"], HelpRole.MachineScope, null, 0) { ChoiceState = "unreadable" },
            // A repository's role stays in a workspace's room only where the repository is one of its own.
            new HelpRole("auditor", "Auditor", ["review"], HelpRole.RepositoryScope, "no-such-repository", 1) { Choices = ["claude-code opus high"] },
            new HelpRole("designer", "Designer", ["design"], HelpRole.WorkspaceScope, "work", 2)
            {
                Choices = ["claude-code opus high"],
                Week = "last 7 days: 1 start (claude-code opus high ×1)",
            },
        ],
        OpenAsks =
        [
            new HelpAsk("a1b2c3d4", "fix the chunk streamer's stall", "work", "Published", ["q1a2b3c4", "q5e6f7a8"]),
            new HelpAsk("a2none00", "a test ask", "default", "Proposed", []),
            new HelpAsk("a3long00", "stream   the tiles\nfrom the cold cache, and keep the budget under the ceiling the engine sets for "
                + "every chunk it streams, whatever the weather", "work", "Open", []),
        ],
        Plugins =
        [
            new HelpPlugin("example.lands", Enabled: true, ["work/perform"]) { Source = "folder" },
            new HelpPlugin("example.off", Enabled: false, []) { Source = "offer" },
            new HelpPlugin("example.broken", Enabled: true, []) { Problem = "`apiVersion` must be an integer." },
            new HelpPlugin("example.quiet", Enabled: true, []) { Source = "folder" },
        ],
        Parked = [new ParkedQuest("q5e6f7a8", "engine"), new ParkedQuest("q6b7c8d9", "tools")],
        // A quest the person's stop holds, with the session stopped.
        Held = [new HeldQuest("q7c8d9e0", "console-ui", "s8b9c0d1")],
        // An ask's work paused, and one quest on its own.
        Paused = [new PausedBy(WorkScope.Ask, "a1b2c3d4"), new PausedBy(WorkScope.Quest, "q6b7c8d9")],
        // A workspace keeping finished work, some of it kept, and one keeping none; the home's left-over files once.
        History =
        [
            new HistoryReading("work")
            {
                Quests = 3, Asks = 1, Sessions = 7, Teammates = 1, Bytes = new HistoryBytes(1024 * 1024, 0, 0, 0, 0),
                Takes = new HistoryTakes(2, 0, 5, 0, 800 * 1024),
                KeptBy = new Dictionary<string, int> { [HistoryWords.Open] = 1, [HistoryWords.NeedsYou] = 1 },
                LeftOver = 2, LeftOverBytes = 4096,
            },
            new HistoryReading("default") { LeftOver = 2, LeftOverBytes = 4096 },
        ],
        Browser = new HelpBrowser("edge", "daoris", "refuse", ["https://site.example/board", "https://docs.example/"]),
        Landed =
        [
            // This machine's access setup accepts it, which the room says with the level.
            new HelpLanded("engine", "feature/q2-second", "s2a3b4c5", Pushed: false, PullRequest: null) { Access = "machine" },
            // The kept answer about its pull request, which the helper reads rather than asks.
            new HelpLanded("game", "feature/q3-third", "s3", Pushed: true, PullRequest: "https://example.test/pr/3")
            {
                State = new PullRequestState(PullRequestStates.Completed)
                {
                    PullRequest = "https://example.test/pr/3", MergeCommit = new string('a', 40), SourceCommit = new string('b', 40),
                    Target = "main", How = MergeHow.Squash, At = new DateTimeOffset(2026, 10, 4, 13, 50, 0, TimeSpan.Zero),
                    Plugin = "example.lands", AskedAt = new DateTimeOffset(2026, 10, 4, 14, 2, 0, TimeSpan.Zero),
                },
            },
            new HelpLanded("game", "feature/q4-fourth", "s4", Pushed: true, PullRequest: null)
            {
                AskFailed = new PullRequestAskFailed(PluginEvents.Late, "example.lands", new DateTimeOffset(2026, 10, 4, 14, 30, 0, TimeSpan.Zero)),
            },
        ],
        Offers =
        [
            new HelpOffer("github-pull-request", "GitHub pull request", "1.0.0", ["work/perform"], ["gh, signed in: `gh auth login`."]),
            new HelpOffer("in-app-browser", "In-app browser", "1.0.0", [], ["node on the PATH."]) { Servers = ["browser"] },
            new HelpOffer("example.agent", "example.agent", "", ["custom/point", "session/ended"], []) { Harnesses = ["acme-agent"] },
        ],
    };

    /// <summary>Plugins that land work installed, and nothing else: no agent, no repository, no offer.</summary>
    internal static readonly HelpMachine Sparse = new()
    {
        Asks = 1,
        LandingPlugins = ["example.github-pull-request", "example.lands"],
    };

    /// <summary>A machine with nothing at all.</summary>
    internal static readonly HelpMachine Empty = new();

    /// <summary>
    /// A workspace's room: <c>alpha</c>'s, projected from a machine with <c>beta</c>'s and the default's facts beside
    /// it, so its first lines say its scope and every list and count is its own. <c>full</c>, <c>sparse</c> and <c>empty</c> are
    /// <c>default</c>'s room, the whole machine.
    /// </summary>
    internal static HelpMachine Scoped => HelpRoomScopeFixture.Machine().Within("alpha");

    internal static HelpMachine Named(string name) => name switch
    {
        "full" => Full,
        // Every section speaking, in full access, so review reads the room as the helper does there.
        "full-access" => Full with { Mode = HelpModes.Full },
        "sparse" => Sparse,
        "scoped" => Scoped,
        _ => Empty,
    };

    private static string RepositoryRoot()
    {
        var at = new DirectoryInfo(AppContext.BaseDirectory);
        while (at is not null && !File.Exists(Path.Combine(at.FullName, "daoris.json"))) at = at.Parent;
        return at?.FullName ?? throw new InvalidOperationException("no workspace root above the test binary");
    }

    internal static string GoldenPath(string file) =>
        Path.Combine(RepositoryRoot(), "src", "Daoris.Desktop", "Daoris.Desktop.Driver.Tests", "Knowledge", "golden", "help-room", file);

    /// <summary>Set to 1, the tests write each golden from the render instead of comparing; unset, they compare.</summary>
    internal const string WriteSwitch = "DAORIS_GOLDEN_WRITE";

    internal static bool WriteRequested => Environment.GetEnvironmentVariable(WriteSwitch) == "1";

    /// <summary>
    /// Compares <paramref name="actual"/> with the golden at <paramref name="path"/>; with <paramref name="write"/>
    /// writes it there instead (BOM-less UTF-8, as rendered, so LF stays LF) and says so on the test's output.
    /// </summary>
    internal static void Settle(string path, string actual, bool write)
    {
        if (!write)
        {
            Assert.Equal(File.ReadAllText(path), actual);
            return;
        }
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var staged = path + ".new";
        File.WriteAllText(staged, actual, new System.Text.UTF8Encoding(false));
        File.Move(staged, path, overwrite: true);
        Console.Error.WriteLine($"{WriteSwitch}: wrote {path}");
    }

    [Fact]
    public void The_room_renders_the_text_it_rendered_before_its_sections_were_split()
    {
        Settle(GoldenPath("full.md"), HelpRoom.Render(Full), WriteRequested);
    }

    /// <summary>The sections a sparse or empty machine has no facts for.</summary>
    private static readonly string[] Unfilled =
        ["## Daoris's own plugins", "### Workspace `default`", "### Workspace `work`", "### Agents", "### Roles and their policies", "### Asks"];

    /// <summary>
    /// Each other machine's room against the full one: the sections it lacks and the ones only it has, and for each section that
    /// differs the sentence that makes it differ. Every other section is the full room's, byte for byte.
    /// </summary>
    private static readonly Dictionary<string, (string[] Gone, string[] Added, (string Section, string Says)[] Varies)> Variants = new()
    {
        ["full-access"] = ([], [],
        [
            ("# Ask Daoris", "This conversation runs with full access, as the person chose: you may act on this machine yourself"),
            ("## What you may do", "You act on this machine as the person, as they chose: a shell, file edits and the plugins' servers below,"),
        ]),
        ["sparse"] = (Unfilled, [],
        [
            ("## What you may do", "You have no shell, no web fetch or search, and you read no checkout"),
            ("## Done at a terminal", "Plugins that perform pull-request steps here: `example.github-pull-request`, `example.lands`."),
            ("## This machine, now", "- The driver: no agent is set for quests."),
            ("## Where work has got to", "The milestones were not read, so do not say where any work stands"),
            ("## What waits on what", "No repository is registered on this machine yet."),
        ]),
        ["empty"] = (Unfilled, [],
        [
            ("## What you may do", "You have no shell, no web fetch or search, and you read no checkout"),
            ("## Done at a terminal", "or the Plugins place), so never press a workflow naming one."),
            ("## This machine, now", "- The driver: no agent is set for quests."),
            ("## Where work has got to", "The milestones were not read, so do not say where any work stands"),
            ("## What waits on what", "No repository is registered on this machine yet."),
        ]),
        ["scoped"] = (["## Daoris's own plugins", "### Workspace `default`", "### Workspace `work`", "### Roles and their policies"], ["### Workspace `alpha`"],
        [
            ("# Ask Daoris", "This conversation is workspace `alpha`'s: the person started it from a window on that workspace."),
            ("## What you may do", "- `engine` (workspace `alpha`)\n- `console` (workspace `beta`)"),
            ("## Done at a terminal", "or the Plugins place), so never press a workflow naming one."),
            ("## This machine, now", "- The driver: quests run on `claude-code`, up to 3 sessions at once."),
            ("## Where work has got to", "reached: every commit of it is on `main`"),
            ("## What waits on what", "What each line waits on was not read, so do not say what any session waits on"),
            ("### Agents", "- `claude-code` (Claude Code 2.1.0): account `account-1` signed in"),
            ("### Asks", "- `#aa1` at `alpha`: “stream the tiles” (Proposed)"),
        ]),
    };

    /// <summary>The room's sections in order, each heading line with the text up to the next: the title's own text first.</summary>
    internal static List<(string Heading, string Text)> Sections(string room)
    {
        var sections = new List<(string, string)>();
        var heading = "";
        var text = new List<string>();
        foreach (var line in room.Split('\n'))
        {
            if (line.StartsWith('#') && line.TrimStart('#').StartsWith(' '))
            {
                if (heading.Length > 0 || text.Count > 0) sections.Add((heading, string.Join('\n', text)));
                (heading, text) = (line, []);
            }
            else text.Add(line);
        }

        sections.Add((heading, string.Join('\n', text)));
        return sections;
    }

    [Theory]
    [InlineData("full-access")]
    [InlineData("sparse")]
    [InlineData("empty")]
    [InlineData("scoped")]
    public void Another_machine_s_room_is_the_full_one_but_where_its_facts_differ(string machine)
    {
        var (gone, added, varies) = Variants[machine];
        var full = Sections(HelpRoom.Render(Full));
        var room = Sections(HelpRoom.Render(Named(machine)));
        var fullHeadings = full.Select(section => section.Heading).ToList();
        var headings = room.Select(section => section.Heading).ToList();

        Assert.Equal(gone, fullHeadings.Except(headings));
        Assert.Equal(added, headings.Except(fullHeadings));
        Assert.Equal(fullHeadings.Where(headings.Contains), headings.Where(fullHeadings.Contains));
        foreach (var (heading, text) in room.Where(section => fullHeadings.Contains(section.Heading)))
        {
            var before = full.First(section => section.Heading == heading).Text;
            var says = varies.Where(each => each.Section == heading).Select(each => each.Says).ToList();
            if (says.Count == 0)
            {
                Assert.True(before == text, $"{machine}: `{heading}` differs from the full room's, and no sentence here says why");
                continue;
            }

            Assert.Contains(says[0], text);
            Assert.DoesNotContain(says[0], before);
        }

        Assert.All(varies, each => Assert.Contains(each.Section, headings));
    }

    /// <summary>The switch writes the render, byte for byte and without a byte-order mark; without it a difference fails.</summary>
    [Fact]
    public void The_switch_writes_the_render_and_without_it_a_difference_fails()
    {
        var folder = Path.Combine(AppContext.BaseDirectory, "golden-write-" + Guid.NewGuid().ToString("N")[..8]);
        var path = Path.Combine(folder, "x.md");
        try
        {
            var text = "# Room\n\nline — two\n";
            Settle(path, text, write: true);
            Assert.Equal(System.Text.Encoding.UTF8.GetBytes(text), File.ReadAllBytes(path));
            Settle(path, text, write: false);
            Assert.ThrowsAny<Xunit.Sdk.XunitException>(() => Settle(path, text + "more\n", write: false));
        }
        finally
        {
            try { Directory.Delete(folder, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }

    /// <summary>The allow-list the room writes is the one it wrote before its kinds each named their own tool.</summary>
    [Fact]
    public void The_rooms_settings_are_the_ones_it_wrote_before()
    {
        var home = Path.Combine(TestHomes.Root, "daoris-help-golden-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            var room = HelpRoom.Prepare(home, Empty);

            Settle(GoldenPath("settings.json"), File.ReadAllText(Path.Combine(room, ".claude", "settings.json")), WriteRequested);
            using var settings = JsonDocument.Parse(File.ReadAllText(GoldenPath("settings.json")));
            Assert.Equal(
                settings.RootElement.GetProperty("permissions").GetProperty("allow").EnumerateArray().Select(rule => rule.GetString()!),
                HelpRoom.Allowed);
        }
        finally
        {
            try { Directory.Delete(home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }
}
