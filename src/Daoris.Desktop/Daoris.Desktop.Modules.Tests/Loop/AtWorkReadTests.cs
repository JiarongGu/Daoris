using System.Text.Json.Nodes;
using Daoris.Driver;
using Microsoft.Extensions.Logging.Abstractions;
using Shenora.Core.Events;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// COWORKVIEW1a's read (the design §4.1, rule 10): its inputs are the <c>groups</c> slice's entries and the <c>sessions</c> slice's
/// records as judged in memory, with each slice's health, and each part's lineage file; never a service read.
/// </summary>
public sealed class AtWorkReadTests
{
    private readonly EventBus _bus = new(NullLogger<EventBus>.Instance);
    private JsonObject _entries = new();
    private JsonObject _records = new();
    private string? _groupsFail;
    private string? _sessionsFail;
    private bool _landed = true;

    private StateSlice Groups() => new(GroupsStateSlice.Name, _bus, _ => _groupsFail is { } why
        ? throw new InvalidOperationException(why)
        : Task.FromResult<IReadOnlyList<SliceJudged>?>(_landed ? [new(GroupsStateSlice.Entries, _entries.DeepClone()), new(GroupsStateSlice.Rows, new JsonObject())] : null), "g1")
    { Keyed = true };

    private StateSlice Sessions() => new(SessionsStateSlice.Name, _bus, _ => _sessionsFail is { } why
        ? throw new InvalidOperationException(why)
        : Task.FromResult<IReadOnlyList<SliceJudged>?>(_landed ? [new("default", _records.DeepClone())] : null), "s1")
    { Keyed = true };

    private static JsonObject Record(string id, string state, string? adapter = null, string? kind = null) => new()
    {
        ["id"] = id, ["state"] = state, ["adapter"] = adapter, ["kind"] = kind, ["note"] = "moves often",
    };

    private static JsonObject Entry(params string[] parts) => new()
    {
        ["entry"] = parts[0], ["parts"] = new JsonArray([.. parts.Select(part => (JsonNode)new JsonObject { ["session"] = part, ["shown"] = "working" })]),
    };

    private static AtWorkHeld Read(string session, StateSlice groups, StateSlice sessions, List<AtWorkRecord>? asked = null) =>
        AtWorkRead.Compose(session, groups, sessions, record =>
        {
            asked?.Add(record);
            return LineageReading.None;
        });

    [Fact]
    public async Task It_reads_until_both_slices_judge_and_then_answers_the_entry_s_parts_with_their_records_read_fields()
    {
        _records = new JsonObject { ["s1"] = Record("s1", "working", "claude-code", "chat"), ["s2"] = Record("s2", "completed") };
        _entries = new JsonObject { ["s2"] = Entry("s2", "s1") };
        var (groups, sessions) = (Groups(), Sessions());

        Assert.Equal(AtWork.WaitsSlices, Read("s1", groups, sessions).Waits);
        await groups.JudgeAsync();
        Assert.Equal(AtWork.WaitsSlices, Read("s1", groups, sessions).Waits);
        await sessions.JudgeAsync();
        var asked = new List<AtWorkRecord>();
        var answer = Read("s1", groups, sessions, asked).Answer!;

        Assert.Equal(["s1", "s2"], answer.Parts.Select(part => part.Key));
        Assert.Equal(("working", "claude-code"), (answer.Parts[0].State, answer.Parts[0].Agent));
        Assert.Contains(asked, record => record is { Id: "s1", Kind: "chat", Adapter: "claude-code" });
    }

    [Fact]
    public async Task An_ended_intake_in_no_entry_is_its_own_one_part()
    {
        _records = new JsonObject { ["i1"] = Record("i1", "completed") };
        var (groups, sessions) = (Groups(), Sessions());
        await groups.JudgeAsync();
        await sessions.JudgeAsync();

        Assert.Equal(["i1"], Read("i1", groups, sessions).Answer!.Parts.Select(part => part.Key));
    }

    [Fact]
    public async Task A_sessions_slice_failing_before_it_judged_fails_with_its_words_and_after_it_answers_stale()
    {
        _records = new JsonObject { ["s1"] = Record("s1", "working") };
        var (groups, sessions) = (Groups(), Sessions());
        await groups.JudgeAsync();

        _sessionsFail = "the service refused";
        await sessions.JudgeAsync();
        Assert.Equal("the service refused", Read("s1", groups, sessions).Failed?.Why);

        _sessionsFail = null;
        await sessions.JudgeAsync();
        _sessionsFail = "timed out";
        await sessions.JudgeAsync();
        var stale = Read("s1", groups, sessions);
        Assert.Equal(("timed out", 1), (stale.Stale?.Why, stale.Answer?.Parts.Count));
    }

    [Fact]
    public async Task An_attended_record_the_judged_slice_does_not_hold_waits_on_its_record()
    {
        var (groups, sessions) = (Groups(), Sessions());
        await groups.JudgeAsync();
        await sessions.JudgeAsync();

        Assert.Equal(AtWork.WaitsRecord, Read("s9", groups, sessions).Waits);
    }
}
