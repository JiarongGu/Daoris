using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The reviewer's instruction rendered whole (XAGENT1d, D155 points 5 and 6; the second-agent design §4–§5): a pass over a
/// chain's work with everything the packet can carry, and one asked of a session's tip that carries nothing but the candidate.
/// A sentence added, moved or reworded shows here as the whole instruction's difference, byte for byte.
/// </summary>
/// <remarks>
/// The golden files under <c>golden/opinion-prompt/</c> are the instruction a reviewer is handed. A change to what it says
/// changes its golden file in the same commit, so review reads the instruction as the reviewer will; the facts beside it say
/// why the sentences that hold it read-only are there. This class runs no process, so it is in the fast half (MOD8).
/// </remarks>
public sealed class OpinionPromptGoldenTests
{
    private static readonly DateTimeOffset Asked = DateTimeOffset.Parse("2026-10-01T02:26:00Z");

    private static readonly OpinionCandidateRead Candidate = new(
        "reports",
        "1111111111111111111111111111111111111111",
        "2222222222222222222222222222222222222222",
        ["3333333333333333333333333333333333333333", "2222222222222222222222222222222222222222"],
        [new OpinionPath("M", "src/report.ts"), new OpinionPath("A", "docs/comparison.md"), new OpinionPath("R", "src/bridge/v3.ts")]);

    /// <summary>
    /// A chain's work in one repository, before it lands: its quest with a requirement, how its done answered it and what
    /// Daoris read of its evidence, the person's words on the ask, the repository's rules, the diff's file, and `verify`.
    /// </summary>
    internal static readonly OpinionPacket Full = new(OpinionRules.Landing, Candidate)
    {
        Quests =
        [
            new QuestView("abc123", "ask #a1b2c3", "reports", "Build the comparison report",
                "The ticket asks for a daily comparison report on the v3 bridge.", "Done")
            {
                Requirements =
                [
                    new QuestRequirementView("use the common-report module", "the report is a common-report entry")
                    {
                        Evidence = [new QuestEvidenceItem("docs/comparison.md"), new QuestEvidenceItem(null, "driver")],
                    },
                ],
                Answers = [new QuestAnswerView(1, "the report is registered as a common-report entry", null, null)],
                Note = "Built the report on the common-report module; the tests cover the daily window.",
                Evidence = new EvidenceVerdict(
                    "2222222222222222222222222222222222222222", EvidenceCodes.SessionEnd,
                    [
                        new EvidenceRead(1, "docs/comparison.md", null, EvidenceCodes.Found),
                        new EvidenceRead(1, null, "driver", EvidenceCodes.NoQueue),
                    ]),
            },
        ],
        Words = new AskWords("a1b2c3",
        [
            new AskWordView(AskWordView.Asked, "complete the ticket I logged, and this will need the v3 bridge", Asked),
            new AskWordView(AskWordView.Added, "test locally against dev first", Asked.AddHours(4), "s2", "abc123"),
        ]),
        Rules = new OpinionRulesRead(["AGENTS.md", "CLAUDE.md", "docs/README.md"], "docs/decisions", "daoris.gates.json"),
        Diff = "C:/data/opinions/op1/candidate.diff",
        Verify = true,
        Minutes = 30,
    };

    /// <summary>The person's ask of one session's tip, any time: the candidate alone, read by reading.</summary>
    internal static readonly OpinionPacket Bare = new("asked", Candidate with { Commits = [Candidate.Tip], Paths = [new OpinionPath("M", "README.md")] });

    /// <summary>
    /// The one recheck (XAGENT1e, design §6.5): the commits since the first pass's tip, handed the first pass's findings and how
    /// the session answered each, a fix's commit as git read it.
    /// </summary>
    internal static readonly OpinionPacket Recheck = new(
        OpinionRules.Landing,
        new OpinionCandidateRead(
            "reports", "2222222222222222222222222222222222222222", "5555555555555555555555555555555555555555",
            ["4444444444444444444444444444444444444444", "5555555555555555555555555555555555555555"],
            [new OpinionPath("M", "src/report.ts")]))
    {
        Quests = Full.Quests,
        Rules = Full.Rules,
        Minutes = 30,
        Rechecks = new OpinionRecheckOf(
            new OpinionView(
                "op1", OpinionRules.Landing, "first", "s1", "r1", "reports", "1111111111111111111111111111111111111111",
                "2222222222222222222222222222222222222222", "codex-acp", "another-maker", "given")
            {
                Findings =
                [
                    new OpinionFindingView(1, "must", "src/report.ts:42", "The window's end is exclusive, so the last day is dropped.",
                        "Each daily report misses its last day.", "Ran the report for one day: it held no rows.", "sure"),
                    new OpinionFindingView(2, "should", "src/bridge/v3.ts:7", "The table is written twice.",
                        "Two places to change for one rule.", "Read both files.", "likely"),
                    new OpinionFindingView(3, "note", "general", "The commit message names no quest.", "Harder to trace.", "Read the log.", "unsure"),
                ],
                Answers =
                [
                    new OpinionAnswerView(1, "fixed") { Commit = "4444444" },
                    new OpinionAnswerView(2, "rejected") { Evidence = "The twin test holds the two tables equal; one is the CLI's, one the driver's." },
                ],
            },
            new OpinionReading("s1", "5555555555555555555555555555555555555555", DateTimeOffset.Parse("2026-10-09T09:30:00Z"),
            [
                new OpinionAnswerRead(1, "must", "fixed") { Said = "fixed", Commit = "4444444", Fix = "4444444444444444444444444444444444444444" },
                new OpinionAnswerRead(2, "should", "rejected") { Said = "rejected" },
                new OpinionAnswerRead(3, "note", "unresolved") { Why = OpinionAnswerWhy.NotAnswered },
            ])),
    };

