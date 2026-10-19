using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;

namespace Daoris.Driver;

/// <summary>
/// A session tree's opening, and an opening cut off (OPENCUT1): while <see cref="SessionTrees.OpenAsync"/> opens a tree, git
/// holds it locked under a reason that names the process opening it, and lets go once where its branch started is recorded. An
/// open that a cancel cuts off undoes itself here; one a kill cuts off is left locked, and a later look tells it from a person's
/// lock by its reason (<see cref="TreeStrays"/>).
/// </summary>
internal static class TreeOpenings
{
    /// <summary>What the reason of an opening's lock starts with; the process's id and a closing bracket follow.</summary>
    private const string Opening = "Daoris is opening this tree (process ";

    /// <summary>
    /// git's own reason for the lock it holds while <c>git worktree add</c> runs, left behind where the add was killed: an open
    /// from before OPENCUT1, or one cut off before git wrote the reason it was handed.
    /// </summary>
    internal const string GitsOwn = "initializing";

    /// <summary>The reason an opening's lock gives, which <c>git worktree list</c> shows a person: Daoris, and which of its processes.</summary>
    internal static string LockReason(int process) => Opening + process.ToString(CultureInfo.InvariantCulture) + ")";

    /// <summary>The process an opening's lock names; null where the reason is no opening's.</summary>
    internal static int? OpenerOf(string? reason) =>
        reason is { } said && said.StartsWith(Opening, StringComparison.Ordinal) && said.EndsWith(')')
        && int.TryParse(said.AsSpan(Opening.Length, said.Length - Opening.Length - 1), NumberStyles.None, CultureInfo.InvariantCulture, out var process)
            ? process
            : null;

    /// <summary>
    /// Whether a tree locked for this reason is an opening that was cut off (OPENCUT1): git's own lock from an add that never
    /// finished, or Daoris's opening lock whose process no longer runs. A person's lock, any other reason or none, is never one.
    /// </summary>
    /// <remarks>
    /// Asked only with the repository's trees held alone (<see cref="TreeLock"/>), which every start holds while it opens a tree:
    /// no open of this home's is under way then, so git's own lock is one a kill left. A process that runs under the id named is
    /// taken to be the opener, though the id may be another's since, and the tree waits until it ends.
    /// </remarks>
    /// <param name="running">Whether a process of that id runs (<see cref="Running"/>; a test's stand-in).</param>
    internal static bool CutOff(string? reason, Func<int, bool> running) =>
        reason == GitsOwn || (OpenerOf(reason) is { } process && !running(process));

    /// <summary>Whether a process of this id runs here. One this process may not look at runs, as far as anything here knows.</summary>
    internal static bool Running(int process)
    {
        try
        {
            using var found = Process.GetProcessById(process);
            return !found.HasExited;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (Exception error) when (error is InvalidOperationException or Win32Exception or NotSupportedException)
        {
            return true;
        }
    }

    /// <summary>
    /// An open that did not finish, undone (OPENCUT1): git lets go of its tree and deletes the tree's files, lock or none
    /// (<c>--force</c> twice); what git did not delete of the folder is deleted here, where the open made it; and its branch goes.
    /// Each step runs to its end whatever the open's token said, since a cancel is what brought it here.
    /// </summary>
    /// <remarks>
    /// TREELINK1: git's removal follows a junction out of the tree, as a copy of the dependencies may leave one, so each link in it
    /// goes as a link first, where the open made the folder or git may keep it as this tree (one git cannot list may be). A tree whose
    /// links cannot all go is left, with its branch, for the look, which moves its folder aside whole (<see cref="TreeStrays"/>).
    /// </remarks>
    /// <param name="ours">Whether the open made the folder: one that was there before the add is never deleted.</param>
    internal static async Task UndoAsync(string root, string path, string branch, bool ours)
    {
        var done = CancellationToken.None;
        if ((ours || await TreeLeftovers.RegisteredAsync(root, path, done).ConfigureAwait(false) is not false) && TreeLinks.Unlinked(path) is not null)
        {
            return;
        }

        await WorkingTree.GitAsync(root, ["worktree", "remove", "--force", "--force", path], done).ConfigureAwait(false);
        if (ours && Directory.Exists(path))
        {
            try
            {
                foreach (var file in Directory.EnumerateFiles(path, "*", new EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = FileAttributes.ReparsePoint }))
                {
                    File.SetAttributes(file, FileAttributes.Normal);
                }

                Directory.Delete(path, recursive: true);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                // Something holds it: the look moves what is left aside once nothing does (TreeStrays).
            }
        }

        // A record git still keeps of it, its folder gone now: that record alone, never a prune of every stale one (AUTOTIDY1).
        if (await TreeLeftovers.RegisteredAsync(root, path, done).ConfigureAwait(false) is true)
        {
            await WorkingTree.GitAsync(root, ["worktree", "remove", "--force", "--force", path], done).ConfigureAwait(false);
        }

        await WorkingTree.GitAsync(root, ["branch", "-D", branch], done).ConfigureAwait(false);
    }
}
