using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// One writer at a time from the re-read to the rename of a home file an edit keeps (CONFIGLOCK1, D173's CONFIG1 note). A keeping write
/// reads the file again, makes its edit onto it and renames its own text over it (<see cref="DriverConfig.Save"/>,
/// <see cref="JsonKeeping.Save"/>); another writer whose rename landed between that read and that rename was lost. The CLI's twin is
/// <c>keeplock.ts</c>, and both read this suite's tests' <c>fixtures/driver-config-keeps.json</c>'s <c>lock</c> rows.
/// </summary>
/// <remarks>
/// <para><b>Where:</b> <c>&lt;file&gt;.lock</c>, the file's full path with <c>.lock</c> after it, beside the file whoever names it: a
/// writer is handed a file, not a home, so two writers of one file meet at one lock wherever it lives. Never <c>driver.lock</c>, an older
/// build's lease.</para>
///
/// <para><b>Not <see cref="RecordLock"/>.</b> Its handle opened with no sharing is a lock Node cannot hold, and two Node writers would not
/// exclude each other through one. This lock is the file's being there: put in place with no overwrite, written beside first so it is
/// never seen half written (twins.md: C# moves, Node links), saying <c>{ "pid", "since" }</c>, and removed by its holder only while it
/// still says what that holder wrote.</para>
///
/// <para><b>Waited for, briefly, then refused:</b> a write holds it for milliseconds, so a writer that finds it looks again until
/// <see cref="Wait"/> has passed, then says so as a tool error and writes nothing. <b>Stale</b> (<see cref="IsStale"/>): its process gone,
/// written more than <see cref="Stale"/> ago, or not that object; it is removed only while it still says what was judged stale, then
/// taken. The writers of one process queue on a gate first, as <see cref="RecordList{T}"/>'s do, so the file is the lock between
/// processes.</para>
/// </remarks>
internal sealed class KeepLock : IDisposable
{
    /// <summary>How long a writer waits for another's edit before it is refused: <see cref="RecordLock.Wait"/>'s, as the CLI's.</summary>
    public static readonly TimeSpan Wait = RecordLock.Wait;

    /// <summary>How old a lock may be before it is taken for one whose holder died: a write holds it for milliseconds.</summary>
    public static readonly TimeSpan Stale = TimeSpan.FromMinutes(1);

    private static readonly TimeSpan Poll = TimeSpan.FromMilliseconds(15);

    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    /// <summary>
    /// One gate per lock in this process, so its writers queue here rather than polling the file: a semaphore, not a monitor, since a
    /// holder may let go on another thread than the one that took it.
    /// </summary>
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> Gates =
        new(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);

    private readonly string _path;
    private readonly string _written;
    private readonly SemaphoreSlim _gate;
    private bool _released;

    private KeepLock(string path, string written, SemaphoreSlim gate) => (_path, _written, _gate) = (path, written, gate);

    /// <summary>Where the lock of <paramref name="file"/> is: its full path with <c>.lock</c> after it.</summary>
    public static string PathOf(string file) => Path.GetFullPath(file) + ".lock";

