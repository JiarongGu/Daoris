using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// COWORKVIEW1b-i's composition (the design §R11.2 points 1 and 3): each part's wait as the strip places it, how it joined, where it
/// runs, and the ended parts that wait on nothing folded past the newest three, all from the groups rows, the quests and the records.
/// </summary>
public sealed class AtWorkRoutingTests
{
    private static AtWorkEntryPart Part(string session, string shown, string began = "opened", string? quest = null, string? after = null) =>
        new(session, quest, shown, began) { After = after };

    private static AtWorkQuest Quest(string id, string status = "Open") => new(id, status);

    private static AtWorkAnswer Routed(
        string session, IReadOnlyList<AtWorkEntryPart> parts, IReadOnlyList<AtWorkRecord> records, IReadOnlyList<AtWorkQuest>? quests = null,
        IReadOnlyDictionary<string, AtWorkRow>? rows = null, SliceHealth? health = null) =>
        AtWorkTests.Answered(RoutedHeld(session, parts, records, quests, rows, health));

    private static AtWorkHeld RoutedHeld(
        string session, IReadOnlyList<AtWorkEntryPart> parts, IReadOnlyList<AtWorkRecord> records, IReadOnlyList<AtWorkQuest>? quests = null,
        IReadOnlyDictionary<string, AtWorkRow>? rows = null, SliceHealth? health = null)
    {
        quests ??= [];
        var routing = new AtWorkRouting(
            parts, id => rows?.GetValueOrDefault(id), id => quests.FirstOrDefault(quest => quest.Id == id),
            id => quests.Count(quest => quest.PublishedBy == id), health ?? AtWorkTests.Judged);
        return AtWork.Compose(
            session, parts.Select(part => part.Session).ToList(), id => records.FirstOrDefault(record => record.Id == id), _ => LineageReading.None,
            AtWorkTests.Judged, AtWorkTests.Judged, routing);
    }

    private static AtWorkPart PartOf(AtWorkAnswer answer, string key) => answer.Parts.Single(part => part.Key == key);

    [Theory]
    [InlineData("awaiting-reply", true)]
    [InlineData("awaiting-helper", true)]
    [InlineData("completed", false)]
    [InlineData("working", false)]
    [InlineData("parked", false)]
    [InlineData("answered", false)]
    [InlineData("going-on", false)]
    [InlineData("limit-waits", false)]
    [InlineData("stood-down", false)]
    public void Each_word_the_strip_shows_a_part_with_is_a_wait_or_it_is_not(string shown, bool waits)
    {
        AtWorkEntryPart[] parts = [Part("lead", shown, quest: "q1"), Part("lane", "working", quest: "q2")];
        AtWorkRecord[] records = [new("lead", "completed"), new("lane", "working")];
        var rows = new Dictionary<string, AtWorkRow> { ["lead"] = new("q2", "core") };

        var wait = PartOf(Routed("lead", parts, records, [Quest("q2")], rows), "lead").Waits;

        Assert.Equal(waits, wait is not null);
        if (shown == "awaiting-helper") Assert.True(wait!.Helper);
        if (shown == "awaiting-reply") Assert.Equal(("q2", "core"), (wait!.Quest, wait.Of));
    }

    [Fact]
    public void A_wait_on_a_quest_carries_that_quest_s_lanes_and_a_row_naming_none_is_no_wait()
    {
        AtWorkEntryPart[] parts = [Part("lead", "awaiting-reply", quest: "q1")];
        AtWorkRecord[] records = [new("lead", "completed")];

        var waiting = Routed("lead", parts, records, [new AtWorkQuest("q2", "Open") { Lanes = "core+docs" }], new Dictionary<string, AtWorkRow> { ["lead"] = new("q2", null) });
        var none = Routed("lead", parts, records, [], new Dictionary<string, AtWorkRow> { ["lead"] = new(null, null) });

        Assert.Equal(["core", "docs"], PartOf(waiting, "lead").Waits!.Lanes);
        Assert.Null(PartOf(none, "lead").Waits);
    }

