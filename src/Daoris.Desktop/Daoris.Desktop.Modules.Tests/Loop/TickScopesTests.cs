using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// What a look declares to the windows beside its tick (FOUNDATION1s, D182 point 7): the page's tick no longer asks the rules, the
/// repositories and the registry again, so what a look moved of them is told as a change of its own scope, and only what moved.
/// </summary>
public sealed class TickScopesTests : DriverModuleBridge
{
    [Fact]
    public void Each_scope_a_look_moved_is_told_and_none_it_did_not()
    {
        Assert.Equal(StateScopes.None, DriverLoop.LookScopes(rulesMoved: false, registryMoved: false, indexMoved: false));
        Assert.Equal(StateScopes.Rules, DriverLoop.LookScopes(rulesMoved: true, registryMoved: false, indexMoved: false));
        Assert.Equal(StateScopes.Registry, DriverLoop.LookScopes(rulesMoved: false, registryMoved: true, indexMoved: false));
        Assert.Equal(StateScopes.Repositories, DriverLoop.LookScopes(rulesMoved: false, registryMoved: false, indexMoved: true));
        Assert.Equal(StateScopes.Rules | StateScopes.Registry, DriverLoop.LookScopes(rulesMoved: true, registryMoved: true, indexMoved: false));
    }

    /// <summary>
    /// FOUNDATION1s part B: the rules' scope is told where their reading moved, never at every look that says a line about them (the
    /// "could not be settled" retry said again each look). The first reading is the page's own first read.
    /// </summary>
    [Fact]
    public void The_rules_reading_is_quiet_across_looks_that_change_nothing_and_moves_once_with_a_change()
    {
        var reading = new RulesReading();

        Assert.False(reading.Read(Home).Moved);
        Assert.False(reading.Read(Home).Moved);
        Assert.False(reading.Read(Home).Moved);

        PermissionRules.Save(Home, PermissionRules.Add(PermissionRules.Load(Home), RuleScope.Machine, null, RuleList.Deny, "Bash(rm:*)"));
        Assert.True(reading.Read(Home).Moved);
        Assert.False(reading.Read(Home).Moved);
    }

    /// <summary>
    /// FOUNDATION1s part B: the knowledge index's size as the service lists it moves while it feeds and is quiet once it has fed, so the
    /// look tells the repositories' scope and the status bar reads its size again: 555 entries mid-feed, then 1,050 once fed.
    /// </summary>
    [Fact]
    public void The_index_feeding_moves_the_repositories_until_it_has_fed()
    {
        var feed = new IndexFeed();
        static string Listed(int total, string at) =>
            $$"""[{"repository":"engine","total":{{total}},"local":{{total}},"canonical":0,"workspace":"default","indexedAt":"{{at}}"}]""";

        Assert.False(feed.Moved(IndexFeed.Signature(Listed(555, "2026-09-23T10:00:00Z"))));
        Assert.True(feed.Moved(IndexFeed.Signature(Listed(1050, "2026-09-23T10:00:05Z"))));
        Assert.False(feed.Moved(IndexFeed.Signature(Listed(1050, "2026-09-23T10:00:05Z"))));
        // A look whose read failed is no move, and the next read is compared with the last that answered.
        Assert.False(feed.Moved(null));
        Assert.False(feed.Moved(IndexFeed.Signature(Listed(1050, "2026-09-23T10:00:05Z"))));
    }
}
