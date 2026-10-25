namespace Daoris.Driver;

/// <summary>Why a piece is refused, held or uncertain, by code: what the state file's <c>code</c> says and the note's <c>why</c> carries.</summary>
public static class PartnerWhy
{
    /// <summary>The asker is a partner, a helper, a reviewer, an intake, Ask Daoris's conversation or a brainstorm pass.</summary>
    public const string AskerUnfit = "asker-unfit";

    /// <summary>The asking session is not on this machine's record.</summary>
    public const string AskerGone = "asker-gone";

    /// <summary>The piece is blank.</summary>
    public const string Blank = "blank";

    /// <summary>A path the ask names leads outside the repository.</summary>
    public const string PathOutside = "path-outside";

    /// <summary>The lead has no commit to grow from.</summary>
    public const string NoCommit = "no-commit";

    /// <summary>The ask's base commit is not in the repository: no fallback to another base.</summary>
    public const string BaseMissing = "base-missing";

    /// <summary>The lead already has two pieces open across its <c>from</c> chain.</summary>
    public const string TooMany = "too-many";

    /// <summary>The named agent is not one the person's rule lists.</summary>
    public const string NotListed = "not-listed";

    /// <summary>The agent hands no connector to its sessions.</summary>
    public const string NoConnector = "no-connector";

    /// <summary>No measured row holds the effective configuration a start would hand the agent.</summary>
    public const string NotMeasured = "not-measured";

    /// <summary>The partner's copy could not be made.</summary>
    public const string CopyFailed = "copy-failed";

    /// <summary>The partner's process could not be joined to its strict job, so it was never run.</summary>
    public const string NoJob = "no-job";

    /// <summary>The state file could not be read: the piece waits for the person, its copy untouched.</summary>
    public const string StateUnreadable = "state-unreadable";

    /// <summary>A restart found the partner's job unknown or not empty.</summary>
    public const string JobUnknown = "job-unknown";

    /// <summary>The piece's ref stands at a commit other than the keeper's.</summary>
    public const string RefMoved = "ref-moved";

    /// <summary>The import refused what the copy held, leaving nothing to hand (<see cref="PieceImport"/>).</summary>
    public const string ImportRefused = "import-refused";

    /// <summary>The partner left no work in its copy.</summary>
    public const string NoWork = "no-work";

    /// <summary>A restart cut the hand of its answer after the door was asked: the word may be on the lead's record.</summary>
    public const string HandCut = "hand-cut";
}
