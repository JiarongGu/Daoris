using Daoris.Desktop.Driver.Tests;
using System.Text.Json.Nodes;
using Daoris.Driver;
using Microsoft.Extensions.Logging.Abstractions;
using Shenora.Core.Events;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// The one judge: each landed composition registers what it read, and the judge wakes a
/// read once where any of it moved, its dependencies then leaving the union until it is asked again and lands.
/// </summary>
public sealed class AtWorkWatchTests : IDisposable
{
    private readonly string P1;
    private readonly string _home = Path.Combine(TestHomes.Root, "daoris-atwork-" + Guid.NewGuid().ToString("N")[..8]);
    private readonly EventBus _bus = new(NullLogger<EventBus>.Instance);
    private readonly LineageTokens _tokens = new(4242, new DateTime(2026, 10, 31, 0, 0, 0, DateTimeKind.Utc));
    private readonly StateSlice _groups;
    private readonly StateSlice _sessions;
    private readonly StateSlice _quests;
    private JsonObject _records = new() { ["s1"] = Record("s1", "working"), ["s2"] = Record("s2", "working") };
    private JsonObject _rows = new() { ["s1"] = new JsonObject { ["awaits"] = "q9", ["awaitsOf"] = "core", ["updated"] = "1" } };
    private JsonObject _questsHeld = new()
    {
        ["q9"] = new JsonObject { ["id"] = "q9", ["status"] = "Open", ["to"] = "core", ["title"] = "a" },
        ["q1"] = new JsonObject { ["id"] = "q1", ["status"] = "Open", ["publishedBy"] = "s1" },
        ["q2"] = new JsonObject { ["id"] = "q2", ["status"] = "Open" },
    };
    private int _woken;

    public AtWorkWatchTests()
    {
        Directory.CreateDirectory(_home);
        // A process this window watches alive: the test's own.
        P1 = _tokens.Issue();
        _tokens.Hand(P1, System.Diagnostics.Process.GetCurrentProcess());
        var entries = new JsonObject
        {
            ["s1"] = new JsonObject
            {
                ["parts"] = new JsonArray(
                    new JsonObject { ["session"] = "s1", ["shown"] = "awaiting-reply" }, new JsonObject { ["session"] = "s2", ["shown"] = "working" }),
            },
        };
        _groups = new StateSlice(GroupsStateSlice.Name, _bus,
            _ => Task.FromResult<IReadOnlyList<SliceJudged>?>([new(GroupsStateSlice.Entries, entries.DeepClone()), new(GroupsStateSlice.Rows, _rows.DeepClone())]), "g")
        { Keyed = true };
        _sessions = new StateSlice(SessionsStateSlice.Name, _bus, _ => Task.FromResult<IReadOnlyList<SliceJudged>?>([new("default", _records.DeepClone())]), "s") { Keyed = true };
        _quests = new StateSlice(QuestsStateSlice.Name, _bus, _ => Task.FromResult<IReadOnlyList<SliceJudged>?>([new("default", _questsHeld.DeepClone())]), "q") { Keyed = true };
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
        await _quests.JudgeAsync();
        var watch = new AtWorkWatch(() => _woken++, _groups, _sessions, _quests, _tokens);
        Land(watch);
        return watch;
    }

    private AtWorkHeld Compose() =>
        AtWorkRead.Compose("s1", _groups, _sessions, _quests, record => ChildLineage.Reading(_home, record.Id, record.Kind, _tokens), PathOf);

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
    public async Task The_wait_a_part_is_read_by_is_a_dependency_and_a_row_field_the_read_never_asked_for_is_not()
    {
        var watch = await HeldAsync();
        var wait = Compose().Answer!.Parts.Single(part => part.Key == "s1").Waits;
        Assert.Equal(("q9", "core"), (wait?.Quest, wait?.Of));

        // The row's other fields move at every look: nothing woken.
        _rows = new JsonObject { ["s1"] = new JsonObject { ["awaits"] = "q9", ["awaitsOf"] = "core", ["updated"] = "2" } };
        await _groups.JudgeAsync();
        Assert.Equal(0, _woken);

        _rows = new JsonObject { ["s1"] = new JsonObject { ["awaits"] = "q8", ["awaitsOf"] = "core" } };
        await _groups.JudgeAsync();
        Assert.Equal(1, _woken);
        Assert.Equal(0, watch.Holding);
    }

    [Fact]
    public async Task A_quest_the_read_looked_up_moving_wakes_and_one_it_never_looked_up_does_not()
    {
        await HeldAsync();

        _questsHeld["q2"] = new JsonObject { ["id"] = "q2", ["status"] = "Taken" };
        await _quests.JudgeAsync();
        Assert.Equal(0, _woken);

        _questsHeld["q9"] = new JsonObject { ["id"] = "q9", ["status"] = "Open", ["to"] = "core", ["lanes"] = new JsonArray("docs") };
        await _quests.JudgeAsync();
        Assert.Equal(1, _woken);
    }

    [Fact]
    public async Task A_session_s_published_count_moving_wakes_the_read()
    {
        var watch = await HeldAsync();
        Assert.Equal(1, Compose().Answer!.Published);

        _questsHeld["q2"] = new JsonObject { ["id"] = "q2", ["status"] = "Open", ["publishedBy"] = "s1" };
        await _quests.JudgeAsync();
        Assert.Equal(1, _woken);

        Land(watch);
        Assert.Equal(2, Compose().Answer!.Published);
    }

    [Fact]
    public async Task What_the_quests_half_reads_of_a_taker_wakes_the_read_and_what_it_does_not_read_does_not()
    {
        var watch = await HeldAsync();
        Assert.True(Compose().Answer!.Quests!.Single().Taker!.Untaken);

        // A session that serves nothing the entry asked, and a record's note, move nothing the row says.
        _records["s7"] = Record("s7", "working", "elsewhere");
        _records["s2"] = Record("s2", "working", "a note that moved");
        await _sessions.JudgeAsync();
        Assert.Equal(0, _woken);

        // An open quest is untaken whoever's record names it; the take that moves the row is the quest's own status.
        _records["s3"] = new JsonObject { ["id"] = "s3", ["state"] = "working", ["quest"] = "q1", ["door"] = "claude-code" };
        await _sessions.JudgeAsync();
        Assert.Equal(0, _woken);

        _questsHeld["q1"] = new JsonObject { ["id"] = "q1", ["status"] = "Taken", ["publishedBy"] = "s1" };
        await _quests.JudgeAsync();
        Assert.Equal(1, _woken);
        Land(watch);
        Assert.Equal("s3", Compose().Answer!.Quests!.Single().Taker!.Session);
    }

    [Fact]
    public async Task A_taker_s_state_moving_wakes_the_read_once_it_is_on_the_row()
    {
        _questsHeld["q1"] = new JsonObject { ["id"] = "q1", ["status"] = "Taken", ["publishedBy"] = "s1" };
        _records["s3"] = new JsonObject { ["id"] = "s3", ["state"] = "working", ["quest"] = "q1" };
        await HeldAsync();

        _records["s3"] = new JsonObject { ["id"] = "s3", ["state"] = "completed", ["quest"] = "q1" };
        await _sessions.JudgeAsync();

        Assert.Equal(1, _woken);
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