    [Fact]
    public void A_resumed_record_is_beside_the_record_that_waited_which_no_longer_waits()
    {
        // The strip places the resumed record, the quest's newest run; the record that waited reads as the ended part it is.
        AtWorkEntryPart[] parts = [Part("first", "completed", quest: "q1"), Part("resumed", "working", "resumed", "q1", "first")];
        AtWorkRecord[] records = [new("first", "completed"), new("resumed", "working")];

        var answer = Routed("resumed", parts, records);

        Assert.Equal(["resumed", "first"], answer.Parts.Select(part => part.Key));
        Assert.All(answer.Parts, part => Assert.Null(part.Waits));
        Assert.Equal(("resumed", 1), (PartOf(answer, "resumed").Joined!.Began, PartOf(answer, "resumed").Joined!.After));
        Assert.Null(answer.Earlier);
    }

    [Fact]
    public void A_stood_down_newest_record_leaves_the_wait_the_strip_draws_on_the_record_before_it()
    {
        AtWorkEntryPart[] parts = [Part("lead", "awaiting-reply", quest: "q1"), Part("replacement", "stood-down", "resumed", "q1", "lead")];
        AtWorkRecord[] records = [new("lead", "completed"), new("replacement", "stood-down")];
        var rows = new Dictionary<string, AtWorkRow> { ["lead"] = new("q2", "core") };

        var answer = Routed("replacement", parts, records, [Quest("q2")], rows);

        Assert.Equal(("q2", null), (PartOf(answer, "lead").Waits!.Quest, PartOf(answer, "replacement").Waits?.Quest));
    }

    [Fact]
    public void A_chat_waits_on_the_question_of_a_quest_it_took_while_that_question_stands()
    {
        AtWorkEntryPart[] parts = [Part("chat", "completed")];
        AtWorkRecord[] records = [new("chat", "completed") { Kind = ChildBindings.Chat, TookQuest = "t1" }];
        AtWorkQuest taken = new("t1", "Taken") { Awaits = "q9" };

        var standing = Routed("chat", parts, records, [taken, new AtWorkQuest("q9", "Open") { To = "core" }]);
        var answered = Routed("chat", parts, records, [taken, new AtWorkQuest("q9", "Done")]);
        var declined = Routed("chat", parts, records, [taken, new AtWorkQuest("q9", "Declined")]);
        var elsewhere = Routed("chat", parts, records, [taken with { Holder = "laptop", Here = "desk" }, new AtWorkQuest("q9", "Open")]);
        var here = Routed("chat", parts, records, [taken with { Holder = "desk", Here = "desk" }, new AtWorkQuest("q9", "Open")]);
        var closedTake = Routed("chat", parts, records, [taken with { Status = "Done" }, new AtWorkQuest("q9", "Open")]);
        // A done held for the person's yes or its evidence stays on the open list, and the planner keeps waiting on it (D144 §6).
        var held = Routed("chat", parts, records, [taken, new AtWorkQuest("q9", "Done") { Held = true }]);

        Assert.Equal(("q9", "core"), (PartOf(standing, "chat").Waits!.Quest, PartOf(standing, "chat").Waits!.Of));
        Assert.Equal("q9", PartOf(held, "chat").Waits!.Quest);
        Assert.Equal("q9", PartOf(here, "chat").Waits!.Quest);
        Assert.All(new[] { answered, declined, elsewhere, closedTake }, each => Assert.Null(PartOf(each, "chat").Waits));
        Assert.True(PartOf(standing, "chat").Joined!.Took);
        Assert.Equal("t1", PartOf(standing, "chat").Joined!.Quest);
    }

