using System.Globalization;
using Daoris.Ports.Git;
using Daoris.Ports.Repositories;

namespace Daoris.Driver;

/// <summary>
/// The real repository behind <see cref="IGitRepository"/>: each operation is the one git the families asked, run through
/// <see cref="WorkingTree"/>'s funnel so this flow's memos and counts hold, or through a run a claim holds in its own job
/// (<see cref="Held"/>). The funnel answers from the git library where it can; a failure is told as the port says, never as an empty answer.
/// </summary>
public sealed class GitRepository : IGitRepository
{
    internal delegate Task<(int Code, string Stdout, string Stderr)> Runner(string folder, IReadOnlyList<string> arguments, CancellationToken ct);

    /// <summary>The repository as every family reads it, which the roots hand: through the application's git funnel.</summary>
    public static GitRepository Shared { get; } = new(WorkingTree.GitAsync, WorkingTree.GitAsync);

    private readonly Runner RunGitAsync;
    private readonly GitRefTransacting.InputRunner? RunGitWithInputAsync;

    /// <param name="run">The git every operation runs.</param>
    /// <param name="withInput">The git that reads standard input, which a ref transaction needs; null where the run holds none.</param>
    internal GitRepository(Runner run, GitRefTransacting.InputRunner? withInput = null) => (RunGitAsync, RunGitWithInputAsync) = (run, withInput);

    public async Task<string?> HeadAsync(string folder, CancellationToken ct)
    {
        var (code, stdout, _) = await RunGitAsync(folder, ["rev-parse", "HEAD"], ct).ConfigureAwait(false);
        return code == 0 ? stdout.Trim() : null;
    }

    public async Task<string?> BranchAsync(string folder, CancellationToken ct)
    {
        var (code, stdout, stderr) = await RunGitAsync(folder, WorkingTree.BranchAsk, ct).ConfigureAwait(false);
        return ReviewChoices.Branch(code, stdout, stderr);
    }

    public async Task<string?> CommitOfAsync(string folder, string revision, CancellationToken ct)
    {
        // A name git would read as an option names no commit here.
        if (revision.Length == 0 || revision.StartsWith('-')) return null;
        var (code, stdout, _) = await RunGitAsync(folder, ["rev-parse", "--verify", "--quiet", $"{revision}^{{commit}}"], ct).ConfigureAwait(false);
        return code == 0 && stdout.Trim() is { Length: > 0 } sha ? sha : null;
    }

    public async Task<string?> MergeBaseAsync(string folder, string first, string second, CancellationToken ct)
    {
        var (code, stdout, _) = await RunGitAsync(folder, ["merge-base", first, second], ct).ConfigureAwait(false);
        return code == 0 && stdout.Trim() is { Length: > 0 } sha ? sha : null;
    }

    public async Task<bool?> IsAncestorAsync(string folder, string ancestor, string descendant, CancellationToken ct) =>
        (await RunGitAsync(folder, ["merge-base", "--is-ancestor", ancestor, descendant], ct).ConfigureAwait(false)).Code switch { 0 => true, 1 => false, _ => null };

    public async Task<int?> CountAsync(string folder, string tip, IReadOnlyList<string> exclude, CancellationToken ct)
    {
        var (code, stdout, _) = await RunGitAsync(folder, exclude.Count == 0 ? ["rev-list", "--count", tip] : ["rev-list", "--count", tip, "--not", .. exclude], ct).ConfigureAwait(false);
        return code == 0 && int.TryParse(stdout.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var count) ? count : null;
    }

