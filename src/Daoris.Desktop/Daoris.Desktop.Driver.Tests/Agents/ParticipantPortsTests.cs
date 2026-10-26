using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

public sealed class ParticipantPortsTests : IDisposable
{
    private readonly string _home = Path.Combine(GitSeed.RepoRoot(), "_fixtures", "participant-ports", Guid.NewGuid().ToString("N")[..8]);

    public ParticipantPortsTests() => Directory.CreateDirectory(_home);
    public void Dispose() => GitSeed.Discard(_home);

    [Fact]
    public async Task The_installed_watcher_keeps_parents_unknowns_and_per_run_state_through_capture()
    {
        Assert.IsType<ParticipantLineageStreams>(LineageStreams.Current);
        const string lines = """
            {"type":"system","subtype":"task_started","task_id":"a1","tool_use_id":"t1","spawn_depth":1,"task_type":"local_agent"}
            {"type":"system","subtype":"task_started","task_id":"a2","tool_use_id":"t2","spawn_depth":2,"parent_task_id":"a1","task_type":"local_agent"}
            """;
        await Capture(lines, "s1", _home);
        var children = ChildLineage.Read(_home, "s1").Children;
        Assert.Equal([("a1", (string?)null), ("a2", "a1")], children.Select(child => (child.Native, child.Parent)));
        Assert.All(children, child => Assert.Equal(ChildBindings.Active, child.State));
        ChildLineage.End(_home, "s1", clean: false, DateTimeOffset.UnixEpoch);
        Assert.All(ChildLineage.Read(_home, "s1").Children, child => Assert.Equal(ChildBindings.Lost, child.State));
        await Capture(lines, "s2", _home);
        Assert.Equal(2, ChildLineage.Read(_home, "s2").Children.Count);
        await Capture("{\"type\":\"foreign\"}", "s3", _home);
        Assert.Empty(ChildLineage.Read(_home, "s3").Children);
        Assert.Null(ChildLineage.HookFor(_home, "../unknown"));
    }

    [Fact]
    public async Task An_unrelated_watcher_gets_each_line_before_mapping_and_none_after_capture()
    {
        var actual = LineageStreams.Current;
        var stand = new Streams();
        try
        {
            LineageStreams.Current = stand;
            await Capture("first\nsecond", "s1", _home);
            Assert.Equal(["watch:s1", "first", "second"], stand.Calls);
            await Capture("unwatched", "s2", null);
            Assert.Equal(3, stand.Calls.Count);
            LineageStreams.Current = null!;
            await Assert.ThrowsAsync<InvalidOperationException>(() => Capture("never", "s3", _home));
            await Capture("no lineage requested", "s4", null);
            LineageStreams.Current = new RefusedStreams();
            var refused = await Assert.ThrowsAsync<IOException>(() => Capture("never", "s5", _home));
            Assert.Equal("watch refused", refused.Message);
        }
        finally { LineageStreams.Current = actual; }
        Assert.Same(actual, LineageStreams.Current);
    }

    [Fact]
    public void An_unrelated_access_fact_drives_native_arguments_and_the_higher_setup_judgement()
    {
        var adapter = AdapterSet.Built().Resolve("claude-code");
        IAgentAccess facts = new Access();
        var target = new SessionTarget("q1", "Title", "Body", "person", "engine", _home, "http://localhost") { Access = facts };
        var args = adapter.Prepare(target, null).ArgumentList.ToList();
        Assert.Equal("stand-mode", args[args.IndexOf("--permission-mode") + 1]);
        Assert.Null(facts.ChangedIn(DriverConfig.Parse("{}"), adapter));
        Assert.Null(facts.SwapIn(DriverConfig.Parse("{}"), adapter));
        Assert.Equal("stand refused", target.Access.Unoffered("unknown", "none"));
    }

    private Task Capture(string lines, string session, string? home) => HarnessCapture.CaptureStructuredAsync(
        new StringReader(lines), new StringReader(""), Path.Combine(_home, session + ".log"), session,
        output: null, events: null, new ClaudeStreamJson(), prompt: null, CancellationToken.None, lineageHome: home);

    private sealed class Streams : ILineageStreams
    {
        public List<string> Calls { get; } = [];
        public Action<string> Watch(string home, string sessionId)
        {
            Calls.Add("watch:" + sessionId);
            return Calls.Add;
        }
    }

    private sealed class Access : IAgentAccess
    {
        public string Permissions => "auto";
        public string Repository => "engine";
        public string? Workspace => null;
        public string? PermissionMode => "stand-mode";
        public string? Posture => null;
        public string Unoffered(string named, string offers) => "stand refused";
    }

    private sealed class RefusedStreams : ILineageStreams
    {
        public Action<string> Watch(string home, string sessionId) => throw new IOException("watch refused");
    }
}
