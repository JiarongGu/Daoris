using System.Text.RegularExpressions;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The driver's config is resolved once, by <see cref="DriverConfig"/>, for every door. The one file is
/// the home's <c>driver.json</c>: no variable names another file, so a run that wants other choices runs on a
/// home of its own.
/// </summary>
[Trait(Category.Name, Category.Census)]
public sealed partial class ConfigSeamTests
{
    private static Func<string, string?> Env(params (string Name, string? Value)[] pairs) =>
        name => pairs.FirstOrDefault(p => p.Name == name).Value;

    /// <summary>The retired variable, spelled here alone so a test can name it to the resolution.</summary>
    private const string Retired = "DAORIS_DRIVER_CONFIG";

    /// <summary>A home that need not exist: the resolution is a question about paths, never about the disk.</summary>
    private static readonly string Home = Path.Combine(AppContext.BaseDirectory, "seam", "data");

    private static readonly string Elsewhere = Path.Combine(AppContext.BaseDirectory, "seam", "other");

    [Fact]
    public void A_door_holding_a_home_reads_that_homes_driver_json()
    {
        Assert.Equal(DaorisHome.Entry(Home, HomeEntry.DriverConfig), DriverConfig.ResolvePath(Home));
    }

    /// <summary>The retired variable names nothing the resolution reads, with a home or without one.</summary>
    [Fact]
    public void ONEWAY1c_i_the_retired_variable_is_read_nowhere()
    {
        var named = Path.Combine(Elsewhere, "landing.json");

        Assert.Equal(DaorisHome.Entry(Home, HomeEntry.DriverConfig), DriverConfig.FindPath(Env((Retired, named), (DaorisHome.Variable, Home))));
        Assert.Null(DriverConfig.FindPath(Env((Retired, named))));
        Assert.Equal(Home, DriverConfig.FindHome(Env((Retired, named), (DaorisHome.Variable, Home))));
        Assert.Null(DriverConfig.FindHome(Env((Retired, named))));
    }

    /// <summary>The one resolution, for a door with no home in hand: the home's file, then nothing to read.</summary>
    [Fact]
    public void The_machines_file_is_the_homes_then_none()
    {
        Assert.Equal(DaorisHome.Entry(Home, HomeEntry.DriverConfig), DriverConfig.FindPath(Env((DaorisHome.Variable, Home))));
        Assert.Null(DriverConfig.FindPath(Env()));
    }

    /// <summary>
    /// The home a verb takes the lease in is the home the loop drives, the folder the machine's
    /// file is in, found without reading or making anything.
    /// </summary>
    [Fact]
    public void The_home_a_verb_takes_the_lease_in_is_the_one_the_loop_drives()
    {
        Func<string, string?>[] environments =
        [
            Env((DaorisHome.Variable, Home)),
            Env((DaorisHome.Variable, Home + Path.DirectorySeparatorChar)),
        ];

        Assert.Equal(Home, DriverConfig.FindHome(environments[0]));
        foreach (var environment in environments)
        {
            Assert.Equal(HomeLease.PathIn(DriverConfig.HomeOf(DriverConfig.FindPath(environment)!)), HomeLease.PathIn(DriverConfig.FindHome(environment)!));
        }

        // Blank is none: with no home, nothing to hold.
        Assert.Null(DriverConfig.FindHome(Env()));
        Assert.Null(DriverConfig.FindHome(Env((DaorisHome.Variable, "  "))));
    }

    /// <summary>
    /// The seam held by a scan, as the spawn sites' are (<see cref="NoConsoleWindowTests"/>): the property is one every reader must
    /// carry, and the next reader written will not know to. Outside <c>DriverConfig.cs</c> no source names the file or the default
    /// path, so a reader can only come through the one resolution; and no source at all spells the retired variable. The
    /// home's table (<c>DaorisHome.cs</c>) names the file as its <c>driver</c> entry's place, which only DriverConfig asks for.
    /// </summary>
    [Fact]
    public void Every_reader_of_the_drivers_config_resolves_it_through_the_one_function()
    {
        var root = SourceRoot();
        var scanned = 0;
        var bypasses = new List<string>();
        string? owner = null;
        // The owner found by name, wherever the driver keeps it.
        var owning = Path.GetFullPath(SourceFiles.Driver("DriverConfig.cs"));

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
                if (string.Equals(Path.GetFullPath(file), owning, StringComparison.OrdinalIgnoreCase))
                {
                    owner = source;
                    continue;
                }

                // The home's table names the file as an entry's place.
                if (project == "Daoris.Desktop.Driver" && Path.GetFileName(file) == "DaorisHome.cs") continue;

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
            "a reader of the driver's config resolved it itself; ask DriverConfig.ResolvePath() (or ResolvePath(home), FindPath()) instead:\n"
            + string.Join('\n', bypasses));
        Assert.DoesNotContain(Retired, owner!, StringComparison.Ordinal);
    }

    /// <summary>Every project that can reach the config: the launcher references nothing but the staged build's one file.</summary>
    private static readonly string[] Projects =
        ["Daoris.Desktop.Driver", "Daoris.Desktop.Driver.Host", "Daoris.Desktop.Modules", "Daoris.Desktop.App"];

    /// <summary>The file's name, the retired variable and the default path, each the start of a resolution of its own.</summary>
    [GeneratedRegex(@"""driver\.json""|DAORIS_DRIVER_CONFIG|DriverConfig\.DefaultPath\b")]
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
