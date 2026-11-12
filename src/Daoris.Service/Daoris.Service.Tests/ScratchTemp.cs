using System.Runtime.CompilerServices;

namespace Daoris.Service.Tests;

/// <summary>
/// The test process's temporary folder is the checkout's scratch, <c>_fixtures/test-homes/temp</c>, set as the assembly loads: a
/// test's <c>Path.GetTempPath()</c>, the service's own temporary files under test and every process a test starts land under the
/// gitignored scratch the repository's no-OS-temp rule names, never the machine's temporary folder. The desktop's tests name
/// <c>TestHomes.Root</c> instead and refuse the call outright; these suites root their scratch at the call, so the call is what
/// moves. Linked into the HTTP host's and the devkit's tests.
/// </summary>
internal static class ScratchTemp
{
    [ModuleInitializer]
    internal static void Set()
    {
        var folder = Path.Combine(Checkout(AppContext.BaseDirectory), "_fixtures", "test-homes", "temp");
        Directory.CreateDirectory(folder);
        foreach (var name in (string[])["TMP", "TEMP", "TMPDIR"]) Environment.SetEnvironmentVariable(name, folder);
    }

    /// <summary>The nearest folder at or above <paramref name="start"/> with a <c>.git</c>: a directory, or a linked worktree's file.</summary>
    private static string Checkout(string start)
    {
        var directory = new DirectoryInfo(start);
        while (directory is not null && !Path.Exists(Path.Combine(directory.FullName, ".git"))) directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException($"{start} lies in no checkout");
    }
}
