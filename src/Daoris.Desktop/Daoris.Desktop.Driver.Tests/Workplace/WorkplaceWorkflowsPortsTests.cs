using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FOUNDATION1 mod-x, the Workplace-names-Workflows cut: the seams the cut adds. Workplace declares the run's files and ids, the process
/// facet it reads, the operations it asks of the workflows (the landed declaration, a quest's process, a chat run's bind and press) and the
/// gate's two; Workflows keeps and installs them. The cases that need no new seam are <see cref="WorkplaceWorkflowsBoundaryTests"/>'.
/// </summary>
[Collection("agent operation ports")]
public sealed class WorkplaceWorkflowsPortsTests : IDisposable
{
    private static readonly DateTimeOffset At = new(2026, 10, 28, 9, 0, 0, TimeSpan.Zero);
    private const string Steps = """[{"id":"work","kind":"work"},{"id":"land","kind":"landing","form":"branch","accept":"automatic"}]""";
    private readonly string _home = Path.Combine(GitSeed.RepoRoot(), "_fixtures", "workplace-workflows-ports", Guid.NewGuid().ToString("N")[..8]);
    private string Tree => DaorisHome.Entry(_home, HomeEntry.Trees, "default", "engine", "s1");

    public WorkplaceWorkflowsPortsTests() => Directory.CreateDirectory(_home);

    public void Dispose() => GitSeed.Remove(_home);

    private DriverConfig Config() => DriverConfig.Load(DaorisHome.Entry(_home, HomeEntry.DriverConfig));

    private void Choose()
    {
        WorkflowStore.Add(_home, new WorkflowVersionAdded(
            "docs-to-pr", "Documentation to a pull request", "terminal", "2026-10-09T09:00:00Z", JsonNode.Parse(Steps)), kept: []);
        WorkflowSelection.Apply(Config(), new WorkflowUse("docs-to-pr", "engine", null, null)).Save(DaorisHome.Entry(_home, HomeEntry.DriverConfig));
    }

    // ——— The lower run files

    [Fact]
    public void The_lower_run_files_are_the_values_the_workflows_alias()
    {
        Assert.Equal(("runs", "looks", "chat-"), (WorkplaceRunFiles.Runs, WorkplaceRunFiles.Looks, WorkplaceRunFiles.ChatPrefix));
        Assert.Equal(WorkplaceRunFiles.Runs, WorkflowRunBindings.Folder);
        Assert.Equal(WorkplaceRunFiles.ChatPrefix, ChatRuns.Prefix);
        Assert.Equal(WorkplaceRunFiles.RunsFolder(_home), WorkflowRunBindings.FolderOf(_home));
        Assert.Equal(WorkplaceRunFiles.PathOf(_home, "q1"), WorkflowRunBindings.PathOf(_home, "q1"));
        Assert.Equal(WorkplaceRunFiles.GatesFolder(_home), ChatRuns.GatesFolder(_home));
        Assert.Equal(WorkplaceRunFiles.LooksFolder(_home), ChatRuns.LooksFolder(_home));
        foreach (var run in new[] { "chat-s1", "chat-", "q1", "a/b", "", "chat-a/b" })
        {
            Assert.Equal(WorkplaceRunFiles.IsRunId(run), WorkflowRunBindings.IsRunId(run));
            Assert.Equal(WorkplaceRunFiles.IsChatRun(run), ChatRuns.IsChatRun(run));
            Assert.Equal(WorkplaceRunFiles.RecordOf(run), ChatRuns.RecordOf(run));
        }

        Assert.False(WorkplaceRunFiles.Bound(_home, "q1"));
        Directory.CreateDirectory(WorkplaceRunFiles.RunsFolder(_home));
        File.WriteAllText(WorkplaceRunFiles.PathOf(_home, "q1"), "{}");
        Assert.True(WorkplaceRunFiles.Bound(_home, "q1"));
        Assert.Equal(WorkplaceRunFiles.Bound(_home, "q1"), WorkflowRunBindings.Bound(_home, "q1"));
        Assert.Equal("`a/b` is not a run's id.", Assert.Throws<DriverException>(() => WorkplaceRunFiles.PathOf(_home, "a/b")).Message);
        Assert.Equal("daoris.gates.json", GeneratedWriters.DeclarationFile);
        Assert.Equal(GeneratedWriters.DeclarationFile, LandedGates.File);
    }

