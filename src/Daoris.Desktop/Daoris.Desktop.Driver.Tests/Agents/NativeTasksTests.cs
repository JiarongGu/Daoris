using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The background tasks a native stream says: each command's start and end read once from its frames into one
/// reading, the end in its notification's word or else its update's, marked where it came after its turn's result.
/// </summary>
public sealed class NativeTasksTests
{
    private static readonly string[] BackgroundCut = File.ReadAllLines(Path.Combine(
            WorkspaceRoot.Folder, "src", "Daoris.Desktop", "Daoris.Desktop.Driver.Tests", "fixtures", "native", "claude-background-cut.stream.jsonl"))
        .Where(line => line.Length > 0)
        .ToArray();

    private const string Result = """{"type":"result","subtype":"success","is_error":false}""";

    private static string Started(string id, string name) =>
        $$"""{"type":"system","subtype":"task_started","task_id":"{{id}}","description":"{{name}}","task_type":"local_bash"}""";

    private static string Updated(string id, string status) =>
        $$$"""{"type":"system","subtype":"task_updated","task_id":"{{{id}}}","patch":{"status":"{{{status}}}"}}""";

    private static List<WireReading> Read(IEnumerable<string> lines, NativeTasks? tasks = null)
    {
        tasks ??= new NativeTasks();
        return [.. lines.Select(line => tasks.Read(NativeFrame.Of(line))).OfType<WireReading>()];
    }

    /// <summary>What the run's own ending cuts of <paramref name="readings"/>.</summary>
    private static IReadOnlyList<string> Cut(IEnumerable<WireReading> readings)
    {
        var own = new HarnessWords();
        foreach (var reading in readings) own.Read(reading);
        return own.Ended(0).BackgroundCut;
    }

    /// <summary>
    /// The stream's close: a command its update killed after the turn's result, its notification never written because the process
    /// ended first (Daoris ending it at its turn's end), is ended in its update's word, which the run's ending cuts; one its update killed before
    /// the result is ended too, never cut; each said once.
    /// </summary>
    [Fact]
    public void A_command_its_update_ended_with_no_notification_after_is_ended_in_that_word_at_the_close()
    {
        var tasks = new NativeTasks();
        var read = Read([Started("b1", "npm run gates"), Started("b2", "git fetch"), Updated("b2", "killed"), Result, Updated("b1", "killed")], tasks);

        var closed = tasks.Close();

        Assert.Equal(
            [new WireReading.TaskStarted("b1") { Kind = "local_bash", Name = "npm run gates" }, new WireReading.TaskStarted("b2") { Kind = "local_bash", Name = "git fetch" }],
            read);
        Assert.Equal([new WireReading.TaskEnded("b1", "killed") { AfterTurn = true }, new WireReading.TaskEnded("b2", "killed")], closed);
        Assert.Empty(tasks.Close());
        Assert.Equal(["npm run gates"], Cut([.. read, .. closed]));
    }

    /// <summary>
    /// A command with no ending word at the close, started alone or updated only as running, says no end: its end is unsaid, which the run's
    /// ending reads as no cut, as it reads a stream that ends before its work's end is said; a command its notification ended says none again.
    /// </summary>
    [Fact]
    public void A_command_started_alone_at_the_close_says_no_end()
    {
        var tasks = new NativeTasks();
        var read = Read(
        [
            Started("b1", "npm run gates"), Started("b2", "dev server"), Updated("b2", "running"), Started("b3", "git fetch"), Updated("b3", "killed"),
            """{"type":"system","subtype":"task_notification","task_id":"b3","status":"stopped"}""", Result,
        ], tasks);

        Assert.Empty(tasks.Close());
        Assert.Empty(Cut(read));
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