    public async Task<IReadOnlyList<Ports.Repositories.GitRef>?> RefsAsync(string folder, IReadOnlyList<string> prefixes, string? contains, int? limit, CancellationToken ct)
    {
        List<string> ask = ["for-each-ref", "--format=%(objectname) %(refname)"];
        if (limit is { } most) ask.Add($"--count={most.ToString(CultureInfo.InvariantCulture)}");
        if (contains is not null) ask.AddRange(["--contains", contains]);
        ask.AddRange(prefixes);
        var (code, stdout, _) = await RunGitAsync(folder, ask, ct).ConfigureAwait(false);
        if (code != 0) return null;
        return [.. stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(line => line.TrimEnd('\r')).Where(line => line.IndexOf(' ', StringComparison.Ordinal) > 0)
            .Select(line => new Ports.Repositories.GitRef(line[(line.IndexOf(' ', StringComparison.Ordinal) + 1)..], line[..line.IndexOf(' ', StringComparison.Ordinal)]))];
    }

    public async Task<IReadOnlyList<GitTreeEntry>?> EntriesAsync(string folder, string commit, IReadOnlyList<string> within, bool recursive, CancellationToken ct)
    {
        string[] head = recursive ? ["ls-tree", "-r", "-z", commit] : ["ls-tree", "-z", commit];
        var (code, listed, _) = await RunGitAsync(folder, within.Count == 0 ? head : [.. head, "--", .. within], ct).ConfigureAwait(false);
        if (code != 0) return null;
        return [.. listed.Split('\0', StringSplitOptions.RemoveEmptyEntries).Where(entry => entry.IndexOf('\t', StringComparison.Ordinal) > 0)
            .Select(entry => new GitTreeEntry(entry[..entry.IndexOf(' ', StringComparison.Ordinal)], entry[(entry.IndexOf('\t', StringComparison.Ordinal) + 1)..]))];
    }

    public async Task<IReadOnlyList<GitWorktree>?> WorktreesAsync(string folder, CancellationToken ct)
    {
        var (code, porcelain, _) = await RunGitAsync(folder, ["worktree", "list", "--porcelain"], ct).ConfigureAwait(false);
        return code == 0 ? Worktrees(porcelain) : null;
    }

    /// <summary>The working trees <c>worktree list --porcelain</c> printed: each one's path, branch and lock.</summary>
    internal static IReadOnlyList<GitWorktree> Worktrees(string porcelain)
    {
        var entries = new List<GitWorktree>();
        string? path = null;
        string? branch = null;
        var locked = false;
        string? reason = null;
        foreach (var raw in porcelain.Split('\n').Append(""))
        {
            var line = raw.TrimEnd('\r');
            if (line.Length == 0)
            {
                if (path is not null) entries.Add(new GitWorktree(Path.GetFullPath(path), branch, locked, reason));
                (path, branch, locked, reason) = (null, null, false, null);
            }
            else if (line.StartsWith("worktree ", StringComparison.Ordinal)) path = line["worktree ".Length..];
            else if (line.StartsWith("branch refs/heads/", StringComparison.Ordinal)) branch = line["branch refs/heads/".Length..];
            else if (line == "locked" || line.StartsWith("locked ", StringComparison.Ordinal))
            {
                locked = true;
                reason = line.Length > "locked ".Length ? line["locked ".Length..] : null;
            }
        }

        return entries;
    }

    public async Task<IReadOnlyDictionary<string, string>> StatusAsync(string folder, bool ignored, CancellationToken ct)
    {
        string[] ask = ignored
            ? ["--no-optional-locks", "status", "--porcelain", "-z", "--untracked-files=all", "--ignored=matching"]
            : ["--no-optional-locks", "status", "--porcelain", "-z", "--untracked-files=all"];
        var (code, said, error) = await RunGitAsync(folder, ask, ct).ConfigureAwait(false);
        if (code != 0) throw new DriverException($"git could not read the tree's status: {error.Trim()}");
        var paths = new Dictionary<string, string>(StringComparer.Ordinal);
        var entries = said.Split('\0', StringSplitOptions.RemoveEmptyEntries);
        for (var at = 0; at < entries.Length; at++)
        {
            var entry = entries[at];
            if (entry.Length < 4) continue;
            var state = entry[..2];
            paths[entry[3..]] = state;
            // A rename or a copy names its source in the entry after it.
            if (state[0] is 'R' or 'C') at++;
        }

        return paths;
    }

