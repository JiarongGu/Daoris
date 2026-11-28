namespace Daoris.Service.Tests;

/// <summary>
/// The categories a run filters on, as the HTTP host's tests name them. A class, or the one test of a unit class, whose claim is a real
/// dependency (git on a checkout, the machine's file watchers, a started executable, this repository's own tree) carries <c>[Trait(Category.Name, Category.Process)]</c>:
/// a unit test keeps to the second the layers census gives it, so the service gate runs <c>Category!=Process</c> and the
/// <c>service-process</c> gate runs these after, with <c>src/Daoris.Desktop/process.runsettings</c>.
/// </summary>
internal static class Category
{
    public const string Name = "Category";

    public const string Process = "Process";

    /// <summary>A class that reads the service's sources by text: run with <c>--filter Category=Census</c>, as the desktop's censuses are.</summary>
    public const string Census = "Census";
}
