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
public static partial class Text
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
    public static List<string> Tokenize(string? text) => Words(Segment(text ?? string.Empty));

    /// <summary>
    /// What a question asks: its <see cref="Tokenize">tokens</see>, then each two and three of its adjacent
    /// words joined, as an identifier spells them (ORIENT1f), each once, with how many words it stands for.
    /// </summary>
    /// <remarks>
    /// <para>The index holds <c>ProbeLock</c> whole as well as by its words (<see cref="Segment"/>), and a
    /// question in plain words says <i>probe lock</i>. Its words alone find every entry that says probe and
    /// lock anywhere; joined, they find the one that names the identifier. A join that spells nothing matches
    /// nothing and costs nothing.</para>
    ///
    /// <para><b>A join counts once for each word it joins.</b> Matched, it is those words in the one form the
    /// code gives them, so it stands for each of them again. Measured on this repository's index (ORIENT1f,
    /// D134's ORIENT1g note): asked once, <i>what decided the probe lock</i> still ranked a design section
    /// titled with <i>lock</i> and <i>decided</i> above the FIX-LOG entry and the decision note that name
    /// <c>ProbeLock</c>; counted as its two words, the note came first and the fix second. A phrase of the
    /// adjacent words instead ranked first the note that quotes the question, and proximity is not what the
    /// question asks.</para>
    ///
    /// <para>Only the question's own words are joined, never an identifier's words again, and never a word in
    /// a script whose bigrams joined are no word. The joins come after the tokens, so an excerpt still opens
    /// on the first word the question said.</para>
    /// </remarks>
    public static List<QueryTerm> QueryTerms(string? text)
    {
        var terms = Tokenize(text).Distinct(StringComparer.Ordinal).Select(token => new QueryTerm(token, 1)).ToList();
        var words = Words(Bigrams(text ?? string.Empty));
        for (var length = 2; length <= JoinedWords; length++)
        {
            for (var start = 0; start + length <= words.Count; start++)
            {
                var run = words.GetRange(start, length);
                if (run.Any(IsShortWordScript)) continue;
                var joined = string.Concat(run);
                if (terms.Any(term => term.Term == joined)) continue;
                terms.Add(new QueryTerm(joined, length));
            }
        }

        return terms;
    }

    /// <summary>The most adjacent words a question joins: an identifier of three words is common, of four rare.</summary>
    private const int JoinedWords = 3;

    /// <summary>The words of already-segmented text, lower-cased, the floor applied.</summary>
    private static List<string> Words(string segmented)
    {
        var lowered = segmented.ToLowerInvariant();
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
    /// The same text cut so a tokenizer that splits on spaces and punctuation finds every word in it: each
    /// run of ideographs as its overlapping two-character bigrams, and each identifier with its words
    /// spelled beside it (<see cref="Identifiers"/>, ORIENT1f).
    /// </summary>
    /// <remarks>
    /// Applied at index time (the FTS row) and at query time (through <see cref="Tokenize"/>), never to the
    /// stored body, so an excerpt reads the original. Prose with neither is returned byte for byte.
    /// </remarks>
    public static string Segment(string text) => Identifiers(Bigrams(text));

    /// <summary>
    /// The same text with every identifier followed by its words: <c>ProbeLock</c> becomes
    /// <c>ProbeLock Probe Lock</c>, and <c>probe_lock</c> becomes <c>probe_lock probelock</c>.
    /// </summary>
    /// <remarks>
    /// <para>🔴 <b><c>unicode61</c> keeps <c>ProbeLock</c> as ONE token</b>, as it keeps a run of
    /// ideographs (ORIENT1f). The question <i>what decided the probe lock</i> never reached the decision that
    /// named it: no word of the question was its token (D24's ORIENT1c note). An underscore already
    /// separates, so <c>probe_lock</c> was its words and never itself whole. Each shape now matches the
    /// others and the words, and still matches itself.</para>
    ///
    /// <para><b>Where the words are</b>: a run of letters, digits and underscores, cut at its underscores and
    /// where its case turns: before an upper-case letter after a lower-case one (<c>probe|Lock</c>), and
    /// before the last capital of a run of capitals or a digit when a lower-case letter follows it
    /// (<c>HTTP|Server</c>, <c>UTF8|Encoding</c>), except a plural's <c>s</c> (<c>APIs</c>,
    /// <c>URLs|For</c>). A digit is no boundary on its own, so a task's id (<c>TOOL6g</c>, <c>D125</c>)
    /// stays one word. A run with one word, or with an ideograph in it, is left as it is.</para>
    ///
    /// <para><b>What is added</b>: the run joined where underscores cut it, then each cut piece's case
    /// words where it has two or more, each of three characters or more, since the query's floor never asks
    /// for a shorter one. Hyphens are not joined: a hyphen is prose as often as an identifier's.</para>
    /// </remarks>
    private static string Identifiers(string text)
    {
        var builder = new System.Text.StringBuilder(text.Length + 16);
        var spelled = false;
        var start = -1;
        for (var i = 0; i <= text.Length; i++)
        {
            if (i < text.Length && (char.IsLetterOrDigit(text[i]) || text[i] == '_'))
            {
                if (start < 0) start = i;
                continue;
            }

            if (start >= 0)
            {
                builder.Append(text, start, i - start);
                foreach (var word in WordsOf(text, start, i))
                {
                    builder.Append(' ').Append(word);
                    spelled = true;
                }
                start = -1;
            }
            if (i < text.Length) builder.Append(text[i]);
        }

        return spelled ? builder.ToString() : text;
    }

    /// <summary>The words an identifier is spelled with, beside it; empty for a run that is one word.</summary>
    private static List<string> WordsOf(string text, int start, int end)
    {
        var words = new List<string>();
        var run = text.AsSpan(start, end - start);
        var hasLetter = false;
        foreach (var c in run)
        {
            if (IsIdeograph(c)) return words;
            hasLetter |= char.IsLetter(c);
        }
        if (!hasLetter) return words;

        var pieces = run.ToString().Split('_', StringSplitOptions.RemoveEmptyEntries);
        if (pieces.Length > 1) words.Add(string.Concat(pieces));
        foreach (var piece in pieces)
        {
            var parts = CaseWords(piece);
            if (parts.Count < 2) continue;
            words.AddRange(parts.Where(part => part.Length > 2));
        }
        return words;
    }

    /// <summary>One piece of an identifier, cut where its case turns.</summary>
    private static List<string> CaseWords(string piece)
    {
        var parts = new List<string>();
        var from = 0;
        for (var i = 1; i < piece.Length; i++)
        {
            if (!char.IsUpper(piece[i])) continue;
            var before = piece[i - 1];
            var turns = char.IsLower(before)
                || ((char.IsUpper(before) || char.IsDigit(before))
                    && i + 1 < piece.Length && char.IsLower(piece[i + 1])
                    && !IsPluralS(piece, i + 1));
            if (!turns) continue;
            parts.Add(piece[from..i]);
            from = i;
        }
        parts.Add(piece[from..]);
        return parts;
    }

    /// <summary>A lone <c>s</c> after capitals, at the end or before the next word: a plural, not a word.</summary>
    private static bool IsPluralS(string piece, int at) =>
        piece[at] == 's' && (at + 1 == piece.Length || char.IsUpper(piece[at + 1]));

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
    private static string Bigrams(string text)
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
    ///
    /// **The window is the prose's, never the frontmatter's** (POLISH4). Every canon-shaped entry
    /// opens with its frontmatter, and a window there read "--- name: … applies_when: …" flattened
    /// onto one line. The frontmatter is still searched; a hit found only there shows the prose's
    /// opening, as a semantic hit does.
    /// </remarks>
    public static string Excerpt(string body, IEnumerable<string>? terms = null, int window = 180) =>
        ExcerptAt(body, terms, window).Text;

    /// <summary>
    /// <see cref="Excerpt"/>, and the line of the body it starts on, counted from 0 (ORIENT2e): what a hit names
    /// as the line its excerpt starts on, once the entry's own first line is added.
    /// </summary>
    /// <remarks>
    /// The line of the first character the excerpt shows: the frontmatter it skipped is counted, and a window
    /// that opens on a line's end starts on the next line, as the excerpt's text does.
    /// </remarks>
    public static (string Text, int Line) ExcerptAt(string body, IEnumerable<string>? terms = null, int window = 180)
    {
        var (rest, skipped) = WithoutFrontmatter(body ?? string.Empty);
        // Markup goes within lines, never a newline, so a line of this is the same line of the rest.
        body = WithoutMarkup(rest);

        var index = -1;
        foreach (var term in terms ?? [])
        {
            index = body.IndexOf(term, StringComparison.OrdinalIgnoreCase);
            if (index >= 0) break;
        }

        if (index < 0)
        {
            return (body.Length <= window
                ? Flatten(body)
                : Flatten(body[..window]) + "…", skipped + LineOf(body, 0, window));
        }

        // Start a little before the match so the term has context on both sides rather than sitting
        // at the very edge of the window.
        var start = Math.Max(0, index - window / 3);
        var length = Math.Min(window, body.Length - start);
        return ((start > 0 ? "…" : string.Empty)
             + Flatten(body.Substring(start, length))
             + (start + length < body.Length ? "…" : string.Empty), skipped + LineOf(body, start, start + length));
    }

    /// <summary>The line, from 0, of the first character between two places that is not white space: where a window shows from.</summary>
    private static int LineOf(string text, int start, int end)
    {
        var at = start;
        while (at < Math.Min(end, text.Length) && char.IsWhiteSpace(text[at])) at++;
        if (at >= Math.Min(end, text.Length)) at = start;
        return text.AsSpan(0, Math.Min(at, text.Length)).Count('\n');
    }

    /// <summary>
    /// A body without its leading frontmatter block — a <c>---</c> line, fields, and a closing <c>---</c>
    /// line — and how many lines went with it. A body that only opens with a rule and never closes one keeps
    /// everything.
    /// </summary>
    private static (string After, int Skipped) WithoutFrontmatter(string body)
    {
        if (!body.StartsWith("---\n", StringComparison.Ordinal)) return (body, 0);
        var close = body.IndexOf("\n---", 3, StringComparison.Ordinal);
        if (close < 0) return (body, 0);
        var after = body.IndexOf('\n', close + 4);
        if (after < 0) return (string.Empty, 0);
        var rest = body[(after + 1)..].TrimStart();
        return (rest, body.AsSpan(0, body.Length - rest.Length).Count('\n'));
    }

    /// <summary>
    /// A body without the Markdown that marks its words (UX5 U39): a heading's hashes, emphasis's
    /// <c>**</c> and <c>__</c>, and code's backticks. The frontmatter went (POLISH4) and these stayed, so
    /// an excerpt read "# World streaming … **chunk hydration** runs". The page shows an excerpt as
    /// plain text, so they are machinery too.
    /// </summary>
    /// <remarks>
    /// A single <c>_</c> or <c>*</c> stays: <c>applies_when</c> and <c>a * b</c> are words, and a pattern
    /// that took them for emphasis would eat an identifier.
    /// </remarks>
    private static string WithoutMarkup(string body) =>
        Headings().Replace(body, string.Empty).Replace("**", string.Empty).Replace("__", string.Empty).Replace("`", string.Empty);

    [System.Text.RegularExpressions.GeneratedRegex(@"^#{1,6}[ \t]+", System.Text.RegularExpressions.RegexOptions.Multiline)]
    private static partial System.Text.RegularExpressions.Regex Headings();

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
    public static string ReadDocument(string path) => ReadDocumentAt(path).Text;

    /// <summary>
    /// <see cref="ReadDocument"/>, and the line of the file its text starts on, counted from 1 (ORIENT2e): the
    /// lines the trim took from the top are still the file's, so every line an entry names is counted from them.
    /// </summary>
    public static (string Text, int First) ReadDocumentAt(string path)
    {
        var text = File.ReadAllText(path).TrimStart('﻿').ReplaceLineEndings("\n");
        var lead = text.Length - text.TrimStart().Length;
        return (text.Trim(), 1 + text.AsSpan(0, lead).Count('\n'));
    }
}

/// <summary>One term a question asks (ORIENT1f): a word, or adjacent words joined, and how many words it stands for.</summary>
/// <param name="Term">The term as the index holds it, lower-cased.</param>
/// <param name="Words">1 for a word; the number of words joined for a join.</param>
public readonly record struct QueryTerm(string Term, int Words);
