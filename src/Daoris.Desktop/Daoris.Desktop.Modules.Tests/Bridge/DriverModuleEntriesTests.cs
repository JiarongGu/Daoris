using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// ACCEPTALL1 (D167's 2026-10-15 amendment points 1 and 4): the entry's Accept and the ask's, <c>ENTRY_LANDING</c> and
/// <c>LAND_ENTRY</c>, <c>ASK_LANDING</c> and <c>LAND_ASK</c>. Which parts an entry's Accept lands (those whose tree offers work, in the
/// entry's order), which entries an ask's lands (its own, one per repository, never its intake), and what goes on the wire. The
/// landing over real git is the driver's <c>EntryLandingTests</c>.
/// </summary>
public sealed class DriverModuleEntriesTests
{
    private static EntryPart Part(string session) => new(session, "0fda18", "completed", SessionGroup.Review, PartBegan.Opened);

    private static SessionGrouping Row(string session, bool lands) => new(session, SessionGroup.Review, "completed")
    {
        Lands = lands ? new LandOffer($"daoris/s-{session}", $"s-{session}", 1, 0) : null,
    };

    [Fact]
    public void An_entrys_accept_takes_the_parts_whose_tree_offers_work_in_the_entrys_order()
    {
        var entry = new SessionEntry("ask:a3/engine", "ask:a3", "engine", SessionGroup.Review, "s3", [Part("s1"), Part("s2"), Part("s3")]);

        var parts = EntriesRoutes.PartsOf(entry, [Row("s3", lands: true), Row("s2", lands: false), Row("s1", lands: true)]);

        Assert.Equal(["s1", "s3"], parts.Select(part => part.Session));
    }

    [Fact]
    public void An_asks_accept_takes_its_own_entries_one_per_repository_and_never_its_intake()
    {
        SessionEntry Entry(string key) => new(key, key.Split('/')[0], key.Split('/')[1], SessionGroup.Review, "s1", [Part("s1")]);

        var entries = EntriesRoutes.EntriesOf("a3", [Entry("ask:a3/web"), Entry("ask:a4/engine"), Entry("ask:a3/intake"), Entry("ask:a3/engine")]);

        Assert.Equal(["ask:a3/engine", "ask:a3/web"], entries.Select(entry => entry.Key));
    }

    [Fact]
    public void The_plan_goes_on_the_wire_with_each_part_and_what_superseded_it()
    {
        var plan = new EntryPlan(LandingForm.Branch, "feature/x", [
            new EntryPartPlan("s1", new LandingPlan(LandingForm.Branch, "feature/x", "repository"))
            {
                Superseded = new PartSuperseded("s2", "CONFLICT (content): Merge conflict in app.cs.", Conflicts: true),
            },
            new EntryPartPlan("s2", new LandingPlan(LandingForm.Branch, "feature/x", "repository")) { Clash = new PartClash("s3", "CLAUDE.md") },
            new EntryPartPlan("s3", null) { SameTreeAs = "s2" },
        ]);

        var read = Read(EntriesRoutes.Plan("ask:a3/engine", "engine", plan));

        Assert.Equal(("ask:a3/engine", "engine", "branch", "feature/x"),
            (read.GetProperty("entry").GetString(), read.GetProperty("repository").GetString(), read.GetProperty("form").GetString(),
                read.GetProperty("target").GetString()));
        var parts = read.GetProperty("parts").EnumerateArray().ToList();
        Assert.Equal(["s1", "s2", "s3"], parts.Select(part => part.GetProperty("session").GetString()));
        var superseded = parts[0].GetProperty("superseded");
        Assert.Equal(("s2", true), (superseded.GetProperty("by").GetString(), superseded.GetProperty("conflicts").GetBoolean()));
        Assert.Equal(JsonValueKind.Null, parts[1].GetProperty("superseded").ValueKind);
        Assert.Equal(JsonValueKind.Null, parts[0].GetProperty("clash").ValueKind);
        var clash = parts[1].GetProperty("clash");
        Assert.Equal(("s3", "CLAUDE.md"), (clash.GetProperty("by").GetString(), clash.GetProperty("says").GetString()));
        Assert.Equal("s2", parts[2].GetProperty("sameTreeAs").GetString());
        Assert.Equal("feature/x", parts[1].GetProperty("target").GetString());
    }

