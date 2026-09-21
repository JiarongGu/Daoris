using System.Diagnostics;
using System.Text;

namespace Daoris.Driver;

/// <summary>Where a checkout stands in its repository's history — what a feed's claim is made of.</summary>
/// <param name="Commit">The full SHA of HEAD.</param>
/// <param name="CommittedAt">The committer date, which is what orders two points in one history.</param>
/// <param name="Branch">The line this checkout is on; `(detached)` where it is on none.</param>
public sealed record TreeProvenance(string Commit, DateTimeOffset CommittedAt, string Branch);

/// <summary>
/// The questions the driver asks a working tree: is it clean enough to spawn into, what landed while
/// a session ran, and — since D48 §6 — where in the repository's history this checkout stands. Asked
/// of git itself, because git's answer is the one that matters.
/// </summary>
/// <remarks>
/// <para>A session spawns only onto a clean tree (D46 §3): uncommitted changes are somebody's work in
/// flight — the lesson `reaching-in` was written from — and a tree that is not a git repository at
/// all cannot make that promise, so it refuses too.</para>
///
/// <para>The provenance half exists because two checkouts of one repository are two points in its
/// history, and a deployment fed by both must decide which one speaks. Only this side can answer it:
/// the deployment has no checkout and cannot ask git anything.</para>
/// </remarks>
public static class WorkingTree
{
    /// <summary>
    /// Where this checkout stands — or null when git cannot say, which is a tree that feeds nothing.
    /// </summary>
    public static async Task<TreeProvenance?> ProvenanceAsync(string root, CancellationToken ct = default)
    {
        // One call for both fields, so the commit and its time can never come from different commits —
        // a session landing work between two calls would otherwise produce a provenance describing
        // neither. %H is the SHA and %cI the committer date; %n separates them, because neither field
        // can contain a newline (unlike a commit message, which is deliberately not asked for here).
        var (code, stdout, _) = await GitAsync(root, ["log", "-1", "--format=%H%n%cI"], ct).ConfigureAwait(false);
        if (code != 0) return null;

        var lines = stdout.Trim().Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length != 2
            || !DateTimeOffset.TryParse(
                lines[1], null, System.Globalization.DateTimeStyles.RoundtripKind, out var committedAt))
        {
            return null;
        }

        var (branchCode, branchOut, _) = await GitAsync(root, ["rev-parse", "--abbrev-ref", "HEAD"], ct)
            .ConfigureAwait(false);
        var branch = branchCode == 0 ? branchOut.Trim() : "";

