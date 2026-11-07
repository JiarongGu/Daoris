namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The person is taught one command, <c>daoris</c> (D228): no usage, refusal, brief, guide or comment of the desktop's program says
/// <c>daoris-driver</c>. The name stays only where it is the file the program is built as, the folder an install carries it in, or the
/// identity a protocol hello names; each such site is listed here with its count, so a new one is a decision.
/// </summary>
[Trait(Category.Name, Category.Census)]
public sealed class DaorisDriverWordsTests
{
    private const string Word = "daoris-driver";

    // The program's file and folder (a path, a process start), a hello's client name and the process an asker is named by.
    private static readonly Dictionary<string, int> Named = new(StringComparer.Ordinal)
    {
        ["src/Daoris.Desktop/Daoris.Desktop.Driver/Agents/Acp.cs"] = 1,
        ["src/Daoris.Desktop/Daoris.Desktop.Driver/Agents/Providers/Codex/CodexUsage.cs"] = 1,
        ["src/Daoris.Desktop/Daoris.Desktop.Driver/Agents/Providers/Codex/Layer/CodexUsageLayer.cs"] = 1,
        ["src/Daoris.Desktop/Daoris.Desktop.Driver/Instance/InstanceClient.cs"] = 1,
        ["src/Daoris.Desktop/Daoris.Desktop.Driver/Loop/SystemWiring.cs"] = 2,
        ["src/Daoris.Desktop/Daoris.Desktop.Driver/Platform/CommandWords.cs"] = 1,
        ["src/Daoris.Desktop/Daoris.Desktop.Driver/Platform/DaorisHome.cs"] = 1,
        ["src/Daoris.Desktop/Daoris.Desktop.Driver/Plugins/Relay/RelayCommand.cs"] = 3,
        ["src/Daoris.Desktop/Daoris.Desktop.Driver/Plugins/WorkEventPlugins.cs"] = 2,
        ["src/Daoris.Desktop/Daoris.Desktop.Modules/Platform/PipeCensus.cs"] = 1,
        ["docs/guide/README.md"] = 1,
    };

    [Fact]
    public void No_taught_word_of_the_programs_says_its_file_name()
    {
        var root = GitSeed.RepoRoot();
        var found = new SortedDictionary<string, int>(StringComparer.Ordinal);
        foreach (var file in Scanned(root))
        {
            var count = File.ReadAllText(file).Split(Word).Length - 1;
            if (count > 0) found[Path.GetRelativePath(root, file).Replace('\\', '/')] = count;
        }

        Assert.Equal(Named.OrderBy(pair => pair.Key, StringComparer.Ordinal), found.OrderBy(pair => pair.Key, StringComparer.Ordinal));
    }

    // Scanned beyond the program: the page's catalogues and sources (not its tests or stories, where a process is named as the
    // asker) and the canon's skills, which teach a session's words.
    private static IEnumerable<string> Scanned(string root)
    {
        var desktop = Path.Combine(root, "src", "Daoris.Desktop");
        foreach (var project in new[] { "Daoris.Desktop.Driver", "Daoris.Desktop.Driver.Host", "Daoris.Desktop.Modules" })
        {
            foreach (var file in Directory.EnumerateFiles(Path.Combine(desktop, project), "*.cs", SearchOption.AllDirectories))
            {
                var inBuild = file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                    || file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal);
                if (!inBuild) yield return file;
            }
        }

        foreach (var guide in Directory.EnumerateFiles(Path.Combine(root, "docs", "guide"), "*.md", SearchOption.AllDirectories)) yield return guide;
        foreach (var catalogue in Directory.EnumerateFiles(Path.Combine(root, "src", "Daoris.Web", "src", "locales"), "*.json", SearchOption.AllDirectories)) yield return catalogue;
        foreach (var page in Directory.EnumerateFiles(Path.Combine(root, "src", "Daoris.Web", "src"), "*.ts*", SearchOption.AllDirectories))
        {
            var test = page.Contains(".test.", StringComparison.Ordinal) || page.Contains(".stories.", StringComparison.Ordinal);
            if (!test && !page.Contains($"{Path.DirectorySeparatorChar}locales{Path.DirectorySeparatorChar}", StringComparison.Ordinal)) yield return page;
        }

        foreach (var skill in Directory.EnumerateFiles(Path.Combine(root, "canon"), "SKILL.md", SearchOption.AllDirectories)) yield return skill;
        foreach (var example in Directory.EnumerateFiles(Path.Combine(root, "examples"), "README.md", SearchOption.AllDirectories)) yield return example;
    }
}
