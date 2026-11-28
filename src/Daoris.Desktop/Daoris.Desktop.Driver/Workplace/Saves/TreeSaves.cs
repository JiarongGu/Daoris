using System.Text;
using static Daoris.Driver.TreeGit;

namespace Daoris.Driver;

/// <summary>
/// The save step, a unit of its own: a tree's head pinned under a kept ref in its repository, then its staged, unstaged and
/// untracked-not-ignored files and the ignored paths named as work committed onto that ref, and the record written. Putting a save back
/// is <see cref="TreeRestores"/>'; removing a tree behind one is <see cref="TreeSaveGate"/>'s check in the one remover.
/// </summary>
/// <remarks>
/// <para><b>Three commits under one ref.</b> The ref <c>refs/daoris/saves/&lt;name&gt;/&lt;n&gt;</c> holds the save commit: the head's
/// tree with every listed file as git reads it (its filters applied, so the landed proof compares it with the line), whose parents are the
/// head, the commit of the index as staged and the commit of every listed file's bytes as written with its mode (no filter, so a restore
/// writes them back byte for byte where a line-ending filter would rewrite them). Each is made through an index of its own beside the
/// tree's, so the tree's index and files are never written. The name is the branch, or a detached tree's folder, with every character a
/// ref folder could collide on as <c>-</c>: every save's ref is two names deep, so none is another's folder, and two saves that take one
/// number at once take the next.</para>
/// <para><b>Holds the tree when it fails.</b> A failed save is a typed refusal that says why and leaves the tree as it is; the head's pin
/// goes again where this save made it, a stop and a file it could not write included, and a save whose record could not be written keeps
/// its ref and says so. A file whose bytes move during its save is refused by its blob, which is hashed as it is read and again as git
/// stores it.</para>
/// </remarks>
public sealed class TreeSaves(string home, TimeProvider clock)
{
    /// <summary>The namespace the saves' refs are kept under.</summary>
    public const string Namespace = "refs/daoris/saves/";

    /// <summary>How many numbers a save tries where another save takes the one it read.</summary>
    private const int Tries = 8;

