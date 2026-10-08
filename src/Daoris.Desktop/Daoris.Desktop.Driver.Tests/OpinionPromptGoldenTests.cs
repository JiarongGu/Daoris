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
        Diff = "C:/home/opinions/op1/candidate.diff",
        Verify = true,
        Minutes = 30,
    };

    /// <summary>The person's ask of one session's tip, any time: the candidate alone, read by reading.</summary>
    internal static readonly OpinionPacket Bare = new("asked", Candidate with { Commits = [Candidate.Tip], Paths = [new OpinionPath("M", "README.md")] });

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
    public void The_reviewer_s_instruction_is_its_golden_text(string which)
    {
        Assert.Equal(File.ReadAllText(GoldenPath($"{which}.md")), OpinionInstruction.Compose(which == "full" ? Full : Bare));
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
