namespace Daoris.Driver;

/// <summary>
/// One request a line's parts sent, or one that asked the line, as Daoris observed it. Its delivery
/// (what Daoris saw of the request reaching its receiver) and its outcome (the protocol's own word for where it stands) are kept
/// apart: an answer kept but not yet handed is delivered and answered, and still held.
/// </summary>
/// <param name="Id"><c>helper:h7</c>, <c>piece:p1</c>, <c>opinion:o2</c>, <c>quest:q9</c>, <c>question:&lt;id&gt;</c>, <c>go-ahead:&lt;ask&gt;/&lt;n&gt;</c> or <c>hand-over:&lt;id&gt;</c>.</param>
/// <param name="Part">The session on the line the request belongs to: the one that sent it, or the one it asked.</param>
/// <param name="Act"><see cref="Exchanges.Request"/> or <see cref="Exchanges.HandOver"/>.</param>
/// <param name="Expects">What comes back: <see cref="Exchanges.Answer"/>, <c>work</c>, <c>piece</c>, <c>opinion</c>, <c>decision</c> or <c>successor</c>.</param>
/// <param name="Delivery">One of <see cref="Exchanges.Deliveries"/>.</param>
/// <param name="Outcome">The protocol's own word: asked, answered, approved, refused, done, declined, produced, taken-in, stale and the like.</param>
/// <param name="Holds">Whether the line's turn ended on it: it waits on something that has not come back.</param>
public sealed record Exchange(
    string Id, string Part, string Act, string Expects, string From, string To, DateTimeOffset At,
    string Delivery, string Outcome, bool Holds)
{
    /// <summary>Who took it up, where known.</summary>
    public ExchangeBy? By { get; init; }

    /// <summary>The answer and when it came, with the record it reached where one is named.</summary>
    public ExchangeAnswer? Answer { get; init; }

    /// <summary><c>yes</c>, or <c>not-available</c> for a case whose source is not read or whose door is not built.</summary>
    public string Available { get; init; } = Exchanges.Yes;

    /// <summary><c>read</c>, <c>unread</c> or <c>stale</c>: a source not read is said, never drawn as empty.</summary>
    public string Source { get; init; } = Exchanges.Read;
}

/// <summary>Who took an exchange up: a session, an agent, or someone on another machine.</summary>
public sealed record ExchangeBy(string? Session = null, string? Agent = null, bool Elsewhere = false);

/// <summary>The answer an exchange got: when, the word that carried it, and the record it reached.</summary>
public sealed record ExchangeAnswer(DateTimeOffset At, string? Word = null, string? On = null);

/// <summary>The person's answer to a park's question, read from the word that carried it: the word's <c>question</c> id names what it answered.</summary>
public sealed record ExchangeSaid(string Session, string Question, DateTimeOffset At, string? Word = null);

/// <summary>
/// An opinion asked on a working line, as plain values (Review is above this subdomain, so Loop maps its view): <c>reading</c>, <c>given</c>
/// or <c>failed</c>, and the session its findings were handed to; or, read from the reviewer's record alone, <c>ended</c> once
/// its session stopped, the opinion's own words being the landing gate's.
/// </summary>
public sealed record ExchangeOpinion(string Id, string Working, string Session, string Label, string? Adapter, string State, string? HandedTo);

/// <summary>
/// The snapshot <see cref="Exchanges.Of"/> reads: each source as the look holds it, a source it could not read left null. Plain values,
/// so the structure's lower readers can be handed what they need without naming the read (design §13 point 9).
/// </summary>
public sealed record ExchangeLook
{
    /// <summary>The session records the line is walked in (<see cref="ContextBriefs.LineOf"/>); null reads the session alone.</summary>
    public IReadOnlyDictionary<string, PriorSession>? Records { get; init; }

    public IReadOnlyList<HelperAsk>? Helpers { get; init; }
    public Func<string, HelperState?>? HelperState { get; init; }

    public IReadOnlyList<PartnerAsk>? Pieces { get; init; }
    public Func<string, PartnerState?>? PieceState { get; init; }

    /// <summary>The questions on file now, one per session at most.</summary>
    public IReadOnlyList<ParkQuestion>? Questions { get; init; }

    /// <summary>When the run last handed its agent words: a question asked before it is stale, not the line's turn.</summary>
    public DateTimeOffset? QuestionsSince { get; init; }

    public IReadOnlyList<ExchangeSaid>? Said { get; init; }

