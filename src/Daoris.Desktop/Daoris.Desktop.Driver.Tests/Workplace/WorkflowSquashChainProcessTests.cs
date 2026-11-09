using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A workflow <c>land</c> → <c>push</c> → <c>pull-request</c> → <c>monitor</c> → <c>tidy</c> against stand-ins. A two-part
/// chain lands as one squashed commit, the <c>push</c> door pushes it to <see cref="GitSshHost"/>, the request is opened and then merged as a
/// squash on <see cref="PullRequestApiHost"/>, the answer is kept as the plugin's <c>work/state</c> keeps it, the monitor reads it merged (its
/// <c>when merged</c> route to the tidy; <c>when closed</c> goes to a person's task), and the
/// tidy ends both parts once: their branches gone through the journal with an Undo, each part's commits on a recovery ref, and the press
/// offering none of them again.
/// </summary>
[Trait(Category.Name, Category.Process)]
public sealed class WorkflowSquashChainProcessTests : IClassFixture<GitSshHost>, IAsyncLifetime, IDisposable
{
    private const string Token = "test-token-for-the-squash-chain";

    private const string Landed = "feature/q1-fix";

    private readonly GitSshHost _host;

    private readonly PullRequestApiHost _api = new();

    private readonly string _root = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "workflow-squash", Guid.NewGuid().ToString("N")[..8]);

    private readonly string _path = Guid.NewGuid().ToString("N")[..8] + "/engine.git";

    private string Home => Path.Combine(_root, "data");

    private string Bare => Path.Combine(_host.Projects, _path.Replace('/', Path.DirectorySeparatorChar));

    private string Seed => Path.Combine(_root, "seed");

    private string Work => Path.Combine(_root, "work");

    public WorkflowSquashChainProcessTests(GitSshHost host)
    {
        _host = host;
        Directory.CreateDirectory(Home);
        Directory.CreateDirectory(Path.Combine(_root, "scratch-home"));
        File.WriteAllText(Path.Combine(_root, DaorisHome.InstallMarker), "# Daoris — installed desktop\n");
        File.WriteAllText(Path.Combine(_root, "global.gitconfig"), "[user]\n\tname = Fixture\n\temail = fixture@example.invalid\n");
    }

    public Task InitializeAsync() => _api.InitializeAsync();

    public async Task DisposeAsync() => await _api.DisposeAsync();

    public void Dispose()
    {
        GitSeed.Discard(_root);
        GitSeed.Discard(Path.GetDirectoryName(Bare)!);
    }

    private ProcessStartInfo Git(string folder, params string[] arguments)
    {
        var (info, refusal) = WorkingTree.GitStart(folder, arguments, Home);
        Assert.Null(refusal);
        var scratch = Path.Combine(_root, "scratch-home");
        info!.Environment["GIT_CONFIG_NOSYSTEM"] = "1";
        info.Environment["GIT_CONFIG_GLOBAL"] = Path.Combine(_root, "global.gitconfig");
        info.Environment["HOME"] = scratch;
        info.Environment["USERPROFILE"] = scratch;
        info.Environment["XDG_CONFIG_HOME"] = scratch;
        info.Environment.Remove(GitRouting.GrantVariable);
        return TestGit.Ceiling(info);
    }

    private async Task<string> Ok(ProcessStartInfo info)
    {
        info.UseShellExecute = false;
        info.CreateNoWindow = true;
        info.RedirectStandardOutput = true;
        info.RedirectStandardError = true;
        info.RedirectStandardInput = true;
        using var process = Process.Start(info)!;
        process.StandardInput.Close();
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync(new CancellationTokenSource(TimeSpan.FromSeconds(90)).Token);
        Assert.True(process.ExitCode == 0, $"git {string.Join(' ', info.ArgumentList)}: {await error}");
        return (await output).Trim();
    }

    private async Task<string?> RefAsync(string folder, string reference)
    {
        var (code, at, _) = await WorkingTree.GitAsync(folder, ["rev-parse", "--verify", "--quiet", reference], CancellationToken.None);
        return code == 0 ? at.Trim() : null;
    }

    /// <summary>
    /// The bare repository with one commit, an identity authorized on the host, and a clone holding a two-part chain: <c>work/one</c>, then
    /// <c>work/two</c> grown from it, each a unit of its own, landed as one squashed commit on <see cref="Landed"/>. The clone's checkout is
    /// back on <c>main</c>, so no tree holds either part.
    /// </summary>
    private async Task<(string One, string Two, string Squashed)> ReadyAsync()
    {
        Directory.CreateDirectory(Bare);
        await Ok(Git(Bare, "init", "--bare", "--initial-branch=main"));
        Directory.CreateDirectory(Seed);
        await Ok(Git(Seed, "init", "--initial-branch=main"));
        File.WriteAllText(Path.Combine(Seed, "a.txt"), "a\n");
        await Ok(Git(Seed, "add", "a.txt"));
        await Ok(Git(Seed, "commit", "-m", "first"));
        await Ok(Git(Seed, "push", Bare, "main"));

        var identity = await GitIdentityActs.AddSshAsync(Home, _host.Host, "Fixture", null, null, null, rsa: true, CancellationToken.None);
        _host.Authorize(GitKeys.PublicKey(Home, identity.Id)!);
        var tested = await GitIdentityActs.TestAsync(Home, identity.Id, GitKeys.Describe(_host.HostKey)!.Value.Fingerprint, CancellationToken.None);
        Assert.True(tested.Result == "reached", $"{tested.Result}: {tested.Words}");
        GitDoorsLaid.Lay(_root);

        await Ok(Git(_root, "clone", _host.Address(_path), Work));
        var main = await Ok(Git(Work, "rev-parse", "HEAD"));
        await Ok(Git(Work, "switch", "-c", "work/one"));
        File.AppendAllText(Path.Combine(Work, "a.txt"), "one\n");
        await Ok(Git(Work, "commit", "-am", "the first part"));
        var one = await Ok(Git(Work, "rev-parse", "HEAD"));
        await Ok(Git(Work, "switch", "-c", "work/two"));
        File.AppendAllText(Path.Combine(Work, "a.txt"), "two\n");
        File.WriteAllText(Path.Combine(Work, "b.txt"), "b\n");
        await Ok(Git(Work, "add", "b.txt"));
        await Ok(Git(Work, "commit", "-am", "the second part"));
        var two = await Ok(Git(Work, "rev-parse", "HEAD"));
        var squashed = await Ok(Git(Work, "commit-tree", $"{two}^{{tree}}", "-p", main, "-m", "Fix the gap"));
        await Ok(Git(Work, "branch", Landed, squashed));
        await Ok(Git(Work, "switch", "main"));

        var at = DateTimeOffset.UtcNow;
        var sessions = new SessionBranches(Home);
        sessions.Record(new GrownBranch("engine", "default", "work/one", "main", main, null, at) { Unit = "u1" });
        sessions.Record(new GrownBranch("engine", "default", "work/two", "main", one, "work/one", at) { Unit = "u2" });
        new LandedBranches(Home).Record(new LandedBranch("engine", "default", Landed, "main", squashed, "s-q2", "q1", "Fix the gap", at)
        {
            Squashed = two, Units = ["u1", "u2"],
        });
        return (one, two, squashed);
    }

    /// <summary>The graph a place composes from the prebuilt set: the landing, the push, the plugin's request, its watch and the tidy.</summary>
    private const string Graph = """
        {"schemaVersion":2,"entries":["work"],"end":"tidy","nodes":[
          {"id":"work","kind":"claimed","version":1,"reach":"observed"},
          {"id":"landing","kind":"land","version":1,"reach":"performed","options":{"method":"branch"}},
          {"id":"push","kind":"push","version":1,"reach":"performed"},
          {"id":"pull-request","kind":"acme-host/pull-request","version":1,"reach":"performed"},
          {"id":"monitor","kind":"monitor","version":1,"reach":"observed","outcomes":["merged","closed"]},
          {"id":"tidy","kind":"tidy","version":1,"reach":"performed"},
          {"id":"closed","kind":"human","version":1,"reach":"pressed"}],
         "wires":[{"id":"a","kind":"after","from":"work","to":"landing"},{"id":"b","kind":"after","from":"landing","to":"push"},
                  {"id":"c","kind":"after","from":"push","to":"pull-request"},{"id":"d","kind":"after","from":"pull-request","to":"monitor"},
                  {"id":"e","kind":"when","from":"monitor","to":"tidy","outcome":"merged"},{"id":"f","kind":"when","from":"monitor","to":"closed","outcome":"closed"}]}
        """;

    [Fact]
    public async Task A_squash_merged_chain_is_ended_once_by_its_tidy_its_branches_gone_with_an_Undo_and_the_press_offers_none_of_them()
    {
        var (graph, problems) = NodeGraphs.Read(Graph, onSave: true);
        Assert.Empty(problems);
        var nodes = graph!.Nodes.ToDictionary(node => node.Kind, StringComparer.Ordinal);
        var (one, two, squashed) = await ReadyAsync();
        var landings = new LandedBranches(Home);

        // The push: the landed branch reaches origin through the push act.
        var pushing = new PushDoor(Home);
        Assert.True(pushing.Start(new PushStart("run:q1", nodes[NodeKinds.Push], "engine", "default", Work, Landed, squashed) { Authority = StepAuthority.OfDoor("q1", "push", 1) }));
        await pushing.IdleAsync();
        Assert.Equal(squashed, await RefAsync(Bare, $"refs/heads/{Landed}"));

        // The pull request: opened with the token, its address kept on the landing as the step's evidence keeps it.
        _api.Accept(Token);
        using var client = new HttpClient { BaseAddress = new Uri(_api.Address) };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token);
        var opened = await client.PostAsJsonAsync("/repos/acme/engine/pulls", new { title = "Fix the gap", head = Landed, @base = "main", body = "" });
        Assert.Equal(System.Net.HttpStatusCode.Created, opened.StatusCode);
        var id = Assert.Single(_api.PullRequests).Id;
        var address = $"{_api.Address.TrimEnd('/')}/acme/engine/pull/{id}";
        Assert.True(new PullRequestRecords(Home).Keep("engine", Landed, "acme-host", address));
        Assert.Null(PullRequestReading.Of(landings.Of("engine", Landed)).State);

        // The platform squash-merges it: one new commit on main, holding neither part's commits by ancestry.
        await Ok(Git(Seed, "fetch", Bare, Landed));
        await Ok(Git(Seed, "merge", "--squash", "FETCH_HEAD"));
        await Ok(Git(Seed, "commit", "-m", $"Fix the gap (#{id})"));
        var merged = await Ok(Git(Seed, "rev-parse", "HEAD"));
        await Ok(Git(Seed, "push", Bare, "main"));
        _api.Complete(id, merged);

        // The monitor: the request's state as `work/state` answers it, kept on the landing, read merged.
        var state = await client.GetFromJsonAsync<JsonElement>($"/repos/acme/engine/pulls/{id}");
        Assert.True(state.GetProperty("merged").GetBoolean());
        landings.Answered(landings.Of("engine", Landed)!, new PullRequestState(PullRequestStates.Completed)
        {
            PullRequest = address, MergeCommit = merged, SourceCommit = squashed, Target = "main", How = MergeHow.Squash, Plugin = "acme-host",
            At = DateTimeOffset.UtcNow, AskedAt = DateTimeOffset.UtcNow,
        });
        var watched = PullRequestReading.Of(landings.Of("engine", Landed));
        Assert.True(watched.State == PullRequestReading.Merged, watched.Said);

        // The tidy: both parts ended once, each branch gone through the journal, each part's commits on a recovery ref.
        var tidying = new TidyDoor(Home);
        Assert.True(tidying.Start(new TidyStart("run:q1", nodes[NodeKinds.Tidy], "engine", "default", Work, Landed, squashed) { Authority = StepAuthority.OfDoor("q1", "tidy", 1) }));
        await tidying.IdleAsync();
        var tidied = landings.Of("engine", Landed)!.Tidied!;
        Assert.Equal(squashed, tidied.Tip);
        Assert.Equal([("work/one", one), ("work/two", two)], tidied.Parts.Select(part => (part.Branch, part.Tip)));
        Assert.All(tidied.Parts, part => Assert.Null(part.Kept));
        Assert.Null(await RefAsync(Work, "refs/heads/work/one"));
        Assert.Null(await RefAsync(Work, "refs/heads/work/two"));
        Assert.Equal(one, await RefAsync(Work, tidied.Parts[0].KeptOn!));
        Assert.Equal(two, await RefAsync(Work, tidied.Parts[1].KeptOn!));
        Assert.True(TidyReading.Tidied(landings.Of("engine", Landed)));

        // Once: a second start at the same tip ends nothing more.
        Assert.True(tidying.Start(new TidyStart("run:q1", nodes[NodeKinds.Tidy], "engine", "default", Work, Landed, squashed) { Authority = StepAuthority.OfDoor("q1", "tidy", 1) }));
        await tidying.IdleAsync();
        Assert.Equal(tidied, landings.Of("engine", Landed)!.Tidied);

        // The press offers none of them again.
        var trees = new SessionTrees(Home);
        var plan = await trees.SweepPlanAsync([("engine", "default", Work)], new HashSet<string>());
        Assert.DoesNotContain(plan, item => item.Branch is "work/one" or "work/two");

        // The Undo makes a part's branch again at its commit; the press then reads the tidy's record for it and judges it no more.
        var journal = new ActJournal(Home);
        var undone = await GitRefActs.UndoAsync(new ActRuns(journal), "engine", Work, tidied.Parts[0].Act!, "window", "window", CancellationToken.None);
        Assert.False(undone.Refused, undone.Said);
        Assert.Equal(one, await RefAsync(Work, "refs/heads/work/one"));
        var again = await trees.SweepPlanAsync([("engine", "default", Work)], new HashSet<string>());
        if (again.FirstOrDefault(item => item.Branch == "work/one") is { } row)
        {
            Assert.Equal(("tidy", true), (row.Hold, row.OnLine));
        }
    }

    [Fact]
    public async Task A_request_closed_without_merging_is_routed_to_the_person_and_ends_nothing_and_fails_nothing()
    {
        var graph = NodeGraphs.Read(Graph, onSave: true).Graph!;
        var nodes = graph.Nodes.ToDictionary(node => node.Kind, StringComparer.Ordinal);
        var (one, two, squashed) = await ReadyAsync();
        var landings = new LandedBranches(Home);
        landings.PullRequested("engine", Landed, "acme-host", "https://example.invalid/acme/engine/pull/1");
        landings.Answered(landings.Of("engine", Landed)!, new PullRequestState(PullRequestStates.Abandoned) { Plugin = "acme-host", AskedAt = DateTimeOffset.UtcNow });

        // The monitor reads it closed, which the graph routes to the person's task and never to the tidy.
        Assert.Equal(PullRequestReading.Closed, PullRequestReading.Of(landings.Of("engine", Landed)).State);
        var closed = Assert.Single(graph.Wires, wire => wire.Outcome == PullRequestReading.Closed);
        Assert.Equal(NodeKinds.Human, graph.Nodes.Single(node => node.Id == closed.To.Node).Kind);

        // A tidy started all the same ends nothing and fails nothing.
        var tidying = new TidyDoor(Home);
        Assert.True(tidying.Start(new TidyStart("run:q1", nodes[NodeKinds.Tidy], "engine", "default", Work, Landed, squashed) { Authority = StepAuthority.OfDoor("q1", "tidy", 1) }));
        await tidying.IdleAsync();

        Assert.Null(landings.Of("engine", Landed)!.Tidied);
        Assert.False(File.Exists(NodeRecordFiles.PathOf(Home, "run:q1")));
        Assert.Equal(one, await RefAsync(Work, "refs/heads/work/one"));
        Assert.Equal(two, await RefAsync(Work, "refs/heads/work/two"));
    }
}
