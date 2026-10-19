using System.Text.Json;
using System.Text.Json.Nodes;
using Daoris.Driver;
using Shenora.Core.Ipc;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// The asks, a pushed slice (PUSH1d3, D182 points 2 to 4): one owner in the shell's loop reading what the service answers of every ask,
/// closed and done ones and every workspace's included, allow-listed to the page's fields, a partition per workspace keyed by ask, so an
/// ask a connector wrote reaches every window by one push, and neither the person's words nor a file's path is pushed.
/// </summary>
public sealed class AsksStateSliceTests : DriverModuleBridge
{
    private readonly ServiceStandIn _service = new();

    /// <summary>An ask as the service answers it to this machine: every field it answers, the page's among them.</summary>
    private static JsonObject Ask(string id, string workspace, string state = "Published") => new()
    {
        ["id"] = id, ["workspace"] = workspace, ["sentence"] = $"ask {id}", ["state"] = state, ["tier"] = "declarations",
        ["asked"] = "2026-10-19T08:00:00+00:00", ["updated"] = "2026-10-19T09:00:00+00:00", ["asker"] = null, ["note"] = null,
        ["links"] = new JsonArray(),
        ["attachments"] = new JsonArray(QuestsStateSliceTests.File("trace.log", $"X:/daoris/asks/{id}/trace.log")),
        ["proposal"] = new JsonArray(new JsonObject { ["repository"] = "engine", ["score"] = 3, ["matched"] = new JsonArray("glow") }),
        ["quests"] = new JsonArray("q1"), ["intake"] = null, ["deletable"] = false,
        ["words"] = new JsonArray(new JsonObject { ["kind"] = "asked", ["text"] = "the person's own words", ["at"] = "2026-10-19T08:00:00+00:00" }),
        ["wordsKeptFrom"] = null, ["goAheads"] = new JsonArray(), ["reviewChoices"] = null, ["reviewProposals"] = null,
        ["workflowChoices"] = null, ["workflowProposals"] = null, ["short"] = null,
    };

    /// <summary>A loop come up over the stand-in service, and its asks' slice.</summary>
    private async Task<(DriverLoop Loop, StateSlice Slice)> UpAsync()
    {
        var loop = Loop();
        await loop.ComeUpAsync(new ServiceClient("http://stand-in", null, new HttpClient(_service)));
        return (loop, loop.Slices.Find(AsksStateSlice.Name)!);
    }

    private IReadOnlyList<SlicePush> Pushes() =>
    [
        .. Raised.Where(each => each.Type == StateSlice.PushType).Select(each => (SlicePush)each.Payload!)
            .Where(push => push.Slice == AsksStateSlice.Name),
    ];

    private static JsonObject Written(object? data) => JsonSerializer.SerializeToNode(data, IpcJson.Options)!.AsObject();

    [Fact]
    public async Task Each_workspace_is_a_partition_of_its_asks_by_id_each_the_page_s_fields_as_the_service_answered_them()
    {
        var answered = new[] { Ask("a1", "default"), Ask("a2", "games", "Done"), Ask("a3", "games", "Closed") };
        _service.Asks = QuestsStateSliceTests.List(answered);
        var (_, slice) = await UpAsync();

        await slice.JudgeAsync();
        var snapshot = slice.Snapshot();

        Assert.Equal((AsksStateSlice.Name, StateSlice.Ready), (snapshot.Slice, snapshot.Status));
        Assert.Equal(["default", "games"], snapshot.Partitions.Select(each => each.Partition));
        // A done and a closed ask are listed as the pull with the closed lists them.
        Assert.Equal(["a2", "a3"], Written(snapshot.Partitions[1].Data).Select(each => each.Key));
        foreach (var record in answered)
        {
            var pushed = Written(snapshot.Partitions.Single(each => each.Partition == record["workspace"]!.GetValue<string>()).Data)[record["id"]!.GetValue<string>()]!.AsObject();
            Assert.Equal(AsksStateSlice.Fields.Order(), pushed.Select(each => each.Key).Order());
            Assert.All(AsksStateSlice.Fields.Where(field => field != "attachments"), field => Assert.Equal(record[field]?.ToJsonString(), pushed[field]?.ToJsonString()));
            Assert.Equal(
                """[{"name":"trace.log","sha256":"bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb","bytes":2300,"kept":true}]""",
                pushed["attachments"]!.ToJsonString());
        }
    }

    /// <summary>
    /// The allow-list's twins: every field the page's <c>Ask</c> type reads is pushed, and every field the service answers of an ask is
    /// pushed or named the pull's alone.
    /// </summary>
    [Fact]
    public void Every_field_the_page_reads_is_pushed_and_every_field_the_service_answers_is_classed()
    {
        var page = SourceFields.Page("Ask");
        var service = SourceFields.Service("AskResponse");

        Assert.Contains("workflowProposals", page);
        Assert.Equal("id", service[0]);
        Assert.Contains("words", service);
        Assert.Equal("short", service[^1]);
        Assert.Equal(page.Order(StringComparer.Ordinal), AsksStateSlice.Fields.Order(StringComparer.Ordinal));
        Assert.Empty(AsksStateSlice.Fields.Intersect(AsksStateSlice.PulledOnly));
        var classed = AsksStateSlice.Fields.Concat(AsksStateSlice.PulledOnly).ToHashSet(StringComparer.Ordinal);
        Assert.All(service, field => Assert.Contains(field, classed));
    }

