using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// One heavy run's place in the machine's admission (HEAVYQUEUE1), as its file holds it: <c>{ id, kind, pid, what, since, beat }</c> and,
/// where there is one, the driven <c>session</c>, the <c>repository</c> and the <c>tree</c> it runs in.
/// </summary>
/// <param name="Id">Unique to the run: its process and a random part.</param>
/// <param name="Kind"><c>queue</c> for the merge tool's gate, <c>session</c> for a driven session's or a terminal's run.</param>
/// <param name="Pid">The process that holds or waits; the entry counts only while it runs.</param>
/// <param name="What">What the run is, in words: <c>driver gate</c>, <c>dotnet test --filter Foo</c>.</param>
/// <param name="Since">When the run began to ask (a wait's) or took its place (a hold's: it keeps the time it began to ask).</param>
/// <param name="Beat">When its writer last said it lives; an entry that has not beat for <see cref="MachineAdmission.StaleAfter"/> counts for nothing.</param>
public sealed record AdmissionEntry(
    string Id, string Kind, int Pid, string What, DateTimeOffset Since, DateTimeOffset Beat, string? Session, string? Repository, string? Tree);

/// <summary>The admission as it stands: its capacity, the runs holding a place and the runs waiting, each only while it counts.</summary>
public sealed record AdmissionLook(int Capacity, IReadOnlyList<AdmissionEntry> Holds, IReadOnlyList<AdmissionEntry> Waits);

/// <summary>How a run waiting for the admission looks again and how often it says it still waits; and the clock its entries are read by.</summary>
public sealed record AdmissionWaiting(TimeSpan Poll, TimeSpan SayEvery, TimeSpan BeatEvery, Func<DateTimeOffset> Clock, Func<int, bool> Alive)
{
    /// <summary>A look every two seconds, said every minute: a heavy run is minutes long, and its waiter is told without being flooded.</summary>
    public static AdmissionWaiting Default { get; } = new(
        TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(60), MachineAdmission.BeatEvery, () => DateTimeOffset.UtcNow, KeepLock.Lives);
}

/// <summary>What a run asks the admission for.</summary>
/// <param name="What">What the run is, in words.</param>
public sealed record AdmissionRequest(string What)
{
    /// <summary>The driven session whose run it is.</summary>
    public string? Session { get; init; }

    /// <summary>The repository it runs in, by name.</summary>
    public string? Repository { get; init; }

    /// <summary>The tree it runs in.</summary>
    public string? Tree { get; init; }

    /// <summary>The entry's kind: <c>session</c> for a driven session's run, <c>queue</c> for the integration's own gates (the merge tool's kind, which <see cref="MachineAdmission.Sentence"/> names as the merge queue's).</summary>
    public string Kind { get; init; } = "session";
}

/// <summary>
/// The machine's heavy-run admission (HEAVYQUEUE1, D180's HEAVYRUN1 notes): a driven session's heavy run and the merge queue's heavy gates
/// take one place each before they start a heavy process, so the machine never runs more of them at once than it admits, and a run that
/// waits is told what it waits for. This is the driver's side; <c>tools/machine-admission.mjs</c> is the merge tool's, and
/// <c>fixtures/machine-admission.json</c> the table both carry.
/// </summary>
/// <remarks>
/// <para><b>One file in the home</b> (the table's <c>locks</c> entry): <c>admission/admission.json</c>, <c>{ schema, holds, waits }</c>. A run
/// asks by adding itself to <c>waits</c>, is admitted when the live holds, plus the live waits before it, are fewer than the
/// <see cref="CapacityOf"/>, and moves itself to <c>holds</c>; it lets go by taking itself out. First come, first served, a queue gate and
/// a session's run alike, so neither starves the other.</para>
///
/// <para><b>A holder that dies releases.</b> An entry counts only while its process runs and it has beaten within <see cref="StaleAfter"/>
/// (its writer rewrites <c>beat</c> every <see cref="BeatEvery"/>), so a killed run frees its place at once, and a reused process id cannot
/// hold one past two minutes. There is no lease and no timeout otherwise: a run that lives and beats holds its place as long as it runs.</para>
///
/// <para><b>Changed under the keeping write's lock</b> (<see cref="KeepLock"/>, <c>keeplock.ts</c>): the file is put in place whole, and the
/// waiting is done outside the lock, by looking again. Of the repository's per-repository slot (<see cref="HeavySlot"/>, the handle is
/// the lock) it is the machine-wide counterpart: that one says one run of a repository's gates at a time, this one how many heavy runs
/// the machine holds at all. A Node process cannot hold a handle another program's open refuses, which is why the two are not one.</para>
/// </remarks>
public static class MachineAdmission
{
    /// <summary>How long an entry that has not beaten still counts (<c>STALE_MS</c>).</summary>
    public static readonly TimeSpan StaleAfter = TimeSpan.FromSeconds(120);

