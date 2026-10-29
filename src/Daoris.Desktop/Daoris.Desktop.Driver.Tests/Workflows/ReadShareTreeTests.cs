using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// READSHARE1: each reader of a record AtomicFile moves into place reads it while the move still holds it (MoveHold).
/// </summary>
/// <remarks>
/// Each record is written by the product's own writer, or with <see cref="AtomicFile.WriteText"/> at its reader's path, then held as
/// Windows' move holds it: a plain read is refused there, and the product's reader still reads what the record says.
/// </remarks>
public sealed class ReadShareTreeTests : IDisposable
{
    private const string Unit = "quest:q-17";

    private static readonly DateTimeOffset At = new(2026, 10, 23, 9, 0, 0, TimeSpan.Zero);

    /// <summary>A closure graph of one node, the close (FABRIC-CLOSE1l).</summary>
    private const string Closing = """
        {"schemaVersion":2,"entries":["close"],"end":"close",
         "nodes":[{"id":"close","kind":"quest.close","version":1,"reach":"performed"}],"wires":[]}
        """;

    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-readshare-trees-" + Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    /// <summary>The records under the test's home, written as the loop holding its lease writes them.</summary>
    private NodeRecordFiles Nodes() => new(_home, () => true);

    private ResourceRecordFiles Resources() => new(_home, () => true);

