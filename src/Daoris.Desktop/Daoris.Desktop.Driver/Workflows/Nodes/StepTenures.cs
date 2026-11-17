using System.Text.Json;
using System.Text.Json.Nodes;
using static Daoris.Driver.Json;

namespace Daoris.Driver;

/// <summary>
/// One holder's tenure of a step's attempt: the role it fills, its holder as the holder's channel names it, its epoch (1 for
/// the attempt's first holder, one more at each change), since when and why. A word from an ended epoch is refused.
/// </summary>
public sealed record StepTenure(string Node, int Attempt, string Role, string Holder, int Epoch, DateTimeOffset Since, string Why);

/// <summary>Why a tenure began.</summary>
public static class TenureWhy
{
    /// <summary>The place cast the role's holder at the attempt's start.</summary>
    public const string Cast = "cast";

    /// <summary>The holder or its supervisor handed it over.</summary>
    public const string HandOver = "hand-over";

    /// <summary>The holder's account reached its cap.</summary>
    public const string Cap = "cap";

    /// <summary>The holder made no progress within its lease.</summary>
    public const string Stall = "stall";

    /// <summary>The owner took it.</summary>
    public const string TakeBack = "take-back";

    /// <summary>Given back to the holder the place casts now.</summary>
    public const string GiveBack = "give-back";

    /// <summary>The place's cast at a destination the owner marked: the owner role holds it, and no hand-over moves it (<see cref="SettleDoor.Marked"/>).</summary>
    public const string Marked = "marked";
}

/// <summary>What a write of a tenure came to: the tenure it began, or why it wrote nothing, and the tenure holding the attempt now.</summary>
public sealed record TenureHanded(StepTenure? Tenure, string? Refused = null, StepTenure? Now = null);

/// <summary>
/// The steps' tenures under a home: one file per unit, <c>&lt;home&gt;/workflows/tenures/&lt;key&gt;.json</c>, its key the unit's record's,
/// a folder no older build reads, so nothing of a tenure reaches the node record, whose attempt states an older build checks. A holder change
/// writes here alone: the step's status, attempt and act stay as they are. Read, judged and written in one hold of its own lock, the lease asked
/// inside it, each write naming the epoch it read (<see cref="HandOver"/>), so a write that lost a race to another is refused. A file that
/// does not read takes no write, and refuses what it cannot judge (<see cref="Unread"/>), said in the driver's log.
/// </summary>
public sealed class StepTenureFiles(string home, Func<bool>? holdsLease = null)
{
    /// <summary>Why a word is refused: its epoch has ended, the step held by another since.</summary>
    public const string EndedEpoch = "ended-epoch";

    /// <summary>Why a word or a hold act is refused: the unit's tenures do not read, so who holds the step cannot be judged.</summary>
    public const string Unread = "tenure-unread";

    /// <summary>The driver log's event for a tenures' file that does not read.</summary>
    public const string UnreadEvent = "tenures.unread";

    /// <summary>How many tenures a file keeps past each node's newest attempt's: an ended attempt's go oldest first, a standing one's never.</summary>
    public const int Kept = 200;

    private readonly Func<bool> _holdsLease = holdsLease ?? (() => HomeLease.HeldHere(home));

    public static string FolderOf(string home) => DaorisHome.Entry(home, HomeEntry.Workflows, "tenures");

    public static string PathOf(string home, string unit) => Path.Combine(FolderOf(home), NodeRecordFiles.KeyOf(unit) + ".json");

    /// <summary>An attempt's tenures, by epoch; none where it has none or its file does not read.</summary>
    public IReadOnlyList<StepTenure> Of(string unit, string node, int attempt) =>
        [.. (Load(unit) ?? []).Where(each => each.Node == node && each.Attempt == attempt).OrderBy(each => each.Epoch)];

    /// <summary>Who holds an attempt now: its newest epoch's tenure, or null where none is kept.</summary>
    public StepTenure? Current(string unit, string node, int attempt) => Of(unit, node, attempt).LastOrDefault();

    /// <summary>Whether the unit's tenures read: none kept reads as none.</summary>
    public bool Reads(string unit) => Load(unit) is not null;

    /// <summary>Who holds each node's attempt <paramref name="attempts"/> names now, by node, read once; none where none is kept or the file does not read.</summary>
    public IReadOnlyDictionary<string, StepTenure> Holding(string unit, IReadOnlyDictionary<string, int> attempts)
    {
        var kept = Load(unit) ?? [];
        return kept.Where(each => attempts.TryGetValue(each.Node, out var attempt) && attempt == each.Attempt)
            .GroupBy(each => each.Node, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.MaxBy(each => each.Epoch)!, StringComparer.Ordinal);
    }

