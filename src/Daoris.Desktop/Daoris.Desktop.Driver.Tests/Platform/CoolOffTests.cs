using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// <c>cooloff</c> in <c>driver.json</c> (TOOL4e, D125 §2.2): how long an account cools when its agent's limit names no
/// time this reads, or one it does not believe. The driver's half of a TWIN with the CLI's <c>driverconfig.ts</c>, whose
/// <c>driverconfig.test.ts</c> holds the same table and parses this theory to hold it to its own, cell for cell.
/// </summary>
/// <remarks>🔴 <b>Keep each row on one line, its cells literals</b>: the CLI's test reads them.</remarks>
public sealed class CoolOffTests
{
    /// <summary>Absent is 60 minutes; a whole number of at least 1 is the setting; anything else is not read.</summary>
    [Theory]
    [InlineData("absent is the default, an hour", "{}", 60)]
    [InlineData("a whole number of minutes is the setting", """{"cooloff":90}""", 90)]
    [InlineData("one minute is the least there is", """{"cooloff":1}""", 1)]
    [InlineData("zero is a spin, and is not read", """{"cooloff":0}""", 60)]
    [InlineData("less than nothing is not read", """{"cooloff":-5}""", 60)]
    [InlineData("a part of a minute is not read", """{"cooloff":1.5}""", 60)]
    [InlineData("text is not a number", """{"cooloff":"90"}""", 60)]
    [InlineData("null is absent", """{"cooloff":null}""", 60)]
    public void Cooloff_reads_as_the_cli_reads_it(string name, string file, int minutes)
    {
        var read = DriverConfig.Parse(file).CoolOff;

        Assert.True(TimeSpan.FromMinutes(minutes) == read, $"{name}: {read.TotalMinutes}");
    }

    [Fact]
    public void The_default_is_the_reader_s_own_hour()
    {
        Assert.Equal(AccountLimits.DefaultCoolOff, DriverConfig.Empty.CoolOff);
        Assert.Null(DriverConfig.Empty.CoolOffMinutes);
    }

    [Fact]
    public void A_cool_off_set_survives_the_file_and_is_written_only_when_set()
    {
        var set = DriverConfig.Empty.WithCoolOff(90);

        Assert.Equal(TimeSpan.FromMinutes(90), DriverConfig.Parse(set.ToJson()).CoolOff);
        Assert.Contains("\"cooloff\": 90", set.ToJson(), StringComparison.Ordinal);
        Assert.DoesNotContain("cooloff", DriverConfig.Empty.ToJson(), StringComparison.Ordinal);
        Assert.DoesNotContain("cooloff", set.WithCoolOff(null).ToJson(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Less_than_a_minute_is_refused_since_a_zero_cool_off_is_a_spin(int minutes)
    {
        var refused = Assert.Throws<DriverException>(() => DriverConfig.Empty.WithCoolOff(minutes));

        Assert.Contains("at least 1", refused.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Another_edit_keeps_the_cool_off()
    {
        var edited = DriverConfig.Parse("""{"cooloff":45}""").WithDrivable("engine", true).WithStrikes(2);

        Assert.Equal(TimeSpan.FromMinutes(45), DriverConfig.Parse(edited.ToJson()).CoolOff);
    }
}
