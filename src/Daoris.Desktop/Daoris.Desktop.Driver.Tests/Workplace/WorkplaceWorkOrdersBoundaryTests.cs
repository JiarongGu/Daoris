using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FOUNDATION1 mod-x, the Workplace-names-Work-orders cut: the behaviour the cut keeps, written against the surface that exists on both
/// sides of it (the intake room's folder rule, the question file's suffix, the session group words and the tree rule they share, the
/// offers a tree makes, an entry's parts and how each began), so the same cases pass on the base and after it. The cases that reach a
/// moved class through a new seam are <see cref="WorkplaceWorkOrdersPortsTests"/>'.
/// </summary>
public sealed class WorkplaceWorkOrdersBoundaryTests : IDisposable
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 28, 9, 0, 0, TimeSpan.Zero);
    private readonly string _home = Path.Combine(GitSeed.RepoRoot(), "_fixtures", "workplace-workorders", Guid.NewGuid().ToString("N"));
    private string Sessions => DaorisHome.Entry(_home, HomeEntry.Sessions);

    public WorkplaceWorkOrdersBoundaryTests() => Directory.CreateDirectory(_home);
    public void Dispose() => GitSeed.Remove(_home);

    private static string Hash(string name) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(name)))[..8].ToLowerInvariant();

    [Fact]
    public void The_intake_room_is_one_folder_per_circle_named_by_the_rule_the_workspace_plan_files_share()
    {
        (string Circle, string Folder)[] cases =
        [
            ("", "default"),
            ("   ", "default"),
            ("Default", "default"),
            ("my-circle", "my-circle"),
            ("My_Circle-2", "my_circle-2"),
            ("my circle", $"my-circle-{Hash("my circle")}"),
            ("  Studio One ", $"studio-one-{Hash("studio one")}"),
            ("界", $"workspace-{Hash("界")}"),
            ("---", $"workspace-{Hash("---")}"),
        ];
        foreach (var (circle, folder) in cases)
        {
            Assert.Equal(folder, IntakeRoom.SafeName(circle));
            Assert.Equal(DaorisHome.Entry(_home, HomeEntry.Intake, folder), IntakeRoom.PathOf(_home, circle));
        }

        Assert.Equal(DaorisHome.Entry(_home, HomeEntry.Setup, "my-circle.json"), WorkspaceSetupFile.PathOf(_home, "my-circle"));
        Assert.NotEqual(IntakeRoom.PathOf(_home, "my circle"), IntakeRoom.PathOf(_home, "my-circle"));
    }

    [Fact]
    public void The_question_file_keeps_its_literal_suffix_and_goes_with_its_record_as_a_mark()
    {
        Assert.Equal(".question.json", QuestionFiles.Suffix);
        Assert.Contains(".question.json", SessionHomeFiles.SessionSuffixes);

        Directory.CreateDirectory(Sessions);
        File.WriteAllText(Path.Combine(Sessions, "s1.question.json"), "{}");
        var files = new SessionHomeFiles(_home);
        Assert.Equal(2L, files.Size("s1").Other);

        var removed = files.Remove(["s1"], null);

        Assert.Equal(["mark"], removed.Went["s1"]);
        Assert.False(File.Exists(Path.Combine(Sessions, "s1.question.json")));
    }

    [Fact]
    public void The_session_groups_keep_their_public_spelling_and_order()
    {
        Assert.Equal(["you", "review", "working", "later", "ended", "archived"], SessionGroup.Order);
        Assert.Equal(("you", "review", "working", "later", "ended", "archived"),
            (SessionGroup.You, SessionGroup.Review, SessionGroup.Working, SessionGroup.Later, SessionGroup.Ended, SessionGroup.Archived));
    }

    [Theory]
    [InlineData("C:\\daoris\\trees\\default\\engine\\aurora", "C:/daoris/trees/default/engine/aurora")]
    [InlineData("C:\\daoris\\trees\\default\\engine\\aurora\\", "C:/daoris/trees/default/engine/aurora")]
    [InlineData("/home/a/trees/x//", "/home/a/trees/x")]
    [InlineData("tree", "tree")]
    [InlineData("", "")]
    public void A_tree_path_is_one_tree_whatever_its_separators(string tree, string normal)
    {
        Assert.Equal(normal, SessionGroups.Normal(tree));
    }

    [Fact]
    public void A_tree_with_unlanded_commits_offers_its_landing_named_by_its_folder_and_its_own_branch_where_it_moved()
    {
        var offer = LandOffer.Of("C:\\daoris\\trees\\default\\engine\\aurora\\", new TreeWork(3, 1));
        Assert.Equal(new LandOffer("daoris/aurora", "aurora", 3, 1), offer);

        Assert.Equal(new LandOffer("feature/mine", "aurora", 2, null),
            LandOffer.Of("/t/aurora", new TreeWork(2, null) { Branch = "feature/mine" }));

        Assert.Null(LandOffer.Of("/t/aurora", new TreeWork(0, 4)));
        Assert.Null(LandOffer.Of("/t/aurora", new TreeWork(null, 4)));
        Assert.Null(LandOffer.Of("/t/aurora/", new TreeWork(1, 0) { Held = Held(null, "main") }));
        Assert.Null(LandOffer.Of("/", new TreeWork(1, 0)));
    }

    [Fact]
    public void A_tree_whose_commits_are_held_by_content_offers_its_discard_with_its_words()
    {
        var work = new TreeWork(2, 0) { Held = Held("refs/daoris/discarded/daoris/aurora", "main") };

        var offer = Assert.IsType<DiscardOffer>(DiscardOffer.Of("C:\\t\\aurora", work));

        Assert.Equal(("daoris/aurora", "aurora", work.Held), (offer.Branch, offer.Tree, offer.Work));
        Assert.Equal("Its work is on `main` by content (a squash merge).", offer.Says);
        Assert.Equal(ContentHold.KeptAt("daoris/aurora", "refs/daoris/discarded/daoris/aurora"), offer.KeptAt);
        Assert.Equal("Its work is on `main` by content (a squash merge), so it is not landed again: that would make a second copy of it. "
            + "Discard its tree instead.", offer.NotLanded);

        var stays = DiscardOffer.Of("/t/aurora", new TreeWork(2, 3) { Held = Held(null, "main", stays: "it holds 3 uncommitted paths") })!;
        Assert.Null(stays.KeptAt);
        Assert.Equal("Its work is on `main` by content (a squash merge). Its tree stays: it holds 3 uncommitted paths.", stays.Says);

        Assert.Null(DiscardOffer.Of("/t/aurora", new TreeWork(2, 0)));
        Assert.Null(DiscardOffer.Of("/t/aurora", new TreeWork(0, 0) { Held = Held(null, "main") }));
        Assert.Null(DiscardOffer.Of("/", work));
    }

    private static HeldWork Held(string? keeps, string where, string? stays = null) => new(new ContentHold(where, Squash: true), keeps, stays);

    [Fact]
    public void An_entrys_accept_lands_the_parts_whose_rows_offer_a_landing_and_an_ask_names_its_own_entries()
    {
        var entry = new SessionEntry("quest:q1/engine", "quest:q1", "engine", SessionGroup.Review, "s2",
        [
            new EntryPart("s1", "q1", "failed", SessionGroup.Ended, PartBegan.Opened),
            new EntryPart("s2", "q1", "completed", SessionGroup.Review, PartBegan.Retried) { After = "s1" },
            new EntryPart("s3", "q1", "completed", SessionGroup.Review, PartBegan.Resumed) { After = "s2" },
        ]);
        SessionGrouping[] rows =
        [
            new("s1", SessionGroup.Ended, "failed"),
            new("s2", SessionGroup.Review, "completed") { Lands = new LandOffer("daoris/a", "a", 1, 0) },
            new("s3", SessionGroup.Review, "completed") { Discards = new DiscardOffer("daoris/b", "b", Held(null, "main")) },
        ];

        Assert.Equal(["s2"], EntryAccepts.PartsOf(entry, rows).Select(part => part.Session));
        Assert.Empty(EntryAccepts.PartsOf(entry, []));

        SessionEntry Of(string origin, string repository) => entry with { Origin = origin, Repository = repository };
        var entries = new[] { Of("ask:a1", "zeta"), Of("ask:a1", "intake"), Of("ask:a2", "alpha"), Of("ask:a1", "alpha") };
        Assert.Equal(["alpha", "zeta"], EntryAccepts.EntriesOf("a1", entries).Select(each => each.Repository));
    }

    [Fact]
    public void An_entrys_lead_is_its_oldest_part_only_where_the_session_shares_its_entry_with_another()
    {
        SessionRecord Record(string id, int minute, string? entry = null) =>
            new(id, "engine", "completed") { Created = T0.AddMinutes(minute), Entry = entry };
        SessionRecord[] records = [Record("b", 2, "quest:q1/engine"), Record("a", 1, "quest:q1/engine"), Record("c", 3), Record("d", 4, "quest:q9/engine")];

        Assert.Equal("a", EntryAccepts.EntryLead(records, "b")!.Id);
        Assert.Equal("a", EntryAccepts.EntryLead(records, "a")!.Id);
        Assert.Null(EntryAccepts.EntryLead(records, "c"));
        Assert.Null(EntryAccepts.EntryLead(records, "d"));
        Assert.Null(EntryAccepts.EntryLead(records, "gone"));
    }

    [Fact]
    public void Each_part_of_an_entry_began_the_way_its_link_and_its_quests_chain_say()
    {
        Assert.Equal(
            ["opened", "retried", "resumed", "carried-on", "handed-over", "moved", "asked"],
            [PartBegan.Opened, PartBegan.Retried, PartBegan.Resumed, PartBegan.CarriedOn, PartBegan.HandedOver, PartBegan.Moved, PartBegan.Asked]);

        var records = new JsonArray(
            Record("s1", "q1", "failed", 1),
            Record("s2", "q1", "stopped", 2),
            Record("s3", "q1", "working", 3),
            Record("c1", null, "completed", 4, entry: "session:c1/engine"),
            Record("c2", null, "working", 5, entry: "session:c1/engine", from: "c1", fromWhy: HandOverWhy.CarryOn),
            Record("c3", null, "working", 6, entry: "session:c1/engine", from: "c2", fromWhy: HandOverWhy.Resume),
            Record("c4", null, "working", 7, entry: "session:c1/engine", from: "c3", fromWhy: HandOverWhy.Press),
            Record("c5", null, "working", 8, entry: "session:c1/engine", from: "c4", fromWhy: "unheard-of"));
        var quests = new[] { new QuestView("q1", "game", "engine", "The work", "A body.", "Taken") };
        var look = SessionLook.From(records.ToJsonString(), quests, [], _ => 0);

        var entries = SessionEntries.Read(look);

        var work = Assert.Single(entries, entry => entry.Origin == "quest:q1");
        Assert.Equal([("s1", "opened", null), ("s2", "retried", "s1"), ("s3", "carried-on", "s2")], work.Parts.Select(part => (part.Session, part.Began, part.After)));
        var chain = Assert.Single(entries, entry => entry.Origin == "session:c1");
        Assert.Equal(
            [("c1", "opened", null), ("c2", "carried-on", "c1"), ("c3", "moved", "c2"), ("c4", "handed-over", "c3"), ("c5", "handed-over", "c4")],
            chain.Parts.Select(part => (part.Session, part.Began, part.After)));
        Assert.Equal(["quest:q1/engine", "session:c1/engine"], entries.Select(entry => entry.Key).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void An_entrys_head_is_where_its_first_parts_line_ends_across_carried_on_asked_and_alongside_parts()
    {
        var records = new JsonArray(
            Record("c1", null, "completed", 1, entry: "session:c1/engine", head: "c3"),
            Record("c2", null, "completed", 2, entry: "session:c1/engine", from: "c1", fromWhy: HandOverWhy.CarryOn, head: "c3"),
            Record("c3", null, "completed", 3, entry: "session:c1/engine", from: "c2", fromWhy: HandOverWhy.CarryOn, head: "c3"),
            Record("a1", "q9", "completed", 4, entry: "session:c1/engine", head: "a1"));
        var quests = new[] { new QuestView("q9", "game", "engine", "Asked", "A body.", "Taken") { PublishedBy = "c1" } };

        var entry = Assert.Single(SessionEntries.Read(SessionLook.From(records.ToJsonString(), quests, [], _ => 0)));

        Assert.Equal("c3", entry.Head);
        Assert.Equal(["c3", "c3", "c3", "a1"], entry.Parts.Select(part => part.Head));
    }

    [Fact]
    public void A_chains_head_is_its_newest_step()
    {
        var records = new JsonArray(
            Record("s1", "q1", "completed", 1, head: "s3"),
            Record("s2", "q2", "completed", 2, entry: "quest:q1/engine", head: "s3"),
            Record("s3", "q3", "completed", 3, entry: "quest:q1/engine", head: "s3"));
        var quests = new[]
        {
            new QuestView("q1", "game", "engine", "One", "A body.", "Done"),
            new QuestView("q2", "game", "engine", "Two", "A body.", "Done") { Parent = "q1" },
            new QuestView("q3", "game", "engine", "Three", "A body.", "Done") { Parent = "q2" },
        };

        var entry = Assert.Single(SessionEntries.Read(SessionLook.From(records.ToJsonString(), quests, [], _ => 0)));

        Assert.Equal("s3", entry.Head);
    }

    [Fact]
    public void An_asks_sibling_quests_have_no_head_each_part_marked_with_its_own_lines()
    {
        var records = new JsonArray(
            Record("s1", "q1", "completed", 1, entry: "ask:a1/engine", head: "s1"),
            Record("s2", "q2", "completed", 2, entry: "ask:a1/engine", head: "s2"));
        var quests = new[]
        {
            new QuestView("q1", "ask #a1", "engine", "One", "A body.", "Done"),
            new QuestView("q2", "ask #a1", "engine", "Two", "A body.", "Done"),
        };

        var entry = Assert.Single(SessionEntries.Read(SessionLook.From(records.ToJsonString(), quests, [], _ => 0)));

        Assert.Null(entry.Head);
        Assert.Equal(["s1", "s2"], entry.Parts.Select(part => part.Head));
    }

    [Fact]
    public void A_row_opens_on_the_head_unless_a_part_waits_on_the_person_or_is_working()
    {
        JsonArray Parts(string last) => new(
            Record("c1", null, "completed", 1, entry: "session:c1/engine", head: "c3"),
            Record("c2", null, last, 2, entry: "session:c1/engine", from: "c1", fromWhy: HandOverWhy.CarryOn, head: "c3"),
            Record("c3", null, "completed", 3, entry: "session:c1/engine", from: "c2", fromWhy: HandOverWhy.CarryOn, head: "c3"));
        SessionEntry Read(string last) => Assert.Single(SessionEntries.Read(SessionLook.From(Parts(last).ToJsonString(), [], [], _ => 0)));

        var ended = Read("completed");
        Assert.Equal("c3", ended.Opens);
        var working = Read("working");
        Assert.Equal(working.Leading, working.Opens);
        Assert.Equal("c2", working.Opens);
        var waiting = Read("awaiting-person");
        Assert.Equal("c2", waiting.Opens);
    }

    private static JsonObject Record(string id, string? quest, string state, int minute, string? entry = null, string? from = null, string? fromWhy = null, string? head = null) =>
        new()
        {
            ["head"] = head,
            ["id"] = id,
            ["repository"] = "engine",
            ["adapter"] = "claude-code",
            ["state"] = state,
            ["kind"] = "driven",
            ["quest"] = quest,
            ["entry"] = entry ?? (quest is null ? null : $"quest:{quest}/engine"),
            ["from"] = from,
            ["fromWhy"] = fromWhy,
            ["created"] = T0.AddMinutes(minute).ToString("O"),
            ["updated"] = T0.AddMinutes(minute + 1).ToString("O"),
        };
}
