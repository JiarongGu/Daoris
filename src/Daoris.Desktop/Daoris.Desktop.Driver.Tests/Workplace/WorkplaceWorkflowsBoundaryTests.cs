using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FOUNDATION1 mod-x, the Workplace-names-Workflows cut: the behaviour the cut keeps, written against the surface that exists on both
/// sides of it (a run's id and files, the chat run's key and its three files in a session's inventory, the landed declaration's name, and
/// the landing gate of a tree under the rules live, a bound named workflow, a binding that does not read and a chat run), so the same
/// cases pass on the base and after it. The cases that reach a moved class through a new seam are <see cref="WorkplaceWorkflowsPortsTests"/>'.
/// </summary>
public sealed class WorkplaceWorkflowsBoundaryTests : IDisposable
{
    private static readonly DateTimeOffset At = new(2026, 10, 28, 9, 0, 0, TimeSpan.Zero);
    private const string Steps = """[{"id":"work","kind":"work"},{"id":"land","kind":"landing","form":"branch","accept":"automatic"}]""";
    private readonly string _home = Path.Combine(GitSeed.RepoRoot(), "_fixtures", "workplace-workflows", Guid.NewGuid().ToString("N")[..8]);
    private string Tree => DaorisHome.Entry(_home, HomeEntry.Trees, "default", "engine", "s1");

    public WorkplaceWorkflowsBoundaryTests() => Directory.CreateDirectory(_home);

    public void Dispose() => GitSeed.Remove(_home);

    private DriverConfig Config() => DriverConfig.Load(DaorisHome.Entry(_home, HomeEntry.DriverConfig));

    /// <summary>`docs-to-pr` saved with one version and chosen as `engine`'s default.</summary>
    private void Choose()
    {
        WorkflowStore.Add(_home, new WorkflowVersionAdded(
            "docs-to-pr", "Documentation to a pull request", "terminal", "2026-10-09T09:00:00Z", JsonNode.Parse(Steps)), kept: []);
        WorkflowSelection.Apply(Config(), new WorkflowUse("docs-to-pr", "engine", null, null)).Save(DaorisHome.Entry(_home, HomeEntry.DriverConfig));
    }

    private void Bind(string run) =>
        Assert.True(WorkflowRunBindings.Bind(_home, WorkflowRunBindings.Plan(Config(), _home, run, "engine", "aurora", null, null, [], At)));

    private sealed class World(params QuestView[] quests) : IOpinionWorld
    {
        public Task<IReadOnlyList<QuestView>> QuestsAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<QuestView>>(quests);

        public Task<AskView?> AskAsync(string id, CancellationToken ct) => Task.FromResult<AskView?>(null);

        public Task<OpinionView?> OpinionAsync(string id, CancellationToken ct) => Task.FromResult<OpinionView?>(null);
    }

    private static World Quest() => new(new QuestView("q1", "game", "engine", "Fix the gap", "Fix it.", "Done"));

    // ——— A run's files

    [Fact]
    public void A_run_is_a_plain_id_and_its_binding_lies_in_the_runs_folder_of_the_workflows()
    {
        var runs = Path.Combine(DaorisHome.Entry(_home, HomeEntry.Workflows), "runs");
        Assert.Equal(runs, WorkflowRunBindings.FolderOf(_home));
        Assert.Equal(Path.Combine(runs, "q1.json"), WorkflowRunBindings.PathOf(_home, "q1"));
        Assert.Equal("runs", WorkflowRunBindings.Folder);

        Assert.True(WorkflowRunBindings.IsRunId("0a1b2c3d4e5f"));
        Assert.True(WorkflowRunBindings.IsRunId("chat-s_1"));
        Assert.True(WorkflowRunBindings.IsRunId(new string('a', 64)));
        Assert.False(WorkflowRunBindings.IsRunId(new string('a', 65)));
        Assert.False(WorkflowRunBindings.IsRunId(null));
        Assert.False(WorkflowRunBindings.IsRunId(""));
        Assert.False(WorkflowRunBindings.IsRunId("a/b"));
        Assert.False(WorkflowRunBindings.IsRunId(".."));
        Assert.Equal("`a/b` is not a run's id.", Assert.Throws<DriverException>(() => WorkflowRunBindings.PathOf(_home, "a/b")).Message);

        Assert.False(WorkflowRunBindings.Bound(_home, "q1"));
        Directory.CreateDirectory(runs);
        File.WriteAllText(WorkflowRunBindings.PathOf(_home, "q1"), "{}");
        Assert.True(WorkflowRunBindings.Bound(_home, "q1"));
        Assert.False(WorkflowRunBindings.Bound(_home, "a/b"));
    }

