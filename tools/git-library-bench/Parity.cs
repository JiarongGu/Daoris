using System.Text;
using LibGit2Sharp;

namespace GitLibraryBench;

/// <summary>
/// The parity census: each read Daoris asks, answered by git's binary and by the library over the census's trees, compared as values;
/// a difference is printed with its first cases.
/// </summary>
internal static class Parity
{
    private static int _differences;

    public static async Task RunAsync(GitBinary git, string scratch)
    {
        Report.Machine();
        var layout = await ParityFixture.MakeAsync(git, scratch);
        async Task<string> Git(string folder, params string[] arguments) => (await git.RunAsync(folder, arguments)).Output;

        using (var root = new Repository(layout.Root))
        {
            Same("refs", Lines(await Git(layout.Root, "for-each-ref", "--format=%(refname) %(objectname)")),
                [.. root.Refs.Where(each => !each.CanonicalName.EndsWith("/HEAD", StringComparison.Ordinal))
                    .Select(each => $"{each.CanonicalName} {each.ResolveToDirectReference()?.Target.Sha}")]);
            Same("worktrees", Lines(await Git(layout.Root, "worktree", "list", "--porcelain")).Where(each => each.StartsWith("worktree ", StringComparison.Ordinal))
                    .Skip(1).Select(each => Normal(each["worktree ".Length..])),
                [.. root.Worktrees.Select(each => { using var tree = each.WorktreeRepository; return Normal(tree.Info.WorkingDirectory); })]);
            var head = root.Lookup<Commit>("refs/heads/daoris/s-parity");
            var line = root.Lookup<Commit>(layout.Line);
            Same("merge-base", [(await Git(layout.Root, "merge-base", head.Sha, line.Sha)).Trim()], [root.ObjectDatabase.FindMergeBase(head, line).Sha]);
            var divergence = root.ObjectDatabase.CalculateHistoryDivergence(head, line);
            Same("ahead and behind",
                [(await Git(layout.Root, "rev-list", "--count", head.Sha, "--not", line.Sha)).Trim(), (await Git(layout.Root, "rev-list", "--count", line.Sha, "--not", head.Sha)).Trim()],
                [$"{divergence.AheadBy}", $"{divergence.BehindBy}"]);
            Same("commits no branch holds", Lines(await Git(layout.Root, "log", "--format=%H", head.Sha, "--not", "--exclude=daoris/*", "--branches", "--exclude=*/daoris/*", "--remotes")),
                [.. root.Commits.QueryBy(new CommitFilter { IncludeReachableFrom = head, ExcludeReachableFrom = LibraryRefs.HeldTips(root) }).Select(each => each.Sha)]);
            Same("files the line lacks", (await Git(layout.Root, "diff", "--no-renames", "--name-only", "-z", line.Sha, head.Sha)).Split('\0', StringSplitOptions.RemoveEmptyEntries),
                [.. root.Diff.Compare<TreeChanges>(line.Tree, head.Tree, new CompareOptions { Similarity = SimilarityOptions.None }).Select(each => each.Path.Replace('\\', '/'))]);
            await DiffAsync(git, "committed diff, with renames", layout.Session, $"{layout.Base}..HEAD", root, root.Lookup<Commit>(layout.Base).Tree, head.Tree);
            foreach (var path in new[] { "eol.crlf", "eol.lf", "data.up", "stamp.id", "img.bin", "a.txt" })
            {
                var blob = root.Lookup<Blob>($"{layout.Base}:{path}");
                Same($"{path} as stored", [await Git(layout.Root, "cat-file", "blob", $"{layout.Base}:{path}")], [Read(blob.GetContentStream())]);
                Same($"{path} as checked out", [await Git(layout.Root, "cat-file", "--filters", $"{layout.Base}:{path}")], [Read(blob.GetContentStream(new FilteringOptions(path)))]);
            }
        }

        foreach (var (name, path) in layout.Trees)
        {
            Repository tree;
            try
            {
                tree = new Repository(path);
            }
            catch (LibGit2SharpException error)
            {
                Differs($"{name}: open", [$"the library cannot open it: {error.Message}"]);
                continue;
            }

            using (tree)
            {
                Same($"{name}: head", [(await Git(path, "rev-parse", "HEAD")).Trim()], [tree.Head.Tip.Sha]);
                Same($"{name}: branch", [(await Git(path, "symbolic-ref", "--short", "-q", "HEAD")).Trim()],
                    [tree.Info.IsHeadDetached ? "" : tree.Head.FriendlyName]);
                Same($"{name}: status, untracked all", GitStatus(await Git(path, "--no-optional-locks", "status", "--porcelain", "-z", "--untracked-files=all")),
                    LibraryStatus(tree.RetrieveStatus(new StatusOptions { IncludeUntracked = true, RecurseUntrackedDirs = true, DetectRenamesInIndex = true, DetectRenamesInWorkDir = false })));
                Same($"{name}: status, ignored", GitStatus(await Git(path, "--no-optional-locks", "status", "--porcelain", "-z", "--untracked-files=all", "--ignored=traditional")).Where(each => each.StartsWith("!!", StringComparison.Ordinal)),
                    LibraryStatus(tree.RetrieveStatus(new StatusOptions { IncludeUntracked = true, RecurseUntrackedDirs = true, IncludeIgnored = true, RecurseIgnoredDirs = true })).Where(each => each.StartsWith("!!", StringComparison.Ordinal)));
                Same($"{name}: status, head and branch (changed count)", [$"{Lines(await Git(path, "--no-optional-locks", "status", "--porcelain=v2", "--branch")).Count(each => !each.StartsWith('#'))}"],
                    [$"{tree.RetrieveStatus(new StatusOptions { IncludeUntracked = true, RecurseUntrackedDirs = false }).Count()}"]);
                await DiffAsync(git, $"{name}: uncommitted diff", path, "HEAD", tree, tree.Head.Tip.Tree, null);
                var untracked = ReviewReads.Untracked(await Git(path, "--no-optional-locks", "status", "--porcelain", "-z", "--untracked-files=all")).ToArray();
                if (untracked.Length > 0)
                {
                    var binary = new List<string>();
                    foreach (var file in untracked)
                    {
                        binary.Add(await Git(path, "-c", "core.quotePath=false", "diff", "--no-index", "--no-color", "--no-ext-diff", "--src-prefix=a/", "--dst-prefix=b/", "--", "/dev/null", file));
                    }

                    using var patches = ReviewReads.UntrackedPatches(tree, untracked, new CompareOptions { IndentHeuristic = true });
                    Same($"{name}: untracked files' patches", binary, [.. untracked.Select(file => patches[file]?.Patch ?? "(none)")]);
                }
            }
        }

        Console.WriteLine($"parity: {_differences} differences");
    }

