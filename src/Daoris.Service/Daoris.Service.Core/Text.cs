namespace Daoris.Knowledge;

/// <summary>
/// Text handling shared by every search.
/// </summary>
/// <remarks>
/// These were three near-identical excerpt functions and two separator lists, one per search
/// implementation — which is this project's own thesis showing up in its own code: the same logic
/// re-derived in three places, already diverging in the details (one returned null on no match,
/// another the whole body, a third a truncation with no ellipsis).
/// </remarks>
public static class Text
{
    /// <summary>
    /// A word character is a letter or a digit, in any script; everything else separates. Hyphen and
    /// underscore separate on purpose: `no-tmp-for-repo-files` should be findable by searching for
    /// "tmp", and a reader looking for one word of a hyphenated name is the common case.
    /// </summary>
    /// <remarks>
    /// This is the rule FTS5's <c>unicode61</c> tokeniser applies to the same text, so a query's terms
    /// are the index's terms. It replaced a hand-listed ASCII string, which no list of characters
    /// can be the equal of: <c>D51：会话</c> tokenised to <c>d51：</c> — the fullwidth colon was not in
    /// the list — and no index row had ever held that term.
    /// </remarks>
    private static bool IsWordCharacter(char c) => char.IsLetterOrDigit(c);

    /// <summary>
    /// Lower-cased words worth matching on. Two characters and under are dropped: they are almost all
    /// articles and prepositions, and they match everything, which is the same as matching nothing.
    /// </summary>
    /// <remarks>
    /// 🔴 <b>The floor is a Latin heuristic and says so.</b> Two characters is a whole word in 中文 —
    /// 记录, 会话, 委托 — and on the first real index every Chinese query lost its terms here, fell
    /// through to the browse an <i>empty</i> query gets, and returned the whole corpus in the same
    /// order. A token in a script whose words are that short is kept whatever its length. The same
    /// rule, in the same words, lives in the platform's excerpt marker: found by its test in the
    /// other language, which is what a test in the other language is for.
    /// </remarks>
    public static List<string> Tokenize(string? text)
    {
        var lowered = Segment(text ?? string.Empty).ToLowerInvariant();
        var tokens = new List<string>();
        var start = -1;
        for (var i = 0; i <= lowered.Length; i++)
        {
            if (i < lowered.Length && IsWordCharacter(lowered[i]))
            {
                if (start < 0) start = i;
                continue;
            }
            if (start < 0) continue;
            var token = lowered[start..i];
            if (token.Length > 2 || IsShortWordScript(token)) tokens.Add(token);
            start = -1;
        }
        return tokens;
    }

    /// <summary>
    /// The same text with every run of ideographs cut into its overlapping two-character bigrams,
    /// space-separated — so a tokenizer that splits on spaces and punctuation finds words in it.
    /// </summary>
    /// <remarks>
    /// <para>🔴 <b>FTS5's <c>unicode61</c> keeps a run of ideographs as ONE token.</b> There are no
    /// spaces between words in 中文, so <c>每个会话都留下一份记录</c> is indexed whole and a query for
    /// <c>记录</c> matches nothing inside it — found by the test that was written for the tokeniser's
    /// floor, which cleared the floor and then returned an empty result instead of a wrong one.
    /// Every Chinese query on the first real index had been browsing the whole corpus; with the
    /// floor fixed it would have found nothing at all, which is the same defect wearing a quieter
    /// coat.</para>
    ///
    /// <para><b>Bigrams, not a dictionary.</b> Overlapping two-character units are the standard answer
    /// to CJK search without a segmenter: a two-character word is one unit, a longer word is its
    /// adjacent units, and the index and the query are cut the same way so they meet. Applied at
    /// index time (the FTS row) and at query time (through <see cref="Tokenize"/>), never to the
    /// stored body — an excerpt reads the original. A lone ideograph stays a unigram. Latin text is
    /// returned untouched, byte for byte.</para>
    /// </remarks>
    public static string Segment(string text)
    {
        var builder = new System.Text.StringBuilder(text.Length + text.Length / 2);
        var run = 0;   // length of the current ideograph run, counted in chars

        void FlushRun(int endExclusive)
        {
            if (run == 0) return;
            var start = endExclusive - run;
            if (run == 1)
            {
                builder.Append(text, start, 1);
            }
            else
            {
                for (var i = start; i < endExclusive - 1; i++)
                {
                    if (i > start) builder.Append(' ');
                    builder.Append(text, i, 2);
                }
            }
            run = 0;
        }

        for (var i = 0; i < text.Length; i++)
        {
            if (IsIdeograph(text[i]))
            {
                if (run == 0 && builder.Length > 0 && builder[^1] != ' ') builder.Append(' ');
                run++;
                continue;
            }

            FlushRun(i);
            if (i > 0 && IsIdeograph(text[i - 1]) && text[i] != ' ') builder.Append(' ');
            builder.Append(text[i]);
        }

        FlushRun(text.Length);
        return builder.ToString();
    }

