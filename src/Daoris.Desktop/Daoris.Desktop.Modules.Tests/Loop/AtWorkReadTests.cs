using System.Text.Json.Nodes;
using Daoris.Driver;
using Microsoft.Extensions.Logging.Abstractions;
using Shenora.Core.Events;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// COWORKVIEW1a's read (the design §4.1, rule 10), with COWORKVIEW1b-i's: its inputs are the <c>groups</c> slice's entries and rows,
/// the <c>sessions</c> slice's records and the <c>quests</c> slice's quests as judged in memory, with each slice's health, and each
/// part's lineage file; never a service read.
/// </summary>
public sealed class AtWorkReadTests
{
    private readonly EventBus _bus = new(NullLogger<EventBus>.Instance);
    private JsonObject _entries = new();
    private JsonObject _rows = new();
    private JsonObject _records = new();
    private JsonObject _questsHeld = new();
    private string? _groupsFail;
    private string? _sessionsFail;
    private string? _questsFail;
    private bool _landed = true;

    private StateSlice Groups() => new(GroupsStateSlice.Name, _bus, _ => _groupsFail is { } why
        ? throw new InvalidOperationException(why)
        : Task.FromResult<IReadOnlyList<SliceJudged>?>(_landed ? [new(GroupsStateSlice.Entries, _entries.DeepClone()), new(GroupsStateSlice.Rows, _rows.DeepClone())] : null), "g1")
    { Keyed = true };

    private StateSlice Sessions() => new(SessionsStateSlice.Name, _bus, _ => _sessionsFail is { } why
        ? throw new InvalidOperationException(why)
        : Task.FromResult<IReadOnlyList<SliceJudged>?>(_landed ? [new("default", _records.DeepClone())] : null), "s1")
    { Keyed = true };

    private StateSlice Quests() => new(QuestsStateSlice.Name, _bus, _ => _questsFail is { } why
        ? throw new InvalidOperationException(why)
        : Task.FromResult<IReadOnlyList<SliceJudged>?>(_landed ? [new("default", _questsHeld.DeepClone())] : null), "q1")
    { Keyed = true };

    private static JsonObject Record(string id, string state, string? adapter = null, string? kind = null) => new()
    {
        ["id"] = id, ["state"] = state, ["adapter"] = adapter, ["kind"] = kind, ["note"] = "moves often",
    };

    private static JsonObject Entry(params string[] parts) => new()
    {
        ["entry"] = parts[0], ["parts"] = new JsonArray([.. parts.Select(part => (JsonNode)new JsonObject { ["session"] = part, ["shown"] = "working" })]),
    };

    private static AtWorkHeld Read(string session, StateSlice groups, StateSlice sessions, StateSlice quests, List<AtWorkRecord>? asked = null) =>
        AtWorkRead.Compose(session, groups, sessions, quests, record =>
        {
            asked?.Add(record);
            return LineageReading.None;
        });

    private async Task<(StateSlice Groups, StateSlice Sessions, StateSlice Quests)> JudgedAsync()
    {
        var (groups, sessions, quests) = (Groups(), Sessions(), Quests());
        await groups.JudgeAsync();
        await sessions.JudgeAsync();
        await quests.JudgeAsync();
        return (groups, sessions, quests);
    }

    [Fact]
    public async Task It_reads_until_every_slice_judges_and_then_answers_the_entry_s_parts_with_their_records_read_fields()
    {
        _records = new JsonObject { ["s1"] = Record("s1", "working", "claude-code", "chat"), ["s2"] = Record("s2", "completed") };
        _entries = new JsonObject { ["s2"] = Entry("s2", "s1") };
        var (groups, sessions, quests) = (Groups(), Sessions(), Quests());

        Assert.Equal(AtWork.WaitsSlices, Read("s1", groups, sessions, quests).Waits);
        await groups.JudgeAsync();
        Assert.Equal(AtWork.WaitsSlices, Read("s1", groups, sessions, quests).Waits);
        await sessions.JudgeAsync();
        Assert.Equal(AtWork.WaitsSlices, Read("s1", groups, sessions, quests).Waits);
        await quests.JudgeAsync();
        var asked = new List<AtWorkRecord>();
        var answer = Read("s1", groups, sessions, quests, asked).Answer!;

        Assert.Equal(["s1", "s2"], answer.Parts.Select(part => part.Key));
        Assert.Equal(("working", "claude-code"), (answer.Parts[0].State, answer.Parts[0].Agent));
        Assert.Contains(asked, record => record is { Id: "s1", Kind: "chat", Adapter: "claude-code" });
    }

    [Fact]
    public async Task An_ended_intake_in_no_entry_is_its_own_one_part()
    {
        _records = new JsonObject { ["i1"] = Record("i1", "completed") };
        var (groups, sessions, quests) = await JudgedAsync();

        Assert.Equal(["i1"], Read("i1", groups, sessions, quests).Answer!.Parts.Select(part => part.Key));
    }

