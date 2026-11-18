using System.Text.Json;
using System.Text.Json.Serialization;

namespace Daoris.Knowledge;

/// <summary>
/// A session's turns: each record keeps the turns it ran (the first at its open, one more at each wake), and the session
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

    /// <summary>Daoris carries it on by itself: a conversation lost, a retry, a part no account is offered for.</summary>
    public const string Daoris = "daoris";

    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>What wakes a record continuing another, from the driver's reason and asker; a record continuing none is a start.</summary>
    public static string CauseOf(string? fromWhy, string? fromBy) => fromWhy switch
    {
        null or "" => Start,
        "limit" or "near" => Limit,
        "curve" => Curve,
        "agent" => Agent,
        "press" => Person,
        _ => fromBy == "person" ? Person : Daoris,
    };

    /// <summary>
    /// A record's first turn as its open writes it: the agent, account and adapter it runs on, and the workflow step and attempt it
    /// performs where the graph names one.
    /// </summary>
    public static SessionTurn First(
        string? fromWhy, string? fromBy, DateTimeOffset at, string? agent, string? account, string? adapter, string? node = null, int? attempt = null) =>
        new(1, CauseOf(fromWhy, fromBy), at) { Agent = agent, Account = account, Adapter = adapter, Node = node, Attempt = attempt };

    /// <summary>
    /// The turn a wake adds to a record: the person's when any word waiting is theirs or their supervisor's, else the step's whose word
    /// woke it, with the node and attempt its key names, else Daoris's where its own words name no step (a restart, a limit's reset). It
    /// continues its record's conversation, so it keeps the agent, account, adapter and transcript of the turn before it.
    /// </summary>
    public static SessionTurn Woken(Session record, DateTimeOffset at)
    {
        var own = Own(record);
        var last = own.OrderBy(turn => turn.Number).LastOrDefault();
        var number = own.Count + 1;
        var kept = new SessionTurn(number, Person, at)
        {
            Agent = last?.Agent ?? record.Door, Account = last?.Account ?? record.Profile, Adapter = last?.Adapter ?? record.DoorVersion,
            Transcript = last?.Transcript ?? record.Transcript,
        };
        if (record.Said.Count == 0 || record.Said.Any(word => word.Persons || word.Supervisors)) return kept;
        var (node, attempt) = StepOf(record.Said.Select(word => word.Key).LastOrDefault(key => key is { Length: > 0 }));
        if (node is null && record.Said.All(word => word.From == SaidFrom.Daoris)) return kept with { Cause = Daoris };
        return kept with { Cause = Step, Node = node, Attempt = attempt };
    }

    /// <summary>An ended record's turns as it goes on: its last sealed with the end the go-on forgives on the record, then the wake's.</summary>
    public static IReadOnlyList<SessionTurn> WentOn(Session ended, DateTimeOffset at)
    {
        var own = Own(ended).OrderBy(turn => turn.Number).ToList();
        if (own.Count > 0) own[^1] = own[^1] with { End = SessionTurnEnd.Of(ended) };
        return [.. own, Woken(ended, at)];
    }

    /// <summary>The turns with the last one's transcript set, where a move names the captured run its turn wrote.</summary>
    public static IReadOnlyList<SessionTurn> Sealed(IReadOnlyList<SessionTurn> turns, string transcript) =>
        turns.Count == 0 ? turns : [.. turns.Take(turns.Count - 1), turns[^1] with { Transcript = transcript }];

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

    /// <summary>A record's own turns: those it kept, none where it kept none (a record's opening writes its first).</summary>
    public static IReadOnlyList<SessionTurn> Own(Session record) => record.Turns ?? [];

    /// <summary>
    /// The session's turns across the records of its line, oldest first and numbered from one, each named with the record it ran in and its
    /// agent, account, adapter and transcript, as the turn kept them; a turn that kept none reads its record's. A turn
    /// ends where the next begins, or where its record ended.
    /// </summary>
    public static IReadOnlyList<SessionTurn> OfLine(IEnumerable<Session> members)
    {
        var ordered = members.OrderBy(record => record.Created).ThenBy(record => record.Id, StringComparer.Ordinal).ToList();
        var turns = new List<SessionTurn>();
        foreach (var record in ordered)
        {
            foreach (var turn in Own(record).OrderBy(turn => turn.Number))
            {
                turns.Add(turn with
                {
                    Number = turns.Count + 1, Part = record.Id, Agent = turn.Agent ?? record.Door, Account = turn.Account ?? record.Profile,
                    Adapter = turn.Adapter ?? record.DoorVersion, Transcript = turn.Transcript ?? record.Transcript,
                    Ended = record.Active ? null : record.Updated,
                });
            }
        }

        for (var at = 0; at + 1 < turns.Count; at++) turns[at] = turns[at] with { Ended = turns[at + 1].At };
        return turns;
    }

    /// <summary>The turns as the record's column keeps them.</summary>
    public static string Write(IReadOnlyList<SessionTurn> turns) =>
        JsonSerializer.Serialize(
            turns.Select(turn => new Kept(
                turn.Number, turn.Cause, turn.At, turn.Node, turn.Attempt, turn.Input, turn.Output, turn.Agent, turn.Account, turn.Adapter, turn.Transcript,
                turn.End)),
            Options);

    /// <summary>The turns a record's column keeps; null for a record from before the column, or one that does not read.</summary>
    public static IReadOnlyList<SessionTurn>? Read(string? kept)
    {
        if (string.IsNullOrWhiteSpace(kept)) return null;
        try
        {
            return JsonSerializer.Deserialize<List<Kept>>(kept, Options)?
                .Select(turn => new SessionTurn(turn.N, turn.Cause, turn.At)
                {
                    Node = turn.Node, Attempt = turn.Attempt, Input = turn.Input, Output = turn.Output,
                    Agent = turn.Agent, Account = turn.Account, Adapter = turn.Adapter, Transcript = turn.Transcript, End = turn.End,
                })
                .ToList();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private sealed record Kept(
        int N, string Cause, DateTimeOffset At, string? Node, int? Attempt, long? Input, long? Output, string? Agent, string? Account, string? Adapter,
        string? Transcript, SessionTurnEnd? End = null);
}
