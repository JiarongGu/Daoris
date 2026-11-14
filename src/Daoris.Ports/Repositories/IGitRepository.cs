using Daoris.Ports.Git;

namespace Daoris.Ports.Repositories;

/// <summary>One git as a repository's operations run it: in a folder, with these arguments, answered whole.</summary>
public delegate Task<GitAnswer> GitRun(string folder, IReadOnlyList<string> arguments, CancellationToken ct);

/// <summary>A ref and the commit it names.</summary>
/// <param name="Name">Its full name, <c>refs/heads/x</c>.</param>
/// <param name="Commit">The commit it names, by its full id.</param>
public sealed record GitRef(string Name, string Commit);

/// <summary>One working tree of a repository, as listed.</summary>
/// <param name="Path">Its folder, as one full spelling.</param>
/// <param name="Branch">The branch it has out, without <c>refs/heads/</c>; null for a detached one.</param>
/// <param name="Locked">Whether it is locked.</param>
/// <param name="LockReason">Why it is locked, as the lock states it; null where it states none.</param>
public sealed record GitWorktree(string Path, string? Branch, bool Locked, string? LockReason);

/// <summary>One entry of a commit's tree.</summary>
/// <param name="Mode">Its mode, <c>100644</c> for a file and <c>160000</c> for a submodule.</param>
/// <param name="Path">Its path from the tree's top, with <c>/</c>.</param>
public sealed record GitTreeEntry(string Mode, string Path);

/// <summary>What a repository's act came to: done, or git's words for why not.</summary>
public sealed record GitDone(bool Ok, string Error)
{
    public static GitDone Done { get; } = new(true, "");

    public static GitDone Not(string error) => new(false, error);
}

/// <summary>Where a switch goes, and how it treats what is in its way.</summary>
/// <param name="Branch">The branch it goes to, or the new branch it makes at <paramref name="Start"/>; null to detach.</param>
/// <param name="Start">The commit a new branch grows at, or the commit a detached switch goes to (null: where HEAD stands).</param>
/// <param name="Creates">Whether <paramref name="Branch"/> is made.</param>
/// <param name="KeepIgnored">Whether an ignored file in the way refuses the switch, not overwritten.</param>
/// <param name="HooksPath">A folder git's hooks are read from for this act alone (the repository's own are held), or null.</param>
public sealed record GitSwitch(string? Branch, string? Start = null, bool Creates = false, bool KeepIgnored = false, string? HooksPath = null);

/// <summary>A working tree to add: where, at what, and on which branch.</summary>
/// <param name="Path">Its folder.</param>
/// <param name="Start">The commit it is added at.</param>
/// <param name="Branch">The new branch it is made on at <paramref name="Start"/>; null for a detached tree.</param>
/// <param name="LockReason">The reason it is locked under from the first, or null for no lock.</param>
/// <param name="HooksPath">A folder git's hooks are read from for this act alone (the repository's own are held), or null.</param>
public sealed record GitWorktreeAdd(string Path, string Start, string? Branch = null, string? LockReason = null, string? HooksPath = null);

/// <summary>A tracked line that holds a name searched.</summary>
/// <param name="Path">The file, from the tree's top, with <c>/</c>.</param>
/// <param name="Line">The line's text.</param>
public sealed record GitMatch(string Path, string Line);

/// <summary>
/// A repository's operations at the level the application's families ask them: refs, commits, trees, status and working trees, each
/// with the answer a family reads, never git's whole surface. Every folder is a checkout of the repository, its own or a working
/// tree's. A read that git could not answer is null (or an empty list where a list is the answer and failure is told by null); a
/// write says whether it succeeded. Its integration, its working trees and its set-up are each a named part it joins.
/// </summary>
public interface IGitRepository : IGitIntegration, IGitTrees, IGitSetup
{
    /// <summary>The commit HEAD names in <paramref name="folder"/>, or null where it names none or git could not read it.</summary>
    Task<string?> HeadAsync(string folder, CancellationToken ct);

