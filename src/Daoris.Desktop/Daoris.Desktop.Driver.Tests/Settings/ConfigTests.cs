using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The driver's configuration is the person's standing choices: which repositories this
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
    /// Silence means ON. Every machine that already has a `driver.json` predates this
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

    /// <summary>
    /// Ask Daoris's access mode is the person's choice, restricted until full is named, written
    /// only while full and kept by every other edit. Anything else the file says reads as restricted, never as full; and an edit
    /// naming another mode is refused.
    /// </summary>
    [Fact]
    public void Ask_daoris_runs_restricted_until_the_person_names_full()
    {
        Assert.Equal(HelpModes.Restricted, DriverConfig.Parse("{}").HelpMode);
        Assert.DoesNotContain("helpMode", DriverConfig.Empty.ToJson());
        Assert.Equal(HelpModes.Restricted, DriverConfig.Parse("""{"helpMode":"everything"}""").HelpMode);
        Assert.Equal(HelpModes.Full, DriverConfig.Parse("""{"helpMode":" Full "}""").HelpMode);

        var full = DriverConfig.Parse("""{"helpMode":"full","drivable":["engine"]}""");
        var edited = DriverConfig.Parse(full.WithNotify(false).WithDrivable("tools", true).ToJson());
        Assert.Equal(HelpModes.Full, edited.HelpMode);
        Assert.Equal(HelpModes.Full, DriverConfig.Parse(DriverConfig.Empty.WithHelpMode("full").ToJson()).HelpMode);

        var restricted = edited.WithHelpMode(HelpModes.Restricted).ToJson();
        Assert.Equal(HelpModes.Restricted, DriverConfig.Parse(restricted).HelpMode);
        Assert.DoesNotContain("helpMode", restricted);
        Assert.Throws<DriverException>(() => DriverConfig.Empty.WithHelpMode("everything"));
    }

    /// <summary>
    /// 🔴 <c>timeoutMinutes</c> is no setting: nothing reads it, and <c>quietMinutes</c> is the one quiet setting. A loaded file keeps it
    /// as stored through a save that changes something else, as it keeps every key it does not own; a configuration built in memory
    /// writes none, and the tests' bound is never written or read.
    /// </summary>
    [Fact]
    public void A_timeout_is_no_setting_kept_as_stored_and_never_written_from_memory()
    {
        var dir = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "config-kept-timeout", Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(dir);
        using var cleaned = new Disposer(() => GitSeed.Remove(dir));
        var path = Path.Combine(dir, "driver.json");
        File.WriteAllText(path, """
            {
              "cap": 2,
              "timeoutMinutes": 90,
              "pollSeconds": 15
            }

            """);

        var loaded = DriverConfig.Load(path);
        Assert.Null(loaded.QuietMinutes);
        Assert.Null(loaded.TestBound);
        loaded.WithCap(4).Save(path);

        var text = File.ReadAllText(path);
        Assert.Contains("\"timeoutMinutes\": 90", text);
        Assert.Contains("\"cap\": 4", text);
        Assert.DoesNotContain(QuietSetting.File, text);

        var built = (DriverConfig.Empty with { TestBound = 2 }).WithCap(3).ToJson();
        Assert.DoesNotContain("timeoutMinutes", built);
        Assert.Null(DriverConfig.Parse("""{ "testBound": 2 }""").TestBound);
    }

    [Fact]
    public void A_full_config_round_trips()
    {
        var config = DriverConfig.Parse("""
            {
              "drivable": ["Game", "Tools"],
              "holds": ["Tools"],
              "cap": 3,
              "quietMinutes": 15,
              "pollSeconds": 5,
              "commands": { "stub": ["node", "local/agent.mjs"] }
            }
            """);

        Assert.Equal(["Game", "Tools"], config.Drivable);
        Assert.Equal(["Tools"], config.Holds);
        Assert.Equal(3, config.Cap);
        Assert.Equal(15, config.QuietMinutes);
        Assert.Equal(5, config.PollSeconds);
        Assert.Equal(["node", "local/agent.mjs"], config.Commands["stub"]);
    }

    /// <summary>A missing file is a machine that has not opted anything in — not an error.</summary>
    [Fact]
    public void A_missing_file_loads_as_the_empty_config()
    {
        var config = DriverConfig.Load(Path.Combine(TestHomes.Root, "daoris-no-such", "driver.json"));

        Assert.Empty(config.Drivable);
    }

    /// <summary>What is written is what is read back — the file the controls edit and the loop watches.</summary>
    [Fact]
    public void A_config_survives_its_own_round_trip()
    {
        var config = DriverConfig.Empty
            .WithDrivable("Game", true)
            .WithHold("Game", true)
            with
        { Cap = 3, Commands = new Dictionary<string, IReadOnlyList<string>> { ["stub"] = ["node", "a.mjs"] } };

        var read = DriverConfig.Parse(config.ToJson());

        Assert.Equal(config.Drivable, read.Drivable);
        Assert.Equal(config.Holds, read.Holds);
        Assert.Equal(3, read.Cap);
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
    /// A repository opted in or held is another only as <c>OrdinalIgnoreCase</c> parts them, each letter
    /// to its one capital: a name full case
    /// mapping would widen or lower to the same letters is another repository.
    /// </summary>
    [Fact]
    public void A_repository_in_each_list_is_another_only_as_OrdinalIgnoreCase_parts_them()
    {
        var dotted = $"i{(char)0x0307}zmir";
        var config = DriverConfig.Empty;
        foreach (var name in new[] { "İzmir", dotted, "straße", "STRASSE" })
        {
            config = config.WithDrivable(name, true).WithHold(name, true);
        }

        var read = DriverConfig.Parse(config.ToJson());

        Assert.Equal(["İzmir", dotted, "straße", "STRASSE"], read.Drivable);
        Assert.Equal(["İzmir", dotted, "straße", "STRASSE"], read.Holds);
    }

    [Fact]
    public void Save_writes_atomically_and_load_reads_it_back()
    {
        var path = Path.Combine(TestHomes.Root, "daoris-config-" + Guid.NewGuid().ToString("N")[..8], "driver.json");
        try
        {
            DriverConfig.Empty.WithDrivable("Game", true).Save(path);

            Assert.Equal(["Game"], DriverConfig.Load(path).Drivable);
            Assert.False(File.Exists(path + ".writing"));
        }
        finally
        {
            GitSeed.Remove(Path.GetDirectoryName(path)!);
        }
    }
}
