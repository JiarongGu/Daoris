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
        ["HEAVY_SLOT"] = new { repository = "engine" },
        ["HELP_PROPOSALS"] = new { session = "a1b2c3d4" },
        ["TOOLS_LIST"] = new { },
    };

    public static TheoryData<string> HeldRoutes() => [.. DriverModule.ByEventRoutes.Order(StringComparer.Ordinal)];

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
        Assert.Equal(answered.GetProperty("answer").GetRawText(), JsonSerializer.Serialize(held.Data, Wire));

        // Asked again with the work slow again: what was held, at once; the same answer landing tells nothing more.
        var again = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        module.Held.BeforeWork = _ => again.Task;
        clock.Restart();
        var second = await module.HandleMessageAsync(Request(route, payload, "q1"), CancellationToken.None);
        Assert.True(clock.Elapsed < UiStallWatch.Bound, $"{route} answered after {clock.Elapsed.TotalMilliseconds} ms");
        Assert.Equal(answered.GetProperty("answer").GetRawText(), JsonSerializer.Serialize(second.Data, Wire));
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