    public Task<string?> TextAtAsync(string folder, string commit, string path, CancellationToken ct) => GitObjectReads.TextAsync(folder, GitObjectName.At(commit, path), ct);

    public async Task<bool> IgnoresCaseAsync(string folder, CancellationToken ct)
    {
        var (code, stdout, _) = await RunGitAsync(folder, ["--no-optional-locks", "config", "--bool", "core.ignorecase"], ct).ConfigureAwait(false);
        return code == 0 && stdout.Trim() == "true";
    }

    public async Task<IReadOnlyList<string>> IgnoredTopsAsync(string folder, CancellationToken ct)
    {
        var (code, stdout, stderr) = await RunGitAsync(folder, ["--no-optional-locks", "ls-files", "--others", "--ignored", "--exclude-standard", "--directory", "-z"], ct).ConfigureAwait(false);
        return code == 0 ? [.. stdout.Split('\0', StringSplitOptions.RemoveEmptyEntries)] : throw new DriverException($"git ls-files in {folder} failed: {TreeGit.Failure(stderr)}");
    }

    public async Task<IReadOnlyList<string>> IgnoredAsync(string folder, IReadOnlyList<string> within, CancellationToken ct)
    {
        var (code, said, error) = await RunGitAsync(
            folder, ["--no-optional-locks", "status", "--porcelain", "-z", "--untracked-files=all", "--ignored=matching", "--", .. within], ct).ConfigureAwait(false);
        if (code != 0) throw new DriverException($"git could not list the slot's ignored entries again: {TreeGit.FirstLine(error)}");
        return [.. said.Split('\0', StringSplitOptions.RemoveEmptyEntries).Where(entry => entry.StartsWith("!! ", StringComparison.Ordinal)).Select(entry => entry[3..])];
    }

    public async Task<IReadOnlyList<string>?> HiddenAsync(string folder, CancellationToken ct)
    {
        var (code, listed, _) = await RunGitAsync(folder, ["ls-files", "-v", "-z"], ct).ConfigureAwait(false);
        if (code != 0) return null;
        // `ls-files -v` tags an assume-unchanged entry in lower case and a skip-worktree one `S`.
        return [.. listed.Split('\0', StringSplitOptions.RemoveEmptyEntries).Where(entry => entry.Length > 2 && (char.IsLower(entry[0]) || entry[0] == 'S')).Select(entry => entry[2..])];
    }

    public async Task<GitDone> UpdateRefAsync(string folder, string name, string to, string? from, CancellationToken ct) =>
        Done(await RunGitAsync(folder, from is null ? ["update-ref", name, to] : ["update-ref", name, to, from], ct).ConfigureAwait(false));

    public async Task<GitDone> SwitchAsync(string folder, GitSwitch to, CancellationToken ct)
    {
        List<string> ask = to.HooksPath is { } hooks ? ["-c", $"core.hooksPath={hooks}", "switch"] : ["switch"];
        if (to.Branch is null) ask.Add("--detach");
        if (to.KeepIgnored) ask.Add("--no-overwrite-ignore");
        if (to.Creates) ask.AddRange(["-c", to.Branch!, .. to.Start is { } start ? (string[])[start] : []]);
        else if (to.Branch is not null) ask.Add(to.Branch);
        else if (to.Start is not null) ask.Add(to.Start);
        return Done(await RunGitAsync(folder, ask, ct).ConfigureAwait(false));
    }

    public async Task<GitDone> CommitAllAsync(string folder, string message, CancellationToken ct)
    {
        if (Done(await RunGitAsync(folder, ["add", "-A"], ct).ConfigureAwait(false)) is { Ok: false } staged) return staged;
        return Done(await RunGitAsync(folder, ["commit", "--quiet", "--no-verify", "-m", message], ct).ConfigureAwait(false));
    }

    public async Task<GitDone> LockAsync(string folder, string path, string reason, CancellationToken ct) =>
        Done(await RunGitAsync(folder, ["worktree", "lock", "--reason", reason, path], ct).ConfigureAwait(false));

