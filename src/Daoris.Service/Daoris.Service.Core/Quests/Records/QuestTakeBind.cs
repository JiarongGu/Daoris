namespace Daoris.Knowledge;

/// <summary>
/// What a take made of the closure graph the driver named for it (FABRIC-CLOSE1o, §E5's cutover), judged and consumed in the take's
/// own write: bound, or why not. Only <see cref="Bound"/> appends the bind; every other reading leaves the take unbound, closing by
/// its done as any quest.
/// </summary>
public enum QuestTakeBind
{
    /// <summary>No graph was named for this take: the owner's install, and every take before a closure graph.</summary>
    None,

    /// <summary>Bound in the take's own write.</summary>
    Bound,

    /// <summary>
    /// A pending row whose owner cannot be shown, written by a build before FABRIC-CLOSE1w in the table such builds write, whatever
    /// owner it names, since an older build's upsert there keeps another's owner: discarded.
    /// </summary>
    Unowned,

    /// <summary>Named for another session than the one taking.</summary>
    OtherSession,

    /// <summary>Named for a publication of this id since replaced, by a delete and the same ask again.</summary>
    Replaced,

    /// <summary>Named longer ago than <see cref="QuestStore.TakeBindLife"/> before the take.</summary>
    Expired,

    /// <summary>
    /// The quest's circle syncs through a remote now whose handshake does not say every machine of it knows a bound quest (FABRIC-CLOSE1i):
    /// enabled, or a machine heard undeclared, since the graph was named.
    /// </summary>
    Remote,
}
