using System.Text;

namespace Daoris.Driver;

/// <summary>
/// A unified diff of two texts in git's form, so a folder's change set is drawn as a repository's: each hunk with three lines of
/// context, hunks whose context would touch joined, a missing final newline said as git says it. The common head and tail are set aside
/// and the rest is matched by its longest common run of lines, up to a bound past which the whole middle reads as removed and added.
/// </summary>
public static class TextPatch
{
    private const int Context = 3;
    private const long Cells = 4_000_000;

    /// <summary>The patch from <paramref name="before"/> to <paramref name="after"/>; null on a side is a file that is absent there.</summary>
    public static (string Patch, int Added, int Removed) Unified(string path, string? before, string? after)
    {
        var script = Script(Lines(before), Lines(after));
        var text = new StringBuilder($"diff --git a/{path} b/{path}\n")
            .Append(before is null ? "--- /dev/null\n" : $"--- a/{path}\n")
            .Append(after is null ? "+++ /dev/null\n" : $"+++ b/{path}\n");
        var changed = Enumerable.Range(0, script.Count).Where(at => script[at].Op != ' ').ToList();
        for (var k = 0; k < changed.Count; k++)
        {
            var first = changed[k];
            while (k + 1 < changed.Count && changed[k + 1] - changed[k] <= 2 * Context + 1) k++;
            Hunk(text, script, Math.Max(0, first - Context), Math.Min(script.Count - 1, changed[k] + Context));
        }

        return (text.ToString(), script.Count(line => line.Op == '+'), script.Count(line => line.Op == '-'));
    }

    private static void Hunk(StringBuilder text, List<(char Op, string Line)> script, int from, int to)
    {
        int oldAt = 0, newAt = 0;
        foreach (var (op, _) in script.Take(from))
        {
            if (op != '+') oldAt++;
            if (op != '-') newAt++;
        }

        var range = script.Skip(from).Take(to - from + 1).ToList();
        var oldCount = range.Count(line => line.Op != '+');
        var newCount = range.Count(line => line.Op != '-');
        text.Append($"@@ -{(oldCount > 0 ? oldAt + 1 : oldAt)},{oldCount} +{(newCount > 0 ? newAt + 1 : newAt)},{newCount} @@\n");
        foreach (var (op, line) in range)
        {
            text.Append(op).Append(line.TrimEnd('\n')).Append('\n');
            if (!line.EndsWith('\n')) text.Append("\\ No newline at end of file\n");
        }
    }

    private static List<(char Op, string Line)> Script(IReadOnlyList<string> a, IReadOnlyList<string> b)
    {
        var head = 0;
        while (head < a.Count && head < b.Count && a[head] == b[head]) head++;
        var tail = 0;
        while (tail < a.Count - head && tail < b.Count - head && a[a.Count - 1 - tail] == b[b.Count - 1 - tail]) tail++;

        var script = a.Take(head).Select(line => (' ', line)).ToList();
        var (n, m) = (a.Count - head - tail, b.Count - head - tail);
        if ((long)(n + 1) * (m + 1) > Cells)
        {
            script.AddRange(a.Skip(head).Take(n).Select(line => ('-', line)));
            script.AddRange(b.Skip(head).Take(m).Select(line => ('+', line)));
        }
        else
        {
            // The longest common run of a[i..] and b[j..], then a walk taking a shared line wherever it keeps the run longest.
            var run = new int[n + 1, m + 1];
            for (var i = n - 1; i >= 0; i--)
            {
                for (var j = m - 1; j >= 0; j--)
                {
                    run[i, j] = a[head + i] == b[head + j] ? run[i + 1, j + 1] + 1 : Math.Max(run[i + 1, j], run[i, j + 1]);
                }
            }

            int x = 0, y = 0;
            while (x < n || y < m)
            {
                if (x < n && y < m && a[head + x] == b[head + y])
                {
                    script.Add((' ', a[head + x++]));
                    y++;
                }
                else if (y >= m || (x < n && run[x + 1, y] >= run[x, y + 1])) script.Add(('-', a[head + x++]));
                else script.Add(('+', b[head + y++]));
            }
        }

        script.AddRange(a.Skip(a.Count - tail).Select(line => (' ', line)));
        return script;
    }

    private static List<string> Lines(string? text)
    {
        var lines = new List<string>();
        if (string.IsNullOrEmpty(text)) return lines;
        var start = 0;
        for (var at = text.IndexOf('\n'); at >= 0; at = text.IndexOf('\n', start))
        {
            lines.Add(text[start..(at + 1)]);
            start = at + 1;
        }

        if (start < text.Length) lines.Add(text[start..]);
        return lines;
    }
}
