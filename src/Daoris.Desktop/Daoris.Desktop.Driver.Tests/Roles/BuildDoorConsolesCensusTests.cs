using System.Text.RegularExpressions;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The terminal's consoles read the machine's door as the loop, set-up and the window do, through
/// <c>SetupWorld.Unadopted</c>. It walks the host's sources, so it is a census of its own beside <see cref="BuildDoorTests"/>.
/// </summary>
[Trait(Category.Name, Category.Census)]
public sealed class BuildDoorConsolesCensusTests
{
    // The terminal's consoles once asked Adoption.Unadopted of Build's first agent alone; the loop, set-up and the window ask the whole order.
    [Fact]
    public void No_terminal_console_asks_a_door_of_its_own_but_reads_Builds_whole_order_through_set_up()
    {
        var own = SourceFiles.All("Daoris.Desktop.Driver.Host", "*.cs")
            .Where(path => Regex.IsMatch(File.ReadAllText(path), @"\bAdoption\s*\.\s*Unadopted\s*\(|\bSessionsConsole\s*\.\s*Unadopted\s*\(|catch\s*\(DriverException[^)]*UnknownDoorException"))
            .Select(Path.GetFileName)
            .ToList();

        Assert.True(own.Count == 0, "a console reads BuildDoor through SetupWorld.Unadopted, and lets an unknown door refuse:\n" + string.Join('\n', own));
    }
}
