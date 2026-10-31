using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using Daoris.Driver;
using Shenora.Core.Ipc;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// Nothing the window asks for waits on work (NOBLOCK1, D56's NOBLOCK1 note): a read marked <c>[ByEvent]</c> is answered at once
/// from what is held, its work runs on a task of its own, and its answer is told as <c>DAORIS.ANSWERED</c>, naming the page's query.
/// </summary>
/// <remarks>
/// Each route's work is made slow by the seam every held read passes (<see cref="HeldAnswers.BeforeWork"/>), held until the test
/// lets it go, so the ask is timed against the window's bound (<see cref="UiStallWatch.Bound"/>) while the work is under way.
/// </remarks>
public sealed class DriverModuleHeldTests : DriverModuleBridge
{
    /// <summary>A payload each held route answers from a bare home, by route; a held route missing here fails its theory.</summary>
    private static readonly Dictionary<string, object> Payloads = new(StringComparer.Ordinal)
    {
        ["SESSION_WHERE"] = new { sessions = Array.Empty<object>() },
        ["SESSION_OPENINGS"] = new { ids = new[] { "a1b2c3d4" } },
        ["SESSION_RECORD"] = new { id = "a1b2c3d4" },
        ["HEAVY_SLOT"] = new { repository = "engine" },
        ["ROLES"] = new { },
        ["HELP_PROPOSALS"] = new { session = "a1b2c3d4" },
        ["TOOLS_LIST"] = new { },
        ["SUPERVISOR_WAITING"] = new { },
        ["INTEGRATION"] = new { repository = "engine" },
        ["SESSION_AT_WORK"] = new { session = "a1b2c3d4" },
        ["SESSION_RELATED"] = new { session = "a1b2c3d4" },
        ["SESSION_MEMORY"] = new { session = "a1b2c3d4" },
        ["TOGETHER"] = new { session = "a1b2c3d4" },
    };

    public static TheoryData<string> HeldRoutes() => [.. DriverModule.ByEventRoutes.Order(StringComparer.Ordinal)];

    private List<JsonElement> Heard(out IDisposable hearing)
    {
        var told = new List<JsonElement>();
        hearing = Bus.Subscribe(HeldAnswers.Module, HeldAnswers.Event, message =>
        {
            lock (told) told.Add(JsonSerializer.SerializeToElement(message.Payload, Wire));
            return Task.CompletedTask;
        });
        return told;
    }

    private static JsonElement Of(object? answer) => JsonSerializer.SerializeToElement(answer, Wire);

    /// <summary>
    /// An answer less a numbered read's number (COWORKVIEW1a): a reply carries the newest landing's, which a work landing unchanged
    /// after the event still moves, so the two are compared by what they say.
    /// </summary>
    private static string Unnumbered(string json)
    {
        if (System.Text.Json.Nodes.JsonNode.Parse(json) is not System.Text.Json.Nodes.JsonObject answer) return json;
        answer.Remove("at");
        return answer.ToJsonString();
    }

    /// <summary>
    /// COWORKVIEW1a's numbered read (the design §4.1, C4-2.3, C4-3.1): a work's failure is answered, never thrown, as the failed value
    /// with the refusal's words at every ask until a landing replaces it, told so too; every reply and event carries its epoch and number.
    /// </summary>
    [Fact]
    public async Task A_numbered_read_answers_a_failure_with_its_words_at_every_ask_and_never_throws()
    {
        var held = new HeldAnswers(Bus) { Patience = TimeSpan.FromSeconds(5) };
        var told = Heard(out var hearing);
        using var _ = hearing;
        Task<object?> Fails(CancellationToken ct) => throw new InvalidOperationException("the lineage could not be read");

        var first = Of(await held.AskAsync("k", "q1", Fails, numbered: true));
        var again = Of(await held.AskAsync("k", "q1", Fails, numbered: true));

        Assert.Equal("the lineage could not be read", first.GetProperty("failed").GetProperty("why").GetString());
        Assert.Equal(held.Epoch, first.GetProperty("at").GetProperty("epoch").GetString());
        Assert.Equal("the lineage could not be read", again.GetProperty("failed").GetProperty("why").GetString());
        await UntilAsync(() => { lock (told) return told.Count >= 1; });
        lock (told) Assert.Equal("the lineage could not be read", told[0].GetProperty("answer").GetProperty("failed").GetProperty("why").GetString());
    }

