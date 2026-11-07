using Daoris.Desktop.Driver.Tests;
using System.Text.Json.Nodes;
using Daoris.Driver;
using Microsoft.Extensions.Logging.Abstractions;
using Shenora.Core.Events;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// COWORKVIEW1d: an ask's read (the design §3.5, §R11.5), from the <c>groups</c> slice's entries whose origin is the ask, the <c>quests</c> and
/// <c>sessions</c> slices, and the <c>asks</c> slice as an existence dependency: its health, its fence and its wakes. A held ask with nothing
/// at work, an ask the slice does not hold, and an ask deleted after it was opened are told apart.
/// </summary>
public sealed class AtWorkAskReadTests : IDisposable
{
    private readonly string _home = Path.Combine(TestHomes.Root, "daoris-ask-read-" + Guid.NewGuid().ToString("N")[..8]);
    private readonly EventBus _bus = new(NullLogger<EventBus>.Instance);
    private readonly LineageTokens _tokens = new(4242, new DateTime(2026, 11, 2, 0, 0, 0, DateTimeKind.Utc));
    private readonly StateSlice _groups;
    private readonly StateSlice _sessions;
    private readonly StateSlice _quests;
    private readonly StateSlice _asks;
    private readonly AtWorkWatch _watch;
    private JsonObject _entries = new();
    private JsonObject _records = new();
    private JsonObject _questsHeld = new();
    private JsonObject _asksHeld = new() { ["a1"] = new JsonObject { ["id"] = "a1", ["workspace"] = "default", ["state"] = "Proposed" } };
    private string? _asksFail;
    private int _woken;

    public AtWorkAskReadTests()
    {
        Directory.CreateDirectory(_home);
        _groups = new StateSlice(GroupsStateSlice.Name, _bus,
            _ => Task.FromResult<IReadOnlyList<SliceJudged>?>([new(GroupsStateSlice.Entries, _entries.DeepClone()), new(GroupsStateSlice.Rows, new JsonObject())]), "g")
        { Keyed = true };
        _sessions = new StateSlice(SessionsStateSlice.Name, _bus, _ => Task.FromResult<IReadOnlyList<SliceJudged>?>([new("default", _records.DeepClone())]), "s") { Keyed = true };
        _quests = new StateSlice(QuestsStateSlice.Name, _bus, _ => Task.FromResult<IReadOnlyList<SliceJudged>?>([new("default", _questsHeld.DeepClone())]), "q") { Keyed = true };
        _asks = new StateSlice(AsksStateSlice.Name, _bus, _ => _asksFail is { } why
            ? throw new InvalidOperationException(why)
            : Task.FromResult<IReadOnlyList<SliceJudged>?>([new("default", _asksHeld.DeepClone())]), "a")
        { Keyed = true };
        _watch = new AtWorkWatch(() => _woken++, _groups, _sessions, _quests, _tokens, asks: _asks);
    }

    public void Dispose()
    {
        if (Directory.Exists(_home)) Directory.Delete(_home, recursive: true);
    }

    private static JsonObject Entry(string key, string origin, string repository, params string[] parts) => new()
    {
        ["entry"] = key, ["origin"] = origin, ["repository"] = repository, ["leading"] = parts[^1], ["opens"] = parts[^1],
        ["parts"] = new JsonArray([.. parts.Select(part => (JsonNode)new JsonObject { ["session"] = part, ["shown"] = "working" })]),
    };

    private static JsonObject Record(string id, string state, string? quest = null) => new() { ["id"] = id, ["state"] = state, ["quest"] = quest, ["repository"] = "engine" };

    private async Task JudgeAllAsync()
    {
        foreach (var slice in new[] { _groups, _sessions, _quests, _asks }) await slice.JudgeAsync();
    }

    private AtWorkHeld Compose(string ask = "a1") =>
        AtWorkRead.ComposeAsk(ask, _groups, _sessions, _quests, _asks, record => ChildLineage.Reading(_home, record.Id, record.Kind, _tokens), id => ChildLineage.PathOf(_home, id));

    private AtWorkHeld Land(string ask = "a1")
    {
        var held = Compose(ask);
        _watch.Register(ask, Assert.IsType<AtWorkDependencies>(held.Dependencies));
        return held;
    }

    [Fact]
    public async Task It_reads_until_the_asks_slice_judges_too()
    {
        foreach (var slice in new[] { _groups, _sessions, _quests }) await slice.JudgeAsync();

        Assert.Equal(AtWork.WaitsSlices, Compose().Waits);
        await _asks.JudgeAsync();
        Assert.NotNull(Compose().AskAnswer);
    }

    [Fact]
    public async Task A_held_ask_with_nothing_at_work_is_valid_and_empty_and_an_unknown_ask_is_neither()
    {
        await JudgeAllAsync();

        Assert.Empty(Compose("a1").AskAnswer!.Entries);
        var unknown = Compose("zz");
        Assert.Null(unknown.AskAnswer);
        Assert.Equal((true, AtWorkAsk.WaitsAsk), (unknown.Pending, unknown.Waits));
    }

