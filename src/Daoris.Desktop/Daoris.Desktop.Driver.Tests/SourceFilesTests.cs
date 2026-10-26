namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A source a test reads is found by its name wherever it lies under its project (STRUCT1, D166; the code-structure design
/// §6): the driver's files move into module folders one module per branch, and a reader that named a folder would break
/// at each move. A name is one file, or the read is refused, since a name found twice says neither.
/// </summary>
public sealed class SourceFilesTests
{
    [Fact]
    public void A_name_found_once_is_that_file_wherever_it_lies()
    {
        var deep = Path.Combine("driver", "Trees", "Landing.cs");

        Assert.Equal(deep, SourceFiles.Single(SourceFiles.DriverProject, "Landing.cs", [deep]));
    }

    [Fact]
    public void A_name_found_nowhere_is_refused_with_the_project_it_was_looked_for_in()
    {
        var refused = Assert.Throws<InvalidOperationException>(() => SourceFiles.Single(SourceFiles.DriverProject, "Gone.cs", []));

        Assert.Equal("no Gone.cs under Daoris.Desktop.Driver: a source read by name must be there, at any depth", refused.Message);
    }

    [Fact]
    public void A_name_found_twice_is_refused_with_both_places()
    {
        string[] both = [Path.Combine("driver", "Plugins", "Hooks.cs"), Path.Combine("driver", "Trees", "Hooks.cs")];

        var refused = Assert.Throws<InvalidOperationException>(() => SourceFiles.Single(SourceFiles.DriverProject, "Hooks.cs", both));

        Assert.Equal($"Hooks.cs is under Daoris.Desktop.Driver 2 times, so its name does not say which: {both[0]}, {both[1]}", refused.Message);
    }

    [Fact]
    public void The_driver_s_sources_are_found_at_any_depth_and_the_build_s_output_is_none_of_them()
    {
        var driver = SourceFiles.Project(SourceFiles.DriverProject);

        Assert.Equal(Path.Combine(driver, "Events", "NoteCodes.cs"), SourceFiles.Driver("NoteCodes.cs"));
        Assert.Equal(Path.Combine(driver, "Help", "Proposals", "HelpGoProposals.cs"), SourceFiles.Driver("HelpGoProposals.cs"));
        Assert.Equal(Path.Combine(SourceFiles.Project("Daoris.Desktop.Modules"), "Loop", "DriverLoop.cs"), SourceFiles.Find("Daoris.Desktop.Modules", "DriverLoop.cs"));

        var all = SourceFiles.All(SourceFiles.DriverProject, "*.cs").ToList();
        Assert.Contains(Path.Combine(driver, "Help", "Room", "HelpRoomAsks.cs"), all);
        // The build writes sources of its own under obj/ (the assembly's attributes, the global usings): never a reader's.
        Assert.True(Directory.Exists(Path.Combine(driver, "obj")), "the driver is built before its tests run");
        Assert.DoesNotContain(all, path => Path.GetRelativePath(driver, path).Split(Path.DirectorySeparatorChar)[0] is "bin" or "obj");
        Assert.Equal(all.Order(StringComparer.Ordinal), all);
    }

    [Fact]
    public void The_CLI_s_sources_are_found_in_their_module_folders_and_a_command_row_is_not_its_handler()
    {
        var cli = Path.Combine(WorkspaceRoot.Folder, "src", "Daoris.Cli", "src");

        Assert.Equal(Path.Combine(cli, "platform", "driverprogram.ts"), SourceFiles.Cli("driverprogram.ts"));
        // `cli/analyze.ts` is the verb's row, which shares its name with the module that handles it (STRUCT5d).
        Assert.Equal(Path.Combine(cli, "doctrine", "analyze.ts"), SourceFiles.Cli("analyze.ts"));
        var refused = Assert.Throws<InvalidOperationException>(() => SourceFiles.Cli("gone.ts"));
        Assert.Equal("no gone.ts under Daoris.Cli: a source read by name must be there, at any depth", refused.Message);
    }
}
