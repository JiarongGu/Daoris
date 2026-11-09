using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using static Daoris.Driver.BriefReading;

namespace Daoris.Driver;

/// <summary>
/// The brief a new part is handed of the line it continues: a pure
/// function of that line's records, in Daoris's one English, composed with no model. It replaces <c>HandOverBriefing.Compose</c>
/// and a carry-on's four facts, so a hand-over, an account move, a carry-on and a fallback keep every word the session took.
/// </summary>
/// <remarks>
/// <para><b>What it carries</b>: its opening, which says it is Daoris's record and not the agent's memory; the request as first
/// written; every word the session took, one per id as its latest event says, a word taken back left out, each marked by who said it as
/// the record marks it (the person's verbatim; another agent's findings and a channel's message as that agent's claim, told by their
/// marks, a channel's from before its mark by the claim's opening); each turn's last message and one line of what it did; the work's
/// state, the tree read now, the plan with the turn it is written in, the last check each part ran and the last failed one's kept
/// output, what is still asked and the files attached; the line's course as Daoris counts it;
/// the outgoing agent's note; the entry's other parts; what is left out.</para>
///
/// <para><b>What it never carries</b>: a thought, a tool's output but the last failed check's, Daoris's own composed words
/// (an instruction, an earlier brief, its own words to a session), a summary any model wrote, or a machine path beyond where the
/// person's attachments lie: a file a call touched is named relative to the tree.</para>
///
/// <para><b>Its bound</b> is <see cref="Bound"/>, a wide character weighing <see cref="WideWeight"/>, both provisional until
/// briefs are counted on each harness. Each section fills under its own cap and every item is clipped on its own, each cut said;
/// the words keep the newest under <see cref="WordsCap"/>, the turns the newest under what the rest leave, at least
/// <see cref="TurnsFloor"/>. The caps choose what is kept, not where it stands: the brief reads in the conversation's order.</para>
/// </remarks>
public static class ContextBrief
{
    /// <summary>The most a brief weighs: about 11,000 tokens at 3.6 characters a token, provisional.</summary>
    public const int Bound = 40_000;

    /// <summary>What a Han, kana, Hangul or fullwidth character, or an emoji, weighs toward the bound, provisional and on the safe side until measured.</summary>
    public const int WideWeight = 3;

    /// <summary>The opening and the request.</summary>
    public const int OpeningCap = 4_000;

    /// <summary>The outgoing agent's note.</summary>
    public const int NoteCap = 4_000;

    /// <summary>
    /// How a channel's claim opens: the page's <c>CHANNEL_CLAIM_OPENING</c>, and what a brief tells a claim by on a record from before the
    /// event's own mark (<see cref="SessionEvent.Channel"/>). The work orders' <c>ChannelText.ClaimOpening</c> is this.
    /// </summary>
    internal const string ChannelClaimOpening = "Another session says, on the ask's channel";

    /// <summary>The work's state.</summary>
    public const int StateCap = 4_000;

    /// <summary>The entry's other parts.</summary>
    public const int PartsCap = 1_000;

    /// <summary>The line's course as Daoris counts it.</summary>
    public const int CourseCap = 1_000;

    /// <summary>The words the session took, the newest kept.</summary>
    public const int WordsCap = 12_000;

    /// <summary>The least the turns' outcomes keep: what the other caps leave them is never less.</summary>
    public const int TurnsFloor = 12_000;

    /// <summary>The most one item weighs: a word, the request, a turn's last message, the note.</summary>
    public const int ItemLimit = 4_000;

    /// <summary>The most one plan step weighs.</summary>
    public const int StepLimit = 200;

    /// <summary>The most entries of a list named: plan steps, what is uncommitted, the commits.</summary>
    public const int ListLimit = 50;

    /// <summary>The most things the line waits on that the state names, the oldest; the rest are counted.</summary>
    public const int WaitLimit = 8;

    /// <summary>The most of the last failed check's output carried: the record's own bound on it.</summary>
    public const int OutputLimit = SessionEventText.RawLimit;

    /// <summary>Room kept for what is left out.</summary>
    private const int NoticeRoom = 800;

