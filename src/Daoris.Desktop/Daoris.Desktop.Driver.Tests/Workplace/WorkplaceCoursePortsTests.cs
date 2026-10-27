using System.Net;
using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FOUNDATION1 mod-x, the Workplace-names-Course cut: the seams the cut adds. Workplace declares the lower rebase key, the historical
/// suffixes, the press's telling and the handed quests' facet; Course implements and installs them. The cases that need no new seam are
/// <see cref="WorkplaceCourseBoundaryTests"/>'.
/// </summary>
[Collection("agent operation ports")]
public sealed class WorkplaceCoursePortsTests : IDisposable
{
    private static readonly DateTimeOffset At = new(2026, 10, 27, 9, 0, 0, TimeSpan.Zero);
    private readonly string _home = Path.Combine(GitSeed.RepoRoot(), "_fixtures", "workplace-course-ports", Guid.NewGuid().ToString("N"));
    private string Requests => DaorisHome.Entry(_home, HomeEntry.Requests);
    private string ChatTree => DaorisHome.Entry(_home, HomeEntry.Trees, "default", "engine", "s1");

    public WorkplaceCoursePortsTests() => Directory.CreateDirectory(_home);
    public void Dispose() => GitSeed.Remove(_home);

    [Fact]
    public void The_lower_rebase_key_and_historical_suffixes_are_the_bytes_the_higher_constants_alias()
    {
        Assert.Equal("rebase", RebaseKeys.Kind);
        Assert.Equal(RebaseKeys.Kind, DaorisKeys.RebaseKind);
        foreach (var (session, tip) in new[] { ("s1", "0123abc"), ("origin/s1", "0123abc"), ("a/b/c", "t") })
        {
            Assert.Equal($"rebase/{session}/{tip}", RebaseKeys.Of(session, tip));
            Assert.Equal(DaorisKeys.Rebase(session, tip), RebaseKeys.Of(session, tip));
            Assert.Equal(tip, RebaseKeys.TipOf(RebaseKeys.Of(session, tip)));
        }
        foreach (var key in new[] { null, "", "rebase", "rebase/", "rebase/s1", "rebase/s1/", "Rebase/s1/t", "reopen/q1/m1/3" })
        {
            Assert.Equal(DaorisKeys.RebaseTipOf(key), RebaseKeys.TipOf(key));
            Assert.Null(RebaseKeys.TipOf(key));
        }

        Assert.Equal(".cannot.json", SessionHomeFiles.GoOnMarkSuffix);
        Assert.Equal(".reopened.json", SessionHomeFiles.ReopenMarkSuffix);
        Assert.Equal(".new-session.json", SessionHomeFiles.NewSessionChoiceSuffix);
        Assert.Equal(".hand-over.json", SessionHomeFiles.HandOverBoxSuffix);
        Assert.Equal(".hand-over-ask.json", SessionHomeFiles.HandOverAskSuffix);
        Assert.Equal(".hand-over-state.json", SessionHomeFiles.HandOverStateSuffix);
        Assert.Equal(".limit-move.json", SessionHomeFiles.LimitMoveSuffix);
        Assert.Equal(SessionHomeFiles.GoOnMarkSuffix, GoOnMarks.Suffix);
        Assert.Equal(SessionHomeFiles.ReopenMarkSuffix, ReopenMarks.Suffix);
        Assert.Equal(SessionHomeFiles.NewSessionChoiceSuffix, NewSessionChoices.Suffix);
        Assert.Equal(SessionHomeFiles.HandOverBoxSuffix, HandOverBox.Suffix);
        Assert.Equal(SessionHomeFiles.HandOverAskSuffix, HandOverAskFiles.AskSuffix);
        Assert.Equal(SessionHomeFiles.HandOverStateSuffix, HandOverAskFiles.StateSuffix);
        Assert.Equal(SessionHomeFiles.LimitMoveSuffix, LimitBoxes.Suffix);
    }

    [Fact]
    public void The_installed_telling_is_courses_and_writes_the_original_note_or_reports_false_without_failing()
    {
        Assert.IsType<WorkplacePressTellingOperation>(WorkplacePressTelling.Current);
        Assert.False(Directory.Exists(Requests));

        Assert.True(WorkplacePressTelling.Current.Tell(_home, TreesMarks.Land, "s1", At));
        Assert.True(WorkplacePressTelling.Current.Tell(_home, TreesMarks.LandWithoutLook, "not/an/id", At.AddSeconds(1)));
        Assert.False(WorkplacePressTelling.Current.Tell(_home, "not a route", "s1", At));
        Assert.Equal(2, Directory.EnumerateFiles(Requests, "*" + PressesTold.Suffix).Count());
        Assert.Equal([new PressTold(PressesTold.Land, "s1", At), new PressTold(PressesTold.LandWithoutLook, null, At.AddSeconds(1))],
            new PressesTold(_home).Take(At.AddSeconds(2)));

        Directory.Delete(Requests, recursive: true);
        File.WriteAllText(Requests, "a file where the folder would be");
        Assert.False(WorkplacePressTelling.Current.Tell(_home, TreesMarks.Land, "s1", At));
    }

