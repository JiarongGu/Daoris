using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// AGENTPROVIDER1 s-ii (the agent-provider design §7 row 3; D205 point 1): the sessions' groups report a resume's wait where the continuation
/// resumes its record's own conversation, by one rule (<see cref="OwnResume"/>), on the record's own door and never the machine's
/// <c>adapter</c> key. A reader by the machine's key is planted beside it and disagrees on Build's second choice, so the comparison holds.
/// </summary>
public sealed class OwnResumeReportTests : IDisposable
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 7, 9, 0, 0, TimeSpan.Zero);

    private static readonly CoolingEntry Cooling = new("claude", "work", T0.AddHours(2), Stated: true, Window: null, Seen: T0, Session: null);

    private const string Machine = "claude-code";

    private readonly string _home = Path.Combine(TestHomes.Root, "daoris-own-resume-" + Guid.NewGuid().ToString("N")[..8]);

    public OwnResumeReportTests() => Directory.CreateDirectory(DaorisHome.Entry(_home, HomeEntry.Sessions));

    public void Dispose()
    {
        if (Directory.Exists(_home)) Directory.Delete(_home, recursive: true);
    }

    private static JsonObject Record(string id, string? adapter, string? asked = null)
    {
        var record = new JsonObject
        {
            ["id"] = id, ["repository"] = "engine", ["state"] = "completed", ["kind"] = "driven", ["quest"] = "q-" + id, ["profile"] = "work",
            ["created"] = T0.ToString("O"), ["updated"] = T0.AddMinutes(5).ToString("O"),
            ["said"] = new JsonArray(new JsonObject
            {
                ["id"] = "w-" + id, ["text"] = "go on", ["at"] = T0.AddMinutes(10).ToString("O"), ["files"] = new JsonArray(), ["reopens"] = true,
            }),
        };
        if (adapter is not null) record["door"] = adapter;
        if (asked is not null) record["asked"] = new JsonObject { ["agent"] = asked };
        return record;
    }

    /// <summary>
    /// Each case: the record's own door, the agent its kept conversation was bound under, and the agent its start goes on with, as the walk
    /// would run it (its record's asked choice, else the door it opened on, else Build's first).
    /// </summary>
    public static TheoryData<string, string?, string?, string?> Cases() => new()
    {
        { "own", Machine, null, null },
        { "second", "codex", null, null },
        { "kept", null, "codex", null },
        { "keptOwn", null, Machine, null },
        { "asked", Machine, null, "codex" },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public void The_group_s_report_of_a_resume_agrees_with_the_continuation_s_rule(string id, string? adapter, string? kept, string? asked)
    {
        var json = new JsonArray(Record(id, adapter, asked)).ToJsonString();
        if (kept is not null) new HarnessConversations(_home).Keep(id, kept, "conv-" + id);
        var start = new Consideration(new QuestView("q-" + id, "game", "engine", "Work.", "A body.", "Taken"), StartVerdict.Start, "going on")
        {
            GoesOn = true, Resumes = ServiceClient.ReadRecord(json, id),
        };
        var config = DriverConfig.Empty with { Actors = StaffedRoles.On(Machine) };
        var agent = StartPlan.ContinuedOn(start.Resumes, config);

        var continues = OwnResume.Of(start, agent, session => new HarnessConversations(_home).Read(session)?.Adapter) is not null;
        var reported = SessionGroups.Waiting(
                SessionLook.From(json, [], [start], _ => 0), json, _home, config, fresh: true, (_, profile) => profile == "work" ? Cooling : null,
                TimeZoneInfo.Utc)
            .Held.ContainsKey(id);

        Assert.Equal(continues, reported);
    }

    /// <summary>The old read, by the machine's <c>adapter</c> key, planted: it disagrees with the group on Build's second choice.</summary>
    [Fact]
    public void A_report_by_the_machine_s_adapter_key_disagrees_on_build_s_second_choice()
    {
        var json = new JsonArray(Record("second", "codex")).ToJsonString();
        var start = new Consideration(new QuestView("q-second", "game", "engine", "Work.", "A body.", "Taken"), StartVerdict.Start, "going on")
        {
            GoesOn = true, Resumes = ServiceClient.ReadRecord(json, "second"),
        };
        var config = DriverConfig.Empty with { Actors = StaffedRoles.On(Machine) };

        var byMachine = OwnResume.Of(start, Machine, _ => null) is not null;
        var reported = SessionGroups.Waiting(
                SessionLook.From(json, [], [start], _ => 0), json, _home, config, fresh: true, (_, profile) => profile == "work" ? Cooling : null,
                TimeZoneInfo.Utc)
            .Held.ContainsKey("second");

        Assert.True(reported);
        Assert.NotEqual(byMachine, reported);
    }
}
