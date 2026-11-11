namespace Daoris.Driver;

/// <summary>
/// The kinds of thing Daoris keeps, each with its rule: a table that describes each
/// kind and runs nothing. Each kind's removal is one typed operation in this library, which the look's pass, a route and the terminal
/// alone call alike; the table names it and never calls it. A kind joins the table in the change that builds its rule,
/// and the page's words for each id come with the card that shows them.
/// </summary>
public static class KeepKinds
{
    /// <summary>
    /// The trees the look moved aside: its package folders, a <c>node_modules</c> beside a <c>package.json</c> git
    /// tracks, can be made again and go an hour after the move (<see cref="SetAsideSettle"/>); the rest, git's
    /// own files, <c>bin/</c> and <c>obj/</c> among them, waits the folder's days. The person may set 1 to 90 days, or <c>whole</c>,
    /// which keeps every folder whole as before (<see cref="KeepRules"/>).
    /// </summary>
    public static KeepKind SetAside { get; } = new(
        "set-aside", [HomeEntry.Trees.Name],
        "its package folders, a node_modules beside a package.json git tracks; nothing else of it",
        TimeSpan.FromDays(14), MinDays: KeepKey.SetAsideMinDays, MaxDays: KeepKey.SetAsideMaxDays, MaySetWhole: true, Remover: "the slim, then the purge")
    {
        DefaultGb = 8,
    };

    /// <summary>How long a folder set aside waits after its move before its package folders go: a write by then keeps it whole.</summary>
    public static readonly TimeSpan SetAsideSettle = TimeSpan.FromHours(1);

    /// <summary>
    /// The versions the agents and the tools downloaded: every one is made again by its next pin or use,
    /// and one nothing uses goes seven days into its current unused stretch (<see cref="DownloadKeeping"/>), through the
    /// clearing act, which Tools' delete of one version takes too (<see cref="DownloadClearing"/>). The person may set 0 to 365 days, or
    /// <c>never</c>, which leaves them to the press alone.
    /// </summary>
    public static KeepKind Downloads { get; } = new(
        "downloads", [HomeEntry.Toolchain.Name, HomeEntry.ToolVersions.Name],
        "every version, downloaded again by its next pin or use",
        TimeSpan.FromDays(7), MinDays: KeepKey.DownloadsMinDays, MaxDays: KeepKey.DownloadsMaxDays, MaySetWhole: false, Remover: "CACHECLEAR1's act, which Tools' delete takes too")
    {
        MaySetNever = true,
        DefaultGb = 5,
    };

    /// <summary>
    /// The machine log: nothing of it is made again, and each day's file goes thirty days on, by each process's prune at its open
    /// and by the look's pass once a day at the person's days. The person may set 7 to 30 days: each process's own thirty stays
    /// the ceiling, since the service would otherwise read the driver's choices.
    /// </summary>
    public static KeepKind Logs { get; } = new(
        "logs", [HomeEntry.Logs.Name], "nothing: each day's file is this machine's evidence",
        TimeSpan.FromDays(MachineLog.KeptDays), MinDays: KeepKey.LogsMinDays, MaxDays: KeepKey.LogsMaxDays, MaySetWhole: false,
        Remover: "the log's prune, each process's at its open and the look's daily at the person's days");

    /// <summary>
    /// Finished history (decided by the owner): nothing of it is made again, and by default
    /// none goes by an age, the person's clear alone. The person may set an age of 30 to 3,650 days: once a
    /// day each workspace's finished units whose last move is older go through the clear itself, every refusal holding
    /// (<see cref="HistoryKeeping"/>); <c>never</c> is the default again.
    /// </summary>
    public static KeepKind History { get; } = new(
        "history", [HomeEntry.Sessions.Name, HomeEntry.Workflows.Name], "nothing: a record cleared is gone from this machine",
        TimeSpan.Zero, MinDays: KeepKey.HistoryMinDays, MaxDays: KeepKey.HistoryMaxDays, MaySetWhole: false, Remover: "the clear, each refusal holding")
    {
        MaySetNever = true,
        NeverByDefault = true,
    };

