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
    /// which keeps every folder whole as before (KEEP1d, <see cref="KeepRules"/>).
    /// </summary>
    public static KeepKind SetAside { get; } = new(
        "set-aside", [HomeEntry.Trees.Name],
        "its package folders, a node_modules beside a package.json git tracks; nothing else of it",
        TimeSpan.FromDays(14), MinDays: 1, MaxDays: 90, MaySetWhole: true, Remover: "the slim, then the purge");

    /// <summary>How long a folder set aside waits after its move before its package folders go: a write by then keeps it whole (§5).</summary>
    public static readonly TimeSpan SetAsideSettle = TimeSpan.FromHours(1);

    /// <summary>
    /// The versions the agents and the tools downloaded (KEEP1b, the cleanup design §2.1): every one is made again by its next pin or use,
    /// and one nothing uses goes seven days into its current unused stretch (§2.3, <see cref="DownloadKeeping"/>), through CACHECLEAR1's
    /// act, which Tools' delete of one version takes too (<see cref="DownloadClearing"/>). The person may set 0 to 365 days, or
    /// <c>never</c>, which leaves them to the press alone (KEEP1d).
    /// </summary>
    public static KeepKind Downloads { get; } = new(
        "downloads", [HomeEntry.Toolchain.Name, HomeEntry.ToolVersions.Name],
        "every version, downloaded again by its next pin or use",
        TimeSpan.FromDays(7), MinDays: 0, MaxDays: 365, MaySetWhole: false, Remover: "CACHECLEAR1's act, which Tools' delete takes too")
    {
        MaySetNever = true,
    };

    /// <summary>
    /// The machine log (D94): nothing of it is made again, and each day's file goes thirty days on, by each process's prune at its open
    /// and by the look's pass once a day at the person's days (KEEP1d). The person may set 7 to 30 days: each process's own thirty stays
    /// the ceiling, since the service would otherwise read the driver's choices (the cleanup design §2.1).
    /// </summary>
    public static KeepKind Logs { get; } = new(
        "logs", [HomeEntry.Logs.Name], "nothing: each day's file is this machine's evidence",
        TimeSpan.FromDays(MachineLog.KeptDays), MinDays: 7, MaxDays: MachineLog.KeptDays, MaySetWhole: false,
        Remover: "the log's prune, each process's at its open and the look's daily at the person's days");

    /// <summary>
    /// Finished history (KEEP1g, D195 point 7, decided by the owner; the cleanup design §3.6): nothing of it is made again, and by default
    /// none goes by an age, the person's clear alone (the history clearing design §7). The person may set an age of 30 to 3,650 days: once a
    /// day each workspace's finished units whose last move is older go through the clear itself, every refusal holding
    /// (<see cref="HistoryKeeping"/>); <c>never</c> is the default again.
    /// </summary>
    public static KeepKind History { get; } = new(
        "history", [HomeEntry.Sessions.Name, HomeEntry.Workflows.Name], "nothing: a record cleared is gone from this machine",
        TimeSpan.Zero, MinDays: 30, MaxDays: 3650, MaySetWhole: false, Remover: "the clear, each refusal holding")
    {
        MaySetNever = true,
        NeverByDefault = true,
    };

    /// <summary>Every kind with a rule that runs, in the order the card will show them.</summary>
    public static IReadOnlyList<KeepKind> Table { get; } = [SetAside, Downloads, Logs, History];

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
    string Id, IReadOnlyList<string> Entries, string MadeAgain, TimeSpan Days, int MinDays, int MaxDays, bool MaySetWhole, string Remover)
{
    /// <summary>Whether the person may set <c>never</c>: none by its days, the press alone (the downloads', KEEP1b).</summary>
    public bool MaySetNever { get; init; }

    /// <summary>Whether its default is <c>never</c>, the press alone, so <see cref="Days"/> is not read (history's, KEEP1g).</summary>
    public bool NeverByDefault { get; init; }
}

