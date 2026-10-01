using System.Text.RegularExpressions;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// One answer for Daoris and every child (TOOLS5, D121 §2.4), the modules' half: every process the modules, the
/// application and the launcher start is handed the tools' environment, or names why not. The driver's twin of this
/// scan holds the driver's starts and the terminal (<c>EveryChildIsHandedTheToolsTests</c>); the modules start the
/// rest through the driver's library, which that scan holds.
/// </summary>
/// <remarks>
/// <para><b>Handed</b> is a call to <c>Tools.Hand(</c> after the initializer, before the start's first
/// <c>return</c> or <c>Process.Start(</c>. <b>Why not</b> is a comment directly above the statement that says
/// <c>Not the tools' environment (TOOLS5):</c> and gives the reason: today, the host and the application are
/// Daoris's own programs, and Edge is the system's.</para>
/// <para>A source scan, as <c>NoConsoleWindowTests</c> holds <c>CreateNoWindow</c>: the next start written will not
/// know to.</para>
/// </remarks>
public sealed class EveryModuleChildIsHandedTheToolsTests
{
    private static readonly string[] Projects = ["Daoris.Desktop.Modules", "Daoris.Desktop.App", "Daoris.Desktop.Launcher"];

    private const string Exempt = "Not the tools' environment (TOOLS5):";

    [Fact]
    public void Every_process_the_modules_start_is_handed_the_tools_environment_or_says_why_not()
    {
        var root = Path.Combine(RepositoryRoot(), "src", "Daoris.Desktop");
        var spawns = 0;
        var missing = new List<string>();

        foreach (var project in Projects)
        {
            foreach (var file in Directory.EnumerateFiles(Path.Combine(root, project), "*.cs", SearchOption.AllDirectories))
            {
                if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                    || file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
                {
                    continue;
                }

                var source = File.ReadAllText(file).Replace("\r\n", "\n");
                foreach (Match spawn in Regex.Matches(source, @"new ProcessStartInfo\b"))
                {
                    spawns++;
                    // Comments out first: a call commented out, or a sentence naming it, is not a call.
                    var after = Regex.Replace(source[spawn.Index..], @"//[^\n]*", "");
                    var end = Regex.Match(after, @"\breturn\b|Process\.Start\(");
                    var handed = (end.Success ? after[..end.Index] : after).Contains("Tools.Hand(", StringComparison.Ordinal);
                    if (!handed && !Excused(source, spawn.Index)) missing.Add($"{Path.GetRelativePath(root, file)} at offset {spawn.Index}");
                }
            }
        }

        // The host, Edge, and the application from the launcher.
        Assert.True(spawns >= 3, $"expected to find the modules' spawn sites, found {spawns}");
        Assert.True(missing.Count == 0, "a start that is not handed the tools' environment runs whatever PATH it inherited:\n"
            + string.Join('\n', missing));
    }

    /// <summary>Whether the comment lines directly above the statement say why the tools' environment is not handed.</summary>
    private static bool Excused(string source, int at)
    {
        var lines = source[..at].Split('\n');
        var comment = new List<string>();
        for (var index = lines.Length - 2; index >= 0 && lines[index].TrimStart().StartsWith("//", StringComparison.Ordinal); index--)
        {
            comment.Add(lines[index]);
        }

        return string.Join('\n', comment).Contains(Exempt, StringComparison.Ordinal);
    }

    private static string RepositoryRoot()
    {
        var at = new DirectoryInfo(AppContext.BaseDirectory);
        while (at is not null && !File.Exists(Path.Combine(at.FullName, "daoris.json"))) at = at.Parent;
        return at?.FullName ?? throw new InvalidOperationException("no workspace root above the test binary");
    }
}
