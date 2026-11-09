namespace Daoris.Driver;

/// <summary>
/// A repository's own configuration file read as git reads its plain form, nothing started: each setting by its full name (the section
/// and key blind to case, a subsection as written), every value in order. Includes and the person's own files are not followed, so a
/// setting only they hold is not read here.
/// </summary>
internal sealed class GitConfigFile
{
    private static readonly GitConfigFile Empty = new([]);

    private readonly List<(string Name, string Value)> _settings;

    private GitConfigFile(List<(string Name, string Value)> settings) => _settings = settings;

    /// <summary>The configuration under <paramref name="commonDir"/>; empty where it has none or it is unread.</summary>
    public static GitConfigFile Of(string? commonDir)
    {
        if (commonDir is null) return Empty;
        try
        {
            var path = Path.Combine(commonDir, "config");
            return File.Exists(path) ? Parse(File.ReadAllLines(path)) : Empty; // read-shares: git's own configuration
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return Empty;
        }
    }

    /// <summary>The last value of <paramref name="name"/>, as git's <c>--get</c> answers; null where it is not set.</summary>
    public string? Last(string name) => _settings.LastOrDefault(each => Same(each.Name, name)).Value;

    /// <summary>Every value of <paramref name="name"/>, in order.</summary>
    public IEnumerable<string> All(string name) => _settings.Where(each => Same(each.Name, name)).Select(each => each.Value);

    internal static GitConfigFile Parse(IEnumerable<string> lines)
    {
        var settings = new List<(string, string)>();
        var section = "";
        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.Length == 0 || line[0] is '#' or ';') continue;
            if (line[0] == '[' && line.IndexOf(']') is var close and > 0)
            {
                var header = line[1..close].Trim();
                var quote = header.IndexOf('"');
                section = quote < 0 ? header : $"{header[..quote].Trim()}.{header[(quote + 1)..].TrimEnd('"')}";
                line = line[(close + 1)..].Trim();
                if (line.Length == 0) continue;
            }

            var equals = line.IndexOf('=');
            var key = (equals < 0 ? line : line[..equals]).Trim();
            settings.Add(($"{section}.{key}", equals < 0 ? "true" : Value(line[(equals + 1)..])));
        }

        return new GitConfigFile(settings);
    }

    // A value with its comment cut and its quotes taken off.
    private static string Value(string text)
    {
        var (value, quoted) = (new System.Text.StringBuilder(), false);
        foreach (var c in text)
        {
            if (c == '"') quoted = !quoted;
            else if (!quoted && c is '#' or ';') break;
            else value.Append(c);
        }

        return value.ToString().Trim();
    }

    // The section and the key blind to case; a subsection as written.
    private static bool Same(string stored, string asked)
    {
        var (s, a) = (stored.Split('.'), asked.Split('.'));
        if (s.Length != a.Length) return false;
        for (var at = 0; at < s.Length; at++)
        {
            var blind = at == 0 || at == s.Length - 1;
            if (!string.Equals(s[at], a[at], blind ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)) return false;
        }

        return true;
    }
}
