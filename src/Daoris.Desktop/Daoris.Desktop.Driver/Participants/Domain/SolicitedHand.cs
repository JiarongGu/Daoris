using System.Text.Json;
using static Daoris.Driver.Json;

namespace Daoris.Driver;

/// <summary>What the one door for solicited answers did with a hand (COMMS1b, the comms design §4.2), as the driver reads its answer.</summary>
public enum SolicitedFate
{
    /// <summary>The word was kept now on <see cref="SolicitedAnswer.Holder"/>.</summary>
    Kept,

    /// <summary>
    /// The key's word waits already on <see cref="SolicitedAnswer.Holder"/> (§13 point 4): nothing was kept now, and the look finishes the
    /// dispatch there before it marks the hand done, as for a word just kept.
    /// </summary>
    Waiting,

    /// <summary>The key's word left a record of the line (row p): nothing was kept, and there is no word to dispatch.</summary>
    Settled,

    /// <summary>Nothing was kept, in the host's own sentence; a host older than the door is one.</summary>
    Refused,

    /// <summary>No answer that says either way: the word may wait on the line, and the next look asks again under its key.</summary>
    Uncertain,
}

/// <summary>A hand's answer: its fate, the host's sentence, the word and the record that holds it, or the record a settled word left.</summary>
public sealed record SolicitedAnswer(SolicitedFate Fate, string Message)
{
    /// <summary>The word's id where one was kept now or waits already.</summary>
    public string? Word { get; init; }

    /// <summary>The word's text as the host keeps it.</summary>
    public string? Text { get; init; }

    /// <summary>The line's head that holds the word, as the host answers it; the hand goes on there, never on a record the line left.</summary>
    public PriorSession? Holder { get; init; }

    /// <summary>The record a settled word left.</summary>
    public string? SettledOn { get; init; }

    /// <summary>Whether the answer is on the line or went there before: kept, waiting or settled.</summary>
    public bool Handed => Fate is SolicitedFate.Kept or SolicitedFate.Waiting or SolicitedFate.Settled;
}

/// <summary>What a hand does before its door, by the attempt mark it finds (§13 point 3).</summary>
public enum SolicitedStep
{
    /// <summary>No attempt on file: the mark is written, then the door asked.</summary>
    Hand,

    /// <summary>An attempt inside <see cref="SolicitedHand.Horizon"/>: the door is asked again under the same key, which answers it once.</summary>
    Retry,

    /// <summary>An attempt past the horizon, which the host's settlement no longer answers: never sent again, it goes to the person.</summary>
    Uncertain,
}

/// <summary>How the driver's walk of a line ended (the service's <c>LineEnd</c>).</summary>
public enum SolicitedLineEnd
{
    Head,
    Cycle,
    Bound,
}

/// <summary>A line as the driver reads it from the records: the record named, its head where one was reached, and each record walked.</summary>
public sealed record SolicitedLine(PriorSession? Named, PriorSession? Head, IReadOnlyList<PriorSession> Records, SolicitedLineEnd End = SolicitedLineEnd.Head);

/// <summary>
/// The one hand a helper's, a partner's and an opinion's answers go through (COMMS1b, the comms design §4.2 and §13 points 1 to 4): the attempt
/// mark written before the door and the door's answer settling it, a retry only inside the settlement's horizon, the line's head read as the door
/// walks it for the looks' pre-check, and the door's answer read alike for the three. A host that answers no <c>answers</c> mark keeps each look's
/// own hand.
/// </summary>
public static class SolicitedHand
{
    /// <summary>How long the host answers a settled key (the service's <c>SaidKeys.Horizon</c>): a retry past it could keep the word again.</summary>
    public static readonly TimeSpan Horizon = TimeSpan.FromDays(7);

    /// <summary>The most hops the walk follows (the service's <c>Lines.Bound</c>): a line that runs past it has no head read.</summary>
    public const int Bound = 256;

    /// <summary>What a hand does with the attempt <paramref name="attempted"/> on file at <paramref name="now"/>.</summary>
    public static SolicitedStep Before(DateTimeOffset? attempted, DateTimeOffset now) =>
        attempted is not { } at ? SolicitedStep.Hand
        : now - at < Horizon ? SolicitedStep.Retry
        : SolicitedStep.Uncertain;

