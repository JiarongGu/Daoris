using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using static Daoris.Driver.Json;

namespace Daoris.Driver;

/// <summary>
/// The places' cast and its journal under a home: one file, <c>&lt;home&gt;/workflows/casts.json</c>, which no older build reads, holding each
/// place's cast (<see cref="PlaceCastBook"/>) and the newest <see cref="Kept"/> acts of each kind with their reasons (<see cref="CastEntry"/>).
/// Read, judged and written in one hold of its own lock, the lease asked inside it. A journal entry that does not read is skipped and counted,
/// never the casts dropped for it; casts that do not read keep the last good read and are never written over; a rewrite keeps every holder
/// word and field it did not read, as written.
/// </summary>
public sealed class PlaceCastFiles(string home, Func<bool>? holdsLease = null)
{
    /// <summary>How many acts of each kind the journal keeps, the newest: the owner's casts and the presses on steps are trimmed apart.</summary>
    public const int Kept = 500;

    /// <summary>A role that is no role's id, refused.</summary>
    public const string NotRole = "not-role";

    /// <summary>A holder that is no holder's word, refused.</summary>
    public const string NotHolder = "not-holder";

    /// <summary>The owner role, which nothing casts.</summary>
    public const string OwnerRole = "owner-role";

    /// <summary>The lease is not held here: nothing is written.</summary>
    public const string Unwritable = "unwritable";

    /// <summary>The file's casts do not read, so nothing is written over them.</summary>
    public const string Unreadable = "unreadable-casts";

    private static readonly ConcurrentDictionary<string, PlaceCastBook> LastGood = new(StringComparer.OrdinalIgnoreCase);

    private static readonly ConcurrentDictionary<string, string> Noted = new(StringComparer.OrdinalIgnoreCase);

    private readonly Func<bool> _holdsLease = holdsLease ?? (() => HomeLease.HeldHere(home));

    public string Home => home;

    public static string PathOf(string home) => DaorisHome.Entry(home, HomeEntry.Workflows, "casts.json");

    /// <summary>
    /// The places' cast: the empty book where none is written; where the file's casts do not read, the last good read of this process
    /// (else the empty book), with <see cref="Problem"/> saying so.
    /// </summary>
    public PlaceCastBook Book() => Read().Book;

    /// <summary>The journal's acts, oldest first, each that reads; none where the file does not read.</summary>
    public IReadOnlyList<CastEntry> Journal() => Read().Journal;

    /// <summary>Why the file's casts cannot be read or written over; null where they read.</summary>
    public string? Problem() => Read().Problem;

