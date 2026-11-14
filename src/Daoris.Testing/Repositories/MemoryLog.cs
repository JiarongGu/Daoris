using System.Text.RegularExpressions;
using Daoris.Ports.Repositories;

namespace Daoris.Testing;

/// <summary>
/// The log a memory repository answers as <c>git log --reverse</c> does over a range: each commit with its subject (<c>%s</c>, the first
/// paragraph on one line) and the values of one trailer (<c>%(trailers:key=…,valueonly)</c>), read from the message's last paragraph
/// where every line of it is a <c>Key: value</c> trailer, the key matched without regard to case.
/// </summary>
internal static partial class MemoryLog
{
    [GeneratedRegex(@"^([A-Za-z0-9-]+)\s*:\s*(.*)$")]
    private static partial Regex Trailer();

    internal static GitLogEntry Entry(MemoryCommit commit, string trailer) =>
        new(commit.Id, commit.Parents, string.Join(' ', Paragraphs(commit.Message).FirstOrDefault() ?? []), Trailers(commit.Message, trailer));

    private static IReadOnlyList<string> Trailers(string message, string key)
    {
        var paragraphs = Paragraphs(message);
        if (paragraphs.Count < 2 || !paragraphs[^1].All(line => Trailer().IsMatch(line))) return [];
        return [.. paragraphs[^1].Select(line => Trailer().Match(line))
            .Where(match => string.Equals(match.Groups[1].Value, key, StringComparison.OrdinalIgnoreCase)).Select(match => match.Groups[2].Value.Trim())];
    }

    private static List<List<string>> Paragraphs(string message)
    {
        var paragraphs = new List<List<string>>();
        var current = new List<string>();
        foreach (var line in message.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n').Select(each => each.TrimEnd()))
        {
            if (line.Length > 0) current.Add(line.Trim());
            else if (current.Count > 0)
            {
                paragraphs.Add(current);
                current = [];
            }
        }

        if (current.Count > 0) paragraphs.Add(current);
        return paragraphs;
    }
}
