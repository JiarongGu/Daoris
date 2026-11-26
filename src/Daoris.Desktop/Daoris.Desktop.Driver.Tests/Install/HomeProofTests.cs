using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// What a build says of a home it starts on (<see cref="HomeProof"/>): every agent account it lists, the sessions and runs it reads, the
/// workflows its runs bind, and the paths no entry names. It reads, and changes nothing.
/// </summary>
public sealed class HomeProofTests : IDisposable
{
    private readonly string _install = Path.Combine(TestHomes.Root, "daoris-proof-" + Guid.NewGuid().ToString("N")[..8]);

    public HomeProofTests()
    {
        Directory.CreateDirectory(Path.Combine(_install, "data"));
        File.WriteAllText(Path.Combine(_install, "INSTALLED.md"), "# Daoris — installed desktop\n");
    }

    private string Home => Path.Combine(_install, "data");

    public void Dispose()
    {
        if (Directory.Exists(_install)) Directory.Delete(_install, recursive: true);
    }

    private void Put(string relative, string text = "")
    {
        var path = Path.Combine(_install, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }

    [Fact]
    public void A_home_holding_nothing_amiss_reads_clean_and_lists_its_accounts()
    {
        Directory.CreateDirectory(Path.Combine(_install, "profiles", "agents", "claude-code", "acct-a"));
        Directory.CreateDirectory(Path.Combine(_install, "profiles", "agents", "claude-code", "acct-b"));
        Put("data/sessions/s-1.events.jsonl", "");

        var report = HomeProof.Read(Home);

        Assert.Empty(report.Problems);
        Assert.Equal(["acct-a", "acct-b"], report.Accounts["claude-code"]);
        Assert.Equal(1, report.Sessions);
    }

    [Fact]
    public void An_account_in_a_place_no_entry_names_is_a_stray_and_is_not_listed()
    {
        // The place an install held three accounts at, after the table stopped naming it.
        Directory.CreateDirectory(Path.Combine(_install, "data", "harnesses", "claude-code", "old-acct"));
        Put("data/harnesses/claude-code/old-acct/state.json", "{}");

        var report = HomeProof.Read(Home);

        Assert.DoesNotContain("claude-code", report.Accounts.Keys);
        Assert.Contains(report.Problems, problem => problem.Contains("data/harnesses", StringComparison.Ordinal));
    }

    [Fact]
    public void A_run_naming_a_workflow_the_home_does_not_hold_is_a_problem_and_a_file_that_is_no_run_is_one()
    {
        Assert.True(new WorkflowRunFiles(Home).Bind(
            new WorkflowRunBinding("q1", "reports", "work", "ghost", WorkflowLevels.Repository, new DateTimeOffset(2026, 11, 7, 9, 0, 0, TimeSpan.Zero))));
        Put("data/workflows/runs/q2.json", "{ not json");

        var report = HomeProof.Read(Home);

        Assert.Equal(2, report.Runs);
        Assert.Contains(report.Problems, problem => problem.Contains("q1", StringComparison.Ordinal) && problem.Contains("ghost", StringComparison.Ordinal));
        Assert.Contains(report.Problems, problem => problem.Contains("q2", StringComparison.Ordinal));
    }

    [Fact]
    public void A_run_on_Current_and_a_run_the_bind_held_are_records_the_product_writes_and_no_problem()
    {
        var at = new DateTimeOffset(2026, 11, 7, 9, 0, 0, TimeSpan.Zero);
        var runs = new WorkflowRunFiles(Home);
        Assert.True(runs.Bind(new WorkflowRunBinding("q1", "reports", "work", WorkflowSelection.Current, WorkflowLevels.Repository, at)));
        Assert.True(runs.Bind(new WorkflowRunBinding("q2", "reports", "work", "ghost", WorkflowLevels.Repository, at).Held(BindingProblem.ForNotSaved("ghost"))));

        var report = HomeProof.Read(Home);

        Assert.Equal(2, report.Runs);
        Assert.Empty(report.Problems);
    }

    [Fact]
    public void A_line_that_is_not_the_report_is_not_read_as_one()
    {
        Assert.Null(HomeProof.Parse("{}"));
        Assert.Null(HomeProof.Parse("{\"sessions\":1,\"runs\":2}"));
        Assert.Null(HomeProof.Parse("{\"report\":1}\n{\"error\":\"x\"}"));
    }

    [Fact]
    public void A_workflow_file_that_cannot_be_read_is_a_problem()
    {
        Put("data/workflows/broken.json", "{ not json");

        Assert.Contains(HomeProof.Read(Home).Problems, problem => problem.Contains("broken", StringComparison.Ordinal));
    }

    [Fact]
    public void The_report_round_trips_through_the_words_a_verb_prints()
    {
        Directory.CreateDirectory(Path.Combine(_install, "profiles", "agents", "claude-code", "acct-a"));
        Put("data/workflows/broken.json", "{ not json");
        var report = HomeProof.Read(Home);

        var back = HomeProof.Parse(HomeProof.Say(report));

        Assert.NotNull(back);
        Assert.Equal(report.Accounts["claude-code"], back!.Accounts["claude-code"]);
        Assert.Equal(report.Problems, back.Problems);
        Assert.Null(HomeProof.Parse("usage: daoris home ..."));
    }

    [Fact]
    public void Reading_changes_nothing_in_the_home()
    {
        Directory.CreateDirectory(Path.Combine(_install, "profiles", "agents", "claude-code", "acct-a"));
        Put("data/sessions/s-1.events.jsonl", "");
        Put("data/stray.bin", "x");
        var before = Snapshot();

        HomeProof.Read(Home);

        Assert.Equal(before, Snapshot());
    }

    private List<string> Snapshot() =>
        [.. Directory.EnumerateFileSystemEntries(_install, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(_install, path) + (File.Exists(path) ? ":" + new FileInfo(path).Length : "/"))
            .Order(StringComparer.Ordinal)];
}
