using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Daoris.Driver;
using Shenora.Core.Ipc;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// The session records, the push path's third slice (PUSH1d2, D182 points 2 to 4): one owner in the shell's loop reading what the
/// service answers of every session, closed ones and every workspace's included, allow-listed to the page's fields, a partition per
/// workspace keyed by session, so a record's move pushes that record alone, whoever wrote it.
/// </summary>
/// <remarks>The service read through the driver's own client from a stand-in; the change stream's wake told where the watch tells it.</remarks>
public sealed partial class SessionsStateSliceTests : DriverModuleBridge
{
    private readonly StandIn _service = new();

    /// <summary>A record as the service answers it to this machine: every field it answers, and the page's among them.</summary>
    private static JsonObject Record(string id, string workspace, string state = "completed", string repository = "engine") => new()
    {
        ["id"] = id, ["quest"] = "q1", ["repository"] = repository, ["adapter"] = "claude-code", ["state"] = state,
        ["note"] = "it read the plan", ["evidence"] = "commit abc", ["transcript"] = $"X:/daoris/transcripts/{id}.jsonl",
        ["created"] = "2026-10-19T08:00:00+00:00", ["updated"] = "2026-10-19T09:00:00+00:00", ["workspace"] = workspace,
        ["kind"] = "driven", ["harnessVersion"] = "2.1.4", ["profile"] = "work", ["tree"] = $"X:/daoris/trees/engine/{id}",
        ["baseCommit"] = new string('a', 40), ["ask"] = null, ["took"] = false, ["answer"] = null, ["interrupted"] = false,
        ["limit"] = false, ["deletable"] = true, ["said"] = new JsonArray(), ["noteParts"] = new JsonArray(new JsonObject { ["words"] = "done" }),
        ["opinion"] = null, ["serves"] = null, ["from"] = null, ["fromWhy"] = null, ["fromBy"] = null, ["entry"] = "local/engine",
        ["helpMode"] = null, ["access"] = new JsonObject { ["permissions"] = "ask" }, ["piece"] = null,
        ["actor"] = "build", ["choice"] = "before", ["asked"] = new JsonObject { ["agent"] = "claude-code" },
        ["override"] = null, ["ran"] = new JsonObject { ["models"] = new JsonArray("reported-model"), ["more"] = 0, ["entries"] = new JsonArray(), ["entriesMore"] = 0 },
    };

    private static string Records(params JsonObject[] records) => new JsonArray([.. records.Select(record => record.DeepClone())]).ToJsonString();

    [Fact]
    public async Task A_reported_model_wakes_the_sessions_slice_without_a_state_move()
    {
        var record = Record("s1", "default", "working");
        _service.Sessions = Records(record);
        var (loop, slice) = await UpAsync();
        await slice.JudgeAsync();
        record["ran"]!["models"] = new JsonArray("next-reported-model");
        _service.Sessions = Records(record);
        await loop.Service!.KeepActorAsync("s1", ran: new ActorRan(["next-reported-model"], 0, [], 0));
        Assert.Contains(Raised, each => each.Type == StatePublisher.Type && each.Payload is StateChanged changed && changed.Scopes.Contains("sessions"));
        await slice.JudgeAsync();
        var pushed = Written(slice.Snapshot().Partitions.Single().Data)["s1"]!;
        Assert.Equal("working", pushed["state"]!.GetValue<string>());
        Assert.Equal("next-reported-model", pushed["ran"]!["models"]![0]!.GetValue<string>());
    }

    /// <summary>A loop come up over the stand-in service, and its sessions' slice.</summary>
    private async Task<(DriverLoop Loop, StateSlice Slice)> UpAsync()
    {
        var loop = Loop();
        await loop.ComeUpAsync(new ServiceClient("http://stand-in", null, new HttpClient(_service)));
        return (loop, loop.Slices.Find(SessionsStateSlice.Name)!);
    }

    private IReadOnlyList<SlicePush> Pushes() =>
    [
        .. Raised.Where(each => each.Type == StateSlice.PushType).Select(each => (SlicePush)each.Payload!)
            .Where(push => push.Slice == SessionsStateSlice.Name),
    ];

    private static JsonObject Written(object? data) => JsonSerializer.SerializeToNode(data, IpcJson.Options)!.AsObject();

    [Fact]
    public async Task Each_workspace_is_a_partition_of_its_records_by_id_each_the_page_s_fields_as_the_service_answered_them()
    {
        var answered = new[] { Record("s1", "default"), Record("s2", "games", "working"), Record("teammate/s3", "games") };
        _service.Sessions = Records(answered);
        var (_, slice) = await UpAsync();

        await slice.JudgeAsync();
        var snapshot = slice.Snapshot();

        Assert.Equal((SessionsStateSlice.Name, StateSlice.Ready), (snapshot.Slice, snapshot.Status));
        Assert.Equal(["default", "games"], snapshot.Partitions.Select(each => each.Partition));
        Assert.Equal(["s1"], Written(snapshot.Partitions[0].Data).Select(each => each.Key));
        // A teammate's record is listed as the pull lists it, in its workspace.
        Assert.Equal(["s2", "teammate/s3"], Written(snapshot.Partitions[1].Data).Select(each => each.Key));
        foreach (var record in answered)
        {
            var pushed = Written(snapshot.Partitions.Single(each => each.Partition == record["workspace"]!.GetValue<string>()).Data)[record["id"]!.GetValue<string>()]!.AsObject();
            Assert.Equal(SessionsStateSlice.Fields.Order(), pushed.Select(each => each.Key).Order());
            // The pull's value and the push's, written alike.
            Assert.All(SessionsStateSlice.Fields, field => Assert.Equal(record[field]?.ToJsonString(), pushed[field]?.ToJsonString()));
        }
    }

