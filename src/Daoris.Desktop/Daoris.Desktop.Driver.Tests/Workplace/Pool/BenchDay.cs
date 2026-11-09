using System.Text.Json;
using static Daoris.Desktop.Driver.Tests.GitFixture;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The day a bench replays of this repository's main: the first-parent commits of the 24 hours up to a tip, each made again in the
/// bench's clone with the tip's <c>daoris.files.json</c>, so every line reads the rule file as the rows it measures left it; and the
/// clone's own <c>local/</c> holding a stand-in at each path the rule file copies, never the checkout's.
/// </summary>
internal static class BenchDay
{
    private const string RuleFile = "daoris.files.json";

    /// <summary>The day's commits up to <paramref name="tip"/>, oldest first, each made again over its own tree with the tip's rule file.</summary>
    internal static async Task<IReadOnlyList<string>> OfAsync(string root, string tip)
    {
        var listed = (await GitAsync(root, "log", "--first-parent", "-400", "--format=%H %ct %s", tip))
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Split(' ', 3))
            .Select(parts => (Commit: parts[0], At: long.Parse(parts[1]), Subject: parts.Length > 2 ? parts[2] : parts[0]))
            .ToList();
        var day = listed.Where(each => each.At >= listed[0].At - 86400).Reverse().ToList();
        var rules = (await GitAsync(root, "rev-parse", $"{tip}:{RuleFile}")).Trim();
        var made = new List<string>();
        foreach (var (commit, _, subject) in day)
        {
            var entries = (await GitAsync(root, "ls-tree", commit)).Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Where(line => !line.EndsWith("\t" + RuleFile, StringComparison.Ordinal))
                .Append($"100644 blob {rules}\t{RuleFile}");
            var tree = (await RunWithAsync(root, null, string.Join("\n", entries) + "\n", "mktree")).Stdout.Trim();
            string[] parent = made.Count == 0 ? [] : ["-p", made[^1]];
            made.Add((await GitAsync(root, ["commit-tree", tree, .. parent, "-m", subject])).Trim());
        }

        return made;
    }

    /// <summary>A stand-in written in <paramref name="root"/> at each path the tip's rule file copies into a tree; the paths written.</summary>
    internal static async Task<IReadOnlyList<string>> StandInsAsync(string root, string tip)
    {
        using var file = JsonDocument.Parse(await GitAsync(root, "show", $"{tip}:{RuleFile}"));
        var copied = file.RootElement.GetProperty("rules").EnumerateArray()
            .Where(rule => rule.TryGetProperty("reach", out var reach) && reach.GetString() == "copy")
            .SelectMany(rule => rule.GetProperty("paths").EnumerateArray().Select(path => path.GetString()!))
            .Where(path => !path.StartsWith('!'))
            .Select(path => path.TrimStart('/'))
            .ToList();
        foreach (var path in copied)
        {
            var full = Path.Combine(root, path.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            await File.WriteAllTextAsync(full, "<Project><!-- the tree-pool bench's stand-in for the checkout's own file --></Project>\n");
        }

        return copied;
    }
}