    /// <summary>One character of a script whose words carry no spaces between them.</summary>
    private static bool IsIdeograph(char c) =>
        c is (>= '一' and <= '鿿')      // CJK Unified Ideographs
           or (>= '㐀' and <= '䶿')      // CJK Extension A
           or (>= '぀' and <= 'ヿ')      // Hiragana, Katakana
           or (>= '가' and <= '힯');     // Hangul syllables

    /// <summary>
    /// Whether a token is written in a script where one or two characters is a word, not a fragment —
    /// the same scripts <see cref="Segment"/> cuts, by the same test, so the two cannot disagree.
    /// </summary>
    private static bool IsShortWordScript(string token) => token.Any(IsIdeograph);

    /// <summary>
    /// A window of the body around the first matching term, so a result can show why it matched.
    /// </summary>
    /// <remarks>
    /// A result list that cannot show its reasoning gets treated as an oracle, which is exactly what
    /// it is not. When nothing matches — a semantic hit shares no literal term by definition — the
    /// opening of the body is still more useful than nothing.
    /// </remarks>
    public static string Excerpt(string body, IEnumerable<string>? terms = null, int window = 180)
    {
        body ??= string.Empty;

        var index = -1;
        foreach (var term in terms ?? [])
        {
            index = body.IndexOf(term, StringComparison.OrdinalIgnoreCase);
            if (index >= 0) break;
        }

        if (index < 0)
        {
            return body.Length <= window
                ? Flatten(body)
                : Flatten(body[..window]) + "…";
        }

        // Start a little before the match so the term has context on both sides rather than sitting
        // at the very edge of the window.
        var start = Math.Max(0, index - window / 3);
        var length = Math.Min(window, body.Length - start);
        return (start > 0 ? "…" : string.Empty)
             + Flatten(body.Substring(start, length))
             + (start + length < body.Length ? "…" : string.Empty);
    }

    /// <summary>One line: an excerpt is shown inline, and embedded newlines break every caller's layout.</summary>
    private static string Flatten(string text) => text.Replace('\n', ' ').Replace('\r', ' ').Trim();

    /// <summary>
    /// A document's text, BOM-less and LF, exactly as the CLI's <c>readText</c> reads it.
    /// </summary>
    /// <remarks>
    /// The same rule file is CRLF in a Windows checkout and LF elsewhere, so an unnormalized read makes
    /// a repository's doctrine depend on which machine indexed it. Convergence survived that only by
    /// accident — it splits on whitespace, so CRLF happened to fall out — and an accident in one method
    /// is not a property of the system. Normalizing at the read boundary makes it one, and makes the
    /// two halves of this project agree on what a document's bytes are.
    /// </remarks>
    public static string ReadDocument(string path) =>
        File.ReadAllText(path).TrimStart('﻿').ReplaceLineEndings("\n").Trim();
}
