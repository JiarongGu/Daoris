using System.Text;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A background task's output, read as its harness writes it (CONSOLE2). The wire carries none of it:
/// the task names a file, and the file is the only live view of a dev server
/// (docs/2026-09-28-console2-streams-evidence.md).
/// </summary>
public sealed class OutputTailTests : IDisposable
{
    private readonly string _folder = Path.Combine(
        Path.GetTempPath(), "daoris-tail-" + Guid.NewGuid().ToString("N")[..8]);

    public OutputTailTests() => Directory.CreateDirectory(_folder);

    public void Dispose()
    {
        try { Directory.Delete(_folder, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private static readonly TimeSpan Fast = TimeSpan.FromMilliseconds(20);

    /// <summary>Written by another process, as the harness writes it: shared, appended, flushed.</summary>
    private static void Write(string path, string text)
    {
        using var file = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
        var bytes = Encoding.UTF8.GetBytes(text);
        file.Write(bytes);
    }

    [Fact]
    public async Task Lines_come_out_as_the_file_grows_and_a_half_written_one_waits_for_its_end()
    {
        var path = Path.Combine(_folder, "task.output");
        var lines = new List<string>();
        Write(path, "bg tick 1\nbg tick 2\nbg ti");
        var tail = new OutputTail(path, text => { lock (lines) lines.Add(text); }, Fast);

        await Poll.Until(() => { lock (lines) return lines.Count == 2; }, () => string.Join("|", lines));
        Write(path, "ck 3\r\n");
        await Poll.Until(() => { lock (lines) return lines.Count == 3; }, () => string.Join("|", lines));
        await tail.FinishAsync();

        Assert.Equal(["bg tick 1", "bg tick 2", "bg tick 3"], lines);
    }

    /// <summary>The task names its file before the command has written a byte of it.</summary>
    [Fact]
    public async Task A_file_not_there_yet_is_waited_for()
    {
        var path = Path.Combine(_folder, "later.output");
        var lines = new List<string>();
        var tail = new OutputTail(path, text => { lock (lines) lines.Add(text); }, Fast);

        await Task.Delay(100);
        Write(path, "listening on 4200\n");

        await Poll.Until(() => { lock (lines) return lines.Count == 1; }, () => string.Join("|", lines));
        await tail.FinishAsync();
        Assert.Equal(["listening on 4200"], lines);
    }

    /// <summary>
    /// The end reads what is left, and a last line with no newline is said rather than lost: it is how
    /// the task ended, which is what a person reads its tab for.
    /// </summary>
    [Fact]
    public async Task Finishing_reads_the_rest_and_says_a_last_unfinished_line()
    {
        var path = Path.Combine(_folder, "ended.output");
        var lines = new List<string>();
        var tail = new OutputTail(path, text => { lock (lines) lines.Add(text); }, TimeSpan.FromMinutes(5));

        Write(path, "bg tick 20\n\n[exited with code 0]");
        await tail.FinishAsync();

        Assert.Equal(["bg tick 20", "", "[exited with code 0]"], lines);
    }

    /// <summary>A character split across two writes is one character, never two broken ones.</summary>
    [Fact]
    public async Task A_character_split_across_writes_is_read_whole()
    {
        var path = Path.Combine(_folder, "wide.output");
        var lines = new List<string>();
        var bytes = Encoding.UTF8.GetBytes("道衍\n");
        var tail = new OutputTail(path, text => { lock (lines) lines.Add(text); }, Fast);

        using (var file = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete))
        {
            file.Write(bytes, 0, 2);
            file.Flush();
            await Task.Delay(100);
            file.Write(bytes, 2, bytes.Length - 2);
        }

        await Poll.Until(() => { lock (lines) return lines.Count == 1; }, () => string.Join("|", lines));
        await tail.FinishAsync();
        Assert.Equal(["道衍"], lines);
    }

    /// <summary>Finishing twice is finishing once: a task's end and its session's end can both arrive.</summary>
    [Fact]
    public async Task Finishing_twice_says_nothing_twice()
    {
        var path = Path.Combine(_folder, "twice.output");
        var lines = new List<string>();
        Write(path, "once\n");
        var tail = new OutputTail(path, text => { lock (lines) lines.Add(text); }, TimeSpan.FromMinutes(5));

        await tail.FinishAsync();
        await tail.FinishAsync();

        Assert.Equal(["once"], lines);
    }
}
