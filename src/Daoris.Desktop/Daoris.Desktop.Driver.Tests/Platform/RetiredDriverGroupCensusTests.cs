using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The tree has no <c>driver</c> group: a comment, a guide line, an area sentence, a usage or a tool's text names the group a
/// verb is in. The tracked files that teach or comment are read for the retired two-word form; the records that are history (the
/// decisions, the archive, the fix log, the changelog and the dated design documents) are left out, and each file that may say it
/// is named here with its count and why, so a new one is a decision.
/// </summary>
[Trait(Category.Name, Category.Census)]
public sealed class RetiredDriverGroupCensusTests
{
    private static readonly string Retired = "daoris" + " driver";

    private static readonly Dictionary<string, int> Named = new(StringComparer.Ordinal)
    {
        // Asserts the install note teaches no old form: the pattern is the old form.
        ["src/Daoris.Cli/tool-test/desktop-publish.test.ts"] = 1,
    };

    private static readonly string[] Roots =
        ["src", "tools", "docs/areas", "docs/guide", ".claude/knowledge", ".claude/skills", "canon", "examples", "README.md"];

    private static readonly string[] Kinds = [".cs", ".ts", ".tsx", ".mjs", ".json", ".md", ".txt", ".csproj", ".props"];

    [Fact]
    public void No_comment_or_taught_line_spells_the_retired_group()
    {
        var root = GitSeed.RepoRoot();
        var pattern = new Regex(Regex.Escape(Retired) + @"\b", RegexOptions.CultureInvariant);
        var found = new SortedDictionary<string, int>(StringComparer.Ordinal);
        foreach (var path in Tracked(root))
        {
            var full = Path.Combine(root, path);
            if (!File.Exists(full)) continue;
            var count = pattern.Matches(File.ReadAllText(full)).Count;
            if (count > 0) found[path] = count;
        }

        Assert.Equal(Named.OrderBy(pair => pair.Key, StringComparer.Ordinal), found.OrderBy(pair => pair.Key, StringComparer.Ordinal));
    }

    private static IEnumerable<string> Tracked(string root)
    {
        var start = new ProcessStartInfo("git") { WorkingDirectory = root, RedirectStandardOutput = true, UseShellExecute = false };
        start.ArgumentList.Add("ls-files");
        start.ArgumentList.Add("-z");
        start.ArgumentList.Add("--");
        foreach (var each in Roots) start.ArgumentList.Add(each);
        using var git = Process.Start(TestGit.Past(start)) ?? throw new InvalidOperationException("git did not start");
        var listed = git.StandardOutput.ReadToEnd();
        git.WaitForExit();
        return listed.Split('\0', StringSplitOptions.RemoveEmptyEntries)
            .Where(path => Kinds.Contains(Path.GetExtension(path), StringComparer.Ordinal));
    }
}