    // ——— The process facet

    [Fact]
    public void A_workflow_process_answers_the_facet_the_workplace_reads()
    {
        Choose();
        WorkflowRunBindings.Bind(_home, WorkflowRunBindings.Plan(Config(), _home, "q1", "engine", "aurora", null, null, [], At));
        IWorkplaceProcess current = WorkflowProcess.Current(Config(), "engine", "aurora");
        var process = WorkflowProcesses.Read(_home, Config(), "engine", "aurora", "q1");
        IWorkplaceProcess named = process;

        Assert.Equal((false, "Current", null, null), (current.Named, current.Name, current.BoundRun, current.Record));
        Assert.Equal("since nothing names a workflow", current.Decided);
        Assert.Equal((true, "`docs-to-pr` v1", "q1"), (named.Named, named.Name, named.BoundRun));
        Assert.Equal(process.Landing, named.Landing);
        Assert.Equal(process.Record, named.Record);
        Assert.Equal(process.Opinion, named.Opinion);
        Assert.Equal((process.Problem, process.Unread, process.LandingCannot, process.OpinionCannot), (named.Problem, named.Unread, named.LandingCannot, named.OpinionCannot));
        Assert.Equal(WorkflowRunBinding.Why(process.Binding!), named.Decided);
        Assert.Equal("`engine`'s default", named.Decided);
        Assert.Equal(WorkflowGate.LandingSays(process), named.LandingSays);
    }

    [Fact]
    public async Task The_trees_and_the_end_read_a_process_through_the_facet_alone()
    {
        var landing = new Landing(new LandingRule(LandingForm.Branch, "feature/{quest}", Plugin: "acme.lands"), LandingSource.Repository);
        var record = new LandingWorkflow("docs-to-pr", 3, WorkflowLevels.Repository);
        var named = new Facet { Named = true, Landing = landing, Record = record };

        // The end judgement's cycle: a named process's landing and the version that decided; none reads the rule.
        var cycle = EndCycle.Accepts(Config(), "engine", "aurora", named);
        Assert.Equal((LandingForm.Branch, true, record), (cycle.Form, cycle.Plugin, cycle.Workflow));
        Assert.Null(EndCycle.Accepts(Config(), "engine", "aurora", new Facet { Named = false, Landing = landing, Record = record }).Workflow);
        Assert.Null(EndCycle.Accepts(Config(), "engine", "aurora").Workflow);

        // The plan: a landing step that cannot start says so before the press, in the process's own words.
        Directory.CreateDirectory(Tree);
        var cannot = new Facet { Named = true, Landing = landing, LandingCannot = "it needs an environment.", Says = "It follows `x` v1, and it needs an environment. Nothing was landed." };
        var plan = await new SessionTrees(_home).PlanAsync(Tree, new LandingSubject("s1", "q1", "Fix"), process: cannot);
        Assert.Equal((LandingForm.Branch, "", cannot.Says, "acme.lands"), (plan.Form, plan.Target, plan.Problem, plan.Plugin));
    }

    // ——— The installed operations

    [Fact]
    public void The_installed_operations_are_Workflows_and_one_object_answers_both_ports()
    {
        Assert.IsType<WorkplaceWorkflowsOperations>(WorkplaceWorkflows.Current);
        Assert.Same(WorkplaceWorkflows.Current, WorkplaceGateWorkflows.Current);
    }