    public IReadOnlyList<HandOverAsk>? HandOvers { get; init; }
    public Func<string, HandOverAskState?>? HandOverState { get; init; }

    public IReadOnlyList<QuestView>? Quests { get; init; }

    public IReadOnlyList<ExchangeOpinion>? Opinions { get; init; }

    /// <summary>The ask the line's quest came from, and the go-aheads it holds.</summary>
    public string? Ask { get; init; }
    public IReadOnlyList<GoAheadView>? GoAheads { get; init; }

    /// <summary>Sources read earlier than the rest of the look, by <see cref="Exchanges.Sources"/> name.</summary>
    public IReadOnlySet<string> Stale { get; init; } = new HashSet<string>();
}

/// <summary>
/// The exchanges of a line, read: every request its parts sent and every request that asked them, from
/// the look's snapshot alone. Pure, so a restart that reads the same files reads the same exchanges.
/// </summary>
public static class Exchanges
{
    public const string Request = "request", HandOver = "hand-over";
    public const string Answer = "answer", Work = "work", Piece = "piece", Opinion = "opinion", Decision = "decision", Successor = "successor";
    public const string Yes = "yes", NotAvailable = "not-available";
    public const string Read = "read", Unread = "unread", Stale = "stale";
    public const string Person = "person";

    public const string Kept = "kept", Attempted = "attempted", Sent = "sent", Taken = "taken", Uncertain = "uncertain",
        NotReached = "not-reached", Elsewhere = "elsewhere";

    public static readonly IReadOnlyList<string> Deliveries = [Kept, Attempted, Sent, Taken, Uncertain, NotReached, Elsewhere];

    /// <summary>The sources, by the prefix their exchanges' ids carry.</summary>
    public static readonly IReadOnlyList<string> Sources = ["helper", "piece", "question", "hand-over", "quest", "opinion", "go-ahead"];

    /// <summary>
    /// The exchanges of the line <paramref name="session"/> ends, oldest first: its parts walked back by each record's <c>from</c>, so a
    /// head after a hand-over holds its predecessor's open ones. A source the look could not read is one exchange saying so
    /// (<c>&lt;source&gt;:unread</c>, not available, holding nothing), never an empty list.
    /// </summary>
    public static IReadOnlyList<Exchange> Of(ExchangeLook look, string session)
    {
        var parts = look.Records is { } records
            ? LineKeys.Upto(session, records).Select(part => part.Session).ToList()
            : [];
        if (!parts.Contains(session, StringComparer.Ordinal)) parts.Add(session);
        var on = parts.ToHashSet(StringComparer.Ordinal);
        var found = new List<Exchange>();

        found.AddRange(Helpers(look, on));
        found.AddRange(Pieces(look, on));
        found.AddRange(Questions(look, on));
        found.AddRange(HandOvers(look, on));
        found.AddRange(Quests(look, on, records: look.Records));
        found.AddRange(Opinions(look, on));
        found.AddRange(GoAheads(look, on));

        return
        [
            .. found.Select(exchange => look.Stale.Contains(exchange.Id[..exchange.Id.IndexOf(':')]) && exchange.Source == Read
                    ? exchange with { Source = Stale }
                    : exchange)
                .OrderBy(exchange => exchange.At).ThenBy(exchange => exchange.Id, StringComparer.Ordinal),
        ];
    }

    /// <summary>
    /// Whether <paramref name="session"/>'s turn ends on its own helper alone: an ask of its agent's held, and no question or
    /// go-ahead held on the person, nor one unread. A question on file that does not read is no proof that none is asked,
    /// and a go-ahead the service did not answer is the same: it parks, as one asked does.
    /// </summary>
    public static bool HoldsOnOwnHelper(IReadOnlyList<Exchange> exchanges, string session)
    {
        var own = exchanges.Where(exchange => string.Equals(exchange.Part, session, StringComparison.Ordinal)).ToList();
        if (!own.Any(exchange => exchange.Holds && exchange.Id.StartsWith("helper:", StringComparison.Ordinal))) return false;
        return !own.Any(exchange => exchange.Source == Unread
                                    ? exchange.Id is "question:unread" or "go-ahead:unread"
                                    : exchange.Holds && exchange.To == Person);
    }

    public static bool AsksPersonNothing(IReadOnlyList<Exchange> exchanges, string session) =>
        !exchanges.Where(exchange => string.Equals(exchange.Part, session, StringComparison.Ordinal))
            .Any(exchange => exchange.Source == Unread
                ? exchange.Id is "question:unread" or "go-ahead:unread"
                : exchange.Holds && exchange.To == Person);