    /// <summary>
    /// The browser's caches: made again by browsing, so only a size limit holds them, judged once a day
    /// while the browser does not run and at the browser's start; the engine's own rule ages them, so no days are set.
    /// </summary>
    public static KeepKind BrowserCache { get; } = new(
        "browser-cache", [HomeEntry.BrowserEngine.Name], "its caches, never its cookies, storage or preferences",
        TimeSpan.Zero, MinDays: 0, MaxDays: 0, MaySetWhole: false, Remover: "the browser's cache clearing, while the browser does not run")
    {
        DefaultGb = 2,
        SizeOnly = true,
    };

    /// <summary>
    /// The dependency store's entries: each is installed again at its key's next miss, so one not placed or sealed for thirty days goes,
    /// then, over its size limit, the one placed longest ago, once a day (<see cref="DepsKeeping"/>). A placed copy is its tree's. Its rule
    /// is the table's alone: the person sets none of it.
    /// </summary>
    public static KeepKind Deps { get; } = new(
        "deps", [HomeEntry.Deps.Name], "every entry, installed again at its key's next miss",
        TimeSpan.FromDays(30), MinDays: 30, MaxDays: 30, MaySetWhole: false, Remover: "the store's eviction")
    {
        DefaultGb = 10,
    };

    /// <summary>The salvage folder: each migration's copies, its undo; one whose newest file is thirty days old goes daily (<see cref="SalvageKeeping"/>), and the person's press takes one early.</summary>
    public static KeepKind Salvage { get; } = new(
        "salvage", [HomeEntry.Salvage.Name], "nothing: a folder is the only copy of what its migration changed",
        TimeSpan.FromDays(30), MinDays: 30, MaxDays: 30, MaySetWhole: false, Remover: "the folder's removal, never through a link");

    /// <summary>The most Daoris may use, in GB, by default: what it holds in all (the allowance over the whole).</summary>
    public const int DefaultAllowanceGb = 48;

    /// <summary>Every kind whose rule runs and the person sets, in the order the card shows them.</summary>
    public static IReadOnlyList<KeepKind> Table { get; } = [SetAside, Downloads, BrowserCache, Logs, History];

    /// <summary>The kind of this id, or null where the table holds none.</summary>
    public static KeepKind? Named(string id) => Table.FirstOrDefault(kind => string.Equals(kind.Id, id, StringComparison.Ordinal));
}

/// <summary>One kind of thing Daoris keeps, as the table describes it.</summary>
/// <param name="Id">Its id: <c>keeping.json</c>'s key, the card's row and the machine log's word.</param>
/// <param name="Entries">The entries of the home's table it covers, by name.</param>
/// <param name="MadeAgain">What of it can be made again, which alone goes before its days, in words.</param>
/// <param name="Days">Its default: how long it is kept.</param>
/// <param name="MinDays">The fewest days the person may set.</param>
/// <param name="MaxDays">The most days the person may set.</param>
/// <param name="MaySetWhole">Whether the person may keep it whole, as before its rule.</param>
/// <param name="Remover">Its removal's one typed operation, in words.</param>
public sealed record KeepKind(
    string Id, IReadOnlyList<string> Entries, string MadeAgain, TimeSpan Days, int MinDays, int MaxDays, bool MaySetWhole, string Remover)
{
    /// <summary>Whether the person may set <c>never</c>: none by its days, the press alone (the downloads').</summary>
    public bool MaySetNever { get; init; }

    /// <summary>Whether its default is <c>never</c>, the press alone, so <see cref="Days"/> is not read (history's).</summary>
    public bool NeverByDefault { get; init; }

    /// <summary>The most it may hold, in GB, by default; null where it takes no size limit.</summary>
    public int? DefaultGb { get; init; }

    /// <summary>Whether the person may set a size limit beside its days.</summary>
    public bool MayHavePart => DefaultGb is not null;

    /// <summary>Whether it keeps no days, only a size: the browser's caches, which the engine itself ages.</summary>
    public bool SizeOnly { get; init; }
}

/// <summary>
/// The size limits per kind, beside the days: a kind over its limit lets go of its oldest eligible
/// units first, whichever of its days and its limit comes first. Null is no limit, which an unreadable file gives.
/// </summary>
public sealed record LimitsPolicy(int? SetAsideGb, int? DownloadsGb, int? BrowserCacheGb = null, int? AllowanceGb = null)
{
    /// <summary>The table's defaults.</summary>
    public static LimitsPolicy Default { get; } =
        new(KeepKinds.SetAside.DefaultGb, KeepKinds.Downloads.DefaultGb, KeepKinds.BrowserCache.DefaultGb, KeepKinds.DefaultAllowanceGb);