    public async Task<GitDone> UnlockAsync(string folder, string path, CancellationToken ct) =>
        Done(await RunGitAsync(folder, ["worktree", "unlock", path], ct).ConfigureAwait(false));

    public async Task<GitDone> PruneAsync(string folder, CancellationToken ct) =>
        Done(await RunGitAsync(folder, ["worktree", "prune"], ct).ConfigureAwait(false));

    public async Task<GitDone> AddWorktreeAsync(string folder, GitWorktreeAdd add, CancellationToken ct)
    {
        List<string> ask = add.HooksPath is { } hooks ? ["-c", $"core.hooksPath={hooks}", "worktree", "add"] : ["worktree", "add"];
        if (add.LockReason is { } reason) ask.AddRange(["--lock", "--reason", reason]);
        ask.AddRange(add.Branch is { } branch ? (string[])["-b", branch, add.Path, add.Start] : ["--detach", add.Path, add.Start]);
        var (code, _, stderr) = await RunGitAsync(folder, ask, ct).ConfigureAwait(false);
        return code == 0 ? GitDone.Done : GitDone.Not(TreeGit.Failure(stderr));
    }

    public async Task<string?> TopOfAsync(string folder, CancellationToken ct)
    {
        var (code, stdout, _) = await RunGitAsync(folder, ["rev-parse", "--show-toplevel"], ct).ConfigureAwait(false);
        return code == 0 && stdout.Trim() is { Length: > 0 } top ? Path.TrimEndingDirectorySeparator(Path.GetFullPath(top)) : null;
    }

    public async Task<GitDone> FastForwardAsync(string folder, string commit, CancellationToken ct) =>
        Done(await RunGitAsync(folder, ["merge", "--ff-only", "--quiet", commit], ct).ConfigureAwait(false));

    public async Task<GitDone> ResetHardAsync(string folder, string commit, CancellationToken ct) =>
        Done(await RunGitAsync(folder, ["reset", "--quiet", "--hard", commit], ct).ConfigureAwait(false));

    public Task<string?> DefaultBranchAsync(string folder, CancellationToken ct) => GitDefaultBranch.AskAsync(RunGitAsync, folder, ct);

    public async Task<GitDone> DeleteBranchAsync(string folder, string branch, CancellationToken ct) =>
        Done(await RunGitAsync(folder, ["branch", "-D", branch], ct).ConfigureAwait(false));

    public async Task<IReadOnlyList<string>?> ChangedAsync(string folder, string from, string to, CancellationToken ct)
    {
        var (code, stdout, _) = await RunGitAsync(folder, ["--no-optional-locks", "diff", "--no-ext-diff", "--no-renames", "--ignore-submodules=none", "--name-only", "-z", from, to], ct).ConfigureAwait(false);
        return code == 0 ? [.. stdout.Split('\0', StringSplitOptions.RemoveEmptyEntries)] : null;
    }

    public async Task<IReadOnlyList<GitMatch>> GrepAsync(string folder, string commit, IReadOnlyCollection<string> names, IReadOnlyList<string> pathspecs, CancellationToken ct)
    {
        if (names.Count == 0) return [];
        List<string> arguments = ["--no-optional-locks", "grep", "-I", "-F", "-z", "--no-color"];
        foreach (var name in names) arguments.AddRange(["-e", name]);
        arguments.AddRange([commit, "--", .. pathspecs]);
        var (code, stdout, stderr) = await RunGitAsync(folder, arguments, ct).ConfigureAwait(false);
        if (code > 1) throw new DriverException($"git grep in {folder} failed: {TreeGit.Failure(stderr)}");
        var prefix = commit + ":";
        return [.. stdout.ReplaceLineEndings("\n").Split('\n').Select(line => line.Split('\0', 2)).Where(parts => parts.Length == 2)
            .Select(parts => new GitMatch(parts[0].StartsWith(prefix, StringComparison.Ordinal) ? parts[0][prefix.Length..] : parts[0], parts[1]))];
    }

