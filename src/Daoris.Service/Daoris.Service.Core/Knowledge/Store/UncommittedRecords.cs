using System.Diagnostics;
using System.Text;

namespace Daoris.Knowledge;

/// <summary>
/// Whether a checkout's record folders differ from the commit its <c>HEAD</c> names (KNOWFRESH1a-ii; the knowledge-freshness
/// design's §3.1): a hit's reading is "at that commit" only while the files it read are that commit's, and a search says
/// <c>+ uncommitted</c> where they are not.
/// </summary>
/// <remarks>
/// <para><b>One git status, asked once for a reading.</b> The newest write among the records says a root moved, never whether
/// it moved from its commit, and a <c>git log</c> a hit was cut for its cost; one <c>status</c> over the folders the scan reads
/// answers for every hit of the reading. It is asked as the reading is taken, so what changes during the read marks the next.</para>
///
/// <para><b>It never takes git's index lock</b> (<c>--no-optional-locks</c>, REVIEWLOCK1): a status refreshes the index's stat data and
/// writes it back where it can, and a session writing in the tree would then fail its own <c>git add</c> on a lock this read held.</para>
///
/// <para><b>Where git cannot answer, nothing is said</b>: a folder that is no checkout of its own, a git that is not installed, an
/// answer that does not come in time. That is null, which a hit shows as the commit alone and never as clean.</para>
/// </remarks>
public static class UncommittedRecords
{
    /// <summary>How long git is waited for before the answer is none.</summary>
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(10);

    /// <summary>The records a repository keeps at its top, which a status over its folders alone would not see.</summary>
    private static readonly string[] Top = [":(glob)*.md", ":(glob)*.json", ":(glob)*.lock"];

    /// <summary>
    /// Whether the records the scan reads in <paramref name="root"/> have changes git has not committed, new files git does not
    /// ignore included; null where git did not answer.
    /// </summary>
    /// <param name="documents">The documents folder the deployment names, relative to the root, or null.</param>
    /// <param name="index">The index folder the deployment names, relative to the root, or null.</param>
    public static bool? Of(string root, string? documents = null, string? index = null) =>
        Status(root, [.. Folders(root, documents, index), .. Top]);

    /// <summary>
    /// Whether one record file of <paramref name="root"/> differs from the commit its <c>HEAD</c> names, a new file git does not
    /// ignore included (KNOWCENTRAL2a): a citation's recheck asks of the cited file alone, so another file's edit changes nothing.
    /// Null where git did not answer.
    /// </summary>
    /// <param name="relativePath">The file, repository-relative with forward slashes, as an entry names it.</param>
    public static bool? OfFile(string root, string relativePath) => Status(root, [$":(literal){relativePath}"]);

    private static bool? Status(string root, IReadOnlyList<string> specs)
    {
        // A checkout's own: its `.git` folder, or the file a worktree has there. Never a repository above it, which git would
        // otherwise answer for, at paths that are not this root's.
        var dotGit = Path.Combine(root, ".git");
        if (!Directory.Exists(dotGit) && !File.Exists(dotGit)) return null;

        try
        {
            using var git = Process.Start(Start(root, specs));
            if (git is null) return null;
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

                return null;
            }

            git.WaitForExit();
            _ = said.Result;
            return git.ExitCode == 0 ? answer.Result.Length > 0 : null;
        }
        catch (Exception error) when (error is System.ComponentModel.Win32Exception or IOException or InvalidOperationException)
        {
            return null;
        }
    }

    /// <summary>The root's record folders that exist, as the scan names them: repository-relative with forward slashes, each once.</summary>
    internal static IReadOnlyList<string> Folders(string root, string? documents, string? index)
    {
        var layout = RepositoryLayout.Of(root, DaorisLock.Read(root));
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var folders = new List<string>();
        // The empty string is the repository's root, whose own records the top-level patterns name.
        foreach (var named in layout.Roots.Append("docs").Append(documents).Append(index))
        {
            if (string.IsNullOrWhiteSpace(named)) continue;
            var relative = named.Replace('\\', '/').Trim('/');
            var full = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
            if ((Directory.Exists(full) || File.Exists(full)) && seen.Add(full)) folders.Add(relative);
        }

        return folders;
    }

    /// <summary>The start of the one git <see cref="Of"/> asks, past Git for Windows' launcher where that is the git found (GITSTART2).</summary>
    /// <param name="specs">The pathspecs the status reads, each as git takes it.</param>
    internal static ProcessStartInfo Start(string root, IReadOnlyList<string> specs)
    {
        var start = new ProcessStartInfo("git")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
        };
        foreach (var argument in new[] { "-C", root, "--no-optional-locks", "status", "--porcelain=v1", "-z", "--untracked-files=normal", "--" })
        {
            start.ArgumentList.Add(argument);
        }

        foreach (var spec in specs) start.ArgumentList.Add(spec);
        foreach (var variable in new[] { "GIT_DIR", "GIT_WORK_TREE", "GIT_INDEX_FILE", "GIT_COMMON_DIR" }) start.Environment.Remove(variable);
        start.Environment["GIT_CEILING_DIRECTORIES"] = Path.GetDirectoryName(Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, '/')) ?? string.Empty;
        GitForWindows.Past(start);
        return start;
    }
}
