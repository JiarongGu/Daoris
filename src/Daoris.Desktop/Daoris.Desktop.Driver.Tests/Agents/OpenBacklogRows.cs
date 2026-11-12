using System.Text.RegularExpressions;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The rows a held-pending case may name: the open rows of the backlog, read as the file stands. A row is an open task's id, or a bare
/// letter naming a sub-row its parent task still lists as left; a row the archive holds is no row.
/// </summary>
internal static partial class OpenBacklogRows
{
    private const string Parent = "AGENTPROVIDER1";

    public static IReadOnlyList<string> Read() => Read(File.ReadAllLines(Path.Combine(GitSeed.RepoRoot(), "TASKS.md")));

    public static IReadOnlyList<string> Read(IEnumerable<string> backlog)
    {
        var rows = new List<string>();
        foreach (var line in backlog)
        {
            if (OpenTask().Match(line) is not { Success: true } task) continue;
            var id = task.Groups[1].Value;
            rows.Add(id);
            if (id == Parent)
            {
                var left = line[(line.IndexOf("left", StringComparison.Ordinal) is var at and >= 0 ? at : 0)..];
                rows.AddRange(SubRow().Matches(left).Select(match => match.Groups[1].Value));
            }
            else if (id.StartsWith(Parent, StringComparison.Ordinal) && id.Length > Parent.Length)
            {
                rows.Add(id[Parent.Length..]);
            }
        }

        return rows;
    }

    [GeneratedRegex(@"^- \[ \] \*\*([A-Za-z0-9][A-Za-z0-9\-]*)")]
    private static partial Regex OpenTask();

    [GeneratedRegex(@"\*\*([a-z](?:-[a-z]+)?)\*\*")]
    private static partial Regex SubRow();
}