    /// <summary>
    /// The allow-list's twins: every field the page's <c>Session</c> type reads is pushed, and every field the service answers of a
    /// record is pushed or named the pull's alone, read from the two sources as the route table's test reads the bridge.
    /// </summary>
    [Fact]
    public void Every_field_the_page_reads_is_pushed_and_every_field_the_service_answers_is_classed()
    {
        var page = PageSessionFields();
        var service = ServiceSessionFields();

        Assert.Contains("tree", page);
        // The first parameter, one after a generic type, and the last: the reading went through the whole record.
        Assert.Equal("id", service[0]);
        Assert.Contains("said", service);
        Assert.Equal("ran", service[^1]);
        Assert.Equal(page.Order(StringComparer.Ordinal), SessionsStateSlice.Fields.Order(StringComparer.Ordinal));
        Assert.Empty(SessionsStateSlice.Fields.Intersect(SessionsStateSlice.PulledOnly));
        var classed = SessionsStateSlice.Fields.Concat(SessionsStateSlice.PulledOnly).ToHashSet(StringComparer.Ordinal);
        Assert.All(service, field => Assert.Contains(field, classed));
    }

    [Fact]
    public async Task A_hostile_field_in_a_record_never_reaches_a_partition()
    {
        var hostile = Record("s1", "default");
        hostile["token"] = "sk-hostile-top";
        hostile["said"] = new JsonArray(new JsonObject { ["text"] = "sk-hostile-said" });
        hostile["helpMode"] = "sk-hostile-mode";
        hostile["entry"] = "sk-hostile-entry";
        _service.Sessions = Records(hostile);
        var (_, slice) = await UpAsync();

        await slice.JudgeAsync();

        var pushed = JsonSerializer.Serialize(Pushes().Select(push => push.Data), IpcJson.Options);
        Assert.Contains("s1", pushed);
        Assert.DoesNotContain("sk-hostile", pushed);
        // The transcript's path is the pull's alone; the tree, which the page reads, is a path this desktop-only slice may carry.
        Assert.DoesNotContain("transcripts", pushed);
        Assert.DoesNotContain("deletable", pushed);
    }

    /// <summary>
    /// An install-sized history (D182's PUSH1d2 note: about 2 KiB a record) is pushed once; a record's move then pushes that record
    /// alone, a delta on the number held, never its workspace's history again.
    /// </summary>
    [Fact]
    public async Task A_record_s_move_pushes_that_record_alone_and_an_install_sized_history_is_pushed_once()
    {
        var history = Enumerable.Range(0, 2000).Select(i => Record($"s{i:x5}", "default")).ToArray();
        _service.Sessions = Records(history);
        var (_, slice) = await UpAsync();
        await slice.JudgeAsync();
        var whole = JsonSerializer.Serialize(Pushes().Single().Data, IpcJson.Options).Length;

        history[7]["state"] = "failed";
        _service.Sessions = Records(history);
        await slice.JudgeAsync();

        var moved = Pushes()[^1];
        Assert.Equal(("default", 2L, (long?)1L), (moved.Partition, moved.Seq, moved.BaseSeq));
        Assert.Equal(["s00007"], Written(moved.Data).Select(each => each.Key));
        Assert.Equal("failed", Written(moved.Data)["s00007"]!["state"]!.GetValue<string>());
        Assert.True(JsonSerializer.Serialize(moved.Data, IpcJson.Options).Length * 500 < whole, "a record's move pushed more than a record");
    }

    [Fact]
    public async Task Before_the_service_answers_the_slice_is_not_judged_and_pushes_nothing()
    {
        var loop = Loop();
        var slice = loop.Slices.Find(SessionsStateSlice.Name)!;

        await slice.JudgeAsync();

        Assert.Equal(StateSlice.NotJudged, slice.Snapshot().Status);
        Assert.Empty(Pushes());
    }

