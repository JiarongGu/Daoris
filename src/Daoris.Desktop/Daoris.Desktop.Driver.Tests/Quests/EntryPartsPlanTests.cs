using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// SESSONE1d (D167 point 4 as amended; the co-working design §1.5 and §3): a new quest of a session's entry goes into the tree
/// its newest part on this machine held when nothing live holds it (D51), and otherwise runs alongside in a tree of its own as a
/// further part; nothing waits. The evidence of ask #c98a7e replayed with neutral repositories: an app (`app`) and its database
/// repository (`db`).
/// </summary>
public sealed class EntryPartsPlanTests
{
    private const string AppEntry = "ask:c98a7e/app";
    private const string DbEntry = "ask:c98a7e/db";
    private static readonly DateTimeOffset At = DateTimeOffset.Parse("2026-10-14T06:30:00Z");

    private static readonly RepoView[] Repos = [new("app", true, "D:/fam/app"), new("db", true, "D:/fam/db")];

    private static DriverConfig Config(int cap = 4) =>
        DriverConfig.Empty with { Drivable = ["app", "db"], Trees = ["app", "db"], Cap = cap };

    /// <summary>B's first part: `283ef343` took `3068f747`, asked `711d2524` of the app at 06:58, and ended completed.</summary>
    private static readonly PriorSession B1 = new("283ef343", "D:/trees/db/s-283e", "completed", "asked #711d2524 and waits.", "db")
    {
        Entry = DbEntry, Quest = "3068f747", Created = At,
    };

    /// <summary>A's second part, the retry of `28b052ac`, at work in its own tree until 07:31.</summary>
    private static readonly PriorSession A2 = new("62c005bc", "D:/trees/app/s-62c0", "working", Repository: "app")
    {
        Entry = AppEntry, Quest = "28b052ac", Created = At.AddMinutes(10),
    };

    private static QuestView Quest(string id, string to, string status = "Open") => new(id, "ask #c98a7e", to, $"Ask {id}", "Why.", status);

    /// <summary>B's request of A, `711d2524`, published by `283ef343` at 06:58, while `62c005bc` still works in A's tree.</summary>
    private static Snapshot At0658() =>
        new(
            [Quest("28b052ac", "app", "Taken"), Quest("3068f747", "db", "Taken") with { Awaits = "711d2524" },
                new QuestView("711d2524", "db", "app", "Ask 711d2524", "Why.", "Open") { PublishedBy = "283ef343" }],
            Repos,
            [new SessionView("62c005bc", "app", "working") { Tree = A2.Tree, Quest = "28b052ac" }])
        {
            LastRun = new Dictionary<string, PriorSession>(StringComparer.OrdinalIgnoreCase) { ["28b052ac"] = A2, ["3068f747"] = B1 },
            Entries = new Dictionary<string, string>(StringComparer.Ordinal) { ["283ef343"] = DbEntry, ["62c005bc"] = AppEntry },
        };

    [Fact]
    public void A_request_whose_entrys_tree_a_live_part_holds_starts_alongside_in_a_tree_of_its_own()
    {
        var plan = Planner.Plan(At0658(), Config());

        var started = Assert.Single(plan, c => c.Quest.Id == "711d2524");
        Assert.Equal(StartVerdict.Start, started.Verdict);
        Assert.Null(started.Resumes);
        var entry = Assert.IsType<EntryParts>(started.Entry);
        Assert.Equal(AppEntry, entry.Key);
        Assert.Null(entry.Into);
        Assert.Equal("62c005bc", entry.HeldBy);
        Assert.Equal(["62c005bc"], entry.Parts.Select(part => part.Session));
        Assert.Contains("alongside", started.Reason);
        Assert.Contains("`62c005bc`", started.Reason);
    }

