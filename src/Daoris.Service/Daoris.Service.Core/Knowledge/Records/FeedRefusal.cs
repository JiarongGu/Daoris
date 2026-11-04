namespace Daoris.Knowledge;

/// <summary>Why a knowledge feed was not taken — or <see cref="None"/> when it was.</summary>
public enum FeedRefusal
{
    None,

    /// <summary>The repository has not declared `remote.join`, so nothing of it may leave its machine.</summary>
    NotJoined,

    /// <summary>Joined, but knowledge is a second declaration — its records travel and its lessons stay home.</summary>
    NotSharing,

    /// <summary>The feed named no commit, so nothing could be compared to what is already held.</summary>
    NoProvenance,

    /// <summary>
    /// Fed from a line that is not the repository's canonical one. A feature-branch checkout is work
    /// in flight: unmerged lessons are not yet the family's (D48 §6).
    /// </summary>
    NotDefaultBranch,

    /// <summary>
    /// Fed from a commit older than the one the deployment already holds. Not a problem — information:
    /// the index keeps the newer view, and the machine that is behind is simply behind.
    /// </summary>
    Stale,

    /// <summary>
    /// The feeding machine checked its ancestry against a commit another machine has since replaced
    /// (SYNC5a). Information: the next pass asks git again, against what is held then.
    /// </summary>
    Moved,

    /// <summary>
    /// The commit held, read differently (SYNC0c). The first reading stands, as information: two
    /// readings of one commit are two tools disagreeing, and flapping between them helps nobody.
    /// </summary>
    ContentDiffers,

    /// <summary>What was fed breaks the rules of its own shape — a code map judged whole at the door.</summary>
    Malformed,

    /// <summary>
    /// A declaration naming no commit, where one naming a commit is held (SYNC5b). Nothing orders the
    /// two, so the named one stands. Information: the repository is registered either way, and its
    /// records and quests travel.
    /// </summary>
    Unordered,
}
