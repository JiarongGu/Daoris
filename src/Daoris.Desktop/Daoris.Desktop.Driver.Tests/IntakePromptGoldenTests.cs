using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The intake's instruction rendered whole (DRIFT1c), as the harness reads it: an ask carrying everything
/// the instruction speaks to, and an ask that is its sentence alone. A sentence added, moved or reworded
/// shows here as the whole instruction's difference, byte for byte.
/// </summary>
/// <remarks>
/// The golden files under <c>golden/intake-prompt/</c> are the instruction an intake is handed. A change to
/// what it says changes its golden file in the same commit, so review reads the instruction as the intake
/// will; the tests beside the golden one, and <see cref="IntakeTests"/>, say why each sentence is there.
/// This class runs no process, so it is in the fast half (MOD8), where <see cref="IntakeTests"/> is not.
/// </remarks>
public sealed class IntakePromptGoldenTests
{
    private const string Sentence = "complete the ticket I logged, and this will need the v3 bridge";

    private static readonly DateTimeOffset Asked = DateTimeOffset.Parse("2026-10-01T02:26:00Z");

    /// <summary>Every part speaking: links, a file here and one not, the declarations' proposal, and words since the ask.</summary>
    internal static readonly AskView Full = new("a1b2c3", "work", Sentence, "Proposed", "declarations")
    {
        Links = ["https://tickets.example/T-9"],
        Attachments =
        [
            new QuestFileView("shot.png", "abc", 12, "/data/asks/a1b2c3/shot.png"),
            new QuestFileView("notes.txt", "def", 34, null),
        ],
        Proposed = ["reports", "checker"],
        Words =
        [
            new AskWordView(AskWordView.Asked, Sentence, Asked),
            new AskWordView(AskWordView.Added, "the bridge means the common-report", Asked.AddMinutes(2), "s1"),
        ],
    };

    /// <summary>The sentence alone, its words read and nothing said since.</summary>
    internal static readonly AskView Bare = new("d4e5f6", "default", "cap the frame's work", "Proposed", "declarations")
    {
        Words = [new AskWordView(AskWordView.Asked, "cap the frame's work", Asked)],
    };

    private static string RepositoryRoot()
    {
        var at = new DirectoryInfo(AppContext.BaseDirectory);
        while (at is not null && !File.Exists(Path.Combine(at.FullName, "daoris.json"))) at = at.Parent;
        return at?.FullName ?? throw new InvalidOperationException("no workspace root above the test binary");
    }

    internal static string GoldenPath(string file) =>
        Path.Combine(RepositoryRoot(), "src", "Daoris.Desktop", "Daoris.Desktop.Driver.Tests", "golden", "intake-prompt", file);

    [Theory]
    [InlineData("full")]
    [InlineData("bare")]
    public void The_intakes_instruction_is_its_golden_text(string ask)
    {
        Assert.Equal(File.ReadAllText(GoldenPath($"{ask}.md")), IntakePrompt.Compose(ask == "full" ? Full : Bare));
    }

    /// <summary>
    /// DRIFT1c (D133 §3): the intake names what the person requires in their own words, with the check that
    /// proves it, and keeps its reading for the body. The drift's intake read the right document and wrote
    /// a true sentence that kept the word "bridge" and lost what it meant where the work lived, so the
    /// instruction says a word means what it means there, and the service refuses a quote not theirs.
    /// </summary>
    [Fact]
    public void The_intake_names_requirements_in_the_persons_own_words_with_their_checks()
    {
        var prompt = IntakePrompt.Compose(Full);

        Assert.Contains("`requirements`", prompt);
        Assert.Contains("their own words, copied\nexactly", prompt);
        Assert.Contains("the check that proves the work", prompt);
        Assert.Contains("means what it means where the work lives", prompt);
        Assert.Contains("belongs in the body, never in a requirement", prompt);
        Assert.Contains("A `then` step carries the quest's requirements", prompt);
    }

    /// <summary>
    /// EVID1b (D144 §2): the intake names a requirement's evidence, a path, only where the work plainly leaves a file the person
    /// can name, never one it would guess at; and no gate, which is refused until the landing queue reads gates (EVID1d). Said
    /// after the requirements it belongs to, and before what an unsettled ask does.
    /// </summary>
    [Fact]
    public void The_intake_names_evidence_only_where_the_work_plainly_leaves_a_file_the_person_can_name()
    {
        var prompt = IntakePrompt.Compose(Bare).ReplaceLineEndings(" ").Replace("\n", " ", StringComparison.Ordinal);
        var flat = string.Join(' ', prompt.Split(' ', StringSplitOptions.RemoveEmptyEntries));

        Assert.Contains("plainly turns on a file or folder the work leaves, one the person can name, name it as that requirement's `evidence`", flat);
        Assert.Contains("`{ \"path\": \"docs/report.md\" }`", flat);
        Assert.Contains("Daoris reads each in the work's last commit when its session ends", flat);
        Assert.Contains("Name no path you would be guessing at, and no `gate`: a gate is not taken yet.", flat);
        var requirements = flat.IndexOf("A `then` step carries the quest's requirements", StringComparison.Ordinal);
        var evidence = flat.IndexOf("`evidence`", StringComparison.Ordinal);
        var unsettled = flat.IndexOf("When they do not settle it", StringComparison.Ordinal);
        Assert.True(requirements < evidence && evidence < unsettled, flat);
    }
}
