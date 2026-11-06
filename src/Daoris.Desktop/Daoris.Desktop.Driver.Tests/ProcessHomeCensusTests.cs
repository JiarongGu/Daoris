using System.Text.RegularExpressions;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// No unit of the driver's library reads the home from the process's environment but the sources named here, each with why. A unit
/// that acts for a home carries it, handed by its caller, or reads the home its program named at its entry (<c>ProgramHome</c>): the
/// variable a process inherited is the shell's, which in a test's or a tool's process is whatever the person's account holds, and a
/// unit reading it there reads and writes the person's install. Code alone is read: a word in a comment or a literal reads nothing.
/// </summary>
[Trait(Category.Name, Category.Census)]
public sealed class ProcessHomeCensusTests
{
    private static readonly Regex Reads = new(
        @"\bDaorisHome\.Resolve\(\s*\)|\bDaorisHome\.Resolve\(\s*Environment\.GetEnvironmentVariable\b|\bGetEnvironmentVariable\(\s*DaorisHome\.Variable\s*\)",
        RegexOptions.CultureInvariant);

    /// <summary>Each source of the library that reads the process's home, and why.</summary>
    private static readonly Dictionary<string, string> Kept = new(StringComparer.Ordinal)
    {
        ["Agents/Terminals.cs"] = "the window's terminal offers the shells of the home the window runs on",
        ["Agents/Transports/Shell.cs"] = "a start whose request names no home (a probe, a usage question, an agent's action) takes the process's",
        ["Platform/MachineLog.cs"] = "a program's own log, opened by its entry once the home is adopted",
        ["Platform/PseudoConsole.cs"] = "a terminal whose launch names no home of its own takes the window's",
        ["Plugins/Relay/RelayCommand.cs"] = "the events relay is an entry of its own, routed before the host adopts a home",
        ["Workplace/GitHostKeys.cs"] = "a host's published keys asked with no home read the list beside the process's",
        ["Workplace/SessionGitWiring.cs"] = "a session's git wiring asked by a start whose request names no home",
    };

    internal static int Reading(string text) => Reads.Matches(StructureCheck.CodeOf(text)).Count;

    private static SortedSet<string> Scan()
    {
        var folder = SourceFiles.Project(SourceFiles.DriverProject);
        var found = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var path in SourceFiles.All(SourceFiles.DriverProject, "*.cs"))
        {
            if (Reading(File.ReadAllText(path)) > 0) found.Add(Path.GetRelativePath(folder, path).Replace(Path.DirectorySeparatorChar, '/'));
        }

        return found;
    }

    [Fact]
    public void No_unit_reads_the_process_s_home_but_the_sources_named_here()
    {
        var reading = Scan().Where(file => !Kept.ContainsKey(file)).ToList();

        Assert.True(reading.Count == 0,
            "reads the home from the process's environment: carry the home the caller acts for, or read the program's named home "
            + $"(ProgramHome.Named), or name the source here with why: {string.Join(", ", reading)}");
    }

    [Fact]
    public void Each_source_named_here_still_reads_it()
    {
        var found = Scan();
        var stale = Kept.Keys.Where(file => !found.Contains(file)).ToList();

        Assert.True(stale.Count == 0, $"named here but no longer reading the process's home, so delete its row: {string.Join(", ", stale)}");
    }

    [Fact]
    public void The_census_sees_the_calls_and_not_the_words()
    {
        Assert.Equal(1, Reading("var h = DaorisHome.Resolve();"));
        Assert.Equal(1, Reading("var h = DaorisHome.Resolve(Environment.GetEnvironmentVariable, here);"));
        Assert.Equal(1, Reading("var h = Environment.GetEnvironmentVariable(DaorisHome.Variable);"));
        Assert.Equal(0, Reading("var h = DaorisHome.Resolve(variable);"));
        Assert.Equal(0, Reading("// DaorisHome.Resolve() in a comment\nvar s = \"DaorisHome.Resolve()\";"));
    }
}
