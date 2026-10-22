using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Daoris.Driver;
using Shenora.Core.Ipc;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// Where each wired circle stands with its remote, pushed (PUSH1a2, D182 points 2 and 5): one owner in the shell's loop reading what
/// <c>/api/sync</c> answers for each circle the remotes map wires, a partition per circle, woken after each pass, by the change
/// stream's <c>quests</c> family and by the <c>sync</c> scope, so a tick reads <c>/api/sync</c> again in no window.
/// </summary>
/// <remarks>The service read through the driver's own client from a stand-in; the map written to the test's own home.</remarks>
public sealed partial class SyncStateSliceTests : DriverModuleBridge
{
    private readonly StandIn _service = new();

    /// <summary>What the route answers for a wired circle, as the service writes it: a null field left out.</summary>
    private static JsonObject Standing(string workspace, int ahead = 0, string? problem = null, string tried = "2026-10-20T09:00:00+00:00")
    {
        var answer = new JsonObject
        {
            ["workspace"] = workspace, ["wired"] = true, ["ahead"] = ahead, ["behind"] = new JsonArray("q7"),
            ["conflicts"] = new JsonArray(), ["synced"] = "2026-10-20T08:00:00+00:00", ["tried"] = tried,
        };
        if (problem is not null) answer["problem"] = problem;
        return answer;
    }

    /// <summary>The remotes map of the test's home, wiring <paramref name="circles"/>.</summary>
    private void Wire(params string[] circles) => RemoteTarget.Save(
        DaorisHome.Entry(Home, HomeEntry.Remotes),
        circles.ToDictionary(circle => circle, circle => new RemoteTarget($"https://{circle}.example", "sk-remote-key"), StringComparer.OrdinalIgnoreCase));

    /// <summary>A loop come up over the stand-in service, and its sync slice.</summary>
    private async Task<(DriverLoop Loop, StateSlice Slice)> UpAsync()
    {
        var loop = Loop();
        await loop.ComeUpAsync(new ServiceClient("http://stand-in", null, new HttpClient(_service)));
        return (loop, loop.Slices.Find(SyncStateSlice.Name)!);
    }

    private IReadOnlyList<SlicePush> Pushes() =>
    [
        .. Raised.Where(each => each.Type == StateSlice.PushType).Select(each => (SlicePush)each.Payload!)
            .Where(push => push.Slice == SyncStateSlice.Name),
    ];

    private static JsonObject Written(object? data) => JsonSerializer.SerializeToNode(data, IpcJson.Options)!.AsObject();

    [Fact]
    public async Task Each_wired_circle_is_a_partition_holding_what_the_route_answers_for_it()
    {
        Wire("games", "default");
        _service.Answer("default", Standing("default", ahead: 2));
        _service.Answer("games", Standing("games", problem: "the remote answered 503"));
        var (loop, slice) = await UpAsync();
        var module = new DriverModule(Bus, loop);

        await slice.JudgeAsync();
        var snapshot = slice.Snapshot();
        var answered = await AnswerAsync(module, "STATE_SLICE", new { slice = SyncStateSlice.Name });

        Assert.Equal((SyncStateSlice.Name, StateSlice.Ready), (snapshot.Slice, snapshot.Status));
        Assert.Equal(["default", "games"], snapshot.Partitions.Select(each => each.Partition));
        // The pull's answer and the push's, written alike: every field the route wrote, and none it left out.
        foreach (var circle in new[] { "default", "games" })
        {
            var pushed = Written(snapshot.Partitions.Single(each => each.Partition == circle).Data);
            var route = _service.Answered(circle);
            Assert.Equal(route.Select(each => each.Key).Order(StringComparer.Ordinal), pushed.Select(each => each.Key).Order(StringComparer.Ordinal));
            Assert.All(route, field => Assert.Equal(field.Value?.ToJsonString(), pushed[field.Key]?.ToJsonString()));
        }

        // The snapshot route finds it in the loop's slices; the key the map holds is never read into it.
        Assert.Equal("default", answered.GetProperty("partitions")[0].GetProperty("partition").GetString());
        Assert.DoesNotContain("sk-remote-key", answered.GetRawText());
        Assert.Equal(["/api/sync?workspace=default", "/api/sync?workspace=games"], _service.Asked.Order(StringComparer.Ordinal));
    }

