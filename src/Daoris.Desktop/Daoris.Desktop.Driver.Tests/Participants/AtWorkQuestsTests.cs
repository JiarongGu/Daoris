using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// COWORKVIEW1b-ii's composition (the design §R11.3 row 4, §3.1): the quests a part published that opened no part of the entry, each
/// chain's steps under it, who took each and what asked the attended part's own quest, read by <see cref="QuestUnits.Read"/>.
/// </summary>
public sealed partial class AtWorkTests
{
    private static QuestView Q(string id, string status = "Open", string to = "core", string? by = "lead", string? parent = null, string from = "game") =>
        new(id, from, to, $"Title of {id}", "", status) { PublishedBy = by, Parent = parent };

    private static SessionRecord S(string id, string state = "working", string? quest = null, string? took = null, int second = 0, string kind = "driven") =>
        new(id, "core", state) { Quest = quest, TookQuest = took, Kind = kind, Adapter = "claude-code", Created = T0.AddSeconds(second) };

    private static AtWorkHeld AskedHeld(
        string session, IReadOnlyList<AtWorkEntryPart> parts, IReadOnlyList<AtWorkRecord> records, IReadOnlyList<QuestView> quests,
        IReadOnlyList<SessionRecord> sessions, IReadOnlyDictionary<string, AtWorkRow>? rows = null)
    {
        var routing = new AtWorkRouting(parts, _ => null, _ => null, id => quests.Count(quest => quest.PublishedBy == id), Judged)
        {
            Asking = new AtWorkAsking(quests, sessions, id => rows?.GetValueOrDefault(id)),
        };
        return AtWork.Compose(
            session, parts.Select(part => part.Session).ToList(), id => records.FirstOrDefault(record => record.Id == id), _ => LineageReading.None,
            Judged, Judged, routing);
    }

    private static AtWorkAnswer Asked(
        string session, IReadOnlyList<AtWorkEntryPart> parts, IReadOnlyList<AtWorkRecord> records, IReadOnlyList<QuestView> quests,
        IReadOnlyList<SessionRecord> sessions, IReadOnlyDictionary<string, AtWorkRow>? rows = null) =>
        Answered(AskedHeld(session, parts, records, quests, sessions, rows));

    private static readonly AtWorkEntryPart[] LeadOnly = [Part("lead", "completed")];
    private static readonly AtWorkRecord[] LeadRecord = [new("lead", "completed") { Kind = ChildBindings.Chat }];

    private static AtWorkAskedQuest Row(AtWorkAnswer answer, string id) => Assert.Single(answer.Quests!, quest => quest.Id == id);

    [Fact]
    public void A_quest_a_part_published_that_nobody_took_is_untaken_with_its_name_lanes_and_repository()
    {
        var answer = Asked("lead", LeadOnly, LeadRecord, [Q("q1", to: "docs") with { Lanes = ["core", "docs"], Short = "The short name" }], []);

        var row = Row(answer, "q1");
        Assert.Equal(("The short name", "Open", "docs"), (row.Title, row.Status, row.Of));
        Assert.Equal(["core", "docs"], row.Lanes);
        Assert.True(row.Taker!.Untaken);
        Assert.Null(answer.AskedBy);
    }

    [Fact]
    public void A_quest_a_part_did_not_publish_is_not_drawn()
    {
        var answer = Asked("lead", LeadOnly, LeadRecord, [Q("q1", by: "somebody"), Q("q2", by: null)], []);

        Assert.Null(answer.Quests);
    }

    [Fact]
    public void A_chain_step_stands_under_its_parent_and_is_never_asked_by_you()
    {
        // The step has no publisher: the service published it when its parent closed.
        QuestView[] quests = [Q("q2", by: null, parent: "q1", from: "game"), Q("q1", "Done"), Q("q9", by: "lead")];
        SessionRecord[] sessions = [S("taker", "completed", quest: "q1")];

        var answer = Asked("lead", LeadOnly, LeadRecord, quests, sessions);

        Assert.Equal(["q1", "q2", "q9"], answer.Quests!.Select(quest => quest.Id));
        Assert.Equal(("q1", null), (Row(answer, "q2").Step, Row(answer, "q1").Step));
        Assert.Equal("taker", Row(answer, "q1").Taker!.Session);
    }

    [Fact]
    public void A_step_whose_parent_opened_a_part_of_the_entry_is_still_drawn_with_its_step()
    {
        AtWorkEntryPart[] parts = [Part("lead", "completed"), Part("lane", "working", "published", quest: "q1")];
        AtWorkRecord[] records = [new("lead", "completed"), new("lane", "working")];
        QuestView[] quests = [Q("q1", "Taken"), Q("q2", by: null, parent: "q1")];

        var answer = Asked("lead", parts, records, quests, [S("lane", quest: "q1")]);

        Assert.Equal(["q2"], answer.Quests!.Select(quest => quest.Id));
        Assert.Equal("q1", answer.Quests![0].Step);
    }

