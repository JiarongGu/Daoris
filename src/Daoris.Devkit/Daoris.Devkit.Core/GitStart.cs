using System.Diagnostics;
using System.Text;

namespace Daoris.Devkit;

/// <summary>
/// The devkit's git, started past Git for Windows' launcher (GITSTART3): every git the devkit runs starts here. A
/// start is pointed at the git this process's <c>PATH</c> finds, by its whole path, and where that is the launcher, at
/// the real git behind it with the launcher's variables (<see cref="GitForWindows.Direct"/>). One process per git where
/// the launcher made two, and the sensitive gate's staged scan starts one git per staged file.
/// </summary>
/// <remarks>
/// A <c>PATH</c> that finds no git, or finds a <c>git.cmd</c> or <c>git.bat</c>, starts as before, through
/// <see cref="Process.Run"/>'s own lookup. The lookup is kept while this process's <c>PATH</c> is the one it was made on.
/// </remarks>
public static class GitStart
{
    private sealed record Found(string? Path, string? Git);

    private static Found? _found;

    /// <summary>Run git with <paramref name="arguments"/> and capture what it said, as <see cref="Process.Run"/> does.</summary>
    /// <param name="environment">Variables set for this git alone, over the devkit's own.</param>
    public static ProcessOutput Run(
        IReadOnlyList<string> arguments,
        string workingDirectory,
        IReadOnlyDictionary<string, string>? environment = null)
    {
        if (System() is not { } git) return Process.Run("git", arguments, workingDirectory, environment);
        return Process.Run(Past(Process.Captured(git, arguments, workingDirectory, environment)));
    }

    /// <summary>
    /// Point <paramref name="info"/> at the system's git, past the launcher, where it names <c>git</c> bare. Called after
    /// the start's own variables are set: the launcher reads <c>HOME</c> and <c>PATH</c> from the environment it is
    /// given, and so does this. A start naming any other file is pointed past the launcher only where it names it.
    /// </summary>
    public static ProcessStartInfo Past(ProcessStartInfo info)
    {
        if (info.FileName == "git")
        {
            if (System() is not { } git) return info;
            info.FileName = git;
        }

        GitForWindows.Direct(info);
        return info;
    }

    /// <summary>The git <c>PATH</c> finds as a program the devkit starts itself, or null where it finds none.</summary>
    private static string? System()
    {
        var path = Environment.GetEnvironmentVariable("PATH");
        var found = Volatile.Read(ref _found);
        if (found is null || found.Path != path)
        {
            var located = Process.Locate("git");
            found = new Found(path, located is not null && !Process.IsCommandScript(located) ? located : null);
            Volatile.Write(ref _found, found);
        }

        return found.Git;
    }
}

/// <summary>
/// Git for Windows' launcher, started past (GITSTART1; the devkit's twin of the driver's <c>GitForWindows</c>, GITSTART3):
/// the <c>git.exe</c> in an install's <c>cmd</c> folder is a launcher that starts <c>&lt;top&gt;\&lt;prefix&gt;\bin\git.exe</c>
/// as a second process. Where the system's git is that launcher, the real git starts directly, handed the environment
/// the launcher would have handed it.
/// </summary>
/// <remarks>
/// <para>🔴 A twin, not a reference: the devkit ships as its own binary and references nothing of the desktop's. The
/// driver's <c>Platform/GitForWindows.cs</c> is the source, and a change to the launcher's rule there is made here too;
/// <c>GitForWindowsTests</c> in each holds the same rows. The variables are what the launcher was measured setting (git
/// 2.53.0.windows.3; FIX-LOG, GITSTART1). git started bare with any <c>MSYSTEM</c> set repairs nothing of its
/// <c>PATH</c>, so <c>sh</c>, and with it every hook and <c>!</c> alias, would go unfound; so the start sets them all.</para>
/// <para>Recognised only where the layout is unmistakable: a <c>git.exe</c> in a <c>cmd</c> folder, a git under a known
/// prefix beside it, and <c>usr\bin</c>. An install with an <c>etc\git-bash.config</c>, whose <c>MSYS=</c> lines the
/// launcher reads, starts through the launcher as before, as does any other layout and any other platform.</para>
/// </remarks>
public static class GitForWindows
{
    /// <summary>The folders under an install's top that hold its git, in the order looked for.</summary>
    public static readonly IReadOnlyList<string> Prefixes = ["mingw64", "clangarm64", "mingw32"];

    /// <summary>
    /// The longest <c>HOME</c> whose <c>bin</c> the launcher puts on <c>PATH</c>: its <c>MAX_PATH</c> is 4,096, not
    /// Windows' 260, so a <c>HOME</c> of 4,088 characters still does and 4,089 does not (measured).
    /// </summary>
    private const int LongestHome = 4088;

