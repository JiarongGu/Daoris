using System.Text.Json;
using Daoris.Driver;
using Lyntai.Agents;
using Lyntai.Inference;
using SessionEnded = Lyntai.Agents.SessionEnded;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The children and background tasks a layer door's events say (<see cref="LayerWork"/>, read through <see cref="LayerReadings"/>): the
/// readings Daoris's native reader makes of the frames they stand for (<see cref="NativeChildren"/>, <see cref="NativeTasks"/>), each said
/// once. On events the tests make, beside those frames.
/// </summary>
public sealed class LayerWorkTests
{
    private static LayerReadings Readings() => new("native", TimeProvider.System, TimeSpan.FromHours(1));

    private static NativeFrame System(string subtype, object fields)
    {
        var node = JsonSerializer.SerializeToNode(fields)!.AsObject();
        node["type"] = "system";
        node["subtype"] = subtype;
        return NativeFrame.Of(node.ToJsonString());
    }

    [Fact]
    public void A_subagent_s_start_and_end_are_the_child_readings_the_native_reader_makes_of_its_frames()
    {
        var native = new NativeChildren();
        WireReading?[] expected =
        [
            native.Read(System("task_started", new
            {
                task_id = "a2", task_type = "local_agent", spawn_depth = 2, parent_task_id = "a1", tool_use_id = "toolu_2",
                subagent_type = "general-purpose", description = "look around",
            })),
            native.Read(System("task_notification", new { task_id = "a2", tool_use_id = "toolu_2", status = "completed" })),
        ];
        var translation = Readings();

        var read = translation.Read(new AgentTaskStarted("a2", "local_agent", "look around", "toolu_2")
            {
                ParentTaskId = "a1", Depth = 2, SubagentType = "general-purpose",
            })
            .Concat(translation.Read(new AgentTaskEnded("a2", "completed") { CallId = "toolu_2" }))
            .ToList();

        Assert.Equal(expected, read);
        Assert.Equal("toolu_2", Assert.IsType<WireReading.ChildSeen>(read[0]).Call);
    }

    [Fact]
    public void A_child_is_said_once_its_end_only_after_its_start_and_a_depth_of_none_is_none()
    {
        var translation = Readings();

        Assert.Empty(translation.Read(new AgentTaskEnded("a1", "completed")));
        var seen = Assert.IsType<WireReading.ChildSeen>(Assert.Single(translation.Read(new AgentTaskStarted("a1", "local_agent", null, null) { Depth = 0 })));
        Assert.Empty(translation.Read(new AgentTaskStarted("a1", "local_agent", null, null)));
        var ended = Assert.IsType<WireReading.ChildEnded>(Assert.Single(translation.Read(new AgentTaskEnded("a1", "completed"))));
        Assert.Empty(translation.Read(new AgentTaskEnded("a1", "completed")));

        Assert.Null(seen.Depth);
        Assert.Equal(("a1", ChildSignal.Stream, "completed"), (ended.Child, ended.Signal, ended.Word));
    }

