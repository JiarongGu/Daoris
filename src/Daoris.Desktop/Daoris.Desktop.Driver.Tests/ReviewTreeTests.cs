using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// XAGENT1d (D155 point 5; the second-agent design §5.1): a reviewer's own tree is a clone of the repository at the candidate,
/// made by Daoris under the home's trees, with no remote, and removed when the pass ends. A ref the reviewer writes there never
/// reaches the repository, and nothing in it is read back. Then the pass itself, end to end with no model: the candidate read,
/// the copy made, the record opened, a reviewer that does not follow its instruction run in its copy, and the copy gone after.
/// </summary>
/// <remarks>
/// Real git and a real process — node, the stand-in harness every gate already needs — against a stand-in service on a
/// loopback port, so the class is in the real-process half (MOD8).
/// </remarks>
[Trait(Category.Name, Category.Process)]
public sealed class ReviewTreeTests : IDisposable
{
    private const string Identity = "-c user.name=\"Driver Tests\" -c user.email=\"tests@example.invalid\"";

    private readonly GitTree _repository = new("review-tree");
    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-review-tree-" + Guid.NewGuid().ToString("N")[..8]);
    private readonly string _base;
    private readonly string _middle;
    private readonly string _tip;

    public ReviewTreeTests()
    {
        Directory.CreateDirectory(_home);
        _base = _repository.Output("rev-parse HEAD");
        // The working session's branch, as a session tree grows one: the work is two commits on it, never on the line.
        _repository.Git("checkout -q -b daoris/s-1a2b3c4d");
        _repository.Commit("comparison.md");
        _middle = _repository.Output("rev-parse HEAD");
        File.WriteAllText(Path.Combine(_repository.Root, "README.md"), "# fixture, read again\n");
        _repository.Git($"{Identity} commit -q -am \"the readme read again\"");
        _tip = _repository.Output("rev-parse HEAD");
        _repository.Git("checkout -q main");
    }

