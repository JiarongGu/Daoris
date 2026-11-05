namespace Daoris.Service.Http.Tests;

/// <summary>
/// The categories a run filters on, as the desktop's suites name them (MOD8). A class that holds a wall-clock budget on a real host
/// over a home of real size carries <c>[Trait(Category.Name, Category.Process)]</c>: it is the one a busy machine fails, so the service
/// gate runs <c>Category!=Process</c> and the <c>service-process</c> gate runs it alone, with <c>src/Daoris.Desktop/process.runsettings</c>.
/// </summary>
internal static class Category
{
    public const string Name = "Category";

    public const string Process = "Process";
}
