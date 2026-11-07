using System.Globalization;
using static Daoris.Driver.TreeGit;
using static Daoris.Driver.TreeWorktrees;
using static Daoris.Driver.SetAsideMarks;

namespace Daoris.Driver;

/// <summary>What a move aside reads and puts back around the move: the nested repositories, what is uncommitted, what is held now.</summary>
internal static class SetAsideReads
{
    /// <summary>
    /// Every folder below the tree's top that holds a <c>.git</c> file or folder, as paths from the tree's root with forward
    /// slashes, none read into: what lies inside one is that repository's. Read from the folders, hidden ones included, never
    /// following a link; the tree's own <c>.git</c> at its top is passed over, and never read into. A folder that cannot be read throws.
    /// The same walk names every link it meets, never followed, which the slim's judgement reads (Fable F1.5: one walk shared).
    /// </summary>
    internal static (List<string> Nested, List<string> Links) NestedRepositories(string tree)
    {
        var top = Path.TrimEndingDirectorySeparator(Path.GetFullPath(tree));
        var options = new EnumerationOptions { IgnoreInaccessible = false, AttributesToSkip = 0, RecurseSubdirectories = false };
        var found = new List<string>();
        var links = new List<string>();
        var pending = new Stack<string>();
        pending.Push(top);
        while (pending.TryPop(out var folder))
        {
            var below = new List<string>();
            var linked = new List<string>();
            var repository = false;
            foreach (var entry in new DirectoryInfo(folder).EnumerateFileSystemInfos("*", options))
            {
                if (string.Equals(entry.Name, ".git", StringComparison.OrdinalIgnoreCase))
                {
                    repository = !string.Equals(folder, top, StringComparison.OrdinalIgnoreCase);
                    if (repository) break;
                    continue;
                }

                if (entry.Attributes.HasFlag(FileAttributes.ReparsePoint))
                {
                    linked.Add(Path.GetRelativePath(top, entry.FullName).Replace('\\', '/'));
                }
                else if (entry is DirectoryInfo directory)
                {
                    below.Add(directory.FullName);
                }
            }

            if (repository)
            {
                found.Add(Path.GetRelativePath(top, folder).Replace('\\', '/'));
                continue;
            }

            links.AddRange(linked);
            foreach (var each in below) pending.Push(each);
        }

        return (found, links);
    }

    /// <summary>
    /// A moved folder put back where its tree stood, and its manifest forgotten only once it is back:
    /// where the put-back fails, the manifest stays at <c>judged</c> beside the folder aside, with its facts, so the next look meets a
    /// registration whose folder is gone, removes it and its branch, and marks the manifest <c>deleted</c>, and its record is kept.
    /// </summary>
    internal static (bool Gone, string? To, string? Code, string Sentence) PutBack(string to, string tree, string? manifest, string code, string why)
    {
        try
        {
            if (SetAsideFaults.Current?.RefusePutBack == true) throw new IOException("refused by the test's fault");
            Directory.Move(to, tree);
            Forget(manifest);
            return (false, null, code, $"{why}, so its folder was put back");
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return (false, to, code, $"{why}, and its folder, moved aside to {to}, could not be put back ({error.Message.Trim()})");
        }
    }

    /// <summary>
    /// What the moved tree holds that no commit does, tracked or untracked and not ignored, read by git in the moved folder with no optional
    /// lock: a clause where it holds something or git could not say, else null.
    /// </summary>
    internal static async Task<string?> UncommittedAsync(string folder, CancellationToken ct)
    {
        var (code, porcelain, err) = await WorkingTree.GitAsync(
            folder, ["--no-optional-locks", "status", "--porcelain", "-z", "--untracked-files=all", "--ignore-submodules=none"], ct).ConfigureAwait(false);
        if (code != 0) return $"git could not read its tree again once moved ({FirstLine(err)})";
        var paths = porcelain.Split('\0', StringSplitOptions.RemoveEmptyEntries);
        return paths.Length == 0 ? null : $"{paths.Length} path(s) changed between the judgement and the move: {string.Join(", ", paths.Take(3).Select(each => each.Length > 3 ? each[3..] : each))}";
    }

    /// <summary>
    /// The holding check right before a recorded folder's rename (Codex's read of the branch, finding 1): null where it passes; else why it is
    /// kept. Failing for certain, the manifest is marked <c>unmarked</c>, so the folder waits its days.
    /// </summary>
    internal static async Task<(string Code, string Sentence)?> HeldNowAsync(
        string folder, SlimManifest manifest, string workspace, Func<string, string, string?>? rootOf, CancellationToken ct)
    {
        if (rootOf is null) return (SetAsideKept.Unheld, "its holding check could not run here, so it waits for a pass that can run it");
        var (result, why) = await SetAsideEligibility.HoldingAsync(rootOf(workspace, manifest.Repository), manifest, ct).ConfigureAwait(false);
        if (result == Holding.Passed) return null;
        if (result == Holding.Failed)
        {
            try
            {
                (manifest with { State = SlimStates.Unmarked, Why = why }).Write(SlimManifest.PathFor(folder));
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                // Read again at the next pass, which fails the same way.
            }
        }

        return (SetAsideKept.Unheld, $"its holding check did not pass ({why}), so it is kept");
    }
}