    [Fact]
    public async Task The_operations_do_what_their_callers_did_before_they_were_ports()
    {
        Choose();
        var config = Config();
        var operations = WorkplaceWorkflows.Current;

        // The landed route's accepted blob is the route's own; a declaration that is no blob reads as none.
        var landed = WorkflowSelection.ResolveLanded(config, "engine", "aurora");
        Assert.Equal(landed.Gates, operations.AcceptedGates(config, "engine", "aurora"));
        Assert.Null(operations.AcceptedGates(config, "engine", "aurora"));
        Assert.Null(await operations.DeclarationTextAsync(_home, WorkflowSelection.NoGates, CancellationToken.None));

        // A quest's process is the one the workflows read from the chain the quests answer, the lower facet over it.
        WorkflowRunBindings.Bind(_home, WorkflowRunBindings.Plan(config, _home, "q1", "engine", "aurora", null, null, [], At));
        var asked = 0;
        Task<IReadOnlyList<QuestView>> Quests(CancellationToken ct)
        {
            asked++;
            return Task.FromResult<IReadOnlyList<QuestView>>([new QuestView("q1", "game", "engine", "Fix", "Fix it.", "Done")]);
        }

        var process = await operations.ReadProcessAsync(_home, config, Quests, "#q1", "engine", "aurora", CancellationToken.None);
        Assert.Equal(1, asked);
        Assert.Equal((true, "`docs-to-pr` v1", "q1"), (process.Named, process.Name, process.BoundRun));
        var direct = await WorkflowProcesses.ReadAsync(_home, config, Quests, "#q1", "engine", "aurora");
        Assert.Equal((direct.Named, direct.Name, direct.Binding!.Run, direct.Landing), (process.Named, process.Name, process.BoundRun, process.Landing));

        // A chain the service cannot answer holds only where a named workflow is bound here, as it always did.
        Task<IReadOnlyList<QuestView>> Unanswered(CancellationToken ct) => throw new HttpRequestException("the service did not answer");
        var unread = await operations.ReadProcessAsync(_home, config, Unanswered, "q9", "engine", "aurora", CancellationToken.None);
        Assert.StartsWith("its chain could not be read, so whether a named workflow governs it is not known:", unread.Unread);

        // A chat run bound first answers no failure, and its press reads an unheld record as its own line, live.
        Assert.Null(operations.BindChatFirst(config, _home, "chat-s1", Tree, PluginCatalog.None, At));
        Assert.True(WorkplaceRunFiles.Bound(_home, "chat-s1"));
        var (line, live) = operations.ChatPressOf("s9", "[]");
        Assert.Equal(["s9"], line);
        Assert.True(live);
    }

    [Fact]
    public async Task The_trees_ask_the_gate_operations_for_the_gate_and_the_process_with_what_they_were_handed()
    {
        var actual = WorkplaceGateWorkflows.Current;
        var gates = new GateOperations();
        var world = new World();
        var handed = new Facet();
        try
        {
            WorkplaceGateWorkflows.Current = gates;
            var trees = new SessionTrees(_home);

            var refused = await Assert.ThrowsAsync<IOException>(() => trees.GateAsync(Tree, "q1", world, "s1", process: handed, tip: "abc", run: "chat-s1"));
            Assert.Same(gates.Failure, refused);
            Assert.Equal(["gate:" + Tree + ":q1:s1:abc:chat-s1"], gates.Calls);
            Assert.Same(trees.Gates, gates.Gates);
            Assert.Same(handed, gates.Process);
            Assert.Same(world, gates.World);

            gates.Calls.Clear();
            var read = await trees.ProcessAsync(Config(), "engine", "aurora", "q1", world, run: "chat-s1");
            Assert.Same(gates.Read, read);
            Assert.Equal(["process:engine:aurora:q1:chat-s1"], gates.Calls);
        }
        finally { WorkplaceGateWorkflows.Current = actual; }
    }

