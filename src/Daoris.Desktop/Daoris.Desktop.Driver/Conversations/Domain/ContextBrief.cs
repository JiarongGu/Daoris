using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>One part of the line a brief is composed from (SESSCTX1b): its record, the agent it ran on, and its events as kept.</summary>
/// <param name="Session">The record's id.</param>
/// <param name="Agent">The adapter it ran on, as its record names it; null where it names none.</param>
/// <param name="Events">Its record's events, oldest first, as <see cref="SessionEvents"/> keeps them.</param>
public sealed record BriefPart(string Session, string? Agent, IReadOnlyList<SessionEvent> Events)
{
    /// <summary>Why it continued the part before it on the line, one of <see cref="HandOverWhy"/>; null for the line's first.</summary>
    public string? Why { get; init; }

    /// <summary>Where its kept attachments lie (<see cref="ChatFiles.Folder"/>), named beside each file the person attached.</summary>
    public string? Kept { get; init; }
}

/// <summary>The tree as read when a brief is composed (the session-context design §3.1 item 5), since a part's end commit is not recorded.</summary>
/// <param name="Head">The tree's head, its short id and subject; null where the tree could not be read.</param>
/// <param name="Uncommitted">What it holds uncommitted, git's short lines, relative to the tree.</param>
/// <param name="Commits">The commits on it since the line's first part began, newest first, short id and subject.</param>
public sealed record BriefTree(string? Head, IReadOnlyList<string> Uncommitted, IReadOnlyList<string> Commits);

/// <summary>What a brief is composed from: the line a new part continues, oldest part first, and what was read beside it.</summary>
/// <param name="Why">Why the session moves on now, one of <see cref="HandOverWhy"/>.</param>
/// <param name="Parts">The line, one chain back by each part's <c>from</c>: the part moved from last, never a part beside it.</param>
public sealed record BriefLine(string Why, IReadOnlyList<BriefPart> Parts)
{
    /// <summary>A conversation's line: its request is its first word, which item 3 does not tell again. A driven line's is its quest.</summary>
    public bool Conversation { get; init; }

    /// <summary>The tree the line works in, so a file a call touched is named relative to it; never said itself.</summary>
    public string? Tree { get; init; }

    /// <summary>The tree as read now; null where nothing was read, and the state says the record's alone.</summary>
    public BriefTree? State { get; init; }

    /// <summary>The gates the repository declares, and this machine's heavy-gate mapping for it: what makes a call a check.</summary>
    public IReadOnlyList<HeavyGate> Gates { get; init; } = [];

    /// <summary>
    /// The outgoing agent's own note (§3.1 item 6), as its claim: MIXAGENT1c's <c>handover_propose</c> once it exists, or the closing
    /// note SESSCTX1c asks for (§2.2). Null where none was written, and the brief says nothing of one.
    /// </summary>
    public string? Note { get; init; }

    /// <summary>The entry's other parts, beside the line, one line each (§3.1 item 7).</summary>
    public IReadOnlyList<string> Others { get; init; } = [];

    /// <summary>The questions the session asked that no word answered yet: a go-ahead, a helper's ask still open.</summary>
    public IReadOnlyList<string> Open { get; init; } = [];
}

/// <summary>A piece of a brief: the section it is counted in, and its words.</summary>
public readonly record struct BriefPiece(string Section, string Text);

/// <summary>A brief as composed: its pieces in the order they read, and its account, each section's size, source and cuts.</summary>
public sealed record ComposedBrief(IReadOnlyList<BriefPiece> Pieces, IReadOnlyList<HandedSection> Sections)
{
    /// <summary>The brief whole.</summary>
    public string Text => string.Concat(Pieces.Select(piece => piece.Text));

    /// <summary>The session it continues: the part moved from, the last of its line.</summary>
    public string? Continues { get; init; }

    /// <summary>
    /// Where the line's parts keep the files the person attached, which the brief names: the new part's rules read exactly these
    /// folders (the design §3.1 item 5).
    /// </summary>
    public IReadOnlyList<string> Kept { get; init; } = [];

    /// <summary>Its CONTEXT1 account (D143 point 1): its sections sum to its length, by construction.</summary>
    public InstructionAccount Account => new(Text.Length, Sections);
}

