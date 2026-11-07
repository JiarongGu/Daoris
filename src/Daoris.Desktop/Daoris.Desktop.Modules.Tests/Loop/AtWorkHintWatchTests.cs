using Daoris.Desktop.Driver.Tests;
using System.Text.Json.Nodes;
using Daoris.Driver;
using Microsoft.Extensions.Logging.Abstractions;
using Shenora.Core.Events;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// The hint's standing is a dependency of the read: the registry's readiness is told
/// apart from an answered empty lane list, the first registry answer wakes the read, and the hint moves when <c>driver.json</c> undrives the
/// repository or a lanes-only registration lands, with nothing else written.
/// </summary>
public sealed class AtWorkHintWatchTests : IDisposable
{
    private readonly string _home = Path.Combine(TestHomes.Root, "daoris-hint-" + Guid.NewGuid().ToString("N")[..8]);
    private readonly EventBus _bus = new(NullLogger<EventBus>.Instance);
    private readonly LineageTokens _tokens = new(4242, new DateTime(2026, 11, 2, 0, 0, 0, DateTimeKind.Utc));
    private readonly string _p1;
    private readonly StateSlice _groups;
    private readonly StateSlice _sessions;
    private readonly StateSlice _quests;
    private readonly StateSlice _driver;
    private readonly AtWorkStanding _standing;
    private readonly AtWorkWatch _watch;
    private JsonObject _choices = new() { ["drivable"] = new JsonArray("game"), ["trees"] = new JsonArray() };
    private int _woken;

    public AtWorkHintWatchTests()
    {
        Directory.CreateDirectory(_home);
        _p1 = _tokens.Issue();
        _tokens.Hand(_p1, System.Diagnostics.Process.GetCurrentProcess());
        var entries = new JsonObject
        {
            ["s1"] = new JsonObject { ["parts"] = new JsonArray(new JsonObject { ["session"] = "s1", ["shown"] = "working" }) },
        };
        var records = new JsonObject { ["s1"] = new JsonObject { ["id"] = "s1", ["state"] = "working", ["repository"] = "game" } };
        _groups = new StateSlice(GroupsStateSlice.Name, _bus,
            _ => Task.FromResult<IReadOnlyList<SliceJudged>?>([new(GroupsStateSlice.Entries, entries.DeepClone()), new(GroupsStateSlice.Rows, new JsonObject())]), "g")
        { Keyed = true };
        _sessions = new StateSlice(SessionsStateSlice.Name, _bus, _ => Task.FromResult<IReadOnlyList<SliceJudged>?>([new("default", records.DeepClone())]), "s") { Keyed = true };
        _quests = new StateSlice(QuestsStateSlice.Name, _bus, _ => Task.FromResult<IReadOnlyList<SliceJudged>?>([new("default", new JsonObject())]), "q") { Keyed = true };
        _driver = new StateSlice(DriverStateSlice.Name, _bus, _ => Task.FromResult<IReadOnlyList<SliceJudged>?>([new("choices", _choices.DeepClone())]), "d");
        _standing = new AtWorkStanding(() => _driver);
        _watch = new AtWorkWatch(() => _woken++, _groups, _sessions, _quests, _tokens, _driver, _standing.Word);
    }

    public void Dispose()
    {
        if (Directory.Exists(_home)) Directory.Delete(_home, recursive: true);
    }

    private string Started(string agent) =>
        new JsonObject { ["by"] = "hook", ["event"] = "started", ["agent"] = agent, ["session"] = "root-1", ["proc"] = _p1 }.ToJsonString();

    private async Task TwoSubagentsAsync()
    {
        ChildLineage.Start(_home, "s1", _p1, _tokens.Issuer, DateTimeOffset.UnixEpoch);
        ChildLineage.Append(_home, "s1", Started("x1"));
        ChildLineage.Append(_home, "s1", Started("x2"));
        foreach (var slice in new[] { _groups, _sessions, _quests, _driver }) await slice.JudgeAsync();
    }

