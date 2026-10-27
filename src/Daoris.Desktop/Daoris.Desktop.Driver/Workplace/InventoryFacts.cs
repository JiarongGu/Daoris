namespace Daoris.Driver;

/// <summary>A recovery ref a content-held removal kept (<c>refs/daoris/discarded/&lt;branch&gt;</c>), by its repository.</summary>
public sealed record RecoveryRef(string Repository, string Ref)
{
    /// <summary>The commit it keeps, where the read named one.</summary>
    public string? Commit { get; init; }

    /// <summary>
    /// When the ref was made, its reflog's first entry (RECOVERYDATE1); for a ref with no reflog, its commit's date (<c>committerdate</c>,
    /// CLEANUP1), at or before it was made. Null where neither was read.
    /// </summary>
    public DateTimeOffset? Dated { get; init; }

    /// <summary>
    /// Whether a branch or a tag of the person's holds its commit by ancestry (CLEANUP1, <see cref="RecoveryRetention.Holds"/>): true where
    /// one does, false where none does, null where git could not say.
    /// </summary>
    public bool? Held { get; init; }
}

/// <summary>A <c>daoris/*</c> branch standing in a repository (FABRIC-UNKNOWN1), whether or not a record names it.</summary>
public sealed record StandingBranch(string Repository, string Branch);

/// <summary>What the records, the disk and git said, gathered for <see cref="WorkInventory.Derive"/> to read: nothing here is judged.</summary>
/// <param name="Branches">Every session branch entry, standing and traces.</param>
/// <param name="Landings">Every landing entry, standing and traces.</param>
/// <param name="Trees">The trees under the home's trees folder, and the folders moved aside.</param>
/// <param name="AutoLandings">The auto-landing entries.</param>
/// <param name="Refs">The recovery refs found in each repository read.</param>
/// <param name="InUse">The trees a session still running or waiting holds.</param>
/// <param name="HasHistory">Whether a session's record folder is on this machine.</param>
public sealed record InventoryFacts(
    IReadOnlyList<GrownBranch> Branches, IReadOnlyList<LandedBranch> Landings, IReadOnlyList<SessionTree> Trees,
    IReadOnlyList<AutoLanding> AutoLandings, IReadOnlyList<RecoveryRef> Refs, IReadOnlySet<string> InUse,
    Func<string, bool> HasHistory, DateTimeOffset Now)
{
    /// <summary>
    /// The <c>daoris/*</c> branches standing in each repository read (FABRIC-UNKNOWN1, the design's §D3): one that no record names and
    /// no tree holds is a unit of its own with an unknown owner, blocked. Read in the same git start as the recovery refs.
    /// </summary>
    public IReadOnlyList<StandingBranch> Standing { get; init; } = [];

    /// <summary>
    /// The trees this machine's sessions recorded, from their records, in the order they list them (OVERVIEWNOISE1b): each tree is joined
    /// to the session that recorded it, so its unit is named by that session, and a tree no standing branch record names is that
    /// session's rather than an unknown owner's. None given, none is joined, as before.
    /// </summary>
    public IReadOnlyList<RecordedTree> Recorded { get; init; } = [];

    /// <summary>
    /// How long a folder moved aside is kept, by the one effective policy (KEEP1a, <see cref="KeepPolicy"/>): its purge date is its move
    /// and this. The table's default where none is given.
    /// </summary>
    public TimeSpan SetAsideFor { get; init; } = KeepKinds.SetAside.Days;

    /// <summary>
    /// What each unit still owes beyond its branch records (FABRIC-PERFORM1d, <see cref="WorkObligations"/>), by its id: its plugins'
    /// receipts and its attempts not settled, joined to the unit of that id. None given, none joined.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyList<InventoryEntry>> Owed { get; init; } = new Dictionary<string, IReadOnlyList<InventoryEntry>>();
}