/// <summary>
/// The brief a new part is handed of the line it continues (SESSCTX1b, D189 points 4-5; the session-context design §3): a pure
/// function of that line's records, in Daoris's one English, composed with no model. It replaces <c>HandOverBriefing.Compose</c>
/// and a carry-on's four facts, so a hand-over, an account move, a carry-on and a fallback keep every word the session took.
/// </summary>
/// <remarks>
/// <para><b>What it carries</b> (§3.1): its opening, which says it is Daoris's record and not the agent's memory; the request as first
/// written; every word the session took, one per id as its latest event says, a word taken back left out, each marked by who said it as
/// the record marks it (the person's verbatim; another agent's findings and a channel's message as that agent's claim, told by their
/// marks, a channel's from before its mark by the claim's opening); each turn's last message and one line of what it did; the work's
/// state, the tree read now, the plan with the turn it was written in, the last check each part ran and the last failed one's kept
/// output, what is still asked and the files attached; the outgoing agent's note; the entry's other parts; what was left out.</para>
///
/// <para><b>What it never carries</b> (§3.3): a thought, a tool's output but the last failed check's, Daoris's own composed words
/// (an instruction, an earlier brief, its own words to a session), a summary any model wrote, or a machine path beyond where the
/// person's attachments lie: a file a call touched is named relative to the tree.</para>
///
/// <para><b>Its bound</b> (§3.2) is <see cref="Bound"/>, a Han character weighing <see cref="HanWeight"/>, both provisional until
/// SESSCTX1a counts briefs on each harness. Each section fills under its own cap and every item is clipped on its own, each cut said;
/// the words keep the newest under <see cref="WordsCap"/>, the turns the newest under what the rest leave, at least
/// <see cref="TurnsFloor"/>. The caps choose what is kept, not where it stands: the brief reads in the conversation's order.</para>
/// </remarks>
public static class ContextBrief
{
    /// <summary>The most a brief weighs: about 11,000 tokens at 3.6 characters a token, provisional (§3.2).</summary>
    public const int Bound = 40_000;

    /// <summary>What a Han character weighs toward the bound, provisional and on the safe side until measured (§3.2).</summary>
    public const int HanWeight = 3;

    /// <summary>The opening and the request (items 1-2).</summary>
    public const int OpeningCap = 4_000;

    /// <summary>The outgoing agent's note (item 6).</summary>
    public const int NoteCap = 4_000;

    /// <summary>The work's state (item 5).</summary>
    public const int StateCap = 4_000;

    /// <summary>The entry's other parts (item 7).</summary>
    public const int PartsCap = 1_000;

    /// <summary>The words the session took (item 3), the newest kept.</summary>
    public const int WordsCap = 12_000;

    /// <summary>The least the turns' outcomes keep (item 4): what the other caps leave them is never less.</summary>
    public const int TurnsFloor = 12_000;

    /// <summary>The most one item weighs: a word, the request, a turn's last message, the note.</summary>
    public const int ItemLimit = 4_000;

    /// <summary>The most one plan step weighs.</summary>
    public const int StepLimit = 200;

    /// <summary>The most entries of a list named: plan steps, what is uncommitted, the commits.</summary>
    public const int ListLimit = 50;

    /// <summary>The most of the last failed check's output carried: the record's own bound on it.</summary>
    public const int OutputLimit = SessionEventText.RawLimit;

    /// <summary>Room kept for what was left out (item 8).</summary>
    private const int NoticeRoom = 800;

    /// <summary>The most files a turn's line names, and failed commands; the rest counted.</summary>
    private const int Named = 8;

    /// <summary>What a text weighs toward the bound: a Han character <see cref="HanWeight"/>, every other one.</summary>
    public static int Weight(string text)
    {
        var weight = 0;
        foreach (var c in text) weight += IsHan(c) ? HanWeight : 1;
        return weight;
    }

    // The basic block, extension A and the compatibility block, as a search weighs a Han character (SessionEventText.Searchable).
    private static bool IsHan(char c) => c is (>= '一' and <= '鿿') or (>= '㐀' and <= '䶿') or (>= '豈' and <= '﫿');

