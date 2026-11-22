using System.Text;

namespace Daoris.Driver;

/// <summary>
/// What a failed command's log hands the work beside its exit: each red block the log prints (an <c>== RED &lt;step&gt;</c> line and its
/// lines up to the next <c>== </c>, as the repository's check steps say a red step; only a step's header, <c>== RED</c> then a space or
/// nothing, opens one) and the log's last lines, within about <see cref="Lines"/> lines and <see cref="Bytes"/> bytes, with the log's path
/// relative to the home. A red block keeps its head where the bound cuts it, the end its last lines.
/// </summary>
/// <remarks>🔴 The log is a child's words: a line <see cref="GitWords.Redact"/> cuts is withheld whole (<see cref="Withheld"/>).</remarks>
internal static class GateLogExcerpt
{
    internal const int Lines = 40;

    internal const int Bytes = 4096;

    internal const string Withheld = "(a line holding a secret's shape was withheld)";

    /// <summary>The fewest of the log's last lines kept beside its red blocks.</summary>
    private const int Tail = 10;

    private const int LineChars = 400;

    private const string Step = "== ";

    private const string Red = "== RED";

    private const string Gap = "…";

    /// <summary>The words for the log's <paramref name="text"/>, read at <paramref name="log"/> (null: no path said); null where it holds no line.</summary>
    internal static string? Told(string text, string? log, string home)
    {
        var clean = GitWords.Redact(text) ?? "";
        var lines = clean.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n')
            .Select(line => line.Contains(GitWords.Cut, StringComparison.Ordinal) ? Withheld : line.TrimEnd()).ToList();
        while (lines.Count > 0 && lines[^1].Length == 0) lines.RemoveAt(lines.Count - 1);
        if (lines.Count == 0) return null;

        var red = new List<int>();
        for (var at = 0; at < lines.Count; at++)
        {
            if (!IsRed(lines[at])) continue;
            red.Add(at);
            while (at + 1 < lines.Count && !lines[at + 1].StartsWith(Step, StringComparison.Ordinal)) red.Add(++at);
        }

        red = [.. red.Take(Lines - Tail)];
        var end = Enumerable.Range(Math.Max(0, lines.Count - (Lines - red.Count)), Math.Min(lines.Count, Lines - red.Count)).Except(red).ToList();
        while (Size(lines, red, end) > Bytes && red.Count + end.Count > 1)
        {
            if (red.Count > 1) red.RemoveAt(red.Count - 1);
            else end.RemoveAt(0);
        }

        return $"{(Shown(log, home) is { } shown ? $"its log, {shown}, says:" : "its output says:")}\n{Joined(lines, red, end)}";
    }

    private static bool IsRed(string line) => line.StartsWith(Red, StringComparison.Ordinal) && (line.Length == Red.Length || line[Red.Length] == ' ');

    internal static string? Shown(string? log, string? home)
    {
        if (string.IsNullOrEmpty(log)) return null;
        if (home is null) return Path.GetFileName(log);
        var relative = Path.GetRelativePath(home, log).Replace('\\', '/');
        return relative.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(relative) ? Path.GetFileName(log) : relative;
    }

    private static int Size(List<string> lines, List<int> red, List<int> end) => Encoding.UTF8.GetByteCount(Joined(lines, red, end));

    private static string Joined(List<string> lines, List<int> red, List<int> end)
    {
        var said = new StringBuilder();
        var last = -1;
        foreach (var at in red.Concat(end).Distinct().Order())
        {
            if (said.Length > 0) said.Append('\n');
            if (at > last + 1) said.Append(Gap).Append('\n');
            said.Append(lines[at].Length > LineChars ? lines[at][..LineChars] + Gap : lines[at]);
            last = at;
        }

        return said.ToString();
    }
}