    /// <summary>A service that answers nothing: the trace's reads of it are said unread, and this machine's are read as ever.</summary>
    private sealed class Unanswering : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            throw new HttpRequestException("the stand-in answers nothing");
    }

    /// <summary>READSHARE1: <see cref="NodeRecordFiles.Read"/> reads a unit's node record while its move into place still holds it.</summary>
    [Fact]
    public void A_node_record_is_read_while_its_move_into_place_still_holds_it()
    {
        if (!OperatingSystem.IsWindows()) return;
        Nodes().Write(new NodeRecord(Unit) { Fired = ["trg-1"] });
        var path = NodeRecordFiles.PathOf(_home, Unit);

        using (MoveHold.Take(path))
        {
            Assert.Throws<IOException>(() => File.ReadAllText(path));
            Assert.Equal(["trg-1"], Nodes().Read(Unit)!.Fired);
        }
    }

    /// <summary>READSHARE1: <see cref="NodeRecordFiles.All"/> lists a node record its move into place still holds, never as unreadable.</summary>
    [Fact]
    public void Every_node_record_is_listed_while_a_move_into_place_still_holds_one()
    {
        if (!OperatingSystem.IsWindows()) return;
        Nodes().Write(new NodeRecord(Unit) { Fired = ["trg-1"] });
        var path = NodeRecordFiles.PathOf(_home, Unit);

        using (MoveHold.Take(path))
        {
            Assert.Throws<IOException>(() => File.ReadAllText(path));
            var (records, unreadable) = Nodes().All();
            Assert.Equal([(Unit, "trg-1")], records.Select(each => (each.Unit, each.Fired.Single())));
            Assert.Empty(unreadable);
        }
    }

    /// <summary>
    /// READSHARE1: <see cref="NodeRecordFiles.Unread"/> (its <c>UnitNamed</c>) still reads the unit a record that does not read names,
    /// while its move into place holds it.
    /// </summary>
    [Fact]
    public void A_node_record_that_does_not_read_still_names_its_unit_while_its_move_into_place_holds_it()
    {
        if (!OperatingSystem.IsWindows()) return;
        Directory.CreateDirectory(NodeRecordFiles.FolderOf(_home));
        var path = NodeRecordFiles.PathOf(_home, Unit);
        AtomicFile.WriteText(path, "{ \"unit\": \"quest:q-17\", \"attempts\": [ { \"node\": 1 } ] }\n");

        using (MoveHold.Take(path))
        {
            Assert.Throws<IOException>(() => File.ReadAllText(path));
            var unread = Assert.Single(Nodes().Unread());
            Assert.Equal((NodeRecordFiles.KeyOf(Unit) + ".json", (string?)Unit), (unread.File, unread.Unit));
        }
    }

    /// <summary>READSHARE1: <see cref="ResourceRecordFiles.Read"/> reads a unit's resource record while its move into place still holds it.</summary>
    [Fact]
    public void A_resource_record_is_read_while_its_move_into_place_still_holds_it()
    {
        if (!OperatingSystem.IsWindows()) return;
        Resources().Write(Receipted());
        var path = ResourceRecordFiles.PathOf(_home, Unit);

        using (MoveHold.Take(path))
        {
            Assert.Throws<IOException>(() => File.ReadAllText(path));
            Assert.Equal(["p-1"], Resources().Read(Unit)!.Receipts.Select(each => each.Resource));
        }
    }

    /// <summary>READSHARE1: <see cref="ResourceRecordFiles.All"/> lists a resource record its move into place still holds, never as unreadable.</summary>
    [Fact]
    public void Every_resource_record_is_listed_while_a_move_into_place_still_holds_one()
    {
        if (!OperatingSystem.IsWindows()) return;
        Resources().Write(Receipted());
        var path = ResourceRecordFiles.PathOf(_home, Unit);

        using (MoveHold.Take(path))
        {
            Assert.Throws<IOException>(() => File.ReadAllText(path));
            var (records, unreadable) = Resources().All();
            Assert.Equal([(Unit, "p-1")], records.Select(each => (each.Unit, each.Receipts.Single().Resource)));
            Assert.Empty(unreadable);
        }
    }

    private static ResourceRecord Receipted() => new(Unit)
    {
        Receipts = [new("r-1", "acme.deploy", "acme.deploy/preview@1", "p-1", PluginResourceRoles.Temporary, "deploy", 1, "i-1", At)],
    };

    /// <summary>READSHARE1: <see cref="ClosureGraphs.Read"/> reads a kept closure graph while its move into place still holds it.</summary>
    [Fact]
    public void A_kept_closure_graph_is_read_while_its_move_into_place_still_holds_it()
    {
        if (!OperatingSystem.IsWindows()) return;
        var digest = NodeGraphs.Digest(NodeGraphs.Read(Closing).Graph!);
        ClosureGraphs.Keep(_home, digest, Closing);
        var path = Path.Combine(ClosureGraphs.FolderOf(_home), digest + ".json");
        Assert.True(File.Exists(path));

        using (MoveHold.Take(path))
        {
            Assert.Throws<IOException>(() => File.ReadAllText(path));
            Assert.Equal(digest, NodeGraphs.Digest(ClosureGraphs.Read(_home, digest)!));
        }
    }

    /// <summary>READSHARE1: <see cref="QuestReads.Read"/> reads the kept read of the quests while its move into place still holds it.</summary>
    [Fact]
    public void The_kept_read_of_the_quests_is_read_while_its_move_into_place_still_holds_it()
    {
        if (!OperatingSystem.IsWindows()) return;
        QuestReads.Keep(_home, new Dictionary<string, QuestView>
        {
            ["q7"] = new("q7", "game", "engine", "Ship q7", "", "Taken") { Submission = new QuestSubmissionView("desk", 41, null, "ready") },
            ["q8"] = new("q8", "game", "engine", "Ship q8", "", "Open"),
        });
        var path = QuestReads.PathOf(_home);

        using (MoveHold.Take(path))
        {
            Assert.Throws<IOException>(() => File.ReadAllText(path));
            var read = QuestReads.Read(_home)!;
            Assert.Equal(("desk", 41L), (read["q7"].Submission!.Machine, read["q7"].Submission!.Sequence));
            Assert.Null(read["q8"].Submission);
        }
    }

    /// <summary>READSHARE1: <see cref="SlimManifest.Read"/> reads a set-aside folder's manifest while its move into place still holds it.</summary>
    [Fact]
    public void A_slim_manifest_is_read_while_its_move_into_place_still_holds_it()
    {
        if (!OperatingSystem.IsWindows()) return;
        var folder = DaorisHome.Entry(_home, HomeEntry.Trees, "engine", "default", "q1-20261001T090000000Z");
        Directory.CreateDirectory(Path.GetDirectoryName(folder)!);
        var path = SlimManifest.PathFor(folder);
        new SlimManifest(SlimStates.Moved, At, At.AddMinutes(1), "engine", "daoris/q1", [new SlimPath("web/node_modules", SlimStates.Pending)]).Write(path);

        using (MoveHold.Take(path))
        {
            Assert.Throws<IOException>(() => File.ReadAllText(path));
            var (manifest, problem) = SlimManifest.Read(path);
            Assert.Null(problem);
            Assert.Equal((SlimStates.Moved, "engine", "daoris/q1"), (manifest!.State, manifest.Repository, manifest.Branch));
            Assert.Equal(["web/node_modules"], manifest.Paths.Select(each => each.Path));
        }
    }

    /// <summary>
    /// READSHARE1: the purge's record of the folders it could not delete (<see cref="TreeSetAside.Failed"/>, its <c>FailedEntries</c>, and
    /// the purge's write-if-changed check) is read while its move into place still holds it: kept, never dropped as unread.
    /// </summary>
    [Fact]
    public void The_purge_s_failed_folders_are_read_and_kept_while_their_record_s_move_into_place_still_holds_it()
    {
        if (!OperatingSystem.IsWindows()) return;
        var aside = new TreeSetAside(new TreeContext(_home, new LandingPlugins(_home)));
        var folder = Path.Combine(aside.SetAsideRoot, "work", "engine", "q1-20261001T090000000Z");
        Directory.CreateDirectory(folder);
        const string why = "it could not be read or deleted (a file in it was held open)";
        AtomicFile.WriteText(aside.FailedPath, $$"""{"folders":[{"folder":"work/engine/q1-20261001T090000000Z","why":"{{why}}"}]}""");
        // A pass that meets no folder keeps what the last said of each, written as the purge writes it.
        aside.PurgeSetAside(At, TimeSpan.FromDays(1), () => false, out _, only: _ => false);
        var written = File.ReadAllText(aside.FailedPath);
        File.SetLastWriteTimeUtc(aside.FailedPath, At.UtcDateTime);

        using (MoveHold.Take(aside.FailedPath))
        {
            Assert.Throws<IOException>(() => File.ReadAllText(aside.FailedPath));
            Assert.Equal(why, aside.Failed()[Path.GetFullPath(folder)]);
            Assert.Empty(aside.PurgeSetAside(At, TimeSpan.FromDays(1), () => false, out _, only: _ => false));
        }

        // Read and unchanged: neither dropped as a record that said nothing, nor written again.
        Assert.Equal(written, File.ReadAllText(aside.FailedPath));
        Assert.Equal(At.UtcDateTime, File.GetLastWriteTimeUtc(aside.FailedPath));
        Assert.Equal(why, aside.Failed()[Path.GetFullPath(folder)]);
        Assert.True(Directory.Exists(folder));
    }

    /// <summary>READSHARE1: <see cref="TreeGuard.Install"/> keeps a guard as shipped while its move into place still holds it, never writing it again.</summary>
    [Fact]
    public void An_unchanged_tree_guard_is_kept_while_its_move_into_place_still_holds_it()
    {
        if (!OperatingSystem.IsWindows()) return;
        var path = TreeGuard.Install(_home);
        File.SetLastWriteTimeUtc(path, At.UtcDateTime);

        using (MoveHold.Take(path))
        {
            Assert.Throws<IOException>(() => File.ReadAllText(path));
            Assert.Equal(path, TreeGuard.Install(_home));
        }

        Assert.Equal(At.UtcDateTime, File.GetLastWriteTimeUtc(path));
        Assert.Equal(TreeGuard.Source, File.ReadAllText(path));
    }

    /// <summary>READSHARE1: <see cref="WorkflowFiles.Load"/> reads a named workflow's file while its move into place still holds it.</summary>
    [Fact]
    public void A_named_workflow_is_loaded_while_its_move_into_place_still_holds_its_file()
    {
        if (!OperatingSystem.IsWindows()) return;
        var steps = JsonNode.Parse(
            """[{"id":"work","kind":"work"},{"id":"landing","kind":"landing","form":"branch","accept":"you","pattern":"docs/{quest}","plugin":"none"}]""")!;
        WorkflowStore.Add(_home, new WorkflowVersionAdded("docs-to-pr", "Documentation to a pull request", "terminal", "2026-10-09T09:00:00Z", steps), kept: []);
        var path = WorkflowStore.PathOf(_home, "docs-to-pr");

        using (MoveHold.Take(path))
        {
            Assert.Throws<IOException>(() => File.ReadAllText(path));
            var found = new WorkflowFiles(_home).Load("docs-to-pr")!;
            Assert.Null(found.Read.Problem);
            Assert.Equal(("docs-to-pr", "Documentation to a pull request", 1), (found.Read.Id, found.Read.Name, found.Read.Versions.Count));
        }
    }

    /// <summary>READSHARE1: a trace's read of the landings (<c>Trace.LandingsProblem</c>) finds them readable while their move into place holds them.</summary>
    [Fact]
    public async Task A_trace_reads_the_landings_while_their_move_into_place_still_holds_them()
    {
        if (!OperatingSystem.IsWindows()) return;
        var landings = new LandedBranches(_home);
        landings.Record(new LandedBranch(
            "dashboards", "work", "feature/q1-fix-the-dashboard-figure", "main", "1a2b3c4d5e6f708192a3b4c5d6e7f80910111213", "s2", "q1",
            "Fix the dashboard figure", At));
        using var service = new ServiceClient("http://stand-in", null, new HttpClient(new Unanswering()));

        TraceFacts facts;
        using (MoveHold.Take(landings.FilePath))
        {
            Assert.Throws<IOException>(() => File.ReadAllText(landings.FilePath));
            facts = await Trace.ReadAsync(new TraceSources(service, _home, DriverConfig.Empty), CancellationToken.None);
        }

        Assert.Null(facts.LandingsUnread);
        Assert.Equal(["feature/q1-fix-the-dashboard-figure"], facts.Landings.Select(each => each.Branch));
    }

    /// <summary>READSHARE1: a trace's read of a session's rules file (<c>TraceReads.Rules</c>) reads it while its move into place still holds it.</summary>
    [Fact]
    public void A_trace_reads_a_session_s_rules_file_while_its_move_into_place_still_holds_it()
    {
        if (!OperatingSystem.IsWindows()) return;
        var path = DaorisHome.Entry(_home, HomeEntry.Spawn, "s3.settings.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        const string rules = """{"permissions":{"allow":["Read"],"deny":["Bash(rm:*)"]}}""";
        AtomicFile.WriteText(path, rules);
        using var service = new ServiceClient("http://stand-in", null, new HttpClient(new Unanswering()));

        using (MoveHold.Take(path))
        {
            Assert.Throws<IOException>(() => File.ReadAllText(path));
            var read = TraceReads.Here(new TraceSources(service, _home, DriverConfig.Empty)).ReadRules("s3");
            Assert.Equal((rules, (string?)null), (read.Text, read.Problem));
        }
    }
}
