using Daoris.Driver;
using HelpRoomGoldenTests = Daoris.Driver.Tests.HelpRoomGoldenTests;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A quest's instruction rendered whole, as the harness reads it: a repository's quest that
/// carries nothing, a quest an ask asked with everything the look and the close speak to, a carry-on after the
/// person answered, and a session told its process in a tree of its own. A sentence added, moved or
/// reworded shows here as the whole instruction's difference, byte for byte.
/// </summary>
/// <remarks>
/// The golden files under <c>golden/target-prompt/</c> are the instruction a session is handed. A change to what it says
/// changes its golden file in the same commit, so review reads the instruction as the session will; the tests beside it
/// (<see cref="AskAndWaitPromptTests"/> and the handed tests) say why each sentence is there. This class runs no process,
/// so it is in the fast half.
/// </remarks>
public sealed class TargetPromptGoldenTests
{
    private static readonly DateTimeOffset Asked = DateTimeOffset.Parse("2026-10-01T02:26:00Z");

    /// <summary>A quest one repository asked of another: no ask, no index in its tree, nothing carried.</summary>
    internal static readonly SessionTarget Bare = new(
        QuestId: "abc123",
        Title: "Add the note field",
        Body: "The report needs a note column.",
        Asker: "console-api",
        Repository: "console-ui",
        Root: "C:/work/console-ui",
        ServiceUrl: "http://localhost:5177");

    /// <summary>The person's words on the ask: its sentence, an answer to a parked session, and a message added since.</summary>
    private static readonly AskWords Words = new("a1b2c3",
    [
        new AskWordView(AskWordView.Asked, "complete the ticket I logged, and this will need the v3 bridge", Asked),
        new AskWordView(AskWordView.Answered, "use the common-report module", Asked.AddHours(3), "s1", "q1"),
        new AskWordView(AskWordView.Added, "test locally against dev first", Asked.AddHours(4), "s2", "abc123"),
    ])
    {
        GoAheads =
        [
            new GoAheadView(1, "write", "production", "dashboard configuration",
                [new GoAheadRequestView("s1", "q1", Asked.AddHours(2), "the tile reads its figure from it")])
            {
                Answer = new GoAheadAnswerView(true, "run the put", Asked.AddHours(3)),
            },
            new GoAheadView(2, "release", "production", "report menu entries",
                [new GoAheadRequestView("s2", "abc123", Asked.AddHours(4), "the report is reached from the menu")]),
        ],
    };

    /// <summary>
    /// A quest an ask asked, with everything the look and the close speak to: a link, a requirement, the person's words, the
    /// go-aheads, a checkout it may read, and its standing block naming the indexes its tree keeps and the person's choices.
    /// </summary>
    internal static readonly SessionTarget Full = new(
        QuestId: "abc123",
        Title: "Build the comparison report",
        Body: "The ticket asks for a daily comparison report on the v3 bridge.",
        Asker: "ask #a1b2c3",
        Repository: "reports",
        Root: "C:/work/reports",
        ServiceUrl: "http://localhost:5177")
    {
        Links = ["https://tickets.example/T-9"],
        // The evidence its check turns on, which Daoris reads in the branch's last commit when the session ends.
        Requirements =
        [
            new QuestRequirementView("use the common-report module", "the report is a common-report entry")
            {
                Evidence = [new QuestEvidenceItem("docs/comparison.md")],
            },
        ],
        Words = Words,
        ReadsAcross = [new AcrossCheckout("bridge", "C:/work/bridge")],
        Block = new StandingFacts
        {
            Repository = "reports",
            Workspace = "work",
            Checkouts = true,
            Indexes = ["docs/README.md", ".claude/rules/RULES_INDEX.md", ".claude/rules/RULES_INDEX_CROSS.md"],
            Choices = new StandingAnswer("dev writes allowed; test locally against dev; prod only on a yes.", Asked.AddDays(-1)),
        },
    };

    /// <summary>A carry-on after the person answered a parked session, the answer among their words on the ask.</summary>
    internal static readonly SessionTarget CarryOn = Full with
    {
        CutOff = "asked the person (merge, sign-in), and was answered: use the common-report module",
        PersonSaid = "use the common-report module",
        InFlight = [" M src/report.ts"],
    };

