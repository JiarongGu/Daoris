using Daoris.Desktop.Driver.Tests;
using System.Text.Json.Nodes;

namespace Daoris.Driver.Tests.Platform;

/// <summary>
/// What This Daoris lists as not Daoris's: only a name no entry of the table holds at the folder the list walks, so the
/// stores a live install keeps are never in it, and nothing that removes a listed entry can reach one.
/// </summary>
[Trait(Desktop.Driver.Tests.Category.Name, Desktop.Driver.Tests.Category.Census)]
public sealed class WiringLeftOverTests : IDisposable
{
    private readonly string _root = Path.Combine(TestHomes.Root, "daoris-leftover-tests", Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
            // A scratch folder something still holds is left for the OS to clean.
        }
    }

    private static readonly string[] Files =
    [
        "desktop.db", "desktop.db-shm", "desktop.db-wal", "desktop.db.wake", "knowledge.db", "knowledge.db-wal", "memory.db", "memory.db-shm", "memory.db-wal",
        "files-found.json", "last-build.json", "migration.json", "overlaps-told.json", "agent-offers.json", "usage.json",
    ];

    private static readonly string[] Folders = ["pipe-waiting", "salvage"];

    private static void Lay(string home)
    {
        Directory.CreateDirectory(home);
        foreach (var file in Files) File.WriteAllText(Path.Combine(home, file), "x");
        foreach (var folder in Folders)
        {
            Directory.CreateDirectory(Path.Combine(home, folder));
            File.WriteAllText(Path.Combine(home, folder, "one.json"), "x");
        }
    }

    private static List<JsonObject> Listed(string home, string driver) =>
    [
        .. SystemWiring.Read(new WiringWorld
        {
            Home = home,
            BaseDirectory = driver,
            Process = _ => null,
            Account = _ => null,
            Machine = _ => null,
            Lives = (_, _) => false,
            AskStatus = _ => null,
            Sizes = new WiringSizes(),
        })["leftOver"]!.AsArray().Select(row => row!.AsObject()),
    ];

    [Fact]
    public void An_installs_live_home_lists_nothing_left_over_and_a_stranger_and_an_earlier_build_are_listed()
    {
        var install = Path.Combine(_root, "install");
        var driver = Path.Combine(install, "app", "daoris-driver");
        Directory.CreateDirectory(driver);
        File.WriteAllText(Path.Combine(install, DaorisHome.InstallMarker), "# Daoris\n");
        var home = Path.Combine(install, DaorisHome.InstallData);
        Lay(home);
        Assert.Empty(Listed(home, driver));

        Directory.CreateDirectory(Path.Combine(home, "stranger"));
        Directory.CreateDirectory(Path.Combine(home, "webview2"));
        var listed = Listed(home, driver).ToDictionary(row => (string)row["name"]!);
        Assert.Equal(["stranger", "webview2"], listed.Keys.OrderBy(name => name, StringComparer.Ordinal));
        Assert.Equal("not-daoris", (string?)listed["stranger"]["why"]);
        Assert.Equal("earlier-build", (string?)listed["webview2"]["why"]);
    }

    [Fact]
    public void A_plain_home_holds_the_names_its_data_folder_carries()
    {
        var home = Path.Combine(_root, "plain");
        Lay(Path.Combine(home, "data"));
        Assert.Empty(Listed(home, home));
    }

    [Fact]
    public void The_press_that_frees_an_earlier_builds_leavings_names_no_name_the_table_holds()
    {
        var home = Path.Combine(_root, "press");
        Lay(home);
        var held = HomeEntry.Table.SelectMany(entry => DaorisHome.Places(home, entry)).Select(Path.GetFullPath).ToList();
        foreach (var path in SpaceKinds.Places(home, "left-over").Places.Select(Path.GetFullPath))
        {
            Assert.DoesNotContain(held, place => place.Equals(path, StringComparison.OrdinalIgnoreCase) || place.StartsWith(path + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));
        }
    }
}