    /// <summary>
    /// A connector's write reaches the database with no desktop dispatch: the change stream names the `sessions` family, the watch
    /// tells the loop the move it heard, and the slice reads the service again and pushes the record. Another family moves nothing.
    /// </summary>
    [Fact]
    public async Task A_connector_s_write_heard_on_the_change_stream_pushes_the_change_with_no_desktop_dispatch()
    {
        _service.Sessions = Records(Record("s1", "default", "working"), Record("s2", "default"), Record("s3", "default"));
        var (loop, slice) = await UpAsync();
        using var stop = new CancellationTokenSource();
        var running = slice.RunAsync(stop.Token);
        await UntilAsync(() => Pushes().Count == 1);
        var dispatched = Raised.Count(each => each.Type is StatePublisher.Type or DriverLoop.EndedType);

        // Another family's move, and a ref's, judge nothing.
        var judged = slice.Judgements;
        loop.HeardMove(ChangeTopics.Ledger, "quests,asks");
        loop.HeardMove(ChangeTopics.Refs, "engine");
        await Task.Delay(300);
        Assert.Equal(judged, slice.Judgements);

        // The connector's write, then the change stream's wake as the watch tells it.
        _service.Sessions = Records(Record("s1", "default", "awaiting-person"), Record("s2", "default"), Record("s3", "default"));
        loop.HeardMove(ChangeTopics.Ledger, "channel,sessions");

        await UntilAsync(() => Pushes().Count == 2);
        Assert.Equal("awaiting-person", Written(Pushes()[^1].Data)["s1"]!["state"]!.GetValue<string>());
        Assert.Equal(dispatched, Raised.Count(each => each.Type is StatePublisher.Type or DriverLoop.EndedType));
        await stop.CancelAsync();
        await running;
    }

    [Theory]
    [InlineData("STATE_CHANGED", """{"scopes":["sessions"],"sessions":["s1"]}""", true)]
    [InlineData("STATE_CHANGED", """{"scopes":["driver"]}""", false)]
    [InlineData("STATE_CHANGED", """{"scopes":["quests","asks"]}""", false)]
    [InlineData("SESSION_ENDED", "{}", true)]
    [InlineData("DRIVER_READY", "{}", true)]
    public void What_wakes_the_sessions_slice(string type, string payload, bool wakes)
    {
        var wake = SessionsStateSlice.WakesOn.FirstOrDefault(each => each.Type == type);
        object told = type == StatePublisher.Type ? JsonSerializer.Deserialize<StateChanged>(payload, IpcJson.Options)! : new { };

        Assert.Equal(wakes, wake is not null && (wake.When is null || wake.When(told)));
    }

    /// <summary>Walk up to the workspace root, which the tests run under.</summary>
    private static string RepositoryRoot()
    {
        var at = new DirectoryInfo(AppContext.BaseDirectory);
        while (at is not null && !File.Exists(Path.Combine(at.FullName, "daoris.json"))) at = at.Parent;
        return at?.FullName ?? throw new InvalidOperationException("no workspace root above the test binary");
    }

    /// <summary>The fields the page's <c>Session</c> type declares (<c>data/api.ts</c>), its comments left out.</summary>
    private static IReadOnlyList<string> PageSessionFields()
    {
        var text = File.ReadAllText(Path.Combine(RepositoryRoot(), "src", "Daoris.Web", "src", "data", "api.ts"));
        var block = PageSession().Match(text);
        Assert.True(block.Success, "data/api.ts declares no `export type Session = {`");
        var body = Comments().Replace(block.Groups["body"].Value, "");
        return [.. PageField().Matches(body).Select(field => field.Groups["name"].Value)];
    }

    /// <summary>The fields the service answers of a record (<c>SessionResponse</c>), as its web defaults write them.</summary>
    private static IReadOnlyList<string> ServiceSessionFields()
    {
        var text = File.ReadAllText(Path.Combine(RepositoryRoot(), "src", "Daoris.Service", "Daoris.Service.Http", "ApiContracts.cs"));
        var block = ServiceSession().Match(text);
        Assert.True(block.Success, "ApiContracts.cs declares no `record SessionResponse(`");
        var parameters = Comments().Replace(block.Groups["body"].Value, "");
        return [.. ServiceField().Matches(parameters).Select(field => char.ToLowerInvariant(field.Groups["name"].Value[0]) + field.Groups["name"].Value[1..])];
    }

    [GeneratedRegex(@"export type Session = \{(?<body>.*?)\n\};", RegexOptions.Singleline)]
    private static partial Regex PageSession();

    [GeneratedRegex(@"record SessionResponse\((?<body>.*?)\);", RegexOptions.Singleline)]
    private static partial Regex ServiceSession();

    [GeneratedRegex(@"/\*.*?\*/|//[^\n]*", RegexOptions.Singleline)]
    private static partial Regex Comments();

    [GeneratedRegex(@"(?<name>\w+)\??:")]
    private static partial Regex PageField();

    [GeneratedRegex(@"\b(?<name>[A-Z]\w*)\s*(?==|,|$)")]
    private static partial Regex ServiceField();

    /// <summary>The service's doors the slice reads, standing in: every record as a test sets it, read whole with the closed.</summary>
    private sealed class StandIn : HttpMessageHandler
    {
        private string _sessions = "[]";

        public string Sessions
        {
            get => Volatile.Read(ref _sessions);
            set => Volatile.Write(ref _sessions, value);
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var sessions = request.RequestUri!.PathAndQuery == "/api/sessions?includeClosed=true";
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(sessions ? Sessions : "[]", Encoding.UTF8, "application/json"),
            });
        }
    }
}