    [Fact]
    public async Task A_missing_operation_is_required_only_where_it_is_reached_and_its_failures_propagate_unchanged()
    {
        var actualGates = WorkplaceGateWorkflows.Current;
        var actual = WorkplaceWorkflows.Current;
        try
        {
            WorkplaceGateWorkflows.Current = null!;
            WorkplaceWorkflows.Current = null!;
            Directory.CreateDirectory(Tree);
            var trees = new SessionTrees(_home);

            // The workplace's own second opinion needs neither: no rule reads, so nothing is asked.
            var opinion = await trees.OpinionAsync(Tree, "engine", "aurora", "q1", "s1", new World(), null, OpinionRules.Landing);
            Assert.Equal(OpinionGateStates.None, opinion.State);

            var gate = await Assert.ThrowsAsync<InvalidOperationException>(() => trees.GateAsync(Tree, "q1", new World(), "s1"));
            Assert.Contains("Workflows installs them", gate.Message);
            Assert.Contains("Workflows installs them", (await Assert.ThrowsAsync<InvalidOperationException>(() =>
                trees.ProcessAsync(Config(), "engine", "aurora", "q1", new World()))).Message);
            Assert.Contains("Workflows installs them", (await Assert.ThrowsAsync<InvalidOperationException>(() =>
                GeneratedWriters.AcceptedAsync(Config(), _home, "engine", "aurora", "main", CancellationToken.None))).Message);

            var refused = new IOException("refused");
            WorkplaceWorkflows.Current = new Operations { AcceptedFails = refused };
            Assert.Same(refused, await Assert.ThrowsAsync<IOException>(() =>
                GeneratedWriters.AcceptedAsync(Config(), _home, "engine", "aurora", "main", CancellationToken.None)));
        }
        finally
        {
            WorkplaceGateWorkflows.Current = actualGates;
            WorkplaceWorkflows.Current = actual;
        }
    }

    [Fact]
    public async Task The_generated_writers_ask_the_accepted_blob_first_and_the_declaration_only_when_git_names_that_blob()
    {
        var actual = WorkplaceWorkflows.Current;
        var operations = new Operations();
        try
        {
            WorkplaceWorkflows.Current = operations;

            // The route accepted none: nothing is asked of git or of the declaration.
            Assert.Empty(await GeneratedWriters.AcceptedAsync(Config(), _home, "engine", "aurora", "main", CancellationToken.None));
            Assert.Equal(["accepted:engine:aurora"], operations.Calls);

            // A blob accepted that the line does not hold (no repository here) is an edit asking again: the declaration is not read.
            operations.Accepted = new string('a', 40);
            operations.Calls.Clear();
            Assert.Empty(await GeneratedWriters.AcceptedAsync(Config(), _home, "engine", "aurora", "main", CancellationToken.None));
            Assert.Equal(["accepted:engine:aurora"], operations.Calls);
        }
        finally { WorkplaceWorkflows.Current = actual; }
    }

    [Fact]
    public async Task The_chat_land_binds_first_and_reads_the_press_through_the_operations_and_stops_where_the_bind_failed()
    {
        var actual = WorkplaceWorkflows.Current;
        var operations = new Operations { Failed = "the home is full" };
        using var client = Client();
        var output = new StringWriter();
        var world = new ChatLandWorld(client, _home, new SessionTrees(_home), null, output, new StringWriter());
        try
        {
            WorkplaceWorkflows.Current = operations;

            // A bind the home could not keep refuses the press, before the record is read.
            var code = await ChatLandCommand.RunAsync(world, "s1", Tree, "chat-s1", Ended("s1"), null, null);
            Assert.NotEqual(0, code);
            Assert.Equal([$"bind:chat-s1:{Tree}"], operations.Calls);
            Assert.IsAssignableFrom<IWorkplacePluginCatalog>(operations.Catalog);

            // One bound goes on to the press's view of the record: a live one is held by the running Daoris, and none is reached here.
            operations.Failed = null;
            operations.Calls.Clear();
            await ChatLandCommand.RunAsync(world, "s1", Tree, "chat-s1", Ended("s1"), null, null);
            Assert.Equal([$"bind:chat-s1:{Tree}", "press:s1"], operations.Calls);
        }
        finally { WorkplaceWorkflows.Current = actual; }
    }

    // ——— Stand-ins

