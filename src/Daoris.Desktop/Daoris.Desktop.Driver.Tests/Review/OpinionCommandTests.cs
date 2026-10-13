using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// XAGENT1f (D155 point 10; the second-agent design §8.5, §9, D50): the terminal's doors to the second opinion's gate,
/// <c>daoris-driver opinion ask|show|stop|anyway|myself</c>, read word for word; their usage, and what <c>trees land --plan</c>
/// says of the whole gate, held to golden text byte for byte; and what <c>opinion show</c> says of a finding and its answer.
/// </summary>
/// <remarks>
/// The golden files under <c>golden/opinion-command/</c> and <c>golden/trees-land-plan/</c> are what a person reads at a terminal. A
/// change to them changes the file in the same commit, so review reads the words as the person will. No process runs here.
/// </remarks>
public sealed class OpinionCommandTests
{
    private const string Read = "1111111111111111111111111111111111111111";
    private const string Later = "3333333333333333333333333333333333333333";

    private static string RepositoryRoot()
    {
        var at = new DirectoryInfo(AppContext.BaseDirectory);
        while (at is not null && !File.Exists(Path.Combine(at.FullName, "daoris.json"))) at = at.Parent;
        return at?.FullName ?? throw new InvalidOperationException("no workspace root above the test binary");
    }

    internal static string GoldenPath(string folder, string file) =>
        Path.Combine(RepositoryRoot(), "src", "Daoris.Desktop", "Daoris.Desktop.Driver.Tests", "golden", folder, file);

    // ——— the words read

    public static TheoryData<string, string?> Lines() => new()
    {
        { "ask s1", "ask s1 reviewer= same=False failure=False words=" },
        { "ask #s1 --reviewer dsh please look at the parser", "ask s1 reviewer=dsh same=False failure=False words=please look at the parser" },
        { "ask s1 --same-agent", "ask s1 reviewer= same=True failure=False words=" },
        { "ask s1 --failure stuck on the build", "ask s1 reviewer= same=False failure=True words=stuck on the build" },
        { "show o1", "show o1 reviewer= same=False failure=False words=" },
        { "stop o1", "stop o1 reviewer= same=False failure=False words=" },
        { "anyway s1 a comment only", "anyway s1 reviewer= same=False failure=False words=a comment only" },
        { "myself s1", "myself s1 reviewer= same=False failure=False words=" },
        { "ask", null },
        { "ask --reviewer dsh", null },
        { "ask s1 --reviewer", null },
        { "ask s1 --reviewer dsh --same-agent", null },
        { "ask s1 --verify", null },
        { "show o1 extra", null },
        { "stop", null },
        { "frobnicate s1", null },
    };

    [Theory]
    [MemberData(nameof(Lines))]
    public void Each_line_is_read_as_its_verb_its_id_and_what_goes_with_it(string line, string? read)
    {
        var ask = OpinionCommand.Read(line.Split(' '), out var problem);

        if (read is null)
        {
            Assert.Null(ask);
            Assert.False(string.IsNullOrWhiteSpace(problem));
            return;
        }

        Assert.Null(problem);
        Assert.Equal(read, $"{ask!.Verb} {ask.Id} reviewer={ask.Reviewer} same={ask.SameAgent} failure={ask.Failure} words={ask.Words}");
    }

    // ——— the usage

    [Fact]
    public void The_verbs_usage_is_its_golden_text_and_the_hosts_usage_names_each_verb()
    {
        Assert.Equal(File.ReadAllText(GoldenPath("opinion-command", "usage.txt")).ReplaceLineEndings("\n").TrimEnd('\n'), OpinionCommand.Usage);

        var usage = DriverCommand.Usage.ReplaceLineEndings("\n");
        Assert.Contains("\n  opinion ask <session> [--reviewer <adapter>] [--same-agent] [\"…\"]  ·  opinion show <session|opinion>\n", usage);
        Assert.Contains("\n  opinion stop <opinion>  ·  opinion anyway <session> [\"…\"]  ·  opinion myself <session> [\"…\"]\n", usage);

        var program = File.ReadAllText(Path.Combine(RepositoryRoot(), "src", "Daoris.Desktop", "Daoris.Desktop.Driver.Host", "Program.cs"));
        Assert.Contains("if (args is [\"opinion\", .. var opinionArgs])", program);
        var console = File.ReadAllText(Path.Combine(RepositoryRoot(), "src", "Daoris.Desktop", "Daoris.Desktop.Driver.Host", "OpinionConsole.cs"));
        Assert.Contains("OpinionCommand.Read(args, out var problem)", console);
        Assert.Contains("Console.Error.WriteLine(OpinionCommand.Usage);", console);
        Assert.Contains("OpinionCommand.RunAsync(", console);
    }

