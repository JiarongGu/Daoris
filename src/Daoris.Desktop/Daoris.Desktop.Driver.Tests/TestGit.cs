using System.Diagnostics;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The git a test's own fixture starts (GITSTART1): a start that names <c>git</c> bare is pointed at the git this
/// process's <c>PATH</c> finds, by its whole path, and past Git for Windows' launcher where that is what <c>PATH</c>
/// finds, with the launcher's variables (<see cref="GitForWindows.Direct"/>), as Daoris's own git starts
/// (<c>WorkingTree.GitStart</c>). One process per git where the launcher made two. Each such start is also capped
/// (<see cref="Ceiling"/>, GITISOLATE1) so a fixture that is no repository never climbs into the checkout's.
/// </summary>
/// <remarks>
/// Called just before the start, after the start's own variables are set: the launcher reads <c>HOME</c> and
/// <c>PATH</c> from the environment it is given, and so does this. A start that names any other file is left as it was.
/// The lookup is kept while this process's <c>PATH</c> is the one it was made on.
/// </remarks>
internal static class TestGit
{
    private sealed record Found(string? Path, string? Git);

    private const string CeilingVariable = "GIT_CEILING_DIRECTORIES";

    private static Found? _found;

    /// <summary>Point <paramref name="info"/> at the system's git, past the launcher, where it names <c>git</c> bare.</summary>
    public static ProcessStartInfo Past(ProcessStartInfo info)
    {
        Ceiling(info);
        if (info.FileName != "git" || System() is not { } git) return info;
        info.FileName = git;
        GitForWindows.Direct(info);
        return info;
    }

    /// <summary>
    /// Cap <paramref name="info"/>'s git at its fixture (GITISOLATE1): git looks for its repository in the working folder and each
    /// folder above it, and a fixture that is no repository (yet) sits under the checkout, whose repository it would act on, a
    /// worktree's config shared with every other. The ceiling is the parent of the fixture's repository, found from the working
    /// folder up but never past the checkout or the OS temp folder, or of the working folder itself where none is found; git
    /// still opens the working folder and the repository in it. A start that names its repository (<c>--git-dir</c>,
    /// <c>GIT_DIR</c>) climbs nowhere and is left as it is; a ceiling already on the start keeps its folders.
    /// </summary>
    public static ProcessStartInfo Ceiling(ProcessStartInfo info)
    {
        if (info.Environment.ContainsKey("GIT_DIR")) return info;
        if (NamesGitDir(info.ArgumentList)) return info;
        var folder = Path.GetFullPath(string.IsNullOrEmpty(info.WorkingDirectory) ? Environment.CurrentDirectory : info.WorkingDirectory)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var cap = Path.GetDirectoryName(Repository(folder) ?? folder);
        if (cap is null) return info;
        var held = info.Environment.TryGetValue(CeilingVariable, out var existing) ? existing : null;
        info.Environment[CeilingVariable] = string.IsNullOrEmpty(held) ? cap : held + Path.PathSeparator + cap;
        return info;
    }

    /// <summary>Whether git's own options, those before its command, include <c>--git-dir</c>: a command's <c>--git-dir</c> is its own.</summary>
    private static bool NamesGitDir(IList<string> arguments)
    {
        for (var at = 0; at < arguments.Count && arguments[at].StartsWith('-'); at++)
        {
            if (arguments[at] == "--git-dir" || arguments[at].StartsWith("--git-dir=", StringComparison.Ordinal)) return true;
            if (arguments[at] is "-c" or "-C") at++;
        }

        return false;
    }

    /// <summary>The nearest folder at or above <paramref name="folder"/> with a <c>.git</c>, stopping at the checkout and the OS temp folder.</summary>
    private static string? Repository(string folder)
    {
        var stops = new[] { Checkout(), Path.GetTempPath() }.Where(stop => stop is not null)
            .Select(stop => Path.GetFullPath(stop!).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)).ToList();
        for (var directory = new DirectoryInfo(folder); directory is not null; directory = directory.Parent)
        {
            if (stops.Contains(directory.FullName.TrimEnd(Path.DirectorySeparatorChar), StringComparer.OrdinalIgnoreCase)) return null;
            if (Path.Exists(Path.Combine(directory.FullName, ".git"))) return directory.FullName;
        }

        return null;
    }

    /// <summary>The checkout the tests run in: the nearest folder at or above the test host with a <c>.git</c>.</summary>
    private static string? Checkout()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (Path.Exists(Path.Combine(directory.FullName, ".git"))) return directory.FullName;
        }

        return null;
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
