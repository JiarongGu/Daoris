using System.Text;
using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>
/// What names a landing's branch: a <c>land</c>'s pattern and the ticket key it reads, the words of the work it is named for, and the
/// chain's first quest. The CLI keeps a twin of the pattern and key checks, sentence for sentence.
/// </summary>
public static class BranchPatterns
{
    /// <summary>
    /// What a pattern may say. One of the first three is required, or every session's work would go to one branch; two sessions on one
    /// ticket meet the second's <c>exists</c> refusal.
    /// </summary>
    public static readonly IReadOnlyList<string> Placeholders = ["quest", "session", "ticket", "slug", "repository"];

    /// <summary>What a pattern is tried against before it is kept; the CLI's twin tries the same names.</summary>
    private static readonly LandingNames Sample = new("0fda18", "sample-work", "engine", "s1a2b3c4", "ABC-123");

    private static readonly Regex Placeholder = new(@"\{([^{}]*)\}", RegexOptions.CultureInvariant);

    /// <summary>A ticket's key where the <c>land</c> names none: a project's capitals, a dash and its number, as <c>TK-2202</c>.</summary>
    public const string DefaultKey = @"[A-Z][A-Z0-9]+-\d+";

    /// <summary>How long a key may be: a shape, not a program. The CLI's twin holds the same bound.</summary>
    public const int KeyLimit = 64;

    /// <summary>
    /// The letters a key may escape: the classes and controls .NET and JavaScript read alike. <c>\x</c>, <c>\u</c>, <c>\c</c>, <c>\p</c>
    /// and the rest differ, or are refused by one engine. The CLI's <c>SHARED_ESCAPES</c> is the same list.
    /// </summary>
    public const string SharedEscapes = "dDwWsSbBnrtfv";

    /// <summary>How long one search for a key may run before its words are passed over: a key is the person's, and need not be quick.</summary>
    private static readonly TimeSpan KeyTimeout = TimeSpan.FromMilliseconds(100);

    /// <summary>A web address in a title: never a branch's words.</summary>
    private static readonly Regex WebAddress = new(@"\b(?:https?://|www\.)\S*", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    /// <summary>What is wrong with a branch pattern, in a sentence, or null when it names one branch per session.</summary>
    public static string? Problem(string pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern))
        {
            return "a branch pattern needs a name — e.g. `feature/{quest}-{slug}`.";
        }

        foreach (Match used in Placeholder.Matches(pattern))
        {
            if (!Placeholders.Contains(used.Groups[1].Value))
            {
                return $"`{used.Value}` is not something a pattern can say — "
                    + string.Join(", ", Placeholders.Select(name => $"`{{{name}}}`")) + ".";
            }
        }

        if (!pattern.Contains("{quest}", StringComparison.Ordinal) && !pattern.Contains("{session}", StringComparison.Ordinal)
            && !pattern.Contains("{ticket}", StringComparison.Ordinal))
        {
            return "a pattern needs `{quest}` or `{session}`, or every session's work would be put on one branch.";
        }