    public void Dispose()
    {
        _repository.Dispose();
        try
        {
            foreach (var file in Directory.EnumerateFiles(_home, "*", SearchOption.AllDirectories)) File.SetAttributes(file, FileAttributes.Normal);
            Directory.Delete(_home, recursive: true);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    /// <summary>Every ref the repository holds, with what it points at: what a reviewer's writes must never change.</summary>
    private string Refs() => _repository.Output("for-each-ref --format=\"%(refname) %(objectname)\"");

    private static string In(string tree, string arguments) => GitFixture.RunLine(tree, arguments).Stdout.Trim();

    // ——— The copy.

    [Fact]
    public async Task The_copy_is_a_clone_under_the_home_s_trees_detached_at_the_tip_with_no_remote()
    {
        var opened = await OpinionTree.OpenAsync(_home, _repository.Root, "reports", "work", _tip);

        Assert.Equal(Path.Combine(_home, "trees", "work", "reports"), Path.GetDirectoryName(opened.Path));
        Assert.StartsWith(OpinionTree.Prefix, Path.GetFileName(opened.Path), StringComparison.Ordinal);
        Assert.Equal(_tip, In(opened.Path, "rev-parse HEAD"));
        Assert.Equal("HEAD", In(opened.Path, "rev-parse --abbrev-ref HEAD"));
        Assert.Equal("", In(opened.Path, "remote"));
        Assert.Equal("", In(opened.Path, "branch -r"));
        Assert.True(File.Exists(Path.Combine(opened.Path, "comparison.md")));
        // Its own repository, never a worktree of the person's: the person's lists one working tree, its own.
        Assert.Single(_repository.Output("worktree list --porcelain").Split('\n'), line => line.StartsWith("worktree ", StringComparison.Ordinal));
        Assert.NotEqual(
            Path.GetFullPath(In(opened.Path, "rev-parse --path-format=absolute --git-common-dir")),
            Path.GetFullPath(_repository.Output("rev-parse --path-format=absolute --git-common-dir")));

        Assert.Null(await OpinionTree.RemoveAsync(_home, opened.Path));
        Assert.False(Directory.Exists(opened.Path));
    }

    [Fact]
    public async Task A_ref_written_in_the_copy_never_reaches_the_repository_and_the_copy_is_gone_after()
    {
        var before = Refs();
        var opened = await OpinionTree.OpenAsync(_home, _repository.Root, "reports", "work", _tip);

        // A reviewer that does not follow its instruction: a commit, a branch, a tag, the copy's own line moved, and a push.
        File.WriteAllText(Path.Combine(opened.Path, "written-by-the-reviewer.md"), "x\n");
        Assert.Equal(0, GitFixture.RunLine(opened.Path, $"{Identity} add -A").ExitCode);
        Assert.Equal(0, GitFixture.RunLine(opened.Path, $"{Identity} commit -q -m \"the reviewer wrote\"").ExitCode);
        var written = In(opened.Path, "rev-parse HEAD");
        Assert.Equal(0, GitFixture.RunLine(opened.Path, "branch reviewer-made").ExitCode);
        Assert.Equal(0, GitFixture.RunLine(opened.Path, "tag reviewer-tag").ExitCode);
        Assert.Equal(0, GitFixture.RunLine(opened.Path, $"update-ref refs/heads/main {written}").ExitCode);
        Assert.NotEqual(0, GitFixture.RunLine(opened.Path, "push").ExitCode);
        Assert.NotEqual(0, GitFixture.RunLine(opened.Path, "push origin HEAD").ExitCode);

        Assert.Equal(before, Refs());
        Assert.NotEqual(0, GitFixture.RunLine(_repository.Root, $"cat-file -e {written}^{{commit}}").ExitCode);

        Assert.Null(await OpinionTree.RemoveAsync(_home, opened.Path));
        Assert.False(Directory.Exists(opened.Path));
        Assert.Equal(before, Refs());
    }

    [Fact]
    public async Task A_tip_the_repository_does_not_hold_is_refused_and_leaves_no_folder()
    {
        var error = await Assert.ThrowsAsync<DriverException>(
            () => OpinionTree.OpenAsync(_home, _repository.Root, "reports", "work", new string('9', 40)));

        Assert.Contains("`reports`", error.Message);
        Assert.Empty(Directory.Exists(Path.Combine(_home, "trees", "work", "reports"))
            ? Directory.EnumerateFileSystemEntries(Path.Combine(_home, "trees", "work", "reports"))
            : []);
    }

    // ——— The candidate, as git reads it.

    [Fact]
    public async Task The_candidate_is_read_by_full_ids_its_commits_oldest_first_and_the_paths_its_work_changes()
    {
        var read = await OpinionPackets.ReadAsync(_repository.Root, "reports", "main", "daoris/s-1a2b3c4d");

        Assert.Equal("reports", read.Repository);
        Assert.Equal(_base, read.Base);
        Assert.Equal(_tip, read.Tip);
        Assert.Equal([_middle, _tip], read.Commits);
        Assert.Equal([new OpinionPath("M", "README.md"), new OpinionPath("A", "comparison.md")], read.Paths.OrderByDescending(path => path.Status));
    }

    [Fact]
    public async Task A_candidate_with_no_work_between_or_a_commit_git_does_not_hold_is_refused()
    {
        var same = await Assert.ThrowsAsync<DriverException>(() => OpinionPackets.ReadAsync(_repository.Root, "reports", _tip, _tip));
        Assert.Contains("no work between", same.Message);

        var unknown = await Assert.ThrowsAsync<DriverException>(() => OpinionPackets.ReadAsync(_repository.Root, "reports", _base, "no-such-branch"));
        Assert.Contains("`no-such-branch`", unknown.Message);
    }

    [Fact]
    public async Task The_diff_is_written_whole_into_the_opinion_s_folder()
    {
        var read = await OpinionPackets.ReadAsync(_repository.Root, "reports", _base, _tip);
        var file = Path.Combine(OpinionPackets.Folder(_home, "op1"), OpinionPackets.DiffName);

        Assert.True(await OpinionPackets.WriteDiffAsync(_repository.Root, read, file));

        var diff = File.ReadAllText(file);
        Assert.Contains("+# comparison.md", diff);
        Assert.Contains("+# fixture, read again", diff);
    }

    // ——— The pass, end to end.

    /// <summary>
    /// The pass with no model (§4–§5): the reviewer chosen by the walk, the candidate read, the copy made, the record asked for
    /// with its posture and the rule's minutes, the reviewer run in its copy as its own session, one turn and no words, and the
    /// copy gone after — while a reviewer that writes, commits, branches, tags and pushes there changed nothing of the person's.
    /// </summary>
    [Fact]
    public async Task A_pass_runs_its_reviewer_in_its_copy_and_nothing_it_writes_reaches_the_repository()
    {
        var before = Refs();
        await using var service = OpinionStandIn.Start();
        var config = DriverConfig.Empty with
        {
            Adapter = "claude-code",
            TimeoutMinutes = 2,
            Commands = new Dictionary<string, IReadOnlyList<string>> { ["stub"] = ["node", Agent()] },
        };
        var adapters = AdapterSet.Built();
        var roster = new HarnessRoster(adapters, Path.Combine(_home, "harnesses.json"));
        var driver = new Daoris.Driver.Driver(new ServiceClient(service.Url, null), config, adapters, _home, harnesses: roster);
        var rule = new OpinionRule(["stub"], [OpinionRules.Landing], Minutes: 30);
        var choice = await ReviewerChoice.ChooseAsync(roster, rule, ["claude-code"], config, "work");
        Assert.True(choice.Chosen, choice.Sentence);

        var run = await driver.PassAsync(
            new OpinionPassAsk(OpinionRules.Landing, "w1", "reports", _repository.Root, _base, "daoris/s-1a2b3c4d", rule) { Workspace = "work" },
            choice);

        Assert.True(run.Opened, run.Line);
        Assert.Equal("op1", run.Opinion);
        Assert.Equal("r1", run.Session);
        Assert.Equal("completed", run.State);
        Assert.Equal(OpinionPass.TierAgent, run.Tier);
        Assert.Equal(OpinionPosture.CopyAlone, run.Posture);
        Assert.Null(run.TreeLeft);

        // What the record was asked for: the candidate by full ids, the reviewer as the walk chose it, the work's families by
        // owner, what held it, the rule's minutes, and its own tree.
        var asked = Assert.Single(service.Asked);
        Assert.Equal("landing", asked["occasion"]!.GetValue<string>());
        Assert.Equal("first", asked["pass"]!.GetValue<string>());
        Assert.Equal("w1", asked["working"]!.GetValue<string>());
        Assert.Equal("reports", asked["candidate"]!["repository"]!.GetValue<string>());
        Assert.Equal(_base, asked["candidate"]!["base"]!.GetValue<string>());
        Assert.Equal(_tip, asked["candidate"]!["tip"]!.GetValue<string>());
        Assert.Equal([_middle, _tip], asked["candidate"]!["commits"]!.AsArray().Select(commit => commit!.GetValue<string>()));
        Assert.Equal("stub", asked["reviewer"]!["adapter"]!.GetValue<string>());
        Assert.Equal(ReviewerLabels.MakerNotDeclared, asked["reviewer"]!["label"]!.GetValue<string>());
        Assert.Equal(["claude-code"], asked["families"]!.AsArray().Select(family => family!.GetValue<string>()));
        Assert.Equal(OpinionPosture.CopyAlone, asked["posture"]!.GetValue<string>());
        Assert.Equal(30, asked["minutes"]!.GetValue<int>());
        var tree = asked["tree"]!.GetValue<string>();
        Assert.Equal(Path.Combine(_home, "trees", "work", "reports"), Path.GetDirectoryName(tree));
        Assert.Equal("stub-harness 1.0.0", asked["harnessVersion"]!.GetValue<string>());

        // It ran in its copy as its own session, on no quest, handed the reviewer's instruction.
        var saw = JsonNode.Parse(File.ReadAllText(Path.Combine(_home, "saw.json")))!;
        Assert.Equal(Path.GetFullPath(tree).TrimEnd('\\', '/'), Path.GetFullPath(saw["cwd"]!.GetValue<string>()).TrimEnd('\\', '/'), ignoreCase: true);
        Assert.Equal("r1", saw["session"]!.GetValue<string>());
        Assert.Null(saw["quest"]);
        Assert.Contains("second opinion", saw["target"]!.GetValue<string>());
        Assert.Contains(_tip, saw["target"]!.GetValue<string>());
        Assert.Equal(["starting", "working", "completed"], service.States("r1"));

        // The copy is gone, nothing it wrote reached the repository, and the diff it was handed stays with the opinion.
        Assert.False(Directory.Exists(tree));
        Assert.Equal(before, Refs());
        Assert.Contains("stub: wrote and committed in its copy", File.ReadAllText(Path.Combine(_home, "sessions", "r1.log")));
        Assert.True(File.Exists(Path.Combine(OpinionPackets.Folder(_home, "op1"), OpinionPackets.DiffName)));
    }

    [Fact]
    public async Task A_record_the_service_refuses_starts_nothing_and_leaves_no_copy()
    {
        await using var service = OpinionStandIn.Start(refuse: true);
        var config = DriverConfig.Empty with
        {
            Adapter = "claude-code",
            Commands = new Dictionary<string, IReadOnlyList<string>> { ["stub"] = ["node", Agent()] },
        };
        var adapters = AdapterSet.Built();
        var driver = new Daoris.Driver.Driver(new ServiceClient(service.Url, null), config, adapters, _home);
        var choice = new ReviewerChoice("stub", ReviewerLabels.MakerNotDeclared, ReviewerUnavailable.NotIndependent, "x")
        {
            Selection = new HarnessSelection(null),
            Family = AgentFamily.Of(adapters, "stub"),
        };

        var run = await driver.PassAsync(
            new OpinionPassAsk(OpinionRules.Landing, "w1", "reports", _repository.Root, _base, _tip, new OpinionRule(["stub"], [OpinionRules.Landing])),
            choice);

        Assert.False(run.Opened);
        Assert.Contains("a pass reads the work of a session in the candidate's repository", run.Line);
        Assert.False(File.Exists(Path.Combine(_home, "saw.json")));
        Assert.Empty(Directory.EnumerateDirectories(Path.Combine(_home, "trees", "default", "reports")));
    }

    /// <summary>
    /// The stub reviewer: a harness with real mechanics and no model. It says what it saw, then does everything its instruction
    /// tells it never to — writes, commits, branches, tags and pushes in its copy — so the copy is what holds.
    /// </summary>
    private string Agent()
    {
        var script = Path.Combine(_home, "reviewer-agent.mjs");
        File.WriteAllText(script, """
            import { writeFileSync } from 'node:fs';
            import { execFileSync } from 'node:child_process';
            import { dirname, join } from 'node:path';
            import { fileURLToPath } from 'node:url';
            if (process.argv.includes('--version')) { console.log('stub-harness 1.0.0'); process.exit(0); }
            if (process.argv.includes('--login-state')) { console.log('logged-in'); process.exit(0); }

            const saw = {
              cwd: process.cwd(),
              session: process.env.DAORIS_SESSION_ID ?? null,
              quest: process.env.DAORIS_QUEST_ID ?? null,
              target: process.env.DAORIS_TARGET ?? '',
            };
            writeFileSync(join(dirname(fileURLToPath(import.meta.url)), 'saw.json'), JSON.stringify(saw));

            const git = (...args) => execFileSync('git', ['-c', 'user.name=r', '-c', 'user.email=r@example.invalid', ...args], { stdio: 'pipe' });
            writeFileSync('written-by-the-reviewer.md', 'x\n');
            git('add', '-A');
            git('commit', '-q', '-m', 'the reviewer wrote');
            git('branch', 'reviewer-made');
            git('tag', 'reviewer-tag');
            console.log('stub: wrote and committed in its copy');
            try { git('push'); console.log('stub: pushed'); } catch { console.log('stub: nowhere to push'); }
            """);
        return script;
    }

    /// <summary>
    /// The service's doors a pass crosses (XAGENT1c's <c>POST /api/opinions</c> and the ledger's state door), on a real loopback
    /// listener, because the stub reviewer is a real process. It opens the reviewer's record as the desk would, or refuses it in
    /// the desk's words, and remembers what it was asked.
    /// </summary>
    private sealed class OpinionStandIn : IAsyncDisposable
    {
        private readonly HttpListener _listener;
        private readonly Task _serving;
        private readonly bool _refuse;
        private readonly Dictionary<string, List<string>> _states = [];

        public string Url { get; }

        public List<JsonObject> Asked { get; } = [];

        private OpinionStandIn(HttpListener listener, string url, bool refuse)
        {
            _listener = listener;
            Url = url.TrimEnd('/');
            _refuse = refuse;
            _serving = ServeAsync();
        }

        public static OpinionStandIn Start(bool refuse = false)
        {
            var probe = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
            probe.Start();
            var port = ((IPEndPoint)probe.LocalEndpoint).Port;
            probe.Stop();

            var url = $"http://127.0.0.1:{port}/";
            var listener = new HttpListener();
            listener.Prefixes.Add(url);
            listener.Start();
            return new OpinionStandIn(listener, url, refuse);
        }

        public IReadOnlyList<string> States(string session)
        {
            lock (_states) return _states.TryGetValue(session, out var moved) ? [.. moved] : [];
        }

        private async Task ServeAsync()
        {
            while (_listener.IsListening)
            {
                HttpListenerContext context;
                try { context = await _listener.GetContextAsync(); }
                catch (Exception error) when (error is HttpListenerException or ObjectDisposedException or InvalidOperationException) { return; }

                var (status, body) = Answer(context.Request);
                var bytes = Encoding.UTF8.GetBytes(body);
                context.Response.StatusCode = status;
                context.Response.ContentType = "application/json";
                await context.Response.OutputStream.WriteAsync(bytes);
                context.Response.Close();
            }
        }

        private (int, string) Answer(HttpListenerRequest request)
        {
            var path = request.Url!.AbsolutePath;
            JsonObject Body()
            {
                using var reader = new StreamReader(request.InputStream, Encoding.UTF8);
                return JsonNode.Parse(reader.ReadToEnd())!.AsObject();
            }

            lock (_states)
            {
                switch (request.HttpMethod, path)
                {
                    case ("POST", "/api/opinions"):
                    {
                        var body = Body();
                        Asked.Add(body);
                        if (_refuse)
                        {
                            return (400, """{"error":"Session `w1` worked in `notes`, and the candidate is in `reports`: a pass reads the work of a session in the candidate's repository."}""");
                        }

                        var session = new JsonObject
                        {
                            ["id"] = "r1", ["repository"] = "reports", ["state"] = "queued", ["kind"] = "chat", ["adapter"] = "stub",
                            ["workspace"] = "work", ["opinion"] = "op1", ["tree"] = body["tree"]!.GetValue<string>(),
                        };
                        _states["r1"] = [];
                        return (200, new JsonObject
                        {
                            ["opinion"] = new JsonObject { ["id"] = "op1", ["session"] = "r1", ["state"] = "reading" },
                            ["message"] = "Second opinion `op1` (first pass) asked of `stub`: session `r1` reads `reports` in a clone of its own.",
                            ["session"] = session,
                        }.ToJsonString());
                    }

                    case ("POST", _) when path.StartsWith("/api/sessions/", StringComparison.Ordinal) && path.EndsWith("/state", StringComparison.Ordinal):
                    {
                        var id = path["/api/sessions/".Length..^"/state".Length];
                        var state = Body()["state"]!.GetValue<string>();
                        if (!_states.TryGetValue(id, out var moved)) return (404, """{"error":"no such session"}""");
                        moved.Add(state);
                        return (200, new JsonObject
                        {
                            ["session"] = new JsonObject { ["id"] = id, ["state"] = state }, ["message"] = "moved",
                        }.ToJsonString());
                    }

                    default:
                        return (404, $$"""{"error":"the stand-in has no {{request.HttpMethod}} {{path}}"}""");
                }
            }
        }

        public async ValueTask DisposeAsync()
        {
            _listener.Stop();
            _listener.Close();
            try { await _serving; } catch (ObjectDisposedException) { }
        }
    }
}
