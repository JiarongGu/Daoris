using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The background tasks a native stream says (AGENTPROVIDER1l, BGWAIT1c): each command's start and end read once from its frames into one
/// reading, the end in its notification's word or else its update's, marked where it came after its turn's result.
/// </summary>
public sealed class NativeTasksTests
{
    private static readonly string[] BackgroundCut = File.ReadAllLines(Path.Combine(
            WorkspaceRoot.Folder, "src", "Daoris.Desktop", "Daoris.Desktop.Driver.Tests", "fixtures", "native", "claude-background-cut.stream.jsonl"))
        .Where(line => line.Length > 0)
        .ToArray();

    private static List<WireReading> Read(IEnumerable<string> lines)
    {
        var tasks = new NativeTasks();
        return [.. lines.Select(line => tasks.Read(NativeFrame.Of(line))).OfType<WireReading>()];
    }

    /// <summary>The recorded turn's two commands: one ended before its turn's result, one ended by its harness after it.</summary>
    [Fact]
    public void Each_command_s_start_and_end_is_read_once_from_its_frames()
    {
        Assert.Equal(
            [
                new WireReading.TaskStarted("b0fetch") { Kind = "local_bash", Name = "git fetch" },
                new WireReading.TaskEnded("b0fetch", "completed") { Output = "X:/daoris/scratch/tasks/b0fetch.output" },
                new WireReading.TaskStarted("b1gates") { Kind = "local_bash", Name = "npm run gates" },
                new WireReading.TaskEnded("b1gates", "stopped") { Output = "X:/daoris/scratch/tasks/b1gates.output", AfterTurn = true },
            ],
            Read(BackgroundCut));
    }

    /// <summary>
    /// The wire repeating a start or an end, a subagent's task (a child's, never a command's), an end with no start and a line that is no frame
    /// are each read as nothing more; a notification that gives no word ends its task in its update's.
    /// </summary>
    [Fact]
    public void A_repeat_a_subagent_s_task_an_end_with_no_start_and_a_line_that_is_no_frame_say_nothing_more()
    {
        const string started = """{"type":"system","subtype":"task_started","task_id":"b1","description":"npm test","task_type":"local_bash"}""";

        var read = Read(
        [
            started, started,
            """{"type":"system","subtype":"task_started","task_id":"a1","description":"Read it.","task_type":"local_agent"}""",
            """{"type":"system","subtype":"task_notification","task_id":"a1","status":"completed"}""",
            """{"type":"system","subtype":"task_notification","task_id":"b9","status":"completed"}""",
            "not json",
            """{"type":"system","subtype":"task_updated","task_id":"b1","patch":{"status":"killed"}}""",
            """{"type":"system","subtype":"task_notification","task_id":"b1"}""",
            """{"type":"system","subtype":"task_notification","task_id":"b1","status":"stopped"}""",
        ]);

        Assert.Equal([new WireReading.TaskStarted("b1") { Kind = "local_bash", Name = "npm test" }, new WireReading.TaskEnded("b1", "killed")], read);
    }
}