    /// <summary>A's request of B, `01ba2c71`, published by `8eba4af2` at 07:59: `283ef343` had completed, so B's tree was free.</summary>
    private static Snapshot At0759()
    {
        var a3 = new PriorSession("8eba4af2", "D:/trees/app/s-8eba", "working", Repository: "app")
        {
            Entry = AppEntry, Quest = "711d2524", Created = At.AddMinutes(28),
        };
        return new Snapshot(
            [Quest("3068f747", "db", "Taken") with { Awaits = "711d2524" },
                new QuestView("711d2524", "db", "app", "Ask 711d2524", "Why.", "Taken") { PublishedBy = "283ef343", Awaits = "01ba2c71" },
                new QuestView("01ba2c71", "app", "db", "Ask 01ba2c71", "Why.", "Open") { PublishedBy = "8eba4af2" }],
            Repos,
            [new SessionView("8eba4af2", "app", "working") { Tree = a3.Tree, Quest = "711d2524" }])
        {
            LastRun = new Dictionary<string, PriorSession>(StringComparer.OrdinalIgnoreCase)
            {
                ["28b052ac"] = A2 with { State = "completed" }, ["3068f747"] = B1, ["711d2524"] = a3,
            },
            Entries = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["283ef343"] = DbEntry, ["62c005bc"] = AppEntry, ["8eba4af2"] = AppEntry,
            },
        };
    }

    [Fact]
    public void A_request_whose_entrys_tree_is_free_goes_into_it()
    {
        var plan = Planner.Plan(At0759(), Config());

        var started = Assert.Single(plan, c => c.Quest.Id == "01ba2c71");
        Assert.Equal(StartVerdict.Start, started.Verdict);
        // A new part, not a carry-on: it takes its own quest, in the tree of the part before it.
        Assert.Null(started.Resumes);
        var entry = Assert.IsType<EntryParts>(started.Entry);
        Assert.Equal(DbEntry, entry.Key);
        Assert.Equal("283ef343", entry.Into?.Session);
        Assert.Equal("D:/trees/db/s-283e", entry.Into?.Tree);
        Assert.Null(entry.HeldBy);
        Assert.Contains("`283ef343`", started.Reason);
        // B's own first quest still waits on its question, in the same tree, once it is answered.
        Assert.Equal(StartVerdict.Waiting, Assert.Single(plan, c => c.Quest.Id == "3068f747").Verdict);
    }

    /// <summary>
    /// 🔴 HELPERWAIT1 (Codex's #5 on COWORKUX1): a part that ended waiting on its own helper still holds its tree, as a park does, though it
    /// is completed and in no live list: it goes on there with the answer. A further part of its entry starts alongside, in a tree of its own,
    /// and never into that one while its quest is taken; one whose quest is no longer taken holds nothing.
    /// </summary>
    [Theory]
    [InlineData("Taken", true)]
    [InlineData("Done", false)]
    public void A_request_whose_entrys_newest_part_waits_on_its_own_helper_starts_alongside(string status, bool held)
    {
        var waiting = B1 with { NoteParts = [NoteCodes.EndedAwaitsHelper.Part("It asked a helper, and waits on its answer.")], Note = "It asked a helper." };
        var snapshot = At0759() with
        {
            Quests =
            [
                Quest("3068f747", "db", status),
                new QuestView("01ba2c71", "app", "db", "Ask 01ba2c71", "Why.", "Open") { PublishedBy = "8eba4af2" },
            ],
            Active = [],
            LastRun = new Dictionary<string, PriorSession>(StringComparer.OrdinalIgnoreCase) { ["3068f747"] = waiting },
        };

        var started = Assert.Single(Planner.Plan(snapshot, Config()), c => c.Quest.Id == "01ba2c71");

        Assert.Equal(StartVerdict.Start, started.Verdict);
        var entry = Assert.IsType<EntryParts>(started.Entry);
        Assert.Equal(held ? null : "283ef343", entry.Into?.Session);
        Assert.Equal(held ? "283ef343" : null, entry.HeldBy);
    }

    /// <summary>PAR1 stands: a request of another line of work in the same repository is its own entry, side by side.</summary>
    [Fact]
    public void A_request_of_another_entry_in_the_same_repository_starts_in_a_tree_of_its_own()
    {
        var snapshot = At0759() with
        {
            Quests = [new QuestView("5d1e0a77", "app", "db", "Another", "Why.", "Open") { PublishedBy = "9f0c11aa" }],
        };
        var entries = new Dictionary<string, string>(snapshot.Entries, StringComparer.Ordinal) { ["9f0c11aa"] = "ask:841c38/app" };

        var started = Assert.Single(Planner.Plan(snapshot with { Entries = entries }, Config()));

        Assert.Equal(StartVerdict.Start, started.Verdict);
        Assert.Null(started.Entry);
    }

    /// <summary>One tree takes one session (D51): two requests of one entry at one look, the first into its tree, the second alongside.</summary>
    [Fact]
    public void Two_requests_of_one_entry_at_one_look_take_its_tree_once()
    {
        var snapshot = At0759();
        var second = new QuestView("7c2b9e10", "app", "db", "Ask 7c2b9e10", "Why.", "Open") { PublishedBy = "8eba4af2" };

        var plan = Planner.Plan(snapshot with { Quests = [.. snapshot.Quests, second] }, Config());

        Assert.Equal("283ef343", Assert.Single(plan, c => c.Quest.Id == "01ba2c71").Entry?.Into?.Session);
        var alongside = Assert.Single(plan, c => c.Quest.Id == "7c2b9e10");
        Assert.Equal(StartVerdict.Start, alongside.Verdict);
        Assert.Null(alongside.Entry?.Into);
        Assert.Contains("#01ba2c71", alongside.Reason);
    }

    /// <summary>
    /// A resume goes back into its own tree (D79), and a request of its entry planned at the same look does not go in beside it:
    /// the resume is the older quest, so it takes the tree, and the request runs alongside.
    /// </summary>
    [Fact]
    public void A_resume_planned_first_keeps_its_tree_from_a_request_of_its_entry()
    {
        var snapshot = At0759();
        // `711d2524` is answered: B's first quest resumes in `283ef343`'s tree.
        var plan = Planner.Plan(snapshot with { Quests = [snapshot.Quests[0], snapshot.Quests[2]] }, Config());

        var resumed = Assert.Single(plan, c => c.Quest.Id == "3068f747");
        Assert.Equal(StartVerdict.Start, resumed.Verdict);
        Assert.Equal("283ef343", resumed.Resumes?.Session);
        var request = Assert.Single(plan, c => c.Quest.Id == "01ba2c71");
        Assert.Equal(StartVerdict.Start, request.Verdict);
        Assert.Null(request.Entry?.Into);
    }

    /// <summary>A request planned into a tree at this look holds it, so a resume into that tree waits for the request's part to end.</summary>
    [Fact]
    public void A_resume_into_a_tree_a_request_took_at_this_look_waits()
    {
        var snapshot = At0759();
        var plan = Planner.Plan(snapshot with { Quests = [snapshot.Quests[2], snapshot.Quests[0]] }, Config());

        Assert.Equal("283ef343", Assert.Single(plan, c => c.Quest.Id == "01ba2c71").Entry?.Into?.Session);
        var resume = Assert.Single(plan, c => c.Quest.Id == "3068f747");
        Assert.Equal(StartVerdict.RepositoryBusy, resume.Verdict);
        Assert.Contains("#01ba2c71", resume.Reason);
    }

    /// <summary>A quest whose publisher no record here names has no entry the planner can read: it starts as a first start does.</summary>
    [Fact]
    public void A_request_whose_entry_no_record_names_starts_as_before()
    {
        var snapshot = At0759();
        var started = Assert.Single(
            Planner.Plan(snapshot with { Quests = [snapshot.Quests[2] with { PublishedBy = null }] }, Config()));

        Assert.Equal(StartVerdict.Start, started.Verdict);
        Assert.Null(started.Entry);
        Assert.Equal("starting in `db`.", started.Reason);
    }

    /// <summary>A publisher whose record came down from a teammate is read by the id its own machine knew it by (SYNC4).</summary>
    [Fact]
    public void A_publisher_mirrored_from_a_teammate_names_the_entry()
    {
        var snapshot = At0759();
        var entries = new Dictionary<string, string>(StringComparer.Ordinal) { ["laptop/8eba4af2"] = AppEntry };

        var started = Assert.Single(
            Planner.Plan(snapshot with { Quests = [snapshot.Quests[2]], Entries = entries }, Config()));

        Assert.Equal("283ef343", started.Entry?.Into?.Session);
    }

    /// <summary>
    /// The look reads each record's key as the service answered it (SESSONE1a): this machine's last runs carry theirs and their quest,
    /// and every record's, a teammate's mirror and a conversation's included, is read by its id for a publisher to name.
    /// </summary>
    [Fact]
    public void The_look_reads_each_records_entry_as_the_service_answered_it()
    {
        const string records = """
            [
              { "id": "283ef343", "repository": "db", "state": "completed", "quest": "3068f747", "tree": "D:/trees/db/s-283e",
                "entry": "ask:c98a7e/db", "created": "2026-10-14T06:30:00Z" },
              { "id": "laptop/8eba4af2", "repository": "app", "state": "working", "quest": "711d2524", "entry": "ask:c98a7e/app" },
              { "id": "c0ffee11", "repository": "db", "state": "completed", "kind": "chat", "entry": "session:c0ffee11/db" },
              { "id": "0ld0ld00", "repository": "db", "state": "failed", "quest": "0a0a0a0a" }
            ]
            """;

        var last = ServiceClient.ReadLastRun(records);
        var entries = ServiceClient.ReadEntries(records);

        Assert.Equal(DbEntry, last["3068f747"].Entry);
        Assert.Equal("3068f747", last["3068f747"].Quest);
        Assert.Null(last["0a0a0a0a"].Entry);
        Assert.Equal(AppEntry, entries["laptop/8eba4af2"]);
        Assert.Equal("session:c0ffee11/db", entries["c0ffee11"]);
        Assert.False(entries.ContainsKey("0ld0ld00"));
    }

    /// <summary>A repository whose sessions run in its checkout keeps one at a time (D51): the entry changes only its grouping.</summary>
    [Fact]
    public void Where_sessions_run_in_the_checkout_no_tree_is_gone_into()
    {
        var snapshot = At0759();
        var started = Assert.Single(
            Planner.Plan(snapshot with { Quests = [snapshot.Quests[2]] }, Config() with { Trees = ["app"] }));

        Assert.Equal(StartVerdict.Start, started.Verdict);
        Assert.Null(started.Entry);
    }
}
