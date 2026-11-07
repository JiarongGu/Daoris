namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A run's <c>land</c> is the one way to land. The window's press, a chat's press and the terminal's <c>trees land</c>
/// land nothing in place and judge no result of their own for a landing: they keep the review's word for the run's <c>land</c>, which
/// the performer lands at its pass, or say what holds the work (<see cref="Daoris.Driver.RunLandHolds"/>). Read from the sources by name.
/// </summary>
public sealed class AcceptDoorsTests
{
    [Fact]
    public void No_accept_door_lands_in_place_and_the_terminal_keeps_the_word_for_the_pass()
    {
        var console = TreesConsoleSource.Read(Path.Combine(SourceRoot(), "Daoris.Desktop.Driver.Host"));
        var route = File.ReadAllText(Path.Combine(SourceRoot(), "Daoris.Desktop.Modules", "Bridge", "Routes", "TreesLandDesk.cs"));
        var chat = File.ReadAllText(SourceFiles.Driver("ChatLandCommand.cs"));

        foreach (var door in new[] { console, route, chat })
        {
            Assert.DoesNotContain(".LandAsync(tree, subject", door);
            Assert.DoesNotContain("quests.PressedAsync(", door);
            Assert.Contains("RunLandHolds.Of(gate)", door == console ? File.ReadAllText(SourceFiles.Driver("TreesWords.cs")) : door);
        }

        Assert.Contains("TreesWords.AcceptedForPass(home, gate)", console);
        Assert.Contains("TreesWords.AcceptedForPass(world.Home, gate)", chat);
    }

    private static string SourceRoot()
    {
        var folder = new DirectoryInfo(AppContext.BaseDirectory);
        while (folder is not null && !Directory.Exists(Path.Combine(folder.FullName, "Daoris.Desktop.Driver.Host"))) folder = folder.Parent;
        return folder?.FullName ?? throw new InvalidOperationException("the desktop's source folder was not found");
    }
}
