using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

[Collection("agent operation ports")]
public sealed class WorkplaceContextPortsTests : IDisposable
{
    private readonly string _home = Path.Combine(GitSeed.RepoRoot(), "_fixtures", "workplace-context", Guid.NewGuid().ToString("N"));
    private string Sessions => DaorisHome.Entry(_home, HomeEntry.Sessions);
    private string Conversation => Path.Combine(Sessions, "s1.events.jsonl");
    private string Transcript => Path.Combine(Sessions, "s1.log");

    public WorkplaceContextPortsTests() => Directory.CreateDirectory(_home);
    public void Dispose() => GitSeed.Remove(_home);

    private IWorkplaceEventHistory Open() => WorkplaceEventRecords.Current.Open(Sessions);
    private static SessionEvent Word(string? id = null) => new() { Kind = SessionEventKind.User, Origin = "person", Text = "word", Id = id };

    [Fact]
    public void Installed_factory_is_inert_and_returns_the_original_record_with_shared_write_read_and_watch_identity()
    {
        Assert.IsType<WorkplaceEventRecordOperations>(WorkplaceEventRecords.Current);
        var record = Open();
        Assert.IsType<SessionEvents>(record);
        Assert.False(Directory.Exists(Sessions));
        Assert.NotSame(record, Open());
        ISessionEvents writes = record;
        ISessionEventHistory reads = record;
        ISessionEventRemoval removal = record;
        Assert.Same(record, writes);
        Assert.Same(record, reads);
        Assert.Same(record, removal);
        var heard = new List<SessionEvent>();
        writes.Evented += (_, e) => heard.Add(e);
        writes.Keep("s1", Word("w1"), null);
        writes.Keep("s1", Word("w2"), null);
        Assert.Equal([1L, 2L], reads.ReadAll("s1").Select(e => e.Seq));
        Assert.Equal(reads.ReadAll("s1"), heard);
        Assert.Equal(((SessionEvents)record).After("s1", 0).Events, reads.ReadAll("s1"));
        Assert.False(File.ReadAllBytes(Conversation).Take(3).SequenceEqual(new byte[] { 239, 187, 191 }));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Supplied_removal_resets_the_original_numbering_even_without_a_file_and_retains_its_subscribers(bool absent)
    {
        var record = Open();
        var heard = new List<long>();
        record.Evented += (_, e) => heard.Add(e.Seq);
        record.Keep("s1", Word(), null);
        record.Keep("s1", Word(), null);
        if (absent) File.Delete(Conversation);
        var factory = WorkplaceEventRecords.Current;
        try
        {
            // A supplied record does not consult the standalone factory or replace its numbering cache.
            WorkplaceEventRecords.Current = null!;
            new SessionHomeFiles(_home).Remove(["s1"], record);
        }
        finally { WorkplaceEventRecords.Current = factory; }
        Assert.False(File.Exists(Conversation));
        record.Keep("s1", Word(), null);
        Assert.Equal([1L, 2L, 1L], heard);
        Assert.Equal(1, Assert.Single(record.ReadAll("s1")).Seq);
    }

    [Fact]
    public async Task History_clear_and_daily_tidying_forward_the_supplied_record_and_reset_its_numbering()
    {
        var now = DateTimeOffset.UtcNow;
        var record = Open();
        var heard = new List<long>();
        record.Evented += (_, e) => heard.Add(e.Seq);
        var configPath = DriverConfig.ResolvePath(_home);
        DriverConfig.Empty.WithKeep(new KeepSet("history", Days: 30)).Save(configPath);
        var actual = WorkplaceEventRecords.Current;
        try
        {
            WorkplaceEventRecords.Current = null!;
            foreach (var daily in new[] { false, true })
            {
                record.Keep("s1", Word(), null);
                record.Keep("s1", Word(), null);
                var session = HistoryStandIn.Record("s1", "q1");
                var quest = HistoryStandIn.Quest("q1");
                var updated = (now - TimeSpan.FromDays(100)).ToString("O");
                session["updated"] = updated;
                quest["updated"] = updated;
                using var ledger = new HistoryStandIn { Records = [session], Quests = [quest], Home = _home };
                ledger.Listings["workspace=default"] = [HistoryStandIn.Unit("quest", "q1", quests: ["q1"], sessions: ["s1"])];
                using var service = ledger.Client();
                var processes = new SessionProcesses(Sessions);
                if (daily)
                {
                    var tidying = new BranchTidying(_home) { Events = record, Processes = processes, Clock = () => now, Pace = TimeSpan.Zero };
                    Assert.Same(record, tidying.Events);
                    await tidying.LookAsync(service, null, CancellationToken.None);
                }
                else
                {
                    var world = new HistoryWorld(service, _home, configPath, processes) { Events = record, Clock = () => now };
                    Assert.Same(record, world.Events);
                    await HistoryClearing.ClearAsync(world, HistoryScope.Workspace, "default", [new HistoryUnitName("quest", "q1")], "screen");
                }
                Assert.Equal(["quest:q1"], ledger.Pressed);
                Assert.Empty(record.ReadAll("s1"));
            }
            record.Keep("s1", Word(), null);
            Assert.Equal([1L, 2L, 1L, 2L, 1L], heard);
        }
        finally { WorkplaceEventRecords.Current = actual; }
    }

    [Fact]
    public void Null_removal_keeps_the_original_file_only_path_without_resetting_an_unsupplied_records_cache()
    {
        var record = Open();
        record.Keep("s1", Word(), null);
        record.Keep("s1", Word(), null);
        var removed = new SessionHomeFiles(_home).Remove(["s1"], null);
        Assert.Contains(SessionHomeFiles.Conversation, removed.Went["s1"]);
        record.Keep("s1", Word(), null);
        Assert.Equal(3, Assert.Single(record.ReadAll("s1")).Seq);
    }

    [Fact]
    public void Independent_history_checks_existence_before_reading_and_deduplicates_each_record_separately()
    {
        var history = new History();
        history.Rows["s1"] = [Word("same"), Word("same"), Word(), Word(),
            Word() with { Origin = "daoris" }, Word() with { From = "reviewer" }, Word() with { Kind = SessionEventKind.Note }];
        history.Rows["s2"] = [Word("same")];
        Assert.Equal(6, ChatPresses.TurnsOf(history, ["s1", "missing", "s2"], waiting: 2));
        Assert.Equal(["exists:s1", "read:s1", "exists:missing", "exists:s2", "read:s2"], history.Calls);
    }

    [Fact]
    public void Real_facets_keep_opening_selection_clipping_invalid_ids_torn_lines_and_after_zero_filtering()
    {
        var record = Open();
        record.Keep("s1", Word() with { Text = "carry", Continues = "previous" }, null);
        record.Keep("s1", Word() with { Text = "waiting", Reaches = "resume" }, null);
        var title = "道 " + new string('x', 150);
        record.Keep("s1", Word() with { Text = title + "\nsecond line" }, null);
        record.Keep("s2", Word() with { Origin = "target" }, null);
        File.AppendAllText(Conversation, "{torn\n{\"seq\":0,\"kind\":\"user\",\"origin\":\"person\",\"text\":\"not after zero\"}\n");
        var openings = record.Openings(["s1", "S1", "s2", "missing", "../escape"]);
        var opening = Assert.Single(openings);
        Assert.Equal("s1", opening.Key);
        Assert.Equal(TextBounds.Clip(title, SessionEvents.OpeningLimit), opening.Value);
        Assert.Equal(3, record.ReadAll("s1").Count);
        Assert.False(record.Exists("missing"));
        Assert.Empty(record.ReadAll("missing"));
        Assert.Throws<DriverException>(() => record.Exists("../escape"));
        Assert.Throws<DriverException>(() => record.ReadAll("../escape"));
        Assert.False(record.Forget("../escape"));
    }

    [Fact]
    public void Installed_keep_reports_a_disk_refusal_without_publishing_an_event()
    {
        var record = Open();
        Directory.CreateDirectory(Conversation);
        var diagnostics = new List<string>();
        var published = 0;
        record.Evented += (_, _) => published++;
        record.Keep("s1", Word(), diagnostics.Add);
        Assert.StartsWith("[the conversation record could not keep an event:", Assert.Single(diagnostics));
        Assert.Equal(0, published);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Refused_conversation_removal_counts_actual_bytes_and_continues_the_original_inventory(bool access)
    {
        Directory.CreateDirectory(Sessions);
        File.WriteAllText(Conversation, "1234567890");
        File.WriteAllText(Transcript, "words");
        var calls = new List<string>();
        var history = new History
        {
            Remove = _ =>
            {
                calls.Add("forget");
                File.WriteAllText(Conversation, "1234");
                throw access ? new UnauthorizedAccessException("held") : new IOException("held");
            },
        };
        var removed = new SessionHomeFiles(_home, (path, folder) =>
        {
            Assert.False(folder);
            calls.Add("transcript");
            File.Delete(path);
        }).Remove(["s1"], history);
        Assert.Equal(["forget", "transcript"], calls);
        Assert.Equal([SessionHomeFiles.Conversation], removed.Stayed["s1"]);
        Assert.Equal([SessionHomeFiles.Transcript], removed.Went["s1"]);
        Assert.Equal([Conversation], removed.Failed);
        Assert.Equal(6, removed.Bytes["s1"].Conversations);
        Assert.Equal(5, removed.Bytes["s1"].Transcripts);
        Assert.Equal("1234", File.ReadAllText(Conversation));
    }

    [Fact]
    public void A_false_forget_is_not_relabelled_as_a_failure_and_cancellation_stops_at_the_original_boundary()
    {
        Directory.CreateDirectory(Sessions);
        File.WriteAllText(Conversation, "kept");
        File.WriteAllText(Transcript, "kept too");
        var history = new History();
        var files = new SessionHomeFiles(_home);
        var removed = files.Remove(["s1"], history);
        Assert.Empty(removed.Failed);
        Assert.Empty(removed.Stayed["s1"]);
        Assert.DoesNotContain(SessionHomeFiles.Conversation, removed.Went["s1"]);
        Assert.Equal(0, removed.Bytes["s1"].Conversations);
        File.WriteAllText(Transcript, "still kept");
        var cancelled = new OperationCanceledException(new CancellationToken(true));
        history.Remove = _ => throw cancelled;
        Assert.Same(cancelled, Assert.Throws<OperationCanceledException>(() => files.Remove(["s1"], history)));
        Assert.Equal("still kept", File.ReadAllText(Transcript));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Refusal_or_cancellation_before_delete_acknowledgement_keeps_every_local_file(bool cancel)
    {
        Directory.CreateDirectory(Sessions);
        File.WriteAllText(Conversation, "kept");
        File.WriteAllText(Transcript, "kept too");
        var calls = new List<string>();
        using var closing = new CancellationTokenSource();
        using var ledger = new DeleteLedger(calls) { Refuse = !cancel, CancelDelete = cancel ? closing : null };
        using var service = ledger.Client();
        var history = new History { Remove = _ => throw new InvalidOperationException("must not remove") };
        var deletion = new SessionDeletion(_home);
        if (cancel)
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => deletion.DeleteAsync(service, "s1", "screen", null, history, closing.Token));
        else
            Assert.Equal(DeleteVerdict.Live, (await deletion.DeleteAsync(service, "s1", "screen", null, history)).Verdict);
        Assert.Equal(["GET /api/sessions", "GET /api/sessions/s1/deletable", "DELETE /api/sessions/s1"], calls);
        Assert.Empty(history.Calls);
        Assert.Equal("kept", File.ReadAllText(Conversation));
        Assert.Equal("kept too", File.ReadAllText(Transcript));
    }

    [Fact]
    public async Task Acknowledged_delete_uses_the_supplied_record_before_transcript_and_keeps_synchronous_cleanup_after_cancellation()
    {
        Directory.CreateDirectory(Sessions);
        File.WriteAllText(Conversation, "conversation");
        File.WriteAllText(Transcript, "transcript");
        var calls = new List<string>();
        using var closing = new CancellationTokenSource();
        using var ledger = new DeleteLedger(calls);
        using var service = ledger.Client();
        var history = new History { Remove = _ => { calls.Add("forget"); closing.Cancel(); File.Delete(Conversation); return true; } };
        var result = await new SessionDeletion(_home, (path, folder) =>
        {
            Assert.False(folder);
            calls.Add("transcript");
            File.Delete(path);
        }).DeleteAsync(service, "s1", "screen", null, history, closing.Token);
        Assert.Equal(DeleteVerdict.Deleted, result.Verdict);
        Assert.Equal(["GET /api/sessions", "GET /api/sessions/s1/deletable", "DELETE /api/sessions/s1", "forget", "transcript"], calls);
        Assert.Equal(["record", SessionHomeFiles.Conversation, SessionHomeFiles.Transcript], result.Removed);
        Assert.Empty(result.Stayed);
    }

    [Fact]
    public async Task Standalone_factory_is_required_but_live_and_bind_failed_paths_do_not_resolve_it()
    {
        var actual = WorkplaceEventRecords.Current;
        using var ledger = new DeleteLedger([]);
        using var service = ledger.Client();
        var world = ChatWorld(service);
        try
        {
            WorkplaceEventRecords.Current = null!;
            Assert.Equal(1, await ChatLandCommand.RunAsync(world, "s1", ChatTree, "chat-s1", "[]", null, null));
            var failed = WorkflowRunBindings.PathOf(_home, "chat-s2");
            Directory.CreateDirectory(Path.GetDirectoryName(failed)!);
            File.WriteAllText(failed, "{broken");
            Assert.Equal(1, await ChatLandCommand.RunAsync(world, "s2", ChatTree, "chat-s2", Ended("s2"), null, null));
            await Assert.ThrowsAsync<InvalidOperationException>(() => ChatLandCommand.RunAsync(world, "s1", ChatTree, "chat-s1", Ended("s1"), null, null));
        }
        finally { WorkplaceEventRecords.Current = actual; }
    }

    [Fact]
    public async Task Standalone_factory_receives_exact_directory_after_binding_and_its_failures_propagate_unchanged()
    {
        var actual = WorkplaceEventRecords.Current;
        using var ledger = new DeleteLedger([]);
        using var service = ledger.Client();
        try
        {
            foreach (var failure in new Exception[] { new IOException("refused"), new UnauthorizedAccessException("refused"),
                new OperationCanceledException(new CancellationToken(true)) })
            {
                WorkplaceEventRecords.Current = new Factory(directory =>
                {
                    Assert.Equal(Sessions, directory);
                    Assert.NotNull(WorkflowRunBindings.Read(_home, "chat-s1"));
                    throw failure;
                });
                Assert.Same(failure, await Assert.ThrowsAsync(failure.GetType(), () =>
                    ChatLandCommand.RunAsync(ChatWorld(service), "s1", ChatTree, "chat-s1", Ended("s1"), null, null)));
            }
            Assert.False(Directory.Exists(Sessions));
        }
        finally { WorkplaceEventRecords.Current = actual; }
    }

    [Fact]
    public async Task Standalone_press_constructs_once_and_keeps_history_lazy_when_its_first_git_is_cancelled()
    {
        Directory.CreateDirectory(ChatTree);
        var actual = WorkplaceEventRecords.Current;
        var git = WorkingTree.Runs.Value;
        var calls = new List<string>();
        var history = new History();
        using var closing = new CancellationTokenSource();
        using var ledger = new DeleteLedger([]);
        using var service = ledger.Client();
        try
        {
            WorkplaceEventRecords.Current = new Factory(directory => { Assert.Equal(Sessions, directory); calls.Add("open"); return history; });
            WorkingTree.Runs.Value = (_, _, ct) =>
            {
                calls.Add("git");
                Assert.Equal(closing.Token, ct);
                closing.Cancel();
                return Task.FromCanceled<ChildRun>(ct);
            };
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                ChatLandCommand.RunAsync(ChatWorld(service), "s1", ChatTree, "chat-s1", Ended("s1"), null, null, closing.Token));
            Assert.Equal(["open", "git"], calls);
            Assert.Empty(history.Calls);
        }
        finally { WorkingTree.Runs.Value = git; WorkplaceEventRecords.Current = actual; }
    }

    private string ChatTree => DaorisHome.Entry(_home, HomeEntry.Trees, "default", "engine", "s1");
    private string Ended(string id) => JsonSerializer.Serialize(new[] { new { id, state = "completed", kind = "chat", repository = "engine", tree = ChatTree } });
    private ChatLandWorld ChatWorld(ServiceClient service) => new(service, _home, new SessionTrees(_home), null, new StringWriter(), new StringWriter());

    private sealed class Factory(Func<string, IWorkplaceEventHistory> open) : IWorkplaceEventRecords
    {
        public IWorkplaceEventHistory Open(string directory) => open(directory);
    }

    private sealed class History : IWorkplaceEventHistory
    {
        public List<string> Calls { get; } = [];
        public Dictionary<string, IReadOnlyList<SessionEvent>> Rows { get; } = new(StringComparer.Ordinal);
        public Func<string, bool> Remove { get; set; } = _ => false;
        public event Action<string, SessionEvent>? Evented;
        public bool Exists(string id) { Calls.Add("exists:" + id); return Rows.ContainsKey(id); }
        public IReadOnlyList<SessionEvent> ReadAll(string id) { Calls.Add("read:" + id); return Rows[id]; }
        public bool Forget(string id) { Calls.Add("forget:" + id); return Remove(id); }
        public IReadOnlyDictionary<string, string> Openings(IEnumerable<string> ids) { Calls.Add("openings:" + string.Join(',', ids)); return new Dictionary<string, string>(); }
        public void Keep(string id, SessionEvent e, Action<string>? say) { Calls.Add("keep:" + id); Evented?.Invoke(id, e); }
    }

    private sealed class DeleteLedger(List<string> calls) : HttpMessageHandler
    {
        public bool Refuse { get; init; }
        public CancellationTokenSource? CancelDelete { get; init; }
        public ServiceClient Client() => new("http://ledger.test", null, new HttpClient(this, disposeHandler: false));
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var path = request.RequestUri!.AbsolutePath;
            calls.Add($"{request.Method} {path}");
            JsonNode body;
            var status = HttpStatusCode.OK;
            if (path == "/api/sessions")
            {
                var record = HistoryStandIn.Record("s1", kind: "chat");
                record["deletable"] = true;
                body = new JsonArray(record);
            }
            else if (request.Method == HttpMethod.Get) body = new JsonObject { ["deletable"] = true };
            else
            {
                CancelDelete?.Cancel();
                ct.ThrowIfCancellationRequested();
                status = Refuse ? HttpStatusCode.Conflict : HttpStatusCode.OK;
                body = Refuse
                    ? new JsonObject { ["deletable"] = false, ["refusal"] = "live", ["error"] = "running again" }
                    : new JsonObject { ["id"] = "s1", ["message"] = "deleted" };
            }
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body.ToJsonString(), System.Text.Encoding.UTF8, "application/json") });
        }
    }
}
