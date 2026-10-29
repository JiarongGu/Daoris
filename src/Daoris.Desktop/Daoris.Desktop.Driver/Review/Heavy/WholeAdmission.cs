using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// One whole take of the machine's admission and what it holds (INTEGRATE1f part f2): see <see cref="MachineAdmission.TakeWholeAsync"/>.
/// It is the driver's twin of <c>createAdmission().takeWhole</c> in <c>tools/machine-admission.mjs</c>: the same entries, the same
/// readings, the same doubts in the same words, so a record this writes is one the tool counts, and the other way round.
/// </summary>
internal sealed class WholeAdmission
{
    /// <summary>The kind the whole take's entries are: the merge tool's, whose runs the integration step replaces.</summary>
    internal const string EntryKind = "queue";

    private readonly string _file;
    private readonly AdmissionWaiting _waiting;
    private readonly string _name;
    private readonly int _size;
    private readonly string[] _ids;
    private readonly HashSet<string> _mine;
    private readonly List<WholeDoubt> _doubts = [];
    private readonly object _gate = new();
    private readonly DateTimeOffset _asked;
    private Timer? _beat;
    private bool _started;
    private bool _over;
    private long? _waitedMs;
    private WholeEvidence? _ended;

    private WholeAdmission(string file, AdmissionWaiting waiting, string name, int size, string[] ids)
    {
        (_file, _waiting, _name, _size, _ids) = (file, waiting, name, size, ids);
        _mine = [.. ids];
        _asked = waiting.Clock();
    }

    private sealed record State(List<AdmissionEntry> Holds, List<AdmissionEntry> Waits, int Unread);

    internal static async Task<WholeTake> TakeAsync(
        string home, AdmissionRequest request, TextWriter said, Func<bool> halted, CancellationToken ct, AdmissionWaiting waiting,
        Func<string, string?> variable, int? capacity)
    {
        var size = capacity ?? MachineAdmission.CapacityOf(variable, Environment.ProcessorCount);
        var file = MachineAdmission.PathOf(home);
        var tag = $"{Environment.ProcessId}-{Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(4))}";
        var ids = Enumerable.Range(1, size).Select(at => $"{tag}-{at:D2}").ToArray();
        var whole = new WholeAdmission(file, waiting, request.What, size, ids);
        if (variable(MachineAdmission.InsideVariable) is { Length: > 0 } outer
            && MachineAdmission.Pruned(MachineAdmission.Read(file), waiting.Clock(), waiting.Alive).Holds.Any(entry => entry.Id == outer))
        {
            return whole.Refuse("take", $"it runs inside the hold {outer}, which holds a place it cannot take");
        }

        var since = waiting.Clock();
        var entries = ids.Select((id, at) => new AdmissionEntry(
            id, EntryKind, Environment.ProcessId, $"{request.What} (place {at + 1} of {size})", since, since, request.Session, request.Repository, request.Tree)).ToList();
        string? refused = null;
        try
        {
            whole.Change(
                state =>
                {
                    var own = state.Holds.Concat(state.Waits).Where(entry => entry.Pid == Environment.ProcessId).ToList();
                    if (state.Unread > 0)
                    {
                        refused = $"the record keeps {state.Unread} {(state.Unread == 1 ? "hold that does" : "holds that do")} not read as an entry, so who holds the machine is unknown";
                    }
                    else if (own.Count > 0)
                    {
                        refused = $"its own process holds or asks a place already ({string.Join("; ", own.Select(entry => entry.What))}): the failing gate's own place is let go before the whole admission is taken, which would otherwise wait on itself";
                    }

                    if (refused is not null) return null;
                    return (state.Holds, new List<AdmissionEntry>([.. state.Waits, .. entries]));
                },
                missing: true);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            return whole.Refuse("take", $"the machine's record could not be written ({error.Message})");
        }

        if (refused is not null) return whole.Refuse("take", refused);
        return await whole.WaitAsync(said, halted, ct).ConfigureAwait(false);
    }

    private WholeTake Refuse(string at, string why)
    {
        Doubt(at, why);
        return new WholeTake(false, Why: why, Evidence: Seal()) { Ids = [] };
    }

    private void Doubt(string at, string why)
    {
        lock (_gate) _doubts.Add(new WholeDoubt(at, why));
    }

    private string? FirstDoubt()
    {
        lock (_gate) return _doubts.Count > 0 ? _doubts[0].Why : null;
    }

    private WholeEvidence Seal()
    {
        lock (_gate)
        {
            return new WholeEvidence(_doubts.Count == 0 && Volatile.Read(ref _started) && _over, [.. _doubts], _waitedMs, [.. _ids]);
        }
    }