    private ServiceClient Client() => new("http://ledger.test", null,
        new HttpClient(new Handler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("[]") })), disposeHandler: false));

    private string Ended(string id) => JsonSerializer.Serialize(new[] { new { id, state = "completed", kind = "chat", repository = "engine", tree = Tree } });

    private sealed class Handler(Func<HttpRequestMessage, Task<HttpResponseMessage>> answer) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => answer(request);
    }

    private sealed class World : IOpinionWorld
    {
        public Task<IReadOnlyList<QuestView>> QuestsAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<QuestView>>([]);

        public Task<AskView?> AskAsync(string id, CancellationToken ct) => Task.FromResult<AskView?>(null);

        public Task<OpinionView?> OpinionAsync(string id, CancellationToken ct) => Task.FromResult<OpinionView?>(null);
    }

    private sealed class Facet : IWorkplaceProcess
    {
        public bool Named { get; init; }

        public string Name { get; init; } = "Current";

        public Landing Landing { get; init; } = new(new LandingRule(LandingForm.Merge), LandingSource.Repository);

        public string? Problem { get; init; }

        public string? Unread { get; init; }

        public string? LandingCannot { get; init; }

        public LandingWorkflow? Record { get; init; }

        public string? BoundRun { get; init; }

        public string Decided { get; init; } = "";

        public string Says { get; init; } = "";

        public string LandingSays => Says;

        public ResolvedOpinion? Opinion { get; init; }

        public string? OpinionCannot { get; init; }
    }

    /// <summary>Stands in for the gate's two operations: it records what it was handed and fails the gate as a stand-in would.</summary>
    private sealed class GateOperations : IWorkplaceGateWorkflows
    {
        public List<string> Calls { get; } = [];

        public IOException Failure { get; } = new("the gate is stood in for");

        public IWorkplaceProcess Read { get; } = new Facet { Named = true };

        public TreeGates? Gates { get; private set; }

        public IWorkplaceProcess? Process { get; private set; }

        public IReviewWorld? World { get; private set; }

        public Task<IWorkplaceGate> GateAsync(
            TreeGates gates, string path, string? quest, IReviewWorld world, string? session, CancellationToken ct, IWorkplaceProcess? process,
            string? tip, string? run)
        {
            Calls.Add($"gate:{path}:{quest}:{session}:{tip}:{run}");
            (Gates, Process, World) = (gates, process, world);
            throw Failure;
        }

        public Task<IWorkplaceProcess> ProcessAsync(
            TreeGates gates, DriverConfig config, string repository, string? workspace, string? quest, IReviewWorld world, CancellationToken ct, string? run)
        {
            Calls.Add($"process:{repository}:{workspace}:{quest}:{run}");
            return Task.FromResult(Read);
        }
    }

    /// <summary>Stands in for the workplace's operations: it records what it was asked and answers as set.</summary>
    private sealed class Operations : IWorkplaceWorkflows
    {
        public List<string> Calls { get; } = [];

        public string? Accepted { get; set; }

        public Exception? AcceptedFails { get; init; }

        public string? Failed { get; set; }

        public IWorkplacePluginCatalog? Catalog { get; private set; }

        public string? AcceptedGates(DriverConfig config, string repository, string? workspace)
        {
            Calls.Add($"accepted:{repository}:{workspace}");
            return AcceptedFails is null ? Accepted : throw AcceptedFails;
        }

        public Task<string?> DeclarationTextAsync(string root, string blob, CancellationToken ct)
        {
            Calls.Add($"text:{blob}");
            return Task.FromResult<string?>(null);
        }

        public Task<IWorkplaceProcess> ReadProcessAsync(
            string home, DriverConfig config, Func<CancellationToken, Task<IReadOnlyList<QuestView>>> quests, string quest, string repository,
            string? workspace, CancellationToken ct) =>
            throw new NotSupportedException();

        public string? BindChatFirst(DriverConfig config, string home, string run, string tree, IWorkplacePluginCatalog plugins, DateTimeOffset at)
        {
            Calls.Add($"bind:{run}:{tree}");
            Catalog = plugins;
            return Failed;
        }

        public (IReadOnlyList<string> Line, bool Live) ChatPressOf(string session, string recordsJson)
        {
            Calls.Add($"press:{session}");
            return ([session], true);
        }
    }
}