    [Fact]
    public void A_chat_run_is_its_prefix_and_its_first_record_and_keeps_its_three_files_apart()
    {
        Assert.Equal("chat-", ChatRuns.Prefix);
        Assert.True(ChatRuns.IsChatRun("chat-s1"));
        Assert.False(ChatRuns.IsChatRun("chat-"));
        Assert.False(ChatRuns.IsChatRun("q1"));
        Assert.False(ChatRuns.IsChatRun(null));
        Assert.Equal("s1", ChatRuns.RecordOf("chat-s1"));
        Assert.Null(ChatRuns.RecordOf("q1"));

        Assert.Equal(DaorisHome.Entry(_home, HomeEntry.OpinionGates), ChatRuns.GatesFolder(_home));
        Assert.Equal(Path.Combine(DaorisHome.Entry(_home, HomeEntry.Workflows), "looks"), ChatRuns.LooksFolder(_home));
        Assert.Equal(
            [WorkflowRunBindings.PathOf(_home, "chat-s1"), Path.Combine(ChatRuns.GatesFolder(_home), "chat-s1.json"), Path.Combine(ChatRuns.LooksFolder(_home), "chat-s1.json")],
            ChatRuns.Files(_home, "chat-s1"));
        Assert.Empty(ChatRuns.Files(_home, "q1"));
        Assert.Empty(ChatRuns.Files(_home, "chat-a/b"));
    }

