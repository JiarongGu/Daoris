namespace Daoris.Driver;

/// <summary>
/// An act's own ref: <c>refs/daoris/acts/&lt;act&gt;</c>, holding the blobs of
/// the index entries an act touched (and, from row j, Resolve's stages) until its record expires. It holds nothing for the clean-ups: no
/// holder of a branch's commits (<see cref="ContentHolds.AnyRef"/>), never counted by a landing's (<c>LandedCleanups</c>). Deleted branches,
/// tags and old tips keep their recovery under <see cref="ContentHolds.DiscardedPrefix"/>, never here.
/// </summary>
internal static class ActRefs
{
    /// <summary>The namespace the acts' refs are under.</summary>
    public const string Prefix = "refs/daoris/acts/";

    /// <summary>The ref of <paramref name="act"/>.</summary>
    public static string Of(string act) => Prefix + act;

    /// <summary>Whether <paramref name="reference"/> is an act's own ref.</summary>
    public static bool IsAct(string reference) => reference.StartsWith(Prefix, StringComparison.Ordinal);

    /// <summary>
    /// The act's ref let go of in one transaction, a <c>delete</c> at <paramref name="id"/>, the id its record journaled: refused, nothing
    /// moved, where it stands anywhere else.
    /// </summary>
    public static Task<RefRun> ExpireAsync(string root, string act, string id, CancellationToken ct) =>
        new RefTransaction().Delete(Of(act), id).RunAsync(root, ct);
}
