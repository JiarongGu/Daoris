using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>Which reads may be ended once nobody waits: a read that takes no lock and writes no file.</summary>
public sealed class GitEndableTests
{
    [Fact]
    public void A_read_told_to_write_its_output_to_a_file_is_not_endable()
    {
        Assert.True(GitEndable.Of(["rev-parse", "HEAD"]));
        Assert.False(GitEndable.Of(["--no-optional-locks", "diff", "--output=patch.diff", "A...B"]));
        Assert.False(GitEndable.Of(["log", "--output", "log.txt"]));
    }
}
