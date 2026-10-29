using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// What the run holding a repository's heavy-run slot wrote beside it (HEAVYRUN1a): its process, the tree it runs in, its gates,
/// since when, the driven session whose environment named one, and the job its gates' processes are held in.
/// </summary>
/// <param name="Pid">The holding run's process.</param>
/// <param name="Started">That process's start, in UTC ticks, so a reused id is not read as it.</param>
/// <param name="Checkout">The checkout it runs in.</param>
/// <param name="Gates">The gates it runs, in order.</param>
/// <param name="Since">When it took the slot.</param>
/// <param name="Session">The driven session whose run it is, where its environment named one.</param>
/// <param name="Job">The name of the job its gates' processes are held in, which the next holder ends if it is still there.</param>
public sealed record HeavyHolder(
    int Pid, long Started, string Checkout, IReadOnlyList<string> Gates, DateTimeOffset Since, string? Session, string? Job)
{
    /// <summary>The gate it runs now, rewritten at each gate's start (HEAVYSLOT1); null before its first and in a record older than it.</summary>
    public HeavyGateNow? Now { get; init; }

    /// <summary>The gate that failed and its exit, written before what it left is ended and the slot let go (HEAVYSLOT1).</summary>
    public HeavyGateFailed? Failed { get; init; }
}

/// <summary>The gate a holder runs now (HEAVYSLOT1): its name, its place in the holder's gates from 0, and when it started.</summary>
public sealed record HeavyGateNow(string Gate, int At, DateTimeOffset Since);

/// <summary>The gate a holder's run failed at (HEAVYSLOT1), and the exit it failed with.</summary>
public sealed record HeavyGateFailed(string Gate, int Exit);

/// <summary>
/// What a run waiting for a repository's slot writes beside it while it waits (HEAVYSLOT1, the UI/UX design §3.5): its process, the
/// tree it runs in, since when it waits, and the driven session whose environment named one. Gone as it takes the slot or ends.
/// </summary>
public sealed record HeavyWaiter(int Pid, long Started, string Checkout, DateTimeOffset Since, string? Session);

/// <summary>
/// A repository's slot as it stands (HEAVYSLOT1): the run holding it and the runs waiting for it, each only where its process (its id
/// and its start) is still there, so a record a run that died left behind holds nothing and waits for nothing.
/// </summary>
public sealed record HeavyLook(HeavyHolder? Holder, IReadOnlyList<HeavyWaiter> Waiters);

/// <summary>How a run waiting for the slot looks again, and how often it says that it still waits.</summary>
/// <param name="Poll">Between looks.</param>
/// <param name="SayEvery">Between two sayings of the same holder; a new holder is said at once.</param>
/// <param name="Clock">The wall clock a holder's time is read against; how long this run waited is a monotonic clock's.</param>
public sealed record HeavyWaiting(TimeSpan Poll, TimeSpan SayEvery, Func<DateTimeOffset> Clock)
{
    /// <summary>A look every half second, said every thirty: a heavy run is minutes long, and its waiter is told without being flooded.</summary>
    public static HeavyWaiting Default { get; } = new(TimeSpan.FromMilliseconds(500), TimeSpan.FromSeconds(30), () => DateTimeOffset.UtcNow);
}