    [Fact]
    public void The_attended_part_s_quest_is_asked_by_the_session_the_step_the_ask_or_the_person()
    {
        AtWorkEntryPart[] parts = [Part("lead", "working", quest: "q1")];
        AtWorkRecord[] records = [new("lead", "working")];

        By(Q("q1", by: "other"), out var session);
        By(Q("q1", by: null, parent: "q0"), out var step);
        By(Q("q1", by: null, from: "ask #a7"), out var ask);
        By(Q("q1", by: null, from: "game"), out var person);

        Assert.Equal("other", session.Session);
        Assert.Equal("q0", step.Step);
        Assert.Equal("a7", ask.Ask);
        Assert.True(person.Person);

        void By(QuestView quest, out AtWorkBy by)
        {
            var asker = Asked("lead", parts, records, [quest], [S("lead", quest: "q1")]).AskedBy!;
            Assert.Equal("q1", asker.Quest);
            by = asker.By;
        }
    }

    [Fact]
    public void A_chat_that_took_its_quest_through_its_connector_has_it_as_its_asker_and_a_quest_unknown_here_has_none()
    {
        AtWorkEntryPart[] parts = [Part("chat", "completed")];
        AtWorkRecord[] records = [new("chat", "completed") { Kind = ChildBindings.Chat, TookQuest = "t1" }];

        var known = Asked("chat", parts, records, [Q("t1", "Taken", by: "other")], [S("chat", "completed", took: "t1", kind: "chat")]);
        var unknown = Asked("chat", parts, records, [], [S("chat", "completed", took: "t1", kind: "chat")]);

        Assert.Equal(("t1", "other"), (known.AskedBy!.Quest, known.AskedBy.By.Session));
        Assert.Null(unknown.AskedBy);
    }

    [Fact]
    public void A_chat_s_take_names_the_conversation_and_another_chat_taking_the_publisher_s_lane_quest_leaves_it_where_it_stood()
    {
        // The publisher's entry holds one part: the other chat took the quest through its connector, which writes `took` and no entry.
        QuestView[] quests = [Q("q1", "Taken") with { Lanes = ["core"] }];
        SessionRecord[] sessions = [S("lead", "completed", kind: "chat"), S("other-chat", "working", took: "q1", kind: "chat")];

        var answer = Asked("lead", LeadOnly, LeadRecord, quests, sessions);

        Assert.Equal("other-chat", Row(answer, "q1").Taker!.Conversation);
        Assert.Null(Row(answer, "q1").Taker!.Session);
    }

    [Fact]
    public void A_lane_quest_moves_to_this_session_only_when_the_entry_holds_its_executing_part_and_is_never_in_both()
    {
        QuestView[] quests = [Q("q1", "Taken"), Q("q2", "Taken")];
        SessionRecord[] sessions = [S("lane", quest: "q1"), S("outside", quest: "q2")];
        AtWorkEntryPart[] parts = [Part("lead", "completed"), Part("lane", "working", "published", "q1")];
        AtWorkRecord[] records = [new("lead", "completed"), new("lane", "working") { Adapter = "claude-code" }];

        var answer = Asked("lead", parts, records, quests, sessions);

        // q1's executing part is in the entry; q2's session is not, so q2 stays and names its taker.
        Assert.Equal(["q2"], answer.Quests!.Select(quest => quest.Id));
        Assert.Equal(("outside", "working", "claude-code"), (Row(answer, "q2").Taker!.Session, Row(answer, "q2").Taker!.State, Row(answer, "q2").Taker!.Agent));
        Assert.Contains(answer.Parts, part => part.Key == "lane" && part.Joined!.Quest == "q1");
        Assert.DoesNotContain(answer.Quests, quest => answer.Parts.Any(part => part.Joined?.Quest == quest.Id));
    }

    [Fact]
    public void A_part_that_executes_a_quest_by_its_record_alone_also_keeps_it_out_of_the_quests()
    {
        // The strip's entry names no quest on this part yet; its record does.
        AtWorkEntryPart[] parts = [Part("lead", "completed"), Part("lane", "working")];
        AtWorkRecord[] records = [new("lead", "completed"), new("lane", "working")];

        var answer = Asked("lead", parts, records, [Q("q1", "Taken")], [S("lane", quest: "q1")]);

        Assert.Null(answer.Quests);
    }

