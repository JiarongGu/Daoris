using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The composition of who is at work on a session, from the entry's
/// parts, their records' read fields, each part's lineage reading and the two input slices' health, all handed as plain values.
/// </summary>
public sealed class AtWorkTests
{
    private const string P1 = "a000000000000001";
    private const string Issuer = "4242 638000000000000000";
    private static readonly DateTimeOffset T0 = new(2026, 10, 16, 8, 30, 0, TimeSpan.Zero);
    internal static readonly SliceHealth Judged = new(true);

    private static string Moment(int second) =>
        T0.AddSeconds(second).ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", System.Globalization.CultureInfo.InvariantCulture);

    private static string Start(int second = 0) => ChildBindings.StartLine(P1, Issuer, T0.AddSeconds(second))!;

    /// <summary>A subagent's start as Claude Code's stream says it, in <paramref name="proc"/>'s generation.</summary>
    private static string Child(string agent, int second, string proc = P1) =>
        new JsonObject { ["at"] = Moment(second), ["by"] = "stream", ["event"] = "started", ["agent"] = agent, ["proc"] = proc }.ToJsonString();

    /// <summary>The lines read in a window watching <see cref="P1"/>'s process alive, or <paramref name="reading"/>'s.</summary>
    private static LineageReading Read(IEnumerable<string> lines, TokenReading? reading = TokenReading.Watched) =>
        LineageReading.Of(ChildBindings.Fold(lines, null, (_, _) => reading));

    internal static AtWorkHeld Compose(
        string session, IReadOnlyList<string>? entry, IReadOnlyList<AtWorkRecord> records, Func<AtWorkRecord, LineageReading> lineage,
        SliceHealth? groups = null, SliceHealth? sessions = null) =>
        AtWork.Compose(session, entry, id => records.FirstOrDefault(record => record.Id == id), lineage, groups ?? Judged, sessions ?? Judged);

    internal static AtWorkAnswer Answered(AtWorkHeld held) => Assert.IsType<AtWorkAnswer>(held.Answer);

    /// <summary>Rule 18: the answer says when this process began, the moment <i>nothing heard</i> counts from, and it never changes.</summary>
    [Fact]
    public void The_answer_says_when_the_window_opened_and_it_is_the_same_at_every_read()
    {
        var record = new AtWorkRecord("s1", "working");
        var first = Answered(Compose("s1", null, [record], _ => LineageReading.None));
        var second = Answered(Compose("s1", null, [record], _ => LineageReading.None));

        Assert.NotNull(first.WindowOpened);
        Assert.True(first.WindowOpened <= DateTimeOffset.UtcNow);
        Assert.Equal(first.WindowOpened, second.WindowOpened);
    }

    [Fact]
    public void A_part_s_subagents_are_counted_from_its_lineage_read_with_its_record_s_kind_and_this_window_s_token_readings()
    {
        // The lineage is asked with the part's record, its kind in it, which the fold reads its history by.
        var chat = new AtWorkRecord("s1", "completed") { Kind = ChildBindings.Chat };
        string? kind = null;
        Answered(Compose("s1", null, [chat], record =>
        {
            kind = record.Kind;
            return LineageReading.None;
        }));
        Assert.Equal(ChildBindings.Chat, kind);

        // A child of a process this window watches works; one read where no token is held is not known whether it still runs.
        var driven = new AtWorkRecord("s1", "completed");
        string[] live = [Start(), Child("a1", 1)];
        var watched = Answered(Compose("s1", null, [driven], _ => Read(live)));
        var held = Answered(Compose("s1", null, [driven], _ => Read(live, reading: null)));
        Assert.Equal((1, 1), (watched.Parts[0].Children.Count, watched.Parts[0].Children.Working));
        Assert.Equal((1, 0), (held.Parts[0].Children.Count, held.Parts[0].Children.Working));
    }

    [Fact]
    public void No_file_reads_none_and_an_unreadable_one_says_so_on_its_part()
    {
        var record = new AtWorkRecord("s1", "working");

        var none = Answered(Compose("s1", null, [record], _ => LineageReading.None));
        var unreadable = Answered(Compose("s1", null, [record], _ => LineageReading.Failed("IOException")));

        Assert.Equal((0, (bool?)null), (none.Parts[0].Children.Count, none.Parts[0].Children.Unreadable));
        Assert.True(unreadable.Parts[0].Children.Unreadable);
    }

    [Fact]
    public void Until_both_slices_judge_it_reads_and_a_slice_failing_before_its_first_judgement_fails_with_its_words()
    {
        var record = new AtWorkRecord("s1", "working");

        var waiting = Compose("s1", null, [record], _ => LineageReading.None, sessions: new SliceHealth(false));
        var failed = Compose("s1", null, [record], _ => LineageReading.None, groups: new SliceHealth(false, "the service refused"));

        Assert.Equal((true, AtWork.WaitsSlices), (waiting.Pending, waiting.Waits));
        Assert.Null(waiting.Answer);
        Assert.Equal("the service refused", failed.Failed?.Why);
        Assert.Null(failed.Answer);
    }