    [Fact]
    public void A_sessions_inventory_counts_and_removes_its_chat_runs_three_files_by_the_name_run()
    {
        foreach (var path in ChatRuns.Files(_home, "chat-s1"))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, "{}{}");
        }

        // A quest's run and another chat's stay where they are.
        Directory.CreateDirectory(WorkflowRunBindings.FolderOf(_home));
        File.WriteAllText(WorkflowRunBindings.PathOf(_home, "q1"), "{}");
        File.WriteAllText(WorkflowRunBindings.PathOf(_home, "chat-s2"), "{}");

        var files = new SessionHomeFiles(_home);
        Assert.Equal(12L, files.Size("s1").Total);
        Assert.Equal(2L, files.Size("s2").Total);

        var removed = files.Remove(["s1"], null);

        Assert.Contains("run", removed.Went["s1"]);
        Assert.Equal(12L, removed.Bytes["s1"].Total);
        Assert.Empty(removed.Failed);
        Assert.All(ChatRuns.Files(_home, "chat-s1"), path => Assert.False(File.Exists(path), path));
        Assert.True(File.Exists(WorkflowRunBindings.PathOf(_home, "q1")));
        Assert.True(File.Exists(WorkflowRunBindings.PathOf(_home, "chat-s2")));
    }

    // ——— The landed declaration

    [Fact]
    public async Task The_landed_declaration_is_one_name_at_the_top_and_a_blob_is_read_only_as_git_writes_one()
    {
        Assert.Equal("daoris.gates.json", LandedGates.File);
        Assert.Equal("generated", GeneratedWriters.Key);

        // Neither reaches git: none is no blob, and a name that is not a whole blob id is not one.
        Assert.Null(await LandedGates.TextAsync(_home, WorkflowSelection.NoGates));
        Assert.Null(await LandedGates.TextAsync(_home, "not-a-blob"));
        Assert.Null(await LandedGates.TextAsync(_home, new string('A', 40)));
    }

    // ——— The landing gate

    [Fact]
    public async Task The_gate_of_work_with_no_bound_run_reads_the_rules_live_and_lets_the_workflow_part_go()
    {
        Directory.CreateDirectory(Tree);

        var gate = await new SessionTrees(_home).GateAsync(Tree, "q1", Quest(), "s1");

        Assert.Equal(WorkflowGateStates.None, gate.Workflow!.State);
        Assert.True(gate.Workflow.LetsGo);
        Assert.Equal("s1", gate.Workflow.Session);
        Assert.False(gate.Process!.Named);
        Assert.Equal("Current", gate.Process.Name);
        Assert.Null(gate.Graph);
    }

    [Fact]
    public async Task A_bound_named_workflow_governs_the_gate_of_its_quest_and_names_the_version_that_decided()
    {
        Directory.CreateDirectory(Tree);
        Choose();
        Bind("q1");

        var trees = new SessionTrees(_home);
        var gate = await trees.GateAsync(Tree, "q1", Quest(), "s1");

        Assert.Equal(WorkflowGateStates.Follows, gate.Workflow!.State);
        Assert.True(gate.Process!.Named);
        Assert.Equal("`docs-to-pr` v1", gate.Process.Name);
        Assert.Equal("q1", gate.Process.Binding!.Run);
        Assert.Equal(new LandingWorkflow("docs-to-pr", 1, WorkflowLevels.Repository), gate.Process.Record);
        Assert.Equal(LandingForm.Branch, gate.Process.Landing.Rule.Form);

        // The run's own part alone, judged for the process the door read: the same state, the session said.
        var part = await trees.WorkflowAsync(Tree, gate.Process, "s1");
        Assert.Equal((gate.Workflow.State, gate.Workflow.LetsGo, "s1"), (part.State, part.LetsGo, part.Session));

        // A process handed in is the one every part reads: the quest's chain is not read again.
        var handed = await trees.GateAsync(Tree, "q9", new World(), "s1", process: gate.Process);
        Assert.Equal(WorkflowGateStates.Follows, handed.Workflow!.State);
        Assert.Equal("`docs-to-pr` v1", handed.Process!.Name);
    }

    [Fact]
    public async Task A_binding_that_does_not_read_holds_the_gate_instead_of_reading_the_rules_live()
    {
        Directory.CreateDirectory(Tree);
        Choose();
        Bind("q1");
        File.WriteAllText(WorkflowRunBindings.PathOf(_home, "q1"), "not JSON");

        var gate = await new SessionTrees(_home).GateAsync(Tree, "q1", Quest(), "s1");

        Assert.Equal((WorkflowGateStates.Unread, false), (gate.Workflow!.State, gate.Workflow.LetsGo));
        Assert.Contains("binding could not be read", gate.Process!.Unread);
        Assert.Null(gate.Process.Binding);
    }

    [Fact]
    public async Task A_chat_runs_work_reads_its_bound_process_and_a_chat_run_not_bound_reads_the_rules_live()
    {
        Directory.CreateDirectory(Tree);
        Choose();
        var trees = new SessionTrees(_home);

        var unbound = await trees.GateAsync(Tree, null, new World(), "s1", run: "chat-s1");
        Assert.Equal((WorkflowGateStates.None, "Current"), (unbound.Workflow!.State, unbound.Process!.Name));

        var bound = ChatRuns.Bind(Config(), _home, "chat-s1", "engine", "aurora", [], At);
        Assert.True(bound.Wrote);
        var gate = await trees.GateAsync(Tree, null, new World(), "s1", run: "chat-s1");
        Assert.Equal((WorkflowGateStates.Follows, "`docs-to-pr` v1"), (gate.Workflow!.State, gate.Process!.Name));

        // A run that is not a chat's, with no quest, is read live whatever it names.
        var other = await trees.GateAsync(Tree, null, new World(), "s1", run: "q1");
        Assert.Equal(WorkflowGateStates.None, other.Workflow!.State);
    }

    [Fact]
    public void A_chat_run_binds_first_as_the_press_binds_it_and_is_read_back_where_it_was_bound()
    {
        Choose();

        var first = ChatRuns.BindFirst(Config(), _home, "chat-s1", Tree, [], At);
        Assert.Null(first.Failed);
        Assert.True(first.Wrote);
        Assert.Equal("docs-to-pr", first.Binding!.Workflow);
        Assert.True(WorkflowRunBindings.Bound(_home, "chat-s1"));

        var again = ChatRuns.BindFirst(Config(), _home, "chat-s1", Tree, [], At);
        Assert.False(again.Wrote);
        Assert.Equal(first.Binding, again.Binding);

        // The press's view of a record the service does not hold is its own id alone, live.
        var (line, live) = ChatRuns.PressOf("s9", "[]");
        Assert.Equal(["s9"], line);
        Assert.True(live);
    }

    // ——— What the workplace reads of a process

    [Fact]
    public void A_process_says_whether_a_named_workflow_governs_it_and_what_decided_how_it_lands()
    {
        Choose();
        var config = Config();
        var current = WorkflowProcess.Current(config, "engine", "aurora");
        Assert.False(current.Named);
        Assert.Equal("Current", current.Name);
        Assert.Null(current.Record);
        Assert.Null(current.Binding);
        Assert.Null(current.LandingCannot);

        Bind("q1");
        var named = WorkflowProcesses.Read(_home, config, "engine", "aurora", "q1");
        Assert.True(named.Named);
        Assert.Equal("`docs-to-pr` v1", named.Name);
        Assert.Equal("q1", named.Binding!.Run);
        Assert.Equal(new LandingWorkflow("docs-to-pr", 1, WorkflowLevels.Repository), named.Record);
        Assert.Null(named.Problem);
        Assert.Null(named.Unread);
        Assert.Equal("`engine`'s default", WorkflowRunBinding.Why(named.Binding));
        Assert.Equal($"It follows {named.Name}, and {named.LandingCannot} Nothing was landed.", WorkflowGate.LandingSays(named));
    }
}
