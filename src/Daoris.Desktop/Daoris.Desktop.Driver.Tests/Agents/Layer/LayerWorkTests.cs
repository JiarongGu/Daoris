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
}
