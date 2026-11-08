namespace Daoris.Driver;

/// <summary>What a layer below the winning one said of a path: the layer's word and the kind it named.</summary>
public sealed record FilesLookBelow(string Layer, string Kind);

/// <summary>
/// One path git does not manage as the page shows it: its effective rule's kind and the layer that decided (both null where git
/// tracks it or no rule names it), the floor's code where the floor decided, whether a copy is made, a code for why a dependency acts
/// on nothing, what the analysis counted and saw, and the layers below. Never a machine path: every path is the repository's own.
/// </summary>
public sealed record FilesLookFile(
    string Path, string? Kind, string? Layer, string? Floor, bool Copy, string? Idle, long? Files, long? Bytes, bool More,
    IReadOnlyList<string> Evidence, IReadOnlyList<FilesLookBelow> Below);

/// <summary>One act the effective rules would make in a tree, <c>run(…)</c>, <c>copy(…)</c> or <c>share(…)</c>, and the person's answer: made, denied or held.</summary>
public sealed record FilesLookAct(string Act, string Path, string Verdict);

/// <summary>An allow the person's file holds and who gave it, null where the file names none.</summary>
public sealed record FilesLookAllow(string Rule, string? By);

/// <summary>One scope of the person's permissions, the machine's or the repository's.</summary>
public sealed record FilesLookPermissions(string Scope, IReadOnlyList<string> Deny, IReadOnlyList<string> Ask, IReadOnlyList<FilesLookAllow> Allow);

/// <summary>One entry of the person's own, in the machine's scope or the repository's.</summary>
public sealed record FilesLookRule(string Scope, string Kind, IReadOnlyList<string> Paths, bool? Copy);

/// <summary>A path of the person's file git's syntax cannot state, read as written, with its two git wordings.</summary>
public sealed record FilesLookKept(string Scope, string Kind, string Path, IReadOnlyList<string> Whole, IReadOnlyList<string> Files);

/// <summary>
/// A repository's rule file read whole for the page (row 6b): each path's effective rule, the acts and the person's answers, the
/// person's rules and permissions, and what does not read. <see cref="Unread"/> is why the person's file stands and does not read;
/// <see cref="Kept"/> the person's paths read as written until worded in git's syntax, and <see cref="FirstForm"/> whether the
/// repository's own file says no form and is read as the first form.
/// </summary>
public sealed record FilesLook(
    string Repository, string Commit, string? FoundCommit, DateTimeOffset? FoundAt,
    IReadOnlyList<FilesLookFile> Files, IReadOnlyList<FilesLookAct> Acts, IReadOnlyList<FilesLookPermissions> Permissions,
    IReadOnlyList<FilesLookRule> Rules, string? Unread, IReadOnlyList<string> Problems)
{
    public IReadOnlyList<FilesLookKept> Kept { get; init; } = [];

    public bool FirstForm { get; init; }
}
