using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// COWORKVIEW1a's composition (the design §3.8, §4.1, rules 9, 10, 14, 16 and 17): who is at work on a session, from the entry's
/// parts, their records' read fields, each part's lineage reading and the two input slices' health, all handed as plain values.
/// </summary>
public sealed class AtWorkTests
{
    private const string P1 = "a000000000000001";
    private const string Issuer = "4242 638000000000000000";
    private static readonly DateTimeOffset T0 = new(2026, 10, 16, 8, 30, 0, TimeSpan.Zero);
    private static readonly SliceHealth Judged = new(true);

    private static string Moment(int second) =>
        T0.AddSeconds(second).ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", System.Globalization.CultureInfo.InvariantCulture);

    private static string Start(int second = 0) => ChildBindings.StartLine(P1, Issuer, T0.AddSeconds(second))!;

    /// <summary>A subagent's start as Claude Code's stream says it, under <paramref name="parent"/> where one is named.</summary>
    private static string Child(string agent, int second, string? parent = null, int? depth = null, string? proc = P1)
    {
        var line = new JsonObject { ["at"] = Moment(second), ["by"] = "stream", ["event"] = "started", ["agent"] = agent };
        if (parent is not null) line["parent"] = parent;
        if (depth is not null) line["depth"] = depth;
        if (proc is not null) line["proc"] = proc;
        return line.ToJsonString();
    }

    private static string Stop(string agent, int second, string status = "completed") =>
        new JsonObject { ["at"] = Moment(second), ["by"] = "stream", ["event"] = "ended", ["agent"] = agent, ["status"] = status, ["proc"] = P1 }
            .ToJsonString();

    /// <summary>The lines read in a window watching <see cref="P1"/>'s process alive, or <paramref name="reading"/>'s.</summary>
    private static LineageReading Read(IEnumerable<string> lines, string? kind = null, TokenReading? reading = TokenReading.Watched) =>
        LineageReading.Of(ChildBindings.Fold(lines, kind, (_, _) => reading));

    private static AtWorkHeld Compose(
        string session, IReadOnlyList<string>? entry, IReadOnlyList<AtWorkRecord> records, Func<AtWorkRecord, LineageReading> lineage,
        SliceHealth? groups = null, SliceHealth? sessions = null) =>
        AtWork.Compose(session, entry, id => records.FirstOrDefault(record => record.Id == id), lineage, groups ?? Judged, sessions ?? Judged);

    private static AtWorkAnswer Answered(AtWorkHeld held) => Assert.IsType<AtWorkAnswer>(held.Answer);

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
    public void A_part_s_subagents_are_read_from_its_lineage_with_its_record_s_kind_and_this_window_s_token_readings()
    {
        // A chat's file from before row a closes its child at its clean end; a driven record's claims no boundary there.
        string[] history = [Child("a1", 1, proc: null), ChildBindings.EndLine(true, T0.AddSeconds(2))];
        var chat = new AtWorkRecord("s1", "completed") { Kind = ChildBindings.Chat };
        var driven = new AtWorkRecord("s1", "completed");

        Assert.Equal(ChildBindings.Ended, Answered(Compose("s1", null, [chat], record => Read(history, record.Kind))).Subagents.Children[0].State);
        Assert.Equal(ChildBindings.Unrecorded, Answered(Compose("s1", null, [driven], record => Read(history, record.Kind))).Subagents.Children[0].State);

        // A child of a process this window watches works; one read where no token is held is not known whether it still runs.
        string[] live = [Start(), Child("a1", 1)];
        var watched = Answered(Compose("s1", null, [driven], _ => Read(live)));
        var held = Answered(Compose("s1", null, [driven], _ => Read(live, reading: null)));
        Assert.Equal((ChildBindings.Working, 1), (watched.Subagents.Children[0].State, watched.Subagents.Working));
        Assert.Equal((ChildBindings.Uncertain, 0), (held.Subagents.Children[0].State, held.Subagents.Working));
    }

    [Fact]
    public void Its_turn_waits_for_working_children_on_the_one_adapter_that_holds_it_and_never_for_an_unsaid_run()
    {
        string[] lines = [Start(), Child("a1", 1)];
        var acp = new AtWorkRecord("s1", "working") { Adapter = "claude-code-acp" };
        var pipe = new AtWorkRecord("s1", "working") { Adapter = "claude-code" };

        Assert.True(Answered(Compose("s1", null, [acp], _ => Read(lines))).Subagents.TurnWaits);
        Assert.Null(Answered(Compose("s1", null, [pipe], _ => Read(lines))).Subagents.TurnWaits);
        Assert.Null(Answered(Compose("s1", null, [acp], _ => Read([Start(), Stop("a1", 2)]))).Subagents.TurnWaits);
    }

    [Fact]
    public void A_codex_part_says_its_children_appear_only_by_their_calls()
    {
        var codex = new AtWorkRecord("s1", "working") { Adapter = "codex" };
        var claude = new AtWorkRecord("s1", "working") { Adapter = "claude-code" };

        Assert.True(Answered(Compose("s1", null, [codex], _ => LineageReading.None)).Subagents.SeenByCalls);
        Assert.Null(Answered(Compose("s1", null, [claude], _ => LineageReading.None)).Subagents.SeenByCalls);
    }

