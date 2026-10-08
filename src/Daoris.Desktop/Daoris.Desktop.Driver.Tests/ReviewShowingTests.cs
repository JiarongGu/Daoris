using System.Net;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// REVIEWENV1d (D154 point 5; the review environment design §2.3 steps 1–5, §3.3): what the driver asks of Daoris's browser for a
/// set-up step. Its tab is opened before its session, titled for its quest; once its set-up is posted, the folder its session
/// named is served to that tab at the rule's address only, from the build's own base, and kept served across looks until the
/// person's verdict or their skip, or until the step's tree goes; and <i>Show it again</i> serves the newest set-up again. The
/// shell's half is stood in: <c>ReviewServingTests</c> in the modules holds the serving itself.
/// </summary>
public sealed class ReviewShowingTests : IDisposable
{
    private const string Address = "http://localhost:4200";
    private static readonly ReviewEnvironment Local = new("local", "local", "README.md", Address);
    private static readonly ReviewEnvironment Dev = new("dev", "deployed", "docs/deploying-to-dev.md");

    private readonly string _tree = Path.Combine(Path.GetTempPath(), "daoris-review-showing-" + Guid.NewGuid().ToString("N")[..8]);
    private readonly Tabs _tabs = new();
    private readonly ReviewDesk _desk;

