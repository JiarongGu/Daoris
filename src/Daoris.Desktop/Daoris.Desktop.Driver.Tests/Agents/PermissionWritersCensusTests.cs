using System.Text.RegularExpressions;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// Every writer of the rule file saves through <see cref="PermissionChange"/>: its read, its decision and its save under one lock, so no
/// writer's save undoes another's. A save elsewhere in the desktop's sources would read the rules and write them back outside the lock.
/// </summary>
[Trait(Category.Name, Category.Census)]
public sealed class PermissionWritersCensusTests
{
    private static readonly string[] Projects = ["Daoris.Desktop.Driver", "Daoris.Desktop.Driver.Host", "Daoris.Desktop.Modules", "Daoris.Desktop.App"];

    [Fact]
    public void No_source_saves_the_rule_file_but_the_one_locked_change()
    {
        var root = Path.Combine(WorkspaceRoot.Folder, "src", "Daoris.Desktop");
        var saves = new Regex(@"PermissionRules\s*\.\s*Save\s*\(");
        var found = new List<string>();
        var scanned = 0;
        foreach (var project in Projects)
        foreach (var path in Directory.EnumerateFiles(Path.Combine(root, project), "*.cs", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(root, path).Replace('\\', '/');
            if (relative.Contains("/obj/") || relative.Contains("/bin/")) continue;
            scanned++;
            if (saves.IsMatch(File.ReadAllText(path)) && !relative.EndsWith("Agents/PermissionChange.cs", StringComparison.Ordinal)) found.Add(relative);
        }

        Assert.True(scanned > 100, $"the census read {scanned} files: its folders have drifted");
        Assert.True(found.Count == 0, "These save the rule file outside the one locked change (PermissionChange.Change): " + string.Join(", ", found));
    }
}