    private static string RepositoryRoot()
    {
        var at = new DirectoryInfo(AppContext.BaseDirectory);
        while (at is not null && !File.Exists(Path.Combine(at.FullName, "daoris.json"))) at = at.Parent;
        return at?.FullName ?? throw new InvalidOperationException("no workspace root above the test binary");
    }

    internal static string GoldenPath(string file) =>
        Path.Combine(RepositoryRoot(), "src", "Daoris.Desktop", "Daoris.Desktop.Driver.Tests", "golden", "opinion-prompt", file);

    [Theory]
    [InlineData("full")]
    [InlineData("bare")]
    [InlineData("recheck")]
    public void The_reviewer_s_instruction_is_its_golden_text(string which)
    {
        Assert.Equal(
            File.ReadAllText(GoldenPath($"{which}.md")),
            OpinionInstruction.Compose(which switch { "full" => Full, "bare" => Bare, _ => Recheck }));
    }

    /// <summary>
    /// The recheck (XAGENT1e, design §6.5) is told what the first reading found and how each was answered, each answer the
    /// session's claim beside what git read of a fix; it says of each first-pass finding that it stands or is withdrawn, and its
    /// findings go to the person, never back to the session.
    /// </summary>
    [Fact]
    public void The_recheck_is_handed_the_first_reading_and_says_stands_or_withdrawn_for_the_person()
    {
        var text = OpinionInstruction.Compose(Recheck);

        Assert.Contains("what the session made in answer to a first reading's findings", text);
        Assert.Contains("- Finding 1 (must, sure), at `src/report.ts:42`: The window's end is exclusive", text);
        Assert.Contains("Answered: fixed, in `4444444444444444444444444444444444444444`, which Daoris read from git", text);
        Assert.Contains("Answered: rejected, with this evidence: The twin test holds", text);
        Assert.Contains("Answered: not answered.", text);
        Assert.Contains("`rechecked`", text);
        Assert.Contains("`stands` or is `withdrawn`", text);
        Assert.Contains("they go to the person", text);
        Assert.DoesNotContain("the session that did the work checks each against the code and answers it", text);
        // A first pass is told none of it.
        Assert.DoesNotContain("`rechecked`", OpinionInstruction.Compose(Full));
    }

    /// <summary>
    /// What holds it read-only in words (§5.2, §5.5): the copy it reads in, every act it never does, the repository's own
    /// instructions to write set aside, and no one to ask. The words are the instruction's half; the copy and the rules are
    /// the other, and hold whether or not these are followed.
    /// </summary>
    [Fact]
    public void It_is_told_it_reads_a_copy_nothing_is_taken_back_from_and_every_act_it_never_does()
    {
        var text = OpinionInstruction.Compose(Bare);

        Assert.Contains("a copy of the repository made for this reading", text);
        Assert.Contains("Nothing in it is taken back", text);
        foreach (var never in new[]
                 {
                     "edit, write or delete a file", "commit, merge, rebase or push", "deploy or publish",
                     "change a permission rule or propose one", "a process, a port or a server of the person's",
                     "take, close, decline or publish a quest", "ask for another opinion",
                 })
        {
            Assert.Contains(never, text);
        }

        Assert.Contains("here they do not apply", text);
        Assert.Contains("Nobody will answer a question from you", text);
        // `verify` off: it reads, and runs none of the repository's programs.
        Assert.Contains("Do not build, test or run", text);
        Assert.DoesNotContain("declares safe", text);
    }

    /// <summary>Each finding's parts are the tool's (design §6.1), so the instruction and `opinion_give` ask for the same.</summary>
    [Fact]
    public void It_says_its_opinion_once_through_opinion_give_with_each_finding_s_parts()
    {
        var text = OpinionInstruction.Compose(Full);

        Assert.Contains("`opinion_give`", text);
        foreach (var part in new[] { "`must`", "`should`", "`note`", "`general`", "`sure`", "`likely`", "`unsure`", "what you read" })
        {
            Assert.Contains(part, text);
        }

        Assert.Contains("never `no issues`", text);
        Assert.Contains("at most 30 minutes", text);
    }

    /// <summary>
    /// The working session's closing note is handed as its claim, never as a fact (§4), and the person's words as theirs; what
    /// Daoris read of the evidence is the one fact it carries.
    /// </summary>
    [Fact]
    public void The_working_session_s_close_is_its_claim_and_the_evidence_is_what_Daoris_read()
    {
        var text = OpinionInstruction.Compose(Full);

        Assert.Contains("as its own claim", text);
        Assert.Contains("> Built the report on the common-report module", text);
        Assert.Contains("> use the common-report module", text);
        Assert.Contains("`docs/comparison.md`: found", text);
        Assert.Contains("gate `driver`: no-queue", text);
        // `verify` on: what the repository declares safe, in this copy, and nothing else.
        Assert.Contains("declares safe to run unasked", text);
        Assert.Contains("`daoris.gates.json`", text);
    }

    /// <summary>The instruction names no machine path but the packet's own file, and none at all where it has none (D47 §4).</summary>
    [Fact]
    public void The_bare_instruction_names_the_commits_and_how_to_read_the_diff_in_the_copy()
    {
        var text = OpinionInstruction.Compose(Bare);

        Assert.Contains("`1111111111111111111111111111111111111111`", text);
        Assert.Contains("`2222222222222222222222222222222222222222`", text);
        Assert.Contains("git diff 1111111111111111111111111111111111111111...2222222222222222222222222222222222222222", text);
        Assert.DoesNotContain(":/", text);
    }
}