    [Fact]
    public void A_slice_failing_over_what_it_judged_answers_that_stale_with_its_words()
    {
        var record = new AtWorkRecord("s1", "working");

        var stale = Compose("s1", null, [record], _ => LineageReading.None, sessions: new SliceHealth(true, "timed out"));

        Assert.NotNull(stale.Answer);
        Assert.Equal("timed out", stale.Stale?.Why);
    }

    [Fact]
    public void An_attended_record_the_sessions_slice_does_not_hold_yet_reads_as_waiting_on_its_record()
    {
        var held = Compose("s9", null, [new AtWorkRecord("s1", "working")], _ => LineageReading.None);

        Assert.Equal((true, AtWork.WaitsRecord), (held.Pending, held.Waits));
    }

    [Fact]
    public void The_parts_are_the_strip_s_set_the_attended_one_first_each_with_its_record_s_own_state_and_a_teammate_s_never_read()
    {
        AtWorkRecord[] records =
        [
            new("s1", "completed") { Adapter = "claude-code" },
            new("s2", "working") { Adapter = "codex" },
            new("s3", "waiting"),
        ];
        var read = new List<string>();

        var answer = Answered(Compose("s2", ["s1", "s2", "origin/s7", "s3"], records, record =>
        {
            read.Add(record.Id);
            return record.Id == "s1" ? Read([Start(), Child("a1", 1)]) : LineageReading.None;
        }));

        Assert.Equal(["s2", "s1", "origin/s7", "s3"], answer.Parts.Select(part => part.Key));
        Assert.Equal(("completed", "claude-code"), (answer.Parts[1].State, answer.Parts[1].Agent));
        Assert.Equal((1, 1), (answer.Parts[1].Children.Count, answer.Parts[1].Children.Working));
        Assert.True(answer.Parts[2].Children.Elsewhere);
        Assert.Null(answer.Parts[2].Children.Count);
        Assert.DoesNotContain("origin/s7", read);
        Assert.Equal("s2", answer.Attended);
    }

    [Fact]
    public void An_attended_teammate_s_part_counts_its_subagents_as_not_visible_here_and_never_reads_them()
    {
        var read = new List<string>();
        var answer = Answered(Compose("origin/s7", ["s1", "origin/s7"], [new AtWorkRecord("origin/s7", "working"), new AtWorkRecord("s1", "working")],
            record =>
            {
                read.Add(record.Id);
                return LineageReading.None;
            }));

        Assert.Equal(("origin/s7", true), (answer.Parts[0].Key, answer.Parts[0].Children.Elsewhere));
        Assert.DoesNotContain("origin/s7", read);
    }

    [Fact]
    public void An_attended_record_in_no_entry_is_its_own_one_part()
    {
        var answer = Answered(Compose("s1", null, [new AtWorkRecord("s1", "completed")], _ => LineageReading.None));

        Assert.Equal(["s1"], answer.Parts.Select(part => part.Key));
    }

    [Fact]
    public void Not_known_children_and_those_whose_agent_ended_are_counted_but_never_working()
    {
        string[] lines = [Start(), Child("open", 1), Child("old", 2, proc: "b000000000000002")];
        var answer = Answered(Compose("s1", null, [new AtWorkRecord("s1", "working")], _ => LineageReading.Of(ChildBindings.Fold(
            [.. lines, ChildBindings.StartLine("b000000000000002", "1 1", T0)!, ChildBindings.StartLine("c000000000000003", Issuer, T0)!,
                Child("gone", 3, proc: "c000000000000003")],
            null, (proc, _) => proc switch { P1 => TokenReading.Watched, "c000000000000003" => TokenReading.Gone, _ => null }))));

        Assert.Equal((3, 1), (answer.Parts[0].Children.Count, answer.Parts[0].Children.Working));
    }

    [Fact]
    public void Forty_five_parts_draw_forty_and_count_the_rest_by_their_record_s_state()
    {
        var ids = Enumerable.Range(0, 45).Select(i => $"s{i:00}").ToList();
        var records = ids.Select(id => new AtWorkRecord(id, id == "s44" ? "failed" : "completed")).ToList();

        var answer = Answered(Compose("s00", ids, records, _ => LineageReading.None));

        Assert.Equal(AtWork.Bound, answer.Parts.Count);
        Assert.Equal(new Dictionary<string, int> { ["completed"] = 4, ["failed"] = 1 }, answer.MoreParts);
    }
}