    /// <summary>The brief of <paramref name="line"/>, in Daoris's one English, with its account.</summary>
    public static ComposedBrief Compose(BriefLine line)
    {
        var read = Read(line);
        var clipped = new Clips();

        // Item 1 and 2.
        var opening = Opening(line, read, clipped);
        // Items 5, 6 and 7 fill under their own caps.
        var (state, stateLeft, planLeft) = State(line, read, clipped);
        // In MIXAGENT1c's one wording for an outgoing agent's note (HandOverNote), clipped by weight first.
        var note = line.Note?.Trim() is { Length: > 0 } said && HandOverNote.Said(clipped.Head(said, NoteCap - 200)) is { } claim
            ? "\n\n" + claim
            : null;
        var (others, othersLeft) = Others(line.Others);

        // Item 3: the newest words under their cap, the oldest after the request left out first.
        var words = read.Turns.SelectMany(turn => turn.Words).ToList();
        foreach (var word in words)
        {
            word.Piece = $"\n\n  {word.Lead}\n\n"
                         + TextBounds.Quote(Clips.Clip(word.Text, ItemLimit, fromEnd: false, out var cut), indent: "  ", bareBlank: false);
            word.Clipped = cut;
        }

        var wordsUsed = 0;
        foreach (var word in Enumerable.Reverse(words))
        {
            var weight = Weight(word.Piece!);
            if (wordsUsed + weight > WordsCap) break;
            word.Kept = true;
            wordsUsed += weight;
        }

        // Item 4: the newest outcomes under what the rest leave.
        foreach (var turn in read.Turns) turn.Outcome = Outcome(turn, line.Tree);
        var fixedWeight = Weight(opening) + Weight(state ?? "") + Weight(note ?? "") + Weight(others ?? "") + Weight(Intro) + NoticeRoom;
        var budget = Math.Max(TurnsFloor, Bound - fixedWeight - wordsUsed) - Frames(read, line);
        var turnsUsed = 0;
        foreach (var turn in Enumerable.Reverse(read.Turns).Where(turn => turn.Outcome is not null))
        {
            var weight = Weight(turn.Outcome!);
            if (turnsUsed + weight > budget) break;
            turn.Kept = true;
            turnsUsed += weight;
        }

        // The bound holds whatever the frames came to: the oldest kept outcome, then the oldest kept word, goes until it fits.
        while (true)
        {
            var pieces = Pieces(line, read, opening, state, note, others, othersLeft, stateLeft, planLeft, clipped);
            if (Weight(string.Concat(pieces.Select(piece => piece.Text))) <= Bound || !DropOldest(read)) return Accounted(line, read, pieces, clipped, stateLeft, planLeft, othersLeft);
        }
    }

    /// <summary>The frames every kept item may need: a part's line and a turn's, counted for every one so the budget never overruns.</summary>
    private static int Frames(LineRead read, BriefLine line) =>
        read.Turns.Sum(turn => Weight(TurnHead(turn))) + line.Parts.Sum(part => Weight(PartHead(part)));

    private static bool DropOldest(LineRead read)
    {
        if (read.Turns.FirstOrDefault(turn => turn.Kept) is { } turn)
        {
            turn.Kept = false;
            return true;
        }

        if (read.Turns.SelectMany(each => each.Words).FirstOrDefault(word => word.Kept) is { } word)
        {
            word.Kept = false;
            return true;
        }

        return false;
    }

    // ——— Reading the line.

    private const string Intro =
        "\n\nWhat was said and done, in order, each word with the turn that took it. A word already answered is a past exchange, kept "
        + "with the turn that answered it: it is not an instruction to act on again.";

    private sealed class Word(string lead, string text, int part)
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

    private sealed class Call(string id, int at)
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

    private sealed class Turn(int part, int number)
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

    private sealed record Check(string Gate, string Title, string Status, string? Output, int Part);

    private sealed record LineRead(List<Turn> Turns, Word? Request, List<Check> Checks, (IReadOnlyList<PlanEntry> Steps, int Part, int Turn)? Plan);

