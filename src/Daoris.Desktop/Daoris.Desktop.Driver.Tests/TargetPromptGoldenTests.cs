using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A quest's instruction rendered whole (KNOWUSE1c, KNOWUSE1d), as the harness reads it: a repository's quest that
/// carries nothing, a quest an ask asked with everything the look and the close speak to, and a carry-on after the
/// person answered. A sentence added, moved or reworded shows here as the whole instruction's difference, byte for byte.
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
        _ => throw new ArgumentOutOfRangeException(nameof(which), which, null),
    };

    [Theory]
    [InlineData("bare")]
    [InlineData("full")]
    [InlineData("carry-on")]
    [InlineData("set-up")]
    public void The_quests_instruction_is_its_golden_text(string which)
    {
        Assert.Equal(File.ReadAllText(GoldenPath($"{which}.md")), TargetPrompt.Compose(Of(which)));
        // The text the account is kept beside is the same instruction, byte for byte (CONTEXT1).
        Assert.Equal(File.ReadAllText(GoldenPath($"{which}.md")), TargetPrompt.Composed(Of(which)).Text);
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
    public void The_account_beside_it_is_its_golden(string which)
    {
        var account = TargetPrompt.Composed(Of(which)).Account;

        Assert.Equal(File.ReadAllText(GoldenPath($"{which}.md")).Length, account.Chars);
        Assert.Equal(File.ReadAllText(GoldenPath($"{which}.account.json")), Written(account));
    }

    /// <summary>The account as the record's JSON spells it, indented and LF, so a golden reads the same on every machine.</summary>
    internal static string Written(InstructionAccount account) =>
        System.Text.Json.JsonSerializer.Serialize(
            account, new System.Text.Json.JsonSerializerOptions(SessionEvents.Json) { WriteIndented = true, NewLine = "\n" }) + "\n";
}
