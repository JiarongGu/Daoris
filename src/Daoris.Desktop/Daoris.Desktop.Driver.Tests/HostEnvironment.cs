using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The test host's own environment, set once as the assembly loads, before any test runs and so beside none: what the shell that started
/// it held of Daoris's and of git's locating variables is taken out, as the merge tool's gates take it (its <c>GATE_REMOVED</c>), and
/// the process's home is a scratch one of this run's.
/// </summary>
/// <remarks>
/// A spawn handed no home reads this process's (<see cref="SpawnRequest.Home"/>); the driver's own git runs and tools read the home a
/// program named (<see cref="ProgramHome"/>), which no test names, so theirs is the system's. Inherited
/// from a developer's shell, that home is the person's install's: a test's sessions make folders under it and its git reads the install's
/// profile, and a class that passes in that shell fails in the gate's, which names none. Here every run has a home, and never the
/// person's. The one variable of the process the suite sets outside a collection that runs alone (<see cref="ProcessEnvironmentCensusTests"/>).
/// </remarks>
internal static partial class HostEnvironment
{
    /// <summary>The suite's own switches, never Daoris's: the goldens' write and the real reader's run.</summary>
    private static readonly string[] SuiteSwitches = ["DAORIS_GOLDEN_WRITE", RealReaderTheoryAttribute.Variable];

    /// <summary>This run's home, under the checkout's scratch.</summary>
    public static string Home { get; } = Path.Combine(TestHomes.Root, "host-" + Environment.ProcessId);

    [ModuleInitializer]
    internal static void Set()
    {
        // Read before the removal below takes the switch out with the rest of Daoris's variables.
        var pinned = PinnedLeg.Apply();
        // The suite's own switches are never Daoris's, so they are the ones kept.
        foreach (var name in Environment.GetEnvironmentVariables().Keys.Cast<string>()
                     .Where(name => Removed().IsMatch(name) && !SuiteSwitches.Contains(name, StringComparer.OrdinalIgnoreCase))
                     .ToList())
        {
            Environment.SetEnvironmentVariable(name, null);
        }

        Directory.CreateDirectory(Home);
        Environment.SetEnvironmentVariable(DaorisHome.Variable, Home);
        if (pinned is not null) Environment.SetEnvironmentVariable("PATH", pinned + Path.PathSeparator + Environment.GetEnvironmentVariable("PATH"));
        AppDomain.CurrentDomain.ProcessExit += (_, _) =>
        {
            try { Directory.Delete(Home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        };
    }

    [GeneratedRegex(
        "^(DAORIS_.*|GIT_(DIR|WORK_TREE|INDEX_FILE|OBJECT_DIRECTORY|ALTERNATE_OBJECT_DIRECTORIES|COMMON_DIR|NAMESPACE|CEILING_DIRECTORIES|CONFIG.*))$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Removed();
}