    [Fact]
    public void The_press_goes_on_the_wire_with_each_parts_outcome_and_its_own_words()
    {
        var landed = new EntryLanding(false, "Landed 1 part(s) on `feature/x`, in the order they ran.", "feature/x", [
            new EntryPartLanding("s1", EntryPartOutcome.Landed, new TreeLanding(true, "put the work on `feature/x`.", "feature/x")),
            new EntryPartLanding("s2", EntryPartOutcome.Refused, new TreeLanding(false, "Nothing was landed: it conflicts.")
            {
                Conflict = new LandingConflict("CONFLICT (content): Merge conflict in app.cs.", "Rebase your work onto `feature/x`."),
            }),
            new EntryPartLanding("s3", EntryPartOutcome.NotTried),
        ]);

        var read = Read(EntriesRoutes.Landed("ask:a3/engine", "engine", landed));

        Assert.False(read.GetProperty("done").GetBoolean());
        Assert.Equal("feature/x", read.GetProperty("branch").GetString());
        var parts = read.GetProperty("parts").EnumerateArray().ToList();
        Assert.Equal(["landed", "refused", "not-tried"], parts.Select(part => part.GetProperty("outcome").GetString()));
        Assert.Equal("put the work on `feature/x`.", parts[0].GetProperty("message").GetString());
        Assert.Equal("Rebase your work onto `feature/x`.", parts[1].GetProperty("conflict").GetProperty("words").GetString());
        Assert.Equal(JsonValueKind.Null, parts[2].GetProperty("message").ValueKind);
    }

    private static SessionRecord Record(string id, string? entry, int minute) =>
        new(id, "engine", "completed") { Entry = entry, Created = new DateTimeOffset(2026, 10, 16, 9, minute, 0, TimeSpan.Zero) };

    /// <summary>
    /// ACCEPTALL1b (D167's amendment point 2): a part's own Accept is its entry's for that part alone, so <c>LANDING</c> and
    /// <c>LAND_SESSION_TREE</c> name its landing for the entry's first part, oldest by its record's <c>created</c> then id.
    /// </summary>
    [Fact]
    public void A_parts_own_accept_is_named_for_its_entrys_first_part()
    {
        IReadOnlyList<SessionRecord> records =
        [
            Record("s3", "ask:a3/engine", 3), Record("s1", "ask:a3/engine", 1), Record("s9", "ask:a9/engine", 0), Record("s2", "ask:a3/engine", 1),
        ];

        Assert.Equal("s1", TreesRoutes.EntryLead(records, "s3")?.Id);
        Assert.Equal("s1", TreesRoutes.EntryLead(records, "s1")?.Id);
        Assert.Equal("s1", TreesRoutes.EntryLead(records, "s2")?.Id);
    }

    /// <summary>A session with no entry, or its entry's only part, lands exactly as before: no entry leads it.</summary>
    [Fact]
    public void A_session_alone_in_its_entry_or_with_none_has_no_lead()
    {
        // A record from a host before the key is its own line of work (SESSONE1b), as is one the records no longer hold.
        IReadOnlyList<SessionRecord> records = [Record("s1", "ask:a3/engine", 1), Record("s2", null, 2), Record("s3", null, 3)];

        Assert.Null(TreesRoutes.EntryLead(records, "s1"));
        Assert.Null(TreesRoutes.EntryLead(records, "s2"));
        Assert.Null(TreesRoutes.EntryLead(records, "s4"));
    }

    private static JsonElement Read(object wire) => JsonDocument.Parse(JsonSerializer.Serialize(wire, JsonSerializerOptions.Web)).RootElement;
}
