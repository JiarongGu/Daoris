using System.Globalization;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// The home's one run of removals at a time (CLEANUP1, D195 point 4; the cleanup design §3.1): every removal of a kept kind, the look's
/// pass, a press's run or a terminal's alone, runs holding <c>locks/keeping</c>, so two never remove beside each other.
/// </summary>
/// <remarks>
/// <para><b>Tried, never waited for</b> (Fable F3.1), in <see cref="TreeLock"/>'s shape: the handle is the lock, opened with no sharing, at
/// once or not at all, so a process that dies lets go with its handles and nothing stale blocks; never <see cref="RecordLock"/>'s ten
/// seconds, since a run elsewhere is a long one and a wait would hold a pass for nothing.</para>
/// <para><b>Its holder named</b>: the holder writes <c>{ pid, since, door }</c> to a file of its own beside it,
/// <c>locks/keeping.holder</c>, as the keep lock writes its own (<see cref="KeepLock"/>), and removes it at the run's end, while it still
/// says what this holder wrote. A refusal names the holder; a holder's file whose process is gone is a run a crash cut short (Fable F3.3),
/// which the card says (KEEP1c).</para>
/// </remarks>
public sealed class KeepingRun : IDisposable
{
    /// <summary>The look's pass, in the tidy's task.</summary>
    public const string Look = "look";

    /// <summary>The free-space floor's acts (KEEP1f).</summary>
    public const string Floor = "floor";

    /// <summary>The person's press in the window (KEEP1e).</summary>
    public const string Press = "press";

    /// <summary>A terminal acting alone (KEEP1e).</summary>
    public const string Terminal = "terminal";

    private const string Moment = "yyyy-MM-dd'T'HH:mm:ss.fff'Z'";

    private readonly FileStream _lock;
    private readonly string _holder;
    private readonly string _written;

    private KeepingRun(FileStream held, string holder, string written, string door)
    {
        (_lock, _holder, _written) = (held, holder, written);
        Door = door;
    }

    /// <summary>This run's id, which <c>keeping.json</c>'s <c>last</c> names.</summary>
    public string Id { get; } = Guid.NewGuid().ToString("N");

    /// <summary>The door this run came through.</summary>
    public string Door { get; }

    /// <summary>Where the lock is: <c>keeping</c> under the home's <c>locks</c> entry.</summary>
    public static string PathOf(string home) => DaorisHome.Entry(home, HomeEntry.Locks, "keeping");

    /// <summary>Where its holder names itself: beside the lock, <c>keeping.holder</c>.</summary>
    public static string HolderPathOf(string home) => PathOf(home) + ".holder";

    /// <summary>
    /// Hold the home's run, naming this process and <paramref name="door"/>; or null, at once, while another run holds it, in this process
    /// or another. A folder that cannot be made or a lock the person may not open is thrown: "cannot tell" is never "free".
    /// </summary>
    public static KeepingRun? TryTake(string home, string door)
    {
        var path = PathOf(home);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        FileStream held;
        try
        {
            held = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        }
        catch (IOException)
        {
            return null;
        }

        var written = JsonFile.Serialize(writer =>
        {
            writer.WriteStartObject();
            writer.WriteNumber("pid", Environment.ProcessId);
            writer.WriteString("since", DateTime.UtcNow.ToString(Moment, CultureInfo.InvariantCulture));
            writer.WriteString("door", door);
            writer.WriteEndObject();
        });
        try
        {
            AtomicFile.WriteText(HolderPathOf(home), written);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // The run holds the lock all the same; a refusal elsewhere then names no holder, which reads as one unnamed.
        }

        return new KeepingRun(held, HolderPathOf(home), written, door);
    }

    /// <summary>
    /// Who holds the home's run, by its holder's file: null where none is written. <see cref="KeepingHolder.Gone"/> where its process no
    /// longer runs, a run a crash cut short. <paramref name="lives"/> is a test's stand-in for asking the system.
    /// </summary>
    public static KeepingHolder? Holder(string home, Func<int, bool>? lives = null)
    {
        string text;
        try
        {
            var path = HolderPathOf(home);
            if (!File.Exists(path)) return null;
            text = AtomicFile.ReadText(path);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(text);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("pid", out var pid) || !pid.TryGetInt32(out var id)
                || !root.TryGetProperty("since", out var since) || since.ValueKind != JsonValueKind.String
                || !DateTimeOffset.TryParse(since.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var at))
            {
                return null;
            }

            var door = root.TryGetProperty("door", out var named) && named.ValueKind == JsonValueKind.String ? named.GetString()! : "";
            return new(id, at, door, !(lives ?? KeepLock.Lives)(id));
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>The holder's file goes first, while this run still holds the lock and only where it says what this run wrote; then the lock.</summary>
    public void Dispose()
    {
        try
        {
            if (File.Exists(_holder) && AtomicFile.ReadText(_holder) == _written) File.Delete(_holder);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // Left behind, it reads as a run cut short until the next run writes its own: more said, never less.
        }
        finally
        {
            _lock.Dispose();
        }
    }
}

/// <summary>Who holds the home's run of removals, by its file (<see cref="KeepingRun.Holder"/>).</summary>
/// <param name="Door">The door it came through (<see cref="KeepingRun"/>'s).</param>
/// <param name="Gone">Its process no longer runs: a run a crash cut short.</param>
public sealed record KeepingHolder(int Pid, DateTimeOffset Since, string Door, bool Gone)
{
    /// <summary>The holder in a clause, as a refusal and the look's report say it.</summary>
    public string Said => Gone
        ? $"a run of process {Pid}, from the {Door}, since {Since.UtcDateTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)} UTC, stopped before its end"
        : $"a run of process {Pid}, from the {Door}, since {Since.UtcDateTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)} UTC";
}
