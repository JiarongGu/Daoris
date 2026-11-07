using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// <c>cap</c> in <c>driver.json</c> set from the window: how many sessions Daoris starts at
/// once on this machine. A whole number of at least 1; the file's read already clamps, the setter refuses.
/// </summary>
public sealed class CapSetTests
{
    [Fact]
    public void A_cap_set_survives_the_file_and_keeps_the_other_settings()
    {
        var set = DriverConfig.Empty.WithDrivable("engine", true).WithCap(5);

        var read = DriverConfig.Parse(set.ToJson());

        Assert.Equal(5, read.Cap);
        Assert.Equal(["engine"], read.Drivable);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Less_than_one_is_refused_since_no_session_would_ever_start(int cap)
    {
        var refused = Assert.Throws<DriverException>(() => DriverConfig.Empty.WithCap(cap));

        Assert.Contains("at least 1", refused.Message, StringComparison.Ordinal);
    }
}
