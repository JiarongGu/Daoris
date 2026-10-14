using System.Text;

namespace Daoris.Driver;

/// <summary>
/// One earlier part of the session a further part continues (SESSONE1d, D167 point 4 as amended), as its record and this machine's
/// trees say it: what it worked on and how it stands, and where its work is.
/// </summary>
/// <param name="Session">Its record's id.</param>
/// <param name="Quest">The quest it served.</param>
/// <param name="State">Its record's state, in its public spelling.</param>
public sealed record EarlierPart(string Session, string? Quest, string State)
{
    /// <summary>What its record says of how it stands (its note), or null where it says nothing.</summary>
    public string? Note { get; init; }

    /// <summary>Its tree's name under this machine's trees, where that tree still stands; null where it is gone or ran in the root.</summary>
    public string? Tree { get; init; }

    /// <summary>The branch its work is on: its tree's, or, where its tree is gone, the one its work landed on; null for neither.</summary>
    public string? Branch { get; init; }
}

/// <summary>
/// What a further part of a session is handed of the parts before it (SESSONE1d; the co-working design §1.5): each one's record,
/// newest first, and how its own tree stands to theirs. Agent-neutral facts, never a transcript (design §5).
/// </summary>
/// <param name="Earlier">The earlier parts on this machine, newest first.</param>
public sealed record SessionParts(IReadOnlyList<EarlierPart> Earlier)
{
    /// <summary>The part whose tree this one went into, free (D51); null where it is in a tree of its own.</summary>
    public string? Into { get; init; }

    /// <summary>The live part holding the session's tree, so this one runs beside it; null where none does.</summary>
    public string? HeldBy { get; init; }

    /// <summary>The part whose tree this one would have gone into, gone since (after a landing), so its tree grew again.</summary>
    public string? Gone { get; init; }

    /// <summary>Where <see cref="Gone"/> is set: the branch its tree grew from, the one that work landed on (D82); null for the line.</summary>
    public string? GrewFrom { get; init; }
}

/// <summary>
/// What a further part of a session reads of the parts before it (SESSONE1d), beneath the quest on a first start: the newest
/// <see cref="TargetPrompt.PartsShown"/> by name, each in a line, the rest counted, and where its own tree stands to theirs. Short on
/// purpose: their records and branches hold the rest, and the session reads those itself.
/// </summary>
internal static class SessionPartsText
{
    /// <summary>The most characters of an earlier part's note its line says: the record keeps it whole.</summary>
    internal const int NoteLimit = 200;

    /// <summary>The paragraph, or nothing where the start is no further part of a session, or is a resume or a carry-on.</summary>
    internal static string Of(SessionTarget target)
    {
        if (target is not { Parts: { Earlier.Count: > 0 } parts, CutOff: null, Answered: null }) return "";

        var text = new StringBuilder("\n");
        text.Append($"This quest continues a line of work already under way in `{target.Repository}` on this machine. The sessions\n")
            .Append("before you on it, newest first:\n\n");
        foreach (var part in parts.Earlier.Take(TargetPrompt.PartsShown)) text.Append(Line(part)).Append('\n');
        var more = parts.Earlier.Count - TargetPrompt.PartsShown;
        if (more > 0) text.Append($"- And {more} earlier session{(more == 1 ? "" : "s")}.\n");
        return text.Append('\n').Append(Stance(target, parts)).Append('\n').ToString();
    }

    /// <summary>One part in a line: its record, its quest, how it stands and what its record says, and where its work is.</summary>
    private static string Line(EarlierPart part)
    {
        var said = part.State + (Bounded(part.Note) is { } note ? $": {note}" : "");
        if (!said.EndsWith('.') && !said.EndsWith('…') && !said.EndsWith('!') && !said.EndsWith('?')) said += ".";
        var where = (part.Tree, part.Branch) switch
        {
            ({ } tree, { } branch) => $" Tree `{tree}`, branch `{branch}`.",
            ({ } tree, null) => $" Tree `{tree}`.",
            (null, { } branch) => $" Branch `{branch}`.",
            _ => "",
        };
        return $"- Session `{part.Session}`{(part.Quest is { } quest ? $" (quest `#{quest}`)" : "")}, {said}{where}";
    }

    /// <summary>A note's first line, bounded by <see cref="NoteLimit"/>; null where it says nothing.</summary>
    private static string? Bounded(string? note)
    {
        if (note?.ReplaceLineEndings("\n").Split('\n')[0].Trim() is not { Length: > 0 } line) return null;
        return line.Length <= NoteLimit ? line : line[..NoteLimit].TrimEnd() + "…";
    }

    /// <summary>Where this part's tree stands to theirs: gone into, beside a live part, or grown again after theirs went.</summary>
    private static string Stance(SessionTarget target, SessionParts parts)
    {
        if (parts.Into is { } into)
        {
            var stance = $"You are in session `{into}`'s tree, on its branch, so its work is here: read its commits before you\n"
                + "start, and build on them rather than over them.";
            return target.InFlight.Count == 0
                ? stance
                : stance + " It left these changes uncommitted, which are its work\nrather than yours:\n\n"
                  + string.Join("\n", target.InFlight.Select(line => $"    {line}"));
        }

        if (parts.Gone is { } gone)
        {
            return parts.GrewFrom is { } branch
                ? $"The tree session `{gone}` worked in is gone, so yours grew from `{branch}`, the branch that work landed on: its work is here."
                : $"The tree session `{gone}` worked in is gone, and no branch holds its work here, so yours grew from the line: its work is "
                  + "here only where it reached the line.";
        }

        return parts.HeldBy is { } held
            ? $"Session `{held}` is still at work in that tree, so you run beside it in a tree of your own, which\n"
              + "does not hold its work: read its branch where you need it, and do not do its work again."
            : "You run in a tree of your own, which does not hold their work: read their branches where you need them, and do not do "
              + "their work again.";
    }
}