    /// <summary>The file changed under its lock, read strictly: a missing file is empty only where <paramref name="missing"/> allows it, any other read that fails throws.</summary>
    private void Change(Func<State, (List<AdmissionEntry> Holds, List<AdmissionEntry> Waits)?> change, bool missing = false) => Changed(change, missing);

    private State Changed(Func<State, (List<AdmissionEntry> Holds, List<AdmissionEntry> Waits)?> change, bool missing = false)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_file)!);
        using var held = KeepLock.Take(_file);
        var (holds, waits, unread) = ReadStrict(_file, missing);
        var (liveHolds, liveWaits) = MachineAdmission.Pruned((holds, waits), _waiting.Clock(), _waiting.Alive);
        var state = new State(liveHolds, liveWaits, unread);
        if (change(state) is not { } next) return state;
        AtomicFile.WriteText(_file, MachineAdmission.Serialize(next.Holds, next.Waits));
        return state with { Holds = next.Holds, Waits = next.Waits };
    }

    /// <summary>
    /// The record read strictly (<c>readStrict</c>): both lists of the entries that read, and how many holds do not read as an entry. A
    /// record that is missing (unless allowed), does not read as JSON, is not an object or keeps a list that is not a list throws, since
    /// its holds are unknown.
    /// </summary>
    internal static (List<AdmissionEntry> Holds, List<AdmissionEntry> Waits, int Unread) ReadStrict(string file, bool missing)
    {
        string text;
        try
        {
            using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream, Encoding.UTF8);
            text = reader.ReadToEnd();
        }
        catch (Exception error) when (missing && error is FileNotFoundException or DirectoryNotFoundException)
        {
            return ([], [], 0);
        }

        using var document = JsonDocument.Parse(text);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object) throw new InvalidDataException("the record is not an object");
        List<AdmissionEntry?> Entries(string name)
        {
            if (!root.TryGetProperty(name, out var list)) return [];
            if (list.ValueKind != JsonValueKind.Array) throw new InvalidDataException($"its {name} is not a list");
            return [.. list.EnumerateArray().Select(MachineAdmission.Entry)];
        }

        var holds = Entries("holds");
        return ([.. holds.OfType<AdmissionEntry>()], [.. Entries("waits").OfType<AdmissionEntry>()], holds.Count(entry => entry is null));
    }

    /// <summary>
    /// What doubts the whole admission at one reading (<c>wholeDoubts</c>), over the record pruned and read strictly: each of its own
    /// places not held, any live hold not its own, and any hold that does not read as an entry. The asks behind it are no doubt.
    /// </summary>
    private IEnumerable<string> Doubts(State state)
    {
        var held = state.Holds.Where(entry => _mine.Contains(entry.Id)).Select(entry => entry.Id).ToHashSet(StringComparer.Ordinal);
        var lost = _ids.Where(id => !held.Contains(id)).ToList();
        var others = state.Holds.Where(entry => !_mine.Contains(entry.Id)).ToList();
        if (lost.Count > 0) yield return $"{lost.Count} of its {_ids.Length} places {(lost.Count == 1 ? "is" : "are")} no longer held ({string.Join(", ", lost)})";
        if (others.Count > 0) yield return $"another live hold stands beside it: {string.Join("; ", others.Select(Who))}";
        if (state.Unread > 0) yield return $"the record keeps {state.Unread} {(state.Unread == 1 ? "hold that does" : "holds that do")} not read as an entry";
    }

    private static string Who(AdmissionEntry entry) =>
        $"{(entry.Kind == "queue" ? "the merge queue" : entry.Session is { } session ? $"session {session}" : "a driven run")}'s {entry.What}";

    private (List<AdmissionEntry>, List<AdmissionEntry>) Drop(State state) =>
        (state.Holds.Where(entry => !_mine.Contains(entry.Id)).ToList(), state.Waits.Where(entry => !_mine.Contains(entry.Id)).ToList());

    private void LetGo()
    {
        try
        {
            Change(state => Drop(state));
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            try
            {
                MachineAdmission.Change(_file, _waiting, (holds, waits) => ([.. holds.Where(entry => !_mine.Contains(entry.Id))], [.. waits.Where(entry => !_mine.Contains(entry.Id))]));
            }
            catch (Exception second) when (second is IOException or UnauthorizedAccessException)
            {
                // Left: they stop counting when this process ends, or two minutes on.
            }
        }
    }

    /// <summary>One write beats every place; from the start on, the record is read again for its doubts.</summary>
    private void Beat()
    {
        try
        {
            Change(state =>
            {
                if (Volatile.Read(ref _started)) foreach (var why in Doubts(state)) Doubt("beat", why);
                var now = _waiting.Clock();
                return (
                    state.Holds.Select(entry => _mine.Contains(entry.Id) ? entry with { Beat = now } : entry).ToList(),
                    state.Waits.Select(entry => _mine.Contains(entry.Id) ? entry with { Beat = now } : entry).ToList());
            });
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            Doubt(Volatile.Read(ref _started) ? "beat" : "wait", $"the machine's record could not be read or written at a beat ({error.Message})");
        }
    }

    private async Task<WholeTake> WaitAsync(TextWriter said, Func<bool> halted, CancellationToken ct)
    {
        _beat = new Timer(_ => Beat(), null, _waiting.BeatEvery, _waiting.BeatEvery);
        TimeSpan? saidAt = null;
        string? saidOf = null;
        var asked = System.Diagnostics.Stopwatch.StartNew();
        while (true)
        {
            if (FirstDoubt() is { } doubted)
            {
                Stop();
                return new WholeTake(false, Why: doubted, Evidence: Seal());
            }

            IReadOnlyList<string> gone = [];
            State next;
            try
            {
                next = Changed(state =>
                {
                    gone = [.. _ids.Where(id => !state.Holds.Concat(state.Waits).Any(entry => entry.Id == id))];
                    if (gone.Count > 0) return null;
                    var holds = state.Holds;
                    var waits = state.Waits;
                    foreach (var id in _ids)
                    {
                        if (holds.Any(entry => entry.Id == id)) continue;
                        if (!MachineAdmission.Judge(holds, waits, id, _size).Admitted) break;
                        holds = [.. holds, waits.First(entry => entry.Id == id) with { Beat = _waiting.Clock() }];
                        waits = [.. waits.Where(entry => entry.Id != id)];
                    }

                    return (holds, waits);
                });
            }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                Doubt("wait", $"the machine's record could not be read or written while it waited ({error.Message})");
                continue;
            }

            if (gone.Count > 0)
            {
                Doubt("wait", $"{gone.Count} of its {_size} places left the record while it waited ({string.Join(", ", gone)})");
                continue;
            }

            if (_ids.All(id => next.Holds.Any(entry => entry.Id == id))) break;
            if (halted())
            {
                Stop();
                return new WholeTake(false, Dropped: true, Why: $"the wait for the whole admission before {_name} was ended", Evidence: Seal());
            }

            var first = _ids.First(id => next.Holds.All(entry => entry.Id != id));
            var (_, ahead) = MachineAdmission.Judge(next.Holds, next.Waits, first, _size);
            var key = string.Join(',', next.Holds.Select(entry => entry.Id)) + "/" + ahead;
            if (saidAt is null || asked.Elapsed - saidAt >= _waiting.SayEvery || key != saidOf)
            {
                said.WriteLine($"integration: waiting for the machine's whole heavy-run admission before {_name}, all {_size} places: {MachineAdmission.Sentence(next.Holds, ahead, _size, _waiting.Clock())}; it never times out, and looks again every {(int)_waiting.Poll.TotalSeconds}s.");
                saidAt = asked.Elapsed;
                saidOf = key;
            }

            try
            {
                await Task.Delay(_waiting.Poll, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Woken early: the drop is read at the next look.
            }
        }

        Volatile.Write(ref _started, true);
        _waitedMs = (long)(_waiting.Clock() - _asked).TotalMilliseconds;
        if (saidAt is not null) said.WriteLine($"integration: {_name} holds every place after waiting {HeavySlot.Took(asked.Elapsed)}.");
        try
        {
            var raw = ReadStrict(_file, missing: false);
            var (holds, waits) = MachineAdmission.Pruned((raw.Holds, raw.Waits), _waiting.Clock(), _waiting.Alive);
            foreach (var why in Doubts(new State(holds, waits, raw.Unread))) Doubt("start", why);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            Doubt("start", $"the machine's record could not be read or written at its start ({error.Message})");
        }

        return new WholeTake(true, End) { Ids = [.. _ids] };
    }

    private void Stop()
    {
        _beat?.Dispose();
        LetGo();
    }

    /// <summary>Let every place go, reading the record once more for its doubts; answers the evidence, the same every time it is asked.</summary>
    private WholeEvidence End()
    {
        lock (_gate)
        {
            if (_ended is not null) return _ended;
        }

        _beat?.Dispose();
        try
        {
            Change(state =>
            {
                foreach (var why in Doubts(state)) Doubt("end", why);
                return Drop(state);
            });
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            Doubt("end", $"the machine's record could not be read or written at its end ({error.Message})");
            LetGo();
        }

        lock (_gate) _over = true;
        var evidence = Seal();
        lock (_gate) _ended = evidence;
        return evidence;
    }
}
