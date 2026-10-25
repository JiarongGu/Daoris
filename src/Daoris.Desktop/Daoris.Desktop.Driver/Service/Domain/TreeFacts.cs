namespace Daoris.Driver;

/// <summary>Where a checkout stands in its repository's history — what a feed's claim is made of.</summary>
/// <param name="Commit">The full SHA of HEAD.</param>
/// <param name="CommittedAt">The committer date, which is what orders two points in one history.</param>
/// <param name="Branch">The line this checkout is on; `(detached)` where it is on none.</param>
public sealed record TreeProvenance(string Commit, DateTimeOffset CommittedAt, string Branch)
{
    /// <summary>The form a person reads in a note.</summary>
    public string ShortCommit => TextBounds.ShortCommit(Commit, 8);
}

/// <summary>How this checkout's commit stands to one a deployment holds, as git answered (SYNC5a).</summary>
public enum TreeRelation
{
    /// <summary>This checkout's commit has the held one in its history: a fast-forward.</summary>
    Descends,

    /// <summary>The held commit has this checkout's in its history: the checkout is behind.</summary>
    Behind,

    /// <summary>Neither is in the other's history — two lines from a common past.</summary>
    Diverged,

    /// <summary>This checkout does not have the held commit, so git cannot say.</summary>
    Unknown,
}
