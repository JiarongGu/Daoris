using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// KEEPSIZE1d (D195's keep-size note): a size limit per kind beside its days. The choice judged by the one door, stored and read back as a
/// stored shape, and a kind over its limit letting go of its oldest eligible units, the downloads here and the set-aside folders in
/// <see cref="SpaceLimitsGitTests"/>. Files only, in a home under <c>_fixtures/</c>.
/// </summary>
public sealed class SpaceLimitsTests : IDisposable
{
    private readonly string _root = Path.Combine(GitSeed.RepoRoot(), "_fixtures", "space-limits", Guid.NewGuid().ToString("N")[..8]);

    private string Home => Path.Combine(_root, "data");

    private static readonly DateTimeOffset T0 = new(DateTime.UtcNow.Date.AddDays(2).AddHours(9), TimeSpan.Zero);

    public SpaceLimitsTests() => Directory.CreateDirectory(Home);

    public void Dispose() => GitSeed.Discard(_root);

    private string Agent(string version, int payload)
    {
        var folder = HarnessSettings.ManagedHome(Home, "claude-code", version);
        Directory.CreateDirectory(Path.Combine(folder, "bin"));
        File.WriteAllText(Path.Combine(folder, "bin", OperatingSystem.IsWindows() ? "claude.exe" : "claude"), new string('x', payload));
        return folder;
    }

    private DownloadJudge Judge(DateTimeOffset now) =>
        DownloadJudge.Of(Home, DaorisHome.Entry(Home, HomeEntry.Harnesses), configPath: null, _ => Task.FromResult(LiveSessions.None), () => now);

    private Task PassAsync(DateTimeOffset now) =>
        DownloadKeeping.PassAsync(Home, KeepPolicy.Default.Downloads, Judge(now), daily: false, KeepingRun.Look, "run-0");

    private SpaceFreeWorld World(DateTimeOffset now, KeepPolicy policy) =>
        new(Home) { Judge = () => Judge(now), Now = () => now, Policy = _ => policy };

    private static KeepPolicy Limit(int? downloadsGb, DownloadsPolicy? downloads = null) =>
        KeepPolicy.Default with { Limits = new LimitsPolicy(null, downloadsGb), Downloads = downloads ?? KeepPolicy.Default.Downloads };

    /// <summary>🔴 The oldest go first, and only as many as bring the kind to its limit; a unit of unknown size is never taken.</summary>
    [Fact]
    public void The_oldest_go_first_until_the_kind_is_within_its_limit()
    {
        var at = new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);
        var candidates = new (string, long?, DateTimeOffset?)[]
        {
            ("new", 40, at.AddDays(3)), ("unknown size", null, at), ("old", 40, at), ("unknown age", 40, null), ("mid", 40, at.AddDays(1)),
        };

        Assert.Equal(["old", "mid"], SpaceLimits.Oldest(candidates, total: 200, limit: 130));
        Assert.Empty(SpaceLimits.Oldest(candidates, total: 130, limit: 130));
    }

    /// <summary>🔴 A downloads kind over its limit loses its longest unused versions first, and the newest stays.</summary>
    [Fact]
    public async Task Downloads_over_their_limit_lose_the_longest_unused_first()
    {
        var oldest = Agent("2.0.1", 5000);
        await PassAsync(T0);
        var middle = Agent("2.0.2", 5000);
        await PassAsync(T0.AddDays(1));
        var newest = Agent("2.0.3", 5000);
        await PassAsync(T0.AddDays(2));
        var now = T0.AddDays(3);

        var pass = await SpaceLimits.PassAsync(World(now, Limit(1)), KeepingRun.Look, "run-1", CancellationToken.None, unit: 7000);

        var kind = Assert.Single(pass.Kinds);
        Assert.Equal(("downloads", 2), (kind.Id, kind.Removed));
        Assert.False(Directory.Exists(oldest));
        Assert.False(Directory.Exists(middle));
        Assert.True(Directory.Exists(newest));
    }

    /// <summary>🔴 Nothing that is kept goes by a size: a pinned version, and every version under <c>never</c>, stay over any limit.</summary>
    [Fact]
    public async Task A_pinned_version_and_a_never_download_are_never_let_go_by_a_size()
    {
        var pinned = Agent("2.0.1", 5000);
        var other = Agent("2.0.2", 5000);
        await PassAsync(T0);
        HarnessSettings.Pin(DaorisHome.Entry(Home, HomeEntry.Harnesses), Home, "claude-code", "2.0.1");
        var now = T0.AddDays(3);

        var withPin = await SpaceLimits.PassAsync(World(now, Limit(1)), KeepingRun.Look, "run-1", CancellationToken.None, unit: 4000);
        Assert.Equal(1, Assert.Single(withPin.Kinds).Removed);
        Assert.True(Directory.Exists(pinned));
        Assert.False(Directory.Exists(other));

        var never = Agent("2.0.3", 5000);
        var kept = await SpaceLimits.PassAsync(World(now, Limit(1, new DownloadsPolicy(null))), KeepingRun.Look, "run-2", CancellationToken.None, unit: 4000);
        Assert.Empty(kept.Kinds);
        Assert.True(Directory.Exists(never));
    }

    /// <summary>🔴 A limit is a stored shape: set beside the days, read back, and an out-of-range one refused or not read.</summary>
    [Fact]
    public void A_limit_is_set_by_the_one_door_and_read_back()
    {
        var config = DriverConfig.Empty.WithKeep(new KeepSet("set-aside", Gb: 12)).WithKeep(new KeepSet("downloads", Days: 3, Gb: 2));

        Assert.Equal(12, KeepPolicy.From(config.Keep).Limits.SetAsideGb);
        Assert.Equal((2, 3.0), (KeepPolicy.From(config.Keep).Limits.DownloadsGb, KeepPolicy.From(config.Keep).Downloads.Days!.Value.TotalDays));
        Assert.Equal(KeepPolicy.Default.Limits, KeepPolicy.From(config.WithKeep(new KeepSet("set-aside", Default: true)).WithKeep(new KeepSet("downloads", Default: true)).Keep).Limits);
        var refusal = Assert.Throws<ChoiceRefusal>(() => DriverConfig.Empty.WithKeep(new KeepSet("set-aside", Gb: 0)));
        Assert.Equal(ChoiceRefusal.KeepOutOfRange, refusal.Check);
        Assert.Throws<ChoiceRefusal>(() => DriverConfig.Empty.WithKeep(new KeepSet("logs", Gb: 5)));
        Assert.Equal(new KeepSet("downloads", Gb: 6), KeepRules.Asked("downloads", "6GB"));
    }

    /// <summary>🔴 A file whose limit is out of range reads as the default, and one that does not read as no limit at all.</summary>
    [Fact]
    public void A_stored_limit_out_of_range_reads_as_the_default_and_an_unread_file_as_none()
    {
        var path = DaorisHome.Entry(Home, HomeEntry.DriverConfig);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, """{ "keep": { "setAside": { "gb": 0 }, "downloads": { "gb": 3 } } }""");

        Assert.Equal(new LimitsPolicy(KeepKinds.SetAside.DefaultGb, 3, KeepKinds.BrowserCache.DefaultGb), KeepPolicy.Of(Home).Limits);
        File.WriteAllText(path, "{ not json");
        Assert.Equal(LimitsPolicy.None, KeepPolicy.Of(Home).Limits);
    }
}
