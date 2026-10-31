using System.Text.Json.Serialization;

namespace Daoris.Driver;

/// <summary>One part's lineage as a read found it (COWORKVIEW1a, rule 9): no file, read, or unreadable with its kind.</summary>
public sealed record LineageReading(string Outcome, ChildBindings Bindings)
{
    public const string Found = "read";
    public const string Absent = "none";
    public const string Unreadable = "unreadable";

    /// <summary>A teammate's part, whose lineage is on another machine: never read here.</summary>
    public const string Elsewhere = "elsewhere";

    public static LineageReading None { get; } = new(Absent, ChildBindings.Fold([]));

    /// <summary>The error's kind where it could not be read.</summary>
    public string? Error { get; init; }

    /// <summary>The bytes read up to the last full line: where a judge reads on from.</summary>
    public long Length { get; init; }

    public static LineageReading Of(ChildBindings bindings, long length = 0) => new(Found, bindings) { Length = length };

    public static LineageReading Failed(string error) => new(Unreadable, ChildBindings.Fold([])) { Error = error };
}

/// <summary>A record's fields the read takes from the sessions slice (the design §4.1): never its note, ran or updated.</summary>
public sealed record AtWorkRecord(string Id, string? State)
{
    public string? Adapter { get; init; }

    /// <summary>The record's kind, <see cref="ChildBindings.Chat"/> or driven, which the fold reads its history by.</summary>
    public string? Kind { get; init; }
}

/// <summary>An input slice's health, taken with its snapshot: whether it has judged, and its failure's words while it fails.</summary>
public readonly record struct SliceHealth(bool Judged, string? Failing = null);

public sealed record AtWorkWhy(string Why);

/// <summary>
/// What the composition answers (the design §3.8, <c>AtWorkHeld</c> less its number): pending with what it waits on, failed with an
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

    /// <summary>What the composition read, each input with the version it read (the design §4.1): kept beside it, never sent.</summary>
    [JsonIgnore]
    public object? Dependencies { get; init; }
}

public sealed record AtWorkOf(string Session);

public sealed record AtWorkAnswer(AtWorkOf Of, string Attended, IReadOnlyList<AtWorkPart> Parts, AtWorkSubagents Subagents)
{
    /// <summary>The parts past the bound, counted by their record's state.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, int>? MoreParts { get; init; }

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

/// <summary>The attended part's subagents (the design §3.8, <c>AtWorkSubagents</c>).</summary>
public sealed record AtWorkSubagents(string Read, int Working, IReadOnlyList<AtWorkChild> Children)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Unreadable { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Harness { get; init; }

    /// <summary>A harness whose children appear only when they call Daoris (Codex), read from the record's adapter.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? SeenByCalls { get; init; }

    /// <summary><c>claude-code-acp</c> alone, a child working: its turn waits for them.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? TurnWaits { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Observed { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Unplaced { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Folded { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, int>? More { get; init; }
}

public sealed record AtWorkLabel(string Text)
{
    /// <summary>Always true: the harness's announcement is the agent's words.</summary>
    public bool Claim => true;
}

/// <summary>One child's row: its latest run's state and moments (the design §3.8, <c>AtWorkChild</c>).</summary>
public sealed record AtWorkChild(string Key, string State, bool StartSaid)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Type { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public AtWorkLabel? Label { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ParentKey { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Depth { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTimeOffset? Since { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTimeOffset? Ended { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTimeOffset? LastSeen { get; init; }
}

/// <summary>
/// Who is at work on a session (COWORKVIEW1a, the design §3.8 and §4.1): the one function the window's read and the terminal's verb
/// share. Its inputs are plain values (the entry's parts, each record's read fields, each part's lineage reading with this window's
/// token readings already in it, and the two input slices' health), so it reads nothing itself and asks no service.
/// </summary>
public static class AtWork
{
    /// <summary>At most this many rows a list (the design §4.1, <i>the bound</i>).</summary>
    public const int Bound = 40;

    /// <summary>Pending: the groups or sessions slice has not judged.</summary>
    public const string WaitsSlices = "slices";

    /// <summary>Pending: the sessions slice has judged and holds no record of the attended session yet.</summary>
    public const string WaitsRecord = "record";

    /// <summary>How many of the rest, ended by a word, are drawn: the newest.</summary>
    private const int NewestEnded = 3;

    private const string TurnHolder = "claude-code-acp";