    public Task<GitRefRun> TransactAsync(string folder, GitRefTransaction transaction, CancellationToken ct) =>
        RunGitWithInputAsync is { } withInput
            ? GitRefTransacting.RunAsync(RunGitAsync, withInput, folder, transaction, ct)
            : Task.FromResult(new GitRefRun(false, "a git held in a claim's job takes no ref transaction"));

    public async Task<GitDone> RestoreAsync(string folder, IReadOnlyList<string> paths, CancellationToken ct) =>
        Done(await RunGitAsync(folder, ["checkout", "HEAD", "--", .. paths], ct).ConfigureAwait(false));

    public async Task<GitDone> RunHookAsync(string folder, string hook, IReadOnlyList<string> arguments, CancellationToken ct) =>
        Done(await RunGitAsync(folder, ["hook", "run", "--ignore-missing", hook, "--", .. arguments], ct).ConfigureAwait(false));

    public async Task<GitDone> RemoveWorktreeAsync(string folder, string path, CancellationToken ct) =>
        Done(await RunGitAsync(folder, ["worktree", "remove", "--force", "--force", path], ct).ConfigureAwait(false));

    public IGitRepository Held(Ports.Repositories.GitRun run) => new GitRepository(async (folder, arguments, ct) =>
    {
        var answer = await run(folder, arguments, ct).ConfigureAwait(false);
        return (answer.Exit ?? -1, answer.Output, answer.Error);
    });

    private static GitDone Done((int Code, string Stdout, string Stderr) ran) => ran.Code == 0 ? GitDone.Done : GitDone.Not(ran.Stderr.Trim());

    public async Task<GitMerged> MergeAsync(string folder, GitMergeAsk ask, CancellationToken ct)
    {
        IReadOnlyList<string> arguments = ask.Kind switch
        {
            GitMergeKind.FastForwardOnly => ["merge", "--ff-only", "--quiet", ask.Commit],
            GitMergeKind.Commit => ["merge", "--no-ff", "-m", ask.Message ?? $"Merge {ask.Commit}", ask.Commit],
            _ => ["merge", "--no-ff", "--no-commit", ask.Commit],
        };
        var (code, stdout, stderr) = await RunGitAsync(folder, arguments, ct).ConfigureAwait(false);
        if (code == 0)
        {
            var staged = ask.Kind == GitMergeKind.Stage;
            return new GitMerged(true, "", [], staged);
        }

        var pending = (await RunGitAsync(folder, ["rev-parse", "--quiet", "--verify", "MERGE_HEAD"], ct).ConfigureAwait(false)).Code == 0;
        return new GitMerged(false, stderr.Length > 0 ? stderr : stdout, pending ? await UnmergedAsync(folder, ct).ConfigureAwait(false) : [], pending);
    }

