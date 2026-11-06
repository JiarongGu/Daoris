using System.Text.RegularExpressions;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// No test sets a variable of the process's environment or its current directory, but the sources named here, each with why, and each
/// run alone. The suite runs its classes at once (PROCPAR1): what one sets, every class beside it reads and every child they start
/// inherits, which failed classes that passed alone. A test hands the child it starts its own environment instead, or names its home
/// on the start; where the product itself reads the process's environment, the class runs in a collection that runs alone. Code alone
/// is read: a word in a comment or a literal sets nothing.
/// </summary>
[Trait(Category.Name, Category.Census)]
public sealed class ProcessEnvironmentCensusTests
{
    private static readonly Regex Sets = new(
        @"\bEnvironment\.SetEnvironmentVariable\b|\bDirectory\.SetCurrentDirectory\b|\bEnvironment\.CurrentDirectory\s*=(?!=)",
        RegexOptions.CultureInvariant);

    /// <summary>A collection that runs after the rest, one at a time: <see cref="Alone"/>, <see cref="ProcessPath"/>, and the agent ports'.</summary>
    private static readonly Regex RunsAlone = new(
        @"\[Collection\(\s*(?:Alone\.Name|ProcessPath\.Name|""agent operation ports"")\s*\)\]", RegexOptions.CultureInvariant);

    private const string OwnGitConfiguration =
        "the product's git inherits the process's environment, and the person's own configuration is kept from it by GIT_CONFIG_NOSYSTEM and GIT_CONFIG_GLOBAL";

    /// <summary>
    /// Each source that sets the process's environment: why, and the source whose class runs alone where it is a helper (null where its
    /// own class is, or for the host's own).
    /// </summary>
    private static readonly Dictionary<string, (string Why, string? AloneIn)> Kept = new(StringComparer.Ordinal)
    {
        ["HostEnvironment.cs"] = ("the test host's own, once as the assembly loads and before any test: the shell's Daoris and git variables out, a scratch home in", null),
        ["CommandShimTests.cs"] = ("the product resolves a command on the process's PATH, and the shim it finds is put first on it", null),
        ["Agents/HarnessRunTests.cs"] = ("the product resolves a harness on the process's PATH, and the stand-in it finds is put first on it", null),
        ["Review/HeavyToolsProcessTests.cs"] = ("the product resolves the heavy tools on the process's PATH, and the stand-ins it finds are put first on it", null),
        ["Agents/SpawnInheritedTests.cs"] = ("what a spawn inherits of the process's environment is its subject: the variables are planted there", null),
        ["Agents/ProviderConformanceCases.cs"] = ("the conformance cases of what a spawn inherits plant each provider's variables and the run's home in the process", "Agents/ProviderConformanceEnvironmentTests.cs"),
        ["Plugins/PluginHostTokenTests.cs"] = ("the token a plugin's start passes on is read from the process's environment", null),
        ["Review/IntegrationRunnerCallTests.cs"] = ("a variable of the process's is planted to show the runner's gate environment leaves it out", null),
        ["Roles/SessionsCommandRoleTerminalTests.cs"] = ("the role key a terminal command holds is read from the process's environment", null),
        ["Service/RemoteTargetTests.cs"] = ("the service's URL variable is read from the process's environment, and the editor's read must ignore it", null),
        ["Workplace/GitSessionHoldsTests.cs"] = ("what a session is kept from inheriting (the credential variables, gh's and az's folders) is planted in the process", null),
        ["Workplace/GitAuthorTests.cs"] = (OwnGitConfiguration, null),
        ["Workplace/GitCertificateHoldTests.cs"] = (OwnGitConfiguration, null),
        ["Workplace/GitDoorsFloorTests.cs"] = (OwnGitConfiguration, null),
        ["Workplace/GitMoveTests.cs"] = (OwnGitConfiguration, null),
        ["Workplace/GitRemoteRowsTests.cs"] = (OwnGitConfiguration, null),
        ["Workplace/GitLongPathsProcessTests.cs"] = (OwnGitConfiguration, null),
        ["Workplace/GitHistoryEditScopesTests.cs"] = ("each scope of git's configuration a history edit is read from (system, global, the environment's) is planted where git reads it", null),
        ["Workplace/GitHoldsProcessTests.cs"] = (OwnGitConfiguration + ", HOME too; and an install's home is named as the process's, which the ssh door is found by", null),
        ["Workplace/GitSshHoldsProcessTests.cs"] = (OwnGitConfiguration + ", HOME too; and GIT_SSH_COMMAND names the stand-in server's key", null),
    };

    internal static int Setting(string text) => Sets.Matches(StructureCheck.CodeOf(text)).Count;

    private static SortedDictionary<string, string> Scan()
    {
        var folder = SourceFiles.Project(SourceFiles.DriverTestsProject);
        var found = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var path in SourceFiles.All(SourceFiles.DriverTestsProject, "*.cs"))
        {
            var text = File.ReadAllText(path);
            if (Setting(text) > 0) found[Path.GetRelativePath(folder, path).Replace(Path.DirectorySeparatorChar, '/')] = text;
        }

        return found;
    }

    [Fact]
    public void No_test_sets_the_process_s_environment_but_the_sources_named_here()
    {
        var setting = Scan().Keys.Where(file => !Kept.ContainsKey(file)).ToList();

        Assert.True(setting.Count == 0,
            "sets the process's environment, which every class beside it shares: hand the child its own environment or name the home on the "
            + $"start, or name the source here with why and run it alone: {string.Join(", ", setting)}");
    }

    [Fact]
    public void Each_source_named_here_still_sets_it_and_runs_alone()
    {
        var found = Scan();
        var stale = Kept.Keys.Where(file => !found.ContainsKey(file)).ToList();
        var beside = Kept
            .Where(kept => kept.Key != nameof(HostEnvironment) + ".cs")
            .Where(kept => kept.Value.AloneIn is { } caller
                ? !RunsAlone.IsMatch(File.ReadAllText(Path.Combine(SourceFiles.Project(SourceFiles.DriverTestsProject), caller)))
                : found.TryGetValue(kept.Key, out var text) && !RunsAlone.IsMatch(text))
            .Select(kept => kept.Key)
            .ToList();

        Assert.True(stale.Count == 0, $"named here but no longer setting the process's environment, so delete its row: {string.Join(", ", stale)}");
        Assert.True(beside.Count == 0, $"sets the process's environment and runs beside other classes: {string.Join(", ", beside)}");
    }

    [Fact]
    public void The_census_sees_the_calls_and_not_the_words()
    {
        Assert.Equal(1, Setting("void M() => Environment.SetEnvironmentVariable(\"X\", \"1\");"));
        Assert.Equal(1, Setting("void M() => Directory.SetCurrentDirectory(\"x\");"));
        Assert.Equal(1, Setting("void M() { Environment.CurrentDirectory = \"x\"; }"));
        Assert.Equal(0, Setting("// Environment.SetEnvironmentVariable in a comment\nvar s = \"Environment.SetEnvironmentVariable\";"));
        Assert.Equal(0, Setting("var same = Environment.CurrentDirectory == \"x\";"));
    }
}
