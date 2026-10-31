using System.Text.Json.Nodes;
using Daoris.Driver;
using Microsoft.Extensions.Logging.Abstractions;
using Shenora.Core.Events;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// COWORKVIEW1a's one judge (the design §4.1, rules 11 to 13): each landed composition registers what it read, and the judge wakes a
/// read once where any of it moved, its dependencies then leaving the union until it is asked again and lands.
/// </summary>
public sealed class AtWorkWatchTests : IDisposable
{
    private readonly string P1;
    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-atwork-" + Guid.NewGuid().ToString("N")[..8]);
    private readonly EventBus _bus = new(NullLogger<EventBus>.Instance);
    private readonly LineageTokens _tokens = new(4242, new DateTime(2026, 10, 31, 0, 0, 0, DateTimeKind.Utc));
    private readonly StateSlice _groups;
    private readonly StateSlice _sessions;
    private JsonObject _records = new() { ["s1"] = Record("s1", "working"), ["s2"] = Record("s2", "working") };
    private int _woken;

    public AtWorkWatchTests()
    {
        Directory.CreateDirectory(_home);
        // A process this window watches alive: the test's own.
        P1 = _tokens.Issue();
        _tokens.Hand(P1, System.Diagnostics.Process.GetCurrentProcess());
        var entries = new JsonObject
        {
            ["s1"] = new JsonObject { ["parts"] = new JsonArray(new JsonObject { ["session"] = "s1" }, new JsonObject { ["session"] = "s2" }) },
        };
        _groups = new StateSlice(GroupsStateSlice.Name, _bus, _ => Task.FromResult<IReadOnlyList<SliceJudged>?>([new(GroupsStateSlice.Entries, entries.DeepClone())]), "g") { Keyed = true };
        _sessions = new StateSlice(SessionsStateSlice.Name, _bus, _ => Task.FromResult<IReadOnlyList<SliceJudged>?>([new("default", _records.DeepClone())]), "s") { Keyed = true };
    }

    public void Dispose()
    {
        if (Directory.Exists(_home)) Directory.Delete(_home, recursive: true);
    }

    private static JsonObject Record(string id, string state, string note = "") => new() { ["id"] = id, ["state"] = state, ["note"] = note };

    private string PathOf(string session) => ChildLineage.PathOf(_home, session);

    private void Append(string session, string line) => ChildLineage.Append(_home, session, line);

    private string HookCall(string toolUse, string? agent) =>
        new JsonObject { ["by"] = "hook", ["event"] = "call", ["agent"] = agent, ["toolUse"] = toolUse, ["session"] = "root-1", ["proc"] = P1 }.ToJsonString();

    private string Started(string agent) =>
        new JsonObject { ["by"] = "hook", ["event"] = "started", ["agent"] = agent, ["session"] = "root-1", ["proc"] = P1 }.ToJsonString();

    private async Task<AtWorkWatch> HeldAsync()
    {
        await _groups.JudgeAsync();
        await _sessions.JudgeAsync();
        var watch = new AtWorkWatch(() => _woken++, _groups, _sessions, _tokens);
        Land(watch);
        return watch;
    }

    private AtWorkHeld Compose() =>
        AtWorkRead.Compose("s1", _groups, _sessions, record => ChildLineage.Reading(_home, record.Id, record.Kind, _tokens), PathOf);

    private void Land(AtWorkWatch watch) => watch.Register("q", Assert.IsType<AtWorkDependencies>(Compose().Dependencies));

    [Fact]
    public async Task A_held_read_no_page_asks_again_wakes_once_and_never_again()
    {
        ChildLineage.Start(_home, "s1", P1, _tokens.Issuer, DateTimeOffset.UnixEpoch);
        var watch = await HeldAsync();

        Append("s1", Started("x1"));
        Assert.True(watch.JudgeFiles());
        Append("s1", Started("x2"));
        Assert.False(watch.JudgeFiles());

        Assert.Equal(1, _woken);
    }

    [Fact]
    public async Task A_call_naming_a_held_id_or_the_root_wakes_nothing_and_one_naming_a_new_id_wakes()
    {
        ChildLineage.Start(_home, "s1", P1, _tokens.Issuer, DateTimeOffset.UnixEpoch);
        Append("s1", Started("x1"));
        var watch = await HeldAsync();

        for (var i = 0; i < 100; i++) Append("s1", HookCall($"r{i}", null));
        Append("s1", HookCall("t1", "x1"));
        Assert.False(watch.JudgeFiles());
        Append("s1", HookCall("t2", "x9"));
        Assert.True(watch.JudgeFiles());
        Assert.Equal(1, _woken);
    }

    [Fact]
    public async Task A_file_made_after_a_read_registered_it_absent_and_a_sibling_part_s_append_each_wake_it()
    {
        var watch = await HeldAsync();
        ChildLineage.Start(_home, "s2", P1, _tokens.Issuer, DateTimeOffset.UnixEpoch);
        Assert.True(watch.JudgeFiles());

        Land(watch);
        Append("s2", Started("y1"));
        Assert.True(watch.JudgeFiles());
        Assert.Equal(2, _woken);
    }

    [Fact]
    public async Task A_half_line_at_composition_completed_after_wakes_and_a_shorter_file_wakes()
    {
        ChildLineage.Start(_home, "s1", P1, _tokens.Issuer, DateTimeOffset.UnixEpoch);
        File.AppendAllText(PathOf("s1"), """{"by":"hook","event":"started","agent":"x1",""");
        var watch = await HeldAsync();

        File.AppendAllText(PathOf("s1"), "\"session\":\"root-1\",\"proc\":\"" + P1 + "\"}\n");
        Assert.True(watch.JudgeFiles());

        Land(watch);
        File.WriteAllText(PathOf("s1"), "");
        Assert.True(watch.JudgeFiles());
    }

    [Fact]
    public async Task A_record_s_read_field_moving_wakes_and_its_note_moving_alone_does_not()
    {
        var watch = await HeldAsync();

        _records = new JsonObject { ["s1"] = Record("s1", "working", note: "a word"), ["s2"] = Record("s2", "working") };
        await _sessions.JudgeAsync();
        Assert.Equal(0, _woken);

        _records = new JsonObject { ["s1"] = Record("s1", "completed"), ["s2"] = Record("s2", "working") };
        await _sessions.JudgeAsync();
        Assert.Equal(1, _woken);
        Assert.Equal(0, watch.Holding);
    }

    [Fact]
    public async Task A_token_s_reading_moving_wakes_the_read_holding_its_generation_once_with_no_line()
    {
        var token = _tokens.Issue();
        ChildLineage.Start(_home, "s1", token, _tokens.Issuer, DateTimeOffset.UnixEpoch);
        await HeldAsync();

        _tokens.Unspawned(token);

        Assert.Equal(1, _woken);
    }

    [Fact]
    public async Task A_composition_whose_inputs_moved_since_it_read_does_not_stand()
    {
        var watch = await HeldAsync();
        var read = Assert.IsType<AtWorkDependencies>(Compose().Dependencies);

        Assert.True(watch.Stands(read));
        ChildLineage.Start(_home, "s1", P1, _tokens.Issuer, DateTimeOffset.UnixEpoch);
        Assert.False(watch.Stands(read));
    }
}
