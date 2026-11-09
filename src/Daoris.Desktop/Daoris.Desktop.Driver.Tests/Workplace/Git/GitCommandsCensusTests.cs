using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The table kept total at test time: every git the driver's sources start names a command, and its verb where the row reads one, by a
/// literal with a row; the tests run the gate strict, so a start the sources reach some other way and the table does not row fails the test
/// that starts it. At run time nothing is refused for want of a row.
/// </summary>
[Trait(Category.Name, Category.Census)]
public sealed class GitCommandsCensusTests : IDisposable
{
    // Each hands on a list its caller built, which this census reads at that caller or the strict run reads as it starts.
    private static readonly string[] PassedOn =
    [
        "arguments", "Ask", "ask", "BranchAsk", "commit", "Layout", "line", "OriginHeadAsk", "removeArgs", "ReviewChoices.ConventionalAsk(\"main\")",
        "ReviewChoices.ConventionalAsk(\"master\")", "ReviewChoices.ConventionalAsk(candidate)", "Root", "step", "whole", "WorkingTree.BranchAsk",
        "WorkingTree.OriginHeadAsk", "WorkingTree.UncommittedStatus",
    ];

    private readonly string _root = Path.Combine(TestHomes.Root, "daoris-census-" + Guid.NewGuid().ToString("N")[..8]);

    public GitCommandsCensusTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private static List<string> Problems(IEnumerable<(string File, string Source)> sources)
    {
        var problems = new List<string>();
        foreach (var site in GitStartSites.In(sources))
        {
            if (site.Passed is { } passed && !PassedOn.Contains(passed)) problems.Add($"{site.File} starts git with `{passed}`, a list the census cannot read");
            foreach (var list in site.Lists)
            {
                var (command, next, held) = GitStartSites.Named(list);
                if (command is null && held is not null) problems.Add($"{site.File} starts git with its command or verb held in `{held}`");
                else if (command is null) continue;
                else if (held is not null && GitCommands.HasVerbs(command)) problems.Add($"{site.File} starts git with its command or verb held in `{held}`");
                else if (!GitCommands.Rowed(command, next)) problems.Add($"{site.File} starts `git {command}{(next is null ? "" : " " + next)}`, which has no row");
            }
        }

        return [.. problems.Distinct().Order(StringComparer.Ordinal)];
    }

    [Fact]
    public void Every_git_the_sources_start_names_a_rowed_command_by_a_literal()
    {
        var sites = GitStartSites.In(SourceCensus.Driver());
        Assert.True(sites.Count > 300, $"the census read {sites.Count} starts, too few to be the sources'");
        var problems = Problems(SourceCensus.Driver());
        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    [Fact]
    public void A_planted_command_with_no_row_turns_the_census_red()
    {
        var planted = SourceCensus.Driver().Append(("PlantedFrob.cs",
            "internal static class PlantedFrob\n{\n    public static Task<(int, string, string)> RunAsync(string root, CancellationToken ct) =>\n"
            + "        WorkingTree.GitAsync(root, [\"frobnicate\", \"--all\"], ct);\n}\n"));
        Assert.Contains("PlantedFrob.cs starts `git frobnicate`, which has no row", Problems(planted));
    }

    [Fact]
    public void A_planted_command_held_in_a_variable_turns_the_census_red()
    {
        var planted = SourceCensus.Driver().Append(("PlantedHeld.cs",
            "internal static class PlantedHeld\n{\n    public static Task<(int, string, string)> RunAsync(string root, string verb, CancellationToken ct) =>\n"
            + "        WorkingTree.GitAsync(root, [\"-c\", \"core.quotepath=false\", verb, \"--all\"], ct);\n}\n"));
        Assert.Contains("PlantedHeld.cs starts git with its command or verb held in `verb`", Problems(planted));
    }

    [Fact]
    public void A_maintenance_verb_writing_the_persons_own_scheduler_has_no_row()
    {
        Assert.True(GitCommands.Rowed("maintenance", "run"));
        Assert.False(GitCommands.Rowed("maintenance", "register"));
        Assert.False(GitCommands.Rowed("maintenance", "start"));
    }

    [Fact]
    public async Task The_strict_run_fails_a_start_whose_command_has_lost_its_row()
    {
        await GitFixture.GitAsync(_root, "init", "-q", "-b", "main");
        Assert.True(GitStartGate.Strict);
        using (GitCommands.Without("notes", "add"))
        {
            var (code, _, said) = await WorkingTree.GitAsync(_root, ["notes", "add", "-m", "n"], CancellationToken.None);
            Assert.Equal(-1, code);
            Assert.Contains("has no row", said, StringComparison.Ordinal);
        }

        var (listed, _, _) = await WorkingTree.GitAsync(_root, ["notes", "list"], CancellationToken.None);
        Assert.Equal(0, listed);
    }
}
