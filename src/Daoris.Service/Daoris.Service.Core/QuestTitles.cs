using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Daoris.Knowledge;

/// <summary>
/// A quest's short title (SESSUX1j, the session management design §6): the few words that tell it apart where a list
/// gives it one line. Whoever publishes it may give one; where none was given, its name is read from its own words.
/// </summary>
/// <remarks>
/// <para><b>A given short title is the publisher's</b>: kept as written, trimmed, never translated, and refused past
/// <see cref="MaxShort"/> characters or over a line break, in this service's own words, so every door refuses the same.</para>
///
/// <para><b>A derived name is honest about what it is</b> (SESSUX1j, as the dispatch asked it after the install's long
/// titles): only the quest's own words, chosen and never rewritten; whole words, a Chinese character being the smallest
/// cut; an ellipsis where words were left off; the first line that says what is wanted, skipping a line that is wholly a
/// bracketed note and setting aside a note that leads a line (the install's re-filed asks put theirs first); and a ticket key
/// the ask names (<c>TK-2203</c>) leading it, as trackers name work. It is read when asked, never written into the
/// record, so the record keeps only what a publisher said, and a quest from before the field is named the same way.</para>
/// </remarks>
public static partial class QuestTitles
{
    /// <summary>How long a short title may be, in characters: a list's one line, about 20 in Chinese.</summary>
    public const int MaxShort = 40;

    /// <summary>What a cut name ends with, so it never reads as the whole.</summary>
    private const string Cut = "…";

    /// <summary>A ticket key as trackers spell one: upper-case letters (digits after the first), a dash, a number.</summary>
    [GeneratedRegex(@"(?<![A-Za-z0-9-])[A-Z][A-Z0-9]{1,9}-[0-9]{1,7}(?![A-Za-z0-9-])")]
    private static partial Regex TicketKey();

    /// <summary>
    /// What a publisher gave, as a quest keeps it: trimmed, with blank meaning none, or why it cannot be kept.
    /// </summary>
    public static (string? Short, string? Refusal) Judge(string? given)
    {
        var trimmed = given?.Trim() ?? "";
        if (trimmed.Length == 0) return (null, null);
        if (trimmed.Contains('\n') || trimmed.Contains('\r'))
        {
            return (null, $"A short title is one line of at most {MaxShort} characters — the few words that tell this quest "
                          + "apart in a list. Put the rest in the title and the body.");
        }

        var length = new StringInfo(trimmed).LengthInTextElements;
        return length > MaxShort
            ? (null, $"A short title is at most {MaxShort} characters, about 20 in Chinese — this one is {length}. Give the few "
                     + "words that tell this quest apart in a list; the title says the whole.")
            : (trimmed, null);
    }

    /// <summary>A quest's name where its publisher gave no short title: its own words, chosen as the remarks say.</summary>
    public static string Derive(string title, string body)
    {
        var line = FirstLine(title, body);
        var key = KeyIn(title) ?? KeyIn(body);

        var words = Words(line);
        if (key is not null && !line.StartsWith(key, StringComparison.Ordinal))
        {
            // The key leads, said once: the token that is the key, its punctuation aside, leaves the words behind it.
            words = [new Word(key, SpaceBefore: false), .. words.Where(word => word.Text.Trim(Around) != key)
                .Select((word, index) => index == 0 ? word with { SpaceBefore = true } : word)];
        }

        return Fit(words);
    }

    /// <summary>
    /// The first ticket key a text names, or null. A standard's or an algorithm's name is spelled the same way
    /// (<c>UTF-8</c>, <c>SHA-256</c>, <c>RFC-9110</c>) and is not a ticket, so those prefixes are passed over.
    /// </summary>
    private static string? KeyIn(string text) =>
        TicketKey().Matches(text).FirstOrDefault(match => !NotTickets.Contains(match.Value[..match.Value.IndexOf('-')]))?.Value;

    private static readonly HashSet<string> NotTickets =
        ["UTF", "UCS", "SHA", "MD", "ISO", "IEC", "IEEE", "RFC", "PEP", "ECMA", "HTTP", "TLS", "SSL", "AES", "RSA", "IPV", "IP", "ES", "COVID"];

    /// <summary>Punctuation a key or a cut may stand beside, which neither keeps.</summary>
    private static readonly char[] Around = ['(', ')', '[', ']', ':', ',', '.', ';', '（', '）', '，', '：', '；', '。'];

    /// <summary>What a cut name may not end on: a word that only joins what was left off.</summary>
    private static readonly char[] Dangling = [',', ';', ':', '(', '[', '—', '–', '-', '·', '/', '、', '，', '；', '：', '（', '「'];

