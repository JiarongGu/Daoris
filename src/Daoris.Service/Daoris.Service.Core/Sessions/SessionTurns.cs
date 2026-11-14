using System.Text.Json;
using System.Text.Json.Serialization;

namespace Daoris.Knowledge;

/// <summary>
/// A session's turns (D293 point 5): each record keeps the turns it ran (the first at its open, one more at each wake), and the session
/// reads them across its line, numbered from one. A limit or a curve move ends a turn, never the session: its turn is the line's next.
/// </summary>
public static class SessionTurns
{
    /// <summary>Daoris took the work: the record opened on no other.</summary>
    public const string Start = "start";

    /// <summary>A workflow step woke it with its word: a check's red, an opinion's findings, a back wire.</summary>
    public const string Step = "step";

    /// <summary>The person woke or moved it: their words, their press.</summary>
    public const string Person = "person";

    /// <summary>Its account's limit moved it to another account or agent.</summary>
    public const string Limit = "limit";

    /// <summary>Its account went over the person's switching curve, and it moved to another account of its agent.</summary>
    public const string Curve = "curve";

    /// <summary>The agent asked to be handed over.</summary>
    public const string Agent = "agent";

    /// <summary>Daoris carried it on by itself: a conversation lost, a retry, a part no account was offered for.</summary>
    public const string Daoris = "daoris";

    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>What a record continuing another was woken by, from the driver's reason and asker; a record continuing none was a start.</summary>
    public static string CauseOf(string? fromWhy, string? fromBy) => fromWhy switch
    {
        null or "" => Start,
        "limit" or "near" => Limit,
        "curve" => Curve,
        "agent" => Agent,
        "press" => Person,
        _ => fromBy == "person" ? Person : Daoris,
    };

    /// <summary>A record's first turn as its open writes it.</summary>
    public static SessionTurn First(string? fromWhy, string? fromBy, DateTimeOffset at) => new(1, CauseOf(fromWhy, fromBy), at);

    /// <summary>
    /// The turn a wake adds to a record: the person's when any word waiting is theirs or their supervisor's, else the step's whose word
    /// woke it, with the node and attempt its key names.
    /// </summary>
    public static SessionTurn Woken(Session record, DateTimeOffset at)
    {
        var number = Own(record).Count + 1;
        if (record.Said.Count == 0 || record.Said.Any(word => word.Persons || word.Supervisors)) return new(number, Person, at);
        var (node, attempt) = StepOf(record.Said.Select(word => word.Key).LastOrDefault(key => key is { Length: > 0 }));
        return new SessionTurn(number, Step, at) { Node = node, Attempt = attempt };
    }

    /// <summary>
    /// The node and attempt a step's word key names: a broker check's (<c>check/&lt;ask&gt;</c>) is the <c>check_run</c> step, a run's
    /// (<c>&lt;unit&gt;/&lt;run&gt;/&lt;node&gt;/&lt;attempt&gt;</c>) its last two segments.
    /// </summary>
    internal static (string? Node, int? Attempt) StepOf(string? key)
    {
        if (key is null) return (null, null);
        if (key.StartsWith("check/", StringComparison.Ordinal)) return ("check_run", null);
        var parts = key.Split('/');
        return parts.Length >= 3 && int.TryParse(parts[^1], out var attempt) ? (parts[^2], attempt) : (null, null);
    }

    /// <summary>
    /// A record's own turns: those it kept, or for a record opened before records kept turns, one turn read from its <c>from</c> link.
    /// That read stands while such a record is kept, and goes when none is.
    /// </summary>
    public static IReadOnlyList<SessionTurn> Own(Session record) =>
        record.Turns ?? [First(record.FromWhy, record.FromBy, record.Created)];

    /// <summary>
    /// The session's turns across the records of its line, oldest first and numbered from one, each named with the record it ran in, its
    /// agent, account, adapter and conversation; a turn ends where the next begins, or where its record ended.
    /// </summary>
    public static IReadOnlyList<SessionTurn> OfLine(IEnumerable<Session> members)
    {
        var ordered = members.OrderBy(record => record.Created).ThenBy(record => record.Id, StringComparer.Ordinal).ToList();
        var turns = new List<SessionTurn>();
        foreach (var record in ordered)
        {
            var conversation = record.Transcript is { Length: > 0 } transcript ? Path.GetFileNameWithoutExtension(transcript) : null;
            foreach (var turn in Own(record).OrderBy(turn => turn.Number))
            {
                turns.Add(turn with
                {
                    Number = turns.Count + 1, Part = record.Id, Agent = record.Door, Account = record.Profile, Adapter = record.DoorVersion,
                    Conversation = conversation, Ended = record.Active ? null : record.Updated,
                });
            }
        }

        for (var at = 0; at + 1 < turns.Count; at++) turns[at] = turns[at] with { Ended = turns[at + 1].At };
        return turns;
    }

    /// <summary>The turns as the record's column keeps them.</summary>
    public static string Write(IReadOnlyList<SessionTurn> turns) =>
        JsonSerializer.Serialize(turns.Select(turn => new Kept(turn.Number, turn.Cause, turn.At, turn.Node, turn.Attempt)), Options);

    /// <summary>The turns a record's column keeps; null for a record from before the column, or one that does not read.</summary>
    public static IReadOnlyList<SessionTurn>? Read(string? kept)
    {
        if (string.IsNullOrWhiteSpace(kept)) return null;
        try
        {
            return JsonSerializer.Deserialize<List<Kept>>(kept, Options)?
                .Select(turn => new SessionTurn(turn.N, turn.Cause, turn.At) { Node = turn.Node, Attempt = turn.Attempt })
                .ToList();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private sealed record Kept(int N, string Cause, DateTimeOffset At, string? Node, int? Attempt);
}