        return new(
            lines[0],
            committedAt,
            // A detached HEAD is on no line at all. Named rather than blank, because the deployment's
            // refusal quotes it back — and "fed from ``" is a sentence nobody can act on.
            branch is "" or "HEAD" ? "(detached)" : branch);
    }

    /// <summary>
    /// Which line this repository treats as canonical, as far as this checkout knows (D48 §6).
    /// </summary>
    /// <remarks>
    /// <para>Asked of the remote's own HEAD first, which is what a clone records and therefore the
    /// repository's own answer rather than this machine's guess. A checkout made with
    /// `git remote add` has no such ref, so the conventional names are tried in turn.</para>
    ///
    /// <para>Null when none of them exists — and null is PERMISSIVE at the deployment: a repository
    /// that never said which line is canonical has not asked for its branches to be judged, and
    /// refusing every feed from it would silence a real repository over a missing ref.</para>
    /// </remarks>
    public static async Task<string?> DefaultBranchAsync(string root, CancellationToken ct = default)
    {
        var (code, stdout, _) = await GitAsync(
            root, ["symbolic-ref", "--short", "refs/remotes/origin/HEAD"], ct).ConfigureAwait(false);
        if (code == 0 && stdout.Trim() is { Length: > 0 } pointed)
        {
            var slash = pointed.IndexOf('/');
            return slash < 0 ? pointed : pointed[(slash + 1)..];
        }

        foreach (var candidate in new[] { "main", "master" })
        {
            var (exists, _, _) = await GitAsync(
                root, ["rev-parse", "--verify", "--quiet", $"refs/heads/{candidate}"], ct).ConfigureAwait(false);
            if (exists == 0) return candidate;
        }

        return null;
    }

    /// <summary>Clean means: a git repository, with nothing uncommitted.</summary>
    public static async Task<(bool Clean, string Detail)> CleanAsync(string root, CancellationToken ct = default)
    {
        var (code, stdout, stderr) = await GitAsync(root, ["status", "--porcelain"], ct).ConfigureAwait(false);
        if (code != 0)
        {
            return (false, $"not a spawnable tree — git status failed: {FirstLine(stderr)}");
        }

        return string.IsNullOrWhiteSpace(stdout)
            ? (true, "clean")
            : (false, $"the working tree has uncommitted changes ({stdout.Trim().Split('\n').Length} paths) — "
                      + "somebody's work in flight; the driver holds rather than entangling a session with it.");
    }

    /// <summary>Where HEAD is now — the "before" a session's evidence is measured from.</summary>
    public static async Task<string?> HeadAsync(string root, CancellationToken ct = default)
    {
        var (code, stdout, _) = await GitAsync(root, ["rev-parse", "HEAD"], ct).ConfigureAwait(false);
        return code == 0 ? stdout.Trim() : null;
    }

    /// <summary>What landed since — the reviewable half of the record (D46 §4).</summary>
    public static async Task<string> CommitsSinceAsync(string root, string? before, CancellationToken ct = default)
    {
        var range = before is null ? "HEAD" : $"{before}..HEAD";
        var (code, stdout, _) = await GitAsync(root, ["log", "--oneline", range], ct).ConfigureAwait(false);
        if (code != 0) return "no commits readable";

        var commits = stdout.Trim();
        return commits.Length == 0 ? "no commits landed" : $"commits landed:\n{commits}";
    }

    /// <summary>One file in a session's landed work, and the patch that changed it.</summary>
    /// <param name="Path">Repository-relative, as git spells it.</param>
    /// <param name="Status">`added`, `modified`, `deleted`, `renamed` — git's letter, said in words.</param>
    /// <param name="Added">Lines added, or null where git counted none because the file is binary.</param>
    /// <param name="Removed">Lines removed, same.</param>
    /// <param name="Patch">The unified diff for this file, or null when it was dropped by the bound.</param>
    public sealed record DiffFile(
        string Path, string Status, int? Added, int? Removed, string? Patch);

    /// <summary>
    /// A session's landed work: which files changed between the commit the tree stood at when the
    /// spawn began and where it stands now, with the patch for each.
    /// </summary>
    /// <param name="Files">Every changed file, always — the LIST is never truncated, only the patches.</param>
    /// <param name="Truncated">What the bound dropped, said out loud, or null when nothing was.</param>
    /// <param name="Base">The commit the range is measured from.</param>
    public sealed record TreeDiff(
        IReadOnlyList<DiffFile> Files, string? Truncated, string Base);

    /// <summary>What a surface will render before a person stops reading and git on disk has the rest.</summary>
    /// <remarks>
    /// The console's rule, applied again (design §5): a diff has no upper size, so the bound is stated
    /// rather than hidden. It drops whole PATCHES rather than cutting one mid-hunk — half a hunk reads
    /// as a diff and is not one — and the file list survives intact, so the person always learns that
    /// a file changed even when they cannot read how.
    /// </remarks>
    public const int PatchBudget = 400_000;

    /// <summary>The per-file cap, so one generated file cannot spend the whole budget alone.</summary>
    public const int PatchCap = 80_000;

    /// <summary>
    /// What landed in this tree since <paramref name="before"/> — the reviewable half, as a diff.
    /// </summary>
    /// <remarks>
    /// <para><b>Committed work only.</b> The range is `before..HEAD`, which is exactly what the
    /// evidence string counts, so the two can never disagree about what the session did. Uncommitted
    /// changes in the tree are deliberately not here: a chat may open on a dirty tree (D49 §3), so
    /// what is uncommitted is not knowably the session's, and attributing somebody else's work in
    /// flight to an agent is the `reaching-in` mistake in a read-only disguise.</para>
    ///
    /// <para>Null when git cannot answer — no repository, an unknown base, a tree that has moved
    /// away. A caller says so rather than showing an empty diff, which reads as "it changed
    /// nothing".</para>
    /// </remarks>
    public static async Task<TreeDiff?> DiffAsync(
        string root, string before, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(before)) return null;

        // 🔴 git WALKS UP. Run it in a directory that is not a repository and it answers for whatever
        // repository encloses it — so a session tree that has since been deleted would quietly return
        // the diff of the parent checkout, presented as that session's work. Found by a test that
        // pointed at an empty directory inside this repository and got a clean exit code back.
        // Requiring the top level to BE this path also rejects a subdirectory, which the driver never
        // passes and which would silently narrow the review if it ever did.
        var (topCode, topOut, _) = await GitAsync(
            root, ["rev-parse", "--show-toplevel"], ct).ConfigureAwait(false);
        if (topCode != 0 || !SamePath(topOut.Trim(), root)) return null;

        // `--numstat` and `--name-status` in one pass would need parsing two formats out of one
        // stream; two cheap calls read plainly and cannot mis-align, because each is keyed by path.
        var (statusCode, statusOut, _) = await GitAsync(
            root, ["diff", "--name-status", "-M", $"{before}..HEAD"], ct).ConfigureAwait(false);
        if (statusCode != 0) return null;

        var (numCode, numOut, _) = await GitAsync(
            root, ["diff", "--numstat", "-M", $"{before}..HEAD"], ct).ConfigureAwait(false);

        var counts = new Dictionary<string, (int? Added, int? Removed)>(StringComparer.Ordinal);
        if (numCode == 0)
        {
            foreach (var line in numOut.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = line.Trim().Split('\t');
                if (parts.Length < 3) continue;
                // git writes `-` for a binary file rather than a count. Null, not zero: "not counted"
                // and "counted nothing" are different answers.
                var added = int.TryParse(parts[0], out var a) ? a : (int?)null;
                var removed = int.TryParse(parts[1], out var r) ? r : (int?)null;
                counts[parts[^1]] = (added, removed);
            }
        }

        var files = new List<DiffFile>();
        var spent = 0;
        var dropped = 0;

        foreach (var line in statusOut.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = line.Trim().Split('\t');
            if (parts.Length < 2) continue;

            // A rename carries two paths; the one that exists now is the last.
            var path = parts[^1];
            var status = parts[0][0] switch
            {
                'A' => "added",
                'D' => "deleted",
                'R' => "renamed",
                'C' => "copied",
                _ => "modified",
            };

            var (added, removed) = counts.TryGetValue(path, out var count) ? count : (null, null);

            string? patch = null;
            if (spent < PatchBudget)
            {
                var (patchCode, patchOut, _) = await GitAsync(
                    root, ["diff", "-M", $"{before}..HEAD", "--", path], ct).ConfigureAwait(false);
                if (patchCode == 0 && patchOut.Length > 0)
                {
                    patch = patchOut.Length > PatchCap
                        ? patchOut[..PatchCap] + "\n… this file's patch is longer than the surface renders."
                        : patchOut;
                    spent += patch.Length;
                }
            }
            else
            {
                dropped++;
            }

            files.Add(new DiffFile(path, status, added, removed, patch));
        }

        var truncated = dropped == 0
            ? null
            : $"{dropped} more file{(dropped == 1 ? "" : "s")} changed; their patches are not shown here. "
              + "`git diff` in the tree has all of it.";

        return new TreeDiff(files, truncated, before);
    }

    /// <summary>
    /// Whether two paths name the same directory. git answers in forward slashes even on Windows,
    /// and a trailing separator or a differently-cased drive letter is the same place.
    /// </summary>
    private static bool SamePath(string left, string right)
    {
        if (left.Length == 0 || right.Length == 0) return false;

        try
        {
            return string.Equals(
                Path.TrimEndingDirectorySeparator(Path.GetFullPath(left)),
                Path.TrimEndingDirectorySeparator(Path.GetFullPath(right)),
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
        }
        catch (ArgumentException)
        {
            // An unrooted or malformed path is not the one we are standing in.
            return false;
        }
    }

    // Internal rather than private since D51: SessionTrees asks git the same way for the same reason —
    // one process-spawning implementation, not two that differ in encoding or error shape.
    internal static async Task<(int Code, string Stdout, string Stderr)> GitAsync(
        string root, IReadOnlyList<string> arguments, CancellationToken ct)
    {
        var info = new ProcessStartInfo
        {
            FileName = "git",
            WorkingDirectory = root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);

        try
        {
            using var process = Process.Start(info)
                ?? throw new DriverException("git did not start");
            var stdout = process.StandardOutput.ReadToEndAsync(ct);
            var stderr = process.StandardError.ReadToEndAsync(ct);
            await process.WaitForExitAsync(ct).ConfigureAwait(false);
            return (process.ExitCode, await stdout.ConfigureAwait(false), await stderr.ConfigureAwait(false));
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            return (-1, "", error.Message);
        }
    }

    private static string FirstLine(string text)
    {
        var trimmed = text.Trim();
        var newline = trimmed.IndexOf('\n');
        return newline < 0 ? trimmed : trimmed[..newline].Trim();
    }
}
