using System.Text.Json;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// STRUCT3b (D166; the code-structure design §3): Workflows' use cases are written against the ports its Application declares,
/// so a stand-in over memory drives each with no file: a version added and trimmed by the versions the runs name, a run bound to
/// the newest version saved, and the person's keep written on its run. The files behind the ports are held where they always
/// were, over a scratch home: <c>WorkflowNamedTests</c>, <c>WorkflowRunBindingTests</c> and <c>WorkflowGateTests</c>.
/// </summary>
public sealed class WorkflowPortsTests
{
    private static readonly DateTimeOffset At = new(2026, 10, 9, 9, 12, 30, TimeSpan.Zero);

    private static readonly DriverConfig Choice = DriverConfig.Parse("""{"workflows":{"web-app":{"default":"docs-to-pr"}}}""");

    private static JsonNode Steps(string accept) => JsonNode.Parse(
        $$"""[{"id":"work","kind":"work"},{"id":"land","kind":"landing","form":"branch","accept":"{{accept}}","pattern":"docs/{quest}","plugin":"none"}]""")!;

    private static WorkflowVersionAdded Added(string accept) =>
        new("docs-to-pr", "Documentation to a pull request", "terminal", "2026-10-09T09:00:00Z", Steps(accept));

    [Fact]
    public void A_version_is_added_through_the_store_and_the_newest_twenty_kept_with_every_version_a_run_names()
    {
        var store = new MemoryStore();
        var runs = new MemoryRuns();
        runs.Kept["docs-to-pr"] = [1];

        Assert.Equal(1, WorkflowStore.Add(store, runs, Added("automatic")));
        for (var version = 2; version <= 22; version++) Assert.Equal(version, WorkflowStore.Add(store, runs, Added("you")));

        var read = store.Load("docs-to-pr")!.Read;
        Assert.Null(read.Problem);
        Assert.Equal([1, .. Enumerable.Range(4, 19)], read.Versions.Select(each => each.Version));
        Assert.Equal(22, store.Writes);
    }

    [Fact]
    public void A_file_the_store_cannot_read_is_never_written_over()
    {
        var store = new MemoryStore();
        store.Unreadable.Add("docs-to-pr");

        var refused = Assert.Throws<DriverException>(() => WorkflowStore.Add(store, new MemoryRuns(), Added("you")));

        Assert.StartsWith("nothing was written: the workflow's file cannot be read", refused.Message);
        Assert.Equal(0, store.Writes);
    }

    [Fact]
    public void A_run_is_bound_to_the_newest_version_the_store_holds_and_one_it_does_not_hold_is_kept_with_why()
    {
        var store = new MemoryStore();
        var runs = new MemoryRuns();

        var gone = WorkflowRunBindings.Plan(Choice, store, "q0", "web-app", "work", null, null, [], At);
        Assert.Equal(("docs-to-pr", "no workflow `docs-to-pr` is saved here."), (gone.Workflow, gone.Problem));

        WorkflowStore.Add(store, runs, Added("automatic"));
        WorkflowStore.Add(store, runs, Added("you"));
        var binding = WorkflowRunBindings.Plan(Choice, store, "q1", "web-app", "work", null, null, [], At);

        Assert.Equal((2, (string?)null, WorkflowLevels.Repository), (binding.Version, binding.Problem, binding.Level));
        Assert.Equal(store.Load("docs-to-pr")!.Read.Versions[^1].Digest, binding.Digest);
    }

    [Fact]
    public void The_persons_keep_is_written_through_the_runs_port_to_the_second_and_a_run_with_no_binding_keeps_nothing()
    {
        var runs = new MemoryRuns();
        var binding = new WorkflowRunBinding("q1", "web-app", "work", "docs-to-pr", WorkflowLevels.Repository, At) { Version = 1 };
        Assert.True(runs.Bind(binding));

        Assert.Null(WorkflowRunBindings.Keep(runs, "q9", new WorkflowKept(At, ReviewDoors.Terminal)));
        var kept = WorkflowRunBindings.Keep(runs, "q1", new WorkflowKept(At.AddMilliseconds(250), ReviewDoors.Terminal) { Words = "it is the docs" });

        Assert.Equal(binding with { Kept = new WorkflowKept(At, ReviewDoors.Terminal) { Words = "it is the docs" } }, kept);
        Assert.Equal(kept, runs.Read("q1"));
        Assert.False(runs.Bound("q9"));
    }

    /// <summary>The choice of `docs-to-pr` for `web-app`, and its branch rule, which a named landing step chooses within.</summary>
    private static readonly DriverConfig Declared = DriverConfig.Parse("""
        {"workflows":{"web-app":{"default":"docs-to-pr"}},
         "landings":{"web-app":{"form":"branch","pattern":"feature/{quest}","tidy":true}}}
        """);

