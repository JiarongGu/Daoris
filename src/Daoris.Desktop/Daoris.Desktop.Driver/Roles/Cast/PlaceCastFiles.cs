using System.Text.Json;
using System.Text.Json.Nodes;
using static Daoris.Driver.Json;

namespace Daoris.Driver;

/// <summary>
/// The places' cast and its journal under a home: one file, <c>&lt;home&gt;/workflows/casts.json</c>, which no older build reads, holding each
/// place's cast (<see cref="PlaceCastBook"/>) and the newest <see cref="Kept"/> acts with their reasons (<see cref="CastEntry"/>). Read, judged and
/// written in one hold of its own lock, the lease asked inside it; a file that does not read casts nothing and is never written over.
/// </summary>
public sealed class PlaceCastFiles(string home, Func<bool>? holdsLease = null)
{
    /// <summary>How many acts the journal keeps, the newest.</summary>
    public const int Kept = 500;

    /// <summary>A role that is no role's id, refused.</summary>
    public const string NotRole = "not-role";

    /// <summary>A holder that is no holder's word, refused.</summary>
    public const string NotHolder = "not-holder";

    /// <summary>The owner role, which nothing casts.</summary>
    public const string OwnerRole = "owner-role";

    /// <summary>The file does not read, or the lease is not held here: nothing is written.</summary>
    public const string Unwritable = "unwritable";

    private readonly Func<bool> _holdsLease = holdsLease ?? (() => HomeLease.HeldHere(home));

    public string Home => home;

    public static string PathOf(string home) => DaorisHome.Entry(home, HomeEntry.Workflows, "casts.json");

    /// <summary>The places' cast; the empty book where none is written, or where the file does not read.</summary>
    public PlaceCastBook Book() => Load()?.Book ?? PlaceCastBook.Empty;

    /// <summary>The journal's acts, oldest first; none where the file does not read.</summary>
    public IReadOnlyList<CastEntry> Journal() => Load()?.Journal ?? [];

    /// <summary>
    /// The owner's cast of <paramref name="role"/> at <paramref name="place"/> (a <see cref="PlaceRiskBook.KeyOf"/> key) to
    /// <paramref name="holder"/>, or its cast taken away where null, journaled with <paramref name="why"/>. Null where it is made, else the
    /// refusal's code, nothing written.
    /// </summary>
    public string? Cast(string place, string role, string? holder, string? why, DateTimeOffset now)
    {
        if (!StepRoles.Shape.IsMatch(role)) return NotRole;
        if (role == StepRoles.Owner) return OwnerRole;
        if (holder is not null && !CastHolders.Is(holder)) return NotHolder;
        why = string.IsNullOrWhiteSpace(why) ? null : why.Trim();
        return Edit((book, journal) =>
        {
            var next = book.With(place, role, holder is null ? null : new RoleCast(holder, why, now));
            return (next, [.. journal, new CastEntry(now, CastActs.Cast, role, holder, why) { Place = place }]);
        }) ? null : Unwritable;
    }

    /// <summary>One act on a run's step kept in the journal with its reason; false where nothing is written.</summary>
    public bool Keep(CastEntry entry) => Edit((book, journal) => (book, [.. journal, entry]));

    private (PlaceCastBook Book, List<CastEntry> Journal)? Load()
    {
        var path = PathOf(home);
        if (!File.Exists(path)) return (PlaceCastBook.Empty, []);
        try
        {
            using var document = JsonDocument.Parse(AtomicFile.ReadText(path));
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return null;
            var places = new Dictionary<string, IReadOnlyDictionary<string, RoleCast>>(StringComparer.OrdinalIgnoreCase);
            if (root.TryGetProperty("places", out var all) && all.ValueKind == JsonValueKind.Object)
            {
                foreach (var place in all.EnumerateObject())
                {
                    if (place.Value.ValueKind != JsonValueKind.Object) return null;
                    var roles = new Dictionary<string, RoleCast>(StringComparer.Ordinal);
                    foreach (var role in place.Value.EnumerateObject())
                    {
                        if (role.Value.ValueKind != JsonValueKind.Object || Text(role.Value, "holder") is not { } holder || Moments.Read(role.Value, "since") is not { } since) return null;
                        // A holder this build does not know is read as the person's, the safe side: nothing an agent would hold is cast to one.
                        roles[role.Name] = new RoleCast(CastHolders.Is(holder) ? holder : CastHolders.Person, Text(role.Value, "why"), since);
                    }

                    places[place.Name] = roles;
                }
            }

            var journal = new List<CastEntry>();
            if (root.TryGetProperty("journal", out var acts) && acts.ValueKind == JsonValueKind.Array)
            {
                foreach (var each in acts.EnumerateArray())
                {
                    if (each.ValueKind != JsonValueKind.Object || Moments.Read(each, "at") is not { } at || Text(each, "act") is not { } act || Text(each, "role") is not { } role) return null;
                    journal.Add(new CastEntry(at, act, role, Text(each, "holder"), Text(each, "why"))
                    {
                        Place = Text(each, "place"), Run = Text(each, "run"), Node = Text(each, "node"), Attempt = Whole(each, "attempt"), Epoch = Whole(each, "epoch"),
                    });
                }
            }

            return (new PlaceCastBook(places), journal);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    private static int? Whole(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number) ? number : null;

    private bool Edit(Func<PlaceCastBook, List<CastEntry>, (PlaceCastBook Book, List<CastEntry> Journal)> change)
    {
        var path = PathOf(home);
        using (RecordLock.Take(RecordLock.PathOf(home, "casts")))
        {
            if (!_holdsLease() || Load() is not { } kept) return false;
            var (book, journal) = change(kept.Book, kept.Journal);
            var places = new JsonObject();
            foreach (var (place, roles) in book.Places.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                var cast = new JsonObject();
                foreach (var (role, each) in roles.OrderBy(pair => pair.Key, StringComparer.Ordinal))
                {
                    cast[role] = new JsonObject { ["holder"] = each.Holder, ["why"] = each.Why, ["since"] = Moments.Write(each.Since) };
                }

                places[place] = cast;
            }

            var written = new JsonObject
            {
                ["places"] = places,
                ["journal"] = new JsonArray([.. journal.Skip(Math.Max(0, journal.Count - Kept)).Select(each => (JsonNode)Entry(each))]),
            };
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            AtomicFile.WriteText(path, written.ToJsonString() + "\n");
            HomeWrites.Mine(path);
            return true;
        }
    }

    private static JsonObject Entry(CastEntry entry)
    {
        var written = new JsonObject { ["at"] = Moments.Write(entry.At), ["act"] = entry.Act, ["role"] = entry.Role };
        if (entry.Holder is { } holder) written["holder"] = holder;
        if (entry.Why is { } why) written["why"] = why;
        if (entry.Place is { } place) written["place"] = place;
        if (entry.Run is { } run) written["run"] = run;
        if (entry.Node is { } node) written["node"] = node;
        if (entry.Attempt is { } attempt) written["attempt"] = attempt;
        if (entry.Epoch is { } epoch) written["epoch"] = epoch;
        return written;
    }
}
