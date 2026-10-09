using System.Text.RegularExpressions;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// CONFIGSEAM1 (D63's per-file override): the driver's config is resolved once, by <see cref="DriverConfig"/>, for every door.
/// The loop, the planner and the terminal read the file <c>DAORIS_DRIVER_CONFIG</c> names, while the trees joined their home to
/// <c>driver.json</c> themselves, so a landing and the review's press ignored an override's landing and review rules: the planner sat
/// a review step that the landing never asked for, and the work merged into the line. A door that holds its home reads the
/// override where it is a file in that home, since every home is the folder its config sits in, and never another home's file.
/// </summary>
public sealed partial class ConfigSeamTests
{
    private static Func<string, string?> Env(params (string Name, string? Value)[] pairs) =>
        name => pairs.FirstOrDefault(p => p.Name == name).Value;

    /// <summary>A home that need not exist: the resolution is a question about paths, never about the disk.</summary>
    private static readonly string Home = Path.Combine(AppContext.BaseDirectory, "seam", "data");

    private static readonly string Elsewhere = Path.Combine(AppContext.BaseDirectory, "seam", "other");

    [Fact]
    public void An_override_of_another_name_in_the_home_is_the_file_a_door_holding_the_home_reads()
    {
        var chosen = Path.Combine(Home, "landing.json");

        Assert.Equal(chosen, DriverConfig.ResolvePath(Env((DriverConfig.PathVariable, chosen)), Home));
        // The home as a door may hold it: a trailing separator is the same folder.
        Assert.Equal(chosen, DriverConfig.ResolvePath(Env((DriverConfig.PathVariable, chosen)), Home + Path.DirectorySeparatorChar));
    }

    /// <summary>
    /// 🔴 Another home's file is never this home's choices: an override is what the machine's home was derived from, so one that
    /// names a file elsewhere is another machine's, or another test's, and a door holding this home reads its own.
    /// </summary>
    [Fact]
    public void An_override_in_another_home_is_never_this_homes_file()
    {
        Assert.Equal(
            Path.Combine(Home, "driver.json"),
            DriverConfig.ResolvePath(Env((DriverConfig.PathVariable, Path.Combine(Elsewhere, "driver.json"))), Home));
    }

    [Fact]
    public void With_no_override_a_home_reads_its_own_driver_json()
    {
        Assert.Equal(Path.Combine(Home, "driver.json"), DriverConfig.ResolvePath(Env(), Home));
        Assert.Equal(Path.Combine(Home, "driver.json"), DriverConfig.ResolvePath(Env((DaorisHome.Variable, Elsewhere)), Home));
        Assert.Equal(Path.Combine(Home, "driver.json"), DriverConfig.ResolvePath(Env((DriverConfig.PathVariable, "")), Home));
    }

    /// <summary>The one resolution, for a door with no home in hand: the override, then the home's file, then nothing to read.</summary>
    [Fact]
    public void The_machines_file_is_the_override_then_the_homes_then_none()
    {
        var chosen = Path.Combine(Elsewhere, "landing.json");

        Assert.Equal(chosen, DriverConfig.FindPath(Env((DriverConfig.PathVariable, chosen), (DaorisHome.Variable, Home))));
        Assert.Equal(Path.Combine(Home, "driver.json"), DriverConfig.FindPath(Env((DaorisHome.Variable, Home))));
        Assert.Null(DriverConfig.FindPath(Env()));
    }

    /// <summary>
    /// The seam held by a scan, as the spawn sites' are (<see cref="NoConsoleWindowTests"/>): the property is one every reader must
    /// carry, and the next reader written will not know to. Outside <c>DriverConfig.cs</c> no source names the file, the variable,
    /// or the default path, so a reader can only come through the one resolution; inside it, the variable is read once.
    /// </summary>
    [Fact]
    public void Every_reader_of_the_drivers_config_resolves_it_through_the_one_function()
    {
        var root = SourceRoot();
        var scanned = 0;
        var bypasses = new List<string>();
        string? owner = null;

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

                scanned++;
                var source = File.ReadAllText(file);
                if (Path.GetFileName(file) == "DriverConfig.cs" && Path.GetFileName(Path.GetDirectoryName(file)) == "Daoris.Desktop.Driver")
                {
                    owner = source;
                    continue;
                }

                foreach (Match bypass in Bypass().Matches(source))
                {
                    var line = source[..bypass.Index].Count(c => c == '\n') + 1;
                    bypasses.Add($"{Path.GetRelativePath(root, file)}:{line}  {bypass.Value}");
                }
            }
        }

        Assert.True(scanned >= 200, $"expected to scan the desktop's sources, scanned {scanned}");
        Assert.NotNull(owner);
        Assert.True(bypasses.Count == 0,
            "a reader of the driver's config resolved it itself, so an override's rules never reach it; "
            + "ask DriverConfig.ResolvePath() (or ResolvePath(home), FindPath()) instead:\n" + string.Join('\n', bypasses));
        Assert.Single(Regex.Matches(owner!, @"\(PathVariable\)"));
    }

    private static readonly string[] Projects =
    [
        "Daoris.Desktop.Driver", "Daoris.Desktop.Driver.Host", "Daoris.Desktop.Modules", "Daoris.Desktop.App", "Daoris.Desktop.Launcher",
    ];

    /// <summary>The file's name, the override's variable and the default path, each the start of a resolution of its own.</summary>
    [GeneratedRegex(@"""driver\.json""|""DAORIS_DRIVER_CONFIG""|DriverConfig\.PathVariable\b|DriverConfig\.DefaultPath\b")]
    private static partial Regex Bypass();

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
