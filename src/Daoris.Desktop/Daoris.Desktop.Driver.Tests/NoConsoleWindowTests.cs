using System.Text.RegularExpressions;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The desktop is a window, and a console child of a windowed process is given a console of its own
/// unless the spawn says otherwise. Nothing said otherwise for git, the harness probes or the
/// sessions, so the deployed shell flashed a terminal onto the desktop on every tick (owner,
/// 2026-09-23). A source scan rather than a unit test, because the property is one every spawn site
/// must carry and the next one written will not know to.
/// </summary>
public sealed class NoConsoleWindowTests
{
    private static readonly string[] Projects =
        ["Daoris.Desktop.Driver", "Daoris.Desktop.Driver.Host", "Daoris.Desktop.Modules", "Daoris.Desktop.App"];

    [Fact]
    public void Every_process_the_desktop_starts_is_started_without_a_console_window()
    {
        var root = SourceRoot();
        var spawns = 0;
        var missing = new List<string>();

        foreach (var project in Projects)
        {
            var folder = Path.Combine(root, project);
            if (!Directory.Exists(folder)) continue;

            foreach (var file in Directory.EnumerateFiles(folder, "*.cs", SearchOption.AllDirectories))
            {
                if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                    || file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
                {
                    continue;
                }

                var source = File.ReadAllText(file);
                foreach (Match spawn in Regex.Matches(source, @"new ProcessStartInfo\b"))
                {
                    spawns++;
                    // The initializer runs to its closing brace — `};` as a statement, `})` inline in
                    // a call. A spawn without one has no chance to say it.
                    var end = Regex.Match(source[spawn.Index..], @"\}\s*[;)]");
                    var initializer = end.Success ? source.Substring(spawn.Index, end.Index) : "";
                    if (!initializer.Contains("CreateNoWindow = true", StringComparison.Ordinal))
                    {
                        missing.Add($"{Path.GetRelativePath(root, file)} at offset {spawn.Index}");
                    }
                }
            }
        }

        Assert.True(spawns >= 6, $"expected to find the desktop's spawn sites, found {spawns}");
        Assert.True(missing.Count == 0, "a spawn without CreateNoWindow opens a console from the window:\n" + string.Join('\n', missing));
    }

    private static string SourceRoot()
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