    [Fact]
    public async Task Its_lines_of_work_are_the_entries_whose_origin_is_the_ask_less_an_intake_and_its_quests_are_those_it_asked()
    {
        _entries = new JsonObject
        {
            ["e1"] = Entry("e1", "ask:a1", "engine", "s1", "s2"),
            ["e2"] = Entry("e2", "ask:a1", "intake", "i1"),
            ["e3"] = Entry("e3", "ask:a9", "engine", "s5"),
            ["e4"] = Entry("e4", "quest:q4", "engine", "s6"),
        };
        _records = new JsonObject { ["s1"] = Record("s1", "completed"), ["s2"] = Record("s2", "working", "q1"), ["s5"] = Record("s5", "working") };
        _questsHeld = new JsonObject
        {
            ["q1"] = new JsonObject { ["id"] = "q1", ["status"] = "Taken", ["from"] = "ask #a1", ["to"] = "engine", ["title"] = "Index it" },
            ["q2"] = new JsonObject { ["id"] = "q2", ["status"] = "Open", ["from"] = "ask #a1", ["to"] = "docs", ["title"] = "Write it up" },
            ["q3"] = new JsonObject { ["id"] = "q3", ["status"] = "Open", ["from"] = "ask #a9", ["to"] = "docs", ["title"] = "Another ask's" },
        };
        await JudgeAllAsync();

        var answer = Compose().AskAnswer!;

        var entry = Assert.Single(answer.Entries);
        Assert.Equal(("e1", "engine", "s2", 2, 1), (entry.Entry, entry.Repository, entry.Opens, entry.Parts, entry.Working));
        Assert.Equal(["q1", "q2"], answer.Quests!.Select(quest => quest.Id).Order());
        Assert.Equal("s2", answer.Quests!.Single(quest => quest.Id == "q1").Taker?.Session);
        Assert.True(answer.Quests!.Single(quest => quest.Id == "q2").Taker?.Untaken);
    }

    [Fact]
    public async Task An_ask_deleted_after_it_was_opened_wakes_the_read_and_is_no_empty_ask_then()
    {
        await JudgeAllAsync();
        Assert.NotNull(Land().AskAnswer);
        Assert.Equal(0, _woken);

        _asksHeld = new JsonObject();
        await _asks.JudgeAsync();

        Assert.Equal(1, _woken);
        var gone = Compose();
        Assert.Null(gone.AskAnswer);
        Assert.Equal(AtWorkAsk.WaitsAsk, gone.Waits);
    }

    [Fact]
    public async Task The_asks_slice_failing_after_it_judged_wakes_the_read_and_it_answers_stale()
    {
        await JudgeAllAsync();
        Land();

        _asksFail = "the service refused";
        await _asks.JudgeAsync();

        Assert.Equal(1, _woken);
        var stale = Compose();
        Assert.NotNull(stale.AskAnswer);
        Assert.Equal("the service refused", stale.Stale?.Why);
    }

    [Fact]
    public async Task A_new_line_of_work_a_session_s_state_and_a_quest_of_the_ask_wake_it_and_what_is_not_the_asks_wakes_nothing()
    {
        _records = new JsonObject { ["s1"] = Record("s1", "working"), ["x1"] = Record("x1", "working") };
        _entries = new JsonObject { ["e1"] = Entry("e1", "ask:a1", "engine", "s1") };
        await JudgeAllAsync();
        Land();

        // Another ask's entry and a quest of another ask move nothing this read holds.
        _entries["e9"] = Entry("e9", "ask:a9", "docs", "x9");
        _questsHeld["q9"] = new JsonObject { ["id"] = "q9", ["status"] = "Open", ["from"] = "ask #a9", ["to"] = "docs", ["title"] = "x" };
        await _groups.JudgeAsync();
        await _quests.JudgeAsync();
        Assert.Equal(0, _woken);

        // A part's record moving does.
        _records["s1"] = Record("s1", "completed");
        await _sessions.JudgeAsync();
        Assert.Equal(1, _woken);
        Assert.Equal(0, Assert.Single(Land().AskAnswer!.Entries).Working);

        // A quest of the ask appearing does.
        _questsHeld["q1"] = new JsonObject { ["id"] = "q1", ["status"] = "Open", ["from"] = "ask #a1", ["to"] = "engine", ["title"] = "Index it" };
        await _quests.JudgeAsync();
        Assert.Equal(2, _woken);
        Land();

        // And a new line of work of the ask.
        _entries["e2"] = Entry("e2", "ask:a1", "docs", "x1");
        await _groups.JudgeAsync();
        Assert.Equal(3, _woken);
        Assert.Equal(2, Compose().AskAnswer!.Entries.Count);
    }
}