    [Fact]
    public void No_file_reads_none_and_an_unreadable_one_says_so_with_its_kind_on_the_group_and_its_part()
    {
        var record = new AtWorkRecord("s1", "working");

        var none = Answered(Compose("s1", null, [record], _ => LineageReading.None));
        var unreadable = Answered(Compose("s1", null, [record], _ => LineageReading.Failed("IOException")));

        Assert.Equal(LineageReading.Absent, none.Subagents.Read);
        Assert.Equal((LineageReading.Unreadable, "IOException"), (unreadable.Subagents.Read, unreadable.Subagents.Unreadable));
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
    public void An_attended_teammate_s_part_draws_its_subagents_as_not_visible_here()
    {
        var answer = Answered(Compose("origin/s7", ["s1", "origin/s7"], [new AtWorkRecord("origin/s7", "working"), new AtWorkRecord("s1", "working")],
            _ => LineageReading.None));

        Assert.Equal(LineageReading.Elsewhere, answer.Subagents.Read);
    }

    [Fact]
    public void An_attended_record_in_no_entry_is_its_own_one_part()
    {
        var answer = Answered(Compose("s1", null, [new AtWorkRecord("s1", "completed")], _ => LineageReading.None));

        Assert.Equal(["s1"], answer.Parts.Select(part => part.Key));
    }

    [Fact]
    public void Forty_one_working_children_draw_forty_rows_and_count_the_one_left_out_by_its_state()
    {
        var lines = new List<string> { Start() };
        for (var i = 0; i < 41; i++) lines.Add(Child($"c{i:00}", i));

        var subagents = Answered(Compose("s1", null, [new AtWorkRecord("s1", "working")], _ => Read(lines))).Subagents;

        Assert.Equal(AtWork.Bound, subagents.Children.Count);
        Assert.Equal(41, subagents.Working);
        Assert.Equal(new Dictionary<string, int> { [ChildBindings.Working] = 1 }, subagents.More);
        // The newest start first: the oldest is the one left out.
        Assert.DoesNotContain(subagents.Children, child => child.Key == "c00");
    }

    [Fact]
    public void A_child_is_drawn_only_under_its_whole_chain_and_one_whose_chain_does_not_fit_is_passed_over_for_a_shorter()
    {
        // Twenty-five working children, then a chain of twenty ended ancestors under a working child, then a chain of twelve.
        var lines = new List<string> { Start() };
        for (var i = 0; i < 25; i++) lines.Add(Child($"w{i:00}", 100 + i));
        AddChain(lines, "long", 20, 10);
        AddChain(lines, "short", 12, 50);

        var subagents = Answered(Compose("s1", null, [new AtWorkRecord("s1", "working")], _ => Read(lines))).Subagents;
        var drawn = subagents.Children.Select(child => child.Key).ToHashSet();

        Assert.Contains("short11", drawn);
        Assert.Contains("short00", drawn);
        Assert.DoesNotContain("long19", drawn);
        Assert.All(subagents.Children, child => Assert.True(child.ParentKey is null || child.ParentKey == "root" || drawn.Contains(child.ParentKey)));
        Assert.Equal(25 + 20 + 12, subagents.Children.Count + (subagents.Folded ?? 0) + (subagents.More?.Values.Sum() ?? 0));
    }

    [Fact]
    public void Past_the_newest_three_ended_children_fold_and_each_child_is_counted_once()
    {
        var lines = new List<string> { Start() };
        for (var i = 0; i < 6; i++)
        {
            lines.Add(Child($"e{i}", i));
            lines.Add(Stop($"e{i}", 10 + i));
        }

        var subagents = Answered(Compose("s1", null, [new AtWorkRecord("s1", "working")], _ => Read(lines))).Subagents;

        Assert.Equal(["e5", "e4", "e3"], subagents.Children.Select(child => child.Key));
        Assert.Equal(3, subagents.Folded);
        Assert.Null(subagents.More);
    }

    [Fact]
    public void Not_known_children_follow_the_open_and_precede_the_lost_and_those_whose_agent_ended_none_counted_working()
    {
        string[] lines = [Start(), Child("open", 1), Child("old", 2, proc: "b000000000000002")];
        var answer = Answered(Compose("s1", null, [new AtWorkRecord("s1", "working")], _ => LineageReading.Of(ChildBindings.Fold(
            [.. lines, ChildBindings.StartLine("b000000000000002", "1 1", T0)!, ChildBindings.StartLine("c000000000000003", Issuer, T0)!,
                Child("gone", 3, proc: "c000000000000003")],
            null, (proc, _) => proc switch { P1 => TokenReading.Watched, "c000000000000003" => TokenReading.Gone, _ => null }))));

        Assert.Equal(
            [("open", ChildBindings.Working), ("old", ChildBindings.Uncertain), ("gone", ChildBindings.AgentEnded)],
            answer.Subagents.Children.Select(child => (child.Key, child.State)));
        Assert.Equal(1, answer.Subagents.Working);
    }

    [Fact]
    public void Ties_fall_to_the_id_and_a_parent_cycle_draws_each_once()
    {
        string[] lines = [Start(), Child("b", 1, parent: "a"), Child("a", 1, parent: "b")];

        var first = Answered(Compose("s1", null, [new AtWorkRecord("s1", "working")], _ => Read(lines))).Subagents;
        var again = Answered(Compose("s1", null, [new AtWorkRecord("s1", "working")], _ => Read(lines))).Subagents;

        Assert.Equal(2, first.Children.Count);
        Assert.Equal(first.Children.Select(child => child.Key), again.Children.Select(child => child.Key));
        Assert.Equal(["a", "b"], first.Children.Select(child => child.Key).Order());
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

    /// <summary>A chain of <paramref name="length"/> ended ancestors under the root, its deepest child working.</summary>
    private static void AddChain(List<string> lines, string name, int length, int second)
    {
        for (var i = 0; i < length; i++)
        {
            lines.Add(Child($"{name}{i:00}", second + i, parent: i == 0 ? "root" : $"{name}{i - 1:00}", depth: i + 1));
            if (i < length - 1) lines.Add(Stop($"{name}{i:00}", second + 100 + i));
        }
    }
}
