using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>A local remote is answered by its folder's name, never a machine path (the route's no-machine-path rule).</summary>
public sealed class GitRemoteLocalNameTests
{
    [Theory]
    [InlineData("D:\\code\\remotes\\engine.git", "engine")]
    [InlineData("/srv/code/engine/", "engine")]
    [InlineData("file:///srv/git/engine.git", "engine")]
    [InlineData("../sibling", "sibling")]
    public void A_local_remote_is_its_folders_name(string address, string name)
    {
        var said = GitRemoteRows.LocalName(address);

        Assert.Equal(name, said);
        Assert.DoesNotContain("code", said);
        Assert.DoesNotContain("/", said);
    }
}