    /// <summary>
    /// The design §4.1 (O4-F3, O4-F12a): a failure over a held answer keeps it, answered and told stale with the refusal's words, and
    /// the first landing after it is told even where it equals the held answer, which clears the stale; the numbers only grow.
    /// </summary>
    [Fact]
    public async Task A_failure_over_a_held_answer_reads_it_stale_until_the_next_landing_which_is_told_even_unchanged()
    {
        var held = new HeldAnswers(Bus) { Patience = TimeSpan.FromSeconds(5) };
        var told = Heard(out var hearing);
        using var _ = hearing;
        var fail = false;
        Task<object?> Work(CancellationToken ct) => fail ? throw new InvalidOperationException("refused") : Task.FromResult<object?>(new { Rows = 1 });

        var answered = Of(await held.AskAsync("k", "q1", Work, numbered: true));
        fail = true;
        await held.AskAsync("k", "q1", Work, numbered: true);
        await held.LandedAsync("k");
        var stale = Of(await held.AskAsync("k", "q1", Work, numbered: true));
        await held.LandedAsync("k");
        fail = false;
        await held.AskAsync("k", "q1", Work, numbered: true);
        await held.LandedAsync("k");
        var recovered = Of(await held.AskAsync("k", "q1", Work, numbered: true));

        Assert.Equal(1, stale.GetProperty("rows").GetInt32());
        Assert.Equal("refused", stale.GetProperty("stale").GetProperty("why").GetString());
        Assert.False(recovered.TryGetProperty("stale", out var _2));
        Assert.True(recovered.GetProperty("at").GetProperty("n").GetInt64() > answered.GetProperty("at").GetProperty("n").GetInt64());
        List<JsonElement> events;
        lock (told) events = [.. told];
        Assert.Contains(events, each => each.GetProperty("answer").TryGetProperty("stale", out var why) && why.GetProperty("why").GetString() == "refused");
        Assert.False(events[^1].GetProperty("answer").TryGetProperty("stale", out var _3));
    }

    /// <summary>
    /// COWORKVIEW1a rule 14: an ask is answered within the window's bound while a 40-member answer is composed beside three other held
    /// reads, the 40 rows are told once when they land, and the same composition again tells nothing.
    /// </summary>
    [Fact]
    public async Task An_ask_is_answered_within_the_bound_while_a_forty_member_answer_lands_beside_three_other_held_reads()
    {
        var held = new HeldAnswers(Bus) { Patience = TimeSpan.FromMilliseconds(50) };
        var told = Heard(out var hearing);
        using var _ = hearing;
        var rows = Enumerable.Range(0, 40).Select(i => new { Key = $"a{i:00}", State = "working" }).ToArray();
        async Task<object?> Forty(CancellationToken ct)
        {
            await Task.Delay(400, ct);
            return new { Rows = rows };
        }

        async Task<object?> Other(CancellationToken ct)
        {
            await Task.Delay(400, ct);
            return new { Other = 1 };
        }

        var clock = Stopwatch.StartNew();
        var others = new[] { "o1", "o2", "o3" }.Select(key => held.AskAsync(key, key, Other)).ToArray();
        var first = await held.AskAsync("k", "q1", Forty, numbered: true);
        await Task.WhenAll(others);
        Assert.True(clock.Elapsed < UiStallWatch.Bound, $"the asks answered after {clock.Elapsed.TotalMilliseconds} ms");
        Assert.True(HeldAnswers.IsPending(first));

        await UntilAsync(() => { lock (told) return told.Count(each => each.GetProperty("held").GetString() == "q1") >= 1; });
        JsonElement landed;
        lock (told) landed = told.First(each => each.GetProperty("held").GetString() == "q1");
        Assert.Equal(40, landed.GetProperty("answer").GetProperty("rows").GetArrayLength());

        await held.LandedAsync("k");
        await Task.Delay(100);
        lock (told) Assert.Single(told, each => each.GetProperty("held").GetString() == "q1");
    }