    /// <summary>Whether any source is not read: a caller deciding that nothing waits must not read these as none.</summary>
    public static bool AnyUnread(IEnumerable<Exchange> exchanges) => exchanges.Any(exchange => exchange.Source == Unread);

    private static Exchange Unreadable(string source, string session) =>
        new($"{source}:unread", session, Request, Answer, session, source, DateTimeOffset.MinValue, Uncertain, "unread", false)
        {
            Available = NotAvailable,
            Source = Unread,
        };

    private static IEnumerable<Exchange> Helpers(ExchangeLook look, HashSet<string> on)
    {
        if (look.Helpers is not { } asks || look.HelperState is not { } state) return [Unreadable("helper", on.First())];
        return
        [
            .. asks.Where(ask => on.Contains(ask.Asker)).Select(ask =>
            {
                var at = state(ask.Id);
                var kind = at?.State;
                var (delivery, outcome) = kind switch
                {
                    HelperStates.Refused => (NotReached, "refused"),
                    // An answer kept for the person is answered and reaches the line only by their pass.
                    HelperStates.Answered when at?.Handed == HelperState.KeptForPerson => (Kept, "answered"),
                    HelperStates.Answered when at?.Handed is not null => (Taken, "taken-in"),
                    HelperStates.Answered => (Taken, "answered"),
                    HelperStates.Started => (Taken, "asked"),
                    HelperStates.Approved => (Kept, "approved"),
                    _ => (Kept, "asked"),
                };
                return new Exchange(
                    $"helper:{ask.Id}", ask.Asker, Request, Answer, ask.Asker, ask.Agent ?? "helper", ask.At, delivery, outcome,
                    Holds: ask.AskedBy == HelperAsk.ByAgent && !HelperWaits.Settled(at))
                {
                    By = at?.Helper is { } helper ? new ExchangeBy(Session: helper, Agent: at.Agent) : null,
                    Answer = kind == HelperStates.Answered && at is not null ? new ExchangeAnswer(at.At, at.Handed) : null,
                };
            }),
        ];
    }

    private static IEnumerable<Exchange> Pieces(ExchangeLook look, HashSet<string> on)
    {
        if (look.Pieces is not { } asks || look.PieceState is not { } state) return [Unreadable("piece", on.First())];
        return
        [
            .. asks.Where(ask => on.Contains(ask.Asker)).Select(ask =>
            {
                var at = state(ask.Id);
                var kind = at?.State ?? PartnerStates.Asked;
                var delivery = kind switch
                {
                    PartnerStates.Refused => NotReached,
                    PartnerStates.Uncertain => Uncertain,
                    PartnerStates.Asked or PartnerStates.Waiting or PartnerStates.Approved => Kept,
                    _ => Taken,
                };
                return new Exchange(
                    $"piece:{ask.Id}", ask.Asker, Request, Piece, ask.Asker, ask.Agent ?? "partner", ask.At, delivery, kind,
                    Holds: ask.AskedBy == PartnerAsk.ByAgent && !PartnerStates.Final(kind))
                {
                    By = at?.Partner is { } partner ? new ExchangeBy(Session: partner, Agent: at.Agent) : null,
                };
            }),
        ];
    }

