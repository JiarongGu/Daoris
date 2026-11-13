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
    public static GitRepository Shared { get; } = new(WorkingTree.GitAsync);

    private readonly Runner RunGitAsync;

    internal GitRepository(Runner run) => RunGitAsync = run;

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

    public async Task<GitDone> AddWorktreeAsync(string folder, string path, string commit, string? lockReason, CancellationToken ct) =>
        Done(await RunGitAsync(folder, lockReason is null
            ? ["worktree", "add", "--quiet", "--detach", path, commit]
            : ["worktree", "add", "--quiet", "--detach", "--lock", "--reason", lockReason, path, commit], ct).ConfigureAwait(false));

    public async Task<GitDone> RemoveWorktreeAsync(string folder, string path, CancellationToken ct) =>
        Done(await RunGitAsync(folder, ["worktree", "remove", "--force", "--force", path], ct).ConfigureAwait(false));

    public IGitRepository Held(Ports.Repositories.GitRun run) => new GitRepository(async (folder, arguments, ct) =>
    {
        var answer = await run(folder, arguments, ct).ConfigureAwait(false);
        return (answer.Exit ?? -1, answer.Output, answer.Error);
    });

    private static GitDone Done((int Code, string Stdout, string Stderr) ran) => ran.Code == 0 ? GitDone.Done : GitDone.Not(ran.Stderr.Trim());
}
