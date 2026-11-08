using System.Text;

namespace Daoris.Driver;

/// <summary>The tokens the first form's <c>Glob</c> reads a path in: a literal character, <c>**/</c>, <c>**</c>, <c>*</c> and <c>?</c>.</summary>
internal enum FirstFormToken
{
    Literal,
    Folders,
    Across,
    Star,
    One,
}

/// <summary>One token of a word; a literal's character, a brace's characters all literal.</summary>
internal readonly record struct FirstFormPart(FirstFormToken Token, char Character = '\0')
{
    internal bool IsSlash => Token == FirstFormToken.Literal && Character == '/';
}

/// <summary>
/// What git's syntax can state of a first-form word: files only (no string it matches ends in <c>/</c>), closed by its tail (it
/// matches everything under what it matches), or kept, a folder top matched without what lies under it, which no git pattern states.
/// </summary>
internal enum FirstFormClass
{
    Files,
    Closed,
    Kept,
}

/// <summary>
/// The rule file's first form read in git's syntax (the tree-pool design §38.1), derived from the scan the first form's reader runs
/// (<see cref="RoomPaths.Declared"/> through <see cref="UnionJoins.Glob"/>): a path's braces expanded into words, each word sorted by
/// its tail (the longest run of tokens that can match nothing) and its head (the token before it) into exactly one class. A rule
/// becomes one entry of its closed words, one entry for each files-only word (its patterns, then each again as a folder after a
/// <c>!</c>, so the union of the rule's paths holds), and one entry of its paths holding a kept word, read as written.
/// </summary>
internal static class RuleFileFirstForm
{
    /// <summary>The rule's entries in git's syntax, the rule's fields on each; none where every word of it is one git never lists.</summary>
    internal static IReadOnlyList<FileRule> Entries(FileRule rule)
    {
        var closed = new List<string>();
        var files = new List<IReadOnlyList<string>>();
        var kept = new List<string>();
        foreach (var path in rule.Paths)
        {
            var keeps = false;
            foreach (var word in Words(path))
            {
                switch (ClassOf(word))
                {
                    case FirstFormClass.Closed:
                        closed.AddRange(Closed(word));
                        break;
                    case FirstFormClass.Files:
                        files.Add(FilesOnly(word));
                        break;
                    default:
                        keeps = true;
                        break;
                }
            }

            if (keeps) kept.Add(path);
        }

        var entries = new List<FileRule>();
        if (closed.Count > 0) entries.Add(rule with { Paths = [.. closed.Distinct(StringComparer.Ordinal)] });
        entries.AddRange(files.Select(patterns => rule with { Paths = patterns }));
        if (kept.Count > 0) entries.Add(rule with { Paths = kept, FirstForm = true });
        return entries;
    }

    /// <summary>Whether a first-form path holds a kept word.</summary>
    internal static bool Keeps(string path) => Words(path).Any(word => ClassOf(word) == FirstFormClass.Kept);

    /// <summary>
    /// The wordings git can state for a path's kept words: each folder they matched with everything in it, and the files they matched
    /// directly, their folders not. The path's other words are translated exactly in the rule's other entries.
    /// </summary>
    internal static (IReadOnlyList<string> Whole, IReadOnlyList<string> Files) Wordings(string path)
    {
        var kept = Words(path).Where(word => ClassOf(word) == FirstFormClass.Kept).ToList();
        return ([.. kept.SelectMany(Closed).Distinct(StringComparer.Ordinal)], [.. kept.SelectMany(FilesOf).SelectMany(each => each).Distinct(StringComparer.Ordinal)]);
    }

    /// <summary>The entries a kept path of <paramref name="entry"/> becomes in the wording the person chose.</summary>
    internal static IReadOnlyList<FileRule> Worded(FileRule entry, string path, bool whole)
    {
        var plain = entry with { FirstForm = false };
        var (closed, _) = Wordings(path);
        if (whole) return closed.Count == 0 ? [] : [plain with { Paths = closed }];
        return [.. Words(path).Where(word => ClassOf(word) == FirstFormClass.Kept).SelectMany(FilesOf).Select(patterns => plain with { Paths = patterns })];
    }

    /// <summary>A kept word's files: the word without its trailing <c>**/</c>, as a files-only word, or none where it then ends in <c>/</c>.</summary>
    private static IEnumerable<IReadOnlyList<string>> FilesOf(IReadOnlyList<FirstFormPart> word)
    {
        var end = word.Count;
        while (end > 0 && word[end - 1].Token == FirstFormToken.Folders) end--;
        if (end == 0 || word[end - 1].IsSlash) yield break;
        yield return FilesOnly(word.Take(end).ToList());
    }

    /// <summary>The words of a first-form path: a folder path read with <c>**</c> after it, its braces expanded in the order written.</summary>
    internal static IEnumerable<IReadOnlyList<FirstFormPart>> Words(string path)
    {
        var glob = path.EndsWith('/') ? path + "**" : path;
        var places = new List<List<List<FirstFormPart>>>();
        for (var i = 0; i < glob.Length; i++)
        {
            var c = glob[i];
            if (c == '*' && i + 1 < glob.Length && glob[i + 1] == '*')
            {
                i++;
                var folders = i + 1 < glob.Length && glob[i + 1] == '/';
                if (folders) i++;
                places.Add([[new(folders ? FirstFormToken.Folders : FirstFormToken.Across)]]);
            }
            else if (c is '*' or '?')
            {
                places.Add([[new(c == '*' ? FirstFormToken.Star : FirstFormToken.One)]]);
            }
            else if (c == '{' && glob.IndexOf('}', i) > i)
            {
                var end = glob.IndexOf('}', i);
                places.Add([.. glob[(i + 1)..end].Split(',').Select(alternative => alternative.Select(each => new FirstFormPart(FirstFormToken.Literal, each)).ToList())]);
                i = end;
            }
            else
            {
                places.Add([[new(FirstFormToken.Literal, c)]]);
            }
        }

        IEnumerable<List<FirstFormPart>> words = [[]];
        foreach (var place in places) words = [.. words.SelectMany(word => place.Select(choice => (List<FirstFormPart>)[.. word, .. choice]))];
        return words.Where(Listed).Select(word => (IReadOnlyList<FirstFormPart>)word);
    }

