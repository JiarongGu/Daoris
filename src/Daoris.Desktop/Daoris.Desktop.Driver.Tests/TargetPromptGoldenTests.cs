using Daoris.Driver;
using HelpRoomGoldenTests = Daoris.Driver.Tests.HelpRoomGoldenTests;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A quest's instruction rendered whole (KNOWUSE1c, KNOWUSE1d), as the harness reads it: a repository's quest that
/// carries nothing, a quest an ask asked with everything the look and the close speak to, a carry-on after the
/// person answered, and a session told its process in a tree of its own (PROCESS1d-i). A sentence added, moved or
/// reworded shows here as the whole instruction's difference, byte for byte.
/// </summary>
/// <remarks>
/// The golden files under <c>golden/target-prompt/</c> are the instruction a session is handed. A change to what it says
/// changes its golden file in the same commit, so review reads the instruction as the session will; the tests beside it
/// (<see cref="AskAndWaitPromptTests"/> and the handed tests) say why each sentence is there. This class runs no process,
/// so it is in the fast half (MOD8).
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
    /// go-aheads, a standing answer, a checkout it may read, and the indexes its tree keeps.
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
        // EVID1b: the evidence its check turns on, which Daoris reads in the branch's last commit when the session ends.
        Requirements =
        [
            new QuestRequirementView("use the common-report module", "the report is a common-report entry")
            {
                Evidence = [new QuestEvidenceItem("docs/comparison.md")],
            },
        ],
        Words = Words,
        Standing = new StandingAnswer("dev writes allowed; test locally against dev; prod only on a yes.", Asked.AddDays(-1)),
        ReadsAcross = [new AcrossCheckout("bridge", "C:/work/bridge")],
        Indexes = ["docs/README.md", ".claude/rules/RULES_INDEX.md", ".claude/rules/RULES_INDEX_CROSS.md"],
    };

    /// <summary>A carry-on after the person answered a parked session, the answer among their words on the ask.</summary>
    internal static readonly SessionTarget CarryOn = Full with
    {
        CutOff = "asked the person (merge, sign-in), and was answered: use the common-report module",
        PersonSaid = "use the common-report module",
        InFlight = [" M src/report.ts"],
    };

    /// <summary>
    /// A set-up step (REVIEWENV1c, design §2.2): a chain's step that shows the work of the step before, which its tree grew from,
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
    /// A session in a tree of its own (PROCESS1d-i, D164's amendment point 4): its landing rule names the pull request's branch by
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
    /// A further part of a session (SESSONE1d, D167 point 4 as amended): a new quest of the session's entry, gone into the tree its
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

    /// <summary>A further part whose session's tree a live part holds (SESSONE1d): it runs alongside, in a tree of its own.</summary>
    internal static readonly SessionTarget PartAlongside = Bare with
    {
        Parts = new SessionParts(
            [new EarlierPart("62c005bc", "28b052ac", "working") { Tree = "s-62c005bc", Branch = "daoris/s-62c005bc" }])
        {
            HeldBy = "62c005bc",
        },
    };

    /// <summary>
    /// A sibling's request on an ask, whose line of work is handed what waited on the ask's channel (SESSONE2b, D167 point 7): the
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
        ], Asked.AddHours(2)),
    };

    private static string RepositoryRoot()
    {
        var at = new DirectoryInfo(AppContext.BaseDirectory);
        while (at is not null && !File.Exists(Path.Combine(at.FullName, "daoris.json"))) at = at.Parent;
        return at?.FullName ?? throw new InvalidOperationException("no workspace root above the test binary");
    }

    internal static string GoldenPath(string file) =>
        Path.Combine(RepositoryRoot(), "src", "Daoris.Desktop", "Daoris.Desktop.Driver.Tests", "golden", "target-prompt", file);

    internal static SessionTarget Of(string which) => which switch
    {
        "bare" => Bare,
        "full" => Full,
        "carry-on" => CarryOn,
        "set-up" => SetUpStep,
        "own-tree" => OwnTree,
        "part" => Part,
        "part-alongside" => PartAlongside,
        "channel" => Channel,
        // FABRIC-CLOSE1l (CLOSE1g): a quest its closure graph closes, bound at its take: its session says `ready`, never `done`.
        "ready" => Bare with { Ready = true },
        // HEAVYRUN1b1: a repository that marks heavy gates, its session told to run them once, at its end, through `run-heavy`.
        "heavy" => Bare with { Heavy = HeavyTold.Words(["build", "test"]) },
        _ => throw new ArgumentOutOfRangeException(nameof(which), which, null),
    };

    /// <summary>
    /// FABRIC-CLOSE1l (CLOSE1g): a bound quest's resume and carry-on say `ready` as its first start does, and every other quest's say `done`
    /// as they did, so an install with no closure graph hands no `ready`.
    /// </summary>
    [Fact]
    public void A_bound_quests_every_start_says_ready_and_every_other_quests_says_done()
    {
        var resumed = Bare with { Answered = new QuestView("q9", "console-ui", "console-api", "Which field?", "", "Done") { Note = "The note." } };
        var carried = CarryOn;
        foreach (var target in new[] { Bare, resumed, carried })
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
    [InlineData("carry-on")]
    [InlineData("set-up")]
    [InlineData("own-tree")]
    [InlineData("part")]
    [InlineData("part-alongside")]
    [InlineData("channel")]
    [InlineData("ready")]
    [InlineData("heavy")]
    public void The_quests_instruction_is_its_golden_text(string which)
    {
        // GOLDEN1's switch: DAORIS_GOLDEN_WRITE=1 writes the golden from the render, and without it a difference fails.
        HelpRoomGoldenTests.Settle(GoldenPath($"{which}.md"), TargetPrompt.Compose(Of(which)), HelpRoomGoldenTests.WriteRequested);
        // The text the account is kept beside is the same instruction, byte for byte (CONTEXT1).
        Assert.Equal(File.ReadAllText(GoldenPath($"{which}.md")), TargetPrompt.Composed(Of(which)).Text);
    }

    /// <summary>
    /// ACCEPTWORDS1: the person's accepting is their press on the session's page, so every instruction says that words in the
    /// conversation land nothing, and where a proposal waits (a session once thanked the person for "accepting" and nothing landed).
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
    /// The account kept beside each instruction (CONTEXT1), as the session's record writes it: each section's size, its
    /// source and what its bound left out. A section added, moved or resized shows here as the account's difference, and the
    /// sizes add up to the golden text's length.
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
    [InlineData("ready")]
    [InlineData("heavy")]
    public void The_account_beside_it_is_its_golden(string which)
    {
        var account = TargetPrompt.Composed(Of(which)).Account;

        HelpRoomGoldenTests.Settle(GoldenPath($"{which}.account.json"), Written(account), HelpRoomGoldenTests.WriteRequested);
        Assert.Equal(TargetPrompt.Compose(Of(which)).Length, account.Chars);
    }

    /// <summary>The account as the record's JSON spells it, indented and LF, so a golden reads the same on every machine.</summary>
    internal static string Written(InstructionAccount account) =>
        System.Text.Json.JsonSerializer.Serialize(
            account, new System.Text.Json.JsonSerializerOptions(SessionEventText.Json) { WriteIndented = true, NewLine = "\n" }) + "\n";
}
