using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>The hour a limit naming no time cools for (FOUNDATION1 mod-iii): the settings' default, which the account limits answer through.</summary>
public sealed class CoolOffDefaultTests
{
    [Fact]
    public void The_default_is_the_hour_and_the_limits_and_the_config_read_it()
    {
        Assert.Equal(TimeSpan.FromMinutes(60), CoolOffDefault.Value);
        Assert.Equal(CoolOffDefault.Value, AccountLimits.DefaultCoolOff);
        Assert.Equal(CoolOffDefault.Value, DriverConfig.Empty.CoolOff);
        Assert.Equal(TimeSpan.FromMinutes(5), DriverConfig.Empty.WithCoolOff(5).CoolOff);
    }
}
