namespace Daoris.Driver;

/// <summary>The words of a <c>setting</c> proposal's value, read as the terminal verbs read them.</summary>
internal static class HelpSettingWords
{
    /// <summary>
    /// A rule door's words read as its terminal verb reads them: each of <paramref name="valued"/> with the word after it, each
    /// of <paramref name="named"/>, and nothing else — refused in a sentence that names the shape.
    /// </summary>
    public static string? RuleWords(List<string> words, string door, string usage, string[] valued, string[] named,
        out Dictionary<string, string> flags, out HashSet<string> switches)
    {
        flags = new Dictionary<string, string>(StringComparer.Ordinal);
        switches = new HashSet<string>(StringComparer.Ordinal);
        for (var at = 0; at < words.Count; at++)
        {
            var word = words[at];
            if (valued.Contains(word))
            {
                if (at + 1 >= words.Count || words[at + 1].StartsWith("--", StringComparison.Ordinal)) return $"`{word}` needs its value — {usage}";
                flags[word] = words[++at];
            }
            else if (named.Contains(word)) switches.Add(word);
            else return $"`{word}` is not a word `{door}` takes — {usage}";
        }

        return null;
    }

    /// <summary>
    /// A proposal's words split as a terminal splits them: at spaces, a double-quoted run kept whole as one word, <c>\"</c>
    /// inside it a quote. Null where a quote is left open.
    /// </summary>
    public static List<string>? Words(string value)
    {
        var words = new List<string>();
        var word = new System.Text.StringBuilder();
        var quoted = false;
        var held = false;
        for (var at = 0; at < value.Length; at++)
        {
            var c = value[at];
            if (quoted)
            {
                if (c == '\\' && at + 1 < value.Length && value[at + 1] == '"')
                {
                    word.Append('"');
                    at++;
                }
                else if (c == '"') quoted = false;
                else word.Append(c);
            }
            else if (c == '"')
            {
                quoted = true;
                held = true;
            }
            else if (char.IsWhiteSpace(c))
            {
                if (word.Length > 0 || held) words.Add(word.ToString());
                word.Clear();
                held = false;
            }
            else word.Append(c);
        }

        if (quoted) return null;
        if (word.Length > 0 || held) words.Add(word.ToString());
        return words;
    }
}