    /// <summary>
    /// The first line that says what is wanted: the title's, then the body's, past blank lines, quoted lines and lines that
    /// are wholly a bracketed note, with a note that leads its line set aside. Where every line is a note, the title as written.
    /// </summary>
    private static string FirstLine(string title, string body)
    {
        foreach (var raw in title.Split('\n').Concat(body.Split('\n')))
        {
            var line = Unmarked(raw.Trim());
            if (line.Length == 0 || line.StartsWith('>')) continue;
            var rest = AfterLeadingNote(line);
            if (rest.Length > 0) return rest;
        }

        return title.Split('\n')[0].Trim();
    }

    /// <summary>A line without the heading or list marker a body may open it with.</summary>
    private static string Unmarked(string line)
    {
        var at = 0;
        while (at < line.Length && line[at] == '#') at++;
        if (at > 0 && at < line.Length && line[at] == ' ') return line[(at + 1)..].Trim();
        return line.Length > 2 && line[0] is '-' or '*' && line[1] == ' ' ? line[2..].Trim() : line;
    }

    /// <summary>What follows a bracketed note that opens a line — empty when the note is the whole line.</summary>
    private static string AfterLeadingNote(string line)
    {
        if (line.Length == 0) return line;
        var close = line[0] switch { '(' => ')', '[' => ']', '（' => '）', '【' => '】', _ => '\0' };
        if (close == '\0') return line;

        var depth = 0;
        for (var at = 0; at < line.Length; at++)
        {
            if (line[at] == line[0]) depth++;
            else if (line[at] == close && --depth == 0) return line[(at + 1)..].TrimStart(' ', ':', '-', '—', '–').Trim();
        }

        // A bracket that never closes is not a note: it is the line.
        return line;
    }

    /// <summary>One unit a name may be cut after: a word, or a single Chinese character, and whether a space precedes it.</summary>
    private sealed record Word(string Text, bool SpaceBefore);

    /// <summary>A line's words: what white space separates, each wide character its own, so Chinese is cut between characters.</summary>
    private static List<Word> Words(string line)
    {
        var words = new List<Word>();
        foreach (var (token, index) in line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Select((token, index) => (token, index)))
        {
            var run = new StringBuilder();
            var first = true;
            foreach (var element in Elements(token))
            {
                if (!Wide(element))
                {
                    run.Append(element);
                    continue;
                }

                if (run.Length > 0)
                {
                    words.Add(new Word(run.ToString(), first && index > 0));
                    run.Clear();
                    first = false;
                }

                words.Add(new Word(element, first && index > 0));
                first = false;
            }

            if (run.Length > 0) words.Add(new Word(run.ToString(), first && index > 0));
        }

        return words;
    }

    /// <summary>
    /// The words joined, whole when they fit <see cref="MaxShort"/> columns, else as many as fit with room for the cut's
    /// mark, never ending on one that only joins what was left off. A first word too wide alone is cut by its characters.
    /// </summary>
    private static string Fit(List<Word> words)
    {
        var whole = Joined(words);
        if (Width(whole) <= MaxShort) return whole;

        var kept = new List<Word>();
        foreach (var word in words)
        {
            var next = Joined([.. kept, word]);
            if (Width(next) > MaxShort - Width(Cut)) break;
            kept.Add(word);
        }

        if (kept.Count == 0)
        {
            var cut = new StringBuilder();
            foreach (var element in Elements(words[0].Text))
            {
                if (Width(cut.ToString() + element) > MaxShort - Width(Cut)) break;
                cut.Append(element);
            }

            return cut + Cut;
        }

        var text = Joined(kept).TrimEnd(Dangling).TrimEnd();
        return (text.Length > 0 ? text : Joined(kept)) + Cut;
    }

    private static string Joined(IEnumerable<Word> words)
    {
        var text = new StringBuilder();
        foreach (var word in words)
        {
            if (text.Length > 0 && word.SpaceBefore) text.Append(' ');
            text.Append(word.Text);
        }

        return text.ToString();
    }

    /// <summary>How many columns a text takes: a wide character two, every other one.</summary>
    private static int Width(string text) => Elements(text).Sum(element => Wide(element) ? 2 : 1);

    private static IEnumerable<string> Elements(string text)
    {
        var elements = StringInfo.GetTextElementEnumerator(text);
        while (elements.MoveNext()) yield return elements.GetTextElement();
    }

    /// <summary>Whether a character is set wide: Chinese, Japanese and Korean, and the full-width forms beside them.</summary>
    private static bool Wide(string element)
    {
        var code = char.ConvertToUtf32(element, 0);
        return code is >= 0x1100 and <= 0x115F
            or >= 0x2E80 and <= 0x303E
            or >= 0x3040 and <= 0x33FF
            or >= 0x3400 and <= 0x4DBF
            or >= 0x4E00 and <= 0x9FFF
            or >= 0xA000 and <= 0xA4CF
            or >= 0xAC00 and <= 0xD7A3
            or >= 0xF900 and <= 0xFAFF
            or >= 0xFE30 and <= 0xFE4F
            or >= 0xFF00 and <= 0xFF60
            or >= 0xFFE0 and <= 0xFFE6
            or >= 0x20000 and <= 0x3FFFD;
    }
}
