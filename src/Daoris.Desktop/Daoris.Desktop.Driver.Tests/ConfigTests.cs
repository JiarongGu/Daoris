using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The driver's configuration is the person's standing choices (D46 §6/§7): which repositories this
/// machine may drive, what is held, how wide the loop runs. It is machine-local by nature and silence
/// is safe — an empty config drives nothing.
/// </summary>
public sealed class ConfigTests
{
    [Fact]
    public void An_empty_config_drives_nothing()
    {
        var config = DriverConfig.Parse("{}");

        Assert.Empty(config.Drivable);
        Assert.Empty(config.Holds);
        Assert.True(config.Cap >= 1);
    }

    [Fact]
    public void A_full_config_round_trips()
    {
        var config = DriverConfig.Parse("""
            {
              "drivable": ["Game", "Tools"],
              "holds": ["Tools"],
              "cap": 3,
              "adapter": "stub",
              "timeoutMinutes": 10,
              "pollSeconds": 5,
              "commands": { "stub": ["node", "local/agent.mjs"] }
            }
            """);

        Assert.Equal(["Game", "Tools"], config.Drivable);
        Assert.Equal(["Tools"], config.Holds);
        Assert.Equal(3, config.Cap);
        Assert.Equal("stub", config.Adapter);
        Assert.Equal(10, config.TimeoutMinutes);
        Assert.Equal(5, config.PollSeconds);
        Assert.Equal(["node", "local/agent.mjs"], config.Commands["stub"]);
    }

    /// <summary>A missing file is a machine that has not opted anything in — not an error.</summary>
    [Fact]
    public void A_missing_file_loads_as_the_empty_config()
    {
        var config = DriverConfig.Load(Path.Combine(Path.GetTempPath(), "daoris-no-such", "driver.json"));

        Assert.Empty(config.Drivable);
    }

    /// <summary>The supported harness is the default (D23); the stub is chosen deliberately, by tests.</summary>
    [Fact]
    public void The_default_adapter_is_the_supported_harness()
    {
        Assert.Equal("claude-code", DriverConfig.Parse("{}").Adapter);
    }
}