    /// <summary>The most files a turn's line names, and failed commands; the rest counted.</summary>
    private const int Named = 8;

    /// <summary>The most files read, and pages opened, a turn's line names: the rest are counted.</summary>
    private const int ReadsNamed = 20;

    /// <summary>What a text weighs toward the bound: a wide character <see cref="WideWeight"/>, every other one.</summary>
    public static int Weight(string text)
    {
        var weight = 0;
        foreach (var c in text) weight += CharWeight(c);
        return weight;
    }

    // Han, CJK punctuation with kana, Jamo, Hangul and the fullwidth forms each weigh WideWeight; a surrogate pair (emoji, the rarer Han) as
    // its two halves, 2 and 1. Search's own Han test (SessionEventText.Searchable) answers another question and stays as it is.
    private static int CharWeight(char c) => c switch
    {
        (>= '一' and <= '鿿') or (>= '㐀' and <= '䶿') or (>= '豈' and <= '﫿') or (>= '　' and <= 'ヿ')
            or (>= 'ᄀ' and <= 'ᇿ') or (>= '㄰' and <= '㆏') or (>= '가' and <= '힯') or (>= '＀' and <= '￯') => WideWeight,
        _ when char.IsHighSurrogate(c) => WideWeight - 1,
        _ => 1,
    };

    /// <summary>The brief of <paramref name="line"/>, in Daoris's one English, with its account.</summary>
    public static ComposedBrief Compose(BriefLine line)
    {
        var read = Read(line);
        var clipped = new Clips();

        var opening = Opening(line, read, clipped);
        // Items 5, 6 and 7 fill under their own caps.
        var (state, stateLeft, planLeft) = State(line, read, clipped);
        // The outgoing agent's note in its one wording (OutgoingNote), clipped by weight first.
        var note = line.Note?.Trim() is { Length: > 0 } said && OutgoingNote.Said(clipped.QuotedHead(said, NoteCap - 200)) is { } claim
            ? "\n\n" + claim
            // A closing note asked and not written in its one turn is said absent, in the note's own section.
            : line.NoteAsked ? "\n\n" + OutgoingNote.Absent : null;
        var (others, othersLeft) = Others(line.Others);
        var course = Course(line.Counts);

        // The newest words under their cap, the oldest after the request left out first.
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

        // The newest outcomes under what the rest leave.
        foreach (var turn in read.Turns) turn.Outcome = Outcome(turn, line.Tree);
        var fixedWeight = Weight(opening) + Weight(state ?? "") + Weight(course.Text ?? "") + Weight(note ?? "") + Weight(others ?? "") + Weight(Intro)
                          + NoticeRoom;
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
            var pieces = Pieces(line, read, opening, state, course.Text, note, others, othersLeft, stateLeft, planLeft, clipped);
            if (Weight(string.Concat(pieces.Select(piece => piece.Text))) <= Bound || !DropOldest(read))
            {
                return Accounted(line, read, pieces, clipped, stateLeft, planLeft, othersLeft, course);
            }
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

    private static LineRead Read(BriefLine line) => BriefReading.Read(line);

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
            said.Append(lead).Append(TextBounds.Quote(clipped.QuotedHead(request.Text, room), bareBlank: false));
        }