    [Theory]
    [MemberData(nameof(HeldRoutes))]
    public async Task A_held_read_answers_within_the_windows_bound_while_its_slow_work_is_told_by_event(string route)
    {
        Assert.True(Payloads.TryGetValue(route, out var payload), $"{route} is told by event and has no payload in this test's table");
        var module = new DriverModule(Bus, Loop()) { Patience = TimeSpan.Zero };
        var slow = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        module.Held.BeforeWork = _ => slow.Task;
        var told = new ConcurrentQueue<JsonElement>();
        using var heard = Bus.Subscribe(HeldAnswers.Module, HeldAnswers.Event, message =>
        {
            told.Enqueue(JsonSerializer.SerializeToElement(message.Payload, Wire));
            return Task.CompletedTask;
        });

        // Nothing held yet, and the work held back: the ask answers pending, at once.
        var clock = Stopwatch.StartNew();
        var first = await module.HandleMessageAsync(Request(route, payload, "q1"), CancellationToken.None);
        Assert.True(clock.Elapsed < UiStallWatch.Bound, $"{route} answered after {clock.Elapsed.TotalMilliseconds} ms");
        Assert.True(first.Success, first.Error?.Message);
        Assert.True(HeldAnswers.IsPending(first.Data));
        Assert.Empty(told);

        // The work answers: told once, naming the query that asked, with what an ask now answers.
        slow.SetResult();
        await UntilAsync(() => !told.IsEmpty);
        var answered = Assert.Single(told);
        Assert.Equal("q1", answered.GetProperty("held").GetString());
        if (answered.TryGetProperty("failed", out _))
        {
            // A work the bare home refuses (one that needs the service): told failed, and the next ask is that refusal, at once.
            clock.Restart();
            Assert.False((await module.HandleMessageAsync(Request(route, payload, "q1"), CancellationToken.None)).Success);
            Assert.True(clock.Elapsed < UiStallWatch.Bound, $"{route} refused after {clock.Elapsed.TotalMilliseconds} ms");
            return;
        }

        var held = await module.HandleMessageAsync(Request(route, payload, "q1"), CancellationToken.None);
        Assert.Equal(Unnumbered(answered.GetProperty("answer").GetRawText()), Unnumbered(JsonSerializer.Serialize(held.Data, Wire)));

        // Asked again with the work slow again: what was held, at once; the same answer landing tells nothing more.
        var again = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        module.Held.BeforeWork = _ => again.Task;
        clock.Restart();
        var second = await module.HandleMessageAsync(Request(route, payload, "q1"), CancellationToken.None);
        Assert.True(clock.Elapsed < UiStallWatch.Bound, $"{route} answered after {clock.Elapsed.TotalMilliseconds} ms");
        Assert.Equal(Unnumbered(answered.GetProperty("answer").GetRawText()), Unnumbered(JsonSerializer.Serialize(second.Data, Wire)));
        again.SetResult();
        await module.Held.LandedAsync(DriverModule.HeldKey(Request(route, payload, "q1")));
        await Task.Delay(50);
        Assert.Single(told);
    }

