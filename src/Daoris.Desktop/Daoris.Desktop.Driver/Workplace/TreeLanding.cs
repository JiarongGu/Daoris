using System.Text;
using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>What came of a press. A refusal is an answer, as the merge door's are, and names what the person would do.</summary>
/// <param name="Plugin">What the landing's plugin answered once the branch was made (D100) — null where no plugin was spoken to.</param>
public sealed record TreeLanding(bool Landed, string Message, string? Branch = null, PluginLanding? Plugin = null)
{
    /// <summary>
    /// A refusal's code (LAND2b), one of <see cref="AutoLandingCode"/>: <c>uncommitted</c>, <c>nothing</c>, <c>exists</c> or, where a
    /// chain's branch was merged, <c>completed</c> (LAND2c) where the branch form said so, else null, which a landing at done keeps
    /// as <c>refused</c>. The message says it to a person.
    /// </summary>
    public string? Refusal { get; init; }

    /// <summary>
    /// Why the rule's plugin could not land work here, where a landing at done made the branch without it (LAND2b, D145
    /// point 2): nobody is there to fix the plugin, so the branch is made and the push is not tried. Null at a press, which
    /// refuses before anything is made instead (D100).
    /// </summary>
    public string? Unready { get; init; }

    /// <summary>
    /// Why its push was held back where its plugin could have pushed (FABRIC-CLOSE1h2): the automatic landing's quest was held, taken
    /// back or submitted again after the branch was made and before the plugin was told. The branch stands, recorded, and no plugin
    /// was spoken to. Null wherever the push went on, and at every other door, which asks no judgement there.
    /// </summary>
    public string? Withheld { get; init; }

    /// <summary>
    /// Where the landing moved a branch Daoris made on from (LAND2c, D149 point 2): the commit it stood at, recorded, before this
    /// session's work fast-forwarded it. Null for a branch made new, and for every merge.
    /// </summary>
    public string? AdvancedFrom { get; init; }

    /// <summary>
    /// What the landing left out because the line holds it, and what its replay found empty, oldest first (LANDSYNC1, D87's
    /// 2026-10-15 amendment point 4); empty where it replayed the commits onto a line that only moved (LANDREBASE1). Null where it
    /// landed them as made.
    /// </summary>
    public IReadOnlyList<LeftOutCommit>? LeftOut { get; init; }

    /// <summary>The replay's conflict, where that refused the landing (D87's amendment point 5); null otherwise.</summary>
    public LandingConflict? Conflict { get; init; }
}
