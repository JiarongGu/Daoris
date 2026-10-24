namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// KEEPSIZE1b3: the host's build failing fails the case, and with the build's own last lines. The build of a project that is not there
/// ends at once and writes nothing, so no host is built here; the success is every case of <c>KeepSizeServiceProcessTests</c>.
/// </summary>
[Trait(Category.Name, Category.Process)]
public sealed class RealServiceHostBuildTests
{
    [Fact]
    public async Task A_build_that_fails_throws_with_what_the_build_said()
    {
        var absent = Path.Combine(AppContext.BaseDirectory, "absent-host");

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(
            () => RealServiceHost.BuildAsync(Path.Combine(absent, "Missing.csproj"), "Debug", Path.Combine(absent, "artifacts")));

        Assert.Contains("exited", thrown.Message);
        Assert.Contains("Missing.csproj", thrown.Message);
        Assert.Contains("MSB1009", thrown.Message);
        Assert.False(Directory.Exists(absent));
    }
}
