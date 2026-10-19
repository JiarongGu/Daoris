using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Daoris.Driver;
using Shenora.Core.Ipc;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// The quests, a pushed slice (PUSH1d3, D182 points 2 to 4): one owner in the shell's loop reading what the service answers of every
/// quest, closed ones and every workspace's included, allow-listed to the page's fields, a partition per workspace keyed by quest, so a
/// quest's move pushes that quest alone, whoever wrote it, and an attachment's path is never pushed.
/// </summary>
/// <remarks>The service read through the driver's own client from a stand-in; the change stream's wake told where the watch tells it.</remarks>
public sealed class QuestsStateSliceTests : DriverModuleBridge
{
    private readonly ServiceStandIn _service = new();

    /// <summary>A file a quest carries, as the service answers it to this machine: its path where the bytes are here, else null.</summary>
    internal static JsonObject File(string name, string? path) => new()
    {
        ["name"] = name, ["sha256"] = new string('b', 64), ["bytes"] = 2300, ["path"] = path,
    };

    /// <summary>A quest as the service answers it to this machine: every field it answers, the page's among them.</summary>
    private static JsonObject Quest(string id, string workspace, string status = "Open", string to = "engine") => new()
    {
        ["id"] = id, ["from"] = "person", ["to"] = to, ["title"] = $"quest {id}", ["body"] = "the body", ["status"] = status,
        ["note"] = null, ["filed"] = "2026-10-19T08:00:00+00:00", ["updated"] = "2026-10-19T09:00:00+00:00", ["workspace"] = workspace,
        ["links"] = new JsonArray("https://example.test/a"),
        ["attachments"] = new JsonArray(File("trace.log", $"X:/daoris/quests/{id}/trace.log"), File("elsewhere.png", null)),
        ["then"] = new JsonArray(), ["parent"] = null, ["conflicts"] = new JsonArray(), ["awaits"] = null, ["publishedBy"] = null,
        ["deletable"] = true, ["lanes"] = new JsonArray(), ["requirements"] = new JsonArray(), ["answers"] = new JsonArray(),
        ["held"] = false, ["accepted"] = null, ["short"] = "short", ["hold"] = null, ["awaitsEvidence"] = false, ["evidence"] = null,
        ["review"] = null, ["setUpIn"] = null, ["setUps"] = null, ["verdicts"] = null, ["noteParts"] = null, ["closure"] = null,
        ["submission"] = null, ["grewFrom"] = null, ["sourceReopened"] = null,
        ["reopened"] = new JsonObject { ["machine"] = "m1", ["sequence"] = 4, ["note"] = "again" }, ["settled"] = null, ["holder"] = null,
        ["here"] = "m1",
    };

    internal static string List(params JsonObject[] records) => new JsonArray([.. records.Select(record => record.DeepClone())]).ToJsonString();

    /// <summary>A loop come up over the stand-in service, and its quests' slice.</summary>
    private async Task<(DriverLoop Loop, StateSlice Slice)> UpAsync()
    {
        var loop = Loop();
        await loop.ComeUpAsync(new ServiceClient("http://stand-in", null, new HttpClient(_service)));
        return (loop, loop.Slices.Find(QuestsStateSlice.Name)!);
    }

    private IReadOnlyList<SlicePush> Pushes() =>
    [
        .. Raised.Where(each => each.Type == StateSlice.PushType).Select(each => (SlicePush)each.Payload!)
            .Where(push => push.Slice == QuestsStateSlice.Name),
    ];

    private static JsonObject Written(object? data) => JsonSerializer.SerializeToNode(data, IpcJson.Options)!.AsObject();

