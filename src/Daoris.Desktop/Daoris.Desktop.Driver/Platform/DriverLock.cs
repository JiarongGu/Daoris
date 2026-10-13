using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>Which door drives a home — named in a refusal, so the person knows which one to stop.</summary>
public enum DriverKind
{
    /// <summary>The desktop application's loop.</summary>
    Desktop,

    /// <summary>A terminal's <c>daoris-driver drive</c>.</summary>
    Headless,
}

/// <summary>The live driver a home's lock names (DRV8a, D104).</summary>
/// <param name="Kind">Which door it is.</param>
/// <param name="Pid">Its process.</param>
/// <param name="Since">When it took the home.</param>
public sealed record DriverHolder(DriverKind Kind, int Pid, DateTimeOffset Since)
{
    /// <summary>The holder as a sentence names it: which door, its process, and since when.</summary>
    public string Named =>
        $"{(Kind == DriverKind.Desktop ? "the desktop" : "a headless `daoris-driver`")} "
        + $"(pid {Pid}, since {Since.ToLocalTime():yyyy-MM-dd HH:mm:ss})";
}

/// <summary>
/// One live driver loop per Daoris home (DRV8a, D104): the lock a loop takes before it reaches the
/// service, and what a second loop reads to say which one is running.
/// </summary>
/// <remarks>
/// <para><b>It keeps a second loop from being started by accident; the quest lock is still the lock</b>
/// (D46). Two loops on one home race for the same quests and the take decides, so the desktop stood its
/// session down when a stray headless loop took a quest first. Nothing here is needed for that to hold.</para>
///
/// <para><b>Taken without replacing.</b> The file is written beside, then moved into place with no
/// overwrite, so of two loops starting at once only one takes it. It is released on exit, and only while
/// it still says what this lock wrote.</para>
///
/// <para><b>A stale lock never blocks.</b> One whose process is gone, whose process id now belongs to a
/// process started at another time, or that does not read, is replaced — the session markers' test
/// (<see cref="SessionProcesses.AliveOnThisMachine"/>). As for a marker, a process whose start time cannot
/// be read is taken as alive: "cannot tell" must never become "take it".</para>
/// </remarks>
public sealed class DriverLock : IDisposable
{
    /// <summary>The lock's name under the home.</summary>
    public const string FileName = "driver.lock";

    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    private readonly string _path;

    // What this lock wrote, or null when it runs beside another driver and holds nothing.
    private readonly string? _written;
    private int _released;

    private DriverLock(string path, string? written, DriverHolder? beside)
    {
        _path = path;
        _written = written;
        Beside = beside;
    }

    /// <summary>Whether this loop holds its home's lock.</summary>
    public bool Holds => _written is not null;

    /// <summary>The live driver this loop runs beside, as <c>--share</c> asked, or null when it holds the home.</summary>
    public DriverHolder? Beside { get; }

    /// <summary>Where a home's lock is.</summary>
    public static string PathIn(string home) => Path.Combine(home, FileName);

    /// <summary>
    /// Take the home for a loop, or null with the live driver that already holds it.
    /// </summary>
    /// <exception cref="DriverException">The lock changed under every attempt, or could not be written.</exception>
    public static DriverLock? TryAcquire(string home, DriverKind kind, out DriverHolder? holder)
    {
        Directory.CreateDirectory(home);
        var path = PathIn(home);
        Exception? last = null;

        for (var attempt = 0; attempt < 8; attempt++)
        {
            var mine = Describe(kind);
            if (TryCreate(path, mine, ref last))
            {
                holder = null;
                return new DriverLock(path, mine, beside: null);
            }

            var seen = Read(path, out var busy);
            if (seen is null)
            {
                // Gone between the create and the read, or busy for a moment: look again.
                if (busy) Thread.Sleep(10 + attempt * 10);
                continue;
            }

            if (Parse(seen) is { } named && Alive(named.Holder.Pid, named.Started))
            {
                holder = named.Holder;
                return null;
            }

            // Stale. Removed only while it still says what was judged stale, so a lock another loop has
            // just taken is not the one removed.
            if (Read(path, out _) == seen) TryDelete(path);
        }

        throw new DriverException(
            $"the home's driver lock ({path}) could not be taken: it changed under every attempt, or would not "
            + $"be written{(last is null ? "" : $" ({last.Message})")}. Try again.");
    }

