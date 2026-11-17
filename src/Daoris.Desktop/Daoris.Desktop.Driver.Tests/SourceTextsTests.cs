namespace Daoris.Desktop.Driver.Tests;

/// <summary>The run's one scan of the sources: two censuses that read the same files read each from disk once.</summary>
[Trait(Category.Name, Category.Census)]
public sealed class SourceTextsTests
{
    [Fact]
    public void Two_censuses_in_one_run_read_each_driver_file_once()
    {
        var first = SourceCensus.Driver();
        var second = SourceCensus.Driver();
        var third = SourceFiles.All(SourceFiles.DriverProject, "*.cs").Select(SourceTexts.Of).ToList();

        Assert.Equal(first.Count, second.Count);
        Assert.Equal(first.Count, third.Count);
        Assert.Same(first[0].Source, second[0].Source);
        var driver = SourceFiles.All(SourceFiles.DriverProject, "*.cs").Select(Path.GetFullPath).ToList();
        Assert.NotEmpty(driver);
        Assert.All(driver, path => Assert.Equal(1, SourceTexts.DiskReads[path]));
    }

    [Fact]
    public void A_folder_is_walked_once_and_a_text_is_stripped_once()
    {
        Assert.Same(SourceFiles.All(SourceFiles.DriverProject, "*.cs"), SourceFiles.All(SourceFiles.DriverProject, "*.cs"));
        var source = SourceTexts.Of(SourceFiles.All(SourceFiles.DriverProject, "*.cs").First());
        Assert.Same(SourceTexts.Code(source), SourceTexts.Code(source));
    }
}