    /// <summary>How often a live entry beats (<c>BEAT_MS</c>).</summary>
    public static readonly TimeSpan BeatEvery = TimeSpan.FromSeconds(20);

    /// <summary>The variable that sets the capacity on a machine (<c>CAPACITY</c>).</summary>
    public const string CapacityVariable = "DAORIS_ADMISSION_SLOTS";

    /// <summary>
    /// The variable naming the hold a process runs inside (<c>INSIDE</c>): a run an admitted run starts is inside its hold, and takes no
    /// second place, which would wait on the run that started it.
    /// </summary>
    public const string InsideVariable = "DAORIS_ADMISSION_HOLD";

    /// <summary>Where the admission's file is, in the home's <c>locks</c> entry.</summary>
    public static string PathOf(string home) => DaorisHome.Entry(home, HomeEntry.Locks, "admission", "admission.json");

    /// <summary>
    /// How many heavy runs the machine admits at once (<c>capacityOf</c>): the variable where it is a whole number from 1 to 64, else one for
    /// each seven processors, two at least and six at most. On the 22-processor machine this was built for it is three.
    /// </summary>
    public static int CapacityOf(Func<string, string?> variable, int processors)
    {
        var said = variable(CapacityVariable)?.Trim() ?? "";
        if (said.Length is >= 1 and <= 3 && said.All(char.IsAsciiDigit) && int.Parse(said, CultureInfo.InvariantCulture) is >= 1 and <= 64 and var set) return set;
        return Math.Min(6, Math.Max(2, processors / 7));
    }

    /// <summary>This process's: the variable and the processors it sees.</summary>
    public static int Capacity() => CapacityOf(Environment.GetEnvironmentVariable, Environment.ProcessorCount);

    /// <summary>Whether an entry still counts (<c>live</c>): its process runs and it beat within <see cref="StaleAfter"/>.</summary>
    public static bool Live(AdmissionEntry entry, DateTimeOffset now, Func<int, bool> alive) => alive(entry.Pid) && now - entry.Beat < StaleAfter;

    /// <summary>
    /// Whether the run <paramref name="id"/> is admitted (<c>judge</c>) over a state with only live entries, <paramref name="waits"/> in the
    /// order they began to ask: when the holds plus the waits before it are fewer than <paramref name="capacity"/>. A run not among the waits
    /// is last in line.
    /// </summary>
    public static (bool Admitted, int Ahead) Judge(IReadOnlyList<AdmissionEntry> holds, IReadOnlyList<AdmissionEntry> waits, string id, int capacity)
    {
        var at = waits.ToList().FindIndex(entry => entry.Id == id);
        var ahead = at < 0 ? waits.Count : at;
        return (holds.Count + ahead < capacity, ahead);
    }

    /// <summary>The admission as it stands, read only: the entries that count, the waits in the order they began to ask.</summary>
    public static AdmissionLook Look(string home, Func<int, bool>? alive = null, DateTimeOffset? now = null, int? capacity = null)
    {
        var (holds, waits) = Pruned(Read(PathOf(home)), now ?? DateTimeOffset.UtcNow, alive ?? KeepLock.Lives);
        return new AdmissionLook(capacity ?? Capacity(), holds, waits);
    }

