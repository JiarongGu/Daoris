namespace Daoris.Knowledge;

/// <summary>
/// Whether two path globs can name one file. A glob is read up to its first wildcard: two globs overlap where one's literal head begins the
/// other's. It errs toward overlap, which costs an <c>orders</c> edge between two parts and never a lost claim.
/// </summary>
internal static class ClaimPaths
{
    /// <summary>Whether <paramref name="a"/> and <paramref name="b"/> can name one file.</summary>
    public static bool Overlap(string a, string b)
    {
        var (headA, headB) = (Head(a), Head(b));
        return headA.StartsWith(headB, StringComparison.Ordinal) || headB.StartsWith(headA, StringComparison.Ordinal);
    }

    /// <summary>Whether every file <paramref name="claim"/> can name is inside <paramref name="area"/>: a claim wider than the area is not within it.</summary>
    public static bool Within(string claim, string area)
    {
        var (headClaim, headArea) = (Head(claim), Head(area));
        var areaIsOpen = headArea.Length < area.Length;
        return headClaim.StartsWith(headArea, StringComparison.Ordinal) && (areaIsOpen || headClaim.Length == headArea.Length && claim == area);
    }

    private static string Head(string glob)
    {
        var wildcard = glob.IndexOfAny(['*', '?', '[', '{']);
        return wildcard < 0 ? glob : glob[..wildcard];
    }
}
