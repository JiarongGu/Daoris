using System.Text;
using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>What came of a press. A refusal is an answer, as the merge door's are, and names what the person would do.</summary>
public sealed record TreeLanding(bool Landed, string Message, string? Branch = null)
{
    /// <summary>
    /// A refusal's code, one of <see cref="LandingCode"/>: <c>uncommitted</c>, <c>nothing</c>, <c>exists</c> or, where a
    /// chain's branch is merged, <c>completed</c> where the branch form says so, else null, which a landing at done keeps
    /// as <c>refused</c>. The message says it to a person.
    /// </summary>
    public string? Refusal { get; init; }

    /// <summary>
    /// Where the landing moved a branch Daoris made on from: the commit it stood at, recorded, before this
    /// session's work fast-forwarded it. Null for a branch made new, and for every merge.
    /// </summary>
    public string? AdvancedFrom { get; init; }

    /// <summary>
    /// What the landing left out because the line holds it, and what its replay found empty, oldest first; empty where it replayed the commits onto a line that only moved. Null where it
    /// landed them as made.
    /// </summary>
    public IReadOnlyList<LeftOutCommit>? LeftOut { get; init; }

    /// <summary>The replay's conflict, where that refused the landing; null otherwise.</summary>
    public LandingConflict? Conflict { get; init; }
}
