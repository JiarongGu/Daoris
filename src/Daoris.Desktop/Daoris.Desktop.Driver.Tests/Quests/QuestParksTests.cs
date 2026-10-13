using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// What a quest's park is said with (SESSUX1i, D126 §4.6, §4.7): its last session here, how many failed, that session's
/// note and when it ended. Overview's row and the park's notice read it; it is read from the facts the list's *parked* row
/// reads (<see cref="SessionGroups"/>), so the row, the notice and the list count one number.
/// </summary>
/// <remarks>Records as JSON and in-process stand-ins only: the suite's fast half.</remarks>
public sealed class QuestParksTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);

    private static JsonObject Record(string id, string state, string quest, int at, string? note = null, bool interrupted = false) => new()
    {
        ["id"] = id,
        ["repository"] = "engine",
        ["adapter"] = "claude-code",
        ["state"] = state,
        ["kind"] = "driven",
        ["quest"] = quest,
        ["note"] = note,
        ["created"] = T0.AddMinutes(at).ToString("O"),
        ["updated"] = T0.AddMinutes(at + 1).ToString("O"),
        ["interrupted"] = interrupted,
    };

    private static string Records(params JsonObject[] records) => new JsonArray([.. records]).ToJsonString();

    private static QuestView Quest(string id, string status = "Taken", string to = "engine") =>
        new(id, "game", to, $"The work of #{id}", "A body.", status);

    private static Consideration Verdict(QuestView quest, StartVerdict verdict) => new(quest, verdict, "the planner's sentence");

    private static SessionLook Look(string records, Consideration[] considered, Func<string, int>? forgiven = null) =>
        SessionLook.From(records, [], considered, forgiven ?? (_ => 0));

    /// <summary>
    /// The owner's 1 October: three sessions failed on one quest and it parked. Its park names the third, how many failed,
    /// what the third said, and when it ended: the time *What needs you* counts the wait from.
    /// </summary>
    [Fact]
    public void A_parked_quest_names_its_last_session_how_many_failed_what_it_said_and_when_it_ended()
    {
        var look = Look(
            Records(
                Record("s1", "failed", "q1", at: 0, note: "the agent exited 1."),
                Record("s2", "failed", "q1", at: 10, note: "the agent exited 1."),
                Record("s3", "failed", "q1", at: 20, note: "You've hit your limit · resets 4pm.")),
            [Verdict(Quest("q1"), StartVerdict.Exhausted)]);

        var park = Assert.Single(SessionGroups.Parks(look));

        Assert.Equal("q1", park.Quest);
        Assert.Equal("engine", park.Repository);
        Assert.Equal("s3", park.Session);
        Assert.Equal(3, park.Strikes);
        Assert.Equal("You've hit your limit · resets 4pm.", park.Note);
        Assert.Equal(T0.AddMinutes(21), park.Since);
    }

    /// <summary>The number is the planner's: the records' failures less RETRY1's mark, as the list's parked row counts it.</summary>
    [Fact]
    public void The_number_counts_from_the_last_Try_again()
    {
        var look = Look(
            Records(Record("s1", "failed", "q1", 0), Record("s2", "failed", "q1", 10), Record("s3", "failed", "q1", 20)),
            [Verdict(Quest("q1", "Open"), StartVerdict.Exhausted)],
            forgiven: quest => quest == "q1" ? 1 : 0);

        Assert.Equal(2, Assert.Single(SessionGroups.Parks(look)).Strikes);
    }

    /// <summary>
    /// Only the planner's park is a park. A person's stop holds its quest too (SESSUX1b), and is never said: the person
    /// caused it (§4.7). Every other verdict is a wait or a hold, and none waits on the person's Try again.
    /// </summary>
    [Theory]
    [InlineData(StartVerdict.Stopped)]
    [InlineData(StartVerdict.Start)]
    [InlineData(StartVerdict.Held)]
    [InlineData(StartVerdict.Waiting)]
    [InlineData(StartVerdict.RepositoryBusy)]
    public void Only_a_quest_its_failed_sessions_parked_is_a_park(StartVerdict verdict)
    {
        var look = Look(Records(Record("s1", "stopped", "q1", 0)), [Verdict(Quest("q1"), verdict)]);

        Assert.Empty(SessionGroups.Parks(look));
    }

    /// <summary>A whitespace note is the same absence as none: a session may fail without saying why.</summary>
    [Fact]
    public void A_last_session_that_said_nothing_has_no_note()
    {
        var look = Look(
            Records(Record("s1", "stopped", "q1", 0, note: "   ", interrupted: true)),
            [Verdict(Quest("q1"), StartVerdict.Exhausted)]);

        var park = Assert.Single(SessionGroups.Parks(look));
        Assert.Equal("s1", park.Session);
        Assert.Null(park.Note);
    }

    /// <summary>
    /// A park the records no longer show a session of (a host that lost them) is still a park: its session, its number and
    /// its time are absent, never invented.
    /// </summary>
    [Fact]
    public void A_park_the_records_do_not_show_is_still_a_park_with_nothing_invented()
    {
        var park = Assert.Single(SessionGroups.Parks(Look(Records(), [Verdict(Quest("q1"), StartVerdict.Exhausted)])));

        Assert.Equal("q1", park.Quest);
        Assert.Null(park.Session);
        Assert.Null(park.Strikes);
        Assert.Null(park.Since);
    }

    /// <summary>A teammate's failures are theirs: the strikes and the last run are this machine's own (D47 §6).</summary>
    [Fact]
    public void A_teammate_record_is_never_a_park_s_last_session()
    {
        var look = Look(
            Records(Record("s1", "failed", "q1", 0), Record("laptop/s9", "failed", "q1", 30)),
            [Verdict(Quest("q1"), StartVerdict.Exhausted)]);

        var park = Assert.Single(SessionGroups.Parks(look));
        Assert.Equal("s1", park.Session);
        Assert.Equal(1, park.Strikes);
    }

    private static TickReport Tick(params Consideration[] considered) => new(considered, [], Progressed: false);

    /// <summary>
    /// The records are every session ever run (D126 M9), so they are read only when what the planner parked changed: a
    /// park lasts every look until Try again, and reading them every look for the same answer would double the look's
    /// largest read.
    /// </summary>
    [Fact]
    public async Task The_records_are_read_only_when_what_the_planner_parked_changed()
    {
        var reads = 0;
        var reader = new QuestParkReader();
        var records = Records(Record("s1", "failed", "q1", 0));
        Task<string> Read(CancellationToken _) { reads++; return Task.FromResult(records); }
        var parked = Verdict(Quest("q1"), StartVerdict.Exhausted);

        Assert.Equal("s1", Assert.Single((await reader.LookAsync(Tick(parked), _ => 0, Read))!).Session);
        Assert.Null(await reader.LookAsync(Tick(parked), _ => 0, Read));
        Assert.Null(await reader.LookAsync(Tick(parked), _ => 0, Read));
        Assert.Equal(1, reads);
        Assert.Equal("q1", Assert.Single(reader.Latest).Quest);

        // Tried again: nothing is parked, and nothing is worth a read to say so.
        Assert.Empty((await reader.LookAsync(Tick(Verdict(Quest("q1"), StartVerdict.Start)), _ => 0, Read))!);
        Assert.Equal(1, reads);
        Assert.Empty(reader.Latest);

        // Parked again: read again.
        Assert.Single((await reader.LookAsync(Tick(parked), _ => 0, Read))!);
        Assert.Equal(2, reads);
    }

    /// <summary>
    /// A look whose records could not be read says nothing of its parks, and the next look asks again: a park is never
    /// lost to one failed read, nor said from a guess.
    /// </summary>
    [Fact]
    public async Task A_read_that_failed_says_nothing_and_is_asked_again_at_the_next_look()
    {
        var fail = true;
        var reader = new QuestParkReader();
        Task<string> Read(CancellationToken _) => fail
            ? throw new HttpRequestException("refused")
            : Task.FromResult(Records(Record("s1", "failed", "q1", 0)));
        var parked = Tick(Verdict(Quest("q1"), StartVerdict.Exhausted));

        Assert.Null(await reader.LookAsync(parked, _ => 0, Read));
        Assert.Empty(reader.Latest);

        fail = false;
        Assert.Single((await reader.LookAsync(parked, _ => 0, Read))!);
    }
}
