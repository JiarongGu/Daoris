using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FABRIC-PIPELINE1d (the pipeline design §4.3): <c>daoris-driver verify &lt;repository&gt; [--branch &lt;name&gt; | --commit &lt;sha&gt;]</c>, a person's
/// verification of a branch or a commit, binds a run of its own under the repository's landed route and its accepted gates, which the running
/// driver verifies; a repository whose route names no verification pipeline, or whose gates are not accepted, binds nothing.
/// </summary>
/// <remarks>Real git: the Process half's git classes.</remarks>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class VerifyCommandTests : IAsyncLifetime
{
    private const string Graph = """
        {"schemaVersion":2,"entries":["verify"],"end":"verify","nodes":[{"id":"verify","kind":"check","version":1,"reach":"performed"}],"wires":[]}
        """;

    private readonly string _folder = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "verify-command", Guid.NewGuid().ToString("N")[..8]);
    private string _first = "";
    private string _second = "";
    private string _digest = "";

    private string Home => Path.Combine(_folder, "home");

    private string Checkout => Path.Combine(_folder, "engine");

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(Home);
        Directory.CreateDirectory(Checkout);
        await GitFixture.GitAsync(Checkout, "init", "-q", "-b", "main");
        await GitFixture.GitAsync(Checkout, "config", "user.email", "test@example.com");
        await GitFixture.GitAsync(Checkout, "config", "user.name", "Test");
        await GitFixture.GitAsync(Checkout, "commit", "-q", "--allow-empty", "-m", "first");
        _first = (await GitFixture.GitAsync(Checkout, "rev-parse", "HEAD")).Trim();
        await GitFixture.GitAsync(Checkout, "checkout", "-q", "-b", "feature/x");
        await GitFixture.GitAsync(Checkout, "commit", "-q", "--allow-empty", "-m", "second");
        _second = (await GitFixture.GitAsync(Checkout, "rev-parse", "HEAD")).Trim();
        await GitFixture.GitAsync(Checkout, "checkout", "-q", "main");
        Directory.CreateDirectory(WorkflowStore.FolderOf(Home));
        File.WriteAllText(WorkflowStore.PathOf(Home, "verify"),
            $$"""{"id":"verify","name":"Verify","pipeline":"verification","versions":[{"version":1,"at":"2026-10-31T09:00:00Z","door":"terminal","schema":2,"graph":{{Graph}}}]}""");
        _digest = Assert.Single(new WorkflowFiles(Home).Load("verify")!.Read.Versions).Digest!;
    }

    public Task DisposeAsync()
    {
        GitSeed.Discard(_folder);
        return Task.CompletedTask;
    }

    private static WorkflowLandedSelected Route(string? gates = "1111111111111111111111111111111111111111") => new("verify", gates, WorkflowLevels.RepositoryLanded);

    private Task<int> BindAsync(VerifyAsk ask, WorkflowLandedSelected route, TextWriter said) =>
        VerifyCommand.BindAsync(Home, Checkout, ask, route, "default", said, CancellationToken.None);

    [Fact]
    public async Task A_commit_or_a_branch_is_bound_as_a_person_s_run_under_the_route_s_pipeline_and_accepted_gates()
    {
        var said = new SaidWriter();

        Assert.Equal(0, await BindAsync(new VerifyAsk("engine") { Commit = _first[..10] }, Route(), said));
        Assert.Equal(0, await BindAsync(new VerifyAsk("engine") { Branch = "feature/x" }, Route(), said));

        var runs = VerifyRuns.All(Home).OrderBy(run => run.At).ThenBy(run => run.Commit == _second).ToList();
        Assert.Equal(2, runs.Count);
        var commit = runs.Single(run => run.Commit == _first);
        var branch = runs.Single(run => run.Commit == _second);
        Assert.Equal((VerifyRuns.Person, "verify", 1, _digest, "1111111111111111111111111111111111111111", Checkout, (string?)null),
            (commit.Level, commit.Workflow, commit.Version, commit.Digest, commit.Accepted, commit.Root, commit.Branch));
        Assert.Equal("feature/x", branch.Branch);
        Assert.Contains($"verify: `engine` at {_first[..7]}: run {commit.Run}", said.Text);
    }

    [Fact]
    public async Task A_route_naming_no_verification_pipeline_or_no_accepted_gates_binds_nothing()
    {
        var said = new SaidWriter();

        Assert.Equal(1, await BindAsync(new VerifyAsk("engine"), new WorkflowLandedSelected(WorkflowSelection.Current, null, WorkflowLevels.Current), said));
        Assert.Equal(1, await BindAsync(new VerifyAsk("engine"), Route(gates: null), said));

        Assert.Empty(VerifyRuns.All(Home));
        Assert.Contains("names no verification pipeline", said.Text);
        Assert.Contains("Accept the gates", said.Text);
    }

    [Theory]
    [InlineData(new[] { "engine" }, true)]
    [InlineData(new[] { "engine", "--branch", "feature/x" }, true)]
    [InlineData(new[] { "engine", "--commit", "abc1234" }, true)]
    [InlineData(new[] { "engine", "--branch", "a", "--commit", "b" }, false)]
    [InlineData(new string[0], false)]
    public void Its_words_are_read_or_refused(string[] args, bool read)
    {
        Assert.Equal(read, VerifyCommand.Read(args, out var problem) is not null);
        Assert.Equal(read, problem is null);
    }
}
