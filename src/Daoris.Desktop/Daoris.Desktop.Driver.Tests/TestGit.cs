using System.Diagnostics;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The git a test's own fixture starts (GITSTART1): a start that names <c>git</c> bare is pointed at the git this
/// process's <c>PATH</c> finds, by its whole path, and past Git for Windows' launcher where that is what <c>PATH</c>
/// finds, with the launcher's variables (<see cref="GitForWindows.Direct"/>), as Daoris's own git starts
/// (<c>WorkingTree.GitStart</c>). One process per git where the launcher made two.
/// </summary>
/// <remarks>
/// Called just before the start, after the start's own variables are set: the launcher reads <c>HOME</c> and
/// <c>PATH</c> from the environment it is given, and so does this. A start that names any other file is left as it was.
/// The lookup is kept while this process's <c>PATH</c> is the one it was made on.
/// </remarks>
internal static class TestGit
{
    private sealed record Found(string? Path, string? Git);

    private static Found? _found;

    /// <summary>Point <paramref name="info"/> at the system's git, past the launcher, where it names <c>git</c> bare.</summary>
    public static ProcessStartInfo Past(ProcessStartInfo info)
    {
        if (info.FileName != "git" || System() is not { } git) return info;
        info.FileName = git;
        GitForWindows.Direct(info);
        return info;
    }

    private static string? System()
    {
        var path = Environment.GetEnvironmentVariable("PATH");
        var found = Volatile.Read(ref _found);
        if (found is null || found.Path != path)
        {
            found = new Found(path, CommandPresence.Resolve("git", path, startable: true));
            Volatile.Write(ref _found, found);
        }

        return found.Git;
    }
}
