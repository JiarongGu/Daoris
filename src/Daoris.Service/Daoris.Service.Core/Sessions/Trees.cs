namespace Daoris.Knowledge;

/// <summary>
/// The working tree a session holds — the unit of exclusion since D51 — and the two rules every layer
/// must agree on: what silence means, and when two paths are the same tree.
/// </summary>
/// <remarks>
/// <para><b>The tree is the unit of exclusion, and a repository may have more than one</b> (D51).
/// "One active session per repository" was two claims welded together: *two agents in one working tree
/// corrupt each other's git state*, which is the reason and does not move, and *a repository has one
/// working tree*, which was only ever a fact about how the registry was built. This type carries the
/// half that is now a value.</para>
///
/// <para><b>Silence means the repository's main tree, whose path this deployment may not know.</b>
/// A record from before D51, or one mirrored from another machine (which rightly sends no path), says
/// nothing — and the lock reads that as "possibly the tree you are asking about". Unknown is
/// conservative on purpose: the cost of refusing wrongly is a sentence naming what holds the tree, and
/// the cost of permitting wrongly is two agents in one working tree.</para>
///
/// <para><b>Convergence comes from one source, not from clever comparison.</b> Both doors resolve an
/// unstated tree through the registration's root, so two sessions in one tree carry byte-identical
/// strings by construction. <see cref="Normalize"/> is the safety net for the ways a path is spelled
/// rather than the mechanism — and it deliberately does not fold case: a deployment may run where
/// paths are case-sensitive, and two genuinely different trees must not collapse into one.</para>
/// </remarks>
public static class Trees
{
    /// <summary>
    /// A tree path as it is stored and compared: trimmed, without a trailing separator, and null when
    /// nothing was said. A trailing slash is not a different tree, and a comparison that said so would
    /// hand two agents one working tree on a technicality.
    /// </summary>
    public static string? Normalize(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;

        var trimmed = path.Trim().TrimEnd('/', '\\');
        // A root path is all separator — `/` or `C:\` — and trimming it away would turn a real tree
        // into an unstated one, which the lock reads as "every tree in the repository".
        return trimmed.Length == 0 ? path.Trim() : trimmed;
    }
}
