using System.Text;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>The line's records read into the turns a brief is composed from: the words taken, the calls made and the checks run.</summary>
internal static class BriefReading
{
    internal sealed class Word(string lead, string text, int part)
    {
        public string Lead { get; } = lead;
        public string Text { get; } = text;
        public int Part { get; } = part;
        public IReadOnlyList<string> Files { get; init; } = [];
        public bool Person { get; init; }
        public string? Piece { get; set; }
        public bool Kept { get; set; }
        public bool Clipped { get; set; }
    }

    internal sealed class Call(string id, int at)
    {
        public string Id { get; } = id;
        public int At { get; } = at;
        public string? Title { get; set; }
        public string? Kind { get; set; }
        public string? Status { get; set; }
        public string? Input { get; set; }
        public string? Output { get; set; }
        public List<string> Locations { get; } = [];
    }

    internal sealed class Turn(int part, int number)
    {
        public int Part { get; } = part;
        public int Number { get; } = number;
        public List<Word> Words { get; } = [];
        public List<Call> Calls { get; } = [];
        public StringBuilder? Last { get; set; }
        public bool Ended { get; set; }
        public bool Begun { get; set; }

        /// <summary>It took the request, which the opening quotes: its words point back to it rather than tell it twice.</summary>
        public bool Request { get; set; }
        public string? Outcome { get; set; }
        public bool Kept { get; set; }
        public bool Clipped { get; set; }
    }

    internal sealed record Check(string Gate, string Title, string Status, string? Output, int Part);

    internal sealed record LineRead(List<Turn> Turns, Word? Request, List<Check> Checks, (IReadOnlyList<PlanEntry> Steps, int Part, int Turn)? Plan);

    internal static LineRead Read(BriefLine line)
    {
        var turns = new List<Turn>();
        var checks = new List<Check>();
        (IReadOnlyList<PlanEntry>, int, int)? plan = null;

        for (var p = 0; p < line.Parts.Count; p++)
        {
            var events = line.Parts[p].Events;

            // One word is one id, as its latest event says: shown, told again for its level and taken, it counts once,
            // where it is taken; one taken back, or still waiting, is never taken, and follows the brief from `said` if it waits.
            var latest = new Dictionary<string, int>(StringComparer.Ordinal);
            var withdrawn = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < events.Count; i++)
            {
                if (events[i] is { Kind: SessionEventKind.User, Id: { } id }) latest[id] = i;
                if (events[i] is { Kind: SessionEventKind.Note, Code: SessionEventCodes.Withdrawn, Words: { } ids }) withdrawn.UnionWith(ids);
            }

            var turn = new Turn(p, 1);
            var calls = new Dictionary<string, Call>(StringComparer.Ordinal);
            var running = false;
            for (var i = 0; i < events.Count; i++)
            {
                var e = events[i];
                if (e.Kind == SessionEventKind.Usage) continue;
                if (e.Kind == SessionEventKind.Message)
                {
                    if (!running) turn.Last = new StringBuilder();
                    turn.Last!.Append(e.Text);
                    running = turn.Begun = true;
                    continue;
                }

                // A message is broken, as the page breaks it, by anything but a usage reading.
                running = false;
                switch (e.Kind)
                {
                    case SessionEventKind.User when e.Reaches is null:
                        if (e.Id is { } id && (latest[id] != i || withdrawn.Contains(id))) break;
                        turn.Begun = true;
                        if (WordOf(e, p) is { } word) turn.Words.Add(word);
                        break;
                    case SessionEventKind.Tool when e.Id is { } callId:
                        if (!calls.TryGetValue(callId, out var call))
                        {
                            calls[callId] = call = new Call(callId, i);
                            turn.Calls.Add(call);
                        }

                        call.Title = e.Title ?? call.Title;
                        call.Kind = e.ToolKind ?? call.Kind;
                        call.Status = e.Status ?? call.Status;
                        call.Input = e.Input ?? call.Input;
                        call.Output = e.Output ?? ContentText(e) ?? call.Output;
                        if (e.Locations is { } locations) call.Locations.AddRange(locations);
                        turn.Begun = true;
                        break;
                    case SessionEventKind.Plan when e.Entries is { } entries:
                        plan = (entries, p, turn.Number);
                        break;
                    case SessionEventKind.Turn:
                        turn.Ended = true;
                        turns.Add(turn);
                        turn = new Turn(p, turn.Number + 1);
                        break;
                }
            }

            if (turn.Begun) turns.Add(turn);

            foreach (var call in calls.Values.OrderBy(call => call.At))
            {
                if (GateOf(CommandOf(call.Input), line.Gates) is { } gate)
                {
                    checks.Add(new Check(gate, ContextBrief.Unrooted(call.Title ?? gate, line.Tree), call.Status ?? "pending", call.Output, p));
                }
            }
        }

