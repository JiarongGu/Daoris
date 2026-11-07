namespace Daoris.Driver;

/// <summary>One working tree as git lists it: where it is, the branch it has out, and whether it is locked.</summary>
/// <param name="Branch">The branch it has out, without <c>refs/heads/</c>; null for a detached one.</param>
/// <param name="LockReason">Why it is locked, as the person said it to <c>git worktree lock</c>; null where none is given.</param>
internal sealed record WorktreeEntry(string Path, string? Branch, bool Locked, string? LockReason);
