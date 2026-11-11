using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// <c>manifest</c> in <c>driver.json</c>, how many files and how much time a folder's manifest read spends before the place is too
/// large to follow: read only in its bounds, written only when set, said by <c>list</c> with its default, set by
/// <c>daoris settings manifest files|minutes</c> and refused out of bounds, and the bounds a read takes.
/// </summary>
public sealed class ManifestSettingTests : IDisposable
{
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "manifest-setting", Guid.NewGuid().ToString("N")[..8]);
    private readonly StringWriter _out = new();
    private readonly StringWriter _error = new();

    public ManifestSettingTests() => Directory.CreateDirectory(_home);

    public void Dispose() => GitSeed.Discard(_home);

    private string Config => DaorisHome.Entry(_home, HomeEntry.DriverConfig);

    private DriverChoicesWorld World() => new()
    {
        Verbs = DriverChoiceVerbs.All,
        Instance = new InstanceWorld { Home = _home, Environment = _ => null, Out = _out, Error = _error, Verb = "daoris" },
        ConfigPath = Config,
    };

    [Theory]
    [InlineData("""{ "manifest": { "files": 50000, "minutes": 3 } }""", 50000, 3)]
    [InlineData("""{ "manifest": { "files": 1000 } }""", 1000, null)]
    [InlineData("""{ "manifest": { "minutes": 60 } }""", null, 60)]
    [InlineData("""{ "manifest": { "files": 999, "minutes": 61 } }""", null, null)]
    [InlineData("""{ "manifest": { "files": "many", "minutes": 2.5 } }""", null, null)]
    [InlineData("""{ "manifest": 7 }""", null, null)]
    [InlineData("{}", null, null)]
    public void The_file_is_read_only_in_its_bounds_and_anything_else_is_the_default(string json, int? files, int? minutes)
    {
        var config = DriverConfig.Parse(json);

        Assert.Equal((files, minutes), (config.Manifest.Files, config.Manifest.Minutes));
        var bounds = config.Manifest.Bounds;
        Assert.Equal((files ?? ManifestBounds.Default.Files, TimeSpan.FromMinutes(minutes ?? ManifestBounds.Default.Time.TotalMinutes)), (bounds.Files, bounds.Time));
    }

    [Fact]
    public void It_is_written_only_when_set_and_refused_out_of_its_bounds()
    {
        Assert.DoesNotContain(ManifestSetting.File, DriverConfig.Empty.ToJson());
        var set = DriverConfig.Empty.WithManifest(files: 20000, minutes: null);
        Assert.Equal(20000, DriverConfig.Parse(set.ToJson()).Manifest.Files);
        Assert.Null(DriverConfig.Parse(set.ToJson()).Manifest.Minutes);
        var both = set.WithManifest(files: null, minutes: 2);
        Assert.Equal((20000, 2), (DriverConfig.Parse(both.ToJson()).Manifest.Files, DriverConfig.Parse(both.ToJson()).Manifest.Minutes));

        Assert.Equal(ManifestSetting.FilesRefusal, Assert.Throws<DriverException>(() => DriverConfig.Empty.WithManifest(999, null)).Message);
        Assert.Equal(ManifestSetting.MinutesRefusal, Assert.Throws<DriverException>(() => DriverConfig.Empty.WithManifest(null, 61)).Message);
    }

    [Fact]
    public async Task The_terminal_sets_each_bound_and_the_list_says_them()
    {
        Assert.Equal(0, await DriverChoicesCommand.RunAsync(["list"], World(), CancellationToken.None));
        Assert.Contains("  manifest   250000 files and 5 minutes before a folder is too large to follow, 2 GB before it is too large to copy  (the default", _out.ToString());

        _out.GetStringBuilder().Clear();
        Assert.Equal(0, await DriverChoicesCommand.RunAsync(["manifest", "files", "50000"], World(), CancellationToken.None));
        Assert.Contains("a folder's manifest read stops at 50000 files", _out.ToString());
        Assert.Equal(50000, DriverConfig.Load(Config).Manifest.Files);

        _out.GetStringBuilder().Clear();
        Assert.Equal(0, await DriverChoicesCommand.RunAsync(["manifest", "minutes", "3"], World(), CancellationToken.None));
        Assert.Contains("a folder's manifest read stops after 3 minutes", _out.ToString());
        Assert.Equal((50000, 3), (DriverConfig.Load(Config).Manifest.Files, DriverConfig.Load(Config).Manifest.Minutes));

        _out.GetStringBuilder().Clear();
        Assert.Equal(0, await DriverChoicesCommand.RunAsync(["manifest", "copy", "512"], World(), CancellationToken.None));
        Assert.Contains("a folder of more than 512 MB is not copied", _out.ToString());
        Assert.Equal(512L << 20, DriverConfig.Load(Config).Manifest.Bounds.Copy);

        _out.GetStringBuilder().Clear();
        Assert.Equal(0, await DriverChoicesCommand.RunAsync(["list"], World(), CancellationToken.None));
        Assert.Contains("  manifest   50000 files and 3 minutes before a folder is too large to follow, 512 MB before it is too large to copy", _out.ToString());
        Assert.DoesNotContain("the default", _out.ToString().Split('\n').Single(line => line.StartsWith("  manifest")));
    }

    [Fact]
    public void A_read_takes_the_bounds_this_machines_file_sets_and_the_default_without_one()
    {
        Assert.Equal(ManifestBounds.Default, ManifestBounds.Of(_home));

        File.WriteAllText(Config, DriverConfig.Empty.WithManifest(5000, 2).ToJson());

        Assert.Equal(new ManifestBounds(5000, TimeSpan.FromMinutes(2)), ManifestBounds.Of(_home));
    }

    [Theory]
    [InlineData("files", "999")]
    [InlineData("files", "soon")]
    [InlineData("minutes", "0")]
    [InlineData("minutes", "61")]
    [InlineData("minutes", "1.5")]
    [InlineData("hours", "1")]
    [InlineData("files", null)]
    public async Task The_terminal_refuses_a_value_out_of_its_bounds_and_writes_nothing(string which, string? value)
    {
        string[] argv = value is null ? ["manifest", which] : ["manifest", which, value];
        var exit = await DriverChoicesCommand.RunAsync(argv, World(), CancellationToken.None);

        Assert.NotEqual(0, exit);
        Assert.Contains("daoris settings manifest", _error.ToString());
        Assert.False(File.Exists(Config) && File.ReadAllText(Config).Contains("manifest"));
    }
}
