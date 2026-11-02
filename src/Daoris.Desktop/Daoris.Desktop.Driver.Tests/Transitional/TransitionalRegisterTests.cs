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

    [Fact]
    public void The_fold_ends_when_the_file_holds_one_set_per_agent()
    {
        Put(HomeEntry.Harnesses, """{"workspaces":{"w":{"claude-code":["a"]}}}""");
        Assert.False(Entry("account-pool-fold").Ended(_home));
        Put(HomeEntry.Harnesses, """{"rotation":{"claude-code":["a"]}}""");
        Assert.True(Entry("account-pool-fold").Ended(_home));
    }
}
