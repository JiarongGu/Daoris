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

    [Fact]
    public void The_actor_key_ends_when_every_role_holds_a_stored_entry()
    {
        Assert.False(Entry("actor-key-before").Ended(_home));
        Put(HomeEntry.DriverConfig, """{"actors":{"build":[],"chat":[]}}""");
        Assert.False(Entry("actor-key-before").Ended(_home));
        Put(HomeEntry.DriverConfig, """{"actors":{"build":[],"chat":[],"brainstorm":[],"intake":[],"ask-daoris":[]}}""");
        Assert.True(Entry("actor-key-before").Ended(_home));
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
}