    /// <summary>
    /// The allow-list's twins: every field the page's <c>SyncStanding</c> reads is pushed, every field the service's
    /// <c>SyncStandingResponse</c> answers is pushed or named the pull's alone, and the driver's own record reads each of them, read
    /// from the sources as the sessions' slice reads its twins.
    /// </summary>
    [Fact]
    public void Every_field_the_route_answers_is_classed_and_every_field_the_page_reads_is_pushed()
    {
        var page = Fields(Path.Combine("src", "Daoris.Web", "src", "data", "api.ts"), PageStanding(), "export type SyncStanding = {", PageField());
        var service = Fields(
            Path.Combine("src", "Daoris.Service", "Daoris.Service.Http", "ApiContracts.cs"), ServiceStanding(), "record SyncStandingResponse(",
            ServiceField(), camel: true);
        var driver = typeof(SyncStanding).GetProperties().Select(each => char.ToLowerInvariant(each.Name[0]) + each.Name[1..]);

        Assert.Equal("workspace", service[0]);
        Assert.Equal("undeclared", service[^1]);
        Assert.Contains("tried", page);
        Assert.Equal(page.Order(StringComparer.Ordinal), SyncStateSlice.Fields.Order(StringComparer.Ordinal));
        Assert.Empty(SyncStateSlice.Fields.Intersect(SyncStateSlice.PulledOnly));
        var classed = SyncStateSlice.Fields.Concat(SyncStateSlice.PulledOnly).ToHashSet(StringComparer.Ordinal);
        Assert.All(service, field => Assert.Contains(field, classed));
        Assert.Equal(service.Order(StringComparer.Ordinal), driver.Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task A_hostile_field_in_the_answer_never_reaches_a_partition()
    {
        Wire("default");
        var hostile = Standing("default");
        hostile["token"] = "sk-hostile-top";
        hostile["root"] = "X:/daoris/checkouts/engine";
        _service.Answer("default", hostile);
        var (_, slice) = await UpAsync();

        await slice.JudgeAsync();

        var pushed = JsonSerializer.Serialize(Pushes().Select(push => push.Data), IpcJson.Options);
        Assert.Contains("q7", pushed);
        Assert.DoesNotContain("sk-hostile", pushed);
        Assert.DoesNotContain("X:/daoris", pushed);
    }

    [Fact]
    public async Task Before_the_service_answers_the_slice_is_not_judged_and_pushes_nothing()
    {
        Wire("default");
        var loop = Loop();
        var slice = loop.Slices.Find(SyncStateSlice.Name)!;

        await slice.JudgeAsync();

        Assert.Equal(StateSlice.NotJudged, slice.Snapshot().Status);
        Assert.Empty(Pushes());
    }

    /// <summary>No circle wired: judged at once with no partition and no standing read, which the page reads as every circle unwired.</summary>
    [Fact]
    public async Task With_no_circle_wired_it_is_judged_with_no_partition_and_reads_no_standing()
    {
        var (_, slice) = await UpAsync();

        await slice.JudgeAsync();

        Assert.Equal(StateSlice.Ready, slice.Snapshot().Status);
        Assert.Empty(slice.Snapshot().Partitions);
        Assert.Empty(_service.Asked);
    }

    /// <summary>
    /// A pass rewrites when it last tried: the pass's own wake reads each circle again and pushes the one it moved. A pass that moved
    /// nothing pushes nothing, and a circle wired since the last judgement is read at the next pass.
    /// </summary>
    [Fact]
    public async Task A_pass_wakes_it_and_pushes_each_circle_it_moved_alone()
    {
        Wire("default", "games");
        _service.Answer("default", Standing("default"));
        _service.Answer("games", Standing("games"));
        var (loop, slice) = await UpAsync();
        using var stop = new CancellationTokenSource();
        var running = slice.RunAsync(stop.Token);
        await UntilAsync(() => Pushes().Count == 2);

        _service.Answer("games", Standing("games", tried: "2026-10-20T09:05:00+00:00"));
        loop.HeardPass();
        await UntilAsync(() => Pushes().Count == 3);
        Assert.Equal(("games", 2L), (Pushes()[^1].Partition, Pushes()[^1].Seq));
        Assert.Equal("2026-10-20T09:05:00+00:00", Written(Pushes()[^1].Data)["tried"]!.GetValue<string>());

        var judged = slice.Judgements;
        loop.HeardPass();
        await UntilAsync(() => slice.Judgements > judged);
        Assert.Equal(3, Pushes().Count);

        Wire("default", "games", "site");
        _service.Answer("site", Standing("site"));
        loop.HeardPass();
        await UntilAsync(() => Pushes().Count == 4);
        Assert.Equal("site", Pushes()[^1].Partition);
        await stop.CancelAsync();
        await running;
    }

    /// <summary>
    /// A quest written here by any door (a take, a close, a conflict's dismissal through the service's own door) moves what is ahead:
    /// the change stream's <c>quests</c> family wakes the slice. Another family, or a ref, judges nothing.
    /// </summary>
    [Fact]
    public async Task The_change_stream_s_quests_family_wakes_it_and_another_family_does_not()
    {
        Wire("default");
        _service.Answer("default", Standing("default"));
        var (loop, slice) = await UpAsync();
        using var stop = new CancellationTokenSource();
        var running = slice.RunAsync(stop.Token);
        await UntilAsync(() => Pushes().Count == 1);

        var judged = slice.Judgements;
        loop.HeardMove(ChangeTopics.Ledger, "sessions,asks");
        loop.HeardMove(ChangeTopics.Refs, "engine");
        await Task.Delay(300);
        Assert.Equal(judged, slice.Judgements);

        _service.Answer("default", Standing("default", ahead: 1));
        loop.HeardMove(ChangeTopics.Ledger, "channel,quests");

        await UntilAsync(() => Pushes().Count == 2);
        Assert.Equal(1, Written(Pushes()[^1].Data)["ahead"]!.GetValue<int>());
        await stop.CancelAsync();
        await running;
    }

    [Theory]
    [InlineData("STATE_CHANGED", """{"scopes":["sessions","quests","sync","registry"]}""", true)]
    [InlineData("STATE_CHANGED", """{"scopes":["quests"]}""", false)]
    [InlineData("STATE_CHANGED", """{"scopes":["driver"]}""", false)]
    [InlineData("DRIVER_READY", "{}", true)]
    [InlineData("DRIVER_TICK", "{}", false)]
    [InlineData("SESSION_ENDED", "{}", false)]
    public void What_wakes_the_sync_slice(string type, string payload, bool wakes)
    {
        var wake = SyncStateSlice.WakesOn.FirstOrDefault(each => each.Type == type);
        object told = type == StatePublisher.Type ? JsonSerializer.Deserialize<StateChanged>(payload, IpcJson.Options)! : new { };

        Assert.Equal(wakes, wake is not null && (wake.When is null || wake.When(told)));
    }

    /// <summary>
    /// The pass's own wake: the loop's one set tells its hearers at the end of every pass, the tick's and <i>Sync now</i>'s alike,
    /// one that found nothing to do among them.
    /// </summary>
    [Fact]
    public async Task Each_pass_through_the_loop_s_set_is_told_to_its_hearers()
    {
        using var set = new RemoteSyncSet([]);
        var told = 0;
        set.Passed += () => told++;

        await set.RunOnceAsync();
        await set.RunOnceAsync();

        Assert.Equal(2, told);
    }

    /// <summary>Walk up to the workspace root, which the tests run under.</summary>
    private static string RepositoryRoot()
    {
        var at = new DirectoryInfo(AppContext.BaseDirectory);
        while (at is not null && !File.Exists(Path.Combine(at.FullName, "daoris.json"))) at = at.Parent;
        return at?.FullName ?? throw new InvalidOperationException("no workspace root above the test binary");
    }

    /// <summary>The fields a declaration in <paramref name="file"/> names, its comments left out.</summary>
    private static IReadOnlyList<string> Fields(string file, Regex declaration, string declared, Regex field, bool camel = false)
    {
        var block = declaration.Match(File.ReadAllText(Path.Combine(RepositoryRoot(), file)));
        Assert.True(block.Success, $"{file} declares no `{declared}`");
        var body = Comments().Replace(block.Groups["body"].Value, "");
        return [.. field.Matches(body).Select(each => each.Groups["name"].Value).Select(name => camel ? char.ToLowerInvariant(name[0]) + name[1..] : name)];
    }

    [GeneratedRegex(@"export type SyncStanding = \{(?<body>.*?)\n\};", RegexOptions.Singleline)]
    private static partial Regex PageStanding();

    [GeneratedRegex(@"record SyncStandingResponse\((?<body>.*?)\);", RegexOptions.Singleline)]
    private static partial Regex ServiceStanding();

    [GeneratedRegex(@"/\*.*?\*/|//[^\n]*", RegexOptions.Singleline)]
    private static partial Regex Comments();

    [GeneratedRegex(@"(?<name>\w+)\??:")]
    private static partial Regex PageField();

    [GeneratedRegex(@"\b(?<name>[A-Z]\w*)\s*(?==|,|$)")]
    private static partial Regex ServiceField();

    /// <summary>The service's <c>GET /api/sync</c> standing in: each circle's answer as a test sets it, and what was asked.</summary>
    private sealed class StandIn : HttpMessageHandler
    {
        private readonly Lock _gate = new();
        private readonly Dictionary<string, string> _answers = new(StringComparer.Ordinal);
        private readonly List<string> _asked = [];

        public void Answer(string circle, JsonObject answer)
        {
            lock (_gate) _answers[circle] = answer.ToJsonString();
        }

        public JsonObject Answered(string circle)
        {
            lock (_gate) return JsonNode.Parse(_answers[circle])!.AsObject();
        }

        public IReadOnlyList<string> Asked
        {
            get
            {
                lock (_gate) return [.. _asked];
            }
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var asked = request.RequestUri!.PathAndQuery;
            string answer;
            lock (_gate)
            {
                _asked.Add(asked);
                answer = request.Method == HttpMethod.Get && asked.StartsWith("/api/sync?workspace=", StringComparison.Ordinal)
                    && _answers.TryGetValue(Uri.UnescapeDataString(asked["/api/sync?workspace=".Length..]), out var standing)
                        ? standing
                        : "{}";
            }

            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(answer, Encoding.UTF8, "application/json"),
            });
        }
    }
}