    /// <param name="session">The attended session.</param>
    /// <param name="entry">Its entry's parts in the strip's order; null where it is in no entry, an ended intake's among them.</param>
    /// <param name="record">A record's read fields as the sessions slice holds them; null where it holds none.</param>
    /// <param name="lineage">A part's lineage reading; never asked for a teammate's part.</param>
    public static AtWorkHeld Compose(
        string session, IReadOnlyList<string>? entry, Func<string, AtWorkRecord?> record, Func<AtWorkRecord, LineageReading> lineage,
        SliceHealth groups, SliceHealth sessions)
    {
        foreach (var health in new[] { groups, sessions })
        {
            if (health.Judged) continue;
            return health.Failing is { } why ? new AtWorkHeld { Failed = new AtWorkWhy(why) } : new AtWorkHeld { Pending = true, Waits = WaitsSlices };
        }

        if (record(session) is not { } attended) return new AtWorkHeld { Pending = true, Waits = WaitsRecord };

        List<string> ids = [session, .. (entry ?? []).Where(id => id != session).Distinct(StringComparer.Ordinal)];
        var parts = new List<AtWorkPart>();
        var moreParts = new Dictionary<string, int>(StringComparer.Ordinal);
        LineageReading? attendedReading = null;
        foreach (var id in ids)
        {
            var held = id == session ? attended : record(id);
            if (parts.Count >= Bound)
            {
                var state = held?.State ?? "unknown";
                moreParts[state] = moreParts.GetValueOrDefault(state) + 1;
                continue;
            }

            if (Teammate(id))
            {
                parts.Add(new AtWorkPart(id, new AtWorkCounts { Elsewhere = true }) { Agent = held?.Adapter, State = held?.State });
                continue;
            }

            var reading = held is null ? LineageReading.None : lineage(held);
            if (id == session) attendedReading = reading;
            parts.Add(new AtWorkPart(id, Counts(reading)) { Agent = held?.Adapter, State = held?.State });
        }

        var stale = groups.Failing ?? sessions.Failing;
        return new AtWorkHeld
        {
            Answer = new AtWorkAnswer(new AtWorkOf(session), session, parts, Subagents(attended, Teammate(session) ? null : attendedReading))
            {
                MoreParts = moreParts.Count > 0 ? moreParts : null,
            },
            Stale = stale is null ? null : new AtWorkWhy(stale),
        };
    }

    /// <summary>A teammate's record, named <c>origin/id</c>: its lineage is on another machine.</summary>
    private static bool Teammate(string id) => id.Contains('/', StringComparison.Ordinal);

    private static AtWorkCounts Counts(LineageReading reading) => new()
    {
        Count = reading.Bindings.Children.Count,
        Working = Working(reading.Bindings),
        Observed = Positive(reading.Bindings.Unmatched),
        Unplaced = Positive(reading.Bindings.Unplaced),
        Unreadable = reading.Outcome == LineageReading.Unreadable ? true : null,
    };

    private static int Working(ChildBindings bindings) => bindings.Children.Count(child => child.State == ChildBindings.Working);

    private static int? Positive(int count) => count > 0 ? count : null;

    private static AtWorkSubagents Subagents(AtWorkRecord record, LineageReading? reading)
    {
        if (reading is null) return new AtWorkSubagents(LineageReading.Elsewhere, 0, []);

        var bindings = reading.Bindings;
        var working = Working(bindings);
        var (rows, folded, more) = Bounded(bindings.Children);
        var adapter = record.Adapter ?? "";
        return new AtWorkSubagents(reading.Outcome, working, rows)
        {
            Unreadable = reading.Error,
            Harness = bindings.Harness,
            SeenByCalls = adapter == "codex" || adapter.StartsWith("codex-", StringComparison.Ordinal) ? true : null,
            TurnWaits = adapter == TurnHolder && working > 0 ? true : null,
            Observed = Positive(bindings.Unmatched),
            Unplaced = Positive(bindings.Unplaced),
            Folded = folded > 0 ? folded : null,
            More = more.Count > 0 ? more : null,
        };
    }

