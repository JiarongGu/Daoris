namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A census case's clock starts after what the censuses share is made (<see cref="CensusWarm"/>): the say door's scan, each read
/// source's comment-stripped text, the driver's compile and the reference map. A case that found one of them unmade would be charged
/// another case's wait for it, as six were in the merge that measured them at 68 to 79 s.
/// </summary>
[Trait(Category.Name, Category.Census)]
public sealed class CensusWarmTests
{
    [Fact]
    public void A_census_case_starts_after_the_shared_reading_is_made()
    {
        // Past its patience on a loaded machine the run's cases began beside the warm-up, each waiting for what it reads, as before it.
        if (CensusWarm.Late) return;
        Assert.True(SayDoorSources.Made, "the say door's sources were read by a case, not before it");
        Assert.All(SayDoorSources.All, source => Assert.True(SourceTexts.IsStripped(source.Text), $"{source.File}'s stripped text was made by a case, not before it"));
        Assert.True(DesktopCompilations.IsMade(SourceFiles.DriverProject), "the driver's compile was made by a case, not before it");
        Assert.True(ReferenceMap.Loaded, "the reference map was loaded by a case, not before it");
    }

    [Fact]
    public void Only_a_run_that_holds_a_census_case_warms()
    {
        Dictionary<string, List<string>> census = new() { [Category.Name] = [Category.Census] };
        Dictionary<string, List<string>> process = new() { [Category.Name] = [Category.Process] };

        Assert.True(CensusWarm.Wanted([process, census]));
        Assert.False(CensusWarm.Wanted([process, new Dictionary<string, List<string>>()]));
        Assert.False(CensusWarm.Wanted([]));
    }

    [Fact]
    public void A_late_warm_up_leaves_its_mark_for_the_time_ratchet_and_an_on_time_one_clears_it()
    {
        var folder = Path.Combine(TestHomes.Root, "census-warm-late-" + Guid.NewGuid().ToString("N")[..8]);
        var mark = Path.Combine(folder, CensusWarm.LateMark);
        try
        {
            CensusWarm.MarkLate(folder, true);
            Assert.True(File.Exists(mark));
            CensusWarm.MarkLate(folder, false);
            Assert.False(File.Exists(mark));
        }
        finally
        {
            if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void The_binding_begins_with_the_run_only_where_a_semantic_census_runs()
    {
        Assert.True(CensusWarm.Binds(["Daoris.Desktop.Driver.Tests.SayKeysCensusTests", "Daoris.Desktop.Driver.Tests.StructureTests"]));
        Assert.True(CensusWarm.Binds(["Daoris.Desktop.Driver.Tests.DeclarationLiteralsCensusTests"]));
        Assert.False(CensusWarm.Binds(["Daoris.Desktop.Driver.Tests.SayKeysCensusTests", "Daoris.Desktop.Driver.Tests.CensusWarmTests"]));
    }
}