    /// <summary>
    /// A command's start and end, its output file and whether the end came after the turn's terminal said with it, as the native reader
    /// says them; a task is no child, and a child no task.
    /// </summary>
    [Fact]
    public void A_background_command_s_start_and_end_are_the_task_readings_the_native_reader_makes_of_its_frames()
    {
        var native = new NativeTasks();
        var children = new NativeChildren();
        var frames = new[]
        {
            System("task_started", new { task_id = "b1", tool_use_id = "toolu_bg", description = "dev server", task_type = "local_bash" }),
            System("task_notification", new { task_id = "b1", tool_use_id = "toolu_bg", status = "completed", output_file = "out/b1.output" }),
            System("task_started", new { task_id = "b2", tool_use_id = "toolu_g", description = "gates", task_type = "local_bash" }),
            NativeFrame.Of("""{"type":"result","subtype":"success","is_error":false}"""),
            System("task_notification", new { task_id = "b2", tool_use_id = "toolu_g", status = "stopped" }),
        };
        var expected = frames.Select(frame => children.Read(frame) ?? native.Read(frame)).OfType<WireReading>().ToList();
        var translation = Readings();
        var read = new List<WireReading>();

        read.AddRange(translation.Read(new AgentTaskStarted("b1", "local_bash", "dev server", "toolu_bg")));
        read.AddRange(translation.Read(new AgentTaskOutputFile("b1", "out/b1.output")));
        read.AddRange(translation.Read(new AgentTaskEnded("b1", "completed") { CallId = "toolu_bg" }));
        read.AddRange(translation.Read(new AgentTaskStarted("b2", "local_bash", "gates", "toolu_g")));
        translation.Read(new SessionEnded(ProviderVerdict.Ok, false, "success", null, null, null));
        read.AddRange(translation.Read(new AgentTaskEnded("b2", "stopped") { CallId = "toolu_g" }));

        Assert.Equal(4, expected.Count);
        Assert.Equal(expected, read);
        Assert.True(Assert.IsType<WireReading.TaskEnded>(read[^1]).AfterTurn);
    }

    /// <summary>
    /// A command's update is its ending's first word: the notification that follows is the end said, and where none follows the close ends it
    /// in the update's last word, marked where that update came after the turn's terminal, as the native reader closes its stream.
    /// </summary>
    [Fact]
    public void A_command_its_update_ended_with_no_notification_is_ended_at_the_close_as_the_native_reader_ends_it()
    {
        var native = new NativeTasks();
        var frames = new[]
        {
            System("task_started", new { task_id = "b1", description = "gates", task_type = "local_bash" }),
            System("task_updated", new { task_id = "b1", patch = new { status = "running" } }),
            System("task_started", new { task_id = "b2", description = "dev", task_type = "local_bash" }),
            System("task_updated", new { task_id = "b2", patch = new { status = "running" } }),
            NativeFrame.Of("""{"type":"result","subtype":"success","is_error":false}"""),
            System("task_updated", new { task_id = "b1", patch = new { status = "killed" } }),
        };
        var expected = frames.Select(native.Read).OfType<WireReading>().ToList();
        expected.AddRange(native.Close());
        var translation = Readings();
        var read = new List<WireReading>();

        read.AddRange(translation.Read(new AgentTaskStarted("b1", "local_bash", "gates", null)));
        read.AddRange(translation.Read(new AgentTaskUpdated("b1", "running")));
        read.AddRange(translation.Read(new AgentTaskStarted("b2", "local_bash", "dev", null)));
        read.AddRange(translation.Read(new AgentTaskUpdated("b2", "running")));
        translation.Read(new SessionEnded(ProviderVerdict.Ok, false, "success", null, null, null));
        read.AddRange(translation.Read(new AgentTaskUpdated("b1", "killed")));
        read.AddRange(translation.Close());

        Assert.Equal(expected, read);
        var ended = Assert.IsType<WireReading.TaskEnded>(Assert.Single(read.OfType<WireReading.TaskEnded>()));
        Assert.Equal(("b1", "killed", true), (ended.Task, ended.Word, ended.AfterTurn));
    }

    [Fact]
    public void A_notification_after_an_update_is_the_end_and_the_close_ends_it_no_second_time()
    {
        var translation = Readings();

        translation.Read(new AgentTaskStarted("b1", "local_bash", "gates", null));
        translation.Read(new AgentTaskUpdated("b1", "killed"));
        var ended = Assert.IsType<WireReading.TaskEnded>(Assert.Single(translation.Read(new AgentTaskEnded("b1", "stopped"))));

        Assert.Equal("stopped", ended.Word);
        Assert.Empty(translation.Close());
        Assert.Empty(Readings().Read(new AgentTaskUpdated("never-started", "killed")));
    }
}