    private static async Task DiffAsync(GitBinary git, string name, string folder, string range, Repository repository, Tree from, Tree? to)
    {
        var options = new CompareOptions { Similarity = SimilarityOptions.Renames, IndentHeuristic = true };
        using var patch = to is null
            ? repository.Diff.Compare<Patch>(from, DiffTargets.Index | DiffTargets.WorkingDirectory, null, null, options)
            : repository.Diff.Compare<Patch>(from, to, options);
        var raw = (await git.RunAsync(folder, ["--no-optional-locks", "-c", "diff.autoRefreshIndex=false", "diff", "-M", "-z", "--raw", range])).Output.Split('\0');
        var files = new List<string>();
        for (var index = 0; index + 1 < raw.Length; index++)
        {
            if (!raw[index].StartsWith(':')) continue;
            var status = raw[index].Split(' ')[^1][..1];
            files.Add(status is "R" or "C" ? $"{status} {raw[index + 1]} -> {raw[index + 2]}" : $"{status} {raw[index + 1]}");
            index += status is "R" or "C" ? 2 : 1;
        }

        Same($"{name}: files", files, [.. patch.Select(each => each.Status is ChangeKind.Renamed or ChangeKind.Copied
            ? $"{Letter(each.Status)} {Slash(each.OldPath)} -> {Slash(each.Path)}" : $"{Letter(each.Status)} {Slash(each.Path)}")]);
        var numstat = (await git.RunAsync(folder, ["--no-optional-locks", "-c", "diff.autoRefreshIndex=false", "diff", "-M", "--numstat", range])).Output;
        Same($"{name}: counts", Lines(numstat).Select(each => string.Join(' ', each.Split('\t')[..2])),
            [.. patch.Select(each => each.IsBinaryComparison ? "- -" : $"{each.LinesAdded} {each.LinesDeleted}")]);
        var text = (await git.RunAsync(folder, ["--no-optional-locks", "-c", "diff.autoRefreshIndex=false", "-c", "core.quotePath=false", "diff",
            "--no-color", "--no-ext-diff", "--src-prefix=a/", "--dst-prefix=b/", "-M", range])).Output;
        Same($"{name}: patches", Patches(text), [.. patch.Select(each => each.Patch)]);
    }

