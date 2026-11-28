using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// What the run holding a repository's heavy-run slot writes beside it: its process, the tree it runs in, its gates,
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
    /// <summary>The gate it runs now, rewritten at each gate's start; null before its first and in a record older than it.</summary>
    public HeavyGateNow? Now { get; init; }

    /// <summary>The gate that failed and its exit, written before what it left is ended and the slot let go.</summary>
    public HeavyGateFailed? Failed { get; init; }

    /// <summary>The verification run holding it, where the heavy executor does; an older reader passes over it.</summary>
    public string? Run { get; init; }

    /// <summary>When its runner said its builds were done (<see cref="BuiltWatch"/>), so no compile is left to raise its peak; null before, and in a record older than it.</summary>
    public DateTimeOffset? Built { get; init; }
}

/// <summary>The gate a holder runs now: its name, its place in the holder's gates from 0, and when it started.</summary>
public sealed record HeavyGateNow(string Gate, int At, DateTimeOffset Since);

/// <summary>The gate a holder's run failed at, and the exit it failed with.</summary>
public sealed record HeavyGateFailed(string Gate, int Exit);

/// <summary>
/// What a run waiting for a repository's slot writes beside it while it waits: its process, the
/// tree it runs in, since when it waits, and the driven session whose environment named one. Gone as it takes the slot or ends.
/// </summary>
public sealed record HeavyWaiter(int Pid, long Started, string Checkout, DateTimeOffset Since, string? Session);

/// <summary>
/// A repository's slots as they stand: the runs holding them, the first taken first, and the runs waiting, each only where its process
/// (its id and its start) is still there, so a record a run that died left behind holds nothing and waits for nothing.
/// </summary>
public sealed record HeavyLook(IReadOnlyList<HeavyHolder> Holders, IReadOnlyList<HeavyWaiter> Waiters);

/// <summary>How a run waiting for the slot looks again, and how often it says that it still waits.</summary>
/// <param name="Poll">Between looks.</param>
/// <param name="SayEvery">Between two sayings of the same holder; a new holder is said at once.</param>
/// <param name="Clock">The wall clock a holder's time is read against; how long this run waited is a monotonic clock's.</param>
public sealed record HeavyWaiting(TimeSpan Poll, TimeSpan SayEvery, Func<DateTimeOffset> Clock)
{
    /// <summary>A look every half second, said every thirty: a heavy run is minutes long, and its waiter is told without being flooded.</summary>
    public static HeavyWaiting Default { get; } = new(TimeSpan.FromMilliseconds(500), TimeSpan.FromSeconds(30), () => DateTimeOffset.UtcNow);
}

/// <summary>What admits a run waiting for its repository's slot: the rules and the repository's accepted slots (<see cref="AcceptedSlots"/>), read at each look, and its record.</summary>
public sealed record HeavyAdmit(Func<CheckRules> Rules, Func<CancellationToken, Task<int?>> Declared, Func<HeavyHolder> Holder)
{
    /// <summary>The driven session (the agent) whose run it is: its fair share of its project's places.</summary>
    public string? Agent { get; init; }

    public IReadOnlyList<string> Gates { get; init; } = [CheckAdmission.DefaultGate];

    /// <summary>The run's place in its queue's order (<see cref="AdmissionOrder"/>): it asks the admission only when it is the turn's, and tells the turn what came of it; null asks at each look.</summary>
    public AdmissionTurn? Turn { get; init; }
}

/// <summary>A wait for a repository's slot as it ended: the slot, or none and why the admission refused it (null where none judged).</summary>
public sealed record HeavyTake(HeavySlot? Slot, CheckWait? Wait);