        return said.ToString();
    }

    private static string PartHead(BriefPart part) =>
        $"\n\nSession `{part.Session}`, on {part.Agent ?? "its agent"}"
        + (part.Why is { } why ? $", which went on from the one before because {HandOverWhy.Briefed(why)}:" : ":");

    private static string TurnHead(Turn turn) => $"\n\n- Turn {turn.Number}:" + (turn.Request ? "\n\n  The request, above." : "");

    /// <summary>A turn's outcome: its last message, its end kept, and one line of what it did; null where it did nothing to say.</summary>
    private static string? Outcome(Turn turn, string? tree)
    {
        var last = turn.Last?.ToString().Trim() is { Length: > 0 } message ? message : null;
        var did = Did(turn, tree);
        var cut = false;
        var quoted = last is null ? null : TextBounds.Quote(Clips.Clip(last, ItemLimit, fromEnd: true, out cut), indent: "  ", bareBlank: false);
        turn.Clipped = cut;
        if (!turn.Ended)
        {
            // A part stopped mid-turn has no last message for the turn: its outcome is its tool lines, and the word the
            // turn took may still be owed an answer.
            return "\n\n  Its turn did not end before the session moved"
                   + (quoted is null ? "." : "; its last message so far:\n\n" + quoted)
                   + (did is null ? "" : $"\n\n  {did}");
        }

        if (quoted is null && did is null) return null;
        return (quoted is null ? "" : "\n\n  It answered, in its last message of the turn:\n\n" + quoted) + (did is null ? "" : $"\n\n  {did}");
    }

    /// <summary>
    /// One line of what a turn did: the files it edited, the files it read or searched and the pages it opened (names only, never what
    /// they held), and the commands that failed, as its calls' kind, files, inputs and status say.
    /// </summary>
    private static string? Did(Turn turn, string? tree)
    {
        var edited = turn.Calls.Where(call => call.Kind is "edit" or "delete" or "move")
            .SelectMany(call => call.Locations).Select(path => Relative(path, tree)).Distinct(StringComparer.Ordinal).ToList();
        var read = turn.Calls.Where(call => call.Kind is "read" or "search")
            .SelectMany(call => call.Locations.Count > 0 ? call.Locations : PathsOf(call.Input)).Select(path => Relative(path, tree))
            .Where(path => path.Length > 0).Distinct(StringComparer.Ordinal).Except(edited, StringComparer.Ordinal)
            .Select(path => TextBounds.Clip(path, StepLimit)).ToList();
        var opened = turn.Calls.Where(call => call.Kind is "fetch" || (call.Kind is "other" && call.Title is { } title && Navigates.IsMatch(title)))
            .Select(call => PageOf(call.Input, tree)).OfType<string>().Distinct(StringComparer.Ordinal).Select(page => TextBounds.Clip(page, StepLimit)).ToList();
        var failed = turn.Calls.Where(call => call is { Kind: "execute", Status: "failed" })
            .Select(call => TextBounds.Clip(TextBounds.OneLine(Unrooted(call.Title ?? call.Id, tree)), StepLimit)).ToList();
        var said = new List<string>();
        if (edited.Count > 0) said.Add($"It edited {Listed(edited)}");
        if (read.Count > 0) said.Add($"it read {Listed(read, ReadsNamed)}");
        if (opened.Count > 0) said.Add($"it opened {Listed(opened, ReadsNamed)}");
        if (failed.Count > 0) said.Add($"{(failed.Count == 1 ? "a command failed" : "commands failed")}: {Listed(failed)}");
        if (said.Count == 0) return null;
        var line = string.Join("; ", said);
        return char.ToUpperInvariant(line[0]) + line[1..] + ".";
    }

    private static readonly Regex Navigates = new("navigat|goto|open_?url|open_?page|preview_start", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    /// <summary>The path a read or search call's input names: its file, notebook or folder; none where the input names none.</summary>
    private static IEnumerable<string> PathsOf(string? input)
    {
        if (InputObject(input) is not { } root) yield break;
        foreach (var name in new[] { "file_path", "notebook_path", "path" })
        {
            if (root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String && value.GetString() is { Length: > 0 } path)
            {
                yield return path;
                yield break;
            }
        }
    }

    /// <summary>The page address a call's input names, without its user, query or fragment; null where it names no http(s) or file address.</summary>
    private static string? PageOf(string? input, string? tree)
    {
        if (InputObject(input) is not { } root || !root.TryGetProperty("url", out var value) || value.ValueKind != JsonValueKind.String) return null;
        if (!Uri.TryCreate(value.GetString()?.Trim(), UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https" or "file")) return null;
        return uri.Scheme == "file" ? Relative(uri.LocalPath, tree) : uri.GetComponents(UriComponents.SchemeAndServer | UriComponents.Path, UriFormat.UriEscaped);
    }

    private static JsonElement? InputObject(string? input)
    {
        if (input is not { Length: > 0 }) return null;
        try
        {
            using var document = JsonDocument.Parse(input);
            return document.RootElement.ValueKind == JsonValueKind.Object ? document.RootElement.Clone() : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string Listed(IReadOnlyList<string> items, int limit = Named)
    {
        var named = items.Take(limit).Select(item => $"`{item}`").ToList();
        var more = items.Count > limit ? $" and {items.Count - limit} more" : "";
        return named.Count == 1 ? named[0] + more
            : more.Length > 0 ? string.Join(", ", named) + more
            : $"{string.Join(", ", named.Take(named.Count - 1))} and {named[^1]}";
    }

    /// <summary>The work's state under its cap: lists shortened, the longest first, until it fits, each entry left out counted.</summary>
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

        // The last check that failed and is not since run green carries its kept output: what failed is the work's state.
        var newest = read.Checks.GroupBy(check => check.Gate, StringComparer.Ordinal).Select(runs => runs.Last()).ToList();
        if (read.Checks.LastOrDefault(check => check.Status == "failed" && newest.Any(run => ReferenceEquals(run, check))) is { } failedCheck)
        {
            fixedLines.Add(failedCheck.Output is { Length: > 0 } output
                ? $"\n- The last check that failed, `{failedCheck.Gate}`, printed:\n\n" + TextBounds.Quote(clipped.Tail(Unrooted(output.Trim(), line.Tree), OutputLimit), indent: "  ", bareBlank: false)
                : $"\n- The last check that failed, `{failedCheck.Gate}`, kept no output.");
        }

        fixedLines.AddRange(line.Open.Take(WaitLimit).Select(WaitText));
        var waitsLeft = Math.Max(0, line.Open.Count - WaitLimit);
        if (waitsLeft > 0) fixedLines.Add($"\n- … and {waitsLeft} more things this line waits on.");
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
        var listsLeft = uncommitted.Count - shown[0] + commits.Count - shown[1] + Math.Min(steps.Count, ListLimit) - shown[2];
        return (rendered.Length == 0 ? null : rendered, listsLeft, Math.Max(0, steps.Count - ListLimit));
    }

    private static string List(IReadOnlyList<string> entries, int shown) =>
        string.Concat(entries.Take(shown).Select(entry => $"\n    - {entry}")) + (entries.Count > shown ? $"\n    - … and {entries.Count - shown} more" : "");

    private static string WaitText(BriefWait wait)
    {
        var delivery = wait.Outcome == "answered"
            ? "answered, and the answer is kept, not yet handed"
            : wait.Delivery switch
            {
                "kept" => "kept by Daoris, not yet taken up",
                "attempted" => "tried, not yet confirmed",
                "sent" => "sent, not yet taken up",
                "taken" => "taken up, not yet answered",
                "uncertain" => "sent once and not known to have arrived, so it waits for the person",
                "not-reached" => "did not reach its receiver",
                "elsewhere" => "waiting on another repository",
                _ => wait.Delivery,
            };
        var detail = wait.Detail is { Length: > 0 } text ? $": {TextBounds.Clip(TextBounds.OneLine(text), StepLimit)}" : "";
        return $"\n- What this line waits on: `{wait.Id}`, asked by session `{wait.Owner}` of {TextBounds.OneLine(wait.To)}; {delivery}. "
               + $"Its answer comes to this line{detail}";
    }

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

    /// <summary>
    /// The line's course as Daoris counts it under <see cref="CourseCap"/>: one
    /// line per count that is not zero or that Daoris acts on, as facts; those past the cap counted. None where nothing is said.
    /// </summary>
    private static (string? Text, int Shown, int Of) Course(IReadOnlyList<CountFact> counts)
    {
        var lines = CountTexts.Lines(counts);
        if (lines.Count == 0) return (null, 0, 0);
        var text = new StringBuilder($"\n\n{CountTexts.Heading}:");
        var shown = 0;
        foreach (var each in lines)
        {
            var row = $"\n- {each}";
            if (Weight(text.ToString()) + Weight(row) > CourseCap - 40) break;
            text.Append(row);
            shown++;
        }

        if (shown < lines.Count) text.Append($"\n- … and {lines.Count - shown} more counts");
        return (text.ToString(), shown, lines.Count);
    }

    private static HandedSection CourseSection(int chars, int shown, int of) => new(
        HandedSections.BriefCourse, HandedSources.Record,
        $"the line's course: {InstructionAccounting.N(chars)} characters: {shown} of {of} counts, as Daoris counts them")
    {
        Chars = chars,
        Shown = shown,
        Of = of,
        Cuts = shown == of ? null :
        [
            new HandedCut(HandedCuts.BriefCourseBound, $"{of - shown} left out past its bound of {CourseCap} characters") { Count = of - shown, Limit = CourseCap },
        ],
    };

    /// <summary>
    /// What a start whose line holds no brief carries of its course: the brief's item, accounted as the brief accounts it, where
    /// any count is not zero, as on a quest that failed here before; null where none is.
    /// </summary>
    public static ComposedBrief? Counted(IReadOnlyList<CountFact> counts)
    {
        if (!counts.Any(count => count.N > 0)) return null;
        var (text, shown, of) = Course(counts);
        return text is null ? null : new ComposedBrief([new BriefPiece(HandedSections.BriefCourse, text)], [CourseSection(text.Length, shown, of)]);
    }

    // ——— The brief, piece by piece, and its account.

    private static List<BriefPiece> Pieces(
        BriefLine line, LineRead read, string opening, string? state, string? course, string? note, string? others, int othersLeft, int stateLeft,
        int planLeft, Clips clipped)
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
        if (course is not null) pieces.Add(new(HandedSections.BriefCourse, course));
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
            // The connector's `line_read` reads what is left out, so the gap is one call away, not a guess.
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
        BriefLine line, LineRead read, List<BriefPiece> pieces, Clips clipped, int stateLeft, int planLeft, int othersLeft,
        (string? Text, int Shown, int Of) course)
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
                HandedSections.BriefCourse => CourseSection(chars[name], course.Shown, course.Of),
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
            Memory = line.Memory,
        };
    }

    private static IReadOnlyList<HandedCut>? Cuts(params HandedCut?[] cuts) => cuts.OfType<HandedCut>().ToList() is { Count: > 0 } kept ? kept : null;

    // ——— Paths, never a machine's.

    private static readonly Regex Rooted = new(@"^([A-Za-z]:[\\/]|[\\/])", RegexOptions.CultureInvariant);

    /// <summary>A file a call touched, relative to the tree; one outside it by its name alone, never a machine path.</summary>
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
    internal static string Unrooted(string text, string? tree)
    {
        if (tree is not { Length: > 0 }) return text;
        var forward = tree.Replace('\\', '/').TrimEnd('/');
        var back = forward.Replace('/', '\\');
        return text.Replace(forward + "/", "", StringComparison.OrdinalIgnoreCase).Replace(back + "\\", "", StringComparison.OrdinalIgnoreCase)
            .Replace(forward, ".", StringComparison.OrdinalIgnoreCase).Replace(back, ".", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Every item clipped at its own bound, each cut said where it is: the opening's, the state's and the note's counted here,
    /// once each; a word's and a turn's by their own mark, counted only where the bound kept them.
    /// </summary>
    private sealed class Clips
    {
        public int Count { get; private set; }

        /// <summary>The start of a text, at most <paramref name="cap"/> weighed, saying how much is left out.</summary>
        public string Head(string text, int cap)
        {
            var kept = Clip(text, cap, fromEnd: false, out var cut);
            if (cut) Count++;
            return kept;
        }

        /// <summary>The start of a text, at most <paramref name="cap"/> weighed once quoted, since a quote leads each line with <c>&gt; </c>.</summary>
        public string QuotedHead(string text, int cap)
        {
            var inner = cap;
            string kept;
            bool cut;
            for (var tries = 0; ; tries++)
            {
                kept = Clip(text, inner, fromEnd: false, out cut);
                var over = Weight(TextBounds.Quote(kept, bareBlank: false)) - cap;
                if (over <= 0 || inner <= 0 || tries == 8) break;
                inner = Math.Max(0, inner - over);
            }

            if (cut) Count++;
            return kept;
        }

        /// <summary>The end of a text, at most <paramref name="cap"/> weighed, saying how much before it is left out.</summary>
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
                weight += CharWeight(c);
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
