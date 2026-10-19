using System.Text.Json;
using System.Text.Json.Nodes;
using Daoris.Driver;
using Shenora.Core.Ipc;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// WAITSAY1c1: each working session's running call, pushed as it starts and ends (D182's PUSH1d2 note gave keyed slices; D152's
/// WAITSAY1b line gave the call). Judged from the loop's own record in memory, with no service, one partition keyed by session, and
/// woken only where a session's call moved.
/// </summary>
/// <remarks>Every call is the test's own event on the loop's record, each with its stamp. Nothing here starts a process or a service.</remarks>
public sealed class CallsStateSliceTests : DriverModuleBridge
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 19, 12, 0, 0, TimeSpan.Zero);

    private static SessionEvent Call(string id, string? status, string? title = null, int at = 0) =>
        new() { Kind = SessionEventKind.Tool, Id = id, Title = title, Status = status, At = T0.AddMinutes(at) };

    /// <summary>A loop with no service, and its calls' slice.</summary>
    private (DriverLoop Loop, StateSlice Slice) Up()
    {
        var loop = Loop();
        return (loop, loop.Slices.Find(CallsStateSlice.Name)!);
    }

    private IReadOnlyList<SlicePush> Pushes() =>
    [
        .. Raised.Where(each => each.Type == StateSlice.PushType).Select(each => (SlicePush)each.Payload!)
            .Where(push => push.Slice == CallsStateSlice.Name),
    ];

    private static JsonObject Written(object? data) => JsonSerializer.SerializeToNode(data, IpcJson.Options)!.AsObject();

    /// <summary>Start a call in each of <paramref name="sessions"/>, one minute apart.</summary>
    private static void Running(DriverLoop loop, params string[] sessions)
    {
        for (var i = 0; i < sessions.Length; i++) loop.Events.Append(sessions[i], Call("c1", "in_progress", "npm test", at: i));
    }

    [Fact]
    public async Task With_nothing_running_it_is_judged_at_once_as_one_empty_partition_with_no_service()
    {
        var (loop, slice) = Up();
        var module = new DriverModule(Bus, loop);

        await slice.JudgeAsync();
        var snapshot = slice.Snapshot();
        var answered = await AnswerAsync(module, "STATE_SLICE", new { slice = CallsStateSlice.Name });

        Assert.Null(loop.Service);
        Assert.Equal((CallsStateSlice.Name, StateSlice.Ready), (snapshot.Slice, snapshot.Status));
        Assert.Equal([CallsStateSlice.Partition], snapshot.Partitions.Select(each => each.Partition));
        Assert.Empty(Written(snapshot.Partitions[0].Data));
        // The snapshot route finds it in the loop's slices.
        Assert.Equal(CallsStateSlice.Partition, answered.GetProperty("partitions")[0].GetProperty("partition").GetString());
    }

    /// <summary>
    /// A running call is pushed under its session as <c>SESSION_GROUPS</c> writes a row's <c>calling</c>: its title and when it began,
    /// as the ISO string the page's clock reads; an open call with no name yet is no call, and nothing else of its events is pushed.
    /// </summary>
    [Fact]
    public async Task Each_running_call_is_pushed_under_its_session_as_its_title_and_since_and_nothing_else_of_its_events()
    {
        var (loop, slice) = Up();
        loop.Events.Append("s1", Call("c1", "in_progress", "npm test", at: 1) with { Output = "sk-hostile-output" });
        loop.Events.Append("s1", Call("c1", null, at: 2) with { Output = "sk-hostile-update", Text = "sk-hostile-text" });
        loop.Events.Append("s2", Call("c9", "pending", at: 3) with { Output = "sk-hostile-unnamed" });

        await slice.JudgeAsync();

        var partition = Written(Pushes().Single().Data);
        Assert.Equal(["s1"], partition.Select(each => each.Key));
        Assert.Equal(["title", "since", "waiting"], partition["s1"]!.AsObject().Select(each => each.Key));
        Assert.False(partition["s1"]!["waiting"]!.GetValue<bool>());
        Assert.Equal(("npm test", "2026-10-19T12:01:00+00:00"), (partition["s1"]!["title"]!.GetValue<string>(), partition["s1"]!["since"]!.GetValue<string>()));
        // The pull's twin: the same session's `SESSION_GROUPS` row writes its `calling` alike.
        var row = new SessionGrouping("s1", SessionGroup.Working, ShownState.GoingOn) { Calling = new RunningCall("npm test", T0.AddMinutes(1)) };
        Assert.Equal(
            JsonSerializer.SerializeToNode(SessionsRoutes.Grouped(row), IpcJson.Options)!["calling"]!.ToJsonString(),
            partition["s1"]!.ToJsonString());
        Assert.DoesNotContain("sk-hostile", JsonSerializer.Serialize(Pushes().Select(push => push.Data), IpcJson.Options));
    }

    /// <summary>
    /// The proof's first half: a call's start wakes the slice once and pushes that session alone, a delta on the number held; another
    /// session's words, output and words shown waiting wake nothing, since none moved a call.
    /// </summary>
    [Fact]
    public async Task A_call_s_start_wakes_the_slice_once_and_pushes_that_session_alone()
    {
        var (loop, slice) = Up();
        Running(loop, "s1", "s2", "s3");
        await slice.JudgeAsync();
        var woken = 0;
        using var hearing = CallsStateSlice.HearCalls(RunningCalls.Of(loop.Events), () => woken++);

        loop.Events.Append("s1", new SessionEvent { Kind = SessionEventKind.Message, Text = "running the suite" });
        loop.Events.Append("s2", Call("c1", null) with { Output = "PASS 12 of 40" });
        loop.Events.Append("s3", new SessionEvent { Kind = SessionEventKind.User, Origin = "person", Text = "also the readme", Reaches = "next-step", Id = "w1" });
        Assert.Equal(0, woken);

        loop.Events.Append("s4", Call("c1", "in_progress", "dotnet build", at: 9));
        Assert.Equal(1, woken);

        await slice.JudgeAsync();
        var moved = Pushes()[^1];
        Assert.Equal((CallsStateSlice.Partition, 2L, (long?)1L), (moved.Partition, moved.Seq, moved.BaseSeq));
        Assert.Equal(["s4"], Written(moved.Data).Select(each => each.Key));
        Assert.Empty(moved.Removed!);
    }

    /// <summary>
    /// The proof's second half: a call's end pushes its session among the keys gone; the last call's end pushes the partition whole and
    /// empty, never gone, so the page's list of calls empties.
    /// </summary>
    [Fact]
    public async Task A_call_s_end_pushes_its_session_removed_and_the_last_one_s_an_empty_partition()
    {
        var (loop, slice) = Up();
        Running(loop, "s1", "s2", "s3", "s4");
        await slice.JudgeAsync();

        loop.Events.Append("s2", Call("c1", "completed", at: 9));
        await slice.JudgeAsync();

        var ended = Pushes()[^1];
        Assert.Equal((2L, (long?)1L), (ended.Seq, ended.BaseSeq));
        Assert.Empty(Written(ended.Data));
        Assert.Equal(["s2"], ended.Removed);

        foreach (var session in new[] { "s1", "s3" }) loop.Events.Append(session, new SessionEvent { Kind = SessionEventKind.Turn, StopReason = "end_turn" });
        loop.Events.Append("s4", new SessionEvent { Kind = SessionEventKind.User, Origin = "person", Text = "go on", Id = "w2" });
        await slice.JudgeAsync();

        var emptied = Pushes()[^1];
        Assert.Equal((StateSlice.Ready, (long?)null), (emptied.Status, emptied.BaseSeq));
        Assert.Empty(Written(emptied.Data));
        Assert.Empty(Written(slice.Snapshot().Partitions.Single().Data));
    }

    /// <summary>
    /// AUTONOMY1b3b: a tool request held for the person wakes the slice and pushes its session's call marked waiting, timed from the
    /// hold, as <c>SESSION_GROUPS</c> writes the same row's <c>calling</c>; the answer pushes it running again. This is the page's wake
    /// to read what is held, in place of asking every few seconds.
    /// </summary>
    [Fact]
    public async Task A_held_request_pushes_its_call_waiting_and_its_answer_running_again_as_the_rows_twin()
    {
        var (loop, slice) = Up();
        Running(loop, "s1", "s2", "s3");
        await slice.JudgeAsync();
        var woken = 0;
        using var hearing = CallsStateSlice.HearCalls(RunningCalls.Of(loop.Events), () => woken++);
        var registry = HeldPermissions.Of(loop.Events);

        registry.For("s1").Hold("900", "c1", "Run npm test", [new("yes", "Allow", PermissionChoice.AllowOnce)]);
        Assert.Equal(1, woken);
        await slice.JudgeAsync();

        var held = Pushes()[^1];
        Assert.Equal(["s1"], Written(held.Data).Select(each => each.Key));
        var since = registry.Held("s1")[0].Since;
        Assert.Equal(("npm test", true), (Written(held.Data)["s1"]!["title"]!.GetValue<string>(), Written(held.Data)["s1"]!["waiting"]!.GetValue<bool>()));
        var row = new SessionGrouping("s1", SessionGroup.Working, ShownState.GoingOn) { Calling = new RunningCall("npm test", since) { Waiting = true } };
        Assert.Equal(
            JsonSerializer.SerializeToNode(SessionsRoutes.Grouped(row), IpcJson.Options)!["calling"]!.ToJsonString(),
            Written(held.Data)["s1"]!.ToJsonString());

        registry.Answer("s1", "900", "yes");
        Assert.Equal(2, woken);
        await slice.JudgeAsync();

        var answered = Written(Pushes()[^1].Data)["s1"]!;
        Assert.Equal(("2026-10-19T12:00:00+00:00", false), (answered["since"]!.GetValue<string>(), answered["waiting"]!.GetValue<bool>()));
    }

    /// <summary>Run as the loop runs it: a call's start reaches the windows with no bus event of its own, and a disposed hearing hears no more.</summary>
    [Fact]
    public async Task Running_a_call_s_start_is_pushed_from_the_record_alone_and_a_hearing_disposed_hears_no_more()
    {
        var (loop, slice) = Up();
        using var stop = new CancellationTokenSource();
        var running = slice.RunAsync(stop.Token);
        await UntilAsync(() => Pushes().Count == 1);

        loop.Events.Append("s1", Call("c1", "in_progress", "npm test", at: 1));

        await UntilAsync(() => Pushes().Count == 2);
        Assert.Equal(["s1"], Written(Pushes()[^1].Data).Select(each => each.Key));
        Assert.DoesNotContain(Raised, each => each.Type is StatePublisher.Type or DriverLoop.EndedType);
        await stop.CancelAsync();
        await running;

        var woken = 0;
        CallsStateSlice.HearCalls(RunningCalls.Of(loop.Events), () => woken++).Dispose();
        loop.Events.Append("s2", Call("c1", "in_progress", "npm test", at: 2));
        Assert.Equal(0, woken);
    }
}