    /// <summary>
    /// Take a place, waiting for it with no bound, saying on <paramref name="said"/> what it waits for (at <paramref name="waiting"/>'s
    /// cadence) in a sentence. A run inside a hold (<see cref="InsideVariable"/>) takes none.
    /// </summary>
    /// <param name="capacity">The capacity, where not this machine's (<see cref="Capacity"/>).</param>
    /// <exception cref="OperationCanceledException">Stopped while waiting; nothing is held and nothing left waiting.</exception>
    public static async Task<AdmissionHold> TakeAsync(
        string home, AdmissionRequest request, TextWriter said, CancellationToken ct, AdmissionWaiting? waiting = null, int? capacity = null,
        Func<string, string?>? variable = null)
    {
        waiting ??= AdmissionWaiting.Default;
        variable ??= Environment.GetEnvironmentVariable;
        var file = PathOf(home);
        if (variable(InsideVariable) is { Length: > 0 } outer
            && Pruned(Read(file), waiting.Clock(), waiting.Alive).Holds.Any(entry => entry.Id == outer))
        {
            return AdmissionHold.Inside(outer);
        }

        var id = $"{Environment.ProcessId}-{Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(4))}";
        var mine = new AdmissionEntry(id, request.Kind, Environment.ProcessId, request.What, waiting.Clock(), waiting.Clock(), request.Session, request.Repository, request.Tree);
        Change(file, waiting, (holds, waits) => (holds, [.. waits, mine]));
        var hold = new AdmissionHold(file, id, waiting);
        hold.Begin();
        var asked = Stopwatch.StartNew();
        TimeSpan? saidAt = null;
        string? saidOf = null;
        try
        {
            while (true)
            {
                ct.ThrowIfCancellationRequested();
                AdmissionLook look = default!;
                var admitted = false;
                Change(file, waiting, (holds, waits) =>
                {
                    var size = capacity ?? CapacityOf(variable, Environment.ProcessorCount);
                    var (ok, ahead) = Judge(holds, waits, id, size);
                    look = new AdmissionLook(size, holds, waits);
                    admitted = ok;
                    return ok
                        ? ([.. holds, mine with { Beat = waiting.Clock() }], waits.Where(entry => entry.Id != id).ToList())
                        : (holds, waits);
                });
                if (admitted)
                {
                    if (saidAt is not null) said.WriteLine($"run-heavy: the machine's heavy-run admission is this run's, after waiting {HeavySlot.Took(asked.Elapsed)}.");
                    return hold;
                }

                var (_, ahead2) = Judge(look.Holds, look.Waits, id, look.Capacity);
                var key = string.Join(',', look.Holds.Select(entry => entry.Id)) + "/" + ahead2;
                if (saidAt is null || asked.Elapsed - saidAt >= waiting.SayEvery || key != saidOf)
                {
                    said.WriteLine($"run-heavy: waiting for the machine's heavy-run admission before {request.What}: {Sentence(look.Holds, ahead2, look.Capacity, waiting.Clock())}; it never times out, and looks again every {(int)waiting.Poll.TotalSeconds}s.");
                    saidAt = asked.Elapsed;
                    saidOf = key;
                }

                await Task.Delay(waiting.Poll, ct).ConfigureAwait(false);
            }
        }
        catch
        {
            hold.Dispose();
            throw;
        }
    }

    /// <summary>
    /// The whole admission, for a confirm run (INTEGRATE1f part f2; the integration design §4.5, C7, F34): every place the machine admits,
    /// taken as that many ordinary entries of today's schema in one locked write, so they stand together in first-come order, wait behind
    /// the live holds and no other ask falls between them. It answers once every one holds (<see cref="WholeTake.Held"/>); the holds it
    /// sees while it waits are the runs it waits for, never a doubt. From then to <see cref="WholeTake.End"/> it reads the record again at
    /// its start, at each beat and at its end: a live hold not its own, a place no longer held, a hold that does not read, or a read or a
    /// write that fails is a doubt, and the evidence is then not conclusive (the confirm run is inconclusive, read as split).
    /// </summary>
    /// <remarks>
    /// <b>A twin</b> of <c>takeWhole</c> in <c>tools/machine-admission.mjs</c>, the entries of the kind <c>queue</c>, ids
    /// <c>&lt;pid&gt;-&lt;hex&gt;-&lt;nn&gt;</c> and one <c>since</c>. The caller lets go every place this process holds or asks before it (the
    /// failing gate's own), which would otherwise be waited on by itself: the take refuses at once, writing nothing, where this process
    /// holds or asks a live place, or where a hold does not read. Another process's holds, a session's <c>run-heavy</c> among them, are
    /// waited for. A run the confirm run starts is handed the first id as <see cref="InsideVariable"/>, so it asks no place of its own.
    /// </remarks>
    /// <param name="halted">Whether the candidate was dropped: asked at each look while it waits, the wait then ends with every place let go.</param>
    /// <param name="ct">Wakes the wait early; it ends nothing by itself, <paramref name="halted"/> does.</param>
    internal static Task<WholeTake> TakeWholeAsync(
        string home, AdmissionRequest request, TextWriter said, Func<bool> halted, CancellationToken ct, AdmissionWaiting? waiting = null, int? capacity = null,
        Func<string, string?>? variable = null) =>
        WholeAdmission.TakeAsync(home, request, said, halted, ct, waiting ?? AdmissionWaiting.Default, variable ?? Environment.GetEnvironmentVariable, capacity);

