using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The line's one lock between Daoris's landing and the repository's merge tool: the tool's own file (<c>local/merge-run.lock</c>), taken by a
/// landing for its move of the line in the tool's shape, refused while a live run holds it, and taken back from a holder whose process is
/// gone. Files only, so it is the fast half's.
/// </summary>
public sealed class MergeRunLockTests : IDisposable
{
    private readonly string _root = Path.Combine(TestHomes.Root, "daoris-merge-run-" + Guid.NewGuid().ToString("N")[..8]);

    public MergeRunLockTests() => Directory.CreateDirectory(Path.Combine(_root, "local"));

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private static long StartOfThisProcess() => new DateTimeOffset(TreeLock.SelfStarted(), TimeSpan.Zero).ToUnixTimeMilliseconds();

    private const string At = "2026-11-21T10:00:00Z";

    private void Write(object? pid, object? started, string verb) =>
        File.WriteAllText(MergeRunLock.PathOf(_root), JsonSerializer.Serialize(new { pid, started, at = "2026-11-21T10:00:00Z", verb }));

    /// <summary>A free lock is taken in the tool's shape (pid, start in epoch milliseconds, time, verb), and let go by its holder.</summary>
    [Fact]
    public void A_free_lock_is_taken_in_the_tools_shape_and_let_go()
    {
        using (var taken = MergeRunLock.TryTake(_root, At, out var held))
        {
            Assert.NotNull(taken);
            Assert.Null(held);
            using var written = JsonDocument.Parse(File.ReadAllText(MergeRunLock.PathOf(_root)));
            Assert.Equal(Environment.ProcessId, written.RootElement.GetProperty("pid").GetInt32());
            Assert.Equal("landing", written.RootElement.GetProperty("verb").GetString());
            Assert.True(Math.Abs(written.RootElement.GetProperty("started").GetInt64() - StartOfThisProcess()) < 2000);
        }

        Assert.False(File.Exists(MergeRunLock.PathOf(_root)));
    }

    /// <summary>A run of the merge tool that lives holds the lock: the landing is refused, naming it, and leaves the file as it stood.</summary>
    [Fact]
    public void A_live_merge_run_holds_the_lock_and_the_landing_is_refused_naming_it()
    {
        Write(Environment.ProcessId, StartOfThisProcess(), "merge");
        var before = File.ReadAllText(MergeRunLock.PathOf(_root));

        using var taken = MergeRunLock.TryTake(_root, At, out var held);

        Assert.Null(taken);
        Assert.Contains($"pid {Environment.ProcessId}", held, StringComparison.Ordinal);
        Assert.Contains("a merge", held, StringComparison.Ordinal);
        Assert.Equal(before, File.ReadAllText(MergeRunLock.PathOf(_root)));
    }

    /// <summary>A holder with no start time is judged by its pid alone, as the tool does.</summary>
    [Fact]
    public void A_holder_with_no_start_time_is_judged_by_its_pid()
    {
        Write(Environment.ProcessId, null, "rerun");
        using var taken = MergeRunLock.TryTake(_root, At, out var held);
        Assert.Null(taken);
        Assert.NotNull(held);
    }

    /// <summary>A lock a killed holder left is taken back: its pid runs now as another process, begun elsewhen, or no process has it.</summary>
    [Fact]
    public void A_lock_a_killed_holder_left_is_taken_back()
    {
        var elsewhen = StartOfThisProcess() - (60 * 60 * 1000);
        Write(Environment.ProcessId, elsewhen, "merge");
        using (var again = MergeRunLock.TryTake(_root, At, out var held))
        {
            Assert.NotNull(again);
            Assert.Null(held);
            using var written = JsonDocument.Parse(File.ReadAllText(MergeRunLock.PathOf(_root)));
            Assert.Equal("landing", written.RootElement.GetProperty("verb").GetString());
        }

        Write(int.MaxValue - 1, StartOfThisProcess(), "merge");

        using var taken = MergeRunLock.TryTake(_root, At, out var heldAgain);
        Assert.NotNull(taken);
        Assert.Null(heldAgain);
    }

    /// <summary>A lock that does not read is no run's, and is replaced.</summary>
    [Fact]
    public void A_lock_that_does_not_read_is_replaced()
    {
        File.WriteAllText(MergeRunLock.PathOf(_root), "garbage");
        using var taken = MergeRunLock.TryTake(_root, At, out var held);
        Assert.NotNull(taken);
        Assert.Null(held);
    }

    /// <summary>A lock another run took since is not let go by a landing that finished: it removes only what it wrote.</summary>
    [Fact]
    public void A_landing_lets_go_only_of_what_it_wrote()
    {
        var taken = MergeRunLock.TryTake(_root, At, out _)!;
        Write(Environment.ProcessId, StartOfThisProcess(), "merge");

        taken.Dispose();

        Assert.True(File.Exists(MergeRunLock.PathOf(_root)));
    }

    /// <summary>A checkout with no <c>local</c> folder has no tool's lock to meet: nothing is taken, and no folder is made in it.</summary>
    [Fact]
    public void A_checkout_with_no_local_folder_takes_nothing_and_makes_none()
    {
        Directory.Delete(Path.Combine(_root, "local"));

        using var taken = MergeRunLock.TryTake(_root, At, out var held);

        Assert.NotNull(taken);
        Assert.Null(held);
        Assert.False(Directory.Exists(Path.Combine(_root, "local")));
    }
}
