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
            ["q1"] = new JsonObject { ["id"] = "q1", ["status"] = "Taken", ["publishedBy"] = "lead", ["lanes"] = new JsonArray("core", "docs"), ["holder"] = "desk", ["here"] = "desk" },
            ["t1"] = new JsonObject { ["id"] = "t1", ["status"] = "Taken", ["awaits"] = "q2" },
            ["q2"] = new JsonObject { ["id"] = "q2", ["status"] = "Done", ["held"] = true, ["to"] = "docs" },
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

    [Fact]
    public async Task The_quests_a_part_asked_are_read_by_the_quest_units_over_the_two_slices_with_each_taker()
    {
        _records = new JsonObject
        {
            ["lead"] = Record("lead", "completed", kind: "chat"),
            ["other"] = Record("other", "working", kind: "chat"),
            ["serving"] = Record("serving", "working", "claude-code"),
            ["lane"] = Record("lane", "working"),
        };
        _records["other"]!["tookQuest"] = "q2";
        _records["serving"]!["quest"] = "q3";
        _records["serving"]!["created"] = "2026-11-01T08:00:00Z";
        _records["lane"]!["quest"] = "q6";
        _entries = new JsonObject
        {
            ["e"] = new JsonObject
            {
                ["parts"] = new JsonArray(
                    new JsonObject { ["session"] = "lead", ["shown"] = "completed", ["began"] = "opened" },
                    new JsonObject { ["session"] = "lane", ["quest"] = "q6", ["shown"] = "working", ["began"] = "opened", ["after"] = "lead" }),
            },
        };
        JsonObject Quest(string id, string status, string by = "lead", string to = "core") =>
            new() { ["id"] = id, ["status"] = status, ["to"] = to, ["title"] = $"Title {id}", ["publishedBy"] = by };
        _questsHeld = new JsonObject
        {
            ["q1"] = Quest("q1", "Open"),
            ["q2"] = Quest("q2", "Taken"),
            ["q3"] = Quest("q3", "Taken", to: "docs"),
            ["q4"] = Quest("q4", "Taken"),
            ["q5"] = new JsonObject { ["id"] = "q5", ["status"] = "Open", ["parent"] = "q3", ["to"] = "docs", ["title"] = "Step" },
            ["q6"] = Quest("q6", "Taken"),
            ["q7"] = Quest("q7", "Open", by: "stranger"),
        };
        _questsHeld["q4"]!["holder"] = "laptop";
        _questsHeld["q4"]!["here"] = "desk";
        _questsHeld["q1"]!["short"] = "The short";
        var (groups, sessions, quests) = await JudgedAsync();

        var answer = Read("lead", groups, sessions, quests).Answer!;

        Assert.Equal(["q1", "q2", "q3", "q4", "q5"], answer.Quests!.Select(quest => quest.Id).Order());
        AtWorkAskedQuest Row(string id) => answer.Quests!.Single(quest => quest.Id == id);
        Assert.Equal(("The short", true), (Row("q1").Title, Row("q1").Taker!.Untaken == true));
        Assert.Equal("other", Row("q2").Taker!.Conversation);
        Assert.Equal(("serving", "working", "claude-code"), (Row("q3").Taker!.Session, Row("q3").Taker!.State, Row("q3").Taker!.Agent));
        Assert.Equal(("docs", true), (Row("q3").Of, Row("q4").Taker!.Elsewhere == true));
        Assert.Equal("q3", Row("q5").Step);
        // q6 opened the entry's lane part, so it is that part's, and never a row too.
        Assert.DoesNotContain(answer.Quests!, quest => quest.Id is "q6" or "q7");
        Assert.Equal("q6", answer.Parts.Single(part => part.Key == "lane").Joined!.Quest);
    }

    [Fact]
    public async Task The_asker_of_the_attended_part_s_quest_is_read_from_the_quest_it_serves_or_took()
    {
        _records = new JsonObject { ["chat"] = Record("chat", "completed", kind: "chat") };
        _records["chat"]!["tookQuest"] = "t1";
        _questsHeld = new JsonObject
        {
            ["t1"] = new JsonObject { ["id"] = "t1", ["status"] = "Taken", ["from"] = "ask #a7", ["title"] = "T" },
        };
        var (groups, sessions, quests) = await JudgedAsync();

        var asker = Read("chat", groups, sessions, quests).Answer!.AskedBy!;

        Assert.Equal(("t1", "a7"), (asker.Quest, asker.By.Ask));
    }
}