    [Fact]
    public void A_runs_process_is_read_through_the_ports_its_bound_version_and_a_binding_that_does_not_read_holds_it()
    {
        var store = new MemoryStore();
        var runs = new MemoryRuns();

        var none = WorkflowProcesses.Read(store, runs, Declared, "web-app", "work", "q1");
        Assert.Equal(("q1", (WorkflowRunBinding?)null, "feature/{quest}"), (none.Run, none.Binding, none.Landing.Rule.Pattern));

        WorkflowStore.Add(store, runs, Added("automatic"));
        Assert.True(runs.Bind(WorkflowRunBindings.Plan(Declared, store, "q1", "web-app", "work", null, null, [], At)));
        WorkflowStore.Add(store, runs, Added("you"));

        var named = WorkflowProcesses.Read(store, runs, Declared, "web-app", "work", "q1");
        Assert.True(named.Named);
        Assert.Equal((1, "docs/{quest}", true, true), (named.Binding!.Version, named.Landing.Rule.Pattern, named.Landing.Rule.AutoAccept, named.Landing.Rule.Tidy));

        runs.Damage("q2");
        var damaged = WorkflowProcesses.Read(store, runs, Declared, "web-app", "work", "q2");
        Assert.Equal(("its stored run binding could not be read.", (WorkflowRunBinding?)null), (damaged.Unread, damaged.Binding));

        var (name, steps) = WorkflowProcesses.Otherwise(store, Declared, "web-app", "work");
        Assert.Equal(("`docs-to-pr` v2", 2), (name, steps!.Count));
    }

    [Fact]
    public async Task A_chain_the_service_cannot_name_holds_through_the_ports_only_where_a_named_run_is_bound()
    {
        var store = new MemoryStore();
        var runs = new MemoryRuns();
        var world = new Unanswering();

        Assert.Null((await WorkflowProcesses.ReadAsync(store, runs, Declared, world, "q9", "web-app", "work")).Unread);

        WorkflowStore.Add(store, runs, Added("automatic"));
        runs.Bind(WorkflowRunBindings.Plan(Declared, store, "q1", "web-app", "work", null, null, [], At));
        Assert.True((await WorkflowProcesses.ReadAsync(store, runs, Declared, world, "q1", "web-app", "work")).Named, "the quest's own id names its run");
        Assert.StartsWith(
            "its chain could not be read, so whether a named workflow governs it is not known:",
            (await WorkflowProcesses.ReadAsync(store, runs, Declared, world, "q9", "web-app", "work")).Unread);
        Assert.Null((await WorkflowProcesses.ReadAsync(store, runs, Declared, world, "q9", "notes-site", "work")).Unread);
    }

    // ——— stand-ins

    private sealed class Unanswering : IReviewWorld
    {
        public Task<IReadOnlyList<QuestView>> QuestsAsync(CancellationToken ct) => throw new HttpRequestException("the service did not answer");

        public Task<AskView?> AskAsync(string id, CancellationToken ct) => throw new HttpRequestException("the service did not answer");
    }

    /// <summary>The named workflows in memory, each file as written; a name in <see cref="Unreadable"/> is a file that is not JSON.</summary>
    private sealed class MemoryStore : IWorkflowStore
    {
        private readonly Dictionary<string, JsonNode> _files = new(StringComparer.Ordinal);

        public HashSet<string> Unreadable { get; } = new(StringComparer.Ordinal);

        public int Writes { get; private set; }

        public WorkflowFileFound? Load(string id)
        {
            if (Unreadable.Contains(id)) return new WorkflowFileFound(id, null, new NamedWorkflowRead(null, null, [], $"`{id}.json` is not readable JSON."));
            if (!_files.TryGetValue(id, out var file)) return null;
            using var document = JsonDocument.Parse(file.ToJsonString());
            return new WorkflowFileFound(id, file.DeepClone(), WorkflowNamed.Read(document.RootElement.Clone()));
        }

        public IReadOnlyList<WorkflowFileFound> List() => [.. _files.Keys.Order(StringComparer.Ordinal).Select(id => Load(id)!)];

        public void Write(string id, JsonNode file)
        {
            _files[id] = file.DeepClone();
            Writes++;
        }
    }

    /// <summary>The runs' bindings in memory; a run bound to null is a file that does not read as a binding.</summary>
    private sealed class MemoryRuns : IWorkflowRunStore
    {
        private readonly Dictionary<string, WorkflowRunBinding?> _files = new(StringComparer.Ordinal);

        public Dictionary<string, IReadOnlyList<int>> Kept { get; } = new(StringComparer.Ordinal);

        public void Damage(string run) => _files[run] = null;

        public bool Bound(string run) => _files.ContainsKey(run);

        public WorkflowRunBinding? Read(string run) => _files.GetValueOrDefault(run);

        public bool Bind(WorkflowRunBinding binding) => _files.TryAdd(binding.Run, binding);

        public void Write(string run, WorkflowRunBinding binding) => _files[run] = binding;

        public bool AnyNamed(string repository) => _files.Values.Any(binding =>
            binding is null || (!binding.IsCurrent && string.Equals(binding.Repository, repository, StringComparison.OrdinalIgnoreCase)));

        public IReadOnlyList<int> KeptVersions(string workflow) => Kept.GetValueOrDefault(workflow) ?? [];
    }
}