/// <summary>
/// A repository's heavy-run slots, across its trees: as many runs of its heavy gates at once as the machine's admission admits
/// (<see cref="CheckAdmission"/>), or fewer where the repository says so; a run from any checkout of it that finds none admitted waits
/// and says why and behind whom.
/// </summary>
/// <remarks>
/// <para><b>Why, and why not one.</b> Two driven sessions on one repository each ran its full build and suites at once on the installed
/// Daoris (about 25 node workers and 5 GB between them, a neighbour's shell killed). The cost is memory, so the slots are admitted by the
/// one judgement over every heavy run on the machine: a pinned <c>checks</c> is that many, <c>auto</c> another while the memory holds one
/// more, fewer where the repository says (<see cref="HeavyGates.SlotsKey"/>).</para>
///
/// <para><b>Keyed by the repository, never the tree.</b> The key is the repository's common git folder, read from git's own files with
/// no process (<see cref="TreeTips.CommonDirectory"/>), named by the folder's identity where Windows gives one (a junction, a
/// <c>subst</c> drive, another case or a long-path prefix take the same slot), else by its spelling, as the branch hold's is. So
/// the root checkout and every linked tree of it share one slot, with no registry: a root checkout is no session tree, and the tree
/// list names none for it.</para>
///
/// <para><b>Each handle is a lock</b>, as <see cref="TreeLock"/>'s is: a file under the home's <c>locks/heavy/</c> per slot (the first
/// <c>&lt;key&gt;.lock</c>, the n-th <c>&lt;key&gt;-n.lock</c>), opened alone. A process that dies lets go of it with its handles, so
/// nothing stale blocks, and there is no lease and no timeout anywhere: a timed lease admits overlap the moment a run outlasts it.</para>
///
/// <para><b>Each holder says what it runs</b> in a record beside its lock (<see cref="HeavyHolder"/>), which a waiting run reads to say
/// it, and which the holder takes away as it lets go. One left behind is a holder that died holding that slot: the next run to take it
/// reads it (<see cref="Before"/>) and ends the job it names, if it is still there, before its own gates start.</para>
///
/// <para><b>The page reads who holds it and who waits</b>: the holder rewrites its record at each
/// gate with that gate and its start, and each waiting run keeps a record of its own under <see cref="WaitersOf"/>, gone as it takes
/// the slot or stops. <see cref="Look"/> reads both, and only where the record's process (its id and its start) still runs: never
/// the run's output, and never a record a dead run left as a live hold.</para>
/// </remarks>
public sealed class HeavySlot : IDisposable
{
    private readonly FileStream _file;
    private readonly string _record;

    private HeavySlot(FileStream file, string record, string name, HeavyHolder? before)
    {
        _file = file;
        _record = record;
        Name = name;
        Before = before;
    }

    /// <summary>The slot's own name: the repository's key for its first, <c>&lt;key&gt;-n</c> for the n-th, which names the verification tree a check in it runs in.</summary>
    public string Name { get; }

    /// <summary>The record the slot holds when it is taken: the run before, which died holding it; null where it let go as it should.</summary>
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

    /// <summary>Where a repository's slot at <paramref name="index"/> is, under the home's <c>locks/</c>, the first at 0.</summary>
    public static string PathOf(string home, string key, int index = 0) => DaorisHome.Entry(home, HomeEntry.Locks, "heavy", $"{NameOf(key, index)}.lock");

    /// <summary>Where its holder says what it runs, beside the slot.</summary>
    public static string RecordOf(string home, string key, int index = 0) => DaorisHome.Entry(home, HomeEntry.Locks, "heavy", $"{NameOf(key, index)}.json");

    /// <summary>Where the runs waiting for one of them each say so, one file a run, beside the slots.</summary>
    public static string WaitersOf(string home, string key) => DaorisHome.Entry(home, HomeEntry.Locks, "heavy", $"{key}.waits");

    private static string NameOf(string key, int index) => index == 0 ? key : $"{key}-{index + 1}";