    /// <summary>
    /// SESSION_WHERE's look: a landing recorded after the first answer is held is told by event, and the ask after it answers it
    /// at once; the window was held 65.9 s by this read before (the install's <c>ui.stalled</c>).
    /// </summary>
    [Fact]
    public async Task Where_a_session_landed_since_is_told_by_event_and_then_answered_at_once()
    {
        var loop = Loop();
        var trees = new SessionTrees(loop.Home);
        var tree = Path.Combine(trees.TreesRoot, "aurora", "engine", "s-2394e5d9");
        var module = new DriverModule(Bus, loop) { Patience = TimeSpan.Zero };
        var told = new ConcurrentQueue<JsonElement>();
        using var heard = Bus.Subscribe(HeldAnswers.Module, HeldAnswers.Event, message =>
        {
            told.Enqueue(JsonSerializer.SerializeToElement(message.Payload, Wire));
            return Task.CompletedTask;
        });
        var payload = new { sessions = new[] { new { id = "landed1", tree } } };

        Assert.True(HeldAnswers.IsPending((await module.HandleMessageAsync(Request("SESSION_WHERE", payload, "where"), CancellationToken.None)).Data));
        await UntilAsync(() => told.Count == 1);
        Assert.Equal(JsonValueKind.Null, Row(told.Last()).GetProperty("landed").ValueKind);

        var at = DateTimeOffset.Parse("2026-10-01T08:00:00Z", System.Globalization.CultureInfo.InvariantCulture);
        trees.Recorded.Record(new LandedBranch("engine", "aurora", "feature/42-streamer", "main", "abc123", "landed1", "42", null, at));
        // The landing's press tells its scope, one of the inputs the route declares (FOUNDATION1s).
        StatePublisher.Publish(Bus, StateScopes.Landing);
        // What was held, at once; the look's answer, by event.
        var stale = JsonSerializer.SerializeToElement((await module.HandleMessageAsync(Request("SESSION_WHERE", payload, "where"), CancellationToken.None)).Data, Wire);
        Assert.Equal(JsonValueKind.Null, stale.GetProperty("sessions")[0].GetProperty("landed").ValueKind);
        await UntilAsync(() => told.Count == 2);
        Assert.Equal("feature/42-streamer", Row(told.Last()).GetProperty("landed").GetProperty("branch").GetString());
        var now = JsonSerializer.SerializeToElement((await module.HandleMessageAsync(Request("SESSION_WHERE", payload, "where"), CancellationToken.None)).Data, Wire);
        Assert.Equal("feature/42-streamer", now.GetProperty("sessions")[0].GetProperty("landed").GetProperty("branch").GetString());
        // Never a machine path told: the event names the query, not the trees the page sent.
        Assert.DoesNotContain(JsonSerializer.Serialize(Home).Trim('"'), told.Last().GetRawText(), StringComparison.OrdinalIgnoreCase);

        static JsonElement Row(JsonElement told) => told.GetProperty("answer").GetProperty("sessions")[0];
    }

    /// <summary>
    /// FOUNDATION1s: an answer held at its inputs' versions is served with no work while they stand, and made again once one moves; a
    /// version unknown (null) works at every ask, as a route declaring nothing does.
    /// </summary>
    [Fact]
    public async Task A_held_answer_is_served_with_no_work_while_its_inputs_stand_and_made_again_when_one_moves()
    {
        var held = new HeldAnswers(Bus) { Patience = TimeSpan.FromSeconds(5) };
        var version = "v1";
        var works = 0;
        Task<object?> Work(CancellationToken _) => Task.FromResult<object?>($"answer{Interlocked.Increment(ref works)}");

        Assert.Equal("answer1", await held.AskAsync("k", "q", Work, () => version));
        await held.LandedAsync("k");
        Assert.Equal("answer1", await held.AskAsync("k", "q", Work, () => version));
        await held.LandedAsync("k");
        Assert.Equal(1, works);

        version = "v2";
        Assert.Equal("answer1", await held.AskAsync("k", "q", Work, () => version));
        await held.LandedAsync("k");
        Assert.Equal("answer2", await held.AskAsync("k", "q", Work, () => version));
        await held.LandedAsync("k");
        Assert.Equal(2, works);

        Assert.Equal("answer2", await held.AskAsync("k", "q", Work, () => null));
        await held.LandedAsync("k");
        Assert.Equal(3, works);
        Assert.Equal(2, held.Served);
    }