    /// <summary>
    /// Take the home where it is free, or run beside the live driver holding it (<c>--share</c>): the rare
    /// deliberate case, which never takes another driver's lock.
    /// </summary>
    public static DriverLock Share(string home, DriverKind kind) =>
        TryAcquire(home, kind, out var holder) ?? new DriverLock(PathIn(home), written: null, beside: holder);

    /// <summary>The live driver holding the home, or null when none does.</summary>
    public static DriverHolder? HeldBy(string home)
    {
        var seen = Read(PathIn(home), out _);
        return seen is not null && Parse(seen) is { } named && Alive(named.Holder.Pid, named.Started)
            ? named.Holder
            : null;
    }

    /// <summary>What a terminal's loop says when it is refused, naming the driver that holds the home.</summary>
    public static string Refusal(string home, DriverHolder holder) =>
        $"{home} is already driven by {holder.Named}. A second loop on one home races the first for the "
        + "same quests — stop that one first, or pass --share to run beside it on purpose.";

    /// <summary>Let the home go — only while the lock still says what this one wrote.</summary>
    public void Dispose()
    {
        if (_written is null || Interlocked.Exchange(ref _released, 1) == 1) return;
        if (Read(_path, out _) == _written) TryDelete(_path);
    }

    /// <summary>This process, as its lock names it.</summary>
    private static string Describe(DriverKind kind)
    {
        using var self = Process.GetCurrentProcess();
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            writer.WriteString("kind", kind == DriverKind.Desktop ? "desktop" : "headless");
            writer.WriteNumber("pid", Environment.ProcessId);
            writer.WriteNumber("started", self.StartTime.ToUniversalTime().Ticks);
            writer.WriteString("since", DateTimeOffset.UtcNow);
            writer.WriteEndObject();
        }

        return Utf8.GetString(stream.ToArray()) + "\n";
    }

    /// <summary>Written beside, then moved into place only where nothing is — never over another lock.</summary>
    private static bool TryCreate(string path, string text, ref Exception? last)
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
            // Almost always: a lock is already there. Otherwise the loop's read says what is.
            last = error;
            return false;
        }
        finally
        {
            if (File.Exists(beside))
            {
                try { File.Delete(beside); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            }
        }
    }

    /// <summary>The lock's text, or null when there is none — or none readable this moment (<paramref name="busy"/>).</summary>
    private static string? Read(string path, out bool busy)
    {
        busy = false;
        try
        {
            return File.ReadAllText(path, Utf8);
        }
        catch (Exception error) when (error is FileNotFoundException or DirectoryNotFoundException)
        {
            return null;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            busy = true;
            return null;
        }
    }

    private static void TryDelete(string path)
    {
        try { File.Delete(path); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    /// <summary>The holder a lock names and its process's start time, or null for one that does not read.</summary>
    private static (DriverHolder Holder, long Started)? Parse(string text)
    {
        try
        {
            using var document = JsonDocument.Parse(text);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return null;

            DriverKind? kind = root.TryGetProperty("kind", out var named) && named.ValueKind == JsonValueKind.String
                ? named.GetString() switch
                {
                    "desktop" => DriverKind.Desktop,
                    "headless" => DriverKind.Headless,
                    _ => null,
                }
                : null;
            if (kind is null
                || !root.TryGetProperty("pid", out var pid) || !pid.TryGetInt32(out var id)
                || !root.TryGetProperty("started", out var started) || !started.TryGetInt64(out var ticks))
            {
                return null;
            }

            var since = root.TryGetProperty("since", out var at) && at.ValueKind == JsonValueKind.String
                        && DateTimeOffset.TryParse(at.GetString(), out var parsed)
                ? parsed
                : new DateTimeOffset(ticks, TimeSpan.Zero);
            return (new DriverHolder(kind.Value, id, since), ticks);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Whether the process a lock names still runs as the process it named.</summary>
    private static bool Alive(int pid, long started)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            return Math.Abs(process.StartTime.ToUniversalTime().Ticks - started) < TimeSpan.TicksPerSecond;
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
        catch (System.ComponentModel.Win32Exception)
        {
            // Its start time is not ours to read: cannot tell, so it holds.
            return true;
        }
    }
}
