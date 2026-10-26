using System.Diagnostics;
using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

public sealed class AgentWorkplacePortsTests : IDisposable
{
    private readonly string _home = Path.Combine(GitSeed.RepoRoot(), "_fixtures", "agent-workplace", Guid.NewGuid().ToString("N")[..8]);
    public AgentWorkplacePortsTests() => Directory.CreateDirectory(_home);
    public void Dispose() => GitSeed.Discard(_home);
    private SessionTarget Target() => new("q1", "Title", "Body", "person", "engine", _home, "http://localhost") { Home = _home };

    [Fact]
    public void Actual_plans_keep_their_graph_and_serialized_properties_through_start_reads()
    {
        var plan = new LandingPlan(LandingForm.Branch, "review/one", "repository", AutoAccept: true)
        {
            Own = true, Stands = true,
            LeftOut = [new LeftOutCommit("abc", "Already there", LeftOutWhy.Patch, "def")],
            Lands = [new LandingCommit("ghi", "New work")],
            Conflict = new LandingConflict("conflict", "replay these"),
        };
        var commits = new ResolvedCommits(new CommitRule("ask", "Keep the ticket."), "repository");
        var target = Target() with { LandsOn = plan, Commits = commits };
        Assert.Same(plan, target.LandsOn);
        Assert.Same(plan.LeftOut, target.LandsOn.LeftOut);
        Assert.Same(plan.Lands, target.LandsOn.Lands);
        Assert.Same(plan.Conflict, target.LandsOn.Conflict);
        var json = JsonSerializer.SerializeToElement(target);
        Assert.Equal(JsonSerializer.Serialize(plan), json.GetProperty("LandsOn").GetRawText());
        Assert.Equal(JsonSerializer.Serialize(commits), json.GetProperty("Commits").GetRawText());
        Assert.Equal(ProcessWords.Said(commits, plan), AgentWorkplace.Current.ProcessWords(target.Commits, target.LandsOn));
        var bare = JsonSerializer.SerializeToElement(Target());
        Assert.Equal(JsonValueKind.Null, bare.GetProperty("LandsOn").ValueKind);
        Assert.Equal(JsonValueKind.Null, bare.GetProperty("Commits").ValueKind);
    }

    [Fact]
    public void Actual_git_preparation_keeps_token_removal_scope_and_the_no_identity_no_git_bound()
    {
        Assert.IsType<WorkingAgentWorkplace>(AgentWorkplace.Current);
        var actual = new Dictionary<string, string?> { ["GH_TOKEN"] = "stand-token", ["GIT_CONFIG_COUNT"] = "1", ["GIT_CONFIG_KEY_0"] = "user.name", ["GIT_CONFIG_VALUE_0"] = "kept" };
        var expected = new Dictionary<string, string?>(actual);
        GitWiring.Untoken(expected);
        GitAuthor.Hand(expected, null);
        GitLongPaths.Hand(expected, true);
        var gits = 0;
        WorkingTree.Started.Value = _ => Interlocked.Increment(ref gits);
        try { AgentWorkplace.Current.HandGit(actual, _home, _home); }
        finally { WorkingTree.Started.Value = null; }
        Assert.Equal(0, gits);
        Assert.Equal(expected.OrderBy(pair => pair.Key), actual.OrderBy(pair => pair.Key));
        Assert.Equal("user.name", actual["GIT_CONFIG_KEY_0"]);
        Assert.Equal("core.longpaths", actual["GIT_CONFIG_KEY_1"]);
    }

    [Fact]
    public void An_independent_workplace_observes_tools_first_and_receives_the_original_facts()
    {
        var actual = AgentWorkplace.Current;
        var git = ChildGit.Current;
        var calls = new List<string>();
        var stand = new Stand(calls);
        var plan = new Plan();
        var commits = new Commits();
        try
        {
            ChildGit.Current = new ToolsFirst(calls);
            AgentWorkplace.Current = stand;
            var target = Target() with { LandsOn = plan, Commits = commits };
            var info = Spawning.InRoot(target, "stand-command", []);
            Assert.Equal(["tools", "git", "words"], calls);
            Assert.Equal("present", info.Environment["stand-workplace"]);
            Assert.Equal((_home, _home), stand.Git);
            Assert.Same(plan, stand.Plan);
            Assert.Same(commits, stand.Commits);
            Assert.Contains("stand process section", info.Environment["DAORIS_TARGET"]);
            // The actual composer also reads an unrelated fact implementation, without a concrete-plan cast.
            Assert.Contains("review/stand", actual.ProcessWords(commits, plan));
        }
        finally { AgentWorkplace.Current = actual; ChildGit.Current = git; }
    }