    [Fact]
    public void A_part_says_how_it_joined_from_its_beginning_its_quest_and_the_part_that_published_it()
    {
        AtWorkEntryPart[] parts =
        [
            Part("lead", "working", quest: "q0"),
            Part("lane", "working", quest: "q1"),
            Part("next", "working", "carried-on", "q2", "lane"),
            Part("sibling", "working", "asked", "q3") with { AskedBy = "core" },
            Part("ask", "working", quest: "q4"),
        ];
        AtWorkRecord[] records = [.. parts.Select(part => new AtWorkRecord(part.Session, "working") { Repository = "driver" })];
        AtWorkQuest[] quests =
        [
            new("q0", "Taken"),
            new("q1", "Taken") { Lanes = "core", PublishedBy = "lead" },
            new("q2", "Taken") { Parent = "q1" },
            new("q3", "Taken") { From = "ask #a7" },
            new("q4", "Taken") { From = "ask #a7" },
        ];

        var answer = Routed("lead", parts, records, quests);

        var lane = PartOf(answer, "lane").Joined!;
        Assert.Equal((1, "q1", "driver"), (lane.ByPart, lane.Quest, lane.Of));
        Assert.Equal(["core"], lane.Lanes);
        Assert.Equal(("q1", 2), (PartOf(answer, "next").Joined!.Parent, PartOf(answer, "next").Joined!.After));
        Assert.Equal(("asked", "core", "a7"), (PartOf(answer, "sibling").Joined!.Began, PartOf(answer, "sibling").Joined!.AskedBy, PartOf(answer, "sibling").Joined!.Ask));
        Assert.Equal("a7", PartOf(answer, "ask").Joined!.Ask);
        Assert.Null(PartOf(answer, "lead").Joined!.ByPart);
        Assert.Equal([1, 2, 3, 4, 5], parts.Select(part => PartOf(answer, part.Session).Number));
    }

    [Fact]
    public void A_part_in_a_tree_no_earlier_part_worked_in_is_alongside_and_one_sent_into_a_tree_already_worked_in_is_not()
    {
        AtWorkEntryPart[] parts = [Part("lead", "working"), Part("beside", "working"), Part("after", "working"), Part("done", "completed")];
        AtWorkRecord[] records =
        [
            new("lead", "working") { Tree = "X:/trees/a" },
            new("beside", "working") { Tree = "X:/trees/b" },
            new("after", "working") { Tree = "x:\\trees\\A\\" },
            new("done", "completed") { Tree = "X:/trees/c" },
        ];

        var answer = Routed("lead", parts, records);

        Assert.Equal([null, true, null, null], new[] { "lead", "beside", "after", "done" }.Select(key => PartOf(answer, key).Alongside));
    }

    [Fact]
    public void A_waiting_part_in_a_tree_of_its_own_is_alongside_though_its_record_is_ended()
    {
        AtWorkEntryPart[] parts = [Part("lead", "working"), Part("lane", "awaiting-helper")];
        AtWorkRecord[] records = [new("lead", "working") { Tree = "X:/trees/a" }, new("lane", "completed") { Tree = "X:/trees/b" }];

        Assert.True(PartOf(Routed("lead", parts, records), "lane").Alongside);
    }

    [Fact]
    public void Ended_parts_that_wait_on_nothing_fold_past_the_newest_three_and_a_waiting_or_attended_part_never_folds()
    {
        var ids = Enumerable.Range(1, 8).Select(i => $"p{i}").ToList();
        var parts = ids.Select(id => Part(id, id == "p2" ? "awaiting-helper" : id == "p8" ? "working" : "completed")).ToList();
        var records = ids.Select(id => new AtWorkRecord(id, id == "p8" ? "working" : "completed")).ToList();

        // p1 attended and ended; p2 waits; p3..p7 ended and wait on nothing: p3, p4 fold, p5..p7 stay; p8 works.
        var answer = Routed("p1", parts, records);

        Assert.Equal(["p1", "p2", "p5", "p6", "p7", "p8"], answer.Parts.Select(part => part.Key));
        Assert.Equal(2, answer.Earlier);
    }

