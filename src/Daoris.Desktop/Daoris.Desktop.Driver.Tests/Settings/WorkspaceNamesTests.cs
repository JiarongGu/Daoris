using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A workspace's name as the settings key it (FOUNDATION1 mod-iii): the settings' own reading, which the service's workspace map
/// answers through, so the two files spell a name alike.
/// </summary>
public sealed class WorkspaceNamesTests
{
    [Theory]
    [InlineData(null, "default")]
    [InlineData("", "default")]
    [InlineData("   ", "default")]
    [InlineData("work", "work")]
    [InlineData("  work  ", "work")]
    [InlineData("Work", "Work")]
    public void A_name_is_trimmed_and_the_default_when_unstated(string? name, string expected)
    {
        Assert.Equal(expected, WorkspaceNames.Of(name));
        Assert.Equal(expected, RemoteTarget.Workspace(name));
    }

    [Fact]
    public void The_default_is_the_service_maps_default()
    {
        Assert.Equal("default", WorkspaceNames.Default);
        Assert.Equal(WorkspaceNames.Default, RemoteTarget.DefaultWorkspace);
    }
}