    [Fact]
    public async Task Each_workspace_is_a_partition_of_its_quests_by_id_each_the_page_s_fields_as_the_service_answered_them()
    {
        var answered = new[] { Quest("q1", "default"), Quest("q2", "games", "Taken"), Quest("q3", "games", "Done") };
        _service.Quests = List(answered);
        var (_, slice) = await UpAsync();

        await slice.JudgeAsync();
        var snapshot = slice.Snapshot();

        Assert.Equal((QuestsStateSlice.Name, StateSlice.Ready), (snapshot.Slice, snapshot.Status));
        Assert.Equal(["default", "games"], snapshot.Partitions.Select(each => each.Partition));
        Assert.Equal(["q1"], Written(snapshot.Partitions[0].Data).Select(each => each.Key));
        // A closed quest is listed as the pull with the closed lists it, in its workspace.
        Assert.Equal(["q2", "q3"], Written(snapshot.Partitions[1].Data).Select(each => each.Key));
        foreach (var record in answered)
        {
            var pushed = Written(snapshot.Partitions.Single(each => each.Partition == record["workspace"]!.GetValue<string>()).Data)[record["id"]!.GetValue<string>()]!.AsObject();
            Assert.Equal(QuestsStateSlice.Fields.Order(), pushed.Select(each => each.Key).Order());
            // The pull's value and the push's, written alike, but the files, which carry whether they are kept here in place of a path.
            Assert.All(QuestsStateSlice.Fields.Where(field => field != "attachments"), field => Assert.Equal(record[field]?.ToJsonString(), pushed[field]?.ToJsonString()));
            Assert.Equal(
                """[{"name":"trace.log","sha256":"bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb","bytes":2300,"kept":true},{"name":"elsewhere.png","sha256":"bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb","bytes":2300,"kept":false}]""",
                pushed["attachments"]!.ToJsonString());
        }
    }

