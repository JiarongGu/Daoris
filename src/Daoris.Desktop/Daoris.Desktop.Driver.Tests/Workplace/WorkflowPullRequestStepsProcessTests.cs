using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// G8's proof (D241 points 4 and 5; the git design §3.1 row G8 and §4.1's workflow row): a workflow <c>land</c> (branch), then <c>push</c>, then a
/// plugin's <c>pull-request</c> step, each step against its stand-in. The <c>push</c> node's door pushes the landed branch through the same push act
/// the person's press uses, under its grant, to <see cref="GitSshHost"/>; the pull request is then opened with the identity's token on
/// <see cref="PullRequestApiHost"/> by the plugin's call (here the stand-in's own HTTP call, the example plugins' real ones being G9's). The graph
/// that says it is judged by the save as a graph of steps.
/// </summary>
[Trait(Category.Name, Category.Process)]
public sealed class WorkflowPullRequestStepsProcessTests : IClassFixture<GitSshHost>, IAsyncLifetime, IDisposable
{
    private const string Token = "test-token-for-the-pull-request-step";

    private readonly GitSshHost _host;

    private readonly PullRequestApiHost _api = new();

    private readonly string _root = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "workflow-pr", Guid.NewGuid().ToString("N")[..8]);

    private readonly string _path = Guid.NewGuid().ToString("N")[..8] + "/engine.git";

    private string Home => Path.Combine(_root, "data");

    private string Bare => Path.Combine(_host.Projects, _path.Replace('/', Path.DirectorySeparatorChar));

    private string Work => Path.Combine(_root, "work");

    public WorkflowPullRequestStepsProcessTests(GitSshHost host)
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

    /// <summary>The bare repository with one commit, an identity authorized on the host, the doors laid, and a clone with one commit on a branch of its own.</summary>
    private async Task<string> ReadyAsync()
    {
        Directory.CreateDirectory(Bare);
        await Ok(Git(Bare, "init", "--bare", "--initial-branch=main"));
        var seed = Path.Combine(_root, "seed");
        Directory.CreateDirectory(seed);
        await Ok(Git(seed, "init", "--initial-branch=main"));
        File.WriteAllText(Path.Combine(seed, "a.txt"), "a\n");
        await Ok(Git(seed, "add", "a.txt"));
        await Ok(Git(seed, "commit", "-m", "first"));
        await Ok(Git(seed, "push", Bare, "main"));

        var identity = await GitIdentityActs.AddSshAsync(Home, _host.Host, "Fixture", null, null, null, rsa: true, CancellationToken.None);
        _host.Authorize(GitKeys.PublicKey(Home, identity.Id)!);
        var tested = await GitIdentityActs.TestAsync(Home, identity.Id, GitKeys.Describe(_host.HostKey)!.Value.Fingerprint, CancellationToken.None);
        Assert.True(tested.Result == "reached", $"{tested.Result}: {tested.Words}");
        GitDoorsLaid.Lay(_root);

        await Ok(Git(_root, "clone", _host.Address(_path), Work));
        await Ok(Git(Work, "switch", "-c", "work/fix"));
        File.AppendAllText(Path.Combine(Work, "a.txt"), "fixed\n");
        await Ok(Git(Work, "commit", "-am", "fixed"));
        return await Ok(Git(Work, "rev-parse", "HEAD"));
    }

    /// <summary>The graph a place composes (§2.5): a branch landing, the push, and the plugin's pull-request step.</summary>
    private const string Graph = """
        {"schemaVersion":2,"entries":["work"],"end":"pull-request","nodes":[
          {"id":"work","kind":"claimed","version":1,"reach":"observed"},
          {"id":"landing","kind":"land","version":1,"reach":"performed","options":{"method":"branch"}},
          {"id":"push","kind":"push","version":1,"reach":"performed"},
          {"id":"pull-request","kind":"acme-host/pull-request","version":1,"reach":"performed"}],
         "wires":[{"id":"a","kind":"after","from":"work","to":"landing"},{"id":"b","kind":"after","from":"landing","to":"push"},
                  {"id":"c","kind":"after","from":"push","to":"pull-request"}]}
        """;

    [Fact]
    public void A_land_then_push_then_a_plugins_pull_request_is_a_graph_of_steps_the_save_judges_whole()
    {
        var (graph, problems) = NodeGraphs.Read(Graph, onSave: true);

        Assert.NotNull(graph);
        Assert.Empty(problems);
        Assert.Equal([NodeKinds.Claimed, NodeKinds.Land, NodeKinds.Push, "acme-host/pull-request"], graph!.Nodes.Select(node => node.Kind));
    }

    [Fact]
    public async Task The_push_node_pushes_the_landed_branch_through_the_push_act_and_the_plugin_then_opens_the_pull_request_with_the_token()
    {
        var tip = await ReadyAsync();
        var landings = new LandedBranches(Home);
        landings.Record(new LandedBranch("engine", "default", "work/fix", "main", tip, "s-q1", "q1", "Fix the gap", DateTimeOffset.UtcNow));
        var node = NodeGraphs.Read(Graph, onSave: true).Graph!.Nodes.Single(each => each.Kind == NodeKinds.Push);

        // The push node's door: the person's push act, bound to this one ref and commit, which origin hears once.
        var door = new PushDoor(Home);
        Assert.True(door.Start(new PushStart("run:q1", node, "engine", "default", Work, "work/fix", tip)));
        await door.IdleAsync();

        var (exit, remote, _) = await Run(Git(Bare, "rev-parse", "--verify", "--quiet", "refs/heads/work/fix"));
        Assert.Equal((0, tip), (exit, remote));
        var kept = landings.Of("engine", "work/fix")!;
        Assert.Equal((true, tip), (kept.Pushed, kept.PushedTip));
        Assert.Empty(Directory.Exists(DaorisHome.Entry(Home, HomeEntry.GitGrants)) ? Directory.GetFiles(DaorisHome.Entry(Home, HomeEntry.GitGrants)) : []);

        // The pull-request step: the handed token opens the request from the pushed branch; a refused token opens none.
        _api.Accept(Token);
        using var client = new HttpClient { BaseAddress = new Uri(_api.Address) };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "not-the-token");
        var refused = await client.PostAsJsonAsync("/repos/acme/engine/pulls", new { title = "Fix the gap", head = "work/fix", @base = "main", body = "" });
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, refused.StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token);
        var opened = await client.PostAsJsonAsync("/repos/acme/engine/pulls", new { title = "Fix the gap", head = "work/fix", @base = "main", body = "" });
        Assert.Equal(System.Net.HttpStatusCode.Created, opened.StatusCode);
        Assert.Equal(("work/fix", "main"), (Assert.Single(_api.PullRequests).Head, _api.PullRequests[0].Base));
    }

    private static async Task<(int Exit, string Out, string Err)> Run(ProcessStartInfo info)
    {
        info.UseShellExecute = false;
        info.CreateNoWindow = true;
        info.RedirectStandardOutput = true;
        info.RedirectStandardError = true;
        using var process = Process.Start(info)!;
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return (process.ExitCode, (await output).Trim(), await error);
    }
}
