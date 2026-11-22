using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The run view's read of a second opinion's gate (<see cref="WorkflowRunRecords.OpinionAsync"/>) over real files: a tree with no git, a
/// gate file that does not parse and a world that fails in a way nobody foresaw each leave the view whole, the gate read as unread and
/// said; only a cancel ends it; and an opinion read at the steps occasion is read with it. The gate's commit is asked of a real git over
/// real folders, so this is a real-process suite.
/// </summary>
[Trait(Category.Name, Category.Process)]
public sealed class WorkflowRunRecordsOpinionTests : IDisposable
{
    private const string Chat = "chat-f1a2b3c4";

    private readonly string _home = Path.Combine(TestHomes.Root, "daoris-run-opinion-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly string _tree;

    public WorkflowRunRecordsOpinionTests()
    {
        _tree = Path.Combine(_home, "tree");
        Directory.CreateDirectory(_tree);
    }

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private static readonly ReviewGateState NoReview = new(ReviewStates.None, new ReviewDecision(ReviewLevels.Nothing, null) { Repository = "engine" });

    private WorkflowProcess Process(string run, string rule = """{"opinions":{"engine":{"reviewers":["codex-acp"]}}}""") =>
        WorkflowProcesses.Bound(_home, DriverConfig.Parse(rule), "engine", "aurora", run, null);

    private RunOpinionRead Read(string? quest, WorkflowProcess process, IOpinionWorld? world = null) =>
        new(_tree, "engine", "aurora", quest, "s1", world ?? new World(), NoReview, process) { Run = quest is null ? Chat : null };

    /// <summary>🔴 A tree with no git, for a quest's run and a chat run alike, reads as a gate unread and said: the run view goes on, never fails.</summary>
    [Theory]
    [InlineData("q1")]
    [InlineData(null)]
    public async Task A_tree_with_no_git_reads_as_a_gate_unread_never_a_failure(string? quest)
    {
        // The scratch lies inside this repository, whose git would answer for it: a pointer to a git folder that is not there stops it.
        File.WriteAllText(Path.Combine(_tree, ".git"), "gitdir: ../no-such-git-folder");

        var gate = await new WorkflowRunRecords(_home).OpinionAsync(Read(quest, Process(quest ?? Chat)), CancellationToken.None);

        Assert.NotNull(gate);
        Assert.Equal(OpinionGateStates.Unread, gate.State);
        Assert.False(string.IsNullOrWhiteSpace(gate.Problem));
    }

    /// <summary>🔴 A folder with no git of its own inside a repository, which git would answer for with the repository's commit, reads as a gate
    /// unread saying so, for a quest's run and a chat run alike.</summary>
    [Theory]
    [InlineData("q1")]
    [InlineData(null)]
    public async Task A_folder_inside_a_repository_with_no_git_of_its_own_reads_as_a_gate_unread(string? quest)
    {
        var gate = await new WorkflowRunRecords(_home).OpinionAsync(Read(quest, Process(quest ?? Chat)), CancellationToken.None);

        Assert.Equal(OpinionGateStates.Unread, gate?.State);
        Assert.Contains("no git of its own", gate?.Problem);
    }

    /// <summary>🔴 A read the gate's own reader already answered as unread (the service did not answer) keeps its reason, never the missing commit's.</summary>
    [Fact]
    public async Task A_gate_already_unread_keeps_its_own_reason()
    {
        var read = Read("q1", Process("q1"), new World { Fails = new HttpRequestException("service unavailable") });

        var gate = await new WorkflowRunRecords(_home).OpinionAsync(read, CancellationToken.None);

        Assert.Equal(OpinionGateStates.Unread, gate?.State);
        Assert.Equal("service unavailable", gate?.Problem);
    }

    /// <summary>A gate file that does not parse leaves the view whole, for the quest's chain and the chat run: the file reads as nothing kept.</summary>
    [Theory]
    [InlineData("q1")]
    [InlineData(null)]
    public async Task A_gate_file_that_does_not_parse_is_no_failure_of_the_view(string? quest)
    {
        var gates = Path.Combine(_home, OpinionGateReading.GateRecord);
        Directory.CreateDirectory(gates);
        foreach (var file in new[] { quest is null ? Chat : "q1", "q1" }) File.WriteAllText(Path.Combine(gates, file + ".json"), "{ not json");

        var gate = await new WorkflowRunRecords(_home).OpinionAsync(Read(quest, Process(quest ?? Chat)), CancellationToken.None);

        Assert.NotNull(gate);
    }

    /// <summary>🔴 Any failure the read did not foresee (a process that would not start, a bad argument) is a gate unread and said, never a
    /// failed view; a cancel is the caller's own and goes on as one.</summary>
    [Fact]
    public async Task Any_failure_but_a_cancel_is_a_gate_unread_and_a_cancel_goes_on()
    {
        var read = Read("q1", Process("q1"), new World { Fails = new System.ComponentModel.Win32Exception("git is not installed") });

        var gate = await new WorkflowRunRecords(_home).OpinionAsync(read, CancellationToken.None);

        Assert.Equal(OpinionGateStates.Unread, gate?.State);
        Assert.Contains("git is not installed", gate?.Problem);

        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => new WorkflowRunRecords(_home).OpinionAsync(Read("q1", Process("q1"), new World { Fails = new OperationCanceledException(cancelled.Token) }), cancelled.Token));
    }

    /// <summary>🔴 A tree that is gone says nothing: the step stays as its record holds it.</summary>
    [Fact]
    public async Task A_tree_that_is_gone_reads_as_no_gate()
    {
        var read = Read("q1", Process("q1")) with { Tree = Path.Combine(_home, "gone") };

        Assert.Null(await new WorkflowRunRecords(_home).OpinionAsync(read, CancellationToken.None));
    }

    /// <summary>🔴 An opinion a rule reads before each next step (not before landing) is read with its own occasion, so the run's view gets a
    /// gate for it rather than the landing's <i>none</i>.</summary>
    [Fact]
    public async Task A_steps_occasion_opinion_is_read_with_its_own_occasion()
    {
        var rule = """{"opinions":{"engine":{"reviewers":["codex-acp"],"on":["steps"]}}}""";
        Directory.CreateDirectory(Path.Combine(_tree, ".git"));

        var gate = await new WorkflowRunRecords(_home).OpinionAsync(Read("q1", Process("q1", rule)), CancellationToken.None);

        Assert.Equal(OpinionRules.Steps, WorkflowRunRecords.OccasionOf(Process("q1", rule)));
        Assert.Equal(OpinionRules.Landing, WorkflowRunRecords.OccasionOf(Process("q1")));
        Assert.NotNull(gate);
        Assert.NotEqual(OpinionGateStates.None, gate.State);
    }

    private sealed class World : IOpinionWorld
    {
        public Exception? Fails { get; init; }

        public Task<IReadOnlyList<QuestView>> QuestsAsync(CancellationToken ct) =>
            Fails is { } fails ? Task.FromException<IReadOnlyList<QuestView>>(fails)
                : Task.FromResult<IReadOnlyList<QuestView>>([new QuestView("q1", "ask:a1", "engine", "The work", "Body.", "Taken")]);

        public Task<AskView?> AskAsync(string id, CancellationToken ct) => Task.FromResult<AskView?>(null);

        public Task<OpinionView?> OpinionAsync(string id, CancellationToken ct) => Task.FromResult<OpinionView?>(null);
    }
}