    /// <summary>The file as read: its casts, its readable acts, how many entries were skipped and what keeps its casts unread.</summary>
    public PlaceCastReading Read()
    {
        var path = PathOf(home);
        var loaded = Load(path);
        if (loaded.Problem is null) LastGood[path] = loaded.Book;
        else if (loaded.Book == PlaceCastBook.Empty) loaded = loaded with { Book = LastGood.GetValueOrDefault(path) ?? PlaceCastBook.Empty };
        Note(path, loaded);
        return new(loaded.Book, loaded.Journal, loaded.Skipped, loaded.Problem);
    }

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
        return Edit(root =>
        {
            var places = ObjectAt(root, "places");
            var spelled = places.Select(each => each.Key).FirstOrDefault(each => string.Equals(each, place, StringComparison.OrdinalIgnoreCase)) ?? place;
            var roles = places[spelled] as JsonObject ?? [];
            if (holder is null)
            {
                roles.Remove(role);
            }
            else
            {
                // An existing cast is edited where it stands, so a field this build does not know stays.
                var written = roles[role] as JsonObject ?? [];
                written["holder"] = holder;
                written["why"] = why;
                written["since"] = Moments.Write(now);
                roles[role] = written;
            }

            if (roles.Count > 0) places[spelled] = roles;
            else places.Remove(spelled);
            Append(root, new CastEntry(now, CastActs.Cast, role, holder, why) { Place = place });
        });
    }

    /// <summary>
    /// One act on a run's step kept in the journal with its reason. Null where it is kept, else the refusal's code (<see cref="Unwritable"/>,
    /// <see cref="Unreadable"/>), nothing written: a hold's caller handles it, never drops it.
    /// </summary>
    [MustUseResult]
    public string? Keep(CastEntry entry) => Edit(root => Append(root, entry));

    private static void Note(string path, Loaded loaded)
    {
        var said = loaded.Problem is null && loaded.Skipped == 0 ? "" : $"{loaded.Problem}|{loaded.Skipped}";
        if (Noted.TryGetValue(path, out var before) ? before == said : said.Length == 0) return;
        Noted[path] = said;
        if (said.Length == 0) return;
        try
        {
            using var log = new MachineLog(Path.GetDirectoryName(Path.GetDirectoryName(path)), "driver");
            log.Warn("cast.file", ("path", path), ("problem", loaded.Problem), ("skipped", loaded.Skipped));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // The log is optional.
        }
    }

    private sealed record Loaded(JsonObject Root, PlaceCastBook Book, List<CastEntry> Journal, int Skipped, string? Problem);

    private static Loaded Load(string path)
    {
        var none = new Loaded([], PlaceCastBook.Empty, [], 0, null);
        if (!File.Exists(path)) return none;
        JsonNode? parsed;
        try
        {
            parsed = JsonNode.Parse(AtomicFile.ReadText(path));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            return none with { Problem = $"`{path}` cannot be read: {error.Message}" };
        }

        if (parsed is not JsonObject root) return none with { Problem = $"`{path}` is not a JSON object" };
        var places = new Dictionary<string, IReadOnlyDictionary<string, RoleCast>>(StringComparer.OrdinalIgnoreCase);
        if (root["places"] is { } all)
        {
            if (all is not JsonObject given) return none with { Root = root, Problem = $"`places` in `{path}` is not an object" };
            foreach (var (name, value) in given)
            {
                if (value is not JsonObject cast) return none with { Root = root, Problem = $"the cast of `{name}` in `{path}` is not an object" };
                var roles = new Dictionary<string, RoleCast>(StringComparer.Ordinal);
                foreach (var (role, entry) in cast)
                {
                    if (entry is not JsonObject each || Text(each, "holder") is not { } holder || Moments.Read(each["since"]) is not { } since)
                    {
                        return none with { Root = root, Problem = $"the cast of `{role}` at `{name}` in `{path}` does not read" };
                    }

                    // A holder this build does not know is read as the person's, the safe side: nothing an agent would hold is cast to one.
                    // A rewrite keeps the word as written.
                    roles[role] = new RoleCast(CastHolders.Is(holder) ? holder : CastHolders.Person, Text(each, "why"), since);
                }

                places[name] = roles;
            }
        }

        var journal = new List<CastEntry>();
        var skipped = 0;
        string? problem = null;
        if (root["journal"] is { } acts)
        {
            if (acts is not JsonArray array)
            {
                problem = $"`journal` in `{path}` is not an array";
            }
            else
            {
                foreach (var each in array)
                {
                    if (ReadEntry(each) is { } entry) journal.Add(entry);
                    else skipped++;
                }
            }
        }

        return new(root, new PlaceCastBook(places), journal, skipped, problem);
    }

    private static CastEntry? ReadEntry(JsonNode? node)
    {
        if (node is not JsonObject each || Moments.Read(each["at"]) is not { } at || Text(each, "act") is not { } act || Text(each, "role") is not { } role) return null;
        return new CastEntry(at, act, role, Text(each, "holder"), Text(each, "why"))
        {
            Place = Text(each, "place"), Run = Text(each, "run"), Node = Text(each, "node"), Attempt = Whole(each, "attempt"), Epoch = Whole(each, "epoch"),
        };
    }

    private static int? Whole(JsonObject element, string name) =>
        element[name] is JsonValue value && value.TryGetValue<int>(out var number) ? number : null;

    private static JsonObject ObjectAt(JsonObject root, string name) => root[name] as JsonObject ?? (JsonObject)(root[name] = new JsonObject());

    /// <summary>The act appended to the journal, each kind (the owner's casts, the presses on steps) trimmed to its newest <see cref="Kept"/>.</summary>
    private static void Append(JsonObject root, CastEntry entry)
    {
        var journal = root["journal"] as JsonArray ?? (JsonArray)(root["journal"] = new JsonArray());
        journal.Add(Entry(entry));
        foreach (var casts in new[] { true, false })
        {
            var of = journal.Select((node, index) => (node, index)).Where(pair => ReadEntry(pair.node) is { } read && (read.Act == CastActs.Cast) == casts)
                .Select(pair => pair.index).ToList();
            foreach (var index in of.Take(Math.Max(0, of.Count - Kept)).OrderDescending()) journal.RemoveAt(index);
        }
    }

    private string? Edit(Action<JsonObject> change)
    {
        var path = PathOf(home);
        using (RecordLock.Take(RecordLock.PathOf(home, "casts")))
        {
            if (!_holdsLease()) return Unwritable;
            var loaded = Load(path);
            if (loaded.Problem is not null) return Unreadable;
            change(loaded.Root);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            AtomicFile.WriteText(path, loaded.Root.ToJsonString() + "\n");
            HomeWrites.Mine(path);
            LastGood[path] = Load(path).Book;
            return null;
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

/// <summary>The casts file as read (<see cref="PlaceCastFiles.Read"/>): <paramref name="Problem"/> says why its casts are the last good read and writes are refused.</summary>
public sealed record PlaceCastReading(PlaceCastBook Book, IReadOnlyList<CastEntry> Journal, int Skipped, string? Problem);

/// <summary>A result the caller must handle: a refusal dropped is an act lost. Marks a method for the reviewer; no compiler check reads it.</summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class MustUseResultAttribute : Attribute;