    [Fact]
    public async Task The_land_press_tells_through_the_supplied_operation_after_the_press_and_before_its_words_with_its_result_ignored()
    {
        var actual = WorkplacePressTelling.Current;
        var spy = new Telling(world => world.Output.ToString() + world.Error);
        using var client = Client();
        var world = World(client);
        spy.Watched = world;
        try
        {
            WorkplacePressTelling.Current = spy;
            Assert.Equal(1, await ChatLandCommand.RunAsync(world, "s1", ChatTree, "chat-s1", Ended("s1"), null, null));
            Assert.Equal(1, await ChatLandCommand.RunAsync(World(client), "s1", ChatTree, "chat-s1", Ended("s1"), null, new ChatLookPress("why", "terminal")));
        }
        finally { WorkplacePressTelling.Current = actual; }

        Assert.Equal([(_home, "LAND_SESSION_TREE", "s1"), (_home, "LAND_WITHOUT_LOOK", "s1")], spy.Told.Select(each => (each.Home, each.Route, each.Session)));
        Assert.All(spy.Told, each => Assert.InRange(each.At, DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow));
        // Told before the press's words are printed, and the false it answered changed nothing.
        Assert.Equal("", spy.SeenWords[0]);
        Assert.NotEqual("", ((StringWriter)world.Error).ToString() + world.Output);
        Assert.False(Directory.Exists(Requests));
    }