    /// <summary>What a run waits for, as a sentence (<c>waitingSentence</c>): each hold, who and what and for how long, and the runs before it.</summary>
    public static string Sentence(IReadOnlyList<AdmissionEntry> holds, int ahead, int capacity, DateTimeOffset now)
    {
        string Who(AdmissionEntry entry) =>
            $"{(entry.Kind == "queue" ? "the merge queue" : entry.Session is { } session ? $"session {session}" : "a driven run")}'s {entry.What} ({Span(now - entry.Since)})";
        return $"the machine runs {holds.Count} of {capacity} heavy runs: {(holds.Count == 0 ? "none" : string.Join("; ", holds.Select(Who)))}"
               + (ahead > 0 ? $"; {ahead} {(ahead == 1 ? "run asked" : "runs asked")} before this one" : "");
    }

    private static string Span(TimeSpan span)
    {
        var seconds = (int)Math.Max(0, Math.Round(span.TotalSeconds));
        return seconds >= 3600 ? $"{seconds / 3600}h {seconds % 3600 / 60}m" : seconds >= 60 ? $"{seconds / 60}m {seconds % 60}s" : $"{seconds}s";
    }

    /// <summary>The file read: every entry that reads, in the order kept. A file that is missing or does not read is empty.</summary>
    internal static (List<AdmissionEntry> Holds, List<AdmissionEntry> Waits) Read(string file)
    {
        try
        {
            using var document = JsonDocument.Parse(ReadShared(file) ?? "");
            var root = document.RootElement;
            return (List(root, "holds"), List(root, "waits"));
        }
        catch (Exception error) when (error is JsonException or IOException or UnauthorizedAccessException)
        {
            return ([], []);
        }
    }

    private static List<AdmissionEntry> List(JsonElement root, string name) =>
        root.ValueKind == JsonValueKind.Object && root.TryGetProperty(name, out var list) && list.ValueKind == JsonValueKind.Array
            ? [.. list.EnumerateArray().Select(Entry).OfType<AdmissionEntry>()]
            : [];

