using System.Text.RegularExpressions;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The census of the readers of a rule's paths (the tree-pool design §37.2, 4): every read of a <c>FileRule</c>'s <c>Paths</c> under
/// the driver's <c>Workplace/</c> passes through <c>GitPattern</c>, or stands among the readers that only show or carry a path.
/// </summary>
[Trait(Category.Name, Category.Census)]
public sealed class RulePathsCensusTests
{
    /// <summary>
    /// The lines that read a rule's paths without matching them: the matcher itself, the first form's translation, the writer, the
    /// rule's own record and the ones that only show, carry, order or count them; and the integration file's merge rules, whose
    /// <c>Paths</c> is one attribute pattern, not a rule file's.
    /// </summary>
    private static readonly string[] OnlyShow =
    [
        "Files/GitPattern.cs", "Files/RuleFileFirstForm.cs", "Files/FileRuleFile.cs", "Files/FileRule.cs", "Files/PersonFilesForm.cs",
        "Files/FilesLooks.cs:rule.Paths.Where(RuleFileFirstForm.Keeps)", "Files/FilesLooks.cs:new FilesLookRule(name, rule.Kind.Word(), rule.Paths",
        "Files/FileAnalysis.cs:ThenBy(rule => rule.Paths[0]", "Files/FileAnalysis.cs:rule.Paths.Concat(WorkspaceMembers.Modules",
        "Files/FilesCommand.cs", "Files/RepositoryFilesReader.cs:rule.Paths[0]", "Files/FileSignatures.cs:rule.Paths.Select(path => path.StartsWith(\"!/\"",
        "Files/FileAnalysis.cs:hit.Rule.Paths.Where(path => !path.StartsWith('!'))",
        "BacklogMerge.cs:Lines.FirstOrDefault(rule => rule.Paths == path)", "IntegrationDeclaration.cs:Select(rule => rule.Paths)",
    ];

    [Fact]
    public void Every_read_of_a_rules_paths_passes_through_git_pattern_or_only_shows_it()
    {
        var root = Path.Combine(WorkspaceRoot.Folder, "src", "Daoris.Desktop", "Daoris.Desktop.Driver", "Workplace");
        var site = new Regex(@"\b(?:rule|Rule|entry|hit\.Rule|each\.Rule|later\.Rule|winner\.Rule)\.Paths\b");
        var unread = new List<string>();
        var found = 0;
        foreach (var path in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(root, path).Replace('\\', '/');
            if (relative.Contains("/obj/") || relative.Contains("/bin/")) continue;
            foreach (var line in File.ReadLines(path).Where(line => site.IsMatch(line) && !line.TrimStart().StartsWith("///", StringComparison.Ordinal)))
            {
                found++;
                if (line.Contains("GitPattern.", StringComparison.Ordinal) || line.Contains(".Matches(", StringComparison.Ordinal)) continue;
                if (OnlyShow.Any(each => each.Split(':', 2) is var parts && parts[0] == relative && (parts.Length == 1 || line.Contains(parts[1], StringComparison.Ordinal)))) continue;
                unread.Add($"{relative}: {line.Trim()}");
            }
        }

        Assert.True(found >= 10, $"the census found {found} reads of a rule's paths: its pattern has drifted");
        Assert.Empty(unread);
    }
}
