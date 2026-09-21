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

    /// <summary>
    /// 🔴 Silence means ON (SURF5b). Every machine that already has a `driver.json` predates this
    /// field, so reading its absence as "off" would ship the feature switched off on exactly the
    /// machines that have been driving longest — where a parked session sitting unnoticed costs most.
    /// </summary>
    [Fact]
    public void A_config_that_says_nothing_about_notifying_notifies()
    {
        Assert.True(DriverConfig.Parse("{}").Notify);
        Assert.True(DriverConfig.Parse("""{"drivable":["engine"],"cap":3}""").Notify);
        Assert.True(DriverConfig.Empty.Notify);
    }

    [Fact]
    public void Turning_notifications_off_survives_the_file()
    {
        var off = DriverConfig.Empty.WithNotify(false);

        Assert.False(DriverConfig.Parse(off.ToJson()).Notify);
        // And back on, because a switch that only goes one way is not a switch.
        Assert.True(DriverConfig.Parse(off.WithNotify(true).ToJson()).Notify);
    }

    /// <summary>A value that is not a boolean is not an answer, so the default stands.</summary>
    [Fact]
    public void A_notify_field_of_the_wrong_shape_falls_back_rather_than_throwing()
    {
        Assert.True(DriverConfig.Parse("""{"notify":"no"}""").Notify);
        Assert.True(DriverConfig.Parse("""{"notify":0}""").Notify);
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

    /// <summary>What is written is what is read back — the file the controls edit and the loop watches.</summary>
    [Fact]
    public void A_config_survives_its_own_round_trip()
    {
        var config = DriverConfig.Empty
            .WithDrivable("Game", true)
            .WithHold("Game", true)
            with
        { Cap = 3, Adapter = "stub", Commands = new Dictionary<string, IReadOnlyList<string>> { ["stub"] = ["node", "a.mjs"] } };

        var read = DriverConfig.Parse(config.ToJson());

        Assert.Equal(config.Drivable, read.Drivable);
        Assert.Equal(config.Holds, read.Holds);
        Assert.Equal(3, read.Cap);
        Assert.Equal("stub", read.Adapter);
        Assert.Equal(["node", "a.mjs"], read.Commands["stub"]);
    }

    /// <summary>One flag flips at a time, idempotently, and case is not identity — like everywhere else.</summary>
    [Fact]
    public void Toggling_is_idempotent_and_case_blind()
    {
        var opted = DriverConfig.Empty.WithDrivable("Game", true).WithDrivable("game", true);
        Assert.Single(opted.Drivable);

        var removed = opted.WithDrivable("GAME", false);
        Assert.Empty(removed.Drivable);
        Assert.Empty(removed.WithDrivable("Game", false).Drivable);
    }

    /// <summary>
    /// Session trees (D51): whose sessions open their own worktree. The same standing-flag shape as
    /// drivable, round-tripping through the same file both editors share.
    /// </summary>
    [Fact]
    public void The_trees_opt_in_round_trips_and_toggles_like_the_others()
    {
        var config = DriverConfig.Empty.WithTrees("Game", true).WithTrees("game", true);
        Assert.Single(config.Trees);

        var read = DriverConfig.Parse(config.ToJson());
        Assert.Equal(["game"], read.Trees);
        Assert.True(read.OpensOwnTree("GAME"));
        Assert.False(read.OpensOwnTree("Tools"));

        Assert.Empty(config.WithTrees("GAME", false).Trees);
        Assert.Empty(DriverConfig.Parse("{}").Trees);
    }

    [Fact]
    public void Save_writes_atomically_and_load_reads_it_back()
    {
        var path = Path.Combine(Path.GetTempPath(), "daoris-config-" + Guid.NewGuid().ToString("N")[..8], "driver.json");
        try
        {
            DriverConfig.Empty.WithDrivable("Game", true).Save(path);

            Assert.Equal(["Game"], DriverConfig.Load(path).Drivable);
            Assert.False(File.Exists(path + ".writing"));
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);
        }
    }
}
