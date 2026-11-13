using System.Text.RegularExpressions;

namespace Daoris.Desktop.Driver.Tests;

public sealed class StoreReferencesTests
{
    private static string Read(params string[] path) => File.ReadAllText(Path.Combine([WorkspaceRoot.Folder, .. path]));

    [Fact]
    public void The_driver_references_the_contracts_and_the_ports_and_no_adapter()
    {
        var driver = Read("src", "Daoris.Desktop", "Daoris.Desktop.Driver", "Daoris.Desktop.Driver.csproj");

        Assert.Equal(
            ["../../Daoris.Contracts/Daoris.Contracts.csproj", "../../Daoris.Ports/Daoris.Ports.csproj"],
            Regex.Matches(driver, "ProjectReference Include=\"([^\"]+)").Select(m => m.Groups[1].Value));
    }

    [Fact]
    public void The_sqlite_adapter_pins_its_packages_at_the_versions_the_desktop_tree_holds()
    {
        var desktop = Read("src", "Daoris.Desktop", "Directory.Packages.props");
        var adapter = Read("src", "Daoris.Adapters", "Daoris.Adapters.DesktopStore.Sqlite", "Daoris.Adapters.DesktopStore.Sqlite.csproj");

        foreach (var package in new[] { "Microsoft.Data.Sqlite", "SQLitePCLRaw.bundle_e_sqlite3" })
        {
            var held = new Regex($"""<PackageVersion Include="{Regex.Escape(package)}" Version="([^"]+)" />""").Match(desktop).Groups[1].Value;
            Assert.NotEmpty(held);
            Assert.Contains($"""<PackageReference Include="{package}" Version="{held}" />""", adapter);
        }
    }
}
