namespace Daoris.Service.Tests;

/// <summary>A test's temporary folder, and so every scratch a test roots there, lies under the checkout's <c>_fixtures</c>.</summary>
public sealed class ScratchTempTests
{
    [Fact]
    public void The_temporary_folder_is_the_checkout_s_scratch()
    {
        var temp = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath()));
        var marker = Path.Combine("_fixtures", "test-homes", "temp");

        Assert.EndsWith(Path.DirectorySeparatorChar + marker, temp, StringComparison.OrdinalIgnoreCase);
        Assert.True(Path.Exists(Path.Combine(temp[..^marker.Length], ".git")), $"{temp} lies in no checkout");
        Assert.True(Directory.Exists(temp));
    }
}