    [Fact]
    public async Task A_sessions_slice_failing_before_it_judged_fails_with_its_words_and_after_it_answers_stale()
    {
        _records = new JsonObject { ["s1"] = Record("s1", "working") };
        var (groups, sessions, quests) = (Groups(), Sessions(), Quests());
        await groups.JudgeAsync();
        await quests.JudgeAsync();

        _sessionsFail = "the service refused";
        await sessions.JudgeAsync();
        Assert.Equal("the service refused", Read("s1", groups, sessions, quests).Failed?.Why);

        _sessionsFail = null;
        await sessions.JudgeAsync();
        _sessionsFail = "timed out";
        await sessions.JudgeAsync();
        var stale = Read("s1", groups, sessions, quests);
        Assert.Equal(("timed out", 1), (stale.Stale?.Why, stale.Answer?.Parts.Count));
    }

    [Fact]
    public async Task The_quests_slice_failing_fails_the_read_before_it_judged_and_answers_stale_after()
    {
        _records = new JsonObject { ["s1"] = Record("s1", "working") };
        var (groups, sessions, quests) = (Groups(), Sessions(), Quests());
        await groups.JudgeAsync();
        await sessions.JudgeAsync();

        _questsFail = "the quests could not be read";
        await quests.JudgeAsync();
        Assert.Equal("the quests could not be read", Read("s1", groups, sessions, quests).Failed?.Why);

        _questsFail = null;
        await quests.JudgeAsync();
        _questsFail = "timed out";
        await quests.JudgeAsync();
        Assert.Equal("timed out", Read("s1", groups, sessions, quests).Stale?.Why);
    }

    [Fact]
    public async Task An_attended_record_the_judged_slice_does_not_hold_waits_on_its_record()
    {
        var (groups, sessions, quests) = await JudgedAsync();

        Assert.Equal(AtWork.WaitsRecord, Read("s9", groups, sessions, quests).Waits);
    }

    [Fact]
    public async Task Each_part_s_wait_and_joining_are_read_from_the_entry_the_row_the_quests_and_the_record_s_take()
    {
        _records = new JsonObject
        {
            ["lead"] = Record("lead", "completed"),
            ["lane"] = Record("lane", "working"),
            ["chat"] = Record("chat", "completed", kind: "chat"),
        };
        _records["chat"]!["tookQuest"] = "t1";
        _records["lane"]!["tree"] = "X:/trees/b";
        _entries = new JsonObject
        {
            ["e"] = new JsonObject
            {
                ["parts"] = new JsonArray(
                    new JsonObject { ["session"] = "lead", ["quest"] = "q0", ["shown"] = "awaiting-reply", ["began"] = "opened" },
                    new JsonObject { ["session"] = "lane", ["quest"] = "q1", ["shown"] = "working", ["began"] = "opened", ["after"] = "lead" }),
            },
            ["c"] = new JsonObject { ["parts"] = new JsonArray(new JsonObject { ["session"] = "chat", ["shown"] = "completed", ["began"] = "opened" }) },
        };
        _rows = new JsonObject { ["lead"] = new JsonObject { ["awaits"] = "q1", ["awaitsOf"] = "core" } };
        _questsHeld = new JsonObject
        {
            ["q1"] = new JsonObject { ["id"] = "q1", ["status"] = "Taken", ["publishedBy"] = "lead", ["lanes"] = new JsonArray("core", "docs"), ["here"] = true },
            ["t1"] = new JsonObject { ["id"] = "t1", ["status"] = "Taken", ["awaits"] = "q2" },
            ["q2"] = new JsonObject { ["id"] = "q2", ["status"] = "Open", ["to"] = "docs" },
        };
        var (groups, sessions, quests) = await JudgedAsync();

        var entry = Read("lead", groups, sessions, quests).Answer!;
        var chat = Read("chat", groups, sessions, quests).Answer!.Parts.Single();

        var (lead, lane) = (entry.Parts[0], entry.Parts[1]);
        Assert.Equal(["core", "docs"], lead.Waits!.Lanes);
        Assert.Equal(("q1", "core"), (lead.Waits.Quest, lead.Waits.Of));
        Assert.Equal((1, "q1"), (lane.Joined!.ByPart, lane.Joined.Quest));
        Assert.Equal(["core", "docs"], lane.Joined.Lanes);
        Assert.Equal(1, lane.Joined.After);
        Assert.Equal(("q2", "docs", true), (chat.Waits!.Quest, chat.Waits.Of, chat.Joined!.Took));
        Assert.Equal(1, entry.Published);
        Assert.True(lane.Alongside);
    }
}