    /// <summary>The word's class, by an exhaustive reading of its tail and head.</summary>
    internal static FirstFormClass ClassOf(IReadOnlyList<FirstFormPart> word)
    {
        var tail = Tail(word);
        var run = word.Skip(word.Count - tail).ToList();
        var head = tail < word.Count ? word[word.Count - tail - 1] : (FirstFormPart?)null;
        if (run.All(each => each.Token == FirstFormToken.Star) && head is not { IsSlash: true }) return FirstFormClass.Files;
        var closes = run.Any(each => each.Token == FirstFormToken.Across)
            || run.Select((each, at) => (each, at)).Any(pair => pair.each.Token == FirstFormToken.Folders && run.Skip(pair.at + 1).Any(later => later.Token == FirstFormToken.Star));
        return closes ? FirstFormClass.Closed : FirstFormClass.Kept;
    }

    /// <summary>A word's patterns where no string it matches ends in <c>/</c>: the word, then each again as a folder after a <c>!</c>.</summary>
    private static IReadOnlyList<string> FilesOnly(IReadOnlyList<FirstFormPart> word)
    {
        var patterns = Patterns(word, word.Count);
        return [.. patterns, .. patterns.Select(each => "!" + each + "/")];
    }

    /// <summary>A word whose tail matches every string: its head-part followed by anything, as a folder where its head is a <c>/</c>.</summary>
    private static IReadOnlyList<string> Closed(IReadOnlyList<FirstFormPart> word)
    {
        var tail = Tail(word);
        if (tail == word.Count) return ["/*"];
        var head = word.Count - tail;
        return word[head - 1].IsSlash ? [.. Patterns(word, head - 1).Select(each => each + "/")] : [.. Patterns(word, head).Select(Starred)];
    }

    /// <summary>The count of the word's last tokens that can match nothing.</summary>
    private static int Tail(IReadOnlyList<FirstFormPart> word)
    {
        var tail = 0;
        while (tail < word.Count && word[word.Count - tail - 1].Token is FirstFormToken.Folders or FirstFormToken.Across or FirstFormToken.Star) tail++;
        return tail;
    }

    /// <summary>
    /// The word written part by part, anchored: a literal escaped where git reads it, <c>*</c> and <c>?</c> as themselves, a <c>**/</c>
    /// opening a part as itself, and one joined to the part before it, or a <c>**</c>, as two patterns each, so 2^n in all.
    /// </summary>
    private static List<string> Patterns(IReadOnlyList<FirstFormPart> word, int count)
    {
        List<StringBuilder> written = [new("/")];
        for (var i = 0; i < count; i++)
        {
            var part = word[i];
            string[] choices = part.Token switch
            {
                FirstFormToken.Literal => [part.Character is '*' or '?' or '[' or ' ' ? "\\" + part.Character : part.Character.ToString()],
                FirstFormToken.Star => ["*"],
                FirstFormToken.One => ["?"],
                FirstFormToken.Folders when Opens(word, i) => ["**/"],
                FirstFormToken.Folders when i + 1 < word.Count && word[i + 1].IsSlash => [""],
                FirstFormToken.Folders => ["", "*/**/"],
                _ => ["*", "*/**/*"],
            };
            written = [.. written.SelectMany(each => choices.Select(choice => Joined(each, choice)))];
        }

        return [.. written.Select(each => each.ToString())];
    }

    /// <summary>Whether the <c>**/</c> at <paramref name="at"/> opens a part: at the word's start, after a <c>/</c>, or after one that opens.</summary>
    private static bool Opens(IReadOnlyList<FirstFormPart> word, int at) =>
        at == 0 || word[at - 1].IsSlash || (word[at - 1].Token == FirstFormToken.Folders && Opens(word, at - 1));

    /// <summary>A piece appended, a <c>*</c> after a written <c>*</c> read as one, so no two stars make a <c>**</c> git reads apart.</summary>
    private static StringBuilder Joined(StringBuilder written, string piece)
    {
        var next = new StringBuilder(written.ToString());
        return next.Append(piece.StartsWith('*') && EndsInStar(next) ? piece[1..] : piece);
    }

    private static string Starred(string pattern) => EndsInStar(new StringBuilder(pattern)) ? pattern : pattern + "*";

    private static bool EndsInStar(StringBuilder text) => text.Length > 0 && text[^1] == '*' && (text.Length < 2 || text[^2] != '\\');

    /// <summary>Whether a word can match a top git lists: no leading <c>/</c>, and no empty, <c>.</c> or <c>..</c> part.</summary>
    private static bool Listed(List<FirstFormPart> word)
    {
        if (word.Count == 0 || word[0].IsSlash) return false;
        var part = new StringBuilder();
        var joined = false;
        foreach (var each in word)
        {
            if (each.IsSlash)
            {
                if (!joined && part.ToString() is "" or "." or "..") return false;
                (part, joined) = (new StringBuilder(), false);
            }
            else if (each.Token == FirstFormToken.Folders)
            {
                joined = joined || part.Length > 0;
            }
            else
            {
                part.Append(each.Token == FirstFormToken.Literal ? each.Character : '*');
            }
        }

        return joined || part.ToString() is not ("." or "..");
    }
}
