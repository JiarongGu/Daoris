using System.Text.Json.Serialization;

namespace Daoris.Driver;

/// <summary>One part's lineage as a read found it: no file, read, or unreadable with its kind.</summary>
public sealed record LineageReading(string Outcome, ChildBindings Bindings)
{
    public const string Found = "read";
    public const string Absent = "none";
    public const string Unreadable = "unreadable";

    public static LineageReading None { get; } = new(Absent, ChildBindings.Fold([]));

    /// <summary>The error's kind where it could not be read.</summary>
    public string? Error { get; init; }

    /// <summary>The bytes read up to the last full line: where a judge reads on from.</summary>
    public long Length { get; init; }

    public static LineageReading Of(ChildBindings bindings, long length = 0) => new(Found, bindings) { Length = length };

    public static LineageReading Failed(string error) => new(Unreadable, ChildBindings.Fold([])) { Error = error };
}

/// <summary>A record's fields the read takes from the sessions slice: never its note, ran or updated.</summary>
public sealed record AtWorkRecord(string Id, string? State)
{
    public string? Adapter { get; init; }

    /// <summary>The record's kind, <see cref="ChildBindings.Chat"/> or driven, which the fold reads its history by.</summary>
    public string? Kind { get; init; }

    /// <summary>The quest a chat took through its own connector, its <c>quest</c> staying null.</summary>
    public string? TookQuest { get; init; }

    public string? Repository { get; init; }

    /// <summary>The tree it works in: only whether another part works in it is answered, never the path.</summary>
    public string? Tree { get; init; }
}

/// <summary>An input slice's health, taken with its snapshot: whether it has judged, and its failure's words while it fails.</summary>
public readonly record struct SliceHealth(bool Judged, string? Failing = null);

public sealed record AtWorkWhy(string Why);