    [Fact]
    public async Task A_missing_telling_is_required_only_where_the_press_returns_and_its_failures_propagate_unchanged()
    {
        var actual = WorkplacePressTelling.Current;
        using var client = Client();
        try
        {
            WorkplacePressTelling.Current = null!;
            var missing = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                ChatLandCommand.RunAsync(World(client), "s1", ChatTree, "chat-s1", Ended("s1"), null, null));
            Assert.Contains("Course installs it", missing.Message);

            // A live conversation, and a binding that failed, answer before any note is told.
            Assert.Equal(1, await ChatLandCommand.RunAsync(World(client), "s1", ChatTree, "chat-s1", "[]", null, null));
            var failed = WorkflowRunBindings.PathOf(_home, "chat-s2");
            Directory.CreateDirectory(Path.GetDirectoryName(failed)!);
            File.WriteAllText(failed, "{broken");
            Assert.Equal(1, await ChatLandCommand.RunAsync(World(client), "s2", ChatTree, "chat-s2", Ended("s2"), null, null));

            var failure = new IOException("refused");
            WorkplacePressTelling.Current = new Telling(_ => "") { Fail = failure };
            Assert.Same(failure, await Assert.ThrowsAsync<IOException>(() =>
                ChatLandCommand.RunAsync(World(client), "s1", ChatTree, "chat-s1", Ended("s1"), null, null)));
        }
        finally { WorkplacePressTelling.Current = actual; }
    }

    [Fact]
    public void The_installed_handed_quest_store_is_the_original_object_and_reads_and_clears_as_the_original_does()
    {
        Assert.IsType<WorkplaceHandedQuestStoreOperations>(WorkplaceHandedQuests.Current);
        var opened = WorkplaceHandedQuests.Current.Open(_home);
        Assert.IsType<HandedQuests>(opened);
        Assert.NotSame(opened, WorkplaceHandedQuests.Current.Open(_home));
        Assert.False(Directory.Exists(DaorisHome.Entry(_home, HomeEntry.Handed)));
        Assert.Empty(opened.Readable());
        opened.Clear("absent");

        var store = new HandedQuests(_home);
        foreach (var quest in new[] { "q2", "Q10", "q1", "q3" })
        {
            store.Write(quest, new HandedQuest("codex-acp", null, "s1", HandOverWhy.Press, HandOverBy.Person, At));
        }
        File.WriteAllText(store.PathOf("q3")!, "{ not json");
        File.WriteAllText(Path.Combine(DaorisHome.Entry(_home, HomeEntry.Handed), "not a name.json"), "{}");

        // Only files that read, in the store's ordinal order: what All() lists today.
        Assert.Equal(["Q10", "q1", "q2"], opened.Readable());
        Assert.Equal(store.All().Keys, opened.Readable());
        opened.Clear("q1");
        opened.Clear("../escape");
        Assert.Equal(["Q10", "q2"], opened.Readable());
        Assert.False(File.Exists(store.PathOf("q1")));
        Assert.True(File.Exists(store.PathOf("q3")));
    }

    [Fact]
    public async Task History_clearing_opens_the_supplied_store_once_at_its_config_step_reads_inside_the_workspace_branch_and_clears_in_order()
    {
        var actual = WorkplaceHandedQuests.Current;
        var configPath = DriverConfig.ResolvePath(_home);
        DriverConfig.Empty.WithForgiven("q1", 1).Save(configPath);
        var session = HistoryStandIn.Record("s1", "q1");
        var quest = HistoryStandIn.Quest("q1");
        using var ledger = new HistoryStandIn { Records = [session], Quests = [quest], Home = _home };
        ledger.Listings["quest=q1"] = [HistoryStandIn.Unit("quest", "q1", quests: ["q1"], sessions: ["s1"])];
        ledger.Listings["workspace=default"] = [HistoryStandIn.Unit("quest", "q1", quests: ["q1"], sessions: ["s1"])];
        using var service = ledger.Client();
        var stores = new Stores(["orphan", "q1", "extra"]);
        try
        {
            WorkplaceHandedQuests.Current = stores;
            var world = new HistoryWorld(service, _home, configPath, new SessionProcesses(DaorisHome.Entry(_home, HomeEntry.Sessions)));
            await HistoryClearing.ClearAsync(world, HistoryScope.Quest, "q1", [new HistoryUnitName("quest", "q1")], "screen");
            Assert.Equal([$"open:{_home}", "clear:q1"], stores.Calls);

            stores.Calls.Clear();
            await HistoryClearing.ClearAsync(world, HistoryScope.Workspace, "default", [new HistoryUnitName("quest", "q1")], "screen");
            Assert.Equal([$"open:{_home}", "readable", "clear:q1", "clear:orphan", "clear:extra"], stores.Calls.Take(5));
        }
        finally { WorkplaceHandedQuests.Current = actual; }
    }

    [Fact]
    public async Task A_missing_handed_quest_factory_is_required_when_history_clearing_reaches_its_config_step()
    {
        var actual = WorkplaceHandedQuests.Current;
        var configPath = DriverConfig.ResolvePath(_home);
        using var ledger = new HistoryStandIn { Records = [HistoryStandIn.Record("s1", "q1")], Quests = [HistoryStandIn.Quest("q1")], Home = _home };
        ledger.Listings["quest=q1"] = [HistoryStandIn.Unit("quest", "q1", quests: ["q1"], sessions: ["s1"])];
        using var service = ledger.Client();
        try
        {
            WorkplaceHandedQuests.Current = null!;
            var world = new HistoryWorld(service, _home, configPath, new SessionProcesses(DaorisHome.Entry(_home, HomeEntry.Sessions)));
            var missing = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                HistoryClearing.ClearAsync(world, HistoryScope.Quest, "q1", [new HistoryUnitName("quest", "q1")], "screen"));
            Assert.Contains("Course installs it", missing.Message);
        }
        finally { WorkplaceHandedQuests.Current = actual; }
    }

    private ServiceClient Client() => new("http://ledger.test", null,
        new HttpClient(new Handler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("[]") })), disposeHandler: false));

    private ChatLandWorld World(ServiceClient service) => new(service, _home, new SessionTrees(_home), null, new StringWriter(), new StringWriter());

    private string Ended(string id) => JsonSerializer.Serialize(new[] { new { id, state = "completed", kind = "chat", repository = "engine", tree = ChatTree } });

    private sealed class Handler(Func<HttpRequestMessage, Task<HttpResponseMessage>> answer) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => answer(request);
    }

    private sealed class Telling(Func<ChatLandWorld, string> seen) : IWorkplacePressTelling
    {
        public ChatLandWorld? Watched { get; set; }
        public Exception? Fail { get; init; }
        public List<(string Home, string Route, string? Session, DateTimeOffset At)> Told { get; } = [];
        public List<string> SeenWords { get; } = [];
        public bool Tell(string home, string route, string? session, DateTimeOffset at)
        {
            if (Fail is not null) throw Fail;
            Told.Add((home, route, session, at));
            SeenWords.Add(Watched is null ? "" : seen(Watched));
            return false;
        }
    }

    private sealed class Stores(string[] readable) : IWorkplaceHandedQuestStores
    {
        public List<string> Calls { get; } = [];
        public IWorkplaceHandedQuests Open(string home)
        {
            Calls.Add("open:" + home);
            return new Quests(this, readable);
        }
    }

    private sealed class Quests(Stores owner, string[] readable) : IWorkplaceHandedQuests
    {
        public IEnumerable<string> Readable() { owner.Calls.Add("readable"); return readable; }
        public void Clear(string quest) => owner.Calls.Add("clear:" + quest);
    }
}
