namespace Daoris.Driver;

/// <summary>
/// What a turn of Ask Daoris's did that no card or hold put to the person, read from its events log, its connector's lineage lines and its
/// press record. A turn runs from the person's words where the session took them to its <c>turn</c> event, or, with none, to the log's
/// last event, interrupted; a word shown waiting is no turn, and one taken while a turn runs joins it. A call's updates merge by its id.
/// </summary>
/// <remarks>
/// Known reads (<c>read</c>, <c>search</c>, <c>fetch</c>, a <c>think</c> that delegates nothing) are counted and never read; an acting kind
/// is read; a card of any other kind is a connector call where a line accounts for it, by the harness's id of the call where both carry it
/// and else by count, classed by the act its door worded, and unknown where none does, which is read. A delegation is read, its child by
/// the same table where its stream is kept. Daoris parses no command: the reader, a model, reads them.
/// </remarks>
public static class AskDaorisSelection
{
    private static readonly HashSet<string> KnownReads = new(StringComparer.Ordinal) { "read", "search", "fetch" };
    private static readonly HashSet<string> Acting = new(StringComparer.Ordinal) { "edit", "delete", "move", "execute" };
    private static readonly HashSet<string> Delegating = new(StringComparer.Ordinal) { "Agent", "Task" };

    /// <summary>Every turn of <paramref name="evidence"/>'s log, in order, each selected or not.</summary>
    public static IReadOnlyList<ReadTurn> Turns(TurnEvidence evidence)
    {
        var turns = new List<ReadTurn>();
        Open? open = null;
        foreach (var e in evidence.Events.OrderBy(each => each.Seq))
        {
            if (e.Kind == SessionEventKind.User && e.Reaches is null)
            {
                if (open is null) open = new Open(e);
                else open.Last = e;
                continue;
            }

            if (open is null) continue;
            open.Last = e;
            if (e.Kind == SessionEventKind.Tool && e.Id is { } id) open.Merge(id, e);
            if (e.Kind != SessionEventKind.Turn) continue;
            turns.Add(Close(open, evidence, interrupted: false));
            open = null;
        }

        if (open is not null) turns.Add(Close(open, evidence, interrupted: true));
        return turns;
    }

    private static ReadTurn Close(Open open, TurnEvidence evidence, bool interrupted)
    {
        var (from, to) = (open.Words.At, open.Last.At);
        var reading = new Reading();
        var lines = evidence.Calls.Select((call, index) => (Call: call, Id: $"line-{index + 1}"))
            .Where(line => line.Call.At >= from && line.Call.At <= to).ToList();
        var byId = lines.Where(line => line.Call.ToolUse is not null).ToDictionary(line => line.Call.ToolUse!, StringComparer.Ordinal);
        var unnamed = new Queue<(ConnectorCall Call, string Id)>(lines.Where(line => line.Call.ToolUse is null));
        var matched = new HashSet<string>(StringComparer.Ordinal);

        foreach (var card in open.Cards)
        {
            if (evidence.Held.Contains(card.Id))
            {
                reading.Yours.Add(card.Id);
                continue;
            }

            if (Classed(card, reading, evidence, prefix: "")) continue;
            if (byId.TryGetValue(card.Id, out var named))
            {
                matched.Add(named.Id);
                Connector(named.Call, card, reading);
            }
            else if (unnamed.TryDequeue(out var counted))
            {
                matched.Add(counted.Id);
                reading.Limit(ReadLimit.ByCount);
                Connector(counted.Call, card, reading);
            }
            else reading.Read(card, SelectedWhy.Unknown, card.ToolName);
        }

        foreach (var (call, id) in lines.Where(line => !matched.Contains(line.Id)))
        {
            Connector(call, new Card(id) { Title = call.Tool, ToolName = call.Tool, At = call.At }, reading);
        }

        var presses = evidence.Presses.Where(press => press.At >= from && press.At <= to)
            .Where(press => !press.Asked || press.Outcome == "allowed")
            .Select(press => new SelectedPress(press.Route, press.Place, press.Outcome, press.Asked)).ToList();
        if (interrupted) reading.Limit(ReadLimit.Interrupted);
        return new ReadTurn(
            open.Words.Id ?? $"seq-{open.Words.Seq}", open.Words.Seq, open.Last.Seq, from, to, interrupted, open.Words.Text,
            reading.Calls, presses, reading.Yours, reading.Reads, reading.Limits);
    }