/// <summary>
/// What the composition answers (<c>AtWorkHeld</c> less its number): pending with what it waits on, failed with an
/// input slice's words, or the answer, stale where a slice judged once is failing now.
/// </summary>
public sealed record AtWorkHeld
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Pending { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Waits { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public AtWorkWhy? Failed { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public AtWorkAnswer? Answer { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public AtWorkWhy? Stale { get; init; }

    /// <summary>An ask's answer: a branch of its own, with no attended part, beside the session's <see cref="Answer"/>.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public AtWorkAskAnswer? AskAnswer { get; init; }

    /// <summary>What the composition read, each input with the version it read: kept beside it, never sent.</summary>
    [JsonIgnore]
    public object? Dependencies { get; init; }
}

public sealed record AtWorkOf(string Session);

public sealed record AtWorkAnswer(AtWorkOf Of, string Attended, IReadOnlyList<AtWorkPart> Parts)
{
    /// <summary>The parts past the bound, counted by their record's state.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, int>? MoreParts { get; init; }

    /// <summary>Ended parts that wait on nothing, past the newest three, which the list leaves out.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Earlier { get; init; }

    /// <summary>How many quests the entry's parts published; null where none, and where the routing half is not read.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Published { get; init; }

    /// <summary>The quests the entry's parts asked that opened no part of it, a chain's steps after each; null where none.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<AtWorkAskedQuest>? Quests { get; init; }

    /// <summary>How many of those the bound left out.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? MoreQuests { get; init; }

    /// <summary>What asked the attended part's quest.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public AtWorkAsker? AskedBy { get; init; }

    /// <summary>
    /// The attended line's requests: those that hold are its <i>Waiting on</i>, the head's and this view's one line; its helpers,
    /// partners and successors are its co-workers. Null where it asked none, and where the exchanges half is not read.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<AtWorkExchange>? Exchanges { get; init; }

    /// <summary>The exchanges' sources that could not be read, by name; null where every one read.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? Unread { get; init; }

    /// <summary>
    /// Where the attended part's newest generation started at least two subagents and the repository's routing standing is not open:
    /// <c>not-driven</c>, <c>no-lanes</c> or <c>one-at-a-time</c>.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Hint { get; init; }

    /// <summary>When this process began: the window's memory of what it heard starts here (rule 18).</summary>
    public DateTimeOffset WindowOpened { get; init; } = Opened;

    private static readonly DateTimeOffset Opened = System.Diagnostics.Process.GetCurrentProcess().StartTime.ToUniversalTime();
}

/// <summary>One of the strip's parts: its record's own state, never the strip's shown word, and its subagents' count.</summary>
public sealed record AtWorkPart(string Key, AtWorkCounts Children)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Agent { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? State { get; init; }

    /// <summary>Its place in the strip, from one; the number the words <i>part 2</i> and <i>retried after part 1</i> say.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Number { get; init; }

    /// <summary>What it waits on, as the strip places it; null where it waits on nothing.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public AtWorkWait? Waits { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public AtWorkJoined? Joined { get; init; }

    /// <summary>It runs in a tree no earlier part of the entry worked in.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Alongside { get; init; }
}

/// <summary>A part's subagents counted, or for a teammate's part <see cref="Elsewhere"/> and no count.</summary>
public sealed record AtWorkCounts
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Count { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Working { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Observed { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Unplaced { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Unreadable { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Elsewhere { get; init; }
}

/// <summary>
/// Who is at work on a session: the one function the window's read and the terminal's verb
/// share. Its inputs are plain values (the entry's parts, each record's read fields, each part's lineage reading with this window's
/// token readings already in it, and the two input slices' health), so it reads nothing itself and asks no service.
/// </summary>
public static class AtWork
{
    /// <summary>At most this many rows a list.</summary>
    public const int Bound = 40;

    /// <summary>Pending: the groups or sessions slice has not judged.</summary>
    public const string WaitsSlices = "slices";

    /// <summary>Pending: the sessions slice has judged and holds no record of the attended session yet.</summary>
    public const string WaitsRecord = "record";

    /// <summary>How many of the rest, ended by a word, are drawn: the newest.</summary>
    private const int NewestEnded = 3;

    /// <param name="session">The attended session.</param>
    /// <param name="entry">Its entry's parts in the strip's order; null where it is in no entry, an ended intake's among them.</param>
    /// <param name="record">A record's read fields as the sessions slice holds them; null where it holds none.</param>
    /// <param name="lineage">A part's lineage reading; never asked for a teammate's part.</param>
    /// <param name="routing">The routing half's inputs; null reads none of it, and no part is folded.</param>
    public static AtWorkHeld Compose(
        string session, IReadOnlyList<string>? entry, Func<string, AtWorkRecord?> record, Func<AtWorkRecord, LineageReading> lineage,
        SliceHealth groups, SliceHealth sessions, AtWorkRouting? routing = null)
    {
        SliceHealth[] inputs = routing is null ? [groups, sessions] : [groups, sessions, routing.Quests];
        foreach (var health in inputs)
        {
            if (health.Judged) continue;
            return health.Failing is { } why ? new AtWorkHeld { Failed = new AtWorkWhy(why) } : new AtWorkHeld { Pending = true, Waits = WaitsSlices };
        }

        if (record(session) is not { } attended) return new AtWorkHeld { Pending = true, Waits = WaitsRecord };

        List<string> order = [.. (entry ?? []).Distinct(StringComparer.Ordinal)];
        List<string> ids = [session, .. order.Where(id => id != session)];
        AtWorkRecord? Held(string id) => id == session ? attended : record(id);
        var routed = routing is null ? null : new Routed(routing, order, ids, Held, session);

        var parts = new List<AtWorkPart>();
        var moreParts = new Dictionary<string, int>(StringComparer.Ordinal);
        LineageReading? attendedReading = null;
        // Inside the limit the attended part and the waiting parts come first, the rest in the strip's order; the list keeps the strip's order.
        var drawn = routed is null ? null : ids.Where(id => !routed.Folded.Contains(id))
            .OrderBy(id => id == session ? 0 : routed.Waits(id) ? 1 : 2).Take(Bound).ToHashSet(StringComparer.Ordinal);
        foreach (var id in ids)
        {
            if (routed?.Folded.Contains(id) == true) continue;
            var held = Held(id);
            if (drawn is null ? parts.Count >= Bound : !drawn.Contains(id))
            {
                var state = held?.State ?? "unknown";
                moreParts[state] = moreParts.GetValueOrDefault(state) + 1;
                continue;
            }

            if (Teammate(id))
            {
                var away = new AtWorkPart(id, new AtWorkCounts { Elsewhere = true }) { Agent = held?.Adapter, State = held?.State };
                parts.Add(routed is null ? away : routed.Place(away, held));
                continue;
            }

            var reading = held is null ? LineageReading.None : lineage(held);
            if (id == session) attendedReading = reading;
            var here = new AtWorkPart(id, Counts(reading)) { Agent = held?.Adapter, State = held?.State };
            parts.Add(routed is null ? here : routed.Place(here, held));
        }

        var stale = groups.Failing ?? sessions.Failing ?? routing?.Quests.Failing;
        var asked = routing?.Asking?.Invoke(session, ids, routing.Parts, attended);
        var exchanges = routing?.Exchanging?.Invoke(session);
        return new AtWorkHeld
        {
            Answer = new AtWorkAnswer(new AtWorkOf(session), session, parts)
            {
                MoreParts = moreParts.Count > 0 ? moreParts : null,
                Earlier = routed?.Folded.Count is > 0 and var earlier ? earlier : null,
                Published = routed?.Published is > 0 and var published ? published : null,
                Quests = asked is { Quests.Count: > 0 } ? asked.Quests : null,
                MoreQuests = asked?.More is > 0 and var more ? more : null,
                AskedBy = asked?.By,
                Hint = Hint(routing, attended, Teammate(session) ? null : attendedReading),
                // A quest waited on is worded with its lanes, as the strip words it.
                Exchanges = exchanges is { Rows.Count: > 0 }
                    ? [.. exchanges.Rows.Select(each => each.Id.StartsWith("quest:", StringComparison.Ordinal)
                        ? each with { Lanes = routing!.Quest(each.Id["quest:".Length..])?.LaneList }
                        : each)]
                    : null,
                Unread = exchanges is { Unread.Count: > 0 } ? exchanges.Unread : null,
            },
            Stale = stale is null ? null : new AtWorkWhy(stale),
        };
    }

    /// <summary>
    /// The routing half over one composition: the waits the strip places, which ended parts fold, and what each part
    /// gains. The parts are numbered and judged in the strip's order, whichever the answer lists first.
    /// </summary>
    private sealed class Routed
    {
        private readonly AtWorkRouting _routing;
        private readonly Dictionary<string, int> _numbers;
        private readonly Dictionary<string, AtWorkEntryPart> _entry;
        private readonly Dictionary<string, AtWorkWait?> _waits = new(StringComparer.Ordinal);
        private readonly Dictionary<string, bool> _alongside = new(StringComparer.Ordinal);

        public Routed(AtWorkRouting routing, List<string> order, List<string> ids, Func<string, AtWorkRecord?> held, string session)
        {
            _routing = routing;
            _numbers = order.Select((id, index) => (id, number: index + 1)).ToDictionary(each => each.id, each => each.number, StringComparer.Ordinal);
            _entry = routing.Parts.GroupBy(part => part.Session, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
            foreach (var id in ids) _waits[id] = AtWorkParts.Wait(routing, id, _entry.GetValueOrDefault(id), held(id));
            for (var at = 0; at < order.Count; at++)
            {
                if (Teammate(order[at])) continue;
                var self = held(order[at]);
                _alongside[order[at]] = AtWorkParts.Alongside(self, _waits[order[at]] is not null, order.Take(at).Select(held));
            }

            // Ended parts that wait on nothing, oldest first, but the newest three; the attended part is the page's own and never folds.
            var ended = order.Where(id => id != session && AtWorkParts.Ended(held(id)) && _waits[id] is null).ToList();
            Folded = [.. ended.Take(Math.Max(0, ended.Count - NewestEnded))];
            Published = ids.Sum(routing.Published);
        }

        public HashSet<string> Folded { get; }

        public bool Waits(string id) => _waits.GetValueOrDefault(id) is not null;

        public int Published { get; }

        public AtWorkPart Place(AtWorkPart part, AtWorkRecord? held)
        {
            var entry = _entry.GetValueOrDefault(part.Key);
            return part with
            {
                Number = _numbers.TryGetValue(part.Key, out var number) ? number : null,
                Waits = _waits.GetValueOrDefault(part.Key),
                Joined = AtWorkParts.Joined(_routing, entry, held, id => id is not null && _numbers.TryGetValue(id, out var each) ? each : null),
                Alongside = _alongside.GetValueOrDefault(part.Key) ? true : null,
            };
        }
    }

    /// <summary>The hint: two subagents started by the attended part's newest generation, and a standing that is not open.</summary>
    private static string? Hint(AtWorkRouting? routing, AtWorkRecord attended, LineageReading? reading)
    {
        if (routing?.StandingOf is not { } standingOf || reading is not { Outcome: LineageReading.Found } || reading.Bindings.StartedNewest < 2) return null;
        if (attended.Repository is not { Length: > 0 } repository) return null;
        var standing = standingOf(repository);
        return standing is RoutingStanding.NotDriven or RoutingStanding.NoLanes or RoutingStanding.OneAtATime ? standing : null;
    }

    /// <summary>A teammate's record, named <c>origin/id</c>: its lineage is on another machine.</summary>
    internal static bool Teammate(string id) => id.Contains('/', StringComparison.Ordinal);

    private static AtWorkCounts Counts(LineageReading reading) => new()
    {
        Count = reading.Bindings.Children.Count,
        Working = Working(reading.Bindings),
        Observed = Positive(reading.Bindings.Unmatched),
        Unplaced = Positive(reading.Bindings.Unplaced),
        Unreadable = reading.Outcome == LineageReading.Unreadable ? true : null,
    };

    internal static int Working(ChildBindings bindings) => bindings.Children.Count(child => child.State == ChildBindings.Working);

    private static int? Positive(int count) => count > 0 ? count : null;
}