    [Fact]
    public void Missing_installation_and_refused_or_cancelled_operations_are_never_silenced()
    {
        var actual = AgentWorkplace.Current;
        try
        {
            AgentWorkplace.Current = null!;
            Assert.Throws<InvalidOperationException>(() => Spawning.InRoot(Target(), "stand", []));
            Assert.Throws<InvalidOperationException>(() => TargetPrompt.Compose(Target()));
            foreach (var failure in new Exception[] { new IOException("refused"), new OperationCanceledException(new CancellationToken(true)) })
            {
                AgentWorkplace.Current = new Stand([]) { Failure = failure };
                Assert.Same(failure, Assert.Throws(failure.GetType(), () => Spawning.InRoot(Target(), "stand", [])));
                Assert.Same(failure, Assert.Throws(failure.GetType(), () => TargetPrompt.Compose(Target())));
            }
        }
        finally { AgentWorkplace.Current = actual; }
    }

    [Fact]
    public void The_real_guard_installs_its_resource_and_keeps_exact_hook_order_arguments_and_refusal()
    {
        var node = Path.Combine(_home, "stand-node.exe");
        AtomicFile.WriteText(node, "stand-in, never executed");
        Tools.UseFile(_home, "node", node);
        var guard = TreeGuard.For(_home, _home, [Path.Combine(_home, "also")]);
        Assert.Equal(TreeGuard.Source, File.ReadAllText(guard.Script));
        Assert.Equal(node, guard.Node);
        var file = SpawnSettings.Write(_home, "s1", RuleLists.Empty, [], guard, new LineageHook("lineage-script", "lineage-file"));
        using var document = JsonDocument.Parse(File.ReadAllText(file!));
        var hooks = document.RootElement.GetProperty("hooks");
        Assert.Equal(["PreToolUse", "SubagentStart", "SubagentStop"], hooks.EnumerateObject().Select(pair => pair.Name));
        var before = hooks.GetProperty("PreToolUse").EnumerateArray().ToList();
        Assert.Equal(["Edit|Write|MultiEdit|NotebookEdit", "mcp__daoris-knowledge__.*"], before.Select(entry => entry.GetProperty("matcher").GetString()));
        var command = Assert.Single(before[0].GetProperty("hooks").EnumerateArray());
        Assert.Equal(["type", "command", "args", "timeout"], command.EnumerateObject().Select(pair => pair.Name));
        Assert.Equal([guard.Script, guard.Tree, .. guard.Also], command.GetProperty("args").EnumerateArray().Select(arg => arg.GetString()));
        Assert.Equal(30, command.GetProperty("timeout").GetInt32());
        SpawnSettings.Remove(file);
        Assert.False(File.Exists(file));
        File.Delete(node);
        Assert.Contains("the tree guard runs on Node.js", Assert.Throws<DriverException>(() => TreeGuard.For(_home, _home)).Message);
        Assert.Equal(TreeLock.Replaying("engine"), HeldWords.Replaying("engine").Why);
    }

    private sealed class ToolsFirst(List<string> calls) : IChildGit
    {
        public void Hand(ProcessStartInfo info, string? home) { calls.Add("tools"); info.Environment["stand-tools"] = "present"; }
    }
    private sealed class Stand(List<string> calls) : IAgentWorkplace
    {
        public Exception? Failure { get; init; }
        public (string?, string)? Git { get; private set; }
        public IStartLanding? Plan { get; private set; }
        public IStartCommits? Commits { get; private set; }
        public void HandGit(IDictionary<string, string?> environment, string? home, string root)
        {
            if (Failure is { } failure) throw failure;
            Assert.Equal("present", environment["stand-tools"]);
            calls.Add("git"); Git = (home, root); environment["stand-workplace"] = "present";
        }
        public string? ProcessWords(IStartCommits? commits, IStartLanding? plan)
        {
            if (Failure is { } failure) throw failure;
            calls.Add("words"); Plan = plan; Commits = commits; return "stand process section";
        }
    }
    private sealed class Plan : IStartLanding
    {
        public string Form => "branch";
        public string Target => "review/stand";
        public string Source => "repository";
        public string? Plugin => null;
        public string? Problem => null;
        public bool AutoAccept => false;
        public bool Own => false;
        public bool Stands => false;
        public object? LeftOut => null;
        public object? Lands => null;
        public object? Conflict => null;
    }
    private sealed class Commits : IStartCommits
    {
        public CommitRule Rule => CommitRule.Default;
        public string Source => "default";
    }
}
