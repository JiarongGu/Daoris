namespace Daoris.Knowledge;

/// <summary>One piece of an entry as it is embedded: where it starts in the body, and the text sent.</summary>
/// <param name="Start">Where the piece's body text starts in <see cref="KnowledgeEntry.Body"/>.</param>
/// <param name="Text">What the embedder is sent: the title's lead, then the piece of the body.</param>
public readonly record struct EntryPiece(int Start, string Text);

/// <summary>
/// How an entry becomes the texts its vectors are made from (SEM3, D123): every part of its body, in
/// pieces no longer than the deployment's window, each led by the entry's title.
/// </summary>
/// <remarks>
/// <para>🔴 <b>Nothing of the body is left out.</b> The semantic tier embedded the title and the first
/// 2,000 characters and dropped the rest without saying so, and an embedder cuts whatever passes its own
/// window the same way. Measured on this repository's own index: 293 of 636 entries were longer, and a
/// third of the text never reached a vector — so a search by meaning could not find what a long decision
/// says past its opening.</para>
///
/// <para><b>The window is the deployment's</b> (D24): it is the most characters one embedded text carries,
/// the title included, and only the deployment knows its embedder's window. Characters only approximate
/// tokens, so a deployment leaves margin for code and for scripts a tokenizer counts densely.</para>
///
/// <para><b>Where a piece ends</b>: at the last blank line in the latter half of the window, else a line
/// break, a sentence end, a space, or at the window itself — never inside a surrogate pair. <b>Where the
/// next begins</b>: at a line or a sentence inside the last <see cref="Overlap"/> of the one before, so a
/// sentence a cut falls through is whole in one of them. Deterministic: the same entry and window always
/// give the same pieces. An entry within the window is one piece, the same text it always was.</para>
/// </remarks>
public static class EntryPieces
{
    /// <summary>
    /// The window when the deployment states none: the number the body was always cut at, now a statement
    /// rather than a silence. About 500 tokens of English prose, a small embedder's window.
    /// </summary>
    public const int DefaultWindow = 2000;

    /// <summary>Below this a piece is too short to mean anything, and the pieces of a long entry multiply.</summary>
    public const int MinimumWindow = 200;

    /// <summary>How far a piece reaches back into the one before, as a share of that piece.</summary>
    internal const double Overlap = 0.15;

    /// <summary>Every piece of an entry's body, in order, each no longer than <paramref name="window"/>.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The window is below <see cref="MinimumWindow"/>.</exception>
    public static IReadOnlyList<EntryPiece> Of(KnowledgeEntry entry, int window)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(window, MinimumWindow);

        var lead = Lead(entry.Title, window);
        var budget = window - lead.Length;
        var body = entry.Body;
        if (body.Length <= budget) return [new EntryPiece(0, lead + body)];

        return Spans(body, budget)
            .Select(span => new EntryPiece(span.Start, lead + body[span.Start..span.End].TrimEnd()))
            .ToList();
    }

    /// <summary>
    /// What leads every piece: the title twice, as the whole entry was always embedded — it is the author's
    /// own summary, and one line of it carries more than several of body — or once where two would take
    /// more than half the window. A title longer than that is cut in the lead alone: the entry keeps it
    /// whole, and the body, which is the knowledge, still reaches a vector in full.
    /// </summary>
    internal static string Lead(string title, int window)
    {
        var half = window / 2;
        var twice = $"{title}\n{title}\n";
        if (twice.Length <= half) return twice;
        if (title.Length + 1 <= half) return $"{title}\n";

        var keep = half - 1;
        if (char.IsHighSurrogate(title[keep - 1])) keep--;
        return $"{title[..keep]}\n";
    }

    /// <summary>The id a piece's vector is stored under: the entry's, and where the piece starts.</summary>
    /// <remarks>
    /// The payload stays the entry's id, which is what every reader of the collection resolves; the id
    /// only has to be unique and say which passage it was. The start is always written, so reading it from
    /// the LAST <c>@</c> is unambiguous whatever an entry's own heading holds.
    /// </remarks>
    public static string VectorId(string entryId, int start) => $"{entryId}@{start}";

    /// <summary>Where the piece a vector id names starts in its entry's body; 0 where it says none.</summary>
    public static int StartOf(string vectorId)
    {
        var at = vectorId.LastIndexOf('@');
        return at >= 0 && int.TryParse(vectorId.AsSpan(at + 1), out var start) && start > 0 ? start : 0;
    }

    /// <summary>The body's pieces as ranges, each at most <paramref name="budget"/> characters.</summary>
    private static IEnumerable<(int Start, int End)> Spans(string body, int budget)
    {
        var start = SkipSpace(body, 0);
        while (start < body.Length)
        {
            if (body.Length - start <= budget)
            {
                yield return (start, body.Length);
                yield break;
            }

            var end = Cut(body, start, budget);
            yield return (start, end);

            // Every cut lies in the latter half of its window and the overlap is a small share of it, so the
            // next piece starts after this one did and ends after this one ended: the walk always advances.
            var next = SkipSpace(body, Restart(body, start, end));
            start = next > start ? next : end;
        }
    }

    /// <summary>Where a piece starting at <paramref name="start"/> ends (exclusive).</summary>
    private static int Cut(string body, int start, int budget)
    {
        var limit = start + budget;
        var floor = start + budget / 2;

        for (var i = limit - 1; i >= floor; i--)
        {
            if (body[i] == '\n' && body[i - 1] == '\n') return i + 1;
        }

        for (var i = limit - 1; i >= floor; i--)
        {
            if (body[i] == '\n') return i + 1;
        }

        for (var i = limit - 1; i >= floor; i--)
        {
            if (EndsSentence(body, i)) return i + 1;
        }

        for (var i = limit - 1; i >= floor; i--)
        {
            if (char.IsWhiteSpace(body[i])) return i + 1;
        }

        return char.IsLowSurrogate(body[limit]) ? limit - 1 : limit;
    }

    /// <summary>Where the piece after <c>[start, end)</c> begins: the earliest line or sentence inside its last <see cref="Overlap"/>.</summary>
    private static int Restart(string body, int start, int end)
    {
        var from = end - (int)((end - start) * Overlap);
        for (var i = from; i < end; i++)
        {
            if (body[i - 1] == '\n' || EndsSentence(body, i - 1) || (char.IsWhiteSpace(body[i - 1]) && EndsSentence(body, i - 2)))
            {
                return i;
            }
        }

        for (var i = from; i < end; i++)
        {
            if (char.IsWhiteSpace(body[i - 1])) return i;
        }

        return end;
    }

    /// <summary>
    /// Whether a sentence ends at <paramref name="i"/>: a full stop, a question or an exclamation followed by
    /// a space, or one of the ideographic ones, which need none.
    /// </summary>
    private static bool EndsSentence(string body, int i) =>
        i >= 0 && i < body.Length
        && (body[i] is '。' or '！' or '？'
            || (body[i] is '.' or '!' or '?' && i + 1 < body.Length && char.IsWhiteSpace(body[i + 1])));

    private static int SkipSpace(string body, int i)
    {
        while (i < body.Length && char.IsWhiteSpace(body[i])) i++;
        return i;
    }
}
