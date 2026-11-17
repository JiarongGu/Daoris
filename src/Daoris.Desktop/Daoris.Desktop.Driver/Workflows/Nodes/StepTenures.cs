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

/// <summary>
/// The steps' tenures under a home: one file per unit, <c>&lt;home&gt;/workflows/tenures/&lt;key&gt;.json</c>, its key the unit's record's,
/// a folder no older build reads, so nothing of a tenure reaches the node record, whose attempt states an older build checks. A holder change
/// writes here alone: the step's status, attempt and act stay as they are. Read, judged and written in one hold of its own lock, the lease asked
/// inside it; a file that does not read holds no tenure and takes none, so no word is refused or taken on a guess.
/// </summary>
public sealed class StepTenureFiles(string home, Func<bool>? holdsLease = null)
{
    /// <summary>Why a word is refused: its epoch has ended, the step held by another since.</summary>
    public const string EndedEpoch = "ended-epoch";

    /// <summary>How many tenures a unit's file keeps, the newest: an older attempt's are read by no word, which its attempt makes stale.</summary>
    public const int Kept = 200;

    private readonly Func<bool> _holdsLease = holdsLease ?? (() => HomeLease.HeldHere(home));

    public static string FolderOf(string home) => DaorisHome.Entry(home, HomeEntry.Workflows, "tenures");

    public static string PathOf(string home, string unit) => Path.Combine(FolderOf(home), NodeRecordFiles.KeyOf(unit) + ".json");

    /// <summary>An attempt's tenures, by epoch; none where it has none or its file does not read.</summary>
    public IReadOnlyList<StepTenure> Of(string unit, string node, int attempt) =>
        [.. (Load(unit) ?? []).Where(each => each.Node == node && each.Attempt == attempt).OrderBy(each => each.Epoch)];

    /// <summary>Who holds an attempt now: its newest epoch's tenure, or null where none is kept.</summary>
    public StepTenure? Current(string unit, string node, int attempt) => Of(unit, node, attempt).LastOrDefault();

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
    /// The attempt held by <paramref name="holder"/> in <paramref name="role"/>: its tenure where it holds it already, else a new epoch, the
    /// first where none is kept. Null where nothing is written (the lease not held here, or the file does not read).
    /// </summary>
    public StepTenure? Hold(string unit, string node, int attempt, string role, string holder, string why, DateTimeOffset now) =>
        Edit(unit, kept =>
        {
            var current = kept.Where(each => each.Node == node && each.Attempt == attempt).MaxBy(each => each.Epoch);
            if (current is not null && current.Holder == holder && current.Role == role) return (null, current);
            var next = new StepTenure(node, attempt, role, holder, (current?.Epoch ?? 0) + 1, now, why);
            return ([.. kept, next], next);
        });

    /// <summary>
    /// The attempt handed to <paramref name="holder"/> in the role it is held in: a new epoch, the step not restarted. Null where the attempt has
    /// no tenure to hand on, or nothing is written.
    /// </summary>
    public StepTenure? HandOver(string unit, string node, int attempt, string holder, string why, DateTimeOffset now) =>
        Current(unit, node, attempt) is { } current ? Hold(unit, node, attempt, current.Role, holder, why, now) : null;

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
        using (RecordLock.Take(RecordLock.PathOf(home, "tenures-" + NodeRecordFiles.KeyOf(unit))))
        {
            if (!_holdsLease() || Load(unit) is not { } kept) return null;
            var (next, result) = change(kept);
            if (next is null) return result;
            var written = new JsonObject
            {
                ["unit"] = unit,
                ["tenures"] = new JsonArray([.. next.Skip(Math.Max(0, next.Count - Kept)).Select(each => (JsonNode)new JsonObject
                {
                    ["node"] = each.Node, ["attempt"] = each.Attempt, ["role"] = each.Role, ["holder"] = each.Holder, ["epoch"] = each.Epoch,
                    ["since"] = Moments.Write(each.Since), ["why"] = each.Why,
                })]),
            };
            Directory.CreateDirectory(FolderOf(home));
            var path = PathOf(home, unit);
            AtomicFile.WriteText(path, written.ToJsonString() + "\n");
            HomeWrites.Mine(path);
            return result;
        }
    }
}
