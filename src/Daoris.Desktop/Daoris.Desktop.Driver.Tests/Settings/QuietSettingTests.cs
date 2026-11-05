using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// <c>quietMinutes</c>, the one quiet setting (D200, the session-watch design §3), at its file and its terminal door: read only in its
/// bounds, written only when set, said by <c>list</c> with its default, set by <c>daoris settings quiet</c> and refused out of bounds.
/// </summary>
public sealed class QuietSettingTests : IDisposable
{
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "quiet-setting", Guid.NewGuid().ToString("N")[..8]);
    private readonly StringWriter _out = new();
    private readonly StringWriter _error = new();

    public QuietSettingTests() => Directory.CreateDirectory(_home);

    public void Dispose() => GitSeed.Discard(_home);

    private string Config => DaorisHome.Entry(_home, HomeEntry.DriverConfig);

    private DriverChoicesWorld World() => new()
    {
        Verbs = DriverChoiceVerbs.All,
        Instance = new InstanceWorld
        {
            Home = _home,
            Environment = _ => null,
            Out = _out,
            Error = _error,
            Verb = "daoris",
        },
        ConfigPath = Config,
    };

    [Theory]
    [InlineData("""{ "quietMinutes": 15 }""", 15)]
    [InlineData("""{ "quietMinutes": 5 }""", 5)]
    [InlineData("""{ "quietMinutes": 120 }""", 120)]
    [InlineData("""{ "quietMinutes": 4 }""", null)]
    [InlineData("""{ "quietMinutes": 121 }""", null)]
    [InlineData("""{ "quietMinutes": "soon" }""", null)]
    [InlineData("""{ "timeoutMinutes": 90 }""", null)]
    [InlineData("{}", null)]
    public void The_file_is_read_only_in_its_bounds_and_anything_else_is_the_default(string json, int? minutes)
    {
        var config = DriverConfig.Parse(json);

        Assert.Equal(minutes, config.QuietMinutes);
        Assert.Equal(TimeSpan.FromMinutes(minutes ?? QuietSetting.Default), QuietSetting.Of(config));
    }

    [Fact]
    public void It_is_written_only_when_set_and_refused_out_of_its_bounds()
    {
        Assert.DoesNotContain(QuietSetting.File, DriverConfig.Empty.ToJson());
        var set = DriverConfig.Empty.WithQuiet(20);
        Assert.Contains("\"quietMinutes\": 20", set.ToJson());
        Assert.Equal(20, DriverConfig.Parse(set.ToJson()).QuietMinutes);
        Assert.DoesNotContain(QuietSetting.File, set.WithQuiet(null).ToJson());

        Assert.Equal(QuietSetting.Refusal, Assert.Throws<DriverException>(() => DriverConfig.Empty.WithQuiet(4)).Message);
        Assert.Equal(QuietSetting.Refusal, Assert.Throws<DriverException>(() => DriverConfig.Empty.WithQuiet(121)).Message);
    }

    [Fact]
    public async Task The_terminal_sets_it_and_the_list_says_it()
    {
        Assert.Equal(0, await DriverChoicesCommand.RunAsync(["list"], World(), CancellationToken.None));
        Assert.Contains("  quiet      10 minutes before Daoris looks in on a session  (the default", _out.ToString());

        _out.GetStringBuilder().Clear();
        Assert.Equal(0, await DriverChoicesCommand.RunAsync(["quiet", "25"], World(), CancellationToken.None));
        Assert.Contains("runs nothing for 25 minutes is looked in on", _out.ToString());
        Assert.Equal(25, DriverConfig.Load(Config).QuietMinutes);

        _out.GetStringBuilder().Clear();
        Assert.Equal(0, await DriverChoicesCommand.RunAsync(["list"], World(), CancellationToken.None));
        Assert.Contains("  quiet      25 minutes before Daoris looks in on a session", _out.ToString());
        Assert.DoesNotContain("timeout", _out.ToString());
    }

    [Theory]
    [InlineData("4")]
    [InlineData("121")]
    [InlineData("1.5")]
    [InlineData("soon")]
    public async Task The_terminal_refuses_a_value_out_of_its_bounds_and_writes_nothing(string value)
    {
        var exit = await DriverChoicesCommand.RunAsync(["quiet", value], World(), CancellationToken.None);

        Assert.NotEqual(0, exit);
        Assert.Contains("`driver quiet` needs a whole number of minutes from 5 to 120", _error.ToString());
        Assert.Null(File.Exists(Config) ? DriverConfig.Load(Config).QuietMinutes : null);
    }
}