    /// <summary>The branch <paramref name="folder"/> stands on by its short name; null where HEAD is detached. A tree git cannot read is thrown.</summary>
    Task<string?> BranchAsync(string folder, CancellationToken ct);

    /// <summary>The commit <paramref name="revision"/> names, by its full id; null where it names none.</summary>
    Task<string?> CommitOfAsync(string folder, string revision, CancellationToken ct);

    /// <summary>The best common ancestor of two revisions; null where they share none or git could not say.</summary>
    Task<string?> MergeBaseAsync(string folder, string first, string second, CancellationToken ct);

    /// <summary>Whether <paramref name="ancestor"/> is <paramref name="descendant"/> or in its history; null where git could not say.</summary>
    Task<bool?> IsAncestorAsync(string folder, string ancestor, string descendant, CancellationToken ct);

    /// <summary>How many commits <paramref name="tip"/> reaches that none of <paramref name="exclude"/> does; null where git could not say.</summary>
    Task<int?> CountAsync(string folder, string tip, IReadOnlyList<string> exclude, CancellationToken ct);

    /// <summary>
    /// The refs under <paramref name="prefixes"/> (<c>refs/heads</c>) by name, those whose history holds <paramref name="contains"/> where it is
    /// given, no more than <paramref name="limit"/> where that is given; null where git could not say.
    /// </summary>
    Task<IReadOnlyList<GitRef>?> RefsAsync(string folder, IReadOnlyList<string> prefixes, string? contains, int? limit, CancellationToken ct);

    /// <summary>
    /// The entries of <paramref name="commit"/>'s tree under each of <paramref name="within"/> (every entry where it is empty): every level
    /// where <paramref name="recursive"/>, else only the files and folders of the top, or of the folder a path ending in <c>/</c> names
    /// (a folder is an entry of mode <c>040000</c>); null where git could not read it.
    /// </summary>
    Task<IReadOnlyList<GitTreeEntry>?> EntriesAsync(string folder, string commit, IReadOnlyList<string> within, bool recursive, CancellationToken ct);

    /// <summary>The text of the file at <paramref name="path"/> in <paramref name="commit"/>; null where there is none or it is not text.</summary>
    Task<string?> TextAtAsync(string folder, string commit, string path, CancellationToken ct);

    /// <summary>Whether git matches its ignore patterns in this checkout without regard to case (<c>core.ignorecase</c>).</summary>
    Task<bool> IgnoresCaseAsync(string folder, CancellationToken ct);

    /// <summary>The paths the checkout ignores, at their tops and never descending (a folder ends in <c>/</c>); thrown where git could not list them.</summary>
    Task<IReadOnlyList<string>> IgnoredTopsAsync(string folder, CancellationToken ct);

    /// <summary>The ignored tops under <paramref name="within"/> (a folder at its top with its <c>/</c>, else a file); thrown where git could not read the status.</summary>
    Task<IReadOnlyList<string>> IgnoredAsync(string folder, IReadOnlyList<string> within, CancellationToken ct);

    /// <summary>The working trees the repository lists; null where git could not list them, which is never an empty list.</summary>
    Task<IReadOnlyList<GitWorktree>?> WorktreesAsync(string folder, CancellationToken ct);

    /// <summary>Every path status names with its two-letter state, ignored ones at their tops where asked; thrown where git could not read it.</summary>
    Task<IReadOnlyDictionary<string, string>> StatusAsync(string folder, bool ignored, CancellationToken ct);

    /// <summary>The tracked paths marked assume-unchanged or skip-worktree; null where the index could not be read.</summary>
    Task<IReadOnlyList<string>?> HiddenAsync(string folder, CancellationToken ct);

    /// <summary>Moves a ref to <paramref name="to"/>, only where it stands at <paramref name="from"/> when that is given.</summary>
    Task<GitDone> UpdateRefAsync(string folder, string name, string to, string? from, CancellationToken ct);

    /// <summary>Switches the checkout in <paramref name="folder"/>; never forced, so work in the way refuses it.</summary>
    Task<GitDone> SwitchAsync(string folder, GitSwitch to, CancellationToken ct);