    public ReviewShowingTests()
    {
        Directory.CreateDirectory(Path.Combine(_tree, "dist", "app"));
        File.WriteAllText(Path.Combine(_tree, "dist", "app", "index.html"), "<!doctype html><title>Reports</title>");
        _desk = new ReviewDesk(_tabs);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tree, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    [Fact]
    public async Task A_local_set_up_step_s_tab_is_opened_before_its_session_titled_for_its_quest()
    {
        var held = await _desk.OpenForStepAsync(ReviewStandIn.Step(), Local, CancellationToken.None);

        Assert.Null(held);
        Assert.Equal([("q2", "Review #q2")], _tabs.Opened);
        Assert.Equal("Review #q2", ReviewDesk.TabTitle("#q2"));
    }

    [Fact]
    public async Task A_deployed_set_up_step_opens_no_tab_and_serves_nothing()
    {
        Assert.Null(await _desk.OpenForStepAsync(ReviewStandIn.Step(), Dev, CancellationToken.None));
        var said = await _desk.ServeSetUpAsync(Served("dist/app"), "s7", _tree, Dev, CancellationToken.None);

        Assert.Empty(_tabs.Opened);
        Assert.Null(said);
        Assert.Empty(_tabs.Serving);
    }

    [Fact]
    public async Task A_tab_that_does_not_open_holds_the_start_and_says_why()
    {
        _tabs.Refuses = "Daoris's browser did not start: the file is in use.";

        var held = await _desk.OpenForStepAsync(ReviewStandIn.Step(), Local, CancellationToken.None);

        Assert.NotNull(held);
        Assert.Contains("#q2", held, StringComparison.Ordinal);
        Assert.Contains("Daoris's browser did not start: the file is in use.", held, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Once_its_set_up_is_posted_the_folder_it_named_is_served_to_its_tab_at_the_rule_s_address_only()
    {
        var said = await _desk.ServeSetUpAsync(Served("dist/app"), "s7", _tree, Local, CancellationToken.None);

        var serve = Assert.Single(_tabs.Serving);
        Assert.Equal("q2", serve.Quest);
        Assert.Equal("Review #q2", serve.Title);
        Assert.Equal(Path.GetFullPath(Path.Combine(_tree, "dist", "app")), serve.Folder);
        Assert.Equal((Address, "/"), (serve.Address, serve.Base));
        Assert.Equal(["http://localhost:4200/*"], serve.Patterns);
        Assert.Equal("http://localhost:4200/reports", serve.Look);
        Assert.Equal("desk/41", serve.SetUp);
        Assert.NotNull(said);
        Assert.Contains("`dist/app`", said, StringComparison.Ordinal);
        Assert.Contains(Address, said, StringComparison.Ordinal);
        Assert.Contains("until", said, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_build_s_own_base_is_where_it_is_served_and_a_look_elsewhere_is_not_followed()
    {
        File.WriteAllText(Path.Combine(_tree, "dist", "app", "index.html"), "<!doctype html><head><BASE Href='/v3/'><title>R</title>");

        await _desk.ServeSetUpAsync(Served("dist/app", look: "http://localhost:4200/v3/reports/42"), "s7", _tree, Local, CancellationToken.None);
        var under = Assert.Single(_tabs.Serving);
        await _desk.ServeSetUpAsync(Served("dist/app", look: "https://sign-in.example/authorize"), "s7", _tree, Local, CancellationToken.None);
        var elsewhere = Assert.Single(_tabs.Serving);

        Assert.Equal(("/v3/", "http://localhost:4200/v3/*", "http://localhost:4200/v3/reports/42"), (under.Base, under.Pattern, under.Look));
        Assert.Equal(["http://localhost:4200/v3/*", "http://localhost:4200/v3"], under.Patterns);
        Assert.Equal("http://localhost:4200/v3/", elsewhere.Look);
    }

    [Theory]
    [InlineData("<base href=\"/v3/\">", "/v3/")]
    [InlineData("<base href=\"/v3\">", "/v3/")]
    [InlineData("<base href=\"http://localhost:4200/app/\">", "/app/")]
    [InlineData("<base href=\"https://other.example/app/\">", "/")]
    [InlineData("<base href=\"//other.example/app/\">", "/")]
    [InlineData("<base href=\"/../etc/\">", "/")]
    [InlineData("<base href=\"./\">", "/")]
    [InlineData("<title>none</title>", "/")]
    public void A_build_s_base_is_its_own_index_s_base_on_this_address_or_the_root(string head, string expected) =>
        Assert.Equal(expected, ReviewServed.BaseOf(head, Address));

    [Fact]
    public async Task A_set_up_that_named_no_folder_is_not_served_and_says_why()
    {
        var said = await _desk.ServeSetUpAsync(Served(folder: null), "s7", _tree, Local, CancellationToken.None);

        Assert.Empty(_tabs.Serving);
        Assert.NotNull(said);
        Assert.Contains("review_serve", said, StringComparison.Ordinal);
        Assert.Contains("your own", said, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("../outside")]
    [InlineData("dist/../..")]
    [InlineData("/dist/app")]
    [InlineData("dist\\app")]
    [InlineData("dist/missing")]
    [InlineData("C:/Windows")]
    public async Task A_folder_that_is_not_a_folder_of_its_tree_is_not_served(string folder)
    {
        var said = await _desk.ServeSetUpAsync(Served(folder), "s7", _tree, Local, CancellationToken.None);

        Assert.Empty(_tabs.Serving);
        Assert.NotNull(said);
        Assert.Contains("not served", said, StringComparison.Ordinal);
    }

    [Fact]
    public async Task It_is_kept_served_across_looks_while_its_set_up_waits_for_the_person()
    {
        var waiting = Served("dist/app");
        await _desk.ServeSetUpAsync(waiting, "s7", _tree, Local, CancellationToken.None);

        var said = await _desk.LookAsync([waiting], Unasked, CancellationToken.None);
        said = [.. said, .. await _desk.LookAsync([waiting], Unasked, CancellationToken.None)];

        Assert.Empty(said);
        Assert.Empty(_tabs.Stopped);
        var row = Assert.Single(_desk.Waiting);
        Assert.Equal(("q2", "local", "http://localhost:4200/reports", "the new column", true), (row.Quest, row.Environment, row.Look, row.Shows, row.Served));
        Assert.Equal("Show #q1 in `local` for review", row.Title);
    }

    [Theory]
    [InlineData("reviewed")]
    [InlineData("not-yet")]
    public async Task It_is_stopped_on_the_person_s_verdict_on_the_set_up_it_shows(string verdict)
    {
        await _desk.ServeSetUpAsync(Served("dist/app"), "s7", _tree, Local, CancellationToken.None);
        var answered = Served("dist/app") with { Verdicts = [new QuestReviewVerdictView(verdict) { SetUpMachine = "desk", SetUpSequence = 41 }] };

        var said = await _desk.LookAsync([answered], Unasked, CancellationToken.None);

        Assert.Equal(["q2"], _tabs.Stopped);
        Assert.Empty(_tabs.Serving);
        Assert.Contains(said, line => line.Contains("#q2", StringComparison.Ordinal) && line.Contains(verdict, StringComparison.Ordinal));
        Assert.Empty(_desk.Waiting);
    }

    [Fact]
    public async Task A_verdict_on_an_older_set_up_does_not_stop_the_newer_one_it_shows()
    {
        await _desk.ServeSetUpAsync(Served("dist/app"), "s7", _tree, Local, CancellationToken.None);
        var older = Served("dist/app") with { Verdicts = [new QuestReviewVerdictView("not-yet") { SetUpMachine = "desk", SetUpSequence = 40 }] };

        await _desk.LookAsync([older], Unasked, CancellationToken.None);

        Assert.Empty(_tabs.Stopped);
        Assert.Single(_tabs.Serving);
    }

    [Fact]
    public async Task It_is_stopped_on_the_person_s_skip_on_its_quest_gone_or_its_tree_gone()
    {
        await _desk.ServeSetUpAsync(Served("dist/app"), "s7", _tree, Local, CancellationToken.None);
        await _desk.LookAsync([Served("dist/app") with { Verdicts = [new QuestReviewVerdictView("skipped")] }], Unasked, CancellationToken.None);
        Assert.Equal(["q2"], _tabs.Stopped);

        await _desk.ServeSetUpAsync(Served("dist/app"), "s7", _tree, Local, CancellationToken.None);
        // Off the outstanding list, and the service no longer answers it: nothing waits on its tab.
        await _desk.LookAsync([], Unasked, CancellationToken.None);
        Assert.Equal(["q2", "q2"], _tabs.Stopped);

        await _desk.ServeSetUpAsync(Served("dist/app"), "s7", _tree, Local, CancellationToken.None);
        Directory.Delete(_tree, recursive: true);
        var said = await _desk.LookAsync([Served("dist/app")], Unasked, CancellationToken.None);
        Assert.Equal(["q2", "q2", "q2"], _tabs.Stopped);
        Assert.Contains(said, line => line.Contains("tree", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_served_quest_off_the_list_is_read_whole_before_it_is_stopped()
    {
        await _desk.ServeSetUpAsync(Served("dist/app"), "s7", _tree, Local, CancellationToken.None);

        // The look's list is the outstanding one; a quest still held is on it, and one read whole that still waits keeps its tab.
        await _desk.LookAsync([], (id, _) => Task.FromResult<QuestView?>(id == "q2" ? Served("dist/app") : null), CancellationToken.None);

        Assert.Empty(_tabs.Stopped);
    }

    [Fact]
    public async Task A_tab_the_shell_no_longer_serves_is_said_not_served_on_the_strip()
    {
        var waiting = Served("dist/app");
        await _desk.ServeSetUpAsync(waiting, "s7", _tree, Local, CancellationToken.None);
        await _desk.LookAsync([waiting], Unasked, CancellationToken.None);

        _tabs.Close("q2");

        Assert.False(Assert.Single(_desk.Waiting).Served);
    }

    [Fact]
    public async Task A_set_up_made_on_another_machine_waits_without_a_row_here()
    {
        var elsewhere = Served("dist/app") with { SetUps = [Served("dist/app").SetUps[0] with { Look = null }] };

        await _desk.LookAsync([elsewhere], Unasked, CancellationToken.None);

        Assert.Empty(_desk.Waiting);
    }

    [Fact]
    public async Task Show_it_again_serves_the_newest_set_up_again_and_brings_its_tab_forward()
    {
        var (ok, message) = await _desk.ShowAgainAsync(Served("dist/app"), Local, _tree, CancellationToken.None);

        Assert.True(ok, message);
        var serve = Assert.Single(_tabs.Serving);
        Assert.Equal(("q2", "http://localhost:4200/reports"), (serve.Quest, serve.Look));
        Assert.Contains("http://localhost:4200/reports", message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Show_it_again_is_refused_where_it_would_show_the_person_s_own_server()
    {
        var unserved = await _desk.ShowAgainAsync(Served(folder: null), Local, _tree, CancellationToken.None);
        var unshown = await _desk.ShowAgainAsync(Served("dist/app") with { SetUps = [] }, Local, _tree, CancellationToken.None);
        var treeless = await _desk.ShowAgainAsync(Served("dist/app"), Local, tree: null, CancellationToken.None);
        var undeclared = await _desk.ShowAgainAsync(Served("dist/app"), environment: null, _tree, CancellationToken.None);
        var deployed = await _desk.ShowAgainAsync(Served("dist/app"), Dev, _tree, CancellationToken.None);

        Assert.Empty(_tabs.Serving);
        Assert.False(unserved.Ok);
        Assert.Contains("review_serve", unserved.Message, StringComparison.Ordinal);
        Assert.False(unshown.Ok);
        Assert.Contains("nothing", unshown.Message, StringComparison.Ordinal);
        Assert.False(treeless.Ok);
        Assert.Contains("tree", treeless.Message, StringComparison.Ordinal);
        Assert.False(undeclared.Ok);
        Assert.Contains("`local`", undeclared.Message, StringComparison.Ordinal);
        Assert.False(deployed.Ok);
        Assert.Contains("`dev`", deployed.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Show_it_again_from_the_strip_reads_the_quest_its_rule_and_its_tree_from_the_service()
    {
        var config = DriverConfig.Empty with { Reviews = new Dictionary<string, ReviewRule> { ["web-app"] = new([Local], Required: true) } };
        var service = QuestStandIn.Client(request => (request.Method.Method, request.RequestUri!.PathAndQuery) switch
        {
            ("GET", "/api/quests?includeClosed=true") => (HttpStatusCode.OK, $"[{StepJson()}]"),
            ("GET", "/api/sessions?includeClosed=true") => (HttpStatusCode.OK,
                $$"""[{"id":"s7","repository":"web-app","state":"completed","quest":"q2","tree":{{System.Text.Json.JsonSerializer.Serialize(_tree)}}}]"""),
            _ => null,
        });

        var (ok, message) = await _desk.ShowAgainAsync(service, config, "#q2", CancellationToken.None);

        Assert.True(ok, message);
        Assert.Equal(Path.GetFullPath(Path.Combine(_tree, "dist", "app")), Assert.Single(_tabs.Serving).Folder);
    }

    /// <summary>The stand-in's set-up step, its newest set-up naming <paramref name="folder"/> as the one its session served.</summary>
    private static QuestView Served(string? folder, string look = "http://localhost:4200/reports")
    {
        var step = ReviewStandIn.Step();
        return step with { SetUps = [step.SetUps[0] with { Served = folder, Look = look }] };
    }

    private static Task<QuestView?> Unasked(string id, CancellationToken ct) => Task.FromResult<QuestView?>(null);

    /// <summary>The set-up step as the service's list carries it, its set-up naming the folder its session served.</summary>
    private static System.Text.Json.Nodes.JsonNode StepJson()
    {
        var json = QuestStandIn.Json(ReviewStandIn.Step()).AsObject();
        json["setUpIn"] = "local";
        json["setUps"] = new System.Text.Json.Nodes.JsonArray(new System.Text.Json.Nodes.JsonObject
        {
            ["commit"] = ReviewStandIn.Commit, ["look"] = "http://localhost:4200/reports", ["shows"] = "the new column",
            ["again"] = "open reports", ["served"] = "dist/app", ["session"] = "s7", ["local"] = true,
            ["at"] = "2026-10-08T09:00:00+00:00", ["machine"] = "desk", ["sequence"] = 41,
        });
        return json;
    }

    /// <summary>Daoris's browser as the review uses it, standing in: each tab opened, served and stopped, kept as asked.</summary>
    private sealed class Tabs : IReviewTabs
    {
        private readonly List<ReviewServe> _serving = [];

        public List<(string Quest, string Title)> Opened { get; } = [];

        public List<string> Stopped { get; } = [];

        public string? Refuses { get; set; }

        public IReadOnlyList<ReviewServe> Serving
        {
            get
            {
                lock (_serving) return [.. _serving];
            }
        }

        public Task OpenAsync(string quest, string title, CancellationToken ct = default)
        {
            if (Refuses is { } why) throw new InvalidOperationException(why);
            Opened.Add((quest, title));
            return Task.CompletedTask;
        }

        public Task ServeAsync(ReviewServe serve, CancellationToken ct = default)
        {
            if (Refuses is { } why) throw new InvalidOperationException(why);
            lock (_serving)
            {
                _serving.RemoveAll(each => each.Quest == serve.Quest);
                _serving.Add(serve);
            }

            return Task.CompletedTask;
        }

        public void Stop(string quest)
        {
            Stopped.Add(quest);
            Close(quest);
        }

        /// <summary>The person closed the tab: the shell serves it no more.</summary>
        public void Close(string quest)
        {
            lock (_serving) _serving.RemoveAll(each => each.Quest == quest);
        }
    }
}
