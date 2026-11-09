using System.Text;

namespace Daoris.Driver;

/// <summary>One include a configuration file names: its path as written, and its condition (<c>gitdir:…</c>, <c>onbranch:…</c>) where it is an <c>includeIf</c>.</summary>
internal sealed record GitConfigInclude(string Path, string? Condition);

/// <summary>
/// The includes a git configuration file names, read as git's parser reads sections and values: <c>[include]</c> and
/// <c>[includeIf "…"]</c>, each <c>path</c> key, its quotes and escapes taken and a comment after it dropped. Every include is named
/// whether its condition matches now or not.
/// </summary>
internal static class GitConfigIncludes
{
    /// <summary>The includes <paramref name="bytes"/> names, in order.</summary>
    internal static IReadOnlyList<GitConfigInclude> Parse(byte[] bytes)
    {
        var found = new List<GitConfigInclude>();
        string? section = null;
        string? condition = null;
        var lines = Encoding.UTF8.GetString(bytes).TrimStart('﻿').Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        for (var at = 0; at < lines.Length; at++)
        {
            var line = lines[at].TrimStart();
            // A value continued on the next line by a trailing backslash is one value.
            while (line.EndsWith('\\') && !line.EndsWith(@"\\", StringComparison.Ordinal) && at + 1 < lines.Length) line = line[..^1] + lines[++at];
            if (line.Length == 0 || line[0] is '#' or ';') continue;
            if (line[0] == '[')
            {
                (section, condition) = Header(line);
                line = line[(line.IndexOf(']') is var close and >= 0 ? close + 1 : line.Length)..].TrimStart();
                if (line.Length == 0) continue;
            }

            if (section is not ("include" or "includeif")) continue;
            var equals = line.IndexOf('=');
            var key = (equals < 0 ? line : line[..equals]).Trim();
            if (equals < 0 || !key.Equals("path", StringComparison.OrdinalIgnoreCase)) continue;
            if (Value(line[(equals + 1)..]) is { Length: > 0 } value) found.Add(new GitConfigInclude(value, section == "includeif" ? condition ?? "" : null));
        }

        return found;
    }

    private static (string? Section, string? Sub) Header(string line)
    {
        var close = line.IndexOf(']');
        var inner = close < 0 ? line[1..] : line[1..close];
        var quote = inner.IndexOf('"');
        if (quote >= 0)
        {
            var name = inner[..quote].Trim().ToLowerInvariant();
            var sub = inner[(quote + 1)..];
            return (name, sub.EndsWith('"') ? sub[..^1].Replace("\\\"", "\"", StringComparison.Ordinal).Replace(@"\\", @"\", StringComparison.Ordinal) : sub);
        }

        var dot = inner.IndexOf('.');
        return dot < 0 ? (inner.Trim().ToLowerInvariant(), null) : (inner[..dot].Trim().ToLowerInvariant(), inner[(dot + 1)..].Trim());
    }

    private static string Value(string raw)
    {
        var value = new StringBuilder();
        var quoted = false;
        var trailing = 0;
        for (var at = 0; at < raw.Length; at++)
        {
            var c = raw[at];
            if (!quoted && c is '#' or ';') break;
            if (c == '"')
            {
                quoted = !quoted;
                continue;
            }

            if (c == '\\' && at + 1 < raw.Length)
            {
                var next = raw[++at];
                value.Append(next switch { 'n' => '\n', 't' => '\t', 'b' => '\b', _ => next });
                trailing = 0;
                continue;
            }

            value.Append(c);
            trailing = !quoted && char.IsWhiteSpace(c) ? trailing + 1 : 0;
        }

        return value.ToString(0, value.Length - trailing).TrimStart();
    }
}