    [Fact]
    public async Task A_hostile_field_the_person_s_words_and_a_file_s_path_never_reach_a_partition()
    {
        var hostile = Ask("a1", "default");
        hostile["token"] = "sk-hostile-top";
        hostile["wordsKeptFrom"] = "sk-hostile-kept";
        _service.Asks = QuestsStateSliceTests.List(hostile);
        var (_, slice) = await UpAsync();

        await slice.JudgeAsync();

        var pushed = JsonSerializer.Serialize(Pushes().Select(push => push.Data), IpcJson.Options);
        Assert.Contains("a1", pushed);
        Assert.DoesNotContain("sk-hostile", pushed);
        Assert.DoesNotContain("the person's own words", pushed);
        Assert.DoesNotContain("X:/daoris", pushed);
        Assert.DoesNotContain("\"path\"", pushed);
    }

    [Fact]
    public async Task Before_the_service_answers_the_slice_is_not_judged_and_pushes_nothing()
    {
        var loop = Loop();
        var slice = loop.Slices.Find(AsksStateSlice.Name)!;

        await slice.JudgeAsync();

        Assert.Equal(StateSlice.NotJudged, slice.Snapshot().Status);
        Assert.Empty(Pushes());
    }

    /// <summary>
    /// An ask a connector wrote reaches the database with no desktop dispatch: the change stream names the `asks` family and the slice
    /// pushes it. An ask is done once its quests closed and deletable while nothing stands on them, so the quests' and the sessions'
    /// families wake it too; the channel's, and a ref, move nothing.
    /// </summary>
    [Fact]
    public async Task A_connector_s_ask_heard_on_the_change_stream_is_pushed_with_no_desktop_dispatch()
    {
        var asks = Enumerable.Range(0, 10).Select(i => Ask($"a{i}", "default")).ToArray();
        _service.Asks = QuestsStateSliceTests.List(asks);
        var (loop, slice) = await UpAsync();
        using var stop = new CancellationTokenSource();
        var running = slice.RunAsync(stop.Token);
        await UntilAsync(() => Pushes().Count == 1);
        var dispatched = Raised.Count(each => each.Type is StatePublisher.Type);

        var judged = slice.Judgements;
        loop.HeardMove(ChangeTopics.Ledger, "channel");
        loop.HeardMove(ChangeTopics.Refs, "engine");
        await Task.Delay(300);
        Assert.Equal(judged, slice.Judgements);

        // The connector's ask, one delta of that ask alone.
        _service.Asks = QuestsStateSliceTests.List([.. asks, Ask("a-new", "default", "Open")]);
        loop.HeardMove(ChangeTopics.Ledger, "asks");
        await UntilAsync(() => Pushes().Count == 2);
        Assert.Equal(["a-new"], Written(Pushes()[^1].Data).Select(each => each.Key));
        Assert.Equal(1L, Pushes()[^1].BaseSeq);

        // Its quest closed: the ask is done, heard on the quests' family.
        var done = Ask("a-new", "default", "Done");
        _service.Asks = QuestsStateSliceTests.List([.. asks, done]);
        loop.HeardMove(ChangeTopics.Ledger, "quests");
        await UntilAsync(() => Pushes().Count == 3);
        Assert.Equal("Done", Written(Pushes()[^1].Data)["a-new"]!["state"]!.GetValue<string>());

        // Nothing stands on its quests any more: deletable, heard on the sessions' family.
        done["deletable"] = true;
        _service.Asks = QuestsStateSliceTests.List([.. asks, done]);
        loop.HeardMove(ChangeTopics.Ledger, "sessions");
        await UntilAsync(() => Pushes().Count == 4);
        Assert.True(Written(Pushes()[^1].Data)["a-new"]!["deletable"]!.GetValue<bool>());

        Assert.Equal(dispatched, Raised.Count(each => each.Type is StatePublisher.Type));
        await stop.CancelAsync();
        await running;
    }

    [Theory]
    [InlineData("STATE_CHANGED", """{"scopes":["asks"]}""", true)]
    [InlineData("STATE_CHANGED", """{"scopes":["quests"]}""", true)]
    [InlineData("STATE_CHANGED", """{"scopes":["sessions"]}""", false)]
    [InlineData("STATE_CHANGED", """{"scopes":["driver"]}""", false)]
    [InlineData("SESSION_ENDED", "{}", false)]
    [InlineData("DRIVER_READY", "{}", true)]
    public void What_wakes_the_asks_slice(string type, string payload, bool wakes)
    {
        var wake = AsksStateSlice.WakesOn.FirstOrDefault(each => each.Type == type);
        object told = type == StatePublisher.Type ? JsonSerializer.Deserialize<StateChanged>(payload, IpcJson.Options)! : new { };

        Assert.Equal(wakes, wake is not null && (wake.When is null || wake.When(told)));
    }
}