    private AtWorkHeld Compose() =>
        AtWorkRead.Compose("s1", _groups, _sessions, _quests, record => ChildLineage.Reading(_home, record.Id, record.Kind, _tokens),
            id => ChildLineage.PathOf(_home, id), _standing.Word);

    /// <summary>Compose and land, as the route does.</summary>
    private string? Hint()
    {
        var held = Compose();
        _watch.Register("q", Assert.IsType<AtWorkDependencies>(held.Dependencies));
        return held.Answer!.Hint;
    }

    private static RepoView Game(params string[] lanes) => new("game", true, null) { Lanes = lanes };

    [Fact]
    public async Task The_registry_not_yet_heard_is_no_answer_of_no_lanes_and_its_first_answer_wakes_the_read()
    {
        await TwoSubagentsAsync();

        Assert.Null(Hint());
        Assert.Equal(AtWorkRouting.Unheard, _standing.Word("game"));
        Assert.False(_standing.Heard);

        // The first answer is an empty lane list for a driven repository: now the standing is `no-lanes`, and the read woke for it.
        Assert.True(_standing.Hear([Game()]));
        Assert.True(_watch.JudgeStandings());
        Assert.Equal(1, _woken);
        Assert.Equal(RoutingStanding.NoLanes, Hint());
    }

    [Fact]
    public async Task A_registry_that_does_not_name_the_repository_draws_no_hint_and_a_driver_choice_not_yet_judged_is_unheard()
    {
        await TwoSubagentsAsync();
        _standing.Hear([new RepoView("other", true, null)]);

        Assert.Equal(AtWorkRouting.Unnamed, _standing.Word("game"));
        Assert.Null(Hint());

        var unjudged = new AtWorkStanding(() => null);
        unjudged.Hear([Game()]);
        Assert.Equal(AtWorkRouting.Unheard, unjudged.Word("game"));
    }

    [Fact]
    public async Task The_hint_moves_when_driver_json_undrives_the_repository_and_at_a_lanes_only_registration()
    {
        await TwoSubagentsAsync();
        _standing.Hear([Game()]);
        Assert.Equal(RoutingStanding.NoLanes, Hint());
        var before = _woken;

        // A lanes-only registration: lanes appear, nothing else written. Driven with no trees of its own: one at a time.
        Assert.True(_standing.Hear([Game("steward")]));
        Assert.True(_watch.JudgeStandings());
        Assert.Equal(before + 1, _woken);
        Assert.Equal(RoutingStanding.OneAtATime, Hint());

        // A lane renamed leaves the standing unchanged: the read is not woken for it.
        Assert.True(_standing.Hear([Game("docs")]));
        Assert.False(_watch.JudgeStandings());
        Assert.Equal(before + 1, _woken);

        // driver.json opens trees: open, so no hint; then undrives the repository: not driven.
        _choices = new JsonObject { ["drivable"] = new JsonArray("game"), ["trees"] = new JsonArray("game") };
        await _driver.JudgeAsync();
        Assert.Equal(before + 2, _woken);
        Assert.Null(Hint());

        _choices = new JsonObject { ["drivable"] = new JsonArray(), ["trees"] = new JsonArray("game") };
        await _driver.JudgeAsync();
        Assert.Equal(before + 3, _woken);
        Assert.Equal(RoutingStanding.NotDriven, Hint());
    }

    [Fact]
    public async Task A_read_that_drew_no_hint_is_not_judged_by_the_driver_slice_or_the_registry()
    {
        ChildLineage.Start(_home, "s1", _p1, _tokens.Issuer, DateTimeOffset.UnixEpoch);
        ChildLineage.Append(_home, "s1", Started("x1"));
        foreach (var slice in new[] { _groups, _sessions, _quests, _driver }) await slice.JudgeAsync();
        _standing.Hear([Game()]);
        Assert.Null(Hint());

        Assert.True(_standing.Hear([Game("steward")]));
        Assert.False(_watch.JudgeStandings());
        _choices = new JsonObject { ["drivable"] = new JsonArray(), ["trees"] = new JsonArray() };
        await _driver.JudgeAsync();
        Assert.Equal(0, _woken);
    }
}
