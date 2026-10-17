using System.Diagnostics;
using System.Text;

namespace Daoris.Driver;

/// <summary>
/// Git for Windows' launcher, started past (GITSTART1): the <c>git.exe</c> in an install's <c>cmd</c> folder is a
/// launcher that starts <c>&lt;top&gt;\&lt;prefix&gt;\bin\git.exe</c> as a second process, and a start costs 100–700 ms
/// on a loaded machine (PROC1a). Where the system's git is that launcher, the real git starts directly, handed the
/// environment the launcher would have handed it.
/// </summary>
/// <remarks>
/// <para>🔴 The launcher's variables are what it was measured setting (git 2.53.0.windows.3; FIX-LOG, GITSTART1), held to
/// a real install by <c>GitForWindowsProcessTests</c>. git started bare with no <c>MSYSTEM</c> repairs its own
/// <c>PATH</c>, but in another order (<c>HOME\bin</c> first), and with any <c>MSYSTEM</c> set it repairs nothing, so
/// <c>sh</c>, and with it every hook and <c>!</c> alias, would go unfound; so the start sets them all.</para>
/// <para>Recognised only where the layout is unmistakable: a <c>git.exe</c> in a <c>cmd</c> folder, a git under a known
/// prefix beside it, and <c>usr\bin</c>. An install with an <c>etc\git-bash.config</c>, whose <c>MSYS=</c> lines the
/// launcher reads, starts through the launcher as before, as does any other layout and any other platform.</para>
/// </remarks>
public static class GitForWindows
{
    /// <summary>The folders under an install's top that hold its git, in the order looked for.</summary>
    public static readonly IReadOnlyList<string> Prefixes = ["mingw64", "clangarm64", "mingw32"];

    /// <summary>The longest <c>HOME</c> whose <c>bin</c> the launcher puts on <c>PATH</c>: its buffer is 260 characters.</summary>
    private const int LongestHome = 252;

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
    /// environment <paramref name="info"/> already holds (the tools' <c>PATH</c> included); true where it now does.
    /// Any other start is left as it was.
    /// </summary>
    public static bool Direct(ProcessStartInfo info)
    {
        if (Behind(info.FileName) is not { } behind) return false;
        var environment = info.Environment;
        foreach (var (name, value) in ChildEnvironment(behind, key => environment.TryGetValue(key, out var held) ? held : null))
        {
            environment[name] = value;
        }

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