    /// <summary>
    /// The longest <c>PATH</c> this start hands git: the largest value Windows sets in a variable, 32,767 characters with
    /// its terminator. A start whose <c>PATH</c> would be longer keeps the launcher, exactly as before.
    /// </summary>
    public const int LongestVariable = 32766;

    /// <summary>The real git behind the launcher at <paramref name="file"/>, or null where it is not one.</summary>
    public static GitBehind? Behind(string file)
    {
        if (!OperatingSystem.IsWindows() || !Path.IsPathRooted(file)) return null;
        if (!string.Equals(Path.GetFileName(file), "git.exe", StringComparison.OrdinalIgnoreCase)) return null;
        var folder = Path.GetDirectoryName(Path.GetFullPath(file));
        if (folder is null || !string.Equals(Path.GetFileName(folder), "cmd", StringComparison.OrdinalIgnoreCase)) return null;
        var top = Path.GetDirectoryName(folder);
        if (top is null
            || !Directory.Exists(Path.Combine(top, "usr", "bin"))
            || File.Exists(Path.Combine(top, "etc", "git-bash.config")))
        {
            return null;
        }

        foreach (var prefix in Prefixes)
        {
            var git = Path.Combine(top, prefix, "bin", "git.exe");
            if (File.Exists(git)) return new GitBehind(top, prefix, git);
        }

        return null;
    }

    /// <summary>
    /// The variables the launcher sets for its child, read from the environment it was started with: <c>MSYSTEM</c> named
    /// for its prefix over any; <c>PLINK_PROTOCOL</c> <c>ssh</c> where unset; <c>HOME</c> where unset, from
    /// <c>HOMEDRIVE</c> and <c>HOMEPATH</c> where that is a folder other than the system's, else <c>USERPROFILE</c>; and
    /// its two folders and <c>HOME</c>'s <c>bin</c> before <c>PATH</c>. A variable set empty counts as set.
    /// </summary>
    /// <param name="variable">A variable's value in that environment; null where it is not set.</param>
    public static IReadOnlyDictionary<string, string> ChildEnvironment(GitBehind behind, Func<string, string?> variable)
    {
        var child = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["MSYSTEM"] = behind.Prefix.ToUpperInvariant(),
        };
        if (variable("PLINK_PROTOCOL") is null) child["PLINK_PROTOCOL"] = "ssh";

        var home = variable("HOME");
        if (home is null)
        {
            if (variable("HOMEPATH") is { } homePath)
            {
                var candidate = (variable("HOMEDRIVE") ?? "") + homePath;
                if (Directory.Exists(candidate) && !IsSystemFolder(candidate)) home = candidate;
            }

            home ??= variable("USERPROFILE");
            if (home is not null) child["HOME"] = home;
        }

        var path = new StringBuilder()
            .Append(Path.Combine(behind.Top, behind.Prefix, "bin")).Append(';')
            .Append(Path.Combine(behind.Top, "usr", "bin")).Append(';');
        // Where nothing names a HOME the launcher still appends "\bin", as it does for one set empty (measured).
        if ((home?.Length ?? 0) <= LongestHome) path.Append(home is not null && home.EndsWith('\\') ? home + "bin" : home + "\\bin").Append(';');
        child["PATH"] = path.Append(variable("PATH") ?? "").ToString();
        return child;
    }

    /// <summary>
    /// Start the real git where <paramref name="info"/> names the launcher, handed the launcher's variables over the
    /// environment <paramref name="info"/> already holds; true where it now does. Any other start is left as it was, and
    /// so is one whose <c>PATH</c> would pass <see cref="LongestVariable"/>.
    /// </summary>
    public static bool Direct(ProcessStartInfo info)
    {
        if (Behind(info.FileName) is not { } behind) return false;
        var environment = info.Environment;
        var child = ChildEnvironment(behind, key => environment.TryGetValue(key, out var held) ? held : null);
        if (child["PATH"].Length > LongestVariable) return false;
        foreach (var (name, value) in child) environment[name] = value;

        info.FileName = behind.Git;
        return true;
    }

    private static bool IsSystemFolder(string folder)
    {
        try
        {
            return string.Equals(
                Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder)),
                Path.TrimEndingDirectorySeparator(Environment.SystemDirectory),
                StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }
}

/// <summary>A Git for Windows install's real git: the install's top folder, the prefix its git is under, and the file.</summary>
public sealed record GitBehind(string Top, string Prefix, string Git);