    [Fact]
    public void Inside_the_forty_part_limit_the_attended_and_the_waiting_parts_come_first_and_the_overflow_is_counted_apart_from_the_fold()
    {
        // 52 parts: p1 attended and ended, p2..p45 working, p46 waits on its helper, p47..p51 ended, p52 ended and waits.
        var ids = Enumerable.Range(1, 52).Select(i => $"p{i}").ToList();
        bool Ended(string id) => id == "p1" || int.Parse(id[1..]) >= 47;
        var parts = ids.Select(id => Part(id, id is "p46" or "p52" ? "awaiting-helper" : Ended(id) ? "completed" : "working")).ToList();
        var records = ids.Select(id => new AtWorkRecord(id, Ended(id) ? "completed" : "working")).ToList();

        var answer = Routed("p1", parts, records);

        // p47 and p48 fold (the newest three of the ended parts that wait on nothing stay); fifty remain for forty places.
        Assert.Equal(2, answer.Earlier);
        Assert.Equal(AtWork.Bound, answer.Parts.Count);
        Assert.Equal("p1", answer.Parts[0].Key);
        Assert.Contains(answer.Parts, part => part.Key == "p46" && part.Waits is not null);
        Assert.Contains(answer.Parts, part => part.Key == "p52" && part.Waits is not null);
        Assert.Equal(answer.Parts.Skip(1).Select(part => part.Number), answer.Parts.Skip(1).Select(part => part.Number).Order());
        Assert.Equal(10, answer.MoreParts!.Values.Sum());
        Assert.DoesNotContain(answer.Parts, part => part.Key is "p47" or "p48" or "p49");
    }

    [Fact]
    public void Three_ended_parts_fold_nothing_and_no_routing_folds_no_part()
    {
        var ids = Enumerable.Range(1, 6).Select(i => $"p{i}").ToList();
        var parts = ids.Select(id => Part(id, "completed")).ToList();
        var records = ids.Select(id => new AtWorkRecord(id, "completed")).ToList();

        var few = Routed("p1", parts.Take(4).ToList(), records.Take(4).ToList());
        var unrouted = AtWorkTests.Answered(AtWorkTests.Compose("p1", ids, records, _ => LineageReading.None));

        Assert.Equal((4, null), (few.Parts.Count, few.Earlier));
        Assert.Equal((6, null), (unrouted.Parts.Count, unrouted.Earlier));
        Assert.All(unrouted.Parts, part => Assert.Null(part.Waits));
    }

    [Fact]
    public void The_quests_a_session_s_parts_published_are_counted_beside_the_parts()
    {
        AtWorkEntryPart[] parts = [Part("lead", "completed"), Part("later", "working")];
        AtWorkRecord[] records = [new("lead", "completed"), new("later", "working")];
        AtWorkQuest[] quests = [new("q1", "Open") { PublishedBy = "lead" }, new("q2", "Taken") { PublishedBy = "later" }, new("q3", "Open") { PublishedBy = "other" }];

        Assert.Equal(2, Routed("lead", parts, records, quests).Published);
        Assert.Null(Routed("lead", parts, records).Published);
    }

    [Fact]
    public void The_quests_slice_not_judged_holds_the_answer_pending_and_failing_fails_it_with_its_words()
    {
        AtWorkEntryPart[] parts = [Part("lead", "working")];
        AtWorkRecord[] records = [new("lead", "working")];

        var pending = RoutedHeld("lead", parts, records, health: new SliceHealth(false));
        var failed = RoutedHeld("lead", parts, records, health: new SliceHealth(false, "the quests could not be read"));
        var stale = RoutedHeld("lead", parts, records, health: new SliceHealth(true, "the quests could not be read"));

        Assert.Equal(AtWork.WaitsSlices, pending.Waits);
        Assert.Equal("the quests could not be read", failed.Failed!.Why);
        Assert.Equal("the quests could not be read", stale.Stale!.Why);
        Assert.NotNull(stale.Answer);
    }
}