    private static LineRead Read(BriefLine line)
    {
        var turns = new List<Turn>();
        var checks = new List<Check>();
        (IReadOnlyList<PlanEntry>, int, int)? plan = null;

        for (var p = 0; p < line.Parts.Count; p++)
        {
            var events = line.Parts[p].Events;

            // One word is one id, as its latest event says (§3.1 item 3): shown, told again for its level and taken, it counts once,
            // where it was taken; one taken back, or still waiting, was never taken, and follows the brief from `said` if it waits.
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
                    checks.Add(new Check(gate, Unrooted(call.Title ?? gate, line.Tree), call.Status ?? "pending", call.Output, p));
                }
            }
        }

        // A conversation's request is its first word (§3.1 item 2), never told again among the words.
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
        // instruction is composed afresh (§3.1 item 3).
        if (e.From == DaorisWords.From) return null;
        if (e.Opinion is { Length: > 0 } opinion) return new Word($"Another agent's findings (opinion `{opinion}`), as its claim:", e.Text ?? "", part);
        if (e.Channel is not null || (e.Text ?? "").StartsWith(ChannelText.ClaimOpening, StringComparison.Ordinal))
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
    /// The declared gate a command runs, by its command line as the repository declares it, never a guess from a title (§3.1 item 4);
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

        return Holds(said, "daoris-driver run-heavy") ? "run-heavy" : null;
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

    // ——— The sections.

    private static string Opening(BriefLine line, LineRead read, Clips clipped)
    {
        var last = line.Parts.Count > 0 ? line.Parts[^1] : null;
        var said = new StringBuilder();
        said.Append($"You continue session `{last?.Session ?? "?"}`, which ran on {last?.Agent ?? "its agent"}; it moved because {HandOverWhy.Briefed(line.Why)}.");
        if (line.Parts.Count > 1)
        {
            said.Append($" It continued {(line.Parts.Count == 2 ? "1 earlier part" : $"{line.Parts.Count - 1} earlier parts")} of the same line, named below.");
        }

        said.Append(" What follows is Daoris's record of that line of work, composed from what Daoris kept and without a model: it is not ")
            .Append("your own memory of it, nor the agent's. What is still to do is in the request, the last turn, the plan and the state of ")
            .Append("the work.");

        if (read.Request is { } request)
        {
            const string lead = "\n\nThe request, as the person first wrote it:\n\n";
            var room = OpeningCap - Weight(said.ToString()) - Weight(lead) - 80;
            said.Append(lead).Append(TextBounds.Quote(clipped.Head(request.Text, room), bareBlank: false));
        }

        return said.ToString();
    }

    private static string PartHead(BriefPart part) =>
        $"\n\nSession `{part.Session}`, on {part.Agent ?? "its agent"}"
        + (part.Why is { } why ? $", which went on from the one before because {HandOverWhy.Briefed(why)}:" : ":");

    private static string TurnHead(Turn turn) => $"\n\n- Turn {turn.Number}:" + (turn.Request ? "\n\n  The request, above." : "");

    /// <summary>A turn's outcome (§3.1 item 4): its last message, its end kept, and one line of what it did; null where it did nothing to say.</summary>
    private static string? Outcome(Turn turn, string? tree)
    {
        var last = turn.Last?.ToString().Trim() is { Length: > 0 } message ? message : null;
        var did = Did(turn, tree);
        var cut = false;
        var quoted = last is null ? null : TextBounds.Quote(Clips.Clip(last, ItemLimit, fromEnd: true, out cut), indent: "  ", bareBlank: false);
        turn.Clipped = cut;
        if (!turn.Ended)
        {
            // A part stopped mid-turn has no last message for the turn: its outcome is its tool lines (§3.1 item 4), and the word the
            // turn took may still be owed an answer.
            return "\n\n  Its turn did not end before the session moved"
                   + (quoted is null ? "." : "; its last message so far:\n\n" + quoted)
                   + (did is null ? "" : $"\n\n  {did}");
        }

        if (quoted is null && did is null) return null;
        return (quoted is null ? "" : "\n\n  It answered, in its last message of the turn:\n\n" + quoted) + (did is null ? "" : $"\n\n  {did}");
    }

    /// <summary>One line of what a turn did: the files it edited and the commands that failed, as its calls' kind, files and status say.</summary>
    private static string? Did(Turn turn, string? tree)
    {
        var edited = turn.Calls.Where(call => call.Kind is "edit" or "delete" or "move")
            .SelectMany(call => call.Locations).Select(path => Relative(path, tree)).Distinct(StringComparer.Ordinal).ToList();
        var failed = turn.Calls.Where(call => call is { Kind: "execute", Status: "failed" })
            .Select(call => TextBounds.Clip(TextBounds.OneLine(Unrooted(call.Title ?? call.Id, tree)), StepLimit)).ToList();
        var said = new List<string>();
        if (edited.Count > 0) said.Add($"It edited {Listed(edited)}");
        if (failed.Count > 0) said.Add($"{(failed.Count == 1 ? "a command failed" : "commands failed")}: {Listed(failed)}");
        if (said.Count == 0) return null;
        var line = string.Join("; ", said);
        return char.ToUpperInvariant(line[0]) + line[1..] + ".";
    }

    private static string Listed(IReadOnlyList<string> items)
    {
        var named = items.Take(Named).Select(item => $"`{item}`").ToList();
        var more = items.Count > Named ? $" and {items.Count - Named} more" : "";
        return named.Count == 1 ? named[0] + more
            : more.Length > 0 ? string.Join(", ", named) + more
            : $"{string.Join(", ", named.Take(named.Count - 1))} and {named[^1]}";
    }

    /// <summary>The work's state (§3.1 item 5) under its cap: lists shortened, the longest first, until it fits, each entry left out counted.</summary>
    private static (string? Text, int ListsLeft, int PlanLeft) State(BriefLine line, LineRead read, Clips clipped)
    {
        var tree = line.State;
        var uncommitted = tree?.Uncommitted.Select(entry => entry.Trim()).Where(entry => entry.Length > 0).ToList() ?? [];
        var commits = tree?.Commits.ToList() ?? [];
        var steps = read.Plan?.Steps.Select(step =>
            $"{(step.Status is { Length: > 0 } status ? $"[{status}] " : "")}{TextBounds.OneLine(clipped.Head(step.Content, StepLimit), trim: false)}").ToList() ?? [];
        var shown = new[] { Math.Min(uncommitted.Count, ListLimit), Math.Min(commits.Count, ListLimit), Math.Min(steps.Count, ListLimit) };

        // The rest of the state, each line once.
        var fixedLines = new List<string>();
        foreach (var part in read.Checks.GroupBy(check => check.Part))
        {
            var check = part.Last();
            fixedLines.Add($"\n- The last check session `{line.Parts[check.Part].Session}` ran: `{check.Gate}` ({TextBounds.Clip(TextBounds.OneLine(check.Title), StepLimit)}), {check.Status}.");
        }

        // The last check that failed and was not since run green carries its kept output (§3.1 item 4): what failed is the work's state.
        var newest = read.Checks.GroupBy(check => check.Gate, StringComparer.Ordinal).Select(runs => runs.Last()).ToList();
        if (read.Checks.LastOrDefault(check => check.Status == "failed" && newest.Any(run => ReferenceEquals(run, check))) is { } failedCheck)
        {
            fixedLines.Add(failedCheck.Output is { Length: > 0 } output
                ? $"\n- The last check that failed, `{failedCheck.Gate}`, printed:\n\n" + TextBounds.Quote(clipped.Tail(Unrooted(output.Trim(), line.Tree), OutputLimit), indent: "  ", bareBlank: false)
                : $"\n- The last check that failed, `{failedCheck.Gate}`, kept no output.");
        }

        fixedLines.AddRange(line.Open.Select(open => $"\n- Asked and not answered yet: {TextBounds.OneLine(open)}"));
        var files = read.Turns.SelectMany(turn => turn.Words).Prepend(read.Request).OfType<Word>()
            .SelectMany(word => word.Files.Select(file => (Name: file, Kept: line.Parts[word.Part].Kept))).Distinct().ToList();
        if (files.Count > 0)
        {
            fixedLines.Add("\n- Files the person attached: " + string.Join("; ", files.Select(file => file.Kept is { } kept ? $"`{file.Name}`, in {kept}" : $"`{file.Name}`")));
        }

        string Render()
        {
            var text = new StringBuilder();
            if (tree is not null)
            {
                text.Append(tree.Head is { } head ? $"\n- The tree's head, read now: {TextBounds.OneLine(head)}" : "\n- The tree could not be read as this brief was composed.");
                if (tree.Head is not null)
                {
                    text.Append(uncommitted.Count == 0 ? "\n- Nothing is uncommitted in the tree." : "\n- Uncommitted in the tree:" + List(uncommitted, shown[0]));
                    if (commits.Count > 0) text.Append("\n- Commits on this line since it began:" + List(commits, shown[1]));
                }
            }

            if (read.Plan is { } plan && steps.Count > 0)
            {
                text.Append($"\n- The plan, as last kept, written in turn {plan.Turn} of session `{line.Parts[plan.Part].Session}`:" + List(steps, shown[2]));
            }

            foreach (var each in fixedLines) text.Append(each);
            return text.Length == 0 ? "" : "\n\nThe state of the work, as Daoris read it when it composed this brief:\n" + text;
        }

        var rendered = Render();
        while (Weight(rendered) > StateCap && shown.Any(count => count > 0))
        {
            var longest = Array.IndexOf(shown, shown.Max());
            shown[longest] /= 2;
            rendered = Render();
        }

        if (Weight(rendered) > StateCap) rendered = clipped.Head(rendered, StateCap);
        var listsLeft = uncommitted.Count - shown[0] + commits.Count - shown[1];
        return (rendered.Length == 0 ? null : rendered, listsLeft, steps.Count - shown[2]);
    }

    private static string List(IReadOnlyList<string> entries, int shown) =>
        string.Concat(entries.Take(shown).Select(entry => $"\n    - {entry}")) + (entries.Count > shown ? $"\n    - … and {entries.Count - shown} more" : "");

    private static (string? Text, int Left) Others(IReadOnlyList<string> others)
    {
        if (others.Count == 0) return (null, 0);
        var text = new StringBuilder("\n\nThe session's other parts, beside this line, one line each:\n");
        var shown = 0;
        foreach (var other in others)
        {
            var row = $"\n- {TextBounds.OneLine(other)}";
            if (Weight(text.ToString()) + Weight(row) > PartsCap - 40) break;
            text.Append(row);
            shown++;
        }

        if (shown < others.Count) text.Append($"\n- … and {others.Count - shown} more");
        return (text.ToString(), others.Count - shown);
    }

    // ——— The brief, piece by piece, and its account.

    private static List<BriefPiece> Pieces(
        BriefLine line, LineRead read, string opening, string? state, string? note, string? others, int othersLeft, int stateLeft, int planLeft,
        Clips clipped)
    {
        var pieces = new List<BriefPiece> { new(HandedSections.BriefOpening, opening) };
        var exchange = read.Turns.Where(turn => turn.Kept || turn.Words.Any(word => word.Kept)).ToList();
        if (exchange.Count > 0)
        {
            pieces.Add(new(HandedSections.BriefTurns, Intro));
            var part = -1;
            foreach (var turn in exchange)
            {
                if (turn.Part != part)
                {
                    part = turn.Part;
                    pieces.Add(new(HandedSections.BriefTurns, PartHead(line.Parts[part])));
                }

                pieces.Add(new(HandedSections.BriefTurns, TurnHead(turn)));
                pieces.AddRange(turn.Words.Where(word => word.Kept).Select(word => new BriefPiece(HandedSections.BriefWords, word.Piece!)));
                if (turn.Kept) pieces.Add(new(HandedSections.BriefTurns, turn.Outcome!));
            }
        }

        if (state is not null) pieces.Add(new(HandedSections.BriefState, state));
        if (note is not null) pieces.Add(new(HandedSections.BriefNote, note));
        if (others is not null) pieces.Add(new(HandedSections.BriefParts, others));

        var (wordsLeft, turnsLeft) = Left(read);
        var left = new List<string>();
        if (wordsLeft > 0) left.Add($"{wordsLeft} of the words the session took, the oldest after the request");
        if (turnsLeft > 0) left.Add($"{turnsLeft} {(turnsLeft == 1 ? "turn's outcome" : "turns' outcomes")}, the oldest");
        if (othersLeft > 0) left.Add($"{othersLeft} of the session's other parts");
        if (stateLeft + planLeft > 0) left.Add($"{stateLeft + planLeft} entries of the work's lists");
        var clips = ClipCount(read, clipped);
        if (clips > 0) left.Add($"part of {clips} {(clips == 1 ? "item" : "items")} each clipped at its own bound");
        if (left.Count > 0)
        {
            // The connector's `line_read` reads what was left out (SESSCTX1f, the design §3.5), so the gap is one call away, not a guess.
            pieces.Add(new(
                HandedSections.BriefLeft,
                $"\n\nLeft out to keep this brief bounded: {InstructionAccounting.Joined(left)}. Daoris's record of this line holds every one of them, "
                + "and `line_read` on Daoris's connector reads it a page at a time."));
        }

        return pieces;
    }

    /// <summary>The items clipped at their own bound that the brief holds: the opening's, the state's and the note's, and each kept word and turn.</summary>
    private static int ClipCount(LineRead read, Clips clipped) =>
        clipped.Count
        + read.Turns.SelectMany(turn => turn.Words).Count(word => word is { Kept: true, Clipped: true })
        + read.Turns.Count(turn => turn is { Kept: true, Clipped: true });

    private static (int Words, int Turns) Left(LineRead read) =>
        (read.Turns.SelectMany(turn => turn.Words).Count(word => !word.Kept),
         read.Turns.Count(turn => turn.Outcome is not null && !turn.Kept));

    private static ComposedBrief Accounted(
        BriefLine line, LineRead read, List<BriefPiece> pieces, Clips clipped, int stateLeft, int planLeft, int othersLeft)
    {
        var chars = pieces.GroupBy(piece => piece.Section, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.Sum(piece => piece.Text.Length));
        var order = pieces.Select(piece => piece.Section).Distinct(StringComparer.Ordinal).ToList();
        var words = read.Turns.SelectMany(turn => turn.Words).ToList();
        var outcomes = read.Turns.Count(turn => turn.Outcome is not null);
        var (wordsLeft, turnsLeft) = Left(read);
        var last = line.Parts.Count > 0 ? line.Parts[^1] : null;

        HandedSection Section(string name)
        {
            var size = $"{InstructionAccounting.N(chars[name])} characters";
            return name switch
            {
                HandedSections.BriefOpening => new HandedSection(
                    name, HandedSources.Record,
                    $"the brief's opening: {size}, session `{last?.Session}` on {last?.Agent ?? "its agent"}, moved because {HandOverWhy.Briefed(line.Why)}"
                    + (read.Request is null ? "" : ", with the request as first written"))
                {
                    From = last?.Session,
                },
                HandedSections.BriefWords => new HandedSection(
                    name, HandedSources.Record, $"the words the session took: {size}: {words.Count - wordsLeft} of {words.Count}, each marked by who said it")
                {
                    Shown = words.Count - wordsLeft,
                    Of = words.Count,
                    Cuts = wordsLeft == 0 ? null :
                    [
                        new HandedCut(HandedCuts.BriefWordsOlder, $"{wordsLeft} older ones left out past its bound of {WordsCap}") { Count = wordsLeft, Limit = WordsCap },
                    ],
                },
                HandedSections.BriefTurns => new HandedSection(
                    name, HandedSources.Record, $"each turn's outcome: {size}: {outcomes - turnsLeft} of {outcomes}, the newest kept")
                {
                    Shown = outcomes - turnsLeft,
                    Of = outcomes,
                    Cuts = turnsLeft == 0 ? null :
                    [
                        new HandedCut(HandedCuts.BriefTurnsOlder, $"{turnsLeft} older ones left out past what the bound of {Bound} leaves them")
                        {
                            Count = turnsLeft, Limit = Bound,
                        },
                    ],
                },
                HandedSections.BriefState => new HandedSection(
                    name, line.State is null ? HandedSources.Record : HandedSources.Tree,
                    $"the work's state: {size}: " + (line.State is null ? "the record's plan, checks and files" : "the tree read now, the plan, the checks and the files"))
                {
                    Cuts = Cuts(
                        planLeft > 0 ? new HandedCut(HandedCuts.PlanBound, $"{planLeft} plan steps left out past its bound of {ListLimit}") { Count = planLeft, Limit = ListLimit } : null,
                        stateLeft > 0 ? new HandedCut(HandedCuts.BriefList, $"{stateLeft} entries of its lists left out past their bound") { Count = stateLeft } : null),
                },
                HandedSections.BriefNote => new HandedSection(name, HandedSources.Record, $"the outgoing agent's note: {size}, as its claim"),
                HandedSections.BriefParts => new HandedSection(name, HandedSources.Record, $"the session's other parts: {size}: {line.Others.Count - othersLeft} of {line.Others.Count}")
                {
                    Shown = line.Others.Count - othersLeft,
                    Of = line.Others.Count,
                    Cuts = othersLeft == 0 ? null :
                    [
                        new HandedCut(HandedCuts.BriefPartsBound, $"{othersLeft} left out past its bound of {PartsCap} characters") { Count = othersLeft, Limit = PartsCap },
                    ],
                },
                _ => new HandedSection(name, HandedSources.DriverWords, $"what the brief left out: {size}, the driver's own words")
                {
                    Cuts = ClipCount(read, clipped) is var clips && clips == 0 ? null :
                    [
                        new HandedCut(HandedCuts.BriefClipped, $"{clips} items each clipped at its own bound") { Count = clips },
                    ],
                },
            } with { Chars = chars[name] };
        }

        return new ComposedBrief(pieces, [.. order.Select(Section)])
        {
            Continues = last?.Session,
            Kept = [.. line.Parts.Select(part => part.Kept).OfType<string>().Distinct(StringComparer.OrdinalIgnoreCase)],
        };
    }

    private static IReadOnlyList<HandedCut>? Cuts(params HandedCut?[] cuts) => cuts.OfType<HandedCut>().ToList() is { Count: > 0 } kept ? kept : null;

    // ——— Paths, never a machine's.

    private static readonly Regex Rooted = new(@"^([A-Za-z]:[\\/]|[\\/])", RegexOptions.CultureInvariant);

    /// <summary>A file a call touched, relative to the tree; one outside it by its name alone, never a machine path (§3.3).</summary>
    private static string Relative(string path, string? tree)
    {
        var file = path.Replace('\\', '/');
        if (tree is { Length: > 0 })
        {
            var root = tree.Replace('\\', '/').TrimEnd('/') + "/";
            if (file.StartsWith(root, StringComparison.OrdinalIgnoreCase)) return file[root.Length..];
        }

        return Rooted.IsMatch(file) ? Path.GetFileName(file) : file;
    }

    /// <summary>A call's title or output with the tree's own folder taken out, so it names files as the tree holds them.</summary>
    private static string Unrooted(string text, string? tree)
    {
        if (tree is not { Length: > 0 }) return text;
        var forward = tree.Replace('\\', '/').TrimEnd('/');
        var back = forward.Replace('/', '\\');
        return text.Replace(forward + "/", "", StringComparison.OrdinalIgnoreCase).Replace(back + "\\", "", StringComparison.OrdinalIgnoreCase)
            .Replace(forward, ".", StringComparison.OrdinalIgnoreCase).Replace(back, ".", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Every item clipped at its own bound, each cut said where it is (§3.2): the opening's, the state's and the note's counted here,
    /// once each; a word's and a turn's by their own mark, counted only where the bound kept them.
    /// </summary>
    private sealed class Clips
    {
        public int Count { get; private set; }

        /// <summary>The start of a text, at most <paramref name="cap"/> weighed, saying how much was left out.</summary>
        public string Head(string text, int cap)
        {
            var kept = Clip(text, cap, fromEnd: false, out var cut);
            if (cut) Count++;
            return kept;
        }

        /// <summary>The end of a text, at most <paramref name="cap"/> weighed, saying how much before it was left out.</summary>
        public string Tail(string text, int cap)
        {
            var kept = Clip(text, cap, fromEnd: true, out var cut);
            if (cut) Count++;
            return kept;
        }

        /// <summary>A text within <paramref name="cap"/>, its start or its end kept, and the cut said where it is.</summary>
        public static string Clip(string text, int cap, bool fromEnd, out bool cut)
        {
            cut = Weight(text) > cap;
            if (!cut) return text;
            var kept = Take(text, Math.Max(0, cap - 60), fromEnd);
            return fromEnd
                ? $"… ({text.Length - kept.Length} earlier characters left out here)\n{kept}"
                : $"{kept}\n… ({text.Length - kept.Length} more characters left out here)";
        }

        private static string Take(string text, int cap, bool fromEnd)
        {
            var weight = 0;
            var count = 0;
            for (var i = 0; i < text.Length; i++)
            {
                var c = text[fromEnd ? text.Length - 1 - i : i];
                weight += IsHan(c) ? HanWeight : 1;
                if (weight > cap) break;
                count++;
            }

            // Never half a surrogate pair.
            if (count > 0 && count < text.Length)
            {
                var edge = fromEnd ? text[text.Length - count] : text[count - 1];
                if (fromEnd ? char.IsLowSurrogate(edge) : char.IsHighSurrogate(edge)) count--;
            }

            return fromEnd ? text[^count..] : text[..count];
        }
    }
}
