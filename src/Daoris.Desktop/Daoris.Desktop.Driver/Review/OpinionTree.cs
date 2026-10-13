namespace Daoris.Driver;

/// <summary>A reviewer's copy as it was made (XAGENT1d): where it is, and the candidate's tip it stands at.</summary>
public sealed record OpinionTreeOpened(string Path, string Tip);

/// <summary>
/// A second opinion's own tree (XAGENT1d, D155 point 5; the second-agent design §5.1): a clone of the repository at the
/// candidate's tip, made by Daoris under the home's trees, with no remote, and removed when the pass ends. Nothing in it is read
/// back: the only thing a pass takes is the opinion said through its tool.
/// </summary>
/// <remarks>
/// <para><b>A clone, never a worktree.</b> A worktree shares its repository's refs, so a branch or a tag the reviewer made would
/// be the person's. A clone keeps its refs, its configuration and its hooks to itself, and with its one remote removed it has
/// nowhere configured to push. <c>--no-hardlinks</c> copies the objects rather than linking them, so no file of the copy's object
/// store is one the repository holds too. What the copy cannot hold alone is a push that names the repository's own path: the
/// rules handed (<c>no-push</c>) and the instruction hold that one.</para>
///
/// <para><b>Where it lives</b>: <c>trees/&lt;workspace&gt;/&lt;repository&gt;/o-&lt;8 hex&gt;</c>, the layout a session tree has
/// (D51), so every reader of that layout finds a workspace and a repository where it looks, and the tree guard can hold the
/// reviewer to it. <c>o-</c> beside a session tree's <c>s-</c>.</para>
///
/// <para><b>Removed whatever happened</b>, read-only objects cleared first, which a recursive delete refuses on Windows. A folder
/// something still holds is said, never left silently.</para>
///
/// <para>Where a copy goes is here; making and removing it, <c>OpenAsync</c> and <c>RemoveAsync</c>, is Review's Infrastructure's,
/// named through this type as before (<see cref="OpinionTreeCopies"/>; STRUCT3c).</para>
/// </remarks>
public static class OpinionTree
{
    /// <summary>What a copy's folder name starts with, beside a session tree's <c>s-</c>.</summary>
    public const string Prefix = "o-";

    /// <summary>A new copy's folder for <paramref name="repository"/> in <paramref name="workspace"/>, the default one where none is named.</summary>
    public static string PathFor(string home, string? workspace, string repository) =>
        Path.Combine(
            home, "trees", workspace is { } named && !string.IsNullOrWhiteSpace(named) ? named.Trim() : RemoteTarget.DefaultWorkspace,
            repository.Trim(), Prefix + Guid.NewGuid().ToString("N")[..8]);
}