    /// <summary>The name a save's commits carry: they are this machine's, kept under its refs and never pushed.</summary>
    private static readonly IReadOnlyDictionary<string, string> Maker = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["GIT_AUTHOR_NAME"] = "Daoris", ["GIT_AUTHOR_EMAIL"] = "saves@daoris.invalid",
        ["GIT_COMMITTER_NAME"] = "Daoris", ["GIT_COMMITTER_EMAIL"] = "saves@daoris.invalid",
    };

    /// <summary>Run once the head is pinned, before anything else: a test's way to fail a save there.</summary>
    internal Func<CancellationToken, Task>? AfterPin { get; init; }

    /// <summary>Saves the tree at <paramref name="tree"/>, taking the ignored paths <paramref name="work"/> names as work, read literally.</summary>
    /// <param name="session">The session whose tree it is, where the caller knows one; the record carries it.</param>
    public async Task<SaveOutcome> SaveAsync(
        string tree, string workspace, string repository, IReadOnlyList<string> work, string? session = null, CancellationToken ct = default)
    {
        var began = clock.GetTimestamp();
        var (listing, refused) = await TreeSaveListing.ListAsync(tree, work, ct).ConfigureAwait(false);
        if (listing is null) return refused!;

        var name = RefName(listing.Branch ?? Path.GetFileName(listing.Tree));
        var (reference, unpinned) = await PinAsync(listing, name, ct).ConfigureAwait(false);
        if (reference is null) return unpinned!;

        var scratch = Path.Combine(listing.GitDir, $"index.daoris-save-{Guid.NewGuid():N}");
        (string Save, string Staged, string Raw)? commits = null;
        SaveOutcome? why;
        try
        {
            if (AfterPin is { } after) await after(ct).ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();
            (commits, why) = await CommitsAsync(listing, scratch, name, ct).ConfigureAwait(false);
            // A git a stop cut short answers a failure of its own: the stop is what is said.
            ct.ThrowIfCancellationRequested();
            if (commits is { } made)
            {
                var moved = await GitRuns.GitAsync(listing.Tree, ["update-ref", "-m", "daoris save", reference, made.Save, listing.Head], ct).ConfigureAwait(false);
                if (moved.Code != 0) (commits, why) = (null, SaveOutcome.No(SaveRefusal.Unread, $"git would not move `{reference}` to the save: {FirstLine(moved.Stderr)}"));
            }
        }
        catch (OperationCanceledException)
        {
            (commits, why) = (null, SaveOutcome.No(SaveRefusal.Stopped, "it was stopped before it finished, and its pin went again."));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            (commits, why) = (null, SaveOutcome.No(SaveRefusal.Unread, $"a file of its own could not be written or read: {error.Message}"));
        }
        finally
        {
            foreach (var file in new[] { ".i", ".r", ".s" }) Forget(scratch + file);
        }

        if (commits is not { } kept)
        {
            // The pin goes only while it is still this save's head, never a save another made under the name since.
            await GitRuns.GitAsync(listing.Tree, ["update-ref", "-d", reference, listing.Head], CancellationToken.None).ConfigureAwait(false);
            return why!;
        }

        var save = new TreeSave(
            reference, workspace, repository, Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(listing.CommonDir))!, listing.Tree, listing.Branch,
            listing.Head, kept.Save, kept.Staged, kept.Raw, work, listing.Files, listing.Files.Sum(file => file.Bytes),
            (long)clock.GetElapsedTime(began).TotalMilliseconds, clock.GetUtcNow()) { Session = session };
        try
        {
            new TreeSaveRecords(home).Keep(save);
        }
        catch (DriverException error)
        {
            return SaveOutcome.No(SaveRefusal.Unrecorded, $"`{reference}` holds the save, but its record could not be written: {error.Message}");
        }

        return SaveOutcome.Of(save);
    }

    /// <summary>
    /// The name a save's ref is kept under: <paramref name="name"/> with every character but letters, digits, <c>.</c>, <c>_</c> and
    /// <c>-</c> as <c>-</c>, so it is one ref folder deep and a valid one.
    /// </summary>
    internal static string RefName(string name)
    {
        var flat = new StringBuilder(name.Length);
        foreach (var each in name) flat.Append(char.IsAsciiLetterOrDigit(each) || each is '.' or '_' or '-' ? each : '-');
        var named = flat.ToString().Replace("..", "--", StringComparison.Ordinal).Trim('.');
        if (named.EndsWith(".lock", StringComparison.Ordinal)) named += "-";
        return named.Length == 0 ? "tree" : named;
    }

    /// <summary>The head pinned under the next free number of <paramref name="name"/>, made only where absent; the next where another save took it.</summary>
    private static async Task<(string? Reference, SaveOutcome? Refused)> PinAsync(SaveListing listing, string name, CancellationToken ct)
    {
        var prefix = $"{Namespace}{name}/";
        var said = "";
        for (var tried = 0; tried < Tries; tried++)
        {
            var kept = await GitRuns.GitAsync(listing.Tree, ["for-each-ref", "--format=%(refname)", prefix], ct).ConfigureAwait(false);
            if (kept.Code != 0) return (null, SaveOutcome.No(SaveRefusal.Unread, $"git could not list the saves under `{prefix}`: {FirstLine(kept.Stderr)}"));
            var number = kept.Stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(each => int.TryParse(each[Math.Min(prefix.Length, each.Length)..], out var n) ? n : 0).DefaultIfEmpty(0).Max() + 1;
            var reference = prefix + number;

            // The head first, under a ref that must not stand yet: from here a kill leaves the head kept, never less.
            var pin = await GitRuns.GitAsync(listing.Tree, ["update-ref", "-m", "daoris save: the head pinned", reference, listing.Head, ""], ct).ConfigureAwait(false);
            if (pin.Code == 0) return (reference, null);
            said = FirstLine(pin.Stderr);
            var taken = await GitRuns.GitAsync(listing.Tree, ["rev-parse", "--verify", "--quiet", reference], ct).ConfigureAwait(false);
            if (taken.Code != 0) break;
        }

        return (null, SaveOutcome.No(SaveRefusal.Unread, $"git would not pin the head under `{prefix}`: {said}"));
    }

    /// <summary>A dropped save's ref deleted, verified at the save's commit, so git may collect what only it held; null where it went, else git's words.</summary>
    internal static async Task<string?> DropRefAsync(TreeSave save, CancellationToken ct)
    {
        var gone = await GitRuns.GitAsync(save.Root, ["update-ref", "-m", "daoris save dropped", "-d", save.Id, save.Commit], ct).ConfigureAwait(false);
        return gone.Code == 0 ? null : FirstLine(gone.Stderr);
    }

    /// <summary>The tree of the index of the tree <paramref name="listing"/> read, written from a copy of it so the tree's own is never written; null where git could not.</summary>
    internal static async Task<string?> IndexTreeAsync(SaveListing listing, CancellationToken ct)
    {
        var copy = Path.Combine(listing.GitDir, $"index.daoris-check-{Guid.NewGuid():N}");
        try
        {
            var index = Path.Combine(listing.GitDir, "index");
            if (File.Exists(index)) File.Copy(index, copy);
            else if ((await Git(listing, copy, ["read-tree", listing.Head], null, ct).ConfigureAwait(false)).Code != 0) return null;
            var tree = await Git(listing, copy, ["write-tree"], null, ct).ConfigureAwait(false);
            return tree.Code == 0 ? tree.Out : null;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return null;
        }
        finally
        {
            Forget(copy);
        }
    }

    /// <summary>git in the listed tree, under a private index where one is named, with the saves' maker; its answer trimmed.</summary>
    private static async Task<(int Code, string Out, string Err)> Git(
        SaveListing listing, string? indexFile, IReadOnlyList<string> arguments, string? input, CancellationToken ct)
    {
        var environment = new Dictionary<string, string>(Maker, StringComparer.Ordinal);
        if (indexFile is not null) environment["GIT_INDEX_FILE"] = indexFile;
        var (code, stdout, stderr) = input is null
            ? await GitRuns.GitAsync(listing.Tree, arguments, environment, GitBounds.Work, ct).ConfigureAwait(false)
            : await GitRuns.GitAsync(listing.Tree, arguments, environment, input, ct).ConfigureAwait(false);
        return (code, stdout.Trim(), stderr);
    }

    /// <summary>The three commits: the index as staged, the save on it, and the files as written with the modes the save reads.</summary>
    private static async Task<((string Save, string Staged, string Raw)? Made, SaveOutcome? Refused)> CommitsAsync(
        SaveListing listing, string scratch, string name, CancellationToken ct)
    {
        var index = Path.Combine(listing.GitDir, "index");
        string staged = scratch + ".i", raw = scratch + ".r", saved = scratch + ".s";
        if (File.Exists(index))
        {
            File.Copy(index, staged);
            File.Copy(index, saved);
        }

        SaveOutcome Failed(string what, string stderr) => SaveOutcome.No(SaveRefusal.Unread, $"git could not {what}: {FirstLine(stderr)}");
        Task<(int Code, string Out, string Err)> Run(string? indexFile, IReadOnlyList<string> arguments, string? input = null) => Git(listing, indexFile, arguments, input, ct);

        foreach (var each in new[] { staged, saved })
        {
            if (!File.Exists(index) && (await Run(each, ["read-tree", listing.Head]).ConfigureAwait(false)) is { Code: not 0 } unread) return (null, Failed("read the head into an index", unread.Err));
        }

        var stagedTree = await Run(staged, ["write-tree"]).ConfigureAwait(false);
        if (stagedTree.Code != 0) return (null, Failed("write the index as a tree", stagedTree.Err));
        var stagedCommit = await Run(null, ["commit-tree", stagedTree.Out, "-p", listing.Head, "-m", $"daoris save of {name}: the index as staged"]).ConfigureAwait(false);
        if (stagedCommit.Code != 0) return (null, Failed("commit the index", stagedCommit.Err));

        var all = await Run(saved, ["add", "-A"]).ConfigureAwait(false);
        if (all.Code != 0) return (null, Failed("add the tree's files to the save", all.Err));
        if (listing.Ignored.Count > 0)
        {
            var forced = await Run(saved, ["--literal-pathspecs", "add", "-f", "--pathspec-from-file=-", "--pathspec-file-nul"], string.Join('\0', listing.Ignored)).ConfigureAwait(false);
            if (forced.Code != 0) return (null, Failed("add the ignored work to the save", forced.Err));
        }

        var saveTree = await Run(saved, ["write-tree"]).ConfigureAwait(false);
        if (saveTree.Code != 0) return (null, Failed("write the save as a tree", saveTree.Err));

        var written = listing.Files.Where(file => !file.Deleted).ToList();
        if (written.Count > 0)
        {
            var stored = await Run(null, ["hash-object", "-w", "--no-filters", "--stdin-paths"], string.Join('\n', written.Select(file => file.Path)) + "\n").ConfigureAwait(false);
            var blobs = stored.Out.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (stored.Code != 0 || blobs.Length != written.Count) return (null, Failed("store the files' bytes", stored.Err));
            for (var at = 0; at < written.Count; at++)
            {
                if (!string.Equals(blobs[at], written[at].Blob, StringComparison.Ordinal))
                {
                    return (null, SaveOutcome.No(SaveRefusal.Moving, $"{written[at].Path} changed while it was saved; save it again once nothing writes it."));
                }
            }

            // Each file's mode as the save reads it (git's own, the index's where the file system keeps none), so an executable stays one.
            var listed = await Run(saved, ["-c", "core.quotePath=false", "ls-files", "-s", "-z"]).ConfigureAwait(false);
            if (listed.Code != 0) return (null, Failed("read the save's modes", listed.Err));
            var modes = listed.Out.Split('\0', StringSplitOptions.RemoveEmptyEntries)
                .Select(entry => (Mode: entry[..6], Path: entry[(entry.IndexOf('\t') + 1)..]))
                .ToDictionary(entry => entry.Path, entry => entry.Mode, StringComparer.Ordinal);
            var placed = await Run(raw, ["update-index", "--add", "--index-info"],
                string.Concat(written.Select(file => $"{(modes.GetValueOrDefault(file.Path) == "100755" ? "100755" : "100644")} {file.Blob}\t{file.Path}\n"))).ConfigureAwait(false);
            if (placed.Code != 0) return (null, Failed("place the files' bytes in an index", placed.Err));
        }

        var rawTree = await Run(raw, ["write-tree"]).ConfigureAwait(false);
        var rawCommit = rawTree.Code != 0 ? rawTree : await Run(null, ["commit-tree", rawTree.Out, "-m", $"daoris save of {name}: the files as written"]).ConfigureAwait(false);
        if (rawCommit.Code != 0) return (null, Failed("commit the files' bytes", rawCommit.Err));

        var saveCommit = await Run(null, ["commit-tree", saveTree.Out, "-p", listing.Head, "-p", stagedCommit.Out, "-p", rawCommit.Out, "-m", $"daoris save of {name}"]).ConfigureAwait(false);
        if (saveCommit.Code != 0) return (null, Failed("commit the save", saveCommit.Err));
        return ((saveCommit.Out, stagedCommit.Out, rawCommit.Out), null);
    }

    private static void Forget(string file)
    {
        try
        {
            File.Delete(file);
            File.Delete(file + ".lock");
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // A scratch index left in the git folder is named for its act and harmless.
        }
    }
}