    [Theory]
    [InlineData("laptop", "desk", true, false)]
    [InlineData("desk", "desk", false, true)]
    [InlineData(null, "desk", false, true)]
    [InlineData("laptop", null, false, true)]
    public void A_taken_quest_with_no_session_here_is_on_another_machine_only_where_its_holder_is_not_this_one(
        string? holder, string? here, bool elsewhere, bool unrecorded)
    {
        var answer = Asked("lead", LeadOnly, LeadRecord, [Q("q1", "Taken") with { Holder = holder, Here = here }], []);

        var taker = Row(answer, "q1").Taker!;
        Assert.Equal((elsewhere, unrecorded), (taker.Elsewhere == true, taker.Unrecorded == true));
        Assert.Null(taker.Session);
        Assert.Null(taker.Untaken);
    }

    [Fact]
    public void A_session_here_wins_over_the_holder_and_a_declined_quest_nobody_took_names_no_taker()
    {
        QuestView[] quests = [Q("q1", "Taken") with { Holder = "laptop", Here = "desk" }, Q("q2", "Declined")];

        var answer = Asked("lead", LeadOnly, LeadRecord, quests, [S("taker", quest: "q1")]);

        Assert.Equal("taker", Row(answer, "q1").Taker!.Session);
        Assert.Null(Row(answer, "q2").Taker);
        Assert.Equal("Declined", Row(answer, "q2").Status);
    }

    [Fact]
    public void The_newest_executor_is_the_taker_of_a_quest_resumed_after_its_answer()
    {
        SessionRecord[] sessions = [S("first", "completed", quest: "q1", second: 0), S("resumed", "working", quest: "q1", second: 60)];

        var answer = Asked("lead", LeadOnly, LeadRecord, [Q("q1", "Taken")], sessions);

        Assert.Equal("resumed", Row(answer, "q1").Taker!.Session);
    }

    [Fact]
    public void A_quest_s_own_wait_is_its_taker_s_row_for_a_session_and_the_question_that_stands_for_a_conversation()
    {
        // a1 is the question another repository's session was asked; nothing of this entry published it, so it is not a row.
        QuestView[] quests =
        [
            Q("q1", "Taken"), Q("q2", "Taken") with { Awaits = "a1" }, Q("q3", "Taken") with { Awaits = "a2" },
            Q("a1", "Open", to: "docs", by: "somebody") with { Lanes = ["docs"] }, Q("a2", "Done", by: "somebody"),
        ];
        SessionRecord[] sessions = [S("s1", quest: "q1"), S("c1", "completed", took: "q2", kind: "chat"), S("s3", quest: "q3")];
        var rows = new Dictionary<string, AtWorkRow> { ["s1"] = new("a1", "docs") };

        var answer = Asked("lead", LeadOnly, LeadRecord, quests, sessions, rows);

        Assert.Equal(("a1", "docs"), (Row(answer, "q1").Waits!.Quest, Row(answer, "q1").Waits!.Of));
        Assert.Equal(["docs"], Row(answer, "q1").Waits!.Lanes);
        Assert.Equal("a1", Row(answer, "q2").Waits!.Quest);
        Assert.Null(Row(answer, "q3").Waits);
    }

    [Fact]
    public void A_cycle_among_parents_is_drawn_once_each_and_the_rows_stop_at_the_bound_counting_the_rest()
    {
        QuestView[] cycle = [Q("c1", parent: "c2"), Q("c2", by: null, parent: "c1")];
        var many = Enumerable.Range(0, AtWork.Bound + 5).Select(n => Q($"m{n:D3}")).ToArray();

        var cyclic = Asked("lead", LeadOnly, LeadRecord, cycle, []);
        var bounded = Asked("lead", LeadOnly, LeadRecord, many, []);

        Assert.Equal(["c1", "c2"], cyclic.Quests!.Select(quest => quest.Id).Order());
        Assert.Equal((AtWork.Bound, 5), (bounded.Quests!.Count, bounded.MoreQuests));
    }

    [Fact]
    public void Open_and_taken_quests_come_before_those_that_ended_and_a_routing_with_no_asking_reads_no_quests()
    {
        var answer = Asked("lead", LeadOnly, LeadRecord, [Q("old", "Done"), Q("new", "Open")], [S("t", "completed", quest: "old")]);
        var unasked = RoutedHeld("lead", LeadOnly, LeadRecord, [Quest("q1")]);

        Assert.Equal(["new", "old"], answer.Quests!.Select(quest => quest.Id));
        Assert.Null(Answered(unasked).Quests);
        Assert.Null(Answered(unasked).AskedBy);
    }
}
