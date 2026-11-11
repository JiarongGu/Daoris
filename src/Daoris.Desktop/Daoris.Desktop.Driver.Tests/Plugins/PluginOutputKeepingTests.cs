using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>A plugin's <c>output</c> folder keeps files 14 days; the plugin's other data stays, and a day's prune runs once a day.</summary>
public sealed class PluginOutputKeepingTests : IDisposable
{
    private readonly string _root = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "plugin-output", Guid.NewGuid().ToString("N")[..8]);

    private DateTimeOffset _now = new(2026, 11, 12, 9, 0, 0, TimeSpan.Zero);

    public void Dispose() => GitSeed.Discard(_root);

    private string Home => Path.Combine(_root, "home");

    private string Data(string plugin, params string[] path) =>
        Path.Combine([DaorisHome.Entry(Home, HomeEntry.Plugins), PluginCatalog.DataFolder, plugin, .. path]);

    private string Write(string file, int daysOld, string text = "x")
    {
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, text);
        File.SetLastWriteTimeUtc(file, _now.UtcDateTime.AddDays(-daysOld));
        return file;
    }

    [Fact]
    public void A_file_past_fourteen_days_goes_and_a_younger_one_and_the_plugins_other_data_stay()
    {
        var old = Write(Data("browser", "output", "shots", "a.png"), 15, "12345");
        var young = Write(Data("browser", "output", "b.png"), 13);
        var other = Write(Data("browser", "profile", "state.json"), 90);
        var keeping = new PluginOutputKeeping(() => _now);

        Assert.True(keeping.Due(Home));
        var (removed, bytes) = keeping.Keep(Home);

        Assert.Equal((1, 5L), (removed, bytes));
        Assert.False(File.Exists(old));
        Assert.False(Directory.Exists(Data("browser", "output", "shots")));
        Assert.True(File.Exists(young));
        Assert.True(File.Exists(other));
        Assert.True(Directory.Exists(Data("browser", "output")));
    }

    [Fact]
    public void The_prune_is_due_once_a_day_and_only_where_a_file_is_past_its_days()
    {
        var keeping = new PluginOutputKeeping(() => _now);
        Assert.False(keeping.Due(Home));

        Write(Data("browser", "output", "a.png"), 20);
        _now = _now.AddHours(1);
        Assert.False(keeping.Due(Home));

        _now = _now.AddDays(1);
        Assert.True(keeping.Due(Home));
        keeping.Keep(Home);
        Write(Data("browser", "output", "c.png"), 20);
        Assert.False(keeping.Due(Home));
    }
}
