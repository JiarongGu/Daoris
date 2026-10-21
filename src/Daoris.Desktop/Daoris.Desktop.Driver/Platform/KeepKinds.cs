namespace Daoris.Driver;

/// <summary>
/// The kinds of thing Daoris keeps, each with its rule (CLEANUP1, D195 point 1; the cleanup design §2.1): a table that describes each
/// kind and runs nothing. Each kind's removal is one typed operation in this library, which the look's pass, a route and the terminal
/// alone call alike (D194 point 4); the table names it and never calls it. A kind joins the table in the change that builds its rule:
/// KEEP1a's set-aside folders first, the downloads with KEEP1b, and the page's words for each id with the card that shows them (KEEP1c).
/// </summary>
public static class KeepKinds
{
    /// <summary>
    /// The trees the look moved aside (D88's AUTOTIDY1): its package folders, a <c>node_modules</c> beside a <c>package.json</c> git
    /// tracks, can be made again and go an hour after the move (<see cref="SetAsideSettle"/>, the cleanup design §5); the rest, git's
    /// own files, <c>bin/</c> and <c>obj/</c> among them, waits the folder's days. The person may set 1 to 90 days, or <c>whole</c>,
    /// which keeps every folder whole as before (KEEP1d).
    /// </summary>
    public static KeepKind SetAside { get; } = new(
        "set-aside", [HomeEntry.Trees.Name],
        "its package folders, a node_modules beside a package.json git tracks; nothing else of it",
        TimeSpan.FromDays(14), MinDays: 1, MaxDays: 90, MaySetWhole: true, Remover: "the slim, then the purge");

    /// <summary>How long a folder set aside waits after its move before its package folders go: a write by then keeps it whole (§5).</summary>
    public static readonly TimeSpan SetAsideSettle = TimeSpan.FromHours(1);

    /// <summary>Every kind with a rule that runs, in the order the card will show them.</summary>
    public static IReadOnlyList<KeepKind> Table { get; } = [SetAside];

    /// <summary>The kind of this id, or null where the table holds none.</summary>
    public static KeepKind? Named(string id) => Table.FirstOrDefault(kind => string.Equals(kind.Id, id, StringComparison.Ordinal));
}

/// <summary>One kind of thing Daoris keeps, as the table describes it (the cleanup design §2.1).</summary>
/// <param name="Id">Its id: <c>keeping.json</c>'s key, the card's row and the machine log's word.</param>
/// <param name="Entries">The entries of D172's table it covers, by name.</param>
/// <param name="MadeAgain">What of it can be made again, which alone goes before its days, in words.</param>
/// <param name="Days">Its default: how long it is kept.</param>
/// <param name="MinDays">The fewest days the person may set.</param>
/// <param name="MaxDays">The most days the person may set.</param>
/// <param name="MaySetWhole">Whether the person may keep it whole, as before its rule.</param>
/// <param name="Remover">Its removal's one typed operation, in words.</param>
public sealed record KeepKind(
    string Id, IReadOnlyList<string> Entries, string MadeAgain, TimeSpan Days, int MinDays, int MaxDays, bool MaySetWhole, string Remover);

/// <summary>
/// The one effective policy (the cleanup design §2.1, Codex 5.2): the table's defaults under the person's choices, the only reader of a
/// kind's days. Every place that says or acts on the set-aside folders' days reads it: the purge, the look's report, the work inventory
/// and the terminal's trees list.
/// </summary>
/// <param name="SetAside">The set-aside folders' rule.</param>
public sealed record KeepPolicy(SetAsidePolicy SetAside)
{
    /// <summary>The table's defaults.</summary>
    public static KeepPolicy Default { get; } = new(new SetAsidePolicy(KeepKinds.SetAside.Days, Whole: false));

    /// <summary>
    /// The policy this home keeps by. Today the table's defaults: the person's choices in <c>driver.json</c>'s <c>keep</c> arrive with
    /// their editor and their terminal verb (KEEP1d), read here, so no reader changes then.
    /// </summary>
    public static KeepPolicy Of(string home)
    {
        ArgumentException.ThrowIfNullOrEmpty(home);
        return Default;
    }
}

/// <summary>The set-aside folders' rule: their days, and whether every folder is kept whole (no slim).</summary>
public sealed record SetAsidePolicy(TimeSpan Days, bool Whole);