    /// <summary>The first of the repository's first <paramref name="slots"/> slots that is free, or null while runs hold every one. Never waits.</summary>
    /// <remarks>
    /// The file held the other way is an answer: the next slot. A folder that cannot be made, or an error that is no sharing violation, is
    /// thrown, since "cannot tell" is never "free". Off Windows .NET takes an advisory lock, which every Daoris process honours, and
    /// says a held one as any other failure to open, so there every failure to open is read as held.
    /// </remarks>
    public static HeavySlot? TryTake(string home, string key, int slots = 1)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(PathOf(home, key))!);
        for (var index = 0; index < slots; index++)
        {
            FileStream file;
            try
            {
                file = new FileStream(PathOf(home, key, index), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException held) when (!OperatingSystem.IsWindows() || (held.HResult & 0xFFFF) is SharingViolation or LockViolation)
            {
                continue;
            }

            return new HeavySlot(file, RecordOf(home, key, index), NameOf(key, index), Holder(home, key, index));
        }

        return null;
    }

    /// <summary>
    /// A slot, waited for while the machine's admission admits none, saying on <paramref name="said"/> why and behind which runs, at
    /// <paramref name="waiting"/>'s cadence, until one is this run's. An admitted free slot is taken at once, saying nothing.
    /// </summary>
    /// <param name="admit">What admits the run (<see cref="CheckAdmission.TryTake"/>), its rules read at each look; null takes the first slot alone, writing nothing.</param>
    /// <param name="waiter">
    /// What this run writes beside the slots while it waits, so the page can say who waits; taken away as it takes a
    /// slot or stops. Null writes nothing.
    /// </param>
    /// <exception cref="OperationCanceledException">Stopped while waiting; nothing is held.</exception>
    public static async Task<HeavySlot> TakeAsync(
        string home, string key, string repository, TextWriter said, CancellationToken ct, HeavyWaiting? waiting = null, HeavyWaiter? waiter = null,
        HeavyAdmit? admit = null) =>
        (await TakeUnlessAsync(home, key, repository, said, ct, waiting, waiter, yieldTo: null, admit).ConfigureAwait(false)).Slot!;

    /// <summary>
    /// <see cref="TakeAsync"/>, giving up without waiting once a slot of the repository is held by one <paramref name="yieldTo"/> names (a
    /// session's run never waits behind a verification): no slot then, the admission's wait, nothing held and no waiter left.
    /// </summary>
    public static async Task<HeavyTake> TakeUnlessAsync(
        string home, string key, string repository, TextWriter said, CancellationToken ct, HeavyWaiting? waiting, HeavyWaiter? waiter,
        Func<HeavyHolder, bool>? yieldTo, HeavyAdmit? admit = null)
    {
        waiting ??= HeavyWaiting.Default;
        var waited = Stopwatch.StartNew();
        TimeSpan? saidAt = null;
        string? saidOf = null;
        string? waits = null;
        try
        {
            for (var looks = 0; ; looks++)
            {
                ct.ThrowIfCancellationRequested();
                CheckWait? wait = null;
                HeavySlot? slot = null;

                // A run in an order asks only at its turn, so a slot that frees goes to the first of the order; one not yet its turn asks nothing.
                var asked = true;
                if (admit?.Turn is { } turn && !turn.MayTry(out wait))
                {
                    asked = false;
                    if (wait is not null) turn.Waits(wait);
                }
                else if (admit is null)
                {
                    slot = TryTake(home, key);
                }
                else
                {
                    (var taken, wait) = CheckAdmission.TryTake(home, admit.Rules(), new HeavyAsk(key, repository, await admit.Declared(ct).ConfigureAwait(false), admit.Holder) { Agent = admit.Agent, Gates = admit.Gates });
                    slot = taken?.Repository;
                    if (slot is null && wait is not null) admit.Turn?.Refused(wait);
                }

                if (slot is not null)
                {
                    admit?.Turn?.Admitted();
                    if (looks > 0) said.WriteLine($"run-heavy: `{repository}`'s heavy-run slot is this run's, after waiting {Took(waited.Elapsed)}.");
                    return new HeavyTake(slot, null);
                }

                var holders = Records(home, key);
                if (asked && yieldTo is not null && holders.Any(yieldTo)) return new HeavyTake(null, wait);
                if (waiter is not null && waits is null) waits = KeepWaiter(home, key, waiter);
                var of = string.Join('|', holders.Select(holder => $"{holder.Pid}:{holder.Started}:{holder.Since:O}")) + wait?.Code;
                if (saidAt is null || waited.Elapsed - saidAt >= waiting.SayEvery || of != saidOf)
                {
                    said.WriteLine(Waiting(repository, holders, wait, waiting.Clock(), waited.Elapsed));
                    saidAt = waited.Elapsed;
                    saidOf = of;
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
    /// The slots as they stand: their holders and the runs waiting, the longest waiting first, each only where
    /// <paramref name="alive"/> finds its process (its id and its start). Neither file alone is a hold: the handle is the lock, and a
    /// run that died leaves its record behind, so one whose process is gone is read as nothing. Reads files only, and takes nothing.
    /// </summary>
    /// <param name="alive">Whether a process of that id and that start, in UTC ticks, runs; this machine's processes where null.</param>
    public static HeavyLook Look(string home, string key, Func<int, long, bool>? alive = null)
    {
        alive ??= Running;
        var holders = Records(home, key).Where(held => alive(held.Pid, held.Started)).ToList();
        var waiters = new List<HeavyWaiter>();
        foreach (var file in Files(WaitersOf(home, key), "*.json"))
        {
            // A holder's own record is never a wait: a run that took a slot took its wait away first, or is about to.
            if (ReadShared(file) is { } text && ParseWaiter(text) is { } one && alive(one.Pid, one.Started)
                && !holders.Any(holder => holder.Pid == one.Pid && holder.Started == one.Started))
            {
                waiters.Add(one);
            }
        }

        return new HeavyLook(holders, [.. waiters.OrderBy(one => one.Since).ThenBy(one => one.Pid)]);
    }

    /// <summary>The records beside the repository's slots that read, the first taken first, whatever their processes.</summary>
    private static List<HeavyHolder> Records(string home, string key) =>
        [.. Files(Path.GetDirectoryName(PathOf(home, key))!, $"{key}*.json").Select(ReadShared).OfType<string>().Select(Parse).OfType<HeavyHolder>().OrderBy(held => held.Since)];

    /// <summary>Every repository's slots' holders, each with its repository's key, only where its process runs. Reads files only.</summary>
    public static IReadOnlyList<(string Key, HeavyHolder Holder)> Holders(string home, Func<int, long, bool>? alive = null)
    {
        alive ??= Running;
        var held = new List<(string, HeavyHolder)>();
        foreach (var file in Files(Path.GetDirectoryName(PathOf(home, "_"))!, "*.json"))
        {
            var name = Path.GetFileNameWithoutExtension(file);
            var dash = name.LastIndexOf('-');
            var key = dash > 0 && name[(dash + 1)..].All(char.IsAsciiDigit) ? name[..dash] : name;
            if (ReadShared(file) is { } text && Parse(text) is { } holder && alive(holder.Pid, holder.Started)) held.Add((key, holder));
        }

        return held;
    }

    private static List<string> Files(string folder, string pattern)
    {
        try
        {
            return Directory.Exists(folder) ? [.. Directory.EnumerateFiles(folder, pattern)] : [];
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return [];
        }
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

    /// <summary>What the holder of the slot at <paramref name="index"/> says it runs, or null where nothing is said or what is there does not read.</summary>
    public static HeavyHolder? Holder(string home, string key, int index = 0)
        => ReadShared(RecordOf(home, key, index)) is { } text ? Parse(text) : null;

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
            if (holder.Run is { } run) writer.WriteString("run", run);
            if (holder.Built is { } built) writer.WriteString("built", Stamp(built));
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
                at, Text(root, "session"), Text(root, "job")) { Now = NowOf(root), Failed = FailedOf(root), Run = Text(root, "run"), Built = Moment(root, "built") };
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static DateTimeOffset? Moment(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String && value.TryGetDateTimeOffset(out var at) ? at : null;

    private static string? Text(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    /// <summary>Why the run waits: behind its repository's holders, or for the machine's memory or slots, beside them where there are any.</summary>
    internal static string Waiting(string repository, IReadOnlyList<HeavyHolder> holders, CheckWait? wait, DateTimeOffset now, TimeSpan waited)
    {
        var who = holders.Count == 0 ? "a run that has not said what it runs yet" : string.Join("; ", holders.Select(holder =>
            (holder.Run is { } run ? $"the verification run {run}" : holder.Session is { } session ? $"session {session} (process {holder.Pid})" : $"process {holder.Pid}")
            + $", running {(holder.Gates.Count == 0 ? "its gates" : string.Join(", ", holder.Gates))} for {Took(now - holder.Since)} in {holder.Checkout}"));
        var why = wait?.Code is CheckWait.Memory or CheckWait.Slots or CheckWait.Admitting
            ? $"waits for {wait.Sentence}{(holders.Count > 0 ? $", beside {who}" : "")}"
            : $"is held by {who}";
        return $"run-heavy: `{repository}`'s heavy-run slot {why}; this run waits for it ({Took(waited)} so far).";
    }

    private static string? Common(string directory) =>
        CheckoutOf(directory) is { } checkout ? TreeTips.CommonDirectory(checkout) : null;

    /// <remarks>As the branch hold names a folder: its identity where Windows gives one, else its spelling.</remarks>
    private static string Identity(string folder) =>
        (OperatingSystem.IsWindows() ? FolderIdentity.Of(folder) : null)
        ?? Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder)).ToLowerInvariant();

    private const int SharingViolation = 32;
    private const int LockViolation = 33;
}
