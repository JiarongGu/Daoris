using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

[Collection("agent operation ports")]
public sealed class ParticipantPortsTests : IDisposable
{
    private readonly string _home = Path.Combine(GitSeed.RepoRoot(), "_fixtures", "participant-ports", Guid.NewGuid().ToString("N")[..8]);

    public ParticipantPortsTests() => Directory.CreateDirectory(_home);
    public void Dispose() => GitSeed.Discard(_home);

    private const string Family = """
        {"type":"system","subtype":"task_started","task_id":"a1","tool_use_id":"t1","spawn_depth":1,"task_type":"local_agent"}
        {"type":"system","subtype":"task_started","task_id":"a2","tool_use_id":"t2","spawn_depth":2,"parent_task_id":"a1","task_type":"local_agent"}
        """;

    [Fact]
    public async Task The_lineage_part_keeps_parents_unknowns_and_per_run_state_through_capture()
    {
        const string lines = Family;
        await Capture(lines, "s1", _home);
        var children = ChildLineage.Read(_home, "s1").Children;
        Assert.Equal([("a1", (string?)null), ("a2", "a1")], children.Select(child => (child.Native, child.Parent)));
        // No process bracketed them: history, its process's exit proven by nothing, and a driven record's end claims no boundary.
        Assert.All(children, child => Assert.Equal(ChildBindings.Uncertain, child.State));
        ChildLineage.End(_home, "s1", clean: false, DateTimeOffset.UnixEpoch);
        Assert.All(ChildLineage.Read(_home, "s1").Children, child => Assert.Equal(ChildBindings.Unrecorded, child.State));
        await Capture(lines, "s2", _home);
        Assert.Equal(2, ChildLineage.Read(_home, "s2").Children.Count);
        await Capture("{\"type\":\"foreign\"}", "s3", _home);
        Assert.Empty(ChildLineage.Read(_home, "s3").Children);
        Assert.Null(ClaudeLineageHook.For(_home, "../unknown", ChildLineage.PathOf(_home, "unknown")));
    }

    /// <summary>No transport names the lineage: the capture says each child as a reading, and a sink with no lineage part writes none.</summary>
    [Fact]
    public async Task A_run_whose_sink_has_no_lineage_part_says_its_children_and_writes_no_lineage()
    {
        var seen = new List<WireReading.ChildSeen>();

        await Capture(Family, "s1", null, ReadingSinks.On<WireReading.ChildSeen>(seen.Add));

        Assert.Equal([("a1", (string?)null), ("a2", "a1")], seen.Select(child => (child.Child, child.Parent)));
        Assert.False(File.Exists(ChildLineage.PathOf(_home, "s1")));
    }

    [Fact]
    public void An_unrelated_access_fact_drives_native_arguments_and_the_higher_setup_judgement()
    {
        var adapter = ProviderRegistry.Built().Door("claude-code");
        IAgentAccess facts = new Access();
        var target = new SessionTarget("q1", "Title", "Body", "person", "engine", _home, "http://localhost") { Access = facts };
        var args = ProviderRegistry.Built().Start(adapter, target, null).ArgumentList.ToList();
        Assert.Equal("stand-mode", args[args.IndexOf("--permission-mode") + 1]);
        Assert.Null(facts.ChangedIn(DriverConfig.Parse("{}"), adapter));
        Assert.Null(facts.SwapIn(DriverConfig.Parse("{}"), adapter));
        Assert.Equal("stand refused", target.Access.Unoffered("unknown", "none"));
    }

    /// <summary>The native capture, its sink the part's lineage where <paramref name="home"/> is given, beside <paramref name="beside"/>.</summary>
    private Task Capture(string lines, string session, string? home, IReadingSink? beside = null) => HarnessCapture.CaptureStructuredAsync(
        new StringReader(lines), new StringReader(""), Path.Combine(_home, session + ".log"),
        ReadingSinks.Of(beside, home is null ? null : new LineageReadings(home, session)), new ClaudeStreamJson(), prompt: null, CancellationToken.None);

    private sealed class Access : IAgentAccess
    {
        public string Permissions => "auto";
        public string Repository => "engine";
        public string? Workspace => null;
        public string? PermissionMode => "stand-mode";
        public string? Posture => null;
        public string Unoffered(string named, string offers) => "stand refused";
    }
}
