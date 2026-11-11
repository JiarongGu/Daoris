namespace Daoris.Driver;

/// <summary>The kinds of work a review record reads.</summary>
public static class ReviewKinds
{
    /// <summary>A session's branch worked apart, at its tip.</summary>
    public const string Branch = "branch";

    /// <summary>A git checkout worked in place, by its turns' intervals.</summary>
    public const string Checkout = "checkout";

    /// <summary>A folder worked in place, from the run's baseline to its newest turn's end.</summary>
    public const string Folder = "folder";

    /// <summary>A folder worked in a copy, against the copy's base.</summary>
    public const string Copy = "copy";

    /// <summary>A system's plan, by its record and number.</summary>
    public const string Plan = "plan";

    /// <summary>Work that left nothing to read but its handbacks.</summary>
    public const string Answer = "answer";
}

/// <summary>
/// What one attempt of a run's review read: the artifact by its kind's reference, the handbacks, the rows, and what could not be read,
/// written once when the attempt begins and never edited (<see cref="ReviewRecords"/>). A moved revision is a new attempt and a new record;
/// the older one stays, what an earlier Accept accepted.
/// </summary>
/// <param name="Kind">One of <see cref="ReviewKinds"/>.</param>
/// <param name="Revision">A digest over the reference and the handbacks' ids (<see cref="ReviewWork.RevisionOf"/>), which the review's binding holds.</param>
public sealed record ReviewRecord(string Run, string Node, int Attempt, string Kind, string Revision, DateTimeOffset At)
{
    /// <summary>The repository or department the run works in.</summary>
    public string Place { get; init; } = "";

    public ReviewReference Reference { get; init; } = new();

    /// <summary>Each quest of the run's chain here with its handback, in the chain's order.</summary>
    public IReadOnlyList<ReviewPart> Parts { get; init; } = [];

    /// <summary>The files a folder's or a copy's work added, changed or removed, each with its hashes; empty where it changed nothing.</summary>
    public IReadOnlyList<ReviewRow> Rows { get; init; } = [];

    /// <summary>What could not be read or shown, said apart from what changed nothing.</summary>
    public IReadOnlyList<ReviewGap> Incomplete { get; init; } = [];

    /// <summary>How far back the rows reach: <see cref="ReviewCoverage"/>.</summary>
    public string Coverage { get; init; } = ReviewCoverage.Whole;
}

/// <summary>The artifact a record names, by the fields its kind reads; the rest are null.</summary>
public sealed record ReviewReference
{
    /// <summary>A branch's name, its line and the tip the review is bound to.</summary>
    public string? Branch { get; init; }

    public string? Line { get; init; }

    public string? Tip { get; init; }

    /// <summary>A checkout's turns, each its <c>HEAD</c> at its start and end, in order: the commits between two turns are not this work's.</summary>
    public IReadOnlyList<ReviewInterval> Turns { get; init; } = [];

    /// <summary>A checkout's own uncommitted paths at its newest end, with their hashes.</summary>
    public IReadOnlyList<RunTurnPath> Uncommitted { get; init; } = [];

    /// <summary>The place's manifest store a folder's or a copy's manifests are kept in.</summary>
    public string? Workspace { get; init; }

    public string? Department { get; init; }

    /// <summary>The manifest record the rows are read from: the run's baseline, or the copy's base.</summary>
    public string? Baseline { get; init; }

    /// <summary>The record whose end manifest is the newest turn's, and that end's content digest.</summary>
    public string? End { get; init; }

    public string? EndDigest { get; init; }

    /// <summary>The copy a folder's work is done in.</summary>
    public string? Copy { get; init; }

    /// <summary>A system's plan: the record that kept it, its number, the system it is for and its content's digest.</summary>
    public string? PlanRecord { get; init; }

    public int? Plan { get; init; }

    public string? System { get; init; }

    public string? PlanDigest { get; init; }
}

/// <summary>One turn of a checkout, from its start's <c>HEAD</c> to its end's: commits in it were made while its turn ran, not proved made by it.</summary>
public sealed record ReviewInterval(string Record, DateTimeOffset Started, DateTimeOffset Ended, string? From, string? To);

/// <summary>One quest of the run's chain and its handback: its taker's submission, the requirements it carried, unchecked where it carried none.</summary>
public sealed record ReviewPart(string Quest, string Title)
{
    public string? Session { get; init; }

    /// <summary>The submission's machine and sequence: its id, so a corrected answer is a new one at the same content.</summary>
    public string? Machine { get; init; }

    public long? Sequence { get; init; }

    public DateTimeOffset? At { get; init; }

    public string? Note { get; init; }

    public string? Commit { get; init; }

    public IReadOnlyList<QuestRequirementView> Requirements { get; init; } = [];

    /// <summary>Nothing checked its done: its quest carried no requirement.</summary>
    public bool Unchecked { get; init; }
}

/// <summary>One file a folder's or a copy's work changed: <c>added</c>, <c>modified</c> or <c>deleted</c>, its hashes before and after, and its marks.</summary>
public sealed record ReviewRow(string Path, string Change)
{
    public string? Before { get; init; }

    public string? After { get; init; }

    /// <summary><see cref="ReviewMarks"/>: what is said of its attribution.</summary>
    public IReadOnlyList<string> Marks { get; init; } = [];
}

/// <summary>What a row says of its attribution, as far as it is known.</summary>
public static class ReviewMarks
{
    /// <summary>A path a later turn's start found changed since the turn before ended: a hash recovers no author.</summary>
    public const string Outside = "changed-outside-its-turns";

    /// <summary>A copy's file the folder changed too since the copy's making.</summary>
    public const string FolderToo = "changed-in-your-folder-too";
}

/// <summary>Something the record could not read or show, with the path it concerns where it is one file.</summary>
public sealed record ReviewGap(string Code, string? Path, string Said);

/// <summary>Why a part of the work is incomplete.</summary>
public static class ReviewGaps
{
    /// <summary>A file whose content could not be read, listed with no hash.</summary>
    public const string Unreadable = "unreadable";

    /// <summary>A file whose content the store did not keep, too large or not text: listed, its diff not drawn.</summary>
    public const string NotKept = "not-kept";

    /// <summary>A folder past the manifest's bounds: what changed in it is not followed.</summary>
    public const string OverBound = "over-bound";

    /// <summary>A part of the reference not there to read: a plan that does not read, a manifest gone.</summary>
    public const string NotRead = "not-read";
}

/// <summary>How far back a record's rows reach.</summary>
public static class ReviewCoverage
{
    /// <summary>From the run's first turn.</summary>
    public const string Whole = "since-it-started";

    /// <summary>From the earliest manifest still kept: the run's first start is gone.</summary>
    public const string Retained = "since-this-retained-baseline";
}
