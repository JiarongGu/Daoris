namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The doors class's census seen failing: the driver's start census (<see cref="GitLaunchTests"/>, every file that starts a process, each
/// the seam it is) run over the driver as it is with each listed file left out in turn names that file, and a planted site starting a
/// variable file through the child runner, as a plugin's declared tool is started, is named. The gate's half, a git with no place refused
/// at every purpose of Daoris's own, is <see cref="GitSchedulerTests"/>'.
/// </summary>
[Trait(Category.Name, Category.Census)]
public sealed class GitStartsCensusTests
{
    private const string What = "starts a process";

    private static SortedSet<string> Found(IEnumerable<(string File, string Source)> sources) => GitLaunchTests.StartFiles(sources);

    [Fact]
    public void Each_starting_file_left_out_of_the_list_turns_the_census_red_naming_it()
    {
        var found = Found(SourceCensus.Driver());

        Assert.Empty(SourceCensus.Problems(found, GitLaunchTests.StartingFiles, What));
        Assert.Empty(SourceCensus.Unproved(found, GitLaunchTests.StartingFiles, What));
    }

    [Fact]
    public void A_planted_site_starting_a_variable_file_turns_the_census_red_naming_it()
    {
        var planted = SourceCensus.Driver().Append(("PlantedToolRuns.cs",
            "internal static class PlantedToolRuns\n{\n    public static Task<ChildRun> RunAsync(string file, CancellationToken ct) =>\n"
            + "        ChildProcess.RunAsync(new ProcessStartInfo(file), TimeSpan.FromSeconds(5), ct, closeInput: true);\n}\n"));

        Assert.Equal(["PlantedToolRuns.cs starts a process and is not listed"], SourceCensus.Problems(Found(planted), GitLaunchTests.StartingFiles, What));
    }
}