    /// <summary>Hold the lock of <paramref name="file"/>, waiting while another writer holds it.</summary>
    /// <param name="clock">The time the lock says it was taken at; a test hands one to age a lock without waiting out its minute.</param>
    /// <exception cref="IOException">A live writer held it for the whole of the wait; nothing was taken.</exception>
    public static KeepLock Take(string file, TimeSpan? within = null, TimeProvider? clock = null)
    {
        var path = PathOf(file);
        var until = DateTime.UtcNow + (within ?? Wait);
        var gate = Gates.GetOrAdd(path, _ => new SemaphoreSlim(1, 1));
        if (!gate.Wait(within ?? Wait))
        {
            throw new IOException($"the edit lock {path} was held by another edit in this process for the whole of the wait, so this edit was not written. Try again.");
        }

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            while (true)
            {
                var at = clock is null ? DateTime.UtcNow : clock.GetUtcNow().UtcDateTime;
                var mine = $"{{\"pid\":{Environment.ProcessId},\"since\":\"{at.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture)}\"}}\n";
                var failed = TryCreate(path, mine);
                if (failed is null) return new KeepLock(path, mine, gate);

                var seen = Read(path);
                // Stale: removed only while it still says what was judged stale, so a lock another has just taken is not the one removed.
                if (seen is not null && IsStale(seen, DateTimeOffset.UtcNow, Lives) && Read(path) == seen && TryDelete(path)) continue;
                if (DateTime.UtcNow >= until)
                {
                    var holder = seen is null ? $"nothing this writer could read ({failed.Message})" : HolderOf(seen);
                    throw new IOException($"the edit lock {path} was held by {holder} for the whole of the wait, so this edit was not written. Try again.", failed);
                }

                Thread.Sleep(Poll);
            }
        }
        catch
        {
            gate.Release();
            throw;
        }
    }

    /// <summary>
    /// Whether a lock's text at <paramref name="now"/> is no live holder's: one that does not read as <c>{ pid, since }</c>, one written
    /// more than <see cref="Stale"/> before <paramref name="now"/>, or one whose process <paramref name="lives"/> says is gone. The CLI's
    /// <c>keepLockStale</c>.
    /// </summary>
    internal static bool IsStale(string text, DateTimeOffset now, Func<int, bool> lives)
    {
        try
        {
            using var document = JsonDocument.Parse(text);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("pid", out var pid) || pid.ValueKind != JsonValueKind.Number || !pid.TryGetInt32(out var id)
                || !root.TryGetProperty("since", out var since) || since.ValueKind != JsonValueKind.String
                || !DateTimeOffset.TryParse(since.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var at))
            {
                return true;
            }

            return now - at > Stale || !lives(id);
        }
        catch (JsonException)
        {
            return true;
        }
    }

    /// <summary>Whether a process runs with this id; one whose state is not ours to read still runs. The run lock's holder reads it too.</summary>
    internal static bool Lives(int pid)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            return !process.HasExited;
        }
        catch (ArgumentException)
        {
            // No process has that id now.
            return false;
        }
        catch (InvalidOperationException)
        {
            // Exited as we looked.
            return false;
        }
        catch (Win32Exception)
        {
            // Not ours to read: cannot tell, so it holds.
            return true;
        }
    }

    /// <summary>The holder a lock names, as a refusal says it.</summary>
    private static string HolderOf(string text)
    {
        try
        {
            using var document = JsonDocument.Parse(text);
            var root = document.RootElement;
            var pid = root.ValueKind == JsonValueKind.Object && root.TryGetProperty("pid", out var id) ? id.GetRawText() : "?";
            var since = root.ValueKind == JsonValueKind.Object && root.TryGetProperty("since", out var at) ? at.ToString() : "?";
            return $"process {pid} since {since}";
        }
        catch (JsonException)
        {
            return "a writer";
        }
    }

    /// <summary>Written beside, then moved into place only where nothing is: never over another holder's lock. Null, or why it was not.</summary>
    private static Exception? TryCreate(string path, string text)
    {
        var beside = $"{path}.{Guid.NewGuid():N}.writing";
        try
        {
            File.WriteAllText(beside, text, Utf8);
            File.Move(beside, path, overwrite: false);
            return null;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // Almost always: a lock is already there. Otherwise the wait's end says what.
            return error;
        }
        finally
        {
            if (File.Exists(beside)) TryDelete(beside);
        }
    }

    /// <summary>
    /// The lock's text, or null where there is none or none readable this moment. Read sharing its removal, so a reader never keeps a
    /// holder from letting go.
    /// </summary>
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

    private static bool TryDelete(string path)
    {
        try
        {
            File.Delete(path);
            return true;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>
    /// Whether the lock is still this holder's: its file there and saying what this holder wrote. An aged lock another writer took as stale
    /// (<see cref="IsStale"/>) says that writer's text, or nothing once it finished, and a lock this holder has let go says nothing of its own.
    /// </summary>
    public bool Holds() => !_released && Read(_path) == _written;

    /// <summary>
    /// <paramref name="text"/> renamed over <paramref name="target"/> only while this lock still holds this holder's text, read again just
    /// before the rename (FOUNDATION1w-s part 2, the design §19.15 C8-1.2): the minute that makes a lock stale runs from its take, however
    /// short the pause after its holder's last read, so a holder whose lock was taken from it must not rename over that writer. Written
    /// beside first, as every write is, and the beside file removed where the rename is refused.
    /// <para>The window from that read to the rename is not closed: a writer that takes the lock and finishes inside it is renamed over. The
    /// older build's writers share only this lock and read none of this, so no fence the newer build keeps closes it (<c>compatible-builds</c>).</para>
    /// </summary>
    /// <exception cref="KeepLockTakenException">The lock was another writer's at the read; nothing was written.</exception>
    public void Write(string target, string text, KeepSeams? seams = null)
    {
        var done = AtomicFile.WriteTextWhile(target, text, () =>
        {
            seams?.BeforeLook?.Invoke();
            var holds = Holds();
            if (holds) seams?.AfterLook?.Invoke();
            return holds;
        });
        if (!done) throw new KeepLockTakenException(_path, target);
    }

    public void Dispose()
    {
        if (_released) return;
        _released = true;
        try
        {
            // A scanner holding the file a moment refuses the removal; a lock left behind would hold every writer for its minute.
            for (var attempt = 0; attempt < 20 && Read(_path) == _written; attempt++)
            {
                if (TryDelete(_path)) return;
                Thread.Sleep(Poll);
            }
        }
        finally
        {
            _gate.Release();
        }
    }
}

/// <summary>
/// What a test hands a fenced write to stand where a real writer pauses (FOUNDATION1w-s part 2): a clock that ages the lock at its take,
/// and the two instants the fence is about, each run in the write itself so the other writer's take lands exactly there.
/// </summary>
internal sealed class KeepSeams
{
    /// <summary>The time the write's lock says it was taken at.</summary>
    public TimeProvider? Clock { get; init; }

    /// <summary>Run just before the lock is read again: a writer that took the aged lock and finished here is seen.</summary>
    public Action? BeforeLook { get; init; }

    /// <summary>Run after the lock was read and found its own, before the rename: a writer that finished here is renamed over, the bound.</summary>
    public Action? AfterLook { get; init; }
}

/// <summary>
/// A write refused, nothing written: the keeping lock it held when it composed its text was another writer's, or gone, when it came to
/// rename (FOUNDATION1w-s part 2, C8-1.2). An <see cref="IOException"/>, as a lock held for the whole of the wait raises, so every door
/// says it as it says that; the next read of the file finds what that writer left.
/// </summary>
public sealed class KeepLockTakenException(string lockFile, string target)
    : IOException($"the edit lock {lockFile} was taken from this edit by another writer before {Path.GetFileName(target)} was renamed, so this edit was not written. Try again.");
