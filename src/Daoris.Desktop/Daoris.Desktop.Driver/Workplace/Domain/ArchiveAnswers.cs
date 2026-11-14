namespace Daoris.Driver;

/// <summary>One session archived on this machine, and when.</summary>
public sealed record ArchiveMark(string Session, DateTimeOffset At);

/// <summary>What an archive did with one session asked of it.</summary>
public enum ArchiveVerdict
{
    /// <summary>Archived now, or already: its mark stands.</summary>
    Archived,

    /// <summary>Still running or waiting; stopped first, it ends, and then it may be archived.</summary>
    Live,

    /// <summary>Waiting on you, or with work to review: archive never hides what needs the person.</summary>
    NeedsYou,

    /// <summary>No record here is this session.</summary>
    Unknown,
}

/// <summary>One session's outcome, with the group that kept it where that is what refused it.</summary>
public sealed record ArchiveOutcome(string Session, ArchiveVerdict Verdict)
{
    /// <summary>For <see cref="ArchiveVerdict.NeedsYou"/>: <see cref="SessionGroup.NeedsYou"/>.</summary>
    public string? Group { get; init; }

    /// <summary>For <see cref="ArchiveVerdict.Live"/> of a record that itself ended while its line goes on: its state; else null.</summary>
    public string? Ended { get; init; }
}

/// <summary>An archive's outcomes, and the marks as they now stand.</summary>
public sealed record ArchiveAnswer(IReadOnlyList<ArchiveOutcome> Outcomes, IReadOnlyList<ArchiveMark> Marks);

/// <summary>The sessions an unarchive found not archived, said as information, and the marks as they now stand.</summary>
public sealed record UnarchiveAnswer(IReadOnlyList<string> NotArchived, IReadOnlyList<ArchiveMark> Marks);
