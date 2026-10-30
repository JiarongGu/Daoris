using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// KEEPSIZE1e (D195's keep-size note): the browser's caches a kind with a size limit and no days. The limit judged by the one door and stored
/// as a shape; the daily pass clearing them over the limit while the browser does not run; the browser's start clearing them over the limit
/// and leaving them within it. Files only, in a home under <c>_fixtures/</c>.
/// </summary>
public sealed class BrowserCacheLimitTests : IDisposable
{
    private readonly string _root = Path.Combine(GitSeed.RepoRoot(), "_fixtures", "browser-cache-limit", Guid.NewGuid().ToString("N")[..8]);

    private string Home => Path.Combine(_root, "data");

    private string Engine => DaorisHome.Entry(Home, HomeEntry.BrowserEngine);

    private static readonly DateTimeOffset Now = new(2026, 10, 22, 9, 0, 0, TimeSpan.Zero);

    public BrowserCacheLimitTests() => Directory.CreateDirectory(Home);

    public void Dispose() => GitSeed.Discard(_root);

    private void LayOut(int cache = 600)
    {
        File.WriteAllBytes(Cached(Path.Combine("Default", "Cache", "data_0")), new byte[cache]);
        File.WriteAllBytes(Cached(Path.Combine("GrShaderCache", "shader")), new byte[cache]);
        File.WriteAllBytes(Cached(Path.Combine("Default", "Cookies")), new byte[10]);
    }

    private string Cached(string relative)
    {
        var path = Path.Combine(Engine, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        return path;
    }

    private static KeepPolicy Limit(int? gb) => KeepPolicy.Default with { Limits = KeepPolicy.Default.Limits with { BrowserCacheGb = gb } };

    private sealed class Browser(bool running) : IBrowserCaches
    {
        public Task<T?> WhileStoppedAsync<T>(Func<T> clear, CancellationToken ct) where T : class => Task.FromResult(running ? null : clear());
    }

    private SpaceFreeWorld World(KeepPolicy policy, IBrowserCaches? caches)
    {
        var sizes = new SpaceSizes();
        sizes.MeasureNow(Home, "browser-cache", WiringBudget.Default, CancellationToken.None);
        return new SpaceFreeWorld(Home)
        {
            Sizes = sizes, Now = () => Now, Policy = _ => policy, Caches = caches,
            Judge = () => new DownloadJudge(() => DownloadUses.Read(Home, new HarnessSettings(), null), () => Now),
        };
    }

    /// <summary>🔴 The limit is set beside no days: a size only, refused as days, stored and read back, the default again.</summary>
    [Fact]
    public void The_browsers_limit_is_a_size_set_by_the_one_door_and_read_back()
    {
        var config = DriverConfig.Empty.WithKeep(new KeepSet("browser-cache", Gb: 3));

        Assert.Equal(3, KeepPolicy.From(config.Keep).Limits.BrowserCacheGb);
        Assert.Equal(2, KeepPolicy.Default.Limits.BrowserCacheGb);
        Assert.Equal(new KeepSet("browser-cache", Gb: 4), KeepRules.Asked("browser-cache", "4GB"));
        Assert.Equal(2, KeepPolicy.From(config.WithKeep(new KeepSet("browser-cache", Default: true)).Keep).Limits.BrowserCacheGb);
        Assert.Throws<ChoiceRefusal>(() => DriverConfig.Empty.WithKeep(new KeepSet("browser-cache", Days: 7)));
        Assert.Throws<ChoiceRefusal>(() => DriverConfig.Empty.WithKeep(new KeepSet("browser-cache", Gb: 0)));
        Assert.Throws<ChoiceRefusal>(() => DriverConfig.Empty.WithKeep(new KeepSet("browser-cache", Never: true)));
    }

    /// <summary>🔴 A stored limit out of range reads as the default; the rule's card row carries the size and the bounds say it takes no days.</summary>
    [Fact]
    public void A_stored_browser_limit_out_of_range_reads_as_the_default()
    {
        var path = DaorisHome.Entry(Home, HomeEntry.DriverConfig);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, """{ "keep": { "browserCache": { "gb": 0 } } }""");
        Assert.Equal(2, KeepPolicy.Of(Home).Limits.BrowserCacheGb);

        File.WriteAllText(path, """{ "keep": { "browserCache": { "gb": 5 } } }""");
        Assert.Equal(5, KeepPolicy.Of(Home).Limits.BrowserCacheGb);
    }

    /// <summary>🔴 Over its limit, the caches go at the daily pass while the browser does not run; the browser running keeps them.</summary>
    [Fact]
    public async Task The_daily_pass_clears_the_caches_over_their_limit_only_while_the_browser_does_not_run()
    {
        LayOut();

        var running = await SpaceLimits.PassAsync(World(Limit(1), new Browser(running: true)), KeepingRun.Look, "run-1", CancellationToken.None, unit: 1000);
        Assert.True(File.Exists(Path.Combine(Engine, "Default", "Cache", "data_0")));
        Assert.Equal(0, Assert.Single(running.Kinds).Removed);

        var within = await SpaceLimits.PassAsync(World(Limit(2), new Browser(running: false)), KeepingRun.Look, "run-2", CancellationToken.None, unit: 1000);
        Assert.Empty(within.Kinds);

        var stopped = await SpaceLimits.PassAsync(World(Limit(1), new Browser(running: false)), KeepingRun.Look, "run-3", CancellationToken.None, unit: 1000);
        Assert.Equal(("browser-cache", 2), (Assert.Single(stopped.Kinds).Id, stopped.Kinds[0].Removed));
        Assert.False(Directory.Exists(Path.Combine(Engine, "Default", "Cache")));
        Assert.True(File.Exists(Path.Combine(Engine, "Default", "Cookies")));
    }

    /// <summary>🔴 A caches' count not made yet takes nothing and asks to be counted.</summary>
    [Fact]
    public async Task Caches_not_counted_take_nothing()
    {
        LayOut();
        var world = new SpaceFreeWorld(Home) { Sizes = new SpaceSizes(), Now = () => Now, Policy = _ => Limit(1), Caches = new Browser(running: false) };

        var pass = await SpaceLimits.PassAsync(world, KeepingRun.Look, "run-1", CancellationToken.None, unit: 1000);

        Assert.True(pass.Uncounted);
        Assert.Empty(pass.Kinds);
        Assert.True(File.Exists(Path.Combine(Engine, "Default", "Cache", "data_0")));
    }

    /// <summary>🔴 The browser's start: caches over the limit cleared before it opens them, those within it untouched, never the cookies.</summary>
    [Fact]
    public void The_browsers_start_clears_the_caches_over_their_limit_and_leaves_those_within_it()
    {
        LayOut();

        Assert.Null(BrowserCacheStart.Check(Home, _ => Limit(2), unit: 1000));
        Assert.True(File.Exists(Path.Combine(Engine, "Default", "Cache", "data_0")));

        var cleared = BrowserCacheStart.Check(Home, _ => Limit(1), unit: 1000);

        Assert.NotNull(cleared);
        Assert.Equal(2, cleared!.Removed.Count);
        Assert.False(Directory.Exists(Path.Combine(Engine, "Default", "Cache")));
        Assert.True(File.Exists(Path.Combine(Engine, "Default", "Cookies")));
    }
}