    /// <summary>
    /// A carry-on handed Daoris's brief of its line in place of the last plan
    /// and words, the brief's sections counted on their own within the account: two parts, the person's word to the first, a plan, a
    /// failed check by a declared gate, and the tree as read.
    /// </summary>
    internal static readonly SessionTarget CarryOnBrief = CarryOn with
    {
        // The memory its line read is its standing block's, handed with the brief.
        Block = CarryOn.Block! with { Memory = ["reports: the totals row reads the bridge's daily figure, never the hourly one"] },
        PersonSaid = null,
        CutOff = "the agent's turn failed with the quest still taken.",
        Brief = ContextBrief.Compose(new BriefLine(
            HandOverWhy.CarryOn,
            [
                new BriefPart("s1a2b3c4", "claude-code",
                [
                    new SessionEvent { Kind = SessionEventKind.User, Origin = "target", Text = "You are the agent for `reports`…", Account = new InstructionAccount(30, []) },
                    new SessionEvent { Kind = SessionEventKind.Message, Text = "I read the report module." },
                    new SessionEvent { Kind = SessionEventKind.Turn, StopReason = "end_turn" },
                    new SessionEvent { Kind = SessionEventKind.User, Origin = "person", Id = "w1", Text = "Use the common-report module." },
                    new SessionEvent { Kind = SessionEventKind.Plan, Entries = [new("Move the totals", "completed"), new("Add the note column", "in_progress")] },
                    new SessionEvent
                    {
                        Kind = SessionEventKind.Tool, Id = "t1", Title = "Edit src/report.ts", ToolKind = "edit", Status = "completed",
                        Locations = ["C:/work/reports/src/report.ts"],
                    },
                    new SessionEvent
                    {
                        Kind = SessionEventKind.Tool, Id = "t2", Title = "npm test", ToolKind = "execute", Status = "failed",
                        Input = """{"command":"npm test"}""", Output = "FAIL src/report.test.ts\n  the note column is empty",
                    },
                ]),
            ])
        {
            Tree = "C:/work/reports",
            State = new BriefTree("4f2a9c1 Move the totals", [" M src/report.ts"], ["4f2a9c1 Move the totals"]),
            Gates = [new HeavyGate("unit", "npm test", null, false)],
        }),
    };

    /// <summary>
    /// An open quest carried on in the tree of a session that committed work and failed before it took it, handed
    /// Daoris's brief of that line: it takes the quest, then finishes the work rather than starting again.
    /// </summary>
    internal static readonly SessionTarget Retried = Bare with
    {
        Retried = "the connector could not start.",
        Brief = CarryOnBrief.Brief,
    };

    /// <summary>
    /// A set-up step: a chain's step that shows the work of the step before, which its tree grew from,
    /// in a local environment for the person's review, by the procedure the rule names.
    /// </summary>
    internal static readonly SessionTarget SetUpStep = new(
        QuestId: "def456",
        Title: "Show #abc123 in `local` for review",
        Body: "Set the work of #abc123 up in `local` for the person's review, by the route this repository documents for `local`, and "
              + "show it to them there. Your tree holds that work: add none of your own. Say what you showed, then close this quest "
              + "done; it then waits for the person's review.",
        Asker: "ask #a1b2c3",
        Repository: "reports",
        Root: "C:/work/reports",
        ServiceUrl: "http://localhost:5177")
    {
        Parent = "abc123",
        GrewFrom = "daoris/s-1a2b3c4d",
        SetUp = new ReviewEnvironment("local", "local", "README.md", "http://localhost:4200"),
        // Its own section in the account, from the rule set for its repository here (REVIEWENV1c2).
        SetUpFrom = ReviewSource.Repository,
    };