/// <summary>
/// One heavy-run slot per repository, across its trees (HEAVYRUN1a, D180's HEAVYRUN1 note): while one run of a repository's heavy
/// gates holds it, a second, from any checkout of the same repository, waits and says behind whom.
/// </summary>
/// <remarks>
/// <para><b>Why.</b> Two driven sessions on one repository each ran its full build and suites at once on the installed Daoris (6 to 9
/// minutes each, about 25 node workers and 5 GB between them, a neighbour's shell killed).</para>
///
/// <para><b>Keyed by the repository, never the tree.</b> The key is the repository's common git folder, read from git's own files with
/// no process (<see cref="TreeTips.CommonDirectory"/>), named by the folder's identity where Windows gives one (a junction, a
/// <c>subst</c> drive, another case or a long-path prefix take the same slot), else by its spelling, as LANDRACE1's branch hold is. So
/// the root checkout and every linked tree of it share one slot, with no registry: a root checkout is no session tree, and the tree
/// list names none for it.</para>
///
/// <para><b>The handle is the lock</b>, as <see cref="TreeLock"/>'s is: one file under the home's <c>locks/heavy/</c>, opened alone. A
/// process that dies lets go of it with its handles, so nothing stale blocks, and there is no lease and no timeout anywhere: a timed
/// lease admits overlap the moment a run outlasts it.</para>
///
/// <para><b>The holder says what it runs</b> in a record beside the lock (<see cref="HeavyHolder"/>), which a waiting run reads to say
/// it, and which the holder takes away as it lets go. One left behind is a holder that died holding the slot: the next run reads it
/// (<see cref="Before"/>) and ends the job it names, if it is still there, before its own gates start.</para>
///
/// <para><b>The page reads who holds it and who waits</b> (HEAVYSLOT1, the UI/UX design §3.5): the holder rewrites its record at each
/// gate with that gate and its start, and each waiting run keeps a record of its own under <see cref="WaitersOf"/>, gone as it takes
/// the slot or stops. <see cref="Look"/> reads both, and only where the record's process (its id and its start) still runs: never
/// the run's output, and never a record a dead run left as a live hold.</para>
/// </remarks>
public sealed class HeavySlot : IDisposable
{
    private readonly FileStream _file;
    private readonly string _record;

    private HeavySlot(FileStream file, string record, HeavyHolder? before)
    {
        _file = file;
        _record = record;
        Before = before;
    }

    /// <summary>The record the slot held when it was taken: the run before, which died holding it; null where it let go as it should.</summary>
    public HeavyHolder? Before { get; }

    /// <summary>The checkout <paramref name="directory"/> is in: the nearest folder at or above it holding a <c>.git</c>; null where none does.</summary>
    public static string? CheckoutOf(string directory)
    {
        for (var folder = new DirectoryInfo(Path.GetFullPath(directory)); folder is not null; folder = folder.Parent)
        {
            var git = Path.Combine(folder.FullName, ".git");
            if (Directory.Exists(git) || File.Exists(git)) return Path.TrimEndingDirectorySeparator(folder.FullName);
        }

        return null;
    }

