using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FABRIC-CLOSE1p (the fabric events design §E5 step 1; D173's CLOSE1b note, "After review (FABRIC-CLOSE1l)", finding 5): the close's fresh
/// fence reads the branches' tips from git, through the loop's own units read (<see cref="DriverWatch.ReadNodeUnitsAsync"/>) over real git and
/// a stand-in service. A session branch moved after the prerequisite's acceptance sends no close until the work is judged again on the new
/// tip; and every read of the units takes one <c>for-each-ref</c> for the branches sharing a repository, none where no quest is bound.
/// </summary>
/// <remarks>Units the test hands in would miss this: the defect was that the loop's read never asked git (LANDGIT1's counts, PROC1d's one look).</remarks>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class CloseFenceGitTests : IDisposable
{
    private const string Plugin = "acme.deploy";

    /// <summary>The plugin's deploy, then the close: the deploy is the close's prerequisite, its acceptance bound to the quest's revision.</summary>
    private const string Deployed = """
        { "workCycles": { "engine": { "quest": {
            "schemaVersion": 2, "entries": ["deploy"], "end": "close",
            "nodes": [{ "id": "deploy", "kind": "acme.deploy/deploy", "version": 1, "reach": "performed" },
                      { "id": "close", "kind": "quest.close", "version": 1, "reach": "performed" }],
            "wires": [{ "id": "a", "kind": "after", "from": "deploy", "to": "close" }] } } } }
        """;

    private static readonly NodeDeclaration Step = new(Plugin + "/deploy", Plugin + "/deploy", 1, NodeReach.Performed)
    {
        Retry = new NodeRetry(3, [30, 60]),
        Perform = new NodePerform(Idempotent: true, Reconcile: true, Transient: ["unavailable"]),
    };

    private readonly string _scratch;
    private readonly string _home;
    private readonly DateTimeOffset _now = new(2026, 10, 18, 9, 0, 0, TimeSpan.Zero);
    private readonly Port _port = new();
    private readonly Door _door = new();

    public CloseFenceGitTests()
    {
        _scratch = Path.Combine(RepoRoot(), "_fixtures", "close-fence-git", Guid.NewGuid().ToString("N")[..8]);
        _home = Path.Combine(_scratch, "daoris-home");
        Directory.CreateDirectory(_home);
    }

    public void Dispose()
    {
        GitSeed.Discard(_scratch);
    }

    [Fact]
    public async Task A_session_branch_moved_after_the_prerequisites_acceptance_sends_no_close_until_the_work_is_judged_again()
    {
        var root = await RepositoryAsync("engine");
        var main = (await GitAsync(root, "rev-parse", "HEAD")).Trim();
        // Two sessions served the quest, each on its own branch in the same repository.
        foreach (var session in new[] { "s1", "s2" })
        {
            await GitAsync(root, "branch", "daoris/" + session);
            new SessionBranches(_home).Record(new GrownBranch("engine", "default", "daoris/" + session, "main", main, null, _now) { Unit = "unit-" + session });
        }

        var digest = NodeGraphs.Digest(NodeGraphHomes.Work(DriverConfig.Parse(Deployed), "engine", "default", WorkUnitKind.Quest).Graph!);
        var ledger = new Ledger(root, Path.Combine(_scratch, "trees"));
        using var client = ledger.Client();

        // Unbound, as every quest on the owner's install: the loop's read asks git nothing.
        var unbound = new List<string>();
        using (Counting(unbound))
        {
            var read = await DriverWatch.ReadNodeUnitsAsync(_home, client, default);
            Assert.Contains(read.Units, unit => unit.Id == "unit-s1" && unit.Links.Contains(new WorkLink(WorkRelation.ProducedBy, "quest:q7")));
        }

        ledger.Closure = digest;
        var reads = 0;
        Func<Task>? beforeRead = null;
        var look = new NodeRuntimeLook(DriverConfig.Parse(Deployed), Whole: true)
        {
            Units = async ct =>
            {
                reads++;
                if (beforeRead is { } before) await before();
                return await DriverWatch.ReadNodeUnitsAsync(_home, client, ct);
            },
            Steps = () => [Step],
        };
        var runtime = new NodeRuntime(new NodeRecordFiles(_home, () => true), _port, () => _now, () => true, _door);

        // The deploy is sent; the close waits behind it.
        var first = new List<string>();
        using (Counting(first)) await runtime.PassAsync(look, default);
        var deployed = Assert.Single(_port.Sent);
        Assert.Empty(_door.Sent);

        // The deploy is accepted on the quest's revision; the close is judged next, and between the pass's read and the fence's, the
        // second session commits once more on its branch.
        _port.Heard.Enqueue(Reached(deployed.Invocation));
        var fence = reads + 2;
        beforeRead = async () =>
        {
            if (reads == fence) await CommitAsync(root, "daoris/s2", "late.txt");
        };
        var fenced = new List<string>();
        NodeRuntimePass moved;
        using (Counting(fenced)) moved = await runtime.PassAsync(look, default);
        beforeRead = null;
        Assert.Empty(_door.Sent);
        Assert.Contains(moved.Said, line => line.Contains("its close was not sent", StringComparison.Ordinal)
                                             && line.Contains("a branch it worked moved", StringComparison.Ordinal));

        // The next judgement reads the new tip: the deploy's acceptance on the old revision lapses and it is sent again, and still no close.
        await runtime.PassAsync(look, default);
        Assert.Equal(2, _port.Sent.Count);
        Assert.NotEqual(deployed.SubjectRevision, _port.Sent[1].SubjectRevision);
        Assert.Empty(_door.Sent);

        // Judged again on the new tip: the close is sent, once, bound to the revision the new deploy was accepted on.
        _port.Heard.Enqueue(Reached(_port.Sent[1].Invocation));
        await runtime.PassAsync(look, default);
        var (quest, _, _, named) = Assert.Single(_door.Sent);
        Assert.Equal("q7", quest);
        Assert.Equal([new QuestCloseItem("q7", "desk", 41)], named);
        var record = new NodeRecordFiles(_home, () => true).Read("quest:q7")!;
        var close = Assert.Single(record.Attempts, attempt => attempt.Node == "close");
        Assert.Equal(_port.Sent[1].SubjectRevision, close.Call.SubjectRevision);
        Assert.Equal(NodeStates.Reached, record.Statuses["close"].State);

        // One for-each-ref a read for both branches, which share the repository: the pass's, and the fence's own right before the close.
        Assert.Empty(unbound);
        Assert.Equal([Snapshot], first);
        Assert.Equal([Snapshot, Snapshot], fenced);
    }

    [Fact]
    public async Task An_unarmed_look_reads_no_units_and_starts_no_git()
    {
        var reads = 0;
        var look = new NodeRuntimeLook(DriverConfig.Parse("{}"), Whole: true)
        {
            Units = _ =>
            {
                reads++;
                return Task.FromResult(new NodeUnitsRead([], new Dictionary<string, string>()));
            },
        };
        var starts = new List<string>();
        using (Counting(starts))
        {
            var pass = await new NodeRuntime(new NodeRecordFiles(_home, () => true), _port, () => _now, () => true, _door).PassAsync(look, default);
            Assert.False(pass.Armed);
        }

        Assert.Equal(0, reads);
        Assert.Empty(starts);
    }

    /// <summary>The one start a read makes per repository, as the counter writes it.</summary>
    private const string Snapshot = "git for-each-ref --format=%(refname) %(objectname) %(objecttype) %(upstream) %(upstream:short) refs/heads refs/remotes/origin  @ engine";

    private WorkStepHeard Reached(string invocation) =>
        new(Plugin, 1, new WorkStepFrame("deploy", new WorkOutcome(WorkStates.Reached, Evidence: new WorkEvidence("acme.deploy/receipt@1:r1", null, null, "deployed")),
            _now, "e-" + invocation, Invocation: invocation));

    private IDisposable Counting(List<string> starts) => ProcessStarts.Within(info =>
    {
        var program = Path.GetFileNameWithoutExtension(info.FileName).ToLowerInvariant();
        var arguments = info.ArgumentList.SkipWhile(each => each is "-c" or "core.longpaths=true");
        var where = Path.GetRelativePath(_scratch, info.WorkingDirectory).Replace('\\', '/');
        lock (starts) starts.Add(program + " " + string.Join(' ', arguments) + "  @ " + where);
        return Process.Start(info);
    });

    /// <summary>The plugin's work process as the runtime sees it: each perform kept, each completion the test's.</summary>
    private sealed class Port : IWorkRuntimePort
    {
        public List<WorkPerformCall> Sent { get; } = [];

        public Queue<WorkStepHeard> Heard { get; } = new();

        public Task<int> PerformAsync(string plugin, WorkPerformCall call, CancellationToken ct)
        {
            Sent.Add(call);
            return Task.FromResult(1);
        }

        public Task<(WorkReconcileAnswer Answer, int Generation)> ReconcileStepAsync(string plugin, WorkReconcileCall call, CancellationToken ct) =>
            Task.FromResult((new WorkReconcileAnswer(ReconcileStates.Unknown), 1));

        public int? GenerationOf(string plugin) => 1;

        public void Watch(IReadOnlyDictionary<string, IReadOnlyList<WatchedUnit>> awaited)
        {
        }

        public IReadOnlyList<WorkStepHeard> TakeSteps()
        {
            var taken = Heard.ToList();
            Heard.Clear();
            return taken;
        }
    }

    /// <summary>The close door: each close kept, and closed.</summary>
    private sealed class Door : IQuestClosePort
    {
        public List<(string Quest, string Key, string Token, IReadOnlyList<QuestCloseItem> Quests)> Sent { get; } = [];

        public Task<QuestCloseAnswer> CloseQuestsAsync(string quest, string key, string token, IReadOnlyList<QuestCloseItem> quests, CancellationToken ct)
        {
            Sent.Add((quest, key, token, quests));
            return Task.FromResult(new QuestCloseAnswer(QuestCloseOutcomes.Closed, null, "Closed.", []));
        }

        /// <summary>FABRIC-CLOSE1u: every close here is answered as it is sent, so none is ever asked after.</summary>
        public Task<QuestCloseAnswer> CloseOutcomeAsync(string quest, string key, string token, IReadOnlyList<QuestCloseItem> quests, CancellationToken ct) =>
            throw new InvalidOperationException("no close is asked after here");
    }

    /// <summary>The service's reads, standing in: the quest, its two sessions on their trees, no ask, and the repository's checkout.</summary>
    private sealed class Ledger(string root, string trees) : HttpMessageHandler
    {
        public string? Closure { get; set; }

        public ServiceClient Client() => new("http://units.test", null, new HttpClient(this, disposeHandler: false));

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            JsonNode? answer = request.RequestUri!.AbsolutePath switch
            {
                "/api/quests" => new JsonArray(new JsonObject
                {
                    ["id"] = "q7", ["from"] = "game", ["to"] = "engine", ["workspace"] = "default", ["title"] = "Ship q7", ["body"] = "", ["status"] = "Taken",
                    ["closure"] = Closure, ["submission"] = new JsonObject { ["machine"] = "desk", ["sequence"] = 41, ["note"] = "ready" },
                }),
                "/api/asks" => new JsonArray(),
                "/api/sessions" => new JsonArray(Session("s1"), Session("s2")),
                "/api/registry" => new JsonArray(new JsonObject
                {
                    ["repository"] = "engine", ["adopted"] = true, ["registered"] = true, ["owns"] = new JsonArray(), ["accepts"] = new JsonArray(), ["root"] = root,
                }),
                _ => null,
            };
            return Task.FromResult(answer is null
                ? new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent("{\"error\":\"no\"}", Encoding.UTF8, "application/json") }
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(answer.ToJsonString(), Encoding.UTF8, "application/json") });
        }

        private JsonObject Session(string id) => new()
        {
            ["id"] = id, ["quest"] = "q7", ["repository"] = "engine", ["state"] = "working", ["kind"] = "driven",
            ["tree"] = Path.Combine(trees, id), ["created"] = "2026-10-18T08:00:00.0000000+00:00",
        };
    }

    private async Task<string> RepositoryAsync(string name)
    {
        var root = Path.Combine(_scratch, name);
        Directory.CreateDirectory(root);
        await GitAsync(root, "init", "--quiet", "-b", "main");
        await GitAsync(root, "config", "user.email", "fixture@example.test");
        await GitAsync(root, "config", "user.name", "Fixture");
        await File.WriteAllTextAsync(Path.Combine(root, "README.md"), $"# {name}\n");
        await GitAsync(root, "add", "-A");
        await GitAsync(root, "commit", "-q", "-m", "first");
        return root;
    }

    /// <summary>One commit on <paramref name="branch"/>, made without checking it out: its tip moves, and nothing else.</summary>
    private static async Task CommitAsync(string root, string branch, string file)
    {
        var blob = (await GitAsync(root, "rev-parse", $"refs/heads/{branch}^{{tree}}")).Trim();
        var parent = (await GitAsync(root, "rev-parse", $"refs/heads/{branch}")).Trim();
        var commit = (await GitAsync(root, "commit-tree", blob, "-p", parent, "-m", "one more step: " + file)).Trim();
        await GitAsync(root, "update-ref", $"refs/heads/{branch}", commit, parent);
    }

    private static async Task<string> GitAsync(string cwd, params string[] arguments)
    {
        var info = new ProcessStartInfo
        {
            FileName = "git",
            WorkingDirectory = cwd,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        using var process = Process.Start(TestGit.Past(info))!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        Assert.True(process.ExitCode == 0, $"git {string.Join(' ', arguments)}: {await stderr}");
        return await stdout;
    }

    private static string RepoRoot() => GitSeed.RepoRoot();
}