        var sample = Expand(pattern, Sample);
        return sample.Contains('{') || sample.Contains('}') || !BranchName.IsValid(sample)
            ? $"`{pattern}` does not make a branch name git would take — it gives `{sample}`."
            : null;
    }

    /// <summary>
    /// What is wrong with a ticket's key, in a sentence, or null where both readers take it the same way: the driver matches it in .NET
    /// and the CLI checks it in JavaScript, so it is held to what the two engines read alike — at most <see cref="KeyLimit"/> characters,
    /// no <c>(?</c> group, no back-reference, a letter escaped only as one of <see cref="SharedEscapes"/>, no class subtracted and no empty
    /// class. Each engine's own compile is the last word.
    /// </summary>
    public static string? KeyProblem(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return $"a ticket's key needs a shape — e.g. `{DefaultKey}`, the one read where a `land` names none.";
        }

        if (key.Length > KeyLimit) return $"a ticket's key is at most {KeyLimit} characters, and this one is {key.Length}.";

        var inClass = false;
        for (var at = 0; at < key.Length; at++)
        {
            var next = at + 1 < key.Length ? key[at + 1] : '\0';
            if (key[at] == '\\')
            {
                if (next is 'k' or (>= '1' and <= '9'))
                {
                    return "a ticket's key may not refer back to a group (`\\k` or `\\1` to `\\9`), which the driver and the CLI read differently.";
                }

                // A punctuation mark or symbol escaped is itself in both; `_`, a digit, a space and any other letter are not read alike.
                var punctuation = next is > ' ' and < '\x7f' && !char.IsAsciiLetterOrDigit(next) && next != '_';
                if (next != '\0' && !SharedEscapes.Contains(next) && !punctuation)
                {
                    return $"a ticket's key may escape only a punctuation mark or one of `{string.Join(' ', SharedEscapes.Select(c => $"\\{c}"))}`, "
                        + $"which the driver and the CLI read alike, and `\\{next}` is neither.";
                }

                at++; // the escaped character is the escape's own
            }
            else if (inClass)
            {
                // .NET reads `[a-z-[aeiou]]` as a class minus a class, JavaScript as a class then `]`.
                if (key[at] == '-' && next == '[')
                {
                    return "a ticket's key may not subtract a class (`-[` inside a class), which the driver and the CLI read differently.";
                }

                if (key[at] == ']') inClass = false;
            }
            else if (key[at] == '(' && next == '?')
            {
                return "a ticket's key may not use `(?` — a lookaround, a named group or an inline option, which the driver and the "
                    + "CLI read differently.";
            }
            else if (key[at] == '[')
            {
                // `[]` and `[^]` are an empty class in JavaScript, and a class holding `]` in .NET.
                if (next == ']' || (next == '^' && at + 2 < key.Length && key[at + 2] == ']'))
                {
                    return "a ticket's key may not hold an empty class, `[]` or `[^]`, which the driver and the CLI read differently.";
                }

                inClass = true;
            }
        }

        try
        {
            _ = new Regex(key, RegexOptions.CultureInvariant, KeyTimeout);
            return null;
        }
        catch (ArgumentException)
        {
            return $"a ticket's key `{key}` is not a regular expression — e.g. `{DefaultKey}`.";
        }
    }

    /// <summary>
    /// A pattern filled with one session's names. A name that is empty takes with it the separator (<c>-</c>, <c>_</c>, <c>.</c>,
    /// <c>/</c>) it would leave doubled, leading or trailing, keeping a <c>/</c> where the other side's is not one. A ticket the key did
    /// not find is the session's id.
    /// </summary>
    public static string Expand(string pattern, LandingNames names)
    {
        var text = new StringBuilder();
        var gaps = new List<int>();
        var from = 0;
        foreach (Match used in Placeholder.Matches(pattern))
        {
            text.Append(pattern, from, used.Index - from);
            from = used.Index + used.Length;
            var value = used.Groups[1].Value switch
            {
                "quest" => names.Quest,
                "session" => names.Session,
                "ticket" => names.Ticket ?? names.Session,
                "slug" => names.Slug,
                "repository" => names.Repository,
                _ => used.Value,
            };
            // Two empty names side by side leave one gap.
            if (value.Length > 0) text.Append(value);
            else if (gaps.Count == 0 || gaps[^1] != text.Length) gaps.Add(text.Length);
        }

        text.Append(pattern, from, pattern.Length - from);

        // From the last gap back, so a removal never moves a gap still to come.
        for (var at = gaps.Count - 1; at >= 0; at--)
        {
            var gap = gaps[at];
            var before = gap > 0 && IsSeparator(text[gap - 1]);
            var after = gap < text.Length && IsSeparator(text[gap]);
            if (before && after) text.Remove(text[gap - 1] == '/' && text[gap] != '/' ? gap : gap - 1, 1);
            else if (before && gap == text.Length) text.Remove(gap - 1, 1);
            else if (after && gap == 0) text.Remove(gap, 1);
        }

        return text.ToString();
    }

    private static bool IsSeparator(char c) => c is '-' or '_' or '.' or '/';

    /// <summary>
    /// A title in the words git takes: lower-case letters and digits, joined by hyphens, whole words up to 40 characters, with every web
    /// address left out. A title with no words git takes is `work`; a title whose words were all an address leaves nothing.
    /// </summary>
    public static string Slug(string? title) => Slug(title, ticket: null);

    /// <summary>The slug of a title the pattern also names by its ticket: the ticket's own words left out too, in any case.</summary>
    public static string Slug(string? title, string? ticket)
    {
        var text = WebAddress.Replace(title ?? "", " ");
        if (ticket is { Length: > 0 }) text = text.Replace(ticket, " ", StringComparison.OrdinalIgnoreCase);
        var slug = Words(text);
        return slug.Length > 0 ? slug : Words(title).Length > 0 ? "" : "work";
    }

    private static string Words(string? text)
    {
        var slug = new StringBuilder();
        foreach (var word in Regex.Split((text ?? "").ToLowerInvariant(), "[^a-z0-9]+").Where(word => word.Length > 0))
        {
            if (slug.Length + (slug.Length > 0 ? 1 : 0) + word.Length > 40) break;
            if (slug.Length > 0) slug.Append('-');
            slug.Append(word);
        }

        return slug.ToString();
    }

    /// <summary>
    /// The ticket a subject's work is for: the first match of <paramref name="key"/>, else <see cref="DefaultKey"/>, in its name, its
    /// title, its request's body, then each of its links. A match git would not take in a branch's name is passed over, as are words a
    /// search runs past its time on. Null where none is found: the session's id stands in.
    /// </summary>
    public static string? Ticket(LandingSubject subject, string? key)
    {
        Regex shape;
        try
        {
            shape = new Regex(key ?? DefaultKey, RegexOptions.CultureInvariant, KeyTimeout);
        }
        catch (ArgumentException)
        {
            return null; // a key the node's own check refuses is never kept, so none is found by it
        }

        string?[] sources = [subject.Name, subject.Title, subject.Body, .. subject.Links ?? []];
        foreach (var source in sources.OfType<string>())
        {
            try
            {
                for (var match = shape.Match(source); match.Success; match = match.NextMatch())
                {
                    if (BranchName.IsValid(match.Value)) return match.Value;
                }
            }
            catch (RegexMatchTimeoutException)
            {
                // These words took the key too long; the next source is searched.
            }
        }

        return null;
    }

    /// <summary>What one session's branch is named from: where the pattern says <c>{ticket}</c>, the ticket its key finds, and the slug without it.</summary>
    public static LandingNames Names(LandingSubject subject, Landing landing, string repository)
    {
        var ticket = (landing.Pattern ?? "").Contains("{ticket}", StringComparison.Ordinal) ? Ticket(subject, landing.Key) : null;
        var slug = ticket is null ? subject.Slug : Slug(subject.Name ?? subject.Title, ticket);
        return new(subject.Quest ?? subject.Session, slug, repository, subject.Session, ticket);
    }

    /// <summary>The branch a branch landing's pattern names for one session: what the plan says and the press makes.</summary>
    public static string Branch(LandingSubject subject, Landing landing, string repository) =>
        Expand(landing.Pattern ?? "", Names(subject, landing, repository));

    /// <summary>Whether a pattern reads a subject's words: <c>{slug}</c>, or <c>{ticket}</c>, searched in them.</summary>
    public static bool ReadsWords(Landing landing) =>
        (landing.Pattern ?? "").Contains("{slug}", StringComparison.Ordinal) || (landing.Pattern ?? "").Contains("{ticket}", StringComparison.Ordinal);

    /// <summary>The git log format <see cref="OldestSubject"/> reads: each commit's subject after a unit separator, oldest first.</summary>
    public const string SubjectsFormat = "--format=%x1f%s";

    /// <summary>The oldest commit's subject from <c>git log --reverse</c> in <see cref="SubjectsFormat"/>; null where the log names no commit.</summary>
    public static string? OldestSubject(string log)
    {
        var commits = log.Split('\u001f');
        return commits.Length < 2 ? null : commits[1].Trim();
    }

    /// <summary>
    /// What a landing is named for: the session, and the chain's first quest with its title, walking up each step's parent, since a
    /// chain lands from its last step; a chat with no quest has its opening line. The walk stops where the service no longer answers for
    /// a parent, and after twenty steps.
    /// </summary>
    public static async Task<LandingSubject> SubjectAsync(
        string session, string? quest, Func<string, Task<QuestView?>> find, string? opening)
    {
        if (quest is null) return new LandingSubject(session, null, opening);

        var current = await find(quest).ConfigureAwait(false);
        if (current is null) return new LandingSubject(session, quest, opening);
        for (var steps = 0; steps < 20 && current.Parent is { Length: > 0 } parent; steps++)
        {
            if (await find(parent).ConfigureAwait(false) is not { } above) break;
            current = above;
        }

        return LandingSubject.Of(session, current);
    }
}
