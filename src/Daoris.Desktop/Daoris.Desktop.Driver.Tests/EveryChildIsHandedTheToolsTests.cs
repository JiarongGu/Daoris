using System.Text.RegularExpressions;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// One answer for Daoris and every child (TOOLS5, D121 §2.4): every process the driver starts is handed the tools'
/// environment, or names why not, and none is started by a bare name in its initializer. A source scan rather than
/// a unit test, as <see cref="NoConsoleWindowTests"/> holds <c>CreateNoWindow</c>: the property is one every spawn
/// site must carry, and the next one written will not know to.
/// </summary>
/// <remarks>
/// <para><b>Handed</b> is a call to <c>Tools.Hand(</c> after the initializer, before the start's first
/// <c>return</c> or <c>Process.Start(</c>. <b>Why not</b> is a comment directly above the statement that says
/// <c>Not the tools' environment (TOOLS5):</c> and gives the reason — a Daoris program, the system's own.</para>
/// <para>The terminal builds its environment block for <c>CreateProcessW</c> itself, so it is held by name: its
/// block is built through <c>Tools.ChildEnvironment(</c>. The modules hold their own starts in
/// <c>EveryModuleChildIsHandedTheToolsTests</c>.</para>
/// </remarks>
public sealed class EveryChildIsHandedTheToolsTests
{
    private static readonly string[] Projects = ["Daoris.Desktop.Driver", "Daoris.Desktop.Driver.Host"];

    internal const string Exempt = "Not the tools' environment (TOOLS5):";

    [Fact]
    public void Every_process_the_driver_starts_is_handed_the_tools_environment_or_says_why_not()
    {
        var root = SourceRoot();
        var (spawns, missing) = Scan(root, Projects);

        // Spawning's shell, the probe, an agent action, a hook, and git's one start (TOOLS5 made git's two one).
        Assert.True(spawns >= 5, $"expected to find the driver's spawn sites, found {spawns}");
        Assert.True(missing.Count == 0, "a start that is not handed the tools' environment runs whatever PATH it inherited:\n"
            + string.Join('\n', missing));
    }

    [Fact]
    public void No_start_names_its_program_by_a_bare_name()
    {
        var bare = new List<string>();
        foreach (var (file, initializer, offset) in Spawns(SourceRoot(), Projects))
        {
            if (Regex.IsMatch(initializer, @"FileName\s*=\s*""")) bare.Add($"{file} at offset {offset}");
        }

        Assert.True(bare.Count == 0, "a bare name is whatever PATH finds first, never the file Tools resolves:\n" + string.Join('\n', bare));
    }

    [Fact]
    public void The_terminals_environment_block_is_built_through_the_tools()
    {
        var source = File.ReadAllText(Path.Combine(SourceRoot(), "Daoris.Desktop.Driver", "PseudoConsole.cs"));

        Assert.Contains("EnvironmentBlock(launch.Environment)", source);
        var block = source[source.IndexOf("static string EnvironmentBlock(", StringComparison.Ordinal)..];
        block = block[..block.IndexOf("return block", StringComparison.Ordinal)];
        Assert.Contains("Tools.ChildEnvironment(", block);
    }

    /// <summary>The scan, shared with the modules' twin of this test: each start, and those neither handed nor excused.</summary>
    internal static (int Spawns, List<string> Missing) Scan(string root, IEnumerable<string> projects)
    {
        var spawns = 0;
        var missing = new List<string>();
        foreach (var project in projects)
        {
            var folder = Path.Combine(root, project);
            if (!Directory.Exists(folder)) continue;

            foreach (var file in Directory.EnumerateFiles(folder, "*.cs", SearchOption.AllDirectories))
            {
                if (Built(file)) continue;
                var source = File.ReadAllText(file).Replace("\r\n", "\n");
                foreach (Match spawn in Regex.Matches(source, @"new ProcessStartInfo\b"))
                {
                    spawns++;
                    // Comments out first: a call commented out, or a sentence naming it, is not a call.
                    var after = Regex.Replace(source[spawn.Index..], @"//[^\n]*", "");
                    var end = Regex.Match(after, @"\breturn\b|Process\.Start\(");
                    var handed = (end.Success ? after[..end.Index] : after).Contains("Tools.Hand(", StringComparison.Ordinal);
                    if (!handed && !Excused(source, spawn.Index))
                    {
                        missing.Add($"{Path.GetRelativePath(root, file)} at offset {spawn.Index}");
                    }
                }
            }
        }

        return (spawns, missing);
    }

    /// <summary>Whether the comment lines directly above the statement say why the tools' environment is not handed.</summary>
    private static bool Excused(string source, int at)
    {
        var lines = source[..at].Split('\n');
        var comment = new List<string>();
        // The statement's own line is the last; above it, the contiguous comment lines.
        for (var index = lines.Length - 2; index >= 0 && lines[index].TrimStart().StartsWith("//", StringComparison.Ordinal); index--)
        {
            comment.Add(lines[index]);
        }

        return string.Join('\n', comment).Contains(Exempt, StringComparison.Ordinal);
    }

    private static IEnumerable<(string File, string Initializer, int Offset)> Spawns(string root, IEnumerable<string> projects)
    {
        foreach (var project in projects)
        {
            var folder = Path.Combine(root, project);
            if (!Directory.Exists(folder)) continue;

            foreach (var file in Directory.EnumerateFiles(folder, "*.cs", SearchOption.AllDirectories))
            {
                if (Built(file)) continue;
                var source = File.ReadAllText(file);
                foreach (Match spawn in Regex.Matches(source, @"new ProcessStartInfo\b"))
                {
                    var end = Regex.Match(source[spawn.Index..], @"\}\s*[;)]");
                    yield return (Path.GetRelativePath(root, file), end.Success ? source.Substring(spawn.Index, end.Index) : "", spawn.Index);
                }
            }
        }
    }

    private static bool Built(string file) =>
        file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
        || file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}");

    internal static string SourceRoot()
    {
        var folder = new DirectoryInfo(AppContext.BaseDirectory);
        while (folder is not null && !Directory.Exists(Path.Combine(folder.FullName, "Daoris.Desktop.Driver")))
        {
            folder = folder.Parent;
        }

        return folder?.FullName
            ?? throw new InvalidOperationException("the desktop source tree was not found above the test binary");
    }
}