        // A conversation's request is its first word, never told again among the words.
        Word? request = null;
        if (line.Conversation && turns.FirstOrDefault(turn => turn.Part == 0) is { } first
            && first.Words.FirstOrDefault() is { Person: true } asked && !asked.Lead.Contains("channel", StringComparison.Ordinal))
        {
            request = asked;
            first.Words.Remove(asked);
            first.Request = true;
        }

        return new LineRead(turns, request, checks, plan);
    }

    /// <summary>A word the session took, marked by who said it as the record marks it; null for Daoris's own composed words.</summary>
    private static Word? WordOf(SessionEvent e, int part)
    {
        var files = e.Files ?? [];
        if (e.Origin is null or "person")
        {
            return new Word(e.Channel is not null ? "The person, on the ask's channel:" : "The person said:", e.Text ?? "", part)
            {
                Files = files, Person = true,
            };
        }

        // Daoris's own words and every target it composed (an instruction, an earlier brief) are not repeated: the new part's
        // instruction is composed afresh.
        if (e.From == SaidFrom.Daoris) return null;
        if (e.Opinion is { Length: > 0 } opinion) return new Word($"Another agent's findings (opinion `{opinion}`), as its claim:", e.Text ?? "", part);
        if (e.Channel is not null || (e.Text ?? "").StartsWith(ContextBrief.ChannelClaimOpening, StringComparison.Ordinal))
        {
            return new Word("Another session's message on the ask's channel, as that agent's claim:", e.Text ?? "", part);
        }

        return null;
    }

    private static string? ContentText(SessionEvent e) =>
        e.Content?.Select(content => content.Text).OfType<string>().Where(text => text.Length > 0).ToList() is { Count: > 0 } texts
            ? string.Join("\n", texts)
            : null;

    /// <summary>A call's command as its kept input has it: the protocol's <c>command</c>, or the input itself.</summary>
    private static string? CommandOf(string? input)
    {
        if (input is not { Length: > 0 }) return null;
        try
        {
            using var document = JsonDocument.Parse(input);
            if (document.RootElement.ValueKind == JsonValueKind.Object)
            {
                foreach (var name in new[] { "command", "cmd" })
                {
                    if (!document.RootElement.TryGetProperty(name, out var value)) continue;
                    if (value.ValueKind == JsonValueKind.String) return value.GetString();
                    if (value.ValueKind == JsonValueKind.Array)
                    {
                        return string.Join(' ', value.EnumerateArray().Where(item => item.ValueKind == JsonValueKind.String).Select(item => item.GetString()));
                    }
                }
            }
        }
        catch (JsonException)
        {
            // An input cut at its bound is no longer JSON: its words are still the command's.
        }

        return input;
    }

    /// <summary>
    /// The declared gate a command runs, by its command line as the repository declares it, never a guess from a title;
    /// <c>run-heavy</c> runs the heavy gates through the slot (HEAVYRUN1b1). Null for every other command.
    /// </summary>
    private static string? GateOf(string? command, IReadOnlyList<HeavyGate> gates)
    {
        if (command is null) return null;
        var said = TextBounds.Fold(command);
        foreach (var gate in gates)
        {
            if (TextBounds.Fold(gate.Run) is { Length: > 0 } run && Holds(said, run)) return gate.Name;
        }

        return Holds(said, "daoris gate run") ? "run-heavy" : null;
    }

    /// <summary>Whether a command holds a gate's line whole: what follows it ends a word, so <c>npm run verify:fast</c> is not <c>npm run verify</c>.</summary>
    private static bool Holds(string command, string run)
    {
        for (var at = command.IndexOf(run, StringComparison.Ordinal); at >= 0; at = command.IndexOf(run, at + 1, StringComparison.Ordinal))
        {
            var end = at + run.Length;
            if (end == command.Length || command[end] is ' ' or ';' or '&' or '|' or ')' or '"' or '\'') return true;
        }

        return false;
    }
}