    public async Task<IReadOnlyList<string>?> RevListAsync(string folder, string tip, IReadOnlyList<string> exclude, CancellationToken ct)
    {
        var (code, stdout, _) = await RunGitAsync(folder, exclude.Count == 0 ? ["rev-list", tip] : ["rev-list", tip, "--not", .. exclude], ct).ConfigureAwait(false);
        return code == 0 ? [.. stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)] : null;
    }

    public async Task<GitDone> MergeAbortAsync(string folder, CancellationToken ct)
    {
        if ((await RunGitAsync(folder, ["rev-parse", "--quiet", "--verify", "MERGE_HEAD"], ct).ConfigureAwait(false)).Code != 0) return GitDone.Done;
        return Done(await RunGitAsync(folder, ["merge", "--abort"], ct).ConfigureAwait(false));
    }

    public async Task<IReadOnlyList<string>> UnmergedAsync(string folder, CancellationToken ct)
    {
        var (code, stdout, _) = await RunGitAsync(folder, ["--no-optional-locks", "-c", "core.quotePath=false", "diff", "--name-only", "--diff-filter=U"], ct).ConfigureAwait(false);
        return code != 0 ? [] : [.. stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];
    }

    public async Task<GitDone> CommitStagedAsync(string folder, string message, CancellationToken ct) =>
        Done(await RunGitAsync(folder, ["commit", "--quiet", "--no-verify", "-m", message], ct).ConfigureAwait(false));

    public async Task<GitDone> PutBackAsync(string folder, string commit, CancellationToken ct)
    {
        await RunGitAsync(folder, ["merge", "--abort"], ct).ConfigureAwait(false);
        foreach (var arguments in new[]
        {
            new[] { "reset", "--quiet", "--hard" }, new[] { "clean", "--quiet", "-fd" }, new[] { "checkout", "--quiet", "--force", "--detach", commit },
        })
        {
            var ran = await RunGitAsync(folder, arguments, ct).ConfigureAwait(false);
            if (ran.Code != 0) return GitDone.Not(ran.Stderr.Trim());
        }

        return GitDone.Done;
    }

    /// <summary>The act's own start: the process ring places it in the act's job and journals its pid, which no other git here does.</summary>
    public Task<GitHeldAnswer> RunHeldAsync(GitHeldRun run, GitHeldStart start) => start(run);

    public async Task<GitLayout?> LayoutAsync(string folder, CancellationToken ct)
    {
        var (code, stdout, _) = await RunGitAsync(folder, ["rev-parse", "--absolute-git-dir", "--git-common-dir"], ct).ConfigureAwait(false);
        var lines = stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return code != 0 || lines.Length < 2 ? null : new GitLayout(Path.GetFullPath(lines[0]), Path.GetFullPath(Path.Combine(folder, lines[1])));
    }

    public async Task<GitTreeStatus?> TreeStatusAsync(string folder, CancellationToken ct)
    {
        var (code, stdout, _) = await RunGitAsync(folder, TreeStatus.Ask, ct).ConfigureAwait(false);
        return code == 0 && TreeStatus.Parse(stdout) is { } read ? new GitTreeStatus(read.Changed, read.Head, read.Branch) : null;
    }

    public async Task<int?> UncommittedAsync(string folder, CancellationToken ct)
    {
        var (code, porcelain, _) = await RunGitAsync(folder, ["--no-optional-locks", "status", "--porcelain"], ct).ConfigureAwait(false);
        return code != 0 ? null : porcelain.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Length;
    }

    /// <summary>One start for the repository, the working root and the branch where git gives all three; else the two asks one at a time, each with its own failure.</summary>
    public async Task<GitTreePlace> PlaceAsync(string folder, CancellationToken ct)
    {
        var (code, output, _) = await RunGitAsync(folder, ["rev-parse", "--path-format=absolute", "--git-common-dir", "--show-toplevel", "--abbrev-ref", "HEAD"], ct).ConfigureAwait(false);
        var lines = output.Split('\n').Select(line => line.TrimEnd('\r')).Where(line => line.Length > 0).ToArray();
        if (code == 0 && lines.Length == 3)
        {
            GitFacts.SameRepository(folder, lines[0]);
            return new GitTreePlace(0, lines[0], "", lines[2], true, lines[1]);
        }

        var (rootCode, commonDir, rootErr) = await RunGitAsync(folder, ["rev-parse", "--path-format=absolute", "--git-common-dir"], ct).ConfigureAwait(false);
        if (rootCode != 0) return new GitTreePlace(rootCode, commonDir, rootErr, "", false, "");
        GitFacts.SameRepository(folder, commonDir.Trim());
        var (_, branch, _) = await RunGitAsync(folder, ["rev-parse", "--abbrev-ref", "HEAD"], ct).ConfigureAwait(false);
        return new GitTreePlace(0, commonDir, rootErr, branch, false, "");
    }

    public async Task<IReadOnlyList<string>?> UnlandedAsync(string folder, string revision, string? except, CancellationToken ct)
    {
        var (code, log, _) = await RunGitAsync(
            folder,
            except is null
                ? ["log", "--oneline", revision, "--not", "--exclude=daoris/*", "--branches", "--exclude=*/daoris/*", "--remotes"]
                : ["log", "--oneline", revision, "--not", "--exclude=daoris/*", $"--exclude={except}", "--branches", "--exclude=*/daoris/*", "--remotes"],
            ct).ConfigureAwait(false);
        return code != 0 ? null : log.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    public async Task<IReadOnlyList<GitRefFact>?> RefFactsAsync(string folder, CancellationToken ct)
    {
        var (code, output, _) = await RunGitAsync(folder, ["for-each-ref", .. GitLibraryRefs.Listed], ct).ConfigureAwait(false);
        return code == 0 && RefSnapshot.Parse(output) is { } refs
            ? [.. refs.Entries.Select(each => new GitRefFact(each.Name, each.Object, each.Type, each.Upstream ?? "", each.UpstreamShort ?? "", each.Tree ?? ""))]
            : null;
    }

    public async Task<IReadOnlyList<Ports.Repositories.GitRef>?> HoldingRefsAsync(string folder, string commit, CancellationToken ct)
    {
        var (code, containing, _) = await RunGitAsync(folder, ["for-each-ref", "--contains", commit, $"--format={ContentHolds.UnaliasedFormat}"], ct).ConfigureAwait(false);
        return code != 0 ? null : [.. ContentHolds.Unaliased(containing).Select(pair => new Ports.Repositories.GitRef(pair.Ref, pair.Commit))];
    }

    public async Task<GitFreshTop> FreshTopAsync(string folder, CancellationToken ct)
    {
        var (code, stdout, stderr) = await WorkingTree.GitAsync(folder, ["rev-parse", "--show-toplevel"], new Dictionary<string, string>(), timeout: null, ct).ConfigureAwait(false);
        return new GitFreshTop(code == 0 && stdout.Trim().Length > 0, stdout, stderr);
    }

    public async Task<GitDone> RemoveTreeAsync(string folder, string path, bool force, CancellationToken ct) =>
        Done(await RunGitAsync(folder, force ? ["worktree", "remove", "--force", path] : ["worktree", "remove", path], ct).ConfigureAwait(false));

    public async Task<IReadOnlyList<GitStatusEntry>> StatusEntriesAsync(string folder, CancellationToken ct)
    {
        var (code, said, error) = await RunGitAsync(folder, ["--no-optional-locks", "status", "--porcelain", "-z", "--untracked-files=all"], ct).ConfigureAwait(false);
        if (code != 0) throw new DriverException($"git could not read the tree's status: {error.Trim()}");
        var entries = said.Split('\0', StringSplitOptions.RemoveEmptyEntries);
        var listed = new List<GitStatusEntry>();
        for (var at = 0; at < entries.Length; at++)
        {
            var entry = entries[at];
            if (entry.Length < 4) continue;
            var state = entry[..2];
            string? from = null;
            if (state[0] is 'R' or 'C' && at + 1 < entries.Length) from = entries[++at];
            listed.Add(new GitStatusEntry(entry[3..], state, from));
        }

        return listed;
    }

    public async Task<bool> AvailableAsync(CancellationToken ct) =>
        (await RunGitAsync(AppContext.BaseDirectory, ["--version"], ct).ConfigureAwait(false)).Code == 0;

    public async Task<GitDone> InitAsync(string folder, string line, CancellationToken ct)
    {
        Directory.CreateDirectory(folder);
        var init = Done(await RunGitAsync(folder, ["init", "-q"], ct).ConfigureAwait(false));
        return init.Ok ? Done(await RunGitAsync(folder, ["symbolic-ref", "HEAD", $"refs/heads/{line}"], ct).ConfigureAwait(false)) : init;
    }

    public async Task<GitDone> CommitEmptyAsync(string folder, string name, string email, string message, CancellationToken ct) =>
        Done(await RunGitAsync(folder, [.. GitAuthor.Arguments((name, email)), "-c", "commit.gpgsign=false", "commit", "--allow-empty", "--no-verify", "-q", "-m", message], ct).ConfigureAwait(false));
}
