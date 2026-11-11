using System.Security.Cryptography;
using System.Text;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A run's review read by run (<see cref="RunReviews"/>): the newest record, or an attempt named; the presses only on the attempt the review
/// stands on; a newer record announced with the paths it changed; the steps after the Accept with their states; and a file's content drawn
/// from the record's own hash, never from the place as it stands once it moved.
/// </summary>
public sealed class RunReviewsTests : IDisposable
{
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "run-reviews", Guid.NewGuid().ToString("N")[..8]);

    private static readonly DateTimeOffset T = DateTimeOffset.Parse("2026-11-12T09:00:00Z");

    private const string Graph = """
        {"schemaVersion":2,"entries":["work"],"end":"apply",
         "nodes":[{"id":"work","kind":"claimed","version":1,"reach":"observed"},{"id":"review","kind":"review","version":1,"reach":"pressed"},
                  {"id":"apply","kind":"land","version":1,"reach":"performed","options":{"method":"merge"}}],
         "wires":[{"id":"a","kind":"after","from":"work","to":"review"},{"id":"b","kind":"after","from":"review","to":"apply"},
                  {"id":"c","kind":"back","from":"review","on":"rejected","to":"work"}]}
        """;

    public void Dispose() => GitSeed.Discard(_home);

    private void Recorded(int standing)
    {
        RunHome.Bind(_home, "q1", RunHome.SaveGraph(_home, Graph), T);
        var unit = RunUnits.IdOf("q1");
        new NodeRecordFiles(_home, () => true).Write(new NodeRecord(unit)
        {
            Statuses = new SortedDictionary<string, NodeStatus>(StringComparer.Ordinal)
            {
                ["work"] = new(unit, "work", NodeStates.Reached) { Attempt = 1 },
                ["review"] = new(unit, "review", NodeStates.Active) { Attempt = standing, Since = T },
            },
        });
        ReviewRecords.Keep(_home, Record(1, "aaaaaaa1", new ReviewRow("notes.md", "modified") { Before = "b0", After = "b1" }, new ReviewRow("plan.md", "added") { After = "p1" }));
        if (standing > 1) ReviewRecords.Keep(_home, Record(2, "bbbbbbb2", new ReviewRow("notes.md", "modified") { Before = "b0", After = "b2" }, new ReviewRow("plan.md", "added") { After = "p1" }));
    }

    private static ReviewRecord Record(int attempt, string revision, params ReviewRow[] rows) =>
        new("q1", "review", attempt, ReviewKinds.Folder, revision, T.AddMinutes(attempt)) { Place = RunHome.Repository, Rows = rows };

    [Fact]
    public void The_newest_record_is_read_with_its_presses_and_what_follows_its_accept()
    {
        Recorded(standing: 2);

        var review = RunReviews.Read(_home, "q1", null)!;

        Assert.Equal((2, "bbbbbbb2", ReviewKinds.Folder), (review.Record!.Attempt, review.Record.Revision, review.Record.Kind));
        Assert.Equal((NodeStates.Active, 2), (review.State, review.Standing));
        Assert.Equal([NodeWordActs.Accept, NodeWordActs.Changes, NodeWordActs.Stop], review.Presses);
        Assert.Null(review.Newer);
        var follows = Assert.Single(review.Follows);
        Assert.Equal(("apply", NodeKinds.Land), (follows.Node, follows.Kind));
        Assert.Null(review.Operations);
    }

    [Fact]
    public void A_plans_review_is_read_with_the_operations_of_the_plan_its_record_names()
    {
        Recorded(standing: 1);
        new SystemPlans(_home).Keep("s1", "sample-dns", [new SystemOperation("record/www", "update", "10.0.0.4", "10.0.0.9", new("version", "3"))], T);
        // The fixture's first record is a folder's; the plan's are the next two attempts.
        ReviewRecords.Keep(_home, new ReviewRecord("q1", "review", 2, ReviewKinds.Plan, "ccccccc3", T.AddMinutes(2))
        {
            Place = "sample-dns", Reference = PlanReview.Reference(_home, ["s1"])!,
        });
        ReviewRecords.Keep(_home, new ReviewRecord("q1", "review", 3, ReviewKinds.Plan, "ddddddd4", T.AddMinutes(3))
        {
            Place = "sample-dns", Reference = new() { PlanRecord = "s1" },
        });

        var named = RunReviews.Read(_home, "q1", 2)!;
        var unread = RunReviews.Read(_home, "q1", 3)!;

        var operation = Assert.Single(named.Operations!);
        Assert.Equal(("record/www", "update", "10.0.0.9"), (operation.Resource, operation.Op, operation.After));
        Assert.Null(unread.Operations);
    }

    [Fact]
    public void The_review_says_its_kinds_values_the_steps_before_it_and_each_following_steps_way_door_and_recovery()
    {
        Recorded(standing: 2);
        var unit = RunUnits.IdOf("q1");
        var files = new NodeRecordFiles(_home, () => true);
        var record = files.Read(unit)!;
        files.Write(record with
        {
            Statuses = new SortedDictionary<string, NodeStatus>(record.Statuses.ToDictionary(pair => pair.Key, pair => pair.Value), StringComparer.Ordinal)
            {
                ["apply"] = new(unit, "apply", NodeStates.Unknown) { Attempt = 1 },
            },
        });

        var review = RunReviews.Read(_home, "q1", null)!;

        Assert.Equal((ReviewKinds.Folder, 2, 0), (review.Says.Kind, review.Says.Files, review.Says.Parts));
        Assert.Equal(("work", NodeStates.Reached), (Assert.Single(review.Before).Node, review.Before[0].State));
        var apply = Assert.Single(review.Follows);
        Assert.Equal((WireKinds.After, NodeStates.Unknown), (apply.Via, apply.State));
        Assert.Contains(HeldResolutions.Happened, apply.Offers);
    }

    [Fact]
    public void A_reached_steps_door_is_its_evidence()
    {
        var acceptance = new NodeAcceptance(RunUnits.IdOf("q1"), "t", "gph-x", "apply", 1, 1) { Evidence = new NodePort("pull-request", "https://host.example/pr/7", null, null) };

        Assert.Equal("https://host.example/pr/7", RunReviews.DoorOf(new NodeStatus(RunUnits.IdOf("q1"), "apply", NodeStates.Reached) { Acceptance = acceptance }));
    }

    [Fact]
    public void An_earlier_attempt_offers_no_press_and_announces_the_newer_record_with_the_paths_it_changed()
    {
        Recorded(standing: 2);

        var review = RunReviews.Read(_home, "q1", 1)!;

        Assert.Equal(1, review.Record!.Attempt);
        Assert.Empty(review.Presses);
        Assert.Equal((2, "bbbbbbb2"), (review.Newer!.Attempt, review.Newer.Revision));
        Assert.Equal(["notes.md"], review.Newer.Changed);
    }

    [Fact]
    public void A_run_with_no_review_record_yet_is_read_with_its_state_and_none_for_one_not_bound()
    {
        RunHome.Bind(_home, "q1", RunHome.SaveGraph(_home, Graph), T);

        var review = RunReviews.Read(_home, "q1", null)!;

        Assert.Null(review.Record);
        Assert.Empty(review.Presses);
        Assert.Null(RunReviews.Read(_home, "q9", null));
    }

    [Fact]
    public void A_files_content_is_drawn_from_its_hash_and_refused_moved_where_the_place_no_longer_holds_it()
    {
        var root = Path.Combine(_home, "folder");
        Directory.CreateDirectory(root);
        var store = new ManifestStore(_home, "aurora", "docs");
        var kept = Encoding.UTF8.GetBytes("kept words\n");
        store.Content.Keep(Sha(kept), kept);
        File.WriteAllText(Path.Combine(root, "live.md"), "live words\n");
        var live = Sha(Encoding.UTF8.GetBytes("live words\n"));
        store.Keep("r1", ManifestMoment.End, new RunManifest("folder", null, null, T, []) { Root = root });
        var record = new ReviewRecord("q1", "review", 1, ReviewKinds.Folder, "aaaaaaa1", T)
        {
            Reference = new() { Workspace = "aurora", Department = "docs", End = "r1" },
            Rows = [new ReviewRow("kept.md", "modified") { After = Sha(kept) }, new ReviewRow("live.md", "added") { After = live }],
        };

        Assert.Equal("kept words\n", ReviewContent.Read(_home, record, "kept.md", ReviewContent.After).Text);
        Assert.Equal("live words\n", ReviewContent.Read(_home, record, "live.md", ReviewContent.After).Text);
        File.WriteAllText(Path.Combine(root, "live.md"), "changed since\n");
        Assert.Equal(ReviewContent.Moved, ReviewContent.Read(_home, record, "live.md", ReviewContent.After).Refused);
        Assert.Equal(ReviewContent.NotRow, ReviewContent.Read(_home, record, "other.md", ReviewContent.After).Refused);
    }

    private static string Sha(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
}
