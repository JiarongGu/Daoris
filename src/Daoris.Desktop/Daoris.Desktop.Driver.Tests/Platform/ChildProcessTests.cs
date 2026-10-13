using System.Diagnostics;
using System.Text;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The one child-process runner (DEDUP4a, survey §4), against a real child: a node one-liner stands in for any program.
/// The kill that raced a process's end was the defect the four hand-rolled copies carried.
/// </summary>
[Trait(Category.Name, Category.Process)]
public sealed class ChildProcessTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "daoris-child-" + Guid.NewGuid().ToString("N")[..8]);

    public ChildProcessTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private static ProcessStartInfo Node(string script, bool input = false)
    {
        var info = new ProcessStartInfo
        {
            FileName = "node",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = input,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        info.ArgumentList.Add("-e");
        info.ArgumentList.Add(script);
        return info;
    }

    /// <summary>A child that starts a grandchild, which writes <paramref name="marker"/> after three seconds unless it is ended first.</summary>
    private static string WithGrandchild(string marker)
    {
        var grand = "require('fs').writeFileSync(" + System.Text.Json.JsonSerializer.Serialize(marker) + ",'x')";
        return "require('child_process').spawn(process.execPath,['-e'," + System.Text.Json.JsonSerializer.Serialize("setTimeout(()=>{" + grand + "},3000)")
            + "],{stdio:'ignore'});setTimeout(()=>{},60000)";
    }

    [Fact]
    public async Task A_run_to_its_end_answers_its_exit_code_and_both_streams()
    {
        var run = await ChildProcess.RunAsync(
            Node("console.log('said');console.error('complained');process.exit(3)"), TimeSpan.FromSeconds(30), CancellationToken.None, closeInput: false);

        Assert.True(run.Started);
        Assert.False(run.TimedOut);
        Assert.Equal(3, run.ExitCode);
        Assert.Contains("said", run.Stdout);
        Assert.Contains("complained", run.Stderr);
    }

    [Fact]
    public async Task A_run_past_its_patience_is_timed_out_and_its_tree_ended()
    {
        var marker = Path.Combine(_root, "grandchild");
        var run = await ChildProcess.RunAsync(Node(WithGrandchild(marker)), TimeSpan.FromSeconds(1), CancellationToken.None, closeInput: false);

        Assert.True(run.Started);
        Assert.True(run.TimedOut);
        Assert.Null(run.ExitCode);
        await Task.Delay(TimeSpan.FromSeconds(4));
        Assert.False(File.Exists(marker), "the grandchild outlived the kill");
    }

    [Fact]
    public async Task A_process_ending_as_the_patience_runs_out_does_not_throw()
    {
        // The window is a few milliseconds wide, so the patience is swept across a child's whole life.
        for (var patience = 150; patience <= 450; patience += 15)
        {
            var run = await ChildProcess.RunAsync(
                Node("setTimeout(()=>{},200)"), TimeSpan.FromMilliseconds(patience), CancellationToken.None, closeInput: false);
            Assert.True(run.Started);
            Assert.True(run.TimedOut == (run.ExitCode is null));
        }
    }

    [Fact]
    public async Task A_missing_program_is_not_started_and_carries_its_error()
    {
        var info = Node("0");
        info.FileName = Path.Combine(_root, "no-such-program");

        var run = await ChildProcess.RunAsync(info, TimeSpan.FromSeconds(5), CancellationToken.None, closeInput: true);

        Assert.False(run.Started);
        Assert.NotNull(run.Error);
        Assert.Null(run.ExitCode);
    }

    [Fact]
    public async Task Closing_the_input_lets_a_program_that_waits_for_it_end()
    {
        var run = await ChildProcess.RunAsync(
            Node("process.stdin.on('end',()=>console.log('closed'));process.stdin.resume()", input: true),
            TimeSpan.FromSeconds(30), CancellationToken.None, closeInput: true);

        Assert.False(run.TimedOut);
        Assert.Equal(0, run.ExitCode);
        Assert.Contains("closed", run.Stdout);
    }

    [Fact]
    public async Task Cancellation_ends_the_tree_and_throws()
    {
        var marker = Path.Combine(_root, "grandchild");
        using var cancel = new CancellationTokenSource();
        cancel.CancelAfter(TimeSpan.FromMilliseconds(800));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            ChildProcess.RunAsync(Node(WithGrandchild(marker)), TimeSpan.FromSeconds(30), cancel.Token, closeInput: false));

        await Task.Delay(TimeSpan.FromSeconds(4));
        Assert.False(File.Exists(marker), "the grandchild outlived the cancellation");
    }
}

/// <summary>The runner's guarded kill, with no child started (the fast half).</summary>
public sealed class ChildProcessKillTests
{
    [Fact]
    public void Ending_a_process_that_never_ran_does_not_throw()
    {
        using var never = new Process();
        ChildProcess.KillTree(never);
    }
}