    /// <summary>No limit: a file that does not read keeps the most.</summary>
    public static LimitsPolicy None { get; } = new(null, null, null, null) { DepsGb = null };

    /// <summary>The dependency store's limit, the table's alone.</summary>
    public int? DepsGb { get; init; } = KeepKinds.Deps.DefaultGb;
}

/// <summary>
/// The one effective policy: the table's defaults under the person's choices, the only reader of a
/// kind's days. Every place that says or acts on the set-aside folders' days reads it: the purge, the look's report, the work inventory
/// and the terminal's trees list; the downloads' days, the look's pass and the Downloads row; the logs' days, the look's daily
/// prune and the card; history's age, the look's daily clear and the card.
/// </summary>
/// <param name="SetAside">The set-aside folders' rule.</param>
public sealed record KeepPolicy(SetAsidePolicy SetAside)
{
    /// <summary>The table's defaults.</summary>
    public static KeepPolicy Default { get; } = new(new SetAsidePolicy(KeepKinds.SetAside.Days, Whole: false));

    /// <summary>
    /// The policy where the person's choices do not read: what keeps the most, never the least, since a failure keeps: the set-aside folders whole for their most days, no download by its days, the logs their thirty, no
    /// history by an age.
    /// </summary>
    public static KeepPolicy Unread { get; } = new(new SetAsidePolicy(TimeSpan.FromDays(KeepKinds.SetAside.MaxDays), Whole: true))
    {
        Downloads = new(null),
        Logs = new(KeepKinds.Logs.Days),
        // No floor, so nothing goes early on a rule that could not be read.
        Floor = FloorPolicy.None,
        Limits = LimitsPolicy.None,
    };

    /// <summary>The size limit of each kind that takes one: beside its days, whichever comes first.</summary>
    public LimitsPolicy Limits { get; init; } = LimitsPolicy.Default;

    /// <summary>The free-space floor: the smaller of 10% of each volume and 20 GB by default.</summary>
    public FloorPolicy Floor { get; init; } = FloorPolicy.Default;

    /// <summary>The downloads' rule: seven days by default; null is the person's <c>never</c>.</summary>
    public DownloadsPolicy Downloads { get; init; } = new(KeepKinds.Downloads.Days);

    /// <summary>The machine log's rule: thirty days by default.</summary>
    public LogsPolicy Logs { get; init; } = new(KeepKinds.Logs.Days);

    /// <summary>Finished history's rule: no age by default, the person's clear alone.</summary>
    public HistoryPolicy History { get; init; } = new(null);

    /// <summary>How long a store entry not placed or sealed is kept: the table's thirty days, which no choice moves.</summary>
    public TimeSpan DepsDays => KeepKinds.Deps.Days;

    public TimeSpan SalvageDays => KeepKinds.Salvage.Days;

    /// <summary>The policy the person's <paramref name="choices"/> make over the table's defaults.</summary>
    public static KeepPolicy From(KeepChoices choices) =>
        new(new SetAsidePolicy(choices.SetAsideDays is { } days ? TimeSpan.FromDays(days) : KeepKinds.SetAside.Days, choices.SetAsideWhole))
        {
            Downloads = new(choices.DownloadsNever ? null : choices.DownloadsDays is { } kept ? TimeSpan.FromDays(kept) : KeepKinds.Downloads.Days),
            Logs = new(choices.LogsDays is { } logs ? TimeSpan.FromDays(logs) : KeepKinds.Logs.Days),
            History = new(choices.HistoryDays is { } age ? TimeSpan.FromDays(age) : null),
            Limits = new(
                choices.SetAsideGb ?? KeepKinds.SetAside.DefaultGb, choices.DownloadsGb ?? KeepKinds.Downloads.DefaultGb,
                choices.BrowserCacheGb ?? KeepKinds.BrowserCache.DefaultGb, choices.AllowanceGb ?? KeepKinds.DefaultAllowanceGb),
            Floor = choices.FloorOff ? FloorPolicy.None : new FloorPolicy(choices.FloorGb, choices.FloorPercent, Off: false),
        };

