namespace Daoris.Driver;

/// <summary>
/// What one session's own tree holds that nobody has accepted yet (SESSUX1a, D126 §2.1): the fact Sessions' *To review*
/// is read from, by the clean-up's own proof (D88).
/// </summary>
public sealed partial class SessionTrees
{
    /// <summary>
    /// The tree's uncommitted paths and the commits on it no branch of the person's holds (<see cref="UnlandedAsync"/>);
    /// null where there is no tree of this home's to ask.
    /// </summary>
    /// <remarks>
    /// <para><b>Only a tree this home opened, still here, and a repository of its own.</b> 🔴 git walks UP: a folder under
    /// the trees that is no repository of its own (a leftover, D109 §5) would answer for whatever repository encloses the
    /// home, so it is never asked, and nothing is said of it. The clean-up's leftovers are where such a folder goes.</para>
    ///
    /// <para><b>Reads, and never writes.</b> <c>git status</c> and <c>git log</c> in the tree; a count git cannot give is
    /// null, which the reader keeps as work, as the clean-up keeps what it cannot clear.</para>
    /// </remarks>
    public async Task<TreeWork?> WorkAsync(string tree, CancellationToken ct = default)
    {
        bool held;
        try
        {
            held = Holds(tree);
        }
        catch (Exception error) when (error is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }

        if (!held || TreeGone(tree) || !await WorkingTree.IsTopLevelAsync(tree, ct).ConfigureAwait(false)) return null;

        var (code, porcelain, _) = await WorkingTree.GitAsync(tree, ["status", "--porcelain"], ct).ConfigureAwait(false);
        int? uncommitted = code != 0
            ? null
            : porcelain.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Length;
        return new TreeWork(await UnlandedAsync(tree, "HEAD", ct).ConfigureAwait(false), uncommitted);
    }
}