    internal static AdmissionEntry? Entry(JsonElement raw)
    {
        if (raw.ValueKind != JsonValueKind.Object) return null;
        string? Text(string name) => raw.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString()) ? value.GetString() : null;
        DateTimeOffset? At(string name) =>
            Text(name) is { } text && DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var at) ? at : null;
        var kind = Text("kind");
        if (Text("id") is not { } id || kind is not ("queue" or "session")
            || !raw.TryGetProperty("pid", out var pid) || !pid.TryGetInt32(out var process)
            || !raw.TryGetProperty("what", out var what) || what.ValueKind != JsonValueKind.String
            || At("since") is not { } since || At("beat") is not { } beat)
        {
            return null;
        }

        return new AdmissionEntry(id, kind, process, what.GetString()!, since, beat, Text("session"), Text("repository"), Text("tree"));
    }

    /// <summary>The state with the entries that no longer count taken out; the waits in the order they began to ask, then by id.</summary>
    internal static (List<AdmissionEntry> Holds, List<AdmissionEntry> Waits) Pruned(
        (List<AdmissionEntry> Holds, List<AdmissionEntry> Waits) state, DateTimeOffset now, Func<int, bool> alive) =>
        ([.. state.Holds.Where(entry => Live(entry, now, alive))],
            [.. state.Waits.Where(entry => Live(entry, now, alive)).OrderBy(entry => entry.Since).ThenBy(entry => entry.Id, StringComparer.Ordinal)]);

    /// <summary>The file changed under its lock: read, pruned, handed to <paramref name="change"/>, written whole.</summary>
    internal static void Change(
        string file, AdmissionWaiting waiting,
        Func<List<AdmissionEntry>, List<AdmissionEntry>, (List<AdmissionEntry> Holds, List<AdmissionEntry> Waits)> change)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        using var lockHeld = KeepLock.Take(file);
        var (holds, waits) = Pruned(Read(file), waiting.Clock(), waiting.Alive);
        (holds, waits) = change(holds, waits);
        AtomicFile.WriteText(file, Serialize(holds, waits));
    }

    /// <summary>The file as it is written whole: the form the merge tool writes and reads (<c>admission.json</c>).</summary>
    public static string Serialize(IReadOnlyList<AdmissionEntry> holds, IReadOnlyList<AdmissionEntry> waits)
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            writer.WriteNumber("schema", 1);
            foreach (var (name, list) in new[] { ("holds", holds), ("waits", waits) })
            {
                writer.WriteStartArray(name);
                foreach (var entry in list) Write(writer, entry);
                writer.WriteEndArray();
            }

            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.ToArray()).ReplaceLineEndings("\n") + "\n";
    }

    private static void Write(Utf8JsonWriter writer, AdmissionEntry entry)
    {
        writer.WriteStartObject();
        writer.WriteString("id", entry.Id);
        writer.WriteString("kind", entry.Kind);
        writer.WriteNumber("pid", entry.Pid);
        writer.WriteString("what", entry.What);
        writer.WriteString("since", Stamp(entry.Since));
        writer.WriteString("beat", Stamp(entry.Beat));
        if (entry.Session is { } session) writer.WriteString("session", session);
        if (entry.Repository is { } repository) writer.WriteString("repository", repository);
        if (entry.Tree is { } tree) writer.WriteString("tree", tree);
        writer.WriteEndObject();
    }

    internal static string Stamp(DateTimeOffset at) => at.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);

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
}

/// <summary>
/// A place in the machine's admission, held until it is disposed (HEAVYQUEUE1): it beats while it lives, and takes its entry out as it
/// ends. A hold that was never taken (a wait stopped) leaves no entry. Disposing twice lets go once.
/// </summary>
public sealed class AdmissionHold : IDisposable
{
    private readonly string? _file;
    private readonly AdmissionWaiting? _waiting;
    private Timer? _beat;
    private int _released;

    internal AdmissionHold(string file, string id, AdmissionWaiting waiting)
    {
        _file = file;
        Id = id;
        _waiting = waiting;
    }

    private AdmissionHold(string id) => Id = id;

    /// <summary>The hold's id: what a run it starts is handed as <see cref="MachineAdmission.InsideVariable"/>.</summary>
    public string Id { get; }

    /// <summary>Whether this is the place of a run this one is inside, which this run does not own and does not let go.</summary>
    public bool IsInside => _file is null;

    internal static AdmissionHold Inside(string id) => new(id);

    /// <summary>From the moment it asks: its entry beats, a wait's and then a hold's, so it counts as long as its process lives.</summary>
    internal void Begin() => _beat = new Timer(_ => Beat(), null, _waiting!.BeatEvery, _waiting.BeatEvery);

    /// <summary>The beat the entry is rewritten with, so it counts as long as this process runs. A beat missed is the next one's.</summary>
    private void Beat()
    {
        if (_file is null || Volatile.Read(ref _released) != 0) return;
        try
        {
            MachineAdmission.Change(_file, _waiting!, (holds, waits) =>
                ([.. holds.Select(entry => entry.Id == Id ? entry with { Beat = _waiting!.Clock() } : entry)],
                    [.. waits.Select(entry => entry.Id == Id ? entry with { Beat = _waiting!.Clock() } : entry)]));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ObjectDisposedException)
        {
            // Stays until it is stale.
        }
    }

    /// <summary>Let go: the entry taken out, however the run ended.</summary>
    public void Dispose()
    {
        if (_file is null || Interlocked.Exchange(ref _released, 1) != 0) return;
        _beat?.Dispose();
        try
        {
            MachineAdmission.Change(_file, _waiting!, (holds, waits) => ([.. holds.Where(entry => entry.Id != Id)], [.. waits.Where(entry => entry.Id != Id)]));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // Left: it stops counting when its process ends, or two minutes on.
        }
    }
}