    /// <summary>
    /// The policy this home keeps by: the person's choices in <c>driver.json</c>'s <c>keep</c>, at the path this home's loop
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
/// The downloads' rule: how long into its current unused stretch a version nothing uses is kept; null is <c>never</c>, the press
/// alone, below the floor too.
/// </summary>
public sealed record DownloadsPolicy(TimeSpan? Days);

/// <summary>The machine log's rule: how many days of files are kept, today included.</summary>
public sealed record LogsPolicy(TimeSpan Days);

/// <summary>
/// Finished history's rule (decided by the owner): the age past a unit's last move at which the look's daily clear takes it; null,
/// the default, is <c>never</c>: nothing clears it by itself.
/// </summary>
public sealed record HistoryPolicy(TimeSpan? Days);

/// <summary>
/// The free-space floor: the free space at which Daoris frees early what it can make again and
/// says the rest. A trigger, not a reserve. Each volume's floor is computed from its own size: the person's size (at most half of it),
/// their share, or by default the smaller of 10% of it and 20 GB; <c>off</c> frees nothing early and says nothing.
/// </summary>
/// <param name="Gb">The person's size in GB (binary, as Windows shows sizes), or null.</param>
/// <param name="Percent">The person's share of each volume, or null.</param>
/// <param name="Off">No floor.</param>
public sealed record FloorPolicy(int? Gb, int? Percent, bool Off)
{
    /// <summary>The fewest GB the person may set.</summary>
    public const int MinGb = KeepKey.FloorMinGb;

    /// <summary>The most GB a stored size may hold, whatever the volume; a door judges against half the volume it reads.</summary>
    public const int MaxGb = KeepKey.FloorMaxGb;

    /// <summary>The smallest share the person may set.</summary>
    public const int MinPercent = KeepKey.FloorMinPercent;

    /// <summary>The largest share the person may set: half the volume.</summary>
    public const int MaxPercent = KeepKey.FloorMaxPercent;

    /// <summary>The default share of a volume.</summary>
    public const int DefaultPercent = 10;

    /// <summary>The default's cap: 100 GB, where the floor only warns.</summary>
    public const long DefaultCap = 100L << 30;

    /// <summary>The margin above the floor at which a stretch below ends (<c>space.ok</c>): the smaller of 5 GB and 5% of the volume.</summary>
    public const long MarginCap = 5L << 30;

    /// <summary>The table's default.</summary>
    public static FloorPolicy Default { get; } = new(null, null, false);

    /// <summary><c>off</c>.</summary>
    public static FloorPolicy None { get; } = new(null, null, true);

    /// <summary>Where the floor comes from, as <c>SYSTEM_SPACE</c>'s <c>floor.from</c> says it: <c>default</c>, <c>size</c>, <c>percent</c> or <c>off</c>.</summary>
    public string From => Off ? "off" : Gb is not null ? "size" : Percent is not null ? "percent" : "default";

    /// <summary>Half of a volume of <paramref name="total"/> bytes, in whole GB: the most a size may be there.</summary>
    public static int HalfGb(long total) => (int)Math.Min(MaxGb, total / 2 / (1L << 30));

    /// <summary>A volume of <paramref name="total"/> bytes' floor in bytes; null where it is off.</summary>
    public long? Of(long total)
    {
        if (Off || total <= 0) return null;
        if (Gb is { } gb) return Math.Min((long)gb << 30, total / 2);
        if (Percent is { } share) return total / 100 * share;
        return Math.Min(total / 100 * DefaultPercent, DefaultCap);
    }

    /// <summary>The margin above a volume's floor that ends a stretch below it: the smaller of 5 GB and 5% of the volume.</summary>
    public static long Margin(long total) => Math.Min(MarginCap, total / 100 * 5);

    /// <summary>The rule in a clause: the terminal's and Ask Daoris's card's words.</summary>
    public string Said => From switch
    {
        "off" => "no floor: Daoris says nothing of the free space",
        "size" => $"below {Gb} GB free (at most half of a volume), Daoris tells you",
        "percent" => $"below {Percent}% of a volume free, Daoris tells you",
        _ => "below the smaller of 10% of a volume and 100 GB free, Daoris tells you",
    };
}