/// <summary>
/// The one effective policy (the cleanup design §2.1, Codex 5.2): the table's defaults under the person's choices, the only reader of a
/// kind's days. Every place that says or acts on the set-aside folders' days reads it: the purge, the look's report, the work inventory
/// and the terminal's trees list; the downloads' days, the look's pass and the Downloads row (KEEP1b); the logs' days, the look's daily
/// prune and the card (KEEP1d); history's age, the look's daily clear and the card (KEEP1g).
/// </summary>
/// <param name="SetAside">The set-aside folders' rule.</param>
public sealed record KeepPolicy(SetAsidePolicy SetAside)
{
    /// <summary>The table's defaults.</summary>
    public static KeepPolicy Default { get; } = new(new SetAsidePolicy(KeepKinds.SetAside.Days, Whole: false));

    /// <summary>
    /// The policy where the person's choices do not read (KEEP1d): what keeps the most, never the least, since a failure keeps (the
    /// cleanup design §6 point 7): the set-aside folders whole for their most days, no download by its days, the logs their thirty, no
    /// history by an age.
    /// </summary>
    public static KeepPolicy Unread { get; } = new(new SetAsidePolicy(TimeSpan.FromDays(KeepKinds.SetAside.MaxDays), Whole: true))
    {
        Downloads = new(null),
        Logs = new(KeepKinds.Logs.Days),
    };

    /// <summary>The downloads' rule (KEEP1b): seven days by default; null is the person's <c>never</c>.</summary>
    public DownloadsPolicy Downloads { get; init; } = new(KeepKinds.Downloads.Days);

    /// <summary>The machine log's rule (KEEP1d): thirty days by default.</summary>
    public LogsPolicy Logs { get; init; } = new(KeepKinds.Logs.Days);

    /// <summary>Finished history's rule (KEEP1g): no age by default, the person's clear alone.</summary>
    public HistoryPolicy History { get; init; } = new(null);

    /// <summary>The policy the person's <paramref name="choices"/> make over the table's defaults.</summary>
    public static KeepPolicy From(KeepChoices choices) =>
        new(new SetAsidePolicy(choices.SetAsideDays is { } days ? TimeSpan.FromDays(days) : KeepKinds.SetAside.Days, choices.SetAsideWhole))
        {
            Downloads = new(choices.DownloadsNever ? null : choices.DownloadsDays is { } kept ? TimeSpan.FromDays(kept) : KeepKinds.Downloads.Days),
            Logs = new(choices.LogsDays is { } logs ? TimeSpan.FromDays(logs) : KeepKinds.Logs.Days),
            History = new(choices.HistoryDays is { } age ? TimeSpan.FromDays(age) : null),
        };

    /// <summary>
    /// The policy this home keeps by: the person's choices in <c>driver.json</c>'s <c>keep</c> (KEEP1d), at the path this home's loop
    /// resolves, over the table's defaults; <see cref="Unread"/> where that file does not read.
    /// </summary>
    public static KeepPolicy Of(string home)
    {
        ArgumentException.ThrowIfNullOrEmpty(home);
        try
        {
            return From(DriverConfig.Load(DriverConfig.ResolvePath(home)).Keep);
        }
        catch (Exception error) when (error is DriverConfigUnreadableException or HomeEntryTwiceException)
        {
            return Unread;
        }
    }
}

/// <summary>The set-aside folders' rule: their days, and whether every folder is kept whole (no slim).</summary>
public sealed record SetAsidePolicy(TimeSpan Days, bool Whole);

/// <summary>
/// The downloads' rule (KEEP1b): how long into its current unused stretch a version nothing uses is kept; null is <c>never</c>, the press
/// alone, below the floor too (KEEP1f).
/// </summary>
public sealed record DownloadsPolicy(TimeSpan? Days);

/// <summary>The machine log's rule (KEEP1d): how many days of files are kept, today included.</summary>
public sealed record LogsPolicy(TimeSpan Days);

/// <summary>
/// Finished history's rule (KEEP1g, decided by the owner): the age past a unit's last move at which the look's daily clear takes it; null,
/// the default, is <c>never</c>: nothing clears it by itself (the history clearing design §7 as D195 amends it).
/// </summary>
public sealed record HistoryPolicy(TimeSpan? Days);
