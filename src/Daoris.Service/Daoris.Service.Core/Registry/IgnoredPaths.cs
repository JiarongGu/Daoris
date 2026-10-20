using System.Diagnostics;
using System.Text;

namespace Daoris.Knowledge;

/// <summary>What git ignores in a checkout, by git's own answer (KNOWWALK1).</summary>
/// <remarks>
/// <para><b>Git's answer, not a list of names.</b> One <c>git ls-files --others --ignored --exclude-standard --directory</c>
/// names each ignored folder once, with a trailing slash, and never what is in it, since git decides a folder is ignored
/// from its name and does not open it; an ignored file outside one is named alone. What a repository ignores is its own
/// statement of what is not its work (<c>_fixtures/</c>, <c>local/</c>, <c>node_modules/</c>, a build's <c>bin/</c> and
/// <c>obj/</c>, <c>.claude/worktrees/</c>), so a reader that asks git reads none of it and a repository with names of its
/// own needs no list here.</para>
///
/// <para><b>The cost</b> is one process, a tenth to a third of a second on a busy machine for this repository (measured,
/// KNOWWALK1), against the one to two seconds a read of the checkout takes: asked once per read, never per folder.</para>
///
/// <para><b>Where git cannot answer, nothing is ignored</b>: a folder that is no checkout, a git that is not installed, an
/// answer that does not come in time. The read is then the walk it was before; the answer narrows a read and never widens
/// what is missed.</para>
/// </remarks>
public sealed class IgnoredPaths
{
    /// <summary>Nothing ignored: a folder that is no git checkout, or one git did not answer for.</summary>
    public static readonly IgnoredPaths None = new([]);

    /// <summary>How long git is waited for before the read goes on as though nothing were ignored.</summary>
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(10);

    /// <summary>A path's case as the disk compares it: Windows and macOS fold it, Linux does not.</summary>
    private static readonly StringComparison Comparison =
        OperatingSystem.IsLinux() ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

    private readonly string[] _folders;
    private readonly HashSet<string> _files;

    private IgnoredPaths(IReadOnlyList<string> entries)
    {
        Entries = entries;
        _folders = entries.Where(entry => entry.EndsWith('/')).ToArray();
        _files = new HashSet<string>(
            entries.Where(entry => !entry.EndsWith('/')),
            Comparison == StringComparison.Ordinal ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>What git named, repository-relative with forward slashes, a folder with a trailing slash.</summary>
    public IReadOnlyList<string> Entries { get; }

    /// <summary>
    /// Asks git once what it ignores in <paramref name="root"/>, under the repository-relative folders named or everywhere;
    /// <see cref="None"/> where the root is no checkout of its own or git does not answer.
    /// </summary>
    public static IgnoredPaths Of(string root, params string[] under)
    {
        // A checkout's own: its `.git` folder, or the file a worktree has there. Never a repository above it, which git would
        // otherwise find by walking up and answer for, at paths that are not this root's.
        var dotGit = Path.Combine(root, ".git");
        if (!Directory.Exists(dotGit) && !File.Exists(dotGit)) return None;

        try
        {
            using var git = Process.Start(Start(root, under));
            if (git is null) return None;
            var answer = git.StandardOutput.ReadToEndAsync();
            var said = git.StandardError.ReadToEndAsync();
            if (!git.WaitForExit(Patience))
            {
                try
                {
                    git.Kill(entireProcessTree: true);
                }
                catch (InvalidOperationException)
                {
                    // It ended between the wait and the kill.
                }

                return None;
            }

            git.WaitForExit();
            _ = said.Result;
            return git.ExitCode == 0 ? Parse(answer.Result) : None;
        }
        catch (Exception error) when (error is System.ComponentModel.Win32Exception or IOException or InvalidOperationException)
        {
            return None;
        }
    }

    /// <summary>The start of the one git <see cref="Of"/> asks, past Git for Windows' launcher where that is the git found.</summary>
    /// <param name="path">The <c>PATH</c> the start is handed; this process's own where null, as it always is outside a test.</param>
    internal static ProcessStartInfo Start(string root, IReadOnlyList<string> under, string? path = null)
    {
        var start = new ProcessStartInfo("git")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
        };
        foreach (var argument in new[] { "-C", root, "ls-files", "-z", "--others", "--ignored", "--exclude-standard", "--directory", "--" })
        {
            start.ArgumentList.Add(argument);
        }
        foreach (var folder in under) start.ArgumentList.Add(folder);
        foreach (var variable in new[] { "GIT_DIR", "GIT_WORK_TREE", "GIT_INDEX_FILE", "GIT_COMMON_DIR" }) start.Environment.Remove(variable);
        start.Environment["GIT_CEILING_DIRECTORIES"] = Path.GetDirectoryName(Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, '/')) ?? string.Empty;
        if (path is not null) start.Environment["PATH"] = path;
        // GITSTART2: one process where the launcher made two, with the variables it would have set, read after this
        // start's own are; any other git still starts by its bare name.
        GitForWindows.Past(start);
        return start;
    }

    /// <summary>Reads git's <c>ls-files -z</c> answer: paths separated by a zero byte, each folder with a trailing slash.</summary>
    internal static IgnoredPaths Parse(string answer) =>
        new(answer.Split('\0', StringSplitOptions.RemoveEmptyEntries));

    /// <summary>Whether a repository-relative path, a file or a folder with a trailing slash, is ignored or lies in an ignored folder.</summary>
    public bool Covers(string relative)
    {
        if (_files.Contains(relative)) return true;
        foreach (var folder in _folders)
        {
            if (relative.StartsWith(folder, Comparison)) return true;
        }

        return false;
    }

    /// <summary>Whether an ignored folder or file lies within the repository-relative folder.</summary>
    public bool AnyWithin(string folder)
    {
        var prefix = folder.TrimEnd('/') + "/";
        return Entries.Any(entry => entry.StartsWith(prefix, Comparison));
    }
}
