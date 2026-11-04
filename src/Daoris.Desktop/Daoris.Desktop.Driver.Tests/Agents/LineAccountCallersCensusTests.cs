using System.Text.RegularExpressions;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FOUNDATION1a-i's census of the account rule's callers, apart from <c>LineAccountTests</c>' cases so that the <c>censuses</c> gate runs it
/// alone (REVIEW2 T6): every caller of the roster's resume, of <c>CoolingOf</c> and <c>CoolingsOf</c>, and every reader of
/// <c>ResumeChoice.Waits</c>, reads <see cref="LineAccount"/> or is named <i>not yet moved</i> (X4-1.1).
/// </summary>
[Trait(Category.Name, Category.Census)]
public sealed class LineAccountCallersCensusTests
{
    /// <summary>
    /// The files that read a cooling account (<c>CoolingOf</c>, <c>CoolingsOf</c>), ask a resume (<c>ResumeAsync</c>) or read whether its words
    /// wait (<c>ResumeChoice.Waits</c>) and have not moved onto the rule. Each only hands the roster's reading of an account's cool-off to a
    /// listing or a verb of the shell, which decides nothing by it (FOUNDATION1a-i's census; each moves with the row that moves its reader).
    /// </summary>
    private static readonly string[] NotYetMoved =
    [
        "Daoris.Desktop.Driver/WorkOrders/Sessions/SessionCooling.cs",
        "Daoris.Desktop.Modules/Bridge/Routes/SessionsMoveDesk.cs",
        "Daoris.Desktop.Modules/Bridge/Routes/SessionsVerbs.cs",
    ];

    /// <summary>
    /// The projects a caller of the account facts can be written in: the driver, its host, the modules and the app, each of whose sources
    /// reaches this gate (the lane table's MOD9c), never the whole desktop tree, which spans lanes the census cannot place.
    /// </summary>
    private static readonly string[] Projects = ["Daoris.Desktop.Driver", "Daoris.Desktop.Driver.Host", "Daoris.Desktop.Modules", "Daoris.Desktop.App"];

    /// <summary>The walks the rule asks are the places today's resume walks, the handed quest's start beside them.</summary>
    [Fact]
    public void Every_caller_of_the_account_facts_reads_the_rule_or_is_named_not_yet_moved()
    {
        var root = Path.Combine(WorkspaceRoot.Folder, "src", "Daoris.Desktop");
        var sites = new[] { @"Harnesses\s*\.ResumeAsync\s*\(", @"\.CoolingsOf\(", @"\.CoolingOf\(", @"\bresume\s+is\s*\{\s*Waits\b", @"\bresume\.Waits\b" };
        var unread = new List<string>();
        var found = 0;
        foreach (var project in Projects)
        foreach (var path in Directory.EnumerateFiles(Path.Combine(root, project), "*.cs", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(root, path).Replace('\\', '/');
            if (relative.Contains("/obj/") || relative.Contains("/bin/") || relative.EndsWith("Harnesses/HarnessRoster.cs", StringComparison.Ordinal))
            {
                continue;
            }

            var text = File.ReadAllText(path);
            if (!sites.Any(site => Regex.IsMatch(text, site))) continue;

            found++;
            if (!Regex.IsMatch(text, @"\b(LineAccount|LineKind|LineAnswer)\b") && !NotYetMoved.Contains(relative)) unread.Add(relative);
        }

        Assert.True(found >= 5, $"the census found {found} files that read an account's cool-off or resume: its patterns have drifted");
        Assert.True(unread.Count == 0, "read neither the account rule nor named not yet moved: " + string.Join(", ", unread));
        Assert.All(NotYetMoved, name => Assert.True(File.Exists(Path.Combine(root, name)), $"{name} is named not yet moved but is not there"));
        Assert.Equal(["NoToolchain", "Switch", "Elsewhere", "Handed"], Enum.GetNames<LineWalkPlace>());
    }
}
