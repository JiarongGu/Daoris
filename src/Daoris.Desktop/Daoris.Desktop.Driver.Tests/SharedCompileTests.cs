namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The structure classes and the declarations' censuses read one compile of each shipping project's sources a run: the compile and
/// its binding are the censuses' cost, and a second compile of the same sources doubled it under every other class's load.
/// </summary>
[Trait(Category.Name, Category.Census)]
public sealed class SharedCompileTests
{
    [Fact]
    public void The_driver_s_tree_and_the_declarations_censuses_compile_each_project_once()
    {
        _ = DriverTree.Shared.Value;
        _ = DesktopCompilations.Each;

        Assert.All(DesktopCompilations.Shipping, project => Assert.True(DesktopCompilations.Compiles(project) == 1,
            $"{project}'s sources were compiled {DesktopCompilations.Compiles(project)} times"));
    }
}