    /// <summary>Who holds each node's attempt on <paramref name="run"/>'s record now, read once; none where the record does not read.</summary>
    public static IReadOnlyDictionary<string, StepTenure> HoldingOf(string home, string run, NodeRecord? record) => record is null
        ? new Dictionary<string, StepTenure>(StringComparer.Ordinal)
        : new StepTenureFiles(home).Holding(RunUnits.IdOf(run), record.Statuses.ToDictionary(pair => pair.Key, pair => pair.Value.Attempt, StringComparer.Ordinal));

    /// <summary>
    /// The first epoch of each attempt <paramref name="started"/> names that holds none yet, cast to its holder (<see cref="TenureWhy.Cast"/>, or
    /// <see cref="TenureWhy.Marked"/> where the owner's mark keeps it the owner role's), in one hold of the lock; read without it first, so a pass
    /// whose attempts all hold one writes and locks nothing. The tenures written.
    /// </summary>
    public IReadOnlyList<StepTenure> Begin(string unit, IReadOnlyList<(string Node, int Attempt, string Role, string Holder, DateTimeOffset Since, string Why)> started)
    {
        bool Unheld(IReadOnlyList<StepTenure> kept, (string Node, int Attempt, string Role, string Holder, DateTimeOffset Since, string Why) each) =>
            !kept.Any(tenure => tenure.Node == each.Node && tenure.Attempt == each.Attempt);
        if (started.Count == 0 || Load(unit) is not { } seen || !started.Any(each => Unheld(seen, each))) return [];
        var begun = new List<StepTenure>();
        Edit(unit, kept =>
        {
            begun.AddRange(started.Where(each => Unheld(kept, each)).Select(each => new StepTenure(each.Node, each.Attempt, each.Role, each.Holder, 1, each.Since, each.Why)));
            return begun.Count == 0 ? (null, null!) : ([.. kept, .. begun], begun[0]);
        });
        return begun;
    }

    /// <summary>
    /// The attempt's first epoch, held by <paramref name="holder"/> in <paramref name="role"/>, where none is kept; the tenure holding it now
    /// where one is, unchanged. Null where nothing is written (the lease not held here, or the file does not read).
    /// </summary>
    public StepTenure? Hold(string unit, string node, int attempt, string role, string holder, string why, DateTimeOffset now) =>
        Edit(unit, kept =>
        {
            if (NewestOf(kept, node, attempt) is { } current) return (null, current);
            var first = new StepTenure(node, attempt, role, holder, 1, now, why);
            return ([.. kept, first], first);
        });

    /// <summary>The attempt handed to <paramref name="holder"/> from the epoch <paramref name="expected"/> its caller read (<see cref="TenureHold.HandOver"/>).</summary>
    public TenureHanded HandOver(string unit, string node, int attempt, int expected, string holder, string why, DateTimeOffset now) =>
        Settling(unit, hold => hold.HandOver(node, attempt, expected, holder, why, now));

    /// <summary>
    /// <paramref name="settle"/> judged on the unit's tenures and acting on them in one hold of their lock, a settling word's record write
    /// among its acts (the record's lock taken inside this one, never around it): no other tenure write lands between its read and its write.
    /// </summary>
    public T Settling<T>(string unit, Func<TenureHold, T> settle)
    {
        using (RecordLock.Take(LockOf(unit)))
        {
            return settle(new TenureHold(this, unit, Load(unit)));
        }
    }

    /// <summary>A refusal on a unit whose tenures do not read, said in the driver's log with what it refused.</summary>
    public void SayUnread(string unit, string refused)
    {
        using var log = new MachineLog(home, "driver");
        log.Warn(UnreadEvent, ("unit", unit), ("path", PathOf(home, unit)), ("refused", refused));
    }

    /// <summary>A unit's tenures read under their lock (<see cref="Settling{T}"/>): who holds an attempt now, and a hand-over from the epoch read.</summary>
    public sealed class TenureHold
    {
        private readonly StepTenureFiles _files;
        private readonly string _unit;
        private List<StepTenure>? _kept;

        internal TenureHold(StepTenureFiles files, string unit, List<StepTenure>? kept) => (_files, _unit, _kept) = (files, unit, kept);

        /// <summary>Whether the unit's tenures do not read: who holds a step cannot be judged, and nothing is written.</summary>
        public bool Unread => _kept is null;

        /// <summary>Who holds the attempt now: its newest epoch's tenure, or null where none is kept or the file does not read.</summary>
        public StepTenure? Current(string node, int attempt) => _kept is null ? null : NewestOf(_kept, node, attempt);