    /// <summary>
    /// The slot's key for the repository <paramref name="directory"/> is a checkout of: its common git folder's identity, hashed. Null
    /// where it is in no checkout, or git's files do not say its repository: "cannot tell" is never a slot of its own.
    /// </summary>
    public static string? KeyOf(string directory) =>
        Common(directory) is { } common
            ? Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes($"heavy|{Identity(common)}")))[..16]
            : null;

    /// <summary>The repository's name for what a run says: the folder its common git folder is in, whichever tree asks.</summary>
    public static string? RepositoryOf(string directory)
    {
        if (Common(directory) is not { } common) return null;
        var name = Path.GetFileName(Path.TrimEndingDirectorySeparator(common));
        return string.Equals(name, ".git", StringComparison.OrdinalIgnoreCase)
            ? Path.GetFileName(Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(common)))
            : name.EndsWith(".git", StringComparison.OrdinalIgnoreCase) ? name[..^4] : name;
    }

    /// <summary>Where a repository's slot is, under the home's <c>locks/</c>.</summary>
    public static string PathOf(string home, string key) => DaorisHome.Entry(home, HomeEntry.Locks, "heavy", $"{key}.lock");

    /// <summary>Where its holder says what it runs, beside the slot.</summary>
    public static string RecordOf(string home, string key) => DaorisHome.Entry(home, HomeEntry.Locks, "heavy", $"{key}.json");

    /// <summary>Where the runs waiting for it each say so, one file a run (HEAVYSLOT1), beside the slot.</summary>
    public static string WaitersOf(string home, string key) => DaorisHome.Entry(home, HomeEntry.Locks, "heavy", $"{key}.waits");

    /// <summary>The slot, or null while another run holds it. Never waits.</summary>
    /// <remarks>
    /// The file held the other way is an answer: null. A folder that cannot be made, or an error that is no sharing violation, is
    /// thrown, since "cannot tell" is never "free". Off Windows .NET takes an advisory lock, which every Daoris process honours, and
    /// says a held one as any other failure to open, so there every failure to open is read as held.
    /// </remarks>
    public static HeavySlot? TryTake(string home, string key)
    {
        var path = PathOf(home, key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        FileStream file;
        try
        {
            file = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        }
        catch (IOException held) when (!OperatingSystem.IsWindows() || (held.HResult & 0xFFFF) is SharingViolation or LockViolation)
        {
            return null;
        }

        return new HeavySlot(file, RecordOf(home, key), Holder(home, key));
    }

    /// <summary>
    /// The slot, waited for while another run holds it, saying on <paramref name="said"/> behind which run and for how long, at
    /// <paramref name="waiting"/>'s cadence, until it is this run's. A free slot is taken at once, saying nothing.
    /// </summary>
    /// <param name="waiter">
    /// What this run writes beside the slot while it waits (HEAVYSLOT1), so the page can say who waits; taken away as it takes the
    /// slot or stops. Null writes nothing.
    /// </param>
    /// <exception cref="OperationCanceledException">Stopped while waiting; nothing is held.</exception>
    public static async Task<HeavySlot> TakeAsync(
        string home, string key, string repository, TextWriter said, CancellationToken ct, HeavyWaiting? waiting = null, HeavyWaiter? waiter = null)
    {
        waiting ??= HeavyWaiting.Default;
        var waited = Stopwatch.StartNew();
        TimeSpan? saidAt = null;
        HeavyHolder? saidOf = null;
        string? waits = null;
        try
        {
            for (var looks = 0; ; looks++)
            {
                ct.ThrowIfCancellationRequested();
                if (TryTake(home, key) is { } slot)
                {
                    if (looks > 0) said.WriteLine($"run-heavy: `{repository}`'s heavy-run slot is this run's, after waiting {Took(waited.Elapsed)}.");
                    return slot;
                }

                if (waiter is not null && waits is null) waits = KeepWaiter(home, key, waiter);
                var holder = Holder(home, key);
                if (saidAt is null || waited.Elapsed - saidAt >= waiting.SayEvery || !SameRun(holder, saidOf))
                {
                    said.WriteLine(Waiting(repository, holder, waiting.Clock(), waited.Elapsed));
                    saidAt = waited.Elapsed;
                    saidOf = holder;
                }

                await Task.Delay(waiting.Poll, ct).ConfigureAwait(false);
            }
        }
        finally
        {
            if (waits is not null) Forget(waits);
        }
    }

    /// <summary>
    /// The slot as it stands (HEAVYSLOT1): its holder and the runs waiting for it, the longest waiting first, each only where
    /// <paramref name="alive"/> finds its process (its id and its start). Neither file alone is a hold: the handle is the lock, and a
    /// run that died leaves its record behind, so one whose process is gone is read as nothing. Reads files only, and takes nothing.
    /// </summary>
    /// <param name="alive">Whether a process of that id and that start, in UTC ticks, runs; this machine's processes where null.</param>
    public static HeavyLook Look(string home, string key, Func<int, long, bool>? alive = null)
    {
        alive ??= Running;
        var holder = Holder(home, key) is { } held && alive(held.Pid, held.Started) ? held : null;
        var waiters = new List<HeavyWaiter>();
        var folder = WaitersOf(home, key);
        IEnumerable<string> files;
        try
        {
            files = Directory.Exists(folder) ? Directory.EnumerateFiles(folder, "*.json").ToList() : [];
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            files = [];
        }

        foreach (var file in files)
        {
            // The holder's own record is never a wait: a run that took the slot took its wait away first, or is about to.
            if (ReadShared(file) is { } text && ParseWaiter(text) is { } one && alive(one.Pid, one.Started)
                && (holder is null || holder.Pid != one.Pid || holder.Started != one.Started))
            {
                waiters.Add(one);
            }
        }

        return new HeavyLook(holder, [.. waiters.OrderBy(one => one.Since).ThenBy(one => one.Pid)]);
    }

    /// <summary>Whether a process of that id runs and started then: a reused id is another process. One whose start cannot be read is taken as it.</summary>
    public static bool Running(int pid, long started)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            try
            {
                return process.StartTime.ToUniversalTime().Ticks == started;
            }
            catch (Exception error) when (error is System.ComponentModel.Win32Exception or NotSupportedException)
            {
                // Another user's or a protected process: its id is there, and "cannot tell" is never "gone".
                return true;
            }
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException)
        {
            return false;
        }
    }

    /// <summary>What the slot's holder says it runs, or null where nothing is said or what is there does not read.</summary>
    public static HeavyHolder? Holder(string home, string key)
        => ReadShared(RecordOf(home, key)) is { } text ? Parse(text) : null;

    /// <summary>A record's text, read sharing writing and deletion with its writer, so a run letting go is never refused by a reader.</summary>
    private static string? ReadShared(string path)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream, Encoding.UTF8);
            return reader.ReadToEnd();
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>
    /// Say this run waits, in a file of its own beside the slot, named by its process and start; null where it cannot be written,
    /// which costs the page a waiter and the run nothing.
    /// </summary>
    private static string? KeepWaiter(string home, string key, HeavyWaiter waiter)
    {
        var path = Path.Combine(WaitersOf(home, key), $"{waiter.Pid}-{waiter.Started}.json");
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            AtomicFile.WriteText(path, SerializeWaiter(waiter));
            return path;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static void Forget(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // Left: its process is gone soon, and a waiter whose process is gone waits for nothing.
        }
    }

    /// <summary>Say what this run runs, beside the slot it holds, written whole or not at all.</summary>
    public void Keep(HeavyHolder holder) => AtomicFile.WriteText(_record, Serialize(holder));

    /// <summary>Let go: the record first, then the slot, so a run that takes it next never reads this one's as the run before.</summary>
    public void Dispose()
    {
        try
        {
            File.Delete(_record);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // Left: the next run reads it as a run before and finds its job gone, which costs a sentence and nothing more.
        }

        _file.Dispose();
    }

    /// <summary>A span as a run says it: <c>45s</c>, <c>2m 15s</c>, <c>1h 5m</c>.</summary>
    internal static string Took(TimeSpan span)
    {
        if (span < TimeSpan.Zero) span = TimeSpan.Zero;
        return span.TotalHours >= 1 ? $"{(int)span.TotalHours}h {span.Minutes}m"
            : span.TotalMinutes >= 1 ? $"{span.Minutes}m {span.Seconds}s"
            : $"{span.Seconds}s";
    }

    internal static string Serialize(HeavyHolder holder)
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            writer.WriteNumber("pid", holder.Pid);
            writer.WriteNumber("started", holder.Started);
            writer.WriteString("checkout", holder.Checkout);
            writer.WriteStartArray("gates");
            foreach (var gate in holder.Gates) writer.WriteStringValue(gate);
            writer.WriteEndArray();
            writer.WriteString("since", Stamp(holder.Since));
            if (holder.Session is { } session) writer.WriteString("session", session);
            if (holder.Job is { } job) writer.WriteString("job", job);
            if (holder.Now is { } now)
            {
                writer.WriteStartObject("now");
                writer.WriteString("gate", now.Gate);
                writer.WriteNumber("at", now.At);
                writer.WriteString("since", Stamp(now.Since));
                writer.WriteEndObject();
            }

            if (holder.Failed is { } failed)
            {
                writer.WriteStartObject("failed");
                writer.WriteString("gate", failed.Gate);
                writer.WriteNumber("exit", failed.Exit);
                writer.WriteEndObject();
            }

            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.ToArray()).ReplaceLineEndings("\n") + "\n";
    }

    internal static string SerializeWaiter(HeavyWaiter waiter)
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            writer.WriteNumber("pid", waiter.Pid);
            writer.WriteNumber("started", waiter.Started);
            writer.WriteString("checkout", waiter.Checkout);
            writer.WriteString("since", Stamp(waiter.Since));
            if (waiter.Session is { } session) writer.WriteString("session", session);
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.ToArray()).ReplaceLineEndings("\n") + "\n";
    }

    private static string Stamp(DateTimeOffset at) => at.ToUniversalTime().ToString("O", System.Globalization.CultureInfo.InvariantCulture);

    private static HeavyWaiter? ParseWaiter(string text)
    {
        try
        {
            using var document = JsonDocument.Parse(text);
            var root = document.RootElement;
            return root.ValueKind == JsonValueKind.Object
                   && root.TryGetProperty("pid", out var pid) && pid.TryGetInt32(out var id)
                   && root.TryGetProperty("started", out var started) && started.TryGetInt64(out var ticks)
                   && root.TryGetProperty("checkout", out var checkout) && checkout.ValueKind == JsonValueKind.String
                   && root.TryGetProperty("since", out var since) && since.TryGetDateTimeOffset(out var at)
                ? new HeavyWaiter(id, ticks, checkout.GetString()!, at, Text(root, "session"))
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>The gate running, as a record says it; null where it says none, or one that does not read.</summary>
    private static HeavyGateNow? NowOf(JsonElement root) =>
        root.TryGetProperty("now", out var now) && now.ValueKind == JsonValueKind.Object
        && now.TryGetProperty("gate", out var gate) && gate.ValueKind == JsonValueKind.String
        && now.TryGetProperty("at", out var at) && at.TryGetInt32(out var index)
        && now.TryGetProperty("since", out var since) && since.TryGetDateTimeOffset(out var from)
            ? new HeavyGateNow(gate.GetString()!, index, from)
            : null;

    private static HeavyGateFailed? FailedOf(JsonElement root) =>
        root.TryGetProperty("failed", out var failed) && failed.ValueKind == JsonValueKind.Object
        && failed.TryGetProperty("gate", out var gate) && gate.ValueKind == JsonValueKind.String
        && failed.TryGetProperty("exit", out var exit) && exit.TryGetInt32(out var code)
            ? new HeavyGateFailed(gate.GetString()!, code)
            : null;

    private static HeavyHolder? Parse(string text)
    {
        try
        {
            using var document = JsonDocument.Parse(text);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("pid", out var pid) || !pid.TryGetInt32(out var id)
                || !root.TryGetProperty("started", out var started) || !started.TryGetInt64(out var ticks)
                || !root.TryGetProperty("checkout", out var checkout) || checkout.ValueKind != JsonValueKind.String
                || !root.TryGetProperty("gates", out var gates) || gates.ValueKind != JsonValueKind.Array
                || !root.TryGetProperty("since", out var since) || !since.TryGetDateTimeOffset(out var at))
            {
                return null;
            }

            return new HeavyHolder(
                id, ticks, checkout.GetString()!,
                [.. gates.EnumerateArray().Where(gate => gate.ValueKind == JsonValueKind.String).Select(gate => gate.GetString()!)],
                at, Text(root, "session"), Text(root, "job")) { Now = NowOf(root), Failed = FailedOf(root) };
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? Text(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static string Waiting(string repository, HeavyHolder? holder, DateTimeOffset now, TimeSpan waited)
    {
        var who = holder is null
            ? "a run that has not said what it runs yet"
            : (holder.Session is { } session ? $"session {session} (process {holder.Pid})" : $"process {holder.Pid}")
              + $", running {(holder.Gates.Count == 0 ? "its gates" : string.Join(", ", holder.Gates))} for {Took(now - holder.Since)} in {holder.Checkout}";
        return $"run-heavy: `{repository}`'s heavy-run slot is held by {who}; this run waits for it ({Took(waited)} so far).";
    }

    private static bool SameRun(HeavyHolder? one, HeavyHolder? other) =>
        one is null ? other is null : other is not null && one.Pid == other.Pid && one.Started == other.Started && one.Since == other.Since;

    private static string? Common(string directory) =>
        CheckoutOf(directory) is { } checkout ? TreeTips.CommonDirectory(checkout) : null;

    /// <remarks>As LANDRACE1's hold names a folder: its identity where Windows gives one, else its spelling.</remarks>
    private static string Identity(string folder) =>
        (OperatingSystem.IsWindows() ? FolderIdentity.Of(folder) : null)
        ?? Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder)).ToLowerInvariant();

    private const int SharingViolation = 32;
    private const int LockViolation = 33;
}
