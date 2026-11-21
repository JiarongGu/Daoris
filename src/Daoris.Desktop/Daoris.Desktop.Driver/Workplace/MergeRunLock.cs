using System.Text;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// The line's one lock between Daoris's landing and the repository's merge tool: the tool's own file, <c>local/merge-run.lock</c> in the
/// checkout, which a run of the tool holds for its whole run and a landing holds for its move of the line.
/// </summary>
/// <remarks>
/// <para><b>Why.</b> A landing moved the line under a merge the tool had open in the checkout. <see cref="TreeLock"/> holds Daoris's own writers
/// from each other, and the tool never takes it; the tool's lock held its runs from each other, and a landing never read it. One file both
/// take closes it: a landing that finds a live run holds its move and says so (it is tried again at the next look, as every held writer is,
/// never waited for under <see cref="TreeLock"/>), and a run that finds a landing in flight waits for it (<c>tools/merge/checkout-lock.ts</c>).</para>
///
/// <para><b>The tool's shape.</b> <c>{ "pid", "started" (epoch milliseconds), "at", "verb" }</c>, put in place complete (written beside, moved into
/// place only where nothing is) and let go only while it still says what the landing wrote. The landing's <c>verb</c> is
/// <see cref="Verb"/>. A holder is dead when its pid is gone or runs as a process that began elsewhen, judged within the tool's own
/// two seconds (<see cref="Tolerance"/>) through the driver's liveness (<see cref="ProcessLiveness"/>), which a start the machine will not say reads as alive; a lock that does not read is no run's.
/// Taken back only while it still says what it said when judged dead.</para>
///
/// <para><b>Where a checkout keeps no <c>local</c> folder</b> there is no tool's lock to meet, and none is made: a folder is not made in a checkout
/// Daoris did not create for a tool that has never run there.</para>
/// </remarks>
internal sealed class MergeRunLock : IDisposable
{
    /// <summary>What a landing's lock says it is running: the tool waits for a holder of this verb, and refuses a second run of its own.</summary>
    internal const string Verb = "landing";

    /// <summary>How far two readings of one process's start may differ: the tool's own tolerance, which the platforms' rounding needs.</summary>
    internal static readonly TimeSpan Tolerance = TimeSpan.FromSeconds(2);

    private static readonly TimeSpan[] Readings = [TimeSpan.Zero, TimeSpan.FromSeconds(-1), TimeSpan.FromSeconds(1)];

    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    private readonly string? _path;
    private readonly string? _written;
    private int _gone;

    private MergeRunLock(string? path, string? written) => (_path, _written) = (path, written);

    /// <summary>Where the checkout's lock is.</summary>
    internal static string PathOf(string root) => Path.Combine(root, "local", "merge-run.lock");

    /// <summary>
    /// Hold the line of <paramref name="root"/>'s checkout for a move: the lock, or null while a live run holds it, <paramref name="at"/> the moment it says it is taken at, and <paramref name="held"/> then saying
    /// who (its pid, start and verb). A checkout with no <c>local</c> folder takes a lock that holds nothing.
    /// </summary>
    internal static MergeRunLock? TryTake(string root, string at, out string? held)
    {
        held = null;
        var path = PathOf(root);
        if (!Directory.Exists(Path.GetDirectoryName(path))) return new MergeRunLock(null, null);

        var started = new DateTimeOffset(TreeLock.SelfStarted(), TimeSpan.Zero).ToUnixTimeMilliseconds();
        var mine = JsonSerializer.Serialize(new { pid = Environment.ProcessId, started, at, verb = Verb });
        for (var attempt = 0; attempt < 3; attempt++)
        {
            if (Put(path, mine)) return new MergeRunLock(path, mine);

            var seen = Read(path);
            if (seen is not null && Holder(seen) is { } alive && Runs(alive.Pid, alive.Started))
            {
                held = $"pid {alive.Pid}, started {alive.At}, running a {alive.Verb}";
                return null;
            }

            // Dead, or unreadable: taken back only while it still says what it said when judged.
            if (Read(path) == seen) Remove(path);
        }

        var last = Read(path);
        held = last is not null && Holder(last) is { } latest ? $"pid {latest.Pid}, started {latest.At}, running a {latest.Verb}" : "a holder that does not read";
        return null;
    }

    private sealed record Run(int Pid, long? Started, string At, string Verb);

    private static Run? Holder(string text)
    {
        try
        {
            using var document = JsonDocument.Parse(text);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("pid", out var pid) || !pid.TryGetInt32(out var id)) return null;
            long? started = root.TryGetProperty("started", out var at) && at.ValueKind == JsonValueKind.Number && at.TryGetInt64(out var ms) ? ms : null;
            string Text(string name) => root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString()! : "?";
            return new Run(id, started, Text("at"), Text("verb"));
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Whether the process of <paramref name="pid"/> still is the one that began at <paramref name="started"/>; a record with no start counts as alive.</summary>
    private static bool Runs(int pid, long? started) =>
        pid > 0 && (started is not { } at || Readings.Any(shift => ProcessLiveness.Alive(pid, DateTimeOffset.FromUnixTimeMilliseconds(at).Add(shift).UtcTicks)));

    /// <summary>Written beside, then moved into place only where nothing is: never over another holder's lock, and never read half written.</summary>
    private static bool Put(string path, string text)
    {
        var beside = $"{path}.{Guid.NewGuid():N}.writing";
        try
        {
            File.WriteAllText(beside, text, Utf8);
            File.Move(beside, path, overwrite: false);
            return true;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return false;
        }
        finally
        {
            Remove(beside);
        }
    }

    private static string? Read(string path)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream, Utf8);
            return reader.ReadToEnd();
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static void Remove(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // A scanner holding it a moment: the next look removes it.
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _gone, 1) != 0 || _path is null) return;
        // Let go only while it still says what this landing wrote: a lock another run took since is theirs.
        for (var attempt = 0; attempt < 20 && Read(_path) == _written; attempt++)
        {
            Remove(_path);
            if (!File.Exists(_path)) return;
            Thread.Yield();
        }
    }
}