    private static IEnumerable<Exchange> Questions(ExchangeLook look, HashSet<string> on)
    {
        if (look.Questions is not { } questions) return [Unreadable("question", on.First())];
        var said = (look.Said ?? []).Where(word => on.Contains(word.Session)).GroupBy(word => word.Question)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(word => word.At).First(), StringComparer.Ordinal);
        var found = new List<Exchange>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var question in questions.Where(question => on.Contains(question.Session)))
        {
            seen.Add(question.Id);
            if (said.TryGetValue(question.Id, out var answer))
            {
                found.Add(Answered(question.Session, question.Id, question.At, answer));
                continue;
            }

            var stale = look.QuestionsSince is { } since && question.At < since;
            found.Add(new Exchange(
                $"question:{question.Id}", question.Session, Request, Answer, question.Session, Person, question.At, Kept,
                stale ? "stale" : "asked", Holds: !stale));
        }

        // The file goes at the next open, so an answered question is read from the word alone.
        found.AddRange(said.Where(each => !seen.Contains(each.Key)).Select(each => Answered(each.Value.Session, each.Key, each.Value.At, each.Value)));
        return found;

        static Exchange Answered(string session, string id, DateTimeOffset at, ExchangeSaid answer) =>
            new($"question:{id}", session, Request, Answer, session, Person, at, Taken, "answered", Holds: false)
            {
                Answer = new ExchangeAnswer(answer.At, answer.Word),
            };
    }

    private static IEnumerable<Exchange> HandOvers(ExchangeLook look, HashSet<string> on)
    {
        if (look.HandOvers is not { } asks || look.HandOverState is not { } state) return [Unreadable("hand-over", on.First())];
        return
        [
            .. asks.Where(ask => on.Contains(ask.Session)).Select(ask =>
            {
                var at = state(ask.Session) is { } each && each.Id == ask.Id ? each : null;
                var (delivery, outcome, holds) = at?.State switch
                {
                    HandOverAskStates.Taken => (Taken, "taken-in", false),
                    HandOverAskStates.Refused => (NotReached, "refused", false),
                    HandOverAskStates.Approved => (Kept, "approved", true),
                    _ => (Kept, "asked", true),
                };
                return new Exchange(
                    $"hand-over:{ask.Id}", ask.Session, HandOver, Successor, ask.Session, at?.Agent ?? "successor", ask.At, delivery, outcome, holds)
                {
                    By = at is { State: HandOverAskStates.Taken, Agent: { } agent } ? new ExchangeBy(Agent: agent) : null,
                };
            }),
        ];
    }

    private static IEnumerable<Exchange> Quests(ExchangeLook look, HashSet<string> on, IReadOnlyDictionary<string, PriorSession>? records)
    {
        if (look.Quests is not { } quests) return [Unreadable("quest", on.First())];
        var byId = quests.ToDictionary(quest => quest.Id, StringComparer.OrdinalIgnoreCase);
        var found = new List<Exchange>();
        foreach (var part in on)
        {
            // A conversation's quest is the one it took through its own connector.
            if (records is null || !records.TryGetValue(part, out var record) || (record.Quest ?? record.TookQuest) is not { } own
                || !byId.TryGetValue(own, out var mine) || mine.Awaits is not { Length: > 0 } awaits)
            {
                continue;
            }

            var asked = byId.GetValueOrDefault(awaits);
            var (delivery, outcome, holds) = asked?.Status switch
            {
                null => (Elsewhere, "asked", true),
                "Done" => (Taken, "done", false),
                "Taken" => (Taken, "taken-in", true),
                _ => (Kept, "asked", true),
            };
            found.Add(new Exchange($"quest:{awaits}", part, Request, Work, part, asked?.To ?? "another repository", DateTimeOffset.MinValue, delivery, outcome, holds));
        }

        return found;
    }

    private static IEnumerable<Exchange> Opinions(ExchangeLook look, HashSet<string> on)
    {
        if (look.Opinions is not { } opinions) return [Unreadable("opinion", on.First())];
        return
        [
            .. opinions.Where(opinion => on.Contains(opinion.Working)).Select(opinion =>
            {
                var (delivery, outcome) = opinion.State switch
                {
                    "given" when opinion.HandedTo is not null => (Taken, "taken-in"),
                    "given" => (Kept, "produced"),
                    "failed" => (NotReached, "declined"),
                    "ended" => (Taken, "ended"),
                    _ => (Kept, "asked"),
                };
                return new Exchange(
                    $"opinion:{opinion.Id}", opinion.Working, Request, Opinion, opinion.Working, opinion.Label, DateTimeOffset.MinValue, delivery, outcome,
                    Holds: opinion.State == "reading")
                {
                    By = new ExchangeBy(Session: opinion.Session, Agent: opinion.Adapter),
                };
            }),
        ];
    }

    private static IEnumerable<Exchange> GoAheads(ExchangeLook look, HashSet<string> on)
    {
        if (look.GoAheads is not { } goAheads) return [Unreadable("go-ahead", on.First())];
        return
        [
            .. goAheads.SelectMany(goAhead => goAhead.Asked
                .Where(request => on.Contains(request.Session))
                .Take(1)
                .Select(request => new Exchange(
                    $"go-ahead:{look.Ask}/{goAhead.Number}", request.Session, Request, Decision, request.Session, Person, request.At, Kept,
                    goAhead.Answer is null ? "asked" : goAhead.Answer.Approved ? "approved" : "refused", Holds: goAhead.Answer is null)
                {
                    Answer = goAhead.Answer is { } answer ? new ExchangeAnswer(answer.At, answer.Words) : null,
                })),
        ];
    }
}