    /// <summary>
    /// A session in a tree of its own: its landing rule names the pull request's branch by
    /// the ticket, and its commit rule, set for its workspace, asks the person first and quotes their convention.
    /// </summary>
    internal static readonly SessionTarget OwnTree = Bare with
    {
        LandsOn = new LandingPlan(LandingForm.Branch, "feature/T-9-add-the-note-field", LandingSource.Repository),
        Commits = new ResolvedCommits(
            new CommitRule(CommitRules.Ask, "Subject in the imperative, the ticket key first: `T-9 Add the note field`."),
            OpinionSource.Workspace),
    };

    /// <summary>
    /// A further part of a session: a new quest of the session's entry, gone into the tree its
    /// earlier part left free, which left a change uncommitted there. Handed what the earlier part did, its tree and its branch.
    /// </summary>
    internal static readonly SessionTarget Part = Bare with
    {
        Parts = new SessionParts(
        [
            new EarlierPart("283ef343", "3068f747", "completed")
            {
                Note = "asked #711d2524 of `console-api`, and waits on its answer.", Tree = "s-283ef343", Branch = "daoris/s-283ef343",
            },
            new EarlierPart("1f0e9d8c", "3068f747", "failed") { Note = "the agent refused the call: Authentication required." },
        ])
        {
            Into = "283ef343",
        },
        InFlight = [" M src/notes.ts"],
    };

    /// <summary>A further part whose session's tree a live part holds: it runs alongside, in a tree of its own.</summary>
    internal static readonly SessionTarget PartAlongside = Bare with
    {
        Parts = new SessionParts(
            [new EarlierPart("62c005bc", "28b052ac", "working") { Tree = "s-62c005bc", Branch = "daoris/s-62c005bc" }])
        {
            HeldBy = "62c005bc",
        },
    };

    /// <summary>
    /// A sibling's request on an ask, whose line of work is handed what waited on the ask's channel: the
    /// person's message to everyone, as their words, and another session's question to its repository, as that agent's claim.
    /// </summary>
    internal static readonly SessionTarget Channel = Bare with
    {
        Channel = new ChannelHeld("a1b2c3", "ask:a1b2c3/console-ui",
        [
            new ChannelItemView(ChannelItemView.Message, Asked.AddHours(1))
            {
                Id = "m1a2", Person = true, Text = "Keep the note optional; most rows have none.",
            },
            new ChannelItemView(ChannelItemView.Message, Asked.AddHours(2))
            {
                Id = "m3b4", Session = "62c005bc", Repository = "console-api", Agent = "claude-code", Entry = "ask:a1b2c3/console-api",
                To = "console-ui", Re = "m1a2", Question = true,
                Text = "Should the API send the note as null or as an empty string?\nThe report reads both.",
            },
        ], "s1"),
    };

    private static string RepositoryRoot()
    {
        var at = new DirectoryInfo(AppContext.BaseDirectory);
        while (at is not null && !File.Exists(Path.Combine(at.FullName, "daoris.json"))) at = at.Parent;
        return at?.FullName ?? throw new InvalidOperationException("no workspace root above the test binary");
    }

    internal static string GoldenPath(string file) =>
        Path.Combine(RepositoryRoot(), "src", "Daoris.Desktop", "Daoris.Desktop.Driver.Tests", "golden", "target-prompt", file);

    /// <summary>
    /// The standing block a build's start composes for `console-ui`, declaring two lanes, under <paramref name="standing"/>.
    /// </summary>
    internal static StandingFacts Routed(string standing)
    {
        var config = standing == RoutingStanding.NotDriven ? DriverConfig.Empty : DriverConfig.Empty.WithDrivable("console-ui", true);
        var facts = StandingBlocks.OnMachine(
            new StandingFacts { Repository = "console-ui" }, ParticipantWork.Build, config,
            [new RepoView("console-ui", true, "C:/work/console-ui") { Lanes = ["core", "web"] }], "work", apart: standing == RoutingStanding.Open);
        Assert.Equal(standing, facts.Routing);
        return facts;
    }