    /// <summary>A card classed by its kind alone, where its kind places it; false where only a connector line can.</summary>
    private static bool Classed(Card card, Reading reading, TurnEvidence evidence, string prefix)
    {
        var kind = card.Kind?.ToLowerInvariant();
        if (kind is "think" || (card.ToolName is { } name && Delegating.Contains(name)))
        {
            if (card.Stream is null && (card.ToolName is null || !Delegating.Contains(card.ToolName)) && !Delegating.Contains(card.Title ?? ""))
            {
                reading.Count("think");
                return true;
            }

            reading.Read(card, SelectedWhy.Delegated, card.ToolName, prefix);
            if (card.Stream is { } stream && evidence.Streams?.Invoke(stream) is { } child)
            {
                foreach (var each in Merged(child)) Classed(each, reading, evidence, $"{prefix}{card.Id}/");
            }
            else reading.Limit(ReadLimit.Delegated, prefix + card.Id);
            return true;
        }

        if (kind is not null && KnownReads.Contains(kind))
        {
            reading.Count(kind);
            return true;
        }

        if (kind is not null && Acting.Contains(kind))
        {
            reading.Read(card, SelectedWhy.Act, card.ToolName, prefix);
            return true;
        }

        if (prefix.Length == 0) return false;
        reading.Read(card, SelectedWhy.Unknown, card.ToolName, prefix);
        return true;
    }

    private static void Connector(ConnectorCall call, Card card, Reading reading)
    {
        switch (call.Act)
        {
            case "search" or "read":
                reading.Count("connector");
                break;
            case "propose":
                reading.Count("proposed");
                break;
            case "press":
                reading.Read(card, SelectedWhy.Press, call.Tool);
                break;
            default:
                reading.Read(card, SelectedWhy.Unknown, call.Tool);
                break;
        }
    }

    private static IReadOnlyList<Card> Merged(IReadOnlyList<SessionEvent> events)
    {
        var cards = new Open(new SessionEvent { Kind = SessionEventKind.User });
        foreach (var e in events.Where(each => each.Kind == SessionEventKind.Tool && each.Id is not null)) cards.Merge(e.Id!, e);
        return cards.Cards;
    }

    /// <summary>A tool call's card with its updates merged: each field the newest event that carries it.</summary>
    private sealed record Card(string Id)
    {
        public string? Kind { get; set; }
        public string? Title { get; set; }
        public string? ToolName { get; set; }
        public string? Stream { get; set; }
        public string? Status { get; set; }
        public string? Input { get; set; }
        public string? Output { get; set; }
        public IReadOnlyList<string> Locations { get; set; } = [];
        public DateTimeOffset At { get; set; }
    }

    private sealed class Open(SessionEvent words)
    {
        private readonly Dictionary<string, Card> _byId = new(StringComparer.Ordinal);

        public SessionEvent Words { get; } = words;

        public SessionEvent Last { get; set; } = words;

        public List<Card> Cards { get; } = [];

        public void Merge(string id, SessionEvent e)
        {
            if (!_byId.TryGetValue(id, out var card))
            {
                card = new Card(id) { At = e.At };
                _byId[id] = card;
                Cards.Add(card);
            }

            card.Kind = e.ToolKind ?? card.Kind;
            card.Title = e.Title ?? card.Title;
            card.ToolName = e.ToolName ?? card.ToolName;
            card.Stream = e.Stream ?? card.Stream;
            card.Status = e.Status ?? card.Status;
            card.Input = e.Input ?? card.Input;
            card.Output = e.Output ?? card.Output;
            if (e.Locations is { Count: > 0 } locations) card.Locations = locations;
        }
    }

    private sealed class Reading
    {
        public List<SelectedCall> Calls { get; } = [];
        public List<string> Yours { get; } = [];
        public Dictionary<string, int> Reads { get; } = new(StringComparer.Ordinal);
        public List<ReadLimitAt> Limits { get; } = [];

        public void Count(string kind) => Reads[kind] = Reads.GetValueOrDefault(kind) + 1;

        public void Limit(string code, string? call = null)
        {
            var limit = new ReadLimitAt(code, call);
            if (!Limits.Contains(limit)) Limits.Add(limit);
        }

        public void Read(Card card, string why, string? tool, string prefix = "")
        {
            var id = prefix + card.Id;
            IReadOnlyList<string> cut = [.. new[] { ("input", card.Input), ("output", card.Output) }.Where(field => Clipped(field.Item2)).Select(field => field.Item1)];
            if (cut.Count > 0) Limit(ReadLimit.Cut, id);
            Calls.Add(new SelectedCall(id, why, card.Kind, card.Title, tool, card.Status, card.Locations, card.Input, card.Output, cut));
        }

        // The record cuts a raw field at its limit and says the full length after the cut (TextBounds.ClipCounted).
        private static bool Clipped(string? text) =>
            text is { Length: > SessionEventText.RawLimit } && text.EndsWith(" chars)", StringComparison.Ordinal)
            && text.LastIndexOf(TextBounds.Ellipsis + " (", StringComparison.Ordinal) >= SessionEventText.RawLimit - 1;
    }
}