        /// <summary>
        /// A new epoch held by <paramref name="holder"/> where <paramref name="expected"/> still holds the attempt, else refused: <see cref="EndedEpoch"/>
        /// naming the tenure now, <see cref="StepTenureFiles.Unread"/>, or <see cref="PlaceCastFiles.Unwritable"/> (no lease here, or no tenure).
        /// </summary>
        public TenureHanded HandOver(string node, int attempt, int expected, string holder, string why, DateTimeOffset now)
        {
            if (_kept is null) return new(null, StepTenureFiles.Unread);
            if (!_files._holdsLease() || NewestOf(_kept, node, attempt) is not { } current) return new(null, PlaceCastFiles.Unwritable);
            if (current.Epoch != expected) return new(null, EndedEpoch, current);
            if (current.Holder == holder) return new(current, Now: current);
            var next = current with { Holder = holder, Epoch = current.Epoch + 1, Since = now, Why = why };
            _kept = [.. _kept, next];
            _files.Write(_unit, _kept);
            return new(next, Now: next);
        }
    }

    private static StepTenure? NewestOf(IEnumerable<StepTenure> kept, string node, int attempt) =>
        kept.Where(each => each.Node == node && each.Attempt == attempt).MaxBy(each => each.Epoch);

    private string LockOf(string unit) => RecordLock.PathOf(home, "tenures-" + NodeRecordFiles.KeyOf(unit));

    /// <summary>
    /// Whether a word naming <paramref name="epoch"/> is refused: the tenure that holds the attempt now where the epoch named has ended, else
    /// null (the epoch current, or no tenure kept, which refuses nothing).
    /// </summary>
    public StepTenure? Ended(string unit, string node, int attempt, int epoch) =>
        Current(unit, node, attempt) is { } current && current.Epoch != epoch ? current : null;

    /// <summary>The words a refusal says: who holds the step now, and since when.</summary>
    public static string Said(StepTenure current) => $"held now by {current.Holder} since {Moments.Write(current.Since)}";

    private List<StepTenure>? Load(string unit)
    {
        var path = PathOf(home, unit);
        if (!File.Exists(path)) return [];
        try
        {
            using var document = JsonDocument.Parse(AtomicFile.ReadText(path));
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || Text(root, "unit") != unit || !root.TryGetProperty("tenures", out var all) || all.ValueKind != JsonValueKind.Array) return null;
            var read = new List<StepTenure>();
            foreach (var each in all.EnumerateArray())
            {
                if (each.ValueKind != JsonValueKind.Object || Text(each, "node") is not { } node || Whole(each, "attempt") is not { } attempt
                    || Text(each, "role") is not { } role || Text(each, "holder") is not { } holder || Whole(each, "epoch") is not { } epoch
                    || Moments.Read(each, "since") is not { } since || Text(each, "why") is not { } why)
                {
                    return null;
                }

                read.Add(new StepTenure(node, attempt, role, holder, epoch, since, why));
            }

            return read;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    private static int? Whole(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number) ? number : null;

    private StepTenure? Edit(string unit, Func<List<StepTenure>, (List<StepTenure>? Next, StepTenure Result)> change)
    {
        using (RecordLock.Take(LockOf(unit)))
        {
            if (!_holdsLease() || Load(unit) is not { } kept) return null;
            var (next, result) = change(kept);
            if (next is not null) Write(unit, next);
            return result;
        }
    }

    private void Write(string unit, List<StepTenure> next)
    {
        var written = new JsonObject
        {
            ["unit"] = unit,
            ["tenures"] = new JsonArray([.. Trimmed(next).Select(each => (JsonNode)new JsonObject
            {
                ["node"] = each.Node, ["attempt"] = each.Attempt, ["role"] = each.Role, ["holder"] = each.Holder, ["epoch"] = each.Epoch,
                ["since"] = Moments.Write(each.Since), ["why"] = each.Why,
            })]),
        };
        Directory.CreateDirectory(FolderOf(home));
        var path = PathOf(home, unit);
        AtomicFile.WriteText(path, written.ToJsonString() + "\n");
        HomeWrites.Mine(path);
    }

    /// <summary>Each node's newest attempt, the one that may stand, keeps every epoch however long it waits.</summary>
    private static IEnumerable<StepTenure> Trimmed(List<StepTenure> all)
    {
        var newest = all.GroupBy(each => each.Node, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.Max(each => each.Attempt), StringComparer.Ordinal);
        var spare = all.Count - Kept;
        foreach (var each in all)
        {
            if (spare > 0 && each.Attempt < newest[each.Node])
            {
                spare--;
                continue;
            }

            yield return each;
        }
    }
}