    /// <summary>
    /// FOUNDATION1s: <c>SESSION_WHERE</c> declares the sessions, the landings and the clean-up as what its answer reads, so a change to
    /// one of them makes it again, and any other change serves it held with no work.
    /// </summary>
    [Fact]
    public async Task Where_is_made_again_on_its_declared_scopes_and_served_held_on_any_other()
    {
        var module = new DriverModule(Bus, Loop()) { Patience = TimeSpan.FromSeconds(5) };
        var works = 0;
        module.Held.BeforeWork = _ =>
        {
            Interlocked.Increment(ref works);
            return Task.CompletedTask;
        };
        var request = Request("SESSION_WHERE", new { sessions = Array.Empty<object>() }, "where");

        async Task Ask()
        {
            Assert.True((await module.HandleMessageAsync(request, CancellationToken.None)).Success);
            await module.Held.LandedAsync(DriverModule.HeldKey(request));
        }

        await Ask();
        await Ask();
        Assert.Equal(1, works);

        StatePublisher.Publish(Bus, StateScopes.Browser | StateScopes.Tools);
        await Ask();
        Assert.Equal(1, works);

        StatePublisher.Publish(Bus, StateScopes.Sweep);
        await Ask();
        Assert.Equal(2, works);
    }

    /// <summary>A press overtakes the work under way: the ask after it begins its own, and an older work landing late moves nothing.</summary>
    [Fact]
    public async Task A_work_begun_before_a_change_is_joined_no_more_and_never_overwrites_a_newer_one()
    {
        var held = new HeldAnswers(Bus) { Patience = TimeSpan.Zero };
        var older = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        Assert.True(HeldAnswers.IsPending(await held.AskAsync("k", "q", _ => older.Task)));
        held.Forget();
        var newer = 0;
        Assert.True(HeldAnswers.IsPending(await held.AskAsync("k", "q", _ => { newer++; return Task.FromResult<object?>("newer"); })));
        await UntilAsync(() => newer == 1);
        await held.LandedAsync("k");
        older.SetResult("older");
        await Task.Delay(50);
        Assert.Equal("newer", await held.AskAsync("k", "q", _ => Task.FromResult<object?>("newer")));
    }

    /// <summary>A work that fails with nothing held is the next ask's refusal, and told as failed so the page asks again.</summary>
    [Fact]
    public async Task A_failed_work_with_nothing_held_is_told_and_then_refused()
    {
        var held = new HeldAnswers(Bus) { Patience = TimeSpan.Zero };
        var told = new ConcurrentQueue<JsonElement>();
        using var heard = Bus.Subscribe(HeldAnswers.Module, HeldAnswers.Event, message =>
        {
            told.Enqueue(JsonSerializer.SerializeToElement(message.Payload, Wire));
            return Task.CompletedTask;
        });
        Assert.True(HeldAnswers.IsPending(await held.AskAsync("k", "q", _ => throw new DriverException("no record"))));
        await UntilAsync(() => !told.IsEmpty);
        Assert.True(told.Single().GetProperty("failed").GetBoolean());
        await Assert.ThrowsAsync<DriverException>(() => held.AskAsync("k", "q", _ => Task.FromResult<object?>("now")));
    }

    private static readonly JsonSerializerOptions Wire = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private static IpcRequest Request(string route, object payload, string held)
    {
        var element = JsonSerializer.SerializeToElement(payload);
        var fields = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(element.GetRawText())!;
        fields[HeldAnswers.HeldField] = JsonSerializer.SerializeToElement(held);
        return new IpcRequest
        {
            Id = Guid.NewGuid().ToString("N")[..8],
            Module = DriverModule.Name,
            Type = route,
            Payload = JsonSerializer.SerializeToElement(fields),
        };
    }
}
