using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>The home purge is a door the usage names, routed by the host to the library's words, which <c>HomePurgeTests</c> holds.</summary>
public sealed class HomePurgeUsageTests
{
    [Fact]
    public void The_usage_names_the_home_purge_and_the_host_routes_it_to_the_librarys_words()
    {
        Assert.Contains("\n  home purge --history --plan | --yes\n", DriverCommand.Usage.ReplaceLineEndings("\n"));
        Assert.StartsWith("usage: daoris home purge --history --plan", HomePurgeCommand.Usage);

        var program = HostProgramSource.Read(Path.Combine(EveryChildIsHandedTheToolsTests.SourceRoot(), "Daoris.Desktop.Driver.Host"));
        Assert.Contains("if (args is [\"home\", .. var homeArgs])", program);
        Assert.Contains("HomePurgeCommand.Run(", program);
    }
}