    internal static SessionTarget Of(string which) => which switch
    {
        "bare" => Bare,
        "full" => Full,
        "carry-on" => CarryOn,
        "carry-on-brief" => CarryOnBrief,
        "retried" => Retried,
        "set-up" => SetUpStep,
        "own-tree" => OwnTree,
        "part" => Part,
        "part-alongside" => PartAlongside,
        "channel" => Channel,
        // A quest its closure graph closes, bound at its take: its session says `ready`, never `done`.
        "ready" => Bare with { Ready = true },
        // A quest's session in a driven repository that declares lanes and opens trees of its own, told its routing standing
        // in its standing block.
        "routed" => Bare with { Browser = "Daoris has handed you its browser.", Block = Routed("open") },
        "routed-one-at-a-time" => Bare with { Block = Routed("one-at-a-time") },
        "routed-not-driven" => Bare with { Block = Routed("not-driven") },
        // HEAVYRUN1b1: a repository that marks heavy gates, its session told to run them once, at its end, through `run-heavy`.
        "heavy" => Bare with { Heavy = HeavyTold.Words(["build", "test"]) },
        // A light development pipeline, its light gates and its heavy gates as the verification's, refused by the
        // harness of an agent that takes Daoris's rules, and said to one that takes none.
        "light" => Bare with { Heavy = HeavyTold.LightWords(["universal", "code-map"], ["build", "test"], takesRules: true) },
        "light-said" => Bare with { Heavy = HeavyTold.LightWords(["universal", "code-map"], ["build", "test"], takesRules: false) },
        _ => throw new ArgumentOutOfRangeException(nameof(which), which, null),
    };