    /// <summary>Stages every change in the checkout and commits it with <paramref name="message"/>, hooks not run.</summary>
    Task<GitDone> CommitAllAsync(string folder, string message, CancellationToken ct);

    /// <summary>Locks the working tree at <paramref name="path"/> with <paramref name="reason"/>.</summary>
    Task<GitDone> LockAsync(string folder, string path, string reason, CancellationToken ct);

    /// <summary>Unlocks the working tree at <paramref name="path"/>.</summary>
    Task<GitDone> UnlockAsync(string folder, string path, CancellationToken ct);

    /// <summary>Forgets the working trees whose folders are gone.</summary>
    Task<GitDone> PruneAsync(string folder, CancellationToken ct);

    /// <summary>Adds a working tree as <paramref name="add"/> says; refused where its branch stands or its folder holds a tree.</summary>
    Task<GitDone> AddWorktreeAsync(string folder, GitWorktreeAdd add, CancellationToken ct);

    /// <summary>The working root git reports for <paramref name="folder"/>, as one full spelling; null where it is in no repository.</summary>
    Task<string?> TopOfAsync(string folder, CancellationToken ct);

    /// <summary>Moves the checkout's branch on to <paramref name="commit"/>, files included, only where it is a descendant of HEAD and no work in the files is in the way.</summary>
    Task<GitDone> FastForwardAsync(string folder, string commit, CancellationToken ct);

    /// <summary>Moves the checkout's branch, files and index to <paramref name="commit"/>, tracked changes lost; what is untracked stays.</summary>
    Task<GitDone> ResetHardAsync(string folder, string commit, CancellationToken ct);

    /// <summary>The branch the repository treats as its line as far as this checkout knows: the remote's own HEAD, else <c>main</c>, else <c>master</c>; null where none stands.</summary>
    Task<string?> DefaultBranchAsync(string folder, CancellationToken ct);

    /// <summary>Deletes <paramref name="branch"/> though its work is unmerged, with its configuration; refused where a working tree has it out.</summary>
    Task<GitDone> DeleteBranchAsync(string folder, string branch, CancellationToken ct);

    /// <summary>The paths that differ between two commits, by name alone and in path order; null where git could not say.</summary>
    Task<IReadOnlyList<string>?> ChangedAsync(string folder, string from, string to, CancellationToken ct);

    /// <summary>
    /// Every line of the text files <paramref name="pathspecs"/> name in <paramref name="commit"/> that holds one of <paramref name="names"/>,
    /// in path order; a pathspec may be a path, a folder ending in <c>/</c>, a <c>*</c> glob or <c>:(exclude)</c> then one. Thrown where git could not search.
    /// </summary>
    Task<IReadOnlyList<GitMatch>> GrepAsync(string folder, string commit, IReadOnlyCollection<string> names, IReadOnlyList<string> pathspecs, CancellationToken ct);

    /// <summary>Makes every act of the transaction or none: where an expectation fails, git's words say which and nothing moves.</summary>
    Task<GitRefRun> TransactAsync(string folder, GitRefTransaction transaction, CancellationToken ct);

    /// <summary>Puts each path back as HEAD holds it, in the files and the index.</summary>
    Task<GitDone> RestoreAsync(string folder, IReadOnlyList<string> paths, CancellationToken ct);

    /// <summary>Runs the repository's hook named <paramref name="hook"/> with <paramref name="arguments"/>; done where it is absent or ends clean.</summary>
    Task<GitDone> RunHookAsync(string folder, string hook, IReadOnlyList<string> arguments, CancellationToken ct);

    /// <summary>Removes the working tree at <paramref name="path"/> with its files.</summary>
    Task<GitDone> RemoveWorktreeAsync(string folder, string path, CancellationToken ct);

    /// <summary>The same repository whose every git runs through <paramref name="run"/>: a claim's, held in its own job. A fake answers itself.</summary>
    IGitRepository Held(GitRun run);
}