    /// <summary>
    /// The line <paramref name="named"/> is on, walked forward as the door walks it: to the newest record continuing it by <c>from</c>, across
    /// kinds, and for a driven part with none to a later run of its quest; a record walked twice ends it as a cycle, a hop past
    /// <see cref="Bound"/> at the bound, each with no head.
    /// </summary>
    public static SolicitedLine Walk(IEnumerable<PriorSession> records, string named)
    {
        IReadOnlyList<PriorSession> all = [.. records];
        if (all.FirstOrDefault(record => string.Equals(record.Session, named, StringComparison.Ordinal)) is not { } first) return new(null, null, []);
        var line = new List<PriorSession> { first };
        var seen = new HashSet<string>(StringComparer.Ordinal) { first.Session };
        var at = first;
        for (var hops = 0; ; hops++)
        {
            var next = all.Where(record => string.Equals(record.From, at.Session, StringComparison.Ordinal)).OrderByDescending(record => record.Created).FirstOrDefault()
                       ?? LaterRun(all, at);
            if (next is null) return new(first, at, line);
            if (!seen.Add(next.Session)) return new(first, null, line, SolicitedLineEnd.Cycle);
            if (hops == Bound) return new(first, null, line, SolicitedLineEnd.Bound);
            line.Add(next);
            at = next;
        }
    }

    /// <summary>A driven part's quest run here made after it, the newest, which the door's walk reaches where nothing continues it by <c>from</c>.</summary>
    private static PriorSession? LaterRun(IReadOnlyList<PriorSession> all, PriorSession at) =>
        at is { Kind: not "chat", Quest: { } quest, Created: { } made }
            ? all.Where(record => record.Kind != "chat" && string.Equals(record.Quest, quest, StringComparison.Ordinal) && record.Created > made)
                .OrderByDescending(record => record.Created).FirstOrDefault()
            : null;

    /// <summary>
    /// Whether the line's head takes a solicited answer now (§13 point 1): an open conversation between turns, an ended record, and a park the
    /// person answered, beside their answer; never a running turn, nor a park still waiting on the person.
    /// </summary>
    public static bool TakesNow(PriorSession head) => head switch
    {
        { Kind: "chat" } when string.Equals(head.State, "working", StringComparison.OrdinalIgnoreCase) => true,
        { Running: true } => false,
        { Parked: true } => head.AnsweredPark,
        _ => true,
    };

    /// <summary>
    /// A hand door's answer (<c>POST …/hand</c>): only a refusal the host gave says nothing was kept, its sentence on a bad request, a not-found or
    /// a conflict, or a bare 404 from a host older than the door; any other status, or a success that does not read, is uncertain.
    /// </summary>
    public static SolicitedAnswer Read(bool ok, int status, JsonElement? root, string payload, string door, string service)
    {
        if (root is not { ValueKind: JsonValueKind.Object } answer)
        {
            return ok || status != 404
                ? new(SolicitedFate.Uncertain, $"the service at {service} answered `{door}` with {status} that does not read")
                : new(SolicitedFate.Refused, $"the service at {service} has no `{door}` door ({status}) — is it older than this driver?");
        }

        if (!ok)
        {
            var refused = status is 400 or 404 or 409 && Text(answer, "error") is not null;
            return new(refused ? SolicitedFate.Refused : SolicitedFate.Uncertain, Text(answer, "error") ?? payload);
        }

        var message = Text(answer, "message") ?? "";
        // Settled before a word (row p): nothing was kept and there is no word.
        if (Text(answer, "settled") is { Length: > 0 } settled) return new(SolicitedFate.Settled, message) { SettledOn = settled };

        var said = answer.TryGetProperty("said", out var word) ? word : default;
        return new(Flag(answer, "waiting") ? SolicitedFate.Waiting : SolicitedFate.Kept, message)
        {
            Word = Text(said, "id"),
            Text = Text(said, "text"),
            Holder = answer.TryGetProperty("session", out var session) && Text(session, "id") is { Length: > 0 } id ? Holder(session, id) : null,
        };
    }

    /// <summary>A hand that got no answer (a broken connection, a client that gave up): the word may wait on the line.</summary>
    public static SolicitedAnswer Lost(string door, string service, string why) =>
        new(SolicitedFate.Uncertain, $"the service at {service} gave no answer to `{door}` ({why})");

    /// <summary>The record the host answers holding the word, as far as a dispatch reads it: its id, state, kind, tree and the person's answer.</summary>
    private static PriorSession Holder(JsonElement session, string id) =>
        new(id, Text(session, "tree"), Text(session, "state") ?? "", Answer: Text(session, "answer"))
        {
            Kind = Text(session, "kind") ?? "driven",
            From = Text(session, "from"),
        };
}