    // ——— what `trees land --plan` says of the gate

    private static OpinionView Opinion(string state, string tip = Read, params string[] weights) =>
        new("o1", "landing", "first", "s1", "r-o1", "web-app", "0000000000000000000000000000000000000000", tip, "codex-acp", ReviewerLabels.AnotherMaker, state)
        {
            Product = "Codex", Maker = "OpenAI", Minutes = 20,
            Findings = state == "given" ? [.. weights.Select((weight, at) => new OpinionFindingView(at + 1, weight, "src/report.ts:12", "The window's end is exclusive", "a day is lost", "", "sure"))] : null,
            Read = "src/report.ts and its tests",
        };

    private static ResolvedOpinion Rule(bool required) => new(new OpinionRule(["codex-acp"], [OpinionRules.Landing], required), OpinionSource.Repository);

    private static readonly ReviewGateState NoReview = new(ReviewStates.None, new ReviewDecision(ReviewLevels.Nothing, null) { Repository = "web-app" });

    private static readonly ReviewGateState NotShown = new(ReviewStates.NotShown, new ReviewDecision(ReviewLevels.Repository, "local") { Repository = "web-app", Work = "q1" });

    /// <summary>The gates the plan's golden texts are of, each as the doors read it.</summary>
    internal static LandingGate Plan(string which)
    {
        var state = new OpinionGateState(OpinionGateStates.None, "web-app") { Session = "s1", Rule = Rule(required: true), Tip = Read };
        return which switch
        {
            "reading" => new(state with { State = OpinionGateStates.Reading, Opinion = Opinion("reading") }, NoReview),
            "commits-since" => new(state with { State = OpinionGateStates.CommitsSince, Opinion = Opinion("given"), Since = 2, Tip = Later, Disputes = new([], []) }, NoReview),
            "unavailable" => new(state with { State = OpinionGateStates.Unavailable, Code = ReviewerUnavailable.NoReviewer }, NoReview),
            "settled-then-look" => new(state with { State = OpinionGateStates.Settled, Opinion = Opinion("given") }, NotShown),
            _ => new(new OpinionGateState(OpinionGateStates.None, "web-app"), NoReview),
        };
    }

    [Theory]
    [InlineData("reading")]
    [InlineData("commits-since")]
    [InlineData("unavailable")]
    [InlineData("settled-then-look")]
    [InlineData("nothing")]
    public void What_trees_land_plan_says_of_the_gate_is_its_golden_text(string which)
    {
        Assert.Equal(
            File.ReadAllText(GoldenPath("trees-land-plan", $"{which}.txt")).ReplaceLineEndings("\n"),
            string.Concat(LandingGateWords.Plan(Plan(which)).Select(line => line + "\n")));
    }

    // ——— `opinion show`

    [Fact]
    public void Show_says_the_gate_then_each_finding_as_a_claim_with_its_answer_beside_it()
    {
        var opinion = Opinion("given", Read, OpinionViews.Must, "note") with
        {
            Answers = [new OpinionAnswerView(1, OpinionViews.Rejected) { Evidence = "the twin test holds the bound" }],
        };
        var gate = new OpinionGateState(OpinionGateStates.Disputed, "web-app")
        {
            Session = "s1", Rule = Rule(required: false), Tip = Read, Opinion = opinion, Disputes = new([1], []), Since = 0,
            Answers = new OpinionReading("s1", Read, DateTimeOffset.UnixEpoch,
            [
                new OpinionAnswerRead(1, OpinionViews.Must, OpinionViews.Rejected) { Said = OpinionViews.Rejected },
                new OpinionAnswerRead(2, "note", OpinionViews.Unresolved) { Why = OpinionAnswerWhy.NotAnswered },
            ]),
        };

        var lines = OpinionCommand.Show(gate, opinion);

        Assert.Equal($"opinion: {gate.Says}", lines[0]);
        Assert.Equal("  second opinion `o1` (first), by Codex (OpenAI), another maker's agent, on `web-app` at `11111111`: given.", lines[1]);
        Assert.Equal("  it read: src/report.ts and its tests", lines[2]);
        Assert.Equal("  1. must, sure, at src/report.ts:12: The window's end is exclusive", lines[3]);
        Assert.Equal("     if so: a day is lost", lines[4]);
        Assert.Equal("     answered: rejected: the twin test holds the bound", lines[5]);
        Assert.Equal("     answered: not answered", lines[^1]);
    }
}
