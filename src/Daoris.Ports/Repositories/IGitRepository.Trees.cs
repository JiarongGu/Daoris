namespace Daoris.Ports.Repositories;

/// <summary>One git an act's effect step asks, started suspended in the act's job.</summary>
/// <param name="Folder">The checkout it runs in.</param>
/// <param name="Ordinal">The step's place in its act, which the act's journal proves the git's pid under.</param>
/// <param name="Arguments">Git's arguments.</param>
/// <param name="Input">What is written to its standard input, or null.</param>
/// <param name="Environment">Variables set for this one git, or null.</param>
public sealed record GitHeldRun(string Folder, int Ordinal, IReadOnlyList<string> Arguments, string? Input, IReadOnlyDictionary<string, string>? Environment);

/// <summary>What one held git did: its exit, its words, and whether it outlived its bound or its end.</summary>
/// <param name="Exit">Its exit code, or null where it never ran or ended no way the act can read.</param>
/// <param name="Stdout">What it wrote.</param>
/// <param name="Stderr">What it said of itself.</param>
/// <param name="Unended">Whether its processes were still alive past the step's bound.</param>
/// <param name="Said">The act's own words for a start that did not happen or a stop that did not end.</param>
public sealed record GitHeldAnswer(int? Exit, string Stdout, string Stderr, bool Unended, string Said);

/// <summary>The process ring's start of one held git: the real repository calls it, a fake never does.</summary>
public delegate Task<GitHeldAnswer> GitHeldStart(GitHeldRun run);

/// <summary>Where a checkout's git directories stand, read as git reports them.</summary>
/// <param name="GitDir">The checkout's own git directory.</param>
/// <param name="CommonDir">The directory its refs and configuration are shared in.</param>
public sealed record GitLayout(string GitDir, string CommonDir);

/// <summary>A tree's uncommitted paths, its head commit and its branch, read in one ask.</summary>
/// <param name="Changed">The paths changed, unmerged or untracked, as <c>status --porcelain</c> counts them.</param>
/// <param name="Head">The head commit's id; null on an unborn branch.</param>
/// <param name="Branch">The branch the tree is on; null where its head is detached.</param>
public sealed record GitTreeStatus(int Changed, string? Head, string? Branch);

/// <summary>What one read of a tree's repository, working root and branch came to.</summary>
/// <param name="Code">Git's exit for the common directory, 0 where it read it.</param>
/// <param name="CommonDir">The common directory as git printed it, absolute.</param>
/// <param name="Err">Git's words where it failed.</param>
/// <param name="Branch">The branch by its short name, <c>HEAD</c> where detached; empty where not asked, or failing.</param>
/// <param name="Once">Whether one ask gave both, which says the branch's ask succeeded too.</param>
/// <param name="TopLevel">The working root git reports; empty where the one ask did not give it.</param>
public sealed record GitTreePlace(int Code, string CommonDir, string Err, string Branch, bool Once, string TopLevel);

/// <summary>The working root git reports for a folder, read fresh.</summary>
/// <param name="Done">Whether git reported one.</param>
/// <param name="Top">The root as git printed it; empty where it printed none.</param>
/// <param name="Error">Git's words where it did not.</param>
public sealed record GitFreshTop(bool Done, string Top, string Error);

/// <summary>One ref of a snapshot, with what a judgement reads of it.</summary>
/// <param name="Name">Its full name.</param>
/// <param name="Object">The object it names.</param>
/// <param name="Type">That object's type, <c>commit</c> for a branch.</param>
/// <param name="Upstream">The full name of the ref it tracks; empty where it tracks none.</param>
/// <param name="UpstreamShort">The tracked ref's short name; empty where it tracks none.</param>
/// <param name="Tree">The tree of the commit it names; empty where it names none.</param>
public sealed record GitRefFact(string Name, string Object, string Type, string Upstream, string UpstreamShort, string Tree);

public partial interface IGitRepository
{
    /// <summary>The branches under <c>refs/heads</c> and the remote-tracking ones of <c>origin</c>, each with its facts, in name order; null where git could not list them.</summary>
    Task<IReadOnlyList<GitRefFact>?> RefFactsAsync(string folder, CancellationToken ct);

    /// <summary>The refs whose history holds <paramref name="commit"/>, every namespace, a symbolic ref left out; null where git could not say.</summary>
    Task<IReadOnlyList<GitRef>?> HoldingRefsAsync(string folder, string commit, CancellationToken ct);

    /// <summary>The working root git reports for <paramref name="folder"/>, asked as written: never an answer a flow kept, since a door writes through the tree on it.</summary>
    Task<GitFreshTop> FreshTopAsync(string folder, CancellationToken ct);

    /// <summary>The tree's uncommitted paths, head and branch in one ask; null where git could not give them or answered in a shape not understood.</summary>
    Task<GitTreeStatus?> TreeStatusAsync(string folder, CancellationToken ct);

    /// <summary>How many paths <c>status --porcelain</c> lists (an untracked folder as one); null where git could not say.</summary>
    Task<int?> UncommittedAsync(string folder, CancellationToken ct);

    /// <summary>
    /// The commits of <paramref name="revision"/> that no branch of the person's holds, newest first, each as a <c>log --oneline</c> line: a local
    /// branch outside <c>daoris/</c> (and not <paramref name="except"/>) or a remote-tracking one outside <c>*/daoris/*</c> counts as holding them.
    /// Null where git could not list them.
    /// </summary>
    Task<IReadOnlyList<string>?> UnlandedAsync(string folder, string revision, string? except, CancellationToken ct);

    /// <summary>The tree's repository, working root and branch, as <see cref="GitTreePlace"/> says.</summary>
    Task<GitTreePlace> PlaceAsync(string folder, CancellationToken ct);

    /// <summary>Removes the working tree at <paramref name="path"/> as <c>worktree remove</c> does: refused where it holds changes or a lock, unless <paramref name="force"/>.</summary>
    Task<GitDone> RemoveTreeAsync(string folder, string path, bool force, CancellationToken ct);

    /// <summary>
    /// Runs one git of a journaled act's effect step. The real repository starts it through <paramref name="start"/>, which holds the act's
    /// job and journal and so proves the process; a fake answers the run itself and records it.
    /// </summary>
    Task<GitHeldAnswer> RunHeldAsync(GitHeldRun run, GitHeldStart start);

    /// <summary>Where the checkout's git directories stand; null where git could not say.</summary>
    Task<GitLayout?> LayoutAsync(string folder, CancellationToken ct);
}
