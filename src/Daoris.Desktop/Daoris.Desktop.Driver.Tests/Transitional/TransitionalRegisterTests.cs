using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// ONEWAY1ad-i (D228; the oneway design §1.3): the register is a plain list of entries, each with the site it keeps and the row that deletes
/// it. <c>tools/transitional-markers.mjs</c> holds the markers against the same list. D232: what is left is the old workflow logic's and the
/// handed sections the page still names, which their own rows delete with the list.
/// </summary>
[Trait(Category.Name, Category.Census)]
public sealed class TransitionalRegisterTests
{
    private static TransitionalEntry Entry(string id) => TransitionalRegister.Entries.Single(entry => entry.Id == id);

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

    /// <summary>ONEWAY1ad-ii: what is left of WORKFLOWCLEAN1 §4.7's kept reads is registered, each with its row.</summary>
    [Fact]
    public void The_workflow_waves_kept_reads_are_registered()
    {
        Assert.Equal(
            [("current-followed", "WORKFLOWCLEAN1z"), ("auto-lander", "WORKFLOWCLEAN1z"), ("work-cycles", "WORKFLOWCLEAN1j")],
            new[] { "current-followed", "auto-lander", "work-cycles" }.Select(id => (id, Entry(id).Row)));
    }
}
