using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// A session's live console over the bridge (`DriverModule.Console.cs`, MOD5): its backlog, its streams,
/// and a task's stop.
/// </summary>
public sealed class DriverModuleConsoleTests : DriverModuleBridge
{
    /// <summary>
    /// The console degrades to empty rather than failing — the page treats it as the part of the
    /// drawer that may be missing, and the record above it is what the drawer exists to show.
    /// </summary>
    [Fact]
    public async Task Tailing_a_session_nobody_has_heard_of_answers_an_empty_console()
    {
        var tail = await AnswerAsync(Module(), "TAIL_SESSION", new { id = "nothing-here" });

        Assert.Empty(tail.GetProperty("lines").EnumerateArray());
        Assert.False(tail.GetProperty("live").GetBoolean());
        Assert.Equal(0, tail.GetProperty("dropped").GetInt32());
    }

    /// <summary>
    /// CONSOLE2c: what a session runs beside itself is listed over the bridge, each with the key its
    /// console is tailed by, and a stream opening or ending is news the page hears, naming only the
    /// session, so a missed one costs a list the page asks for anyway.
    /// </summary>
    [Fact]
    public async Task A_sessions_streams_are_listed_and_a_stream_opening_or_ending_is_news()
    {
        var loop = Loop();
        var module = new DriverModule(Bus, loop);
        loop.Output.Open("s1", new SessionStream("task/t1", SessionStreamKind.Task, "dev server"));
        loop.Output.End("s1", "task/t1", "completed");
        loop.Output.Open("s1", new SessionStream("subagent/a", SessionStreamKind.Subagent, "reader"));

        var listed = await AnswerAsync(module, "SESSION_STREAMS", new { id = "s1" });

        Assert.Equal("s1", listed.GetProperty("session").GetString());
        var streams = listed.GetProperty("streams").EnumerateArray().ToList();
        Assert.Equal(["s1/task/t1", "s1/subagent/a"], streams.Select(s => s.GetProperty("key").GetString()));
        Assert.Equal(("task", "dev server", false, "completed"), (
            streams[0].GetProperty("kind").GetString(), streams[0].GetProperty("name").GetString(),
            streams[0].GetProperty("live").GetBoolean(), streams[0].GetProperty("state").GetString()));
        Assert.True(streams[1].GetProperty("live").GetBoolean());
        Assert.Equal(JsonValueKind.Null, streams[1].GetProperty("state").ValueKind);

        await UntilAsync(() => Raised.Count(m => m.Type == "SESSION_STREAMS") == 3);
        Assert.All(Raised.Where(m => m.Type == "SESSION_STREAMS"),
            m => Assert.Equal("""{"Session":"s1"}""", JsonSerializer.Serialize(m.Payload)));

        var none = await AnswerAsync(module, "SESSION_STREAMS", new { id = "nothing-here" });
        Assert.Empty(none.GetProperty("streams").EnumerateArray());
    }

    /// <summary>
    /// CONSOLE3a: a running task its harness said can be stopped is listed as one, and its tab's stop
    /// reaches the session that runs it, by the task's own id. Only a task of that session, and a
    /// session nothing here runs stops nothing, as an answer.
    /// </summary>
    [Fact]
    public async Task A_stoppable_task_is_listed_so_and_its_stop_reaches_its_session()
    {
        var loop = Loop();
        var module = new DriverModule(Bus, loop);
        loop.Output.Open("s1", new SessionStream("task/t1", SessionStreamKind.Task, "dev server", CanStop: true));
        loop.Output.Open("s1", new SessionStream("task/t2", SessionStreamKind.Task, "a probe"));
        loop.Output.Open("s1", new SessionStream("subagent/a", SessionStreamKind.Subagent, "reader"));

        var listed = (await AnswerAsync(module, "SESSION_STREAMS", new { id = "s1" })).GetProperty("streams").EnumerateArray().ToList();
        Assert.Equal([true, false, false], listed.Select(s => s.GetProperty("canStop").GetBoolean()));

        Assert.False((await AnswerAsync(module, "STOP_TASK", new { id = "s1", key = "s1/task/t1" })).GetProperty("stopped").GetBoolean());

        var asked = new List<string>();
        using var held = loop.Processes.OpenTaskStops("s1", (task, _) =>
        {
            asked.Add(task);
            return Task.FromResult(true);
        });
        Assert.True((await AnswerAsync(module, "STOP_TASK", new { id = "s1", key = "s1/task/t1" })).GetProperty("stopped").GetBoolean());
        Assert.Equal(["t1"], asked);

        Assert.Contains(Refusals.DriverRefused, await RefusalAsync(module, "STOP_TASK", new { id = "s1", key = "s1/subagent/a" }));
        Assert.Contains(Refusals.DriverRefused, await RefusalAsync(module, "STOP_TASK", new { id = "s1", key = "s2/task/t1" }));
        Assert.Equal(["t1"], asked);
    }
}