    private static List<string> Patches(string text)
    {
        var parts = new List<string>();
        foreach (var piece in ("\n" + text).Split("\ndiff --git ", StringSplitOptions.RemoveEmptyEntries))
        {
            var part = "diff --git " + piece;
            parts.Add(part.EndsWith('\n') ? part : part + "\n");
        }

        return parts;
    }

    private static List<string> GitStatus(string porcelainZ)
    {
        var entries = new List<string>();
        var fields = porcelainZ.Split('\0', StringSplitOptions.RemoveEmptyEntries);
        for (var index = 0; index < fields.Length; index++)
        {
            var field = fields[index];
            if (field.Length > 0 && field[0] is 'R' or 'C') entries.Add($"{field[..2]} {fields[++index]} -> {field[3..]}");
            else entries.Add(field);
        }

        return entries;
    }

    private static List<string> LibraryStatus(RepositoryStatus status)
    {
        var entries = new List<string>();
        foreach (var entry in status)
        {
            var state = entry.State;
            if (state == FileStatus.Unaltered) continue;
            if (state.HasFlag(FileStatus.Ignored))
            {
                entries.Add($"!! {Slash(entry.FilePath)}");
                continue;
            }

            if (state == FileStatus.NewInWorkdir)
            {
                entries.Add($"?? {Slash(entry.FilePath)}");
                continue;
            }

            if (state.HasFlag(FileStatus.Conflicted))
            {
                entries.Add($"UU {Slash(entry.FilePath)}");
                continue;
            }

            var x = state.HasFlag(FileStatus.NewInIndex) ? 'A' : state.HasFlag(FileStatus.ModifiedInIndex) ? 'M' : state.HasFlag(FileStatus.DeletedFromIndex) ? 'D'
                : state.HasFlag(FileStatus.RenamedInIndex) ? 'R' : state.HasFlag(FileStatus.TypeChangeInIndex) ? 'T' : ' ';
            var y = state.HasFlag(FileStatus.ModifiedInWorkdir) ? 'M' : state.HasFlag(FileStatus.DeletedFromWorkdir) ? 'D'
                : state.HasFlag(FileStatus.TypeChangeInWorkdir) ? 'T' : state.HasFlag(FileStatus.RenamedInWorkdir) ? 'R' : ' ';
            entries.Add(x == 'R' && entry.HeadToIndexRenameDetails is { } renamed
                ? $"{x}{y} {Slash(renamed.OldFilePath)} -> {Slash(renamed.NewFilePath)}"
                : $"{x}{y} {Slash(entry.FilePath)}");
        }

        return entries;
    }

    private static char Letter(ChangeKind kind) => kind switch
    {
        ChangeKind.Added => 'A', ChangeKind.Deleted => 'D', ChangeKind.Modified => 'M', ChangeKind.Renamed => 'R', ChangeKind.Copied => 'C',
        ChangeKind.TypeChanged => 'T', ChangeKind.Conflicted => 'U', _ => '?',
    };

    private static void Same(string name, IEnumerable<string> binary, IEnumerable<string> library)
    {
        var a = binary.Order(StringComparer.Ordinal).ToList();
        var b = library.Order(StringComparer.Ordinal).ToList();
        if (a.SequenceEqual(b, StringComparer.Ordinal))
        {
            Console.WriteLine($"  same: {name} ({a.Count})");
            return;
        }

        Differs(name, [.. a.Except(b, StringComparer.Ordinal).Take(4).Select(each => $"git only: {Show(each)}"),
            .. b.Except(a, StringComparer.Ordinal).Take(4).Select(each => $"library only: {Show(each)}")]);
    }

    private static void Differs(string name, IReadOnlyList<string> details)
    {
        _differences++;
        Console.WriteLine($"  DIFFERS: {name}");
        foreach (var detail in details) Console.WriteLine($"    {detail}");
    }

    private static string Show(string value)
    {
        var shown = value.Replace("\r", "\\r").Replace("\n", "\\n").Replace("\0", "\\0");
        return shown.Length > 220 ? shown[..220] + "…" : shown;
    }

    private static string Read(Stream stream)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private static List<string> Lines(string text) => [.. text.Split('\n', StringSplitOptions.RemoveEmptyEntries)];

    private static string Slash(string path) => path.Replace('\\', '/');

    private static string Normal(string path) => Path.GetFullPath(path.Replace('/', Path.DirectorySeparatorChar)).TrimEnd(Path.DirectorySeparatorChar).ToLowerInvariant();
}
