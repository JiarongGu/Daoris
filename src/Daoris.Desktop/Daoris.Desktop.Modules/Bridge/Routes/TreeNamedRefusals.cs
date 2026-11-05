using Daoris.Driver;
using Shenora.Core.Ipc;

namespace Daoris.Desktop;

/// <summary>
/// A tree door asked with a tree's name where it takes a session (TREENAME1): the session whose tree carries that name is said, as the
/// terminal's alone path says it (<see cref="TreesWords.TreeNamedForSession"/>), in place of the door's sentence that the session names no
/// tree here. Records that cannot be read name nobody, and the door's own sentence stands.
/// </summary>
internal static class TreeNamedRefusals
{
    /// <summary>The verb's words: the session the tree is, or <paramref name="otherwise"/>.</summary>
    public static async Task<TreesSaid> SaidAsync(ServiceClient service, string id, TreesSaid otherwise, CancellationToken ct) =>
        TreesWords.TreeNamedForSession(await SessionBranchDoors.RecordsOrNoneAsync(service, ct).ConfigureAwait(false), id) ?? otherwise;

    /// <summary>The page's refusal, <see cref="Refusals.SessionNotReviewable"/>: the session the tree is, or <paramref name="otherwise"/>.</summary>
    public static async Task<ShenoraException> RefusalAsync(ServiceClient service, string id, string otherwise, CancellationToken ct)
    {
        var named = TreesWords.TreeNamedForSession(await SessionBranchDoors.RecordsOrNoneAsync(service, ct).ConfigureAwait(false), id);
        return Refusals.Because(Refusals.SessionNotReviewable, named is null ? otherwise : named.Error[0]["trees: ".Length..], ("session", id));
    }
}