    /// <summary>
    /// A bound quest's resume and carry-on say `ready` as its first start does, and every other quest's say `done`,
    /// so an install with no closure graph hands no `ready`.
    /// </summary>
    [Fact]
    public void A_bound_quests_every_start_says_ready_and_every_other_quests_says_done()
    {
        var resumed = Bare with { Answered = new QuestView("q9", "console-ui", "console-api", "Which field?", "", "Done") { Note = "The note." } };
        var carried = CarryOn;
        foreach (var target in new[] { Bare, resumed, carried, Retried })
        {
            var ready = TargetPrompt.Compose(target with { Ready = true });
            Assert.Contains("`ready` when its work is done", ready, StringComparison.Ordinal);
            Assert.Contains("Never `done`", ready, StringComparison.Ordinal);
            Assert.DoesNotContain("`done` when it has landed", ready, StringComparison.Ordinal);

            var today = TargetPrompt.Compose(target);
            Assert.Contains("`done` when it has landed", today, StringComparison.Ordinal);
            Assert.DoesNotContain("`ready`", today, StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData("bare")]
    [InlineData("full")]
    public void The_quests_instruction_is_its_golden_text(string which)
    {
        // The golden's switch: DAORIS_GOLDEN_WRITE=1 writes the golden from the render, and without it a difference fails.
        HelpRoomGoldenTests.Settle(GoldenPath($"{which}.md"), TargetPrompt.Compose(Of(which)), HelpRoomGoldenTests.WriteRequested);
        // The text the account is kept beside is the same instruction, byte for byte.
        Assert.Equal(File.ReadAllText(GoldenPath($"{which}.md")), TargetPrompt.Composed(Of(which)).Text);
    }

    /// <summary>
    /// Every other start against the golden one it grows from: for each section that differs, or that only it has, the sentence
    /// that makes it differ. Every other section is that golden's, byte for byte, in its order.
    /// </summary>
    private static readonly Dictionary<string, (string Base, (string Section, string Says)[] Varies)> Variants = new()
    {
        ["carry-on"] = ("full",
        [
            ("quest", "You are carrying on quest `#abc123`, asked by `ask #a1b2c3`. It is already taken"),
            ("carry-on", "An earlier session on this quest stopped to ask the person, and they answered"),
            ("close", "Finish from there rather than starting again"),
        ]),
        ["carry-on-brief"] = ("full",
        [
            ("quest", "You are carrying on quest `#abc123`, asked by `ask #a1b2c3`. It is already taken"),
            ("carry-on", "was cut off before it closed it: the agent's turn failed with the quest still taken."),
            ("brief-opening", "continue session `s1a2b3c4`, which ran on claude-code"),
            ("brief-turns", "What was said and done, in order, each word with the turn that took it."),
            ("brief-words", "edited `src/report.ts`; a command failed: `npm test`."),
            ("brief-state", "The state of the work, as Daoris read it when it composed this brief:"),
            ("close", "Finish from there rather than starting again"),
            ("block", "Memory this line read, as it stands now:"),
        ]),
        ["retried"] = ("bare",
        [
            ("carry-on", "committed work in this tree and failed before it took the quest: the connector could not start."),
            ("brief-opening", "continue session `s1a2b3c4`, which ran on claude-code"),
            ("brief-turns", "What was said and done, in order, each word with the turn that took it."),
            ("brief-words", "edited `src/report.ts`; a command failed: `npm test`."),
            ("brief-state", "The state of the work, as Daoris read it when it composed this brief:"),
            ("close", "then finish its work from there rather"),
        ]),
        ["set-up"] = ("bare",
        [
            ("quest", "You are the agent for `reports`, working inside its own repository and nowhere else."),
            ("carried", "It follows quest `#abc123`, which is done"),
            ("set-up", "This quest is a set-up step. It shows the work of its chain to the person in `local`"),
            ("close", "First take the quest (respond to `#def456` with `take`)"),
            ("asking", "then respond to `#def456` with `wait`"),
            ("attributed", "A go-ahead is asked once, on the ask: where your connector offers `go_ahead_ask`"),
        ]),
        ["own-tree"] = ("bare", [("landing", "the person puts this tree's branch on `feature/T-9-add-the-note-field` and opens a pull request")]),
        ["part"] = ("bare", [("carry-on", "You are in session `283ef343`'s tree, on its branch, so its work is here")]),
        ["part-alongside"] = ("bare", [("carry-on", "Session `62c005bc` is still at work in that tree, so you run beside it in a tree of your own")]),
        ["channel"] = ("bare", [("carry-on", "On the ask's channel (ask `#a1b2c3`) since this line of work last read it")]),
        ["ready"] = ("bare", [("close", "then say it is ready: `ready` when its work is done")]),
        ["heavy"] = ("bare", [("close", "run this repository's heavy gates (`build`, `test`) through `daoris gate")]),
        ["light"] = ("bare", [("close", "`daoris gate run` refuses this tree, and your harness refuses each one's declared command.")]),
        ["light-said"] = ("bare", [("close", "Your agent takes no rules from Daoris, so nothing refuses their commands in your shell: they are said to you instead.")]),
        ["routed"] = ("bare", [("close", "Daoris has handed you its browser."), ("block", "Routing standing here: open.")]),
        ["routed-one-at-a-time"] = ("bare", [("block", "Routing standing here: one at a time.")]),
        ["routed-not-driven"] = ("bare", [("block", "Routing standing here: not driven.")]),
    };

    /// <summary>The instruction cut at its account's sections, each by name.</summary>
    private static List<(string Name, string Text)> Sections(SessionTarget target)
    {
        var (text, account) = TargetPrompt.Composed(target);
        var sections = new List<(string, string)>();
        var at = 0;
        foreach (var section in account.Sections.Where(section => section.Chars is > 0))
        {
            sections.Add((section.Name, text.Substring(at, section.Chars!.Value)));
            at += section.Chars!.Value;
        }

        Assert.Equal(text.Length, at);
        return sections;
    }

    [Theory]
    [InlineData("carry-on")]
    [InlineData("carry-on-brief")]
    [InlineData("retried")]
    [InlineData("set-up")]
    [InlineData("own-tree")]
    [InlineData("part")]
    [InlineData("part-alongside")]
    [InlineData("channel")]
    [InlineData("ready")]
    [InlineData("heavy")]
    [InlineData("light")]
    [InlineData("light-said")]
    [InlineData("routed")]
    [InlineData("routed-one-at-a-time")]
    [InlineData("routed-not-driven")]
    public void Another_start_is_the_golden_it_grows_from_but_where_its_facts_differ(string which)
    {
        var (from, varies) = Variants[which];
        var before = Sections(Of(from));
        var after = Sections(Of(which));
        var names = before.Select(section => section.Name).ToList();

        Assert.Equal(names, after.Select(section => section.Name).Where(names.Contains));
        Assert.All(after.Where(section => !names.Contains(section.Name)), section => Assert.Contains(section.Name, varies.Select(each => each.Section)));
        foreach (var (name, text) in after)
        {
            var says = varies.FirstOrDefault(each => each.Section == name).Says;
            var was = before.FirstOrDefault(section => section.Name == name).Text ?? "";
            if (says is null)
            {
                Assert.True(was == text, $"{which}: `{name}` differs from {from}'s, and no sentence here says why");
                continue;
            }

            Assert.Contains(says, text);
            Assert.DoesNotContain(says, was);
        }

        Assert.All(varies, each => Assert.Contains(each.Section, after.Select(section => section.Name)));
    }

    /// <summary>
    /// The person's accepting is their press on the session's page, so every instruction says that words in the
    /// conversation land nothing, and where a proposal waits.
    /// </summary>
    [Theory]
    [InlineData("bare")]
    [InlineData("full")]
    [InlineData("carry-on")]
    [InlineData("set-up")]
    [InlineData("own-tree")]
    [InlineData("part")]
    [InlineData("part-alongside")]
    [InlineData("channel")]
    public void The_instruction_points_accepting_words_to_the_press(string which)
    {
        var text = TargetPrompt.Compose(Of(which));
        Assert.Contains("*Accept…*", text);
        Assert.Contains("land nothing", text);
        Assert.Contains("*Proposals*", text);
    }

    /// <summary>
    /// The account kept beside each instruction, as the session's record writes it: each section's size, its
    /// source and what its bound left out. A section added, moved or resized shows here as the account's difference, and the
    /// sizes add up to the golden text's length.
    /// </summary>
    [Theory]
    [InlineData("bare")]
    [InlineData("full")]
    public void The_account_beside_it_is_its_golden(string which)
    {
        var account = TargetPrompt.Composed(Of(which)).Account;

        HelpRoomGoldenTests.Settle(GoldenPath($"{which}.account.json"), Written(account), HelpRoomGoldenTests.WriteRequested);
        Assert.Equal(TargetPrompt.Compose(Of(which)).Length, account.Chars);
    }

    /// <summary>
    /// The routing rides the standing block at the instruction's end, once, for a quest's session
    /// of every kind of start, and nowhere else in it. An intake's block, composed for a work that publishes to no lane of its own, carries
    /// none.
    /// </summary>
    [Fact]
    public void The_routing_rides_the_standing_block_once_at_the_end_and_an_intake_s_block_never_carries_it()
    {
        const string Head = "Routing standing here: ";
        foreach (var target in new[] { Bare, CarryOn, Full, Retried, SetUpStep, OwnTree })
        {
            var routed = TargetPrompt.Compose(target with { Block = Routed("open") });
            var at = routed.IndexOf(Head, StringComparison.Ordinal);
            Assert.Equal(at, routed.LastIndexOf(Head, StringComparison.Ordinal));
            Assert.True(routed.IndexOf(StandingBlock.Opening, StringComparison.Ordinal) is var opening && opening >= 0 && opening < at);
            Assert.True(routed.IndexOf("Never write outside this repository", StringComparison.Ordinal) < opening);
            Assert.DoesNotContain(Head, TargetPrompt.Compose(target));
        }

        var intakeBlock = StandingBlocks.OnMachine(new StandingFacts(), ParticipantWork.Intake, DriverConfig.Empty, [], "work");
        var intake = Bare with { Prompt = "You are the intake for ask #a1b2c3.", Block = intakeBlock };
        Assert.DoesNotContain(Head, TargetPrompt.Compose(intake));
        Assert.EndsWith(StandingBlock.Compose(intakeBlock).Text, TargetPrompt.Compose(intake), StringComparison.Ordinal);
    }

    /// <summary>The account as the record's JSON spells it, indented and LF, so a golden reads the same on every machine.</summary>
    internal static string Written(InstructionAccount account) =>
        System.Text.Json.JsonSerializer.Serialize(
            account, new System.Text.Json.JsonSerializerOptions(SessionEventText.Json) { WriteIndented = true, NewLine = "\n" }) + "\n";
}