    /// <summary>
    /// The rows within the bound (the design §4.1): the open runs, then the not known, then the lost and those whose agent ended, then the
    /// newest three of the rest, each only with its whole chain of ancestors among the children where that chain fits; the rest of the
    /// ended folded, and every candidate passed over for want of room counted by its state.
    /// </summary>
    private static (IReadOnlyList<AtWorkChild> Rows, int Folded, Dictionary<string, int> More) Bounded(IReadOnlyList<ChildBinding> children)
    {
        var byKey = new Dictionary<string, ChildBinding>(StringComparer.Ordinal);
        foreach (var child in children) byKey.TryAdd(child.Native, child);

        var open = Newest(children.Where(child => child.State == ChildBindings.Working), child => child.Since)
            .Concat(Newest(children.Where(child => child.State == ChildBindings.Unsaid), child => child.Since));
        var uncertain = Newest(children.Where(child => child.State == ChildBindings.Uncertain), Seen);
        var lost = Newest(children.Where(child => child.State is ChildBindings.Lost or ChildBindings.AgentEnded), Seen);
        var rest = Newest(children.Where(child => Tier(child.State) == 3), Seen).ToList();
        List<ChildBinding> candidates = [.. open, .. uncertain, .. lost, .. rest.Take(NewestEnded)];

        var drawn = new List<ChildBinding>();
        var taken = new HashSet<string>(StringComparer.Ordinal);
        foreach (var child in candidates)
        {
            if (taken.Contains(child.Native)) continue;
            var chain = Chain(child, byKey, taken);
            if (drawn.Count + chain.Count > Bound) continue;
            foreach (var each in chain)
            {
                taken.Add(each.Native);
                drawn.Add(each);
            }
        }

        // Counted once all are filled: a candidate passed over may yet be drawn as a later one's ancestor.
        var more = new Dictionary<string, int>(StringComparer.Ordinal);
        var passed = new HashSet<string>(StringComparer.Ordinal);
        foreach (var child in candidates.Where(child => !taken.Contains(child.Native) && passed.Add(child.Native)))
        {
            more[child.State] = more.GetValueOrDefault(child.State) + 1;
        }

        var folded = children.Count(child => !taken.Contains(child.Native) && !passed.Contains(child.Native));
        return (TreeOrder(drawn, taken), folded, more);
    }

    /// <summary>The child and its ancestors among the children not drawn yet, the eldest first; the walk stops at one it walked.</summary>
    private static List<ChildBinding> Chain(ChildBinding child, IReadOnlyDictionary<string, ChildBinding> byKey, HashSet<string> taken)
    {
        var chain = new List<ChildBinding>();
        var walked = new HashSet<string>(StringComparer.Ordinal);
        for (var at = child; at is not null && !taken.Contains(at.Native) && walked.Add(at.Native);)
        {
            chain.Add(at);
            at = at.Parent is { } parent ? byKey.GetValueOrDefault(parent) : null;
        }

        chain.Reverse();
        return chain;
    }

    /// <summary>The drawn rows as a tree: each under its parent, siblings in the order they were drawn; a cycle's members once each.</summary>
    private static List<AtWorkChild> TreeOrder(List<ChildBinding> drawn, HashSet<string> taken)
    {
        var below = drawn.Where(child => child.Parent is { } parent && taken.Contains(parent) && parent != child.Native)
            .GroupBy(child => child.Parent!, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.Ordinal);
        var rows = new List<AtWorkChild>();
        var placed = new HashSet<string>(StringComparer.Ordinal);

        void Place(ChildBinding child)
        {
            if (!placed.Add(child.Native)) return;
            rows.Add(Row(child));
            foreach (var each in below.GetValueOrDefault(child.Native) ?? []) Place(each);
        }

        foreach (var child in drawn.Where(child => child.Parent is not { } parent || !taken.Contains(parent))) Place(child);
        foreach (var child in drawn) Place(child);
        return rows;
    }

    private static AtWorkChild Row(ChildBinding child) => new(child.Native, child.State, child.StartSaid)
    {
        Type = child.Type,
        Label = child.Label is { } label ? new AtWorkLabel(label) : null,
        ParentKey = child.Parent,
        Depth = child.Depth,
        Since = child.Since,
        Ended = child.EndedAt,
        LastSeen = child.LastSeen,
    };

    /// <summary>The last moment a child was known at: its end, else its last line, else its start.</summary>
    private static DateTimeOffset? Seen(ChildBinding child) => child.EndedAt ?? child.LastSeen ?? child.Since;

    /// <summary>Newest first by <paramref name="moment"/>, a tie to the newest start, then to the id.</summary>
    private static IEnumerable<ChildBinding> Newest(IEnumerable<ChildBinding> children, Func<ChildBinding, DateTimeOffset?> moment) =>
        children.OrderByDescending(child => moment(child) ?? DateTimeOffset.MinValue)
            .ThenByDescending(child => child.Since ?? DateTimeOffset.MinValue)
            .ThenBy(child => child.Native, StringComparer.Ordinal);

    /// <summary>The bound's tier of a state: 1 open, 2 not known or lost, 3 ended by a word.</summary>
    private static int Tier(string state) => state switch
    {
        ChildBindings.Working or ChildBindings.Unsaid => 1,
        ChildBindings.Uncertain or ChildBindings.Lost or ChildBindings.AgentEnded => 2,
        _ => 3,
    };
}