    /// <summary>
    /// The allow-list's twins: every field the page's <c>Quest</c> type reads is pushed, and every field the service answers of a quest
    /// is pushed or named the pull's alone; a file's fields likewise, its path the pull's and `kept` the push's.
    /// </summary>
    [Fact]
    public void Every_field_the_page_reads_is_pushed_and_every_field_the_service_answers_is_classed()
    {
        var page = SourceFields.Page("Quest");
        var service = SourceFields.Service("QuestResponse");

        Assert.Contains("here", page);
        // The first parameter and the last: the reading went through the whole record.
        Assert.Equal("id", service[0]);
        Assert.Contains("evidence", service);
        Assert.Equal("here", service[^1]);
        Assert.Equal(page.Order(StringComparer.Ordinal), QuestsStateSlice.Fields.Order(StringComparer.Ordinal));
        Assert.Empty(QuestsStateSlice.Fields.Intersect(QuestsStateSlice.PulledOnly));
        var classed = QuestsStateSlice.Fields.Concat(QuestsStateSlice.PulledOnly).ToHashSet(StringComparer.Ordinal);
        Assert.All(service, field => Assert.Contains(field, classed));

        var file = SourceFields.Page("QuestAttachment");
        Assert.Equal(
            file.Order(StringComparer.Ordinal),
            QuestsStateSlice.FileFields.Append(QuestsStateSlice.Kept).Concat(QuestsStateSlice.FilePulledOnly).Order(StringComparer.Ordinal));
        Assert.Equal(
            SourceFields.Service("QuestAttachmentResponse").Order(StringComparer.Ordinal),
            QuestsStateSlice.FileFields.Concat(QuestsStateSlice.FilePulledOnly).Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task A_hostile_field_and_a_file_s_path_never_reach_a_partition()
    {
        var hostile = Quest("q1", "default");
        hostile["token"] = "sk-hostile-top";
        hostile["evidence"] = new JsonObject { ["commit"] = "sk-hostile-evidence" };
        ((JsonObject)hostile["attachments"]![0]!)["secret"] = "sk-hostile-file";
        _service.Quests = List(hostile);
        var (_, slice) = await UpAsync();

        await slice.JudgeAsync();

        var pushed = JsonSerializer.Serialize(Pushes().Select(push => push.Data), IpcJson.Options);
        Assert.Contains("q1", pushed);
        Assert.DoesNotContain("sk-hostile", pushed);
        // A kept file's path is where this machine keeps it: the page reads only whether it is kept, so the path is never pushed.
        Assert.DoesNotContain("X:/daoris", pushed);
        Assert.DoesNotContain("\"path\"", pushed);
        Assert.DoesNotContain("awaitsEvidence", pushed);
    }

    [Fact]
    public async Task A_quest_s_move_pushes_that_quest_alone()
    {
        var quests = Enumerable.Range(0, 40).Select(i => Quest($"q{i:x3}", "default")).ToArray();
        _service.Quests = List(quests);
        var (_, slice) = await UpAsync();
        await slice.JudgeAsync();

        quests[7]["status"] = "Taken";
        _service.Quests = List(quests);
        await slice.JudgeAsync();

        var moved = Pushes()[^1];
        Assert.Equal(("default", 2L, (long?)1L), (moved.Partition, moved.Seq, moved.BaseSeq));
        Assert.Equal(["q007"], Written(moved.Data).Select(each => each.Key));
        Assert.Equal("Taken", Written(moved.Data)["q007"]!["status"]!.GetValue<string>());
    }

    [Fact]
    public async Task Before_the_service_answers_the_slice_is_not_judged_and_pushes_nothing()
    {
        var loop = Loop();
        var slice = loop.Slices.Find(QuestsStateSlice.Name)!;

        await slice.JudgeAsync();

        Assert.Equal(StateSlice.NotJudged, slice.Snapshot().Status);
        Assert.Empty(Pushes());
    }

    /// <summary>
    /// A connector's write reaches the database with no desktop dispatch: the change stream names the `quests` family, the watch tells
    /// the loop the move it heard, and the slice reads the service again and pushes the quest. A session's move wakes it too, since
    /// whether a quest may be deleted reads the sessions; the asks' and the channel's families, and a ref, move nothing.
    /// </summary>
    [Fact]
    public async Task A_connector_s_write_heard_on_the_change_stream_pushes_the_change_with_no_desktop_dispatch()
    {
        _service.Quests = List(Quest("q1", "default"), Quest("q2", "default"), Quest("q3", "default"));
        var (loop, slice) = await UpAsync();
        using var stop = new CancellationTokenSource();
        var running = slice.RunAsync(stop.Token);
        await UntilAsync(() => Pushes().Count == 1);
        var dispatched = Raised.Count(each => each.Type is StatePublisher.Type);

        var judged = slice.Judgements;
        loop.HeardMove(ChangeTopics.Ledger, "asks,channel");
        loop.HeardMove(ChangeTopics.Refs, "engine");
        await Task.Delay(300);
        Assert.Equal(judged, slice.Judgements);

        // The connector's write, then the change stream's wake as the watch tells it.
        _service.Quests = List(Quest("q1", "default", "Taken"), Quest("q2", "default"), Quest("q3", "default"));
        loop.HeardMove(ChangeTopics.Ledger, "channel,quests");
        await UntilAsync(() => Pushes().Count == 2);
        Assert.Equal("Taken", Written(Pushes()[^1].Data)["q1"]!["status"]!.GetValue<string>());

        // A session began on one: no longer deletable, heard on the sessions' family.
        var started = Quest("q2", "default");
        started["deletable"] = false;
        _service.Quests = List(Quest("q1", "default", "Taken"), started, Quest("q3", "default"));
        loop.HeardMove(ChangeTopics.Ledger, "sessions");
        await UntilAsync(() => Pushes().Count == 3);
        Assert.False(Written(Pushes()[^1].Data)["q2"]!["deletable"]!.GetValue<bool>());

        Assert.Equal(dispatched, Raised.Count(each => each.Type is StatePublisher.Type));
        await stop.CancelAsync();
        await running;
    }

    [Theory]
    [InlineData("STATE_CHANGED", """{"scopes":["quests"]}""", true)]
    [InlineData("STATE_CHANGED", """{"scopes":["sessions","quests","sync","registry"]}""", true)]
    [InlineData("STATE_CHANGED", """{"scopes":["asks"]}""", false)]
    [InlineData("STATE_CHANGED", """{"scopes":["driver"]}""", false)]
    [InlineData("SESSION_ENDED", "{}", false)]
    [InlineData("DRIVER_READY", "{}", true)]
    public void What_wakes_the_quests_slice(string type, string payload, bool wakes)
    {
        var wake = QuestsStateSlice.WakesOn.FirstOrDefault(each => each.Type == type);
        object told = type == StatePublisher.Type ? JsonSerializer.Deserialize<StateChanged>(payload, IpcJson.Options)! : new { };

        Assert.Equal(wakes, wake is not null && (wake.When is null || wake.When(told)));
    }
}

/// <summary>
/// The service's doors the quests' and the asks' slices read, standing in: every quest and every ask as a test sets them, each read
/// whole with the closed.
/// </summary>
internal sealed class ServiceStandIn : HttpMessageHandler
{
    private string _quests = "[]";
    private string _asks = "[]";

    public string Quests
    {
        get => Volatile.Read(ref _quests);
        set => Volatile.Write(ref _quests, value);
    }

    public string Asks
    {
        get => Volatile.Read(ref _asks);
        set => Volatile.Write(ref _asks, value);
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var answer = request.RequestUri!.PathAndQuery switch
        {
            "/api/quests?includeClosed=true" => Quests,
            "/api/asks?includeClosed=true" => Asks,
            _ => "[]",
        };
        return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new StringContent(answer, Encoding.UTF8, "application/json"),
        });
    }
}

