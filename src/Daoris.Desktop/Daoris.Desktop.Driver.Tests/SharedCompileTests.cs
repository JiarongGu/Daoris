namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The structure classes and the declarations' censuses read one compile of each shipping project's sources a run: the compile and
/// its binding are the censuses' cost, and a second compile of the same sources doubled it under every other class's load. The case
/// asks for the compilations made, not bound, so it waits on no walk.
/// </summary>
[Trait(Category.Name, Category.Census)]
public sealed class SharedCompileTests
{
    [Fact]
    public void The_driver_s_tree_and_the_declarations_censuses_compile_each_project_once()
    {
        var driver = DriverTree.Compiled.Value.Compilation;

        Assert.Same(DesktopCompilations.Of(SourceFiles.DriverProject), driver);
        Assert.True(DesktopCompilations.Compiles(SourceFiles.DriverProject) == 1,
            $"the driver's sources were compiled {DesktopCompilations.Compiles(SourceFiles.DriverProject)} times");
    }
}
