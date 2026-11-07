using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The watcher's quiet read: Q is the machine's <c>quietMinutes</c>, which each look hands it, so a
/// session unheard for longer than the default and shorter than the setting is not marked; a test's pinned bound stays as pinned.
/// </summary>
public sealed class SessionWatchQuietSettingTests : IDisposable
{
    private static readonly TimeSpan Pace = TimeSpan.FromSeconds(15);
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "session-watch-quiet", Guid.NewGuid().ToString("N")[..8]);
    private readonly SessionWatch _watch;
    private TimeSpan _now = TimeSpan.FromHours(1);

    public SessionWatchQuietSettingTests()
    {
        Directory.CreateDirectory(_home);
        _watch = SessionWatch.Of(new SessionEvents(DaorisHome.Entry(_home, HomeEntry.Sessions)));
        _watch.Clock = new QuietClock(() => _now, stopsInSleep: true);
    }

    public void Dispose() => GitSeed.Discard(_home);

    private List<QuietStep> Run(TimeSpan span)
    {
        var steps = new List<QuietStep>();
        var end = _now + span;
        while (_now < end)
        {
            _now += Pace;
            steps.AddRange(_watch.Look(_ => new JobRead(true, TimeSpan.Zero), Pace));
        }

        return steps;
    }

    [Fact]
    public void Q_follows_the_setting_so_a_session_is_marked_at_its_minutes_and_not_before()
    {
        _watch.Follow(QuietSetting.Of(DriverConfig.Empty.WithQuiet(20)));
        Assert.Equal(TimeSpan.FromMinutes(20), _watch.Quiet);
        using var run = _watch.Watch("s1", new WatchedRun(SessionWatch.Quest, "q1", "claude-code"));

        Assert.DoesNotContain(Run(TimeSpan.FromMinutes(15)), step => step.Step == SessionWatch.Marked);
        Assert.Contains(Run(TimeSpan.FromMinutes(6)), step => step.Step == SessionWatch.Marked);
    }

    [Fact]
    public void The_default_is_the_settings_default_and_a_pinned_bound_is_not_followed()
    {
        Assert.Equal(TimeSpan.FromMinutes(QuietSetting.Default), QuietSetting.Of(DriverConfig.Empty));
        Assert.Equal(SessionWatch.DefaultQuiet, _watch.Quiet);

        _watch.Quiet = TimeSpan.FromSeconds(3);
        _watch.Follow(TimeSpan.FromMinutes(30));
        Assert.Equal(TimeSpan.FromSeconds(3), _watch.Quiet);
    }
}