/// <summary>
/// The fields a page type declares (<c>data/api.ts</c>) and a service record answers (<c>ApiContracts.cs</c>), read from the sources as
/// the route table's test reads the bridge: comments left out, and a field's own inline shape, whose fields are not the type's.
/// </summary>
internal static partial class SourceFields
{
    /// <summary>The fields of <c>export type {name} = { … };</c>.</summary>
    public static IReadOnlyList<string> Page(string name)
    {
        var text = Comments().Replace(System.IO.File.ReadAllText(Path.Combine(RepositoryRoot(), "src", "Daoris.Web", "src", "data", "api.ts")), "");
        var block = Regex.Match(text, $@"export type {name} = \{{(?<body>.*?)\n?\}};", RegexOptions.Singleline);
        Assert.True(block.Success, $"data/api.ts declares no `export type {name} = {{`");
        var body = block.Groups["body"].Value;
        // An inline shape's own fields (`review?: { choice; words }`) are the shape's, not the type's.
        for (var inner = Inline().Replace(body, "_"); inner != body; inner = Inline().Replace(body, "_")) body = inner;
        return [.. PageField().Matches(body).Select(field => field.Groups["name"].Value)];
    }

    /// <summary>The parameters of <c>record {name}(…);</c>, as its web defaults write them.</summary>
    public static IReadOnlyList<string> Service(string name)
    {
        var text = Comments().Replace(System.IO.File.ReadAllText(Path.Combine(RepositoryRoot(), "src", "Daoris.Service", "Daoris.Service.Http", "ApiContracts.cs")), "");
        var block = Regex.Match(text, $@"record {name}\((?<body>.*?)\);", RegexOptions.Singleline);
        Assert.True(block.Success, $"ApiContracts.cs declares no `record {name}(`");
        return [.. ServiceField().Matches(block.Groups["body"].Value).Select(field => char.ToLowerInvariant(field.Groups["name"].Value[0]) + field.Groups["name"].Value[1..])];
    }

    /// <summary>Walk up to the workspace root, which the tests run under.</summary>
    private static string RepositoryRoot()
    {
        var at = new DirectoryInfo(AppContext.BaseDirectory);
        while (at is not null && !System.IO.File.Exists(Path.Combine(at.FullName, "daoris.json"))) at = at.Parent;
        return at?.FullName ?? throw new InvalidOperationException("no workspace root above the test binary");
    }

    [GeneratedRegex(@"/\*.*?\*/|//[^\n]*", RegexOptions.Singleline)]
    private static partial Regex Comments();

    [GeneratedRegex(@"\{[^{}]*\}")]
    private static partial Regex Inline();

    [GeneratedRegex(@"(?<name>\w+)\??:")]
    private static partial Regex PageField();

    [GeneratedRegex(@"\b(?<name>[A-Z]\w*)\s*(?==|,|$)")]
    private static partial Regex ServiceField();
}
