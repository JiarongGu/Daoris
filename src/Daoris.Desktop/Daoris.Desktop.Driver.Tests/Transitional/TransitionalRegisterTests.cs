using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// ONEWAY1ad-i (D228; the oneway design §1.3): the register is a plain list of entries, each with the site it keeps, the row that
/// deletes it and a condition that reads a home. <c>tools/transitional-markers.mjs</c> holds the markers against the same list.
/// </summary>
[Trait(Category.Name, Category.Census)]
public sealed class TransitionalRegisterTests : IDisposable
{
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "transitional-register", Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        GitSeed.Discard(_home);
    }

    private static TransitionalEntry Entry(string id) => TransitionalRegister.Entries.Single(entry => entry.Id == id);

    private void Put(HomeEntry entry, string json)
    {
        var path = DaorisHome.Entry(_home, entry);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, json);
    }

    [Fact]
    public void Every_entry_is_whole_and_unique()
    {
        var entries = TransitionalRegister.Entries;
        Assert.NotEmpty(entries);
        Assert.Equal(entries.Count, entries.Select(entry => entry.Id).Distinct(StringComparer.Ordinal).Count());
        foreach (var entry in entries)
        {
            Assert.All(new[] { entry.Id, entry.Kept, entry.Reaches, entry.Ends, entry.Row }, text => Assert.False(string.IsNullOrWhiteSpace(text), entry.Id));
            Assert.Matches("^[A-Z][A-Z0-9]*\\d[\\w-]*$", entry.Row);
        }
    }

    [Fact]
    public void Every_entry_s_kept_sites_exist_and_carry_its_marker()
    {
        var driver = Path.Combine(WorkspaceRoot.Folder, "src", "Daoris.Desktop", "Daoris.Desktop.Driver");
        foreach (var entry in TransitionalRegister.Entries)
        {
            foreach (var site in entry.Kept.Split(", "))
            {
                var file = Path.Combine(driver, site[..site.IndexOf(':')].Replace('/', Path.DirectorySeparatorChar));
                Assert.True(File.Exists(file), $"{entry.Id}: {site}");
                Assert.Contains($"transitional: {entry.Id}", File.ReadAllText(file));
            }
        }
    }

    [Fact]
    public void Every_condition_reads_an_empty_home_and_one_that_does_not_read()
    {
        foreach (var entry in TransitionalRegister.Entries) _ = entry.Ended(_home);
        Put(HomeEntry.DriverConfig, "not json");
        Put(HomeEntry.Harnesses, "not json");
        Assert.All(TransitionalRegister.Entries, entry => Assert.False(entry.Ended(_home), entry.Id));
    }

    /// <summary>ONEWAY1b: the old keys' reading ends with the staffing part's census, once the store holds a written order for each of the five.</summary>
    [Fact]
    public void ONEWAY1b_the_actor_key_ends_when_the_store_holds_an_order_for_each_of_the_five()
    {
        try
        {
            Assert.False(Entry("actor-key-before").Ended(_home));
            Assert.Equal(StaffingPart.Part, Entry("actor-key-before").Part);
            Put(HomeEntry.DriverConfig, """{"adapter":"claude-code","actors":{"build":[{"agent":"claude-code"}],"chat":[{"agent":"claude-code"}]}}""");
            var staffing = new RoleStaffing(_home, () => StoreSettled.Yes);
            Assert.Equal(StaffingImported.Done, staffing.ImportFile().How);
            Assert.False(Entry("actor-key-before").Ended(_home));
            staffing.Set([("brainstorm", [new ActorChoice("claude-code")]), ("intake", []), ("ask-daoris", [])]);
            Assert.True(Entry("actor-key-before").Ended(_home));
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        }
    }

    [Fact]
    public void The_git_mark_ends_when_the_key_is_gone()
    {
        Put(HomeEntry.DriverConfig, """{"gitMoved":{"at":"2026-11-01T00:00:00Z"}}""");
        Assert.False(Entry("git-moved-key").Ended(_home));
        Put(HomeEntry.DriverConfig, "{}");
        Assert.True(Entry("git-moved-key").Ended(_home));
    }

    [Fact]
    public void The_old_names_end_when_no_entry_spells_one()
    {
        Put(HomeEntry.Harnesses, """{"rotationUse":{"claude-code":{"early":true,"near":90}}}""");
        Assert.False(Entry("harness-old-names").Ended(_home));
        Put(HomeEntry.Harnesses, """{"rotationUse":{"claude-code":{"switch":"early"}}}""");
        Assert.True(Entry("harness-old-names").Ended(_home));
    }

    /// <summary>
    /// ONEWAY1o (the oneway design §4 M5): COMMS1's and D226's kept paths are each an entry with the row that deletes it, those ended by
    /// the floor or by a row's build never reading as ended from a home.
    /// </summary>
    [Fact]
    public void The_kept_paths_of_COMMS1_and_D226_are_registered_with_their_rows()
    {
        foreach (var (id, row) in new[]
        {
            ("solicited-older-host", "ONEWAY1af"), ("solicited-old-files", "ONEWAY1af"), ("solicited-old-guards", "ONEWAY1af"),
            ("opinion-handed-guard", "ONEWAY1af"), ("answers-alone-events", "ONEWAY1af"), ("presses-older-host", "ONEWAY1af"),
            ("held-press-unasked", "ONEWAY1af"), ("staffing-copy", "ONEWAY1af"), ("conversation-moves-refused", "CONTEXTDRIVE1n"),
        })
        {
            Assert.Equal(row, Entry(id).Row);
        }

        Put(HomeEntry.DriverConfig, "{}");
        foreach (var id in new[] { "presses-older-host", "staffing-copy", "conversation-moves-refused" }) Assert.False(Entry(id).Ended(_home), id);
    }

    /// <summary>ONEWAY1o: a press held before COMMS1e names its choice and no question; the read ends when no held word is such a press.</summary>
    [Fact]
    public void The_held_press_ends_when_no_held_word_names_a_choice_without_its_question()
    {
        Put(HomeEntry.DriverConfig, "{}");
        Assert.True(Entry("held-press-unasked").Ended(_home));
        var held = HeldWordsFile.PathOf(_home);
        Directory.CreateDirectory(Path.GetDirectoryName(held)!);
        File.WriteAllText(held, """{"held":[{"session":"s1","text":"The monthly one","door":"screen","choice":"The monthly one"}]}""");
        Assert.False(Entry("held-press-unasked").Ended(_home));
        File.WriteAllText(held, """{"held":[{"session":"s1","text":"The monthly one","door":"screen","choice":"The monthly one","question":"q-1"},{"session":"s1","text":"words","door":"screen"}]}""");
        Assert.True(Entry("held-press-unasked").Ended(_home));
        File.WriteAllText(held, "not json");
        Assert.False(Entry("held-press-unasked").Ended(_home));
    }

    [Fact]
    public void The_fold_ends_when_the_file_holds_one_set_per_agent()
    {
        Put(HomeEntry.Harnesses, """{"workspaces":{"w":{"claude-code":["a"]}}}""");
        Assert.False(Entry("account-pool-fold").Ended(_home));
        Put(HomeEntry.Harnesses, """{"rotation":{"claude-code":["a"]}}""");
        Assert.True(Entry("account-pool-fold").Ended(_home));
    }

    private HomeReceipt Receipt(string digest, string? build, string outcome = ConversionOutcomes.Done, DateTimeOffset? at = null) =>
        new(digest, at ?? DateTimeOffset.Parse("2026-11-04T10:00:00Z"), new ConversionChoices(), [new PartReceipt("settings", digest, outcome, [])]) { Build = build };

    /// <summary>ONEWAY1ad-ii (the oneway design §1.3): the floor from fixture homes: no receipt, a receipt with no build, one with a build, a store stamp.</summary>
    [Fact]
    public void ONEWAY1ad_ii_the_floor_reads_the_oldest_standing_receipts_build_and_the_stores_stamp()
    {
        var waited = new OlderBuild("the builds before 0.42") { Version = "0.41.0" };
        Assert.Equal(new HomeFloor(null, null), HomeFloor.Of(_home));
        Assert.False(HomeFloor.Of(_home).Passes(waited));

        // A receipt with no build gives none, even beside one that has a build: every entry waiting on the floor stays.
        var receipts = new HomeReceipts(_home);
        receipts.Write(Receipt(new string('a', 64), null, at: DateTimeOffset.Parse("2026-11-03T10:00:00Z")));
        receipts.Write(Receipt(new string('b', 64), "0.43.0"));
        Assert.Null(HomeFloor.Of(_home).Build);

        // Undone, the oldest no longer stands, and the next one's build is the floor.
        receipts.Write(Receipt(new string('a', 64), null, at: DateTimeOffset.Parse("2026-11-03T10:00:00Z")) with { UndoneAt = DateTimeOffset.Parse("2026-11-03T11:00:00Z") });
        Assert.Equal("0.43.0", HomeFloor.Of(_home).Build);
        Assert.True(HomeFloor.Of(_home).Passes(waited));
        Assert.False(HomeFloor.Of(_home).Passes(waited with { Version = "0.43.0" }));
        Assert.False(HomeFloor.Of(_home).Passes(new OlderBuild("no release names it")));

        // A receipt whose only part failed does not stand, so it gives no floor.
        receipts.Write(Receipt(new string('b', 64), "0.43.0", ConversionOutcomes.Failed));
        Assert.Null(HomeFloor.Of(_home).Build);

        // The desktop store's stamp: a build below it reads the store and writes nothing.
        using (DesktopStore.Open(DesktopStore.LocationIn(_home), new DesktopStoreOptions { Settled = () => StoreSettled.Yes })) { }
        var floor = HomeFloor.Of(_home);
        Assert.Equal(DesktopStore.WriterStamp, floor.StoreWriter);
        Assert.True(floor.Passes(new OlderBuild("before the policy writer") { StoreWriter = DesktopStore.DeliveryWriter }));
        Assert.False(floor.Passes(new OlderBuild("the policy writer") { StoreWriter = DesktopStore.WriterStamp }));
    }

    /// <summary>ONEWAY1ad-ii: the list from a fixture register: the parts' census first, then one entry ended and one not, and one read by its part.</summary>
    [Fact]
    public void ONEWAY1ad_ii_the_list_says_each_parts_census_then_each_entry_ended_or_not()
    {
        TransitionalEntry Planted(string id, bool ended, string? part = null) => new()
        {
            Id = id, Kept = "A.cs:1", Reaches = $"{id} reaches", Ends = $"{id} ends", Row = "ONEWAY1af", Ended = _ => ended, Part = part,
            Waits = id == "kept" ? new OlderBuild("the builds before ROWX1's") : null,
        };

        var list = TransitionalRegister.List(_home, [new("workflows", 3, false)],
            [Planted("gone", ended: true), Planted("kept", ended: false), Planted("by-part", ended: true, part: "workflows")]);

        Assert.Equal(
            [(KeptLine.Part, "workflows", false), (KeptLine.Read, "gone", true), (KeptLine.Read, "kept", false), (KeptLine.Read, "by-part", false)],
            list.Select(each => (each.Kind, each.Id, each.Ended)));
        Assert.Equal(3, list[0].Count);
        Assert.Equal(("kept ends", "ONEWAY1af", "the builds before ROWX1's"), (list[2].Ends, list[2].Row, list[2].Waits));
        Assert.True(TransitionalRegister.List(_home, [new("workflows", 0, true)], [Planted("by-part", false, "workflows")])[1].Ended);
        Assert.Equal([KeptLine.Read], TransitionalRegister.List(_home, null, [Planted("gone", true)]).Select(each => each.Kind));
    }

    /// <summary>ONEWAY1ad-ii: WORKFLOWCLEAN1 §4.7's kept reads are registered, each with its row, the engine's ending with the workflow part's census.</summary>
    [Fact]
    public void ONEWAY1ad_ii_the_workflow_waves_kept_reads_are_registered()
    {
        Assert.Equal(
            [("auto-lander", "WORKFLOWCLEAN1z"), ("roles-mode-setup", "WORKFLOWCLEAN1z")],
            new[] { "auto-lander", "roles-mode-setup" }.Select(id => (id, Entry(id).Row)));
        Assert.Equal(ConversionParts.Workflows, Entry("auto-lander").Part);
        Assert.All(new[] { "solicited-older-host", "solicited-old-guards", "say-now-field" }, id => Assert.NotNull(Entry(id).Waits));
    }

    /// <summary>
    /// CONTEXTONE1c's retired codes (D230's note): the account's four sections and a chat's routing note, written no more and kept declared
    /// for the records that hold them, are each an entry that waits out CONTEXTONE1c's build, ended by no file of the home, deleted by its row.
    /// Each of the account's kept sites is the declaration of one of the four.
    /// </summary>
    [Fact]
    public void The_codes_the_standing_block_retired_are_registered_with_their_row()
    {
        foreach (var id in new[] { "handed-before-block", "chat-told-routing" })
        {
            Assert.Equal("ONEWAY1af", Entry(id).Row);
            Assert.NotNull(Entry(id).Waits);
            Assert.False(Entry(id).Ended(_home), id);
        }

        var driver = Path.Combine(WorkspaceRoot.Folder, "src", "Daoris.Desktop", "Daoris.Desktop.Driver");
        var declared = Entry("handed-before-block").Kept.Split(", ").Select(site =>
        {
            var lines = File.ReadAllLines(Path.Combine(driver, site[..site.IndexOf(':')].Replace('/', Path.DirectorySeparatorChar)));
            var at = int.Parse(site[(site.IndexOf(':') + 1)..], System.Globalization.CultureInfo.InvariantCulture);
            Assert.Contains("transitional: handed-before-block", lines[at - 1]);
            return lines[at].Trim();
        });
        Assert.Equal(
            [$"public const string Standing = \"{HandedSections.Standing}\";", $"public const string Map = \"{HandedSections.Map}\";",
                $"public const string Look = \"{HandedSections.Look}\";", $"public const string Indexes = \"{HandedSections.Indexes}\";"],
            declared);
        Assert.Contains("transitional: chat-told-routing", File.ReadAllLines(Path.Combine(driver, "Events", "NoteCodes.cs"))[774]);
        Assert.Contains(NoteCodes.ChatToldRouting.Code, File.ReadAllLines(Path.Combine(driver, "Events", "NoteCodes.cs"))[775]);
    }
}
