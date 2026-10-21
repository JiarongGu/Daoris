using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// One row of This Daoris's <i>Disk space</i> card (KEEP1c, D195 point 5; the cleanup design §2.1, §4.1): a kind a person decides about
/// together, its rule in words, and whose rule it is where it is not Daoris's. A kind whose rule runs here names its <see cref="KeepKind"/>.
/// </summary>
/// <param name="Id">Its id: the card's row, the catalogue's <c>settings.daoris.space.kind.*</c> and the terminal's word.</param>
/// <param name="Rule"><c>days</c> (kept for a number of days by a rule that runs), <c>press</c> (kept until the person clears it),
/// <c>proof</c> (cleaned up once its work is on the line) or <c>own</c> (another's rule, <paramref name="Whose"/>).</param>
/// <param name="Whose">Whose rule it is for <c>own</c>: <c>browser</c>, <c>agents</c>, <c>person</c> or <c>each</c>; null otherwise.</param>
/// <param name="Keeps">The table's kind whose rule this row shows; null for a row Daoris removes nothing of by a rule of its own.</param>
public sealed record SpaceKind(string Id, string Rule, string? Whose, KeepKind? Keeps);

/// <summary>A kind's places on the disk (<see cref="SpaceKinds.Places"/>): what its walk reads, what it passes over, and the entries by name.</summary>
public sealed record SpacePlaces(IReadOnlyList<string> Places, IReadOnlyList<string> Skip, IReadOnlyList<string> Entries);

/// <summary>
/// The card's rows, in its order (the cleanup design §4.1), and where each lies. Every rule a row shows already runs (§9): the set-aside
/// folders' days and slim (KEEP1a), the downloads' days (KEEP1b), the machine log's thirty days, history's press, the trees' clean-up;
/// the rest say whose rule it is. No row shows an editor: each rule's <i>Change…</i> arrives with the row that sets it (KEEP1d).
/// </summary>
public static class SpaceKinds
{
    /// <summary>The kinds, in the order the card and the terminal show them.</summary>
    public static IReadOnlyList<SpaceKind> Table { get; } =
    [
        new("set-aside", "days", null, KeepKinds.SetAside),
        new("downloads", "days", null, KeepKinds.Downloads),
        new("browser-cache", "own", "browser", null),
        new("logs", "days", null, null),
        new("history", "press", null, null),
        new("trees", "proof", null, null),
        new("sign-ins", "own", "agents", null),
        new("left-over", "own", "person", null),
        new("rest", "own", "each", null),
    ];

    /// <summary>The kind of this id, or null where the table holds none.</summary>
    public static SpaceKind? Named(string id) => Table.FirstOrDefault(kind => string.Equals(kind.Id, id, StringComparison.Ordinal));

    /// <summary>
    /// The browser's caches, relative to <c>browser-engine</c>'s root, its active profile being <c>Default</c> (the cleanup design §3.2's
    /// table): counted here as the browser's own; nothing of Daoris's clears them yet (KEEP1e, KEEP1f).
    /// </summary>
    public static IReadOnlyList<string> BrowserCaches { get; } =
    [
        "Default/Cache", "Default/Code Cache", "Default/GPUCache", "Default/DawnGraphiteCache", "Default/DawnWebGPUCache",
        "Default/Shared Dictionary/cache", "component_crx_cache", "GrShaderCache", "ShaderCache",
    ];

    /// <summary>What an earlier build left at the home's top, each by its exact path (§1.6): <c>webview2/</c> and <c>browser/profile/</c>.</summary>
    public static IReadOnlyList<string> EarlierBuild { get; } = ["webview2", "browser/profile"];

    /// <summary>
    /// Where <paramref name="id"/>'s kind lies under <paramref name="home"/>: its places, and what its walk passes over because another
    /// kind holds it. <c>rest</c> is the Daoris folder less every other kind's places. Both places of an entry found holding it is thrown
    /// (<see cref="HomeEntryTwiceException"/>), for the caller to say.
    /// </summary>
    public static SpacePlaces Places(string home, string id)
    {
        var trees = DaorisHome.Entry(home, HomeEntry.Trees);
        var setAside = Path.Combine(trees, TreeSetAside.SetAsideFolder);
        var engine = DaorisHome.Entry(home, HomeEntry.BrowserEngine);
        var caches = BrowserCaches.Select(path => Path.Combine([engine, .. path.Split('/')])).ToList();
        switch (id)
        {
            case "set-aside":
                return new([setAside], [], [HomeEntry.Trees.Name]);
            case "downloads":
                return new([DaorisHome.Entry(home, HomeEntry.Toolchain), DaorisHome.Entry(home, HomeEntry.ToolVersions)], [],
                    [HomeEntry.Toolchain.Name, HomeEntry.ToolVersions.Name]);
            case "browser-cache":
                return new(caches, [], [HomeEntry.BrowserEngine.Name]);
            case "logs":
                return new([DaorisHome.Entry(home, HomeEntry.Logs)], [], [HomeEntry.Logs.Name]);
            case "history":
                return new([.. Of(home, HomeEntry.Sessions), .. Of(home, HomeEntry.Workflows)], [], [HomeEntry.Sessions.Name, HomeEntry.Workflows.Name]);
            case "trees":
                return new([trees], [setAside], [HomeEntry.Trees.Name]);
            case "sign-ins":
                HomeEntry[] signIns = [HomeEntry.AgentHomes, HomeEntry.BrowserEngine, HomeEntry.BrowserEdge, HomeEntry.EdgeCookies, HomeEntry.Window, HomeEntry.Keys, HomeEntry.Remotes];
                return new([.. signIns.SelectMany(entry => Of(home, entry))], caches, [.. signIns.Select(entry => entry.Name)]);
            case "left-over":
                return new([.. EarlierBuild.Select(path => Path.Combine([home, .. path.Split('/')]))], [], []);
            case "rest":
                var others = Table.Where(kind => kind.Id != "rest").SelectMany(kind => Places(home, kind.Id).Places).ToList();
                return new([DaorisHome.FolderOf(home).Folder], others, []);
            default:
                throw new ArgumentException($"no kind `{id}` on the card.", nameof(id));
        }
    }

    /// <summary>
    /// An entry's places: an entry kept per item, the folder holding its items in each of its places; the window's profile under an old
    /// home, the names it holds there, never the old home whole; else the one place <see cref="DaorisHome.Entry"/> finds.
    /// </summary>
    private static IEnumerable<string> Of(string home, HomeEntry entry)
    {
        if (entry.New.Contains('*', StringComparison.Ordinal)) return DaorisHome.Places(home, entry);
        var place = DaorisHome.Entry(home, entry);
        var (_, old) = DaorisHome.FolderOf(home);
        return entry.Holds.Length > 0 && entry.Old.Length == 0 && SamePlace(place, old)
            ? entry.Holds.Select(name => Path.Combine(place, name))
            : [place];
    }

    internal static bool SamePlace(string a, string b) =>
        string.Equals(
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(a)), Path.TrimEndingDirectorySeparator(Path.GetFullPath(b)),
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
}

/// <summary>One kind's last walk (the cleanup design §4.3): when it ended, its bytes and files, why it stopped short, and each set-aside folder's bytes.</summary>
/// <param name="Partial"><c>budget</c>, <c>cancelled</c>, <c>unreadable</c> or <c>twice</c> where the walk read only part; null where whole.</param>
/// <param name="Folders">For the set-aside kind, each folder's bytes by <c>&lt;workspace&gt;/&lt;repository&gt;/&lt;folder&gt;</c>.</param>
public sealed record KindReading(DateTimeOffset Measured, long Bytes, int Files, string? Partial, IReadOnlyDictionary<string, long> Folders);

/// <summary>
/// The card's sizes (KEEP1c, the cleanup design §4.3): a bounded walk per kind through <see cref="WiringSizes.MeasurePlaces"/>, never a
/// ledger and never the whole home on a look. Each kind has its own budget, so one large kind never starves the rest. Walks run in the
/// background, one home's at a time and one kind after another; each kind's reading publishes as it ends, so the card fills in kind by
/// kind (WIRING-SIZES-LIVE1's need). Kept for the process, as <see cref="WiringSizes.Shared"/> keeps the folders' measure.
/// </summary>
public sealed class SpaceSizes
{
    /// <summary>This process's: what every window of the desktop and the look's pass read and start.</summary>
    public static SpaceSizes Shared { get; } = new();

    /// <summary>How often the look's pass counts a kind by itself (§3.1 step 6): at most once a day.</summary>
    public static readonly TimeSpan Daily = TimeSpan.FromDays(1);

    private readonly ConcurrentDictionary<string, KindReading> _readings = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, Action<string>> _told = new(StringComparer.Ordinal);
    private readonly Dictionary<string, (Task Walk, List<string> Waiting, string? Counting)> _walks = new(StringComparer.Ordinal);
    private readonly object _gate = new();

    /// <summary><paramref name="kind"/>'s last reading under <paramref name="home"/>, or null where none has ended.</summary>
    public KindReading? Of(string home, string kind) => _readings.TryGetValue(Key(home, kind), out var reading) ? reading : null;

    /// <summary>Whether <paramref name="kind"/>'s walk is under way or waits behind another's.</summary>
    public bool Counting(string home, string kind)
    {
        lock (_gate)
        {
            return _walks.TryGetValue(HomeKey(home), out var walk) && (walk.Counting == kind || walk.Waiting.Contains(kind));
        }
    }

    /// <summary>
    /// Who hears each kind's walk end under <paramref name="home"/>: the desktop's system routes, which tell every window. One listener per
    /// home, the last named, so a test's module never hears another's home.
    /// </summary>
    public void Tell(string home, Action<string> told) => _told[HomeKey(home)] = told;

    /// <summary>
    /// Walk <paramref name="kinds"/> in the background, each after the one under way, in the card's order; a kind already waiting or
    /// counting is not asked twice. True where a walk began or was queued; false where nothing was asked.
    /// </summary>
    public bool Start(string home, IEnumerable<string> kinds, WiringBudget budget)
    {
        var asked = kinds.Where(kind => SpaceKinds.Named(kind) is not null).Distinct(StringComparer.Ordinal).ToList();
        if (asked.Count == 0) return false;
        var key = HomeKey(home);
        lock (_gate)
        {
            if (_walks.TryGetValue(key, out var under))
            {
                foreach (var kind in asked.Where(kind => kind != under.Counting && !under.Waiting.Contains(kind))) under.Waiting.Add(kind);
                return true;
            }

            var waiting = asked.OrderBy(kind => Order(kind)).ToList();
            var started = new TaskCompletionSource();
            _walks[key] = (started.Task, waiting, null);
            _ = Task.Run(() =>
            {
                try
                {
                    Walk(home, key, budget);
                }
                finally
                {
                    started.TrySetResult();
                }
            });
            return true;
        }
    }

    /// <summary>
    /// The look's pass (§3.1 step 6, §4.3): each kind never counted in this process, or counted a day ago or more, and each kind this run
    /// removed from, walked in the background. True where any was asked.
    /// </summary>
    public bool StartDue(string home, DateTimeOffset now, IEnumerable<string> removed, WiringBudget budget)
    {
        var due = SpaceKinds.Table.Select(kind => kind.Id)
            .Where(kind => Of(home, kind) is not { } reading || now - reading.Measured >= Daily)
            .Concat(removed)
            .ToList();
        return Start(home, due, budget);
    }

    /// <summary>Completes when the walks under way for <paramref name="home"/>, if any, have ended.</summary>
    public Task Finished(string home)
    {
        lock (_gate)
        {
            return _walks.TryGetValue(HomeKey(home), out var walk) ? walk.Walk : Task.CompletedTask;
        }
    }

    /// <summary>Walk one kind now, on this thread, and keep its reading: the terminal's <c>--measure</c>, and what a background walk runs.</summary>
    public KindReading MeasureNow(string home, string kind, WiringBudget budget, CancellationToken cancellationToken)
    {
        KindReading reading;
        try
        {
            var places = SpaceKinds.Places(home, kind);
            var measure = WiringSizes.MeasurePlaces(places.Places, places.Skip, budget, cancellationToken, groupDepth: kind == "set-aside" ? 3 : 0);
            reading = new(DateTimeOffset.UtcNow, measure.Bytes, measure.Files, measure.Partial, measure.Groups);
        }
        catch (HomeEntryTwiceException)
        {
            reading = new(DateTimeOffset.UtcNow, 0, 0, "twice", new Dictionary<string, long>());
        }

        _readings[Key(home, kind)] = reading;
        return reading;
    }

    private void Walk(string home, string key, WiringBudget budget)
    {
        while (true)
        {
            string kind;
            lock (_gate)
            {
                var walk = _walks[key];
                if (walk.Waiting.Count == 0)
                {
                    _walks.Remove(key);
                    return;
                }

                kind = walk.Waiting.OrderBy(Order).First();
                walk.Waiting.Remove(kind);
                _walks[key] = walk with { Counting = kind };
            }

            try
            {
                MeasureNow(home, kind, budget, CancellationToken.None);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException)
            {
                _readings[Key(home, kind)] = new(DateTimeOffset.UtcNow, 0, 0, "unreadable", new Dictionary<string, long>());
            }
            finally
            {
                lock (_gate) _walks[key] = _walks[key] with { Counting = null };
            }

            if (_told.TryGetValue(key, out var told))
            {
                try
                {
                    told(kind);
                }
                catch (Exception error) when (error is not OutOfMemoryException)
                {
                    // Telling the windows is best-effort: the reading is kept, and the next read shows it.
                }
            }
        }
    }

    private static int Order(string kind)
    {
        for (var at = 0; at < SpaceKinds.Table.Count; at++)
        {
            if (SpaceKinds.Table[at].Id == kind) return at;
        }

        return int.MaxValue;
    }

    private static string HomeKey(string home)
    {
        var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(home));
        return OperatingSystem.IsWindows() ? full.ToUpperInvariant() : full;
    }

    private static string Key(string home, string kind) => HomeKey(home) + "|" + kind;
}

/// <summary>What the <i>Disk space</i> read is handed (<see cref="SystemSpace.Read"/>): the home, and the reads a test stands in for.</summary>
public sealed record SpaceWorld(string Home)
{
    /// <summary>The sizes counted, kept for the process.</summary>
    public SpaceSizes Sizes { get; init; } = SpaceSizes.Shared;

    /// <summary>What says which downloaded versions are used, for the downloads' <i>next</i>; null where it is not read.</summary>
    public Func<DownloadUses>? Uses { get; init; }

    /// <summary>What time it is.</summary>
    public Func<DateTimeOffset> Now { get; init; } = () => DateTimeOffset.UtcNow;

    /// <summary>A folder's volume, its free bytes this account may use and its size; null where it cannot be read.</summary>
    public Func<string, (long Free, long Total)?> Volume { get; init; } = SystemSpace.VolumeOf;

    /// <summary>Whether a process still runs, for the run lock's holder; null asks the system.</summary>
    public Func<int, bool>? Lives { get; init; }

    /// <summary>The press's runs in this process (KEEP1e), whose current or last is the answer's <c>run</c>.</summary>
    public SpaceRuns Runs { get; init; } = SpaceRuns.Shared;

    /// <summary>The terminal's world: this home, its downloads' uses read from the files as the loop resolves them.</summary>
    public static SpaceWorld OfThisTerminal(string home) => new(home)
    {
        Uses = () => DownloadUses.Read(home, HarnessSettings.Load(HarnessSettings.ResolvePath(home)), DriverConfig.ResolvePath(home)),
    };
}

/// <summary>
/// This Daoris's <i>Disk space</i> read (KEEP1c, D195 point 5; the cleanup design §4.4): <c>SYSTEM_SPACE</c>'s answer and
/// <c>daoris-driver space</c>'s, one reader. The free space of the Daoris folder's volume; each kind's size with when it was counted, its
/// rule, its next and its last clean; and the run lock's holder, a run under way or one a crash cut short. It walks nothing: sizes are
/// the kinds' last walks (<see cref="SpaceSizes"/>), and a kind never counted says so. It changes nothing.
/// </summary>
/// <remarks>
/// <para>The shape is a wire shape: every field added, never renamed. A fact not read is <c>null</c> beside its <c>…Why</c>. The floor
/// (<c>volume.floor</c>, <c>below</c>, <c>freedEarly</c>, a kind's own <c>volume</c>) is KEEP1f's: answered as none until the rule that
/// fills it runs, so the card never shows a rule that does not run (§9). The press's <c>run</c> is KEEP1e's (<see cref="SpaceRuns"/>).</para>
/// </remarks>
public static class SystemSpace
{
    /// <summary>The shape this build answers.</summary>
    public const int Schema = 1;

    /// <summary>The read, as the route and the terminal answer it.</summary>
    public static JsonObject Read(SpaceWorld world)
    {
        var home = world.Home;
        var now = world.Now();
        var policy = KeepPolicy.Of(home);
        var record = KeepingRecord.Read(home);
        var holder = KeepingRun.Holder(home, world.Lives);
        var folder = DaorisHome.FolderOf(home).Folder;
        var volume = world.Volume(folder);
        var kinds = new JsonArray();
        foreach (var kind in SpaceKinds.Table) kinds.Add(Kind(world, kind, policy, record, now));
        return new JsonObject
        {
            ["schema"] = Schema,
            ["volume"] = volume is { } read
                ? new JsonObject
                {
                    ["path"] = folder, ["free"] = read.Free, ["total"] = read.Total,
                    // KEEP1f's: no floor runs yet, so none is said.
                    ["floor"] = null, ["below"] = false, ["freedEarly"] = null,
                }
                : null,
            ["volumeWhy"] = volume is null ? "unreadable" : null,
            ["kinds"] = kinds,
            // KEEP1e: the press's run, under way or last ended in this process; none where this process ran none.
            ["run"] = world.Runs.Last(home)?.Json(),
            ["held"] = holder is null
                ? null
                : new JsonObject { ["pid"] = holder.Pid, ["since"] = Moments.Write(holder.Since), ["door"] = holder.Door, ["gone"] = holder.Gone },
        };
    }

    /// <summary>The read as the terminal's <c>--json</c> prints it, field for field.</summary>
    public static string Json(JsonObject space) => JsonFile.Serialize(space);

    /// <summary>A folder's volume as the operating system says it: the free bytes this account may use, and its size.</summary>
    public static (long Free, long Total)? VolumeOf(string folder)
    {
        try
        {
            var root = Path.GetPathRoot(Path.GetFullPath(folder));
            if (string.IsNullOrEmpty(root)) return null;
            var drive = new DriveInfo(root);
            return drive.IsReady ? (drive.AvailableFreeSpace, drive.TotalSize) : null;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return null;
        }
    }

    private static JsonObject Kind(SpaceWorld world, SpaceKind kind, KeepPolicy policy, KeepingFile record, DateTimeOffset now)
    {
        var home = world.Home;
        var reading = world.Sizes.Of(home, kind.Id);
        IReadOnlyList<string> entries;
        try
        {
            entries = SpaceKinds.Places(home, kind.Id).Entries;
        }
        catch (HomeEntryTwiceException)
        {
            entries = [];
        }

        var (next, nextWhy) = Next(world, kind, policy, record, reading, now);
        var last = kind.Keeps is { } keeps && record.Last.TryGetValue(keeps.Id, out var ran) ? ran : null;
        return new JsonObject
        {
            ["id"] = kind.Id,
            ["entries"] = new JsonArray([.. entries.Select(entry => (JsonNode?)JsonValue.Create(entry))]),
            ["bytes"] = reading?.Bytes,
            ["files"] = reading?.Files,
            ["measured"] = reading is null ? null : Moments.Write(reading.Measured),
            ["partial"] = reading?.Partial is not null,
            ["partialWhy"] = reading?.Partial,
            ["counting"] = world.Sizes.Counting(home, kind.Id),
            // KEEP1f's: a kind's entry on another volume than the Daoris folder's.
            ["volume"] = null,
            ["rule"] = Rule(kind, policy),
            // KEEP1d's: the person sets no rule yet.
            ["settable"] = false,
            ["next"] = next,
            ["nextWhy"] = nextWhy,
            ["last"] = last is null ? null : Last(last),
            ["lastWhy"] = kind.Keeps is not null && record.Problem is not null ? "unreadable" : null,
        };
    }

    private static JsonObject Rule(SpaceKind kind, KeepPolicy policy)
    {
        var rule = new JsonObject { ["kind"] = kind.Rule, ["days"] = null, ["whole"] = null, ["default"] = true, ["whose"] = kind.Whose };
        switch (kind.Id)
        {
            case "set-aside":
                rule["days"] = (int)policy.SetAside.Days.TotalDays;
                rule["whole"] = policy.SetAside.Whole;
                rule["default"] = policy.SetAside == KeepPolicy.Default.SetAside;
                break;
            case "downloads":
                if (policy.Downloads.Days is { } days) rule["days"] = (int)days.TotalDays;
                else rule["kind"] = "never";
                rule["default"] = policy.Downloads == KeepPolicy.Default.Downloads;
                break;
            case "logs":
                rule["days"] = MachineLog.KeptDays;
                break;
        }

        return rule;
    }

    private static JsonObject Last(KeepingLast last)
    {
        var kept = new JsonObject();
        foreach (var (why, count) in last.Kept.OrderBy(each => each.Key, StringComparer.Ordinal)) kept[why] = count;
        return new JsonObject
        {
            ["at"] = Moments.Write(last.At), ["door"] = last.Door,
            ["removed"] = new JsonObject { ["count"] = last.Removed, ["bytes"] = last.Bytes }, ["kept"] = kept,
        };
    }

    /// <summary>
    /// When a kind's rule next takes something, from the disk and the policy: the earliest day, how many go that day, and their bytes where
    /// the walk knows them. Null where nothing waits, or where it was not read, with why.
    /// </summary>
    private static (JsonObject? Next, string? Why) Next(
        SpaceWorld world, SpaceKind kind, KeepPolicy policy, KeepingFile record, KindReading? reading, DateTimeOffset now)
    {
        try
        {
            return kind.Id switch
            {
                "set-aside" => SetAsideNext(world.Home, policy, reading),
                "downloads" => DownloadsNext(world, policy, record, now),
                "logs" => LogsNext(world.Home),
                _ => (null, null),
            };
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or HomeEntryTwiceException)
        {
            return (null, "unreadable");
        }
    }

    /// <summary>The set-aside folders: each goes its days after its move (the purge), its bytes from the kind's last walk.</summary>
    internal static (JsonObject? Next, string? Why) SetAsideNext(string home, KeepPolicy policy, KindReading? reading)
    {
        var root = Path.Combine(DaorisHome.Entry(home, HomeEntry.Trees), TreeSetAside.SetAsideFolder);
        if (!Directory.Exists(root)) return (null, null);
        var goes = new List<(DateTimeOffset At, string Folder)>();
        foreach (var workspace in Directory.EnumerateDirectories(root))
        foreach (var repository in Directory.EnumerateDirectories(workspace))
        foreach (var folder in Directory.EnumerateDirectories(repository))
        {
            if (TreeSetAside.MovedAt(Path.GetFileName(folder)) is not { } moved) continue;
            goes.Add((moved + policy.SetAside.Days, $"{Path.GetFileName(workspace)}/{Path.GetFileName(repository)}/{Path.GetFileName(folder)}"));
        }

        return Earliest(goes, reading?.Folders);
    }

    /// <summary>The downloads: each unused version goes its days into its current unused stretch (<see cref="DownloadKeeping.Goes"/>).</summary>
    private static (JsonObject? Next, string? Why) DownloadsNext(SpaceWorld world, KeepPolicy policy, KeepingFile record, DateTimeOffset now)
    {
        if (world.Uses is not { } uses) return (null, "unread");
        if (record.Problem is not null) return (null, "unreadable");
        DownloadUses read;
        try
        {
            read = uses();
        }
        catch (Exception error) when (error is DriverException or IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            // What uses a version cannot be read now: no day is said, rather than a day a use would move.
            return (null, "unreadable");
        }

        var listing = DownloadClearing.List(world.Home, read, now, measure: false);
        if (listing.Why is { } why) return (null, why);
        var goes = listing.Versions
            .Select(row => (At: DownloadKeeping.Goes(row, record, policy.Downloads), Folder: DownloadKeeping.Key(row)))
            .Where(row => row.At is not null)
            .Select(row => (row.At!.Value, row.Folder))
            .ToList();
        return Earliest(goes, null);
    }

    /// <summary>The machine log: a day's files go once it is thirty days old, at the next process's open (<see cref="MachineLog.Prune"/>).</summary>
    private static (JsonObject? Next, string? Why) LogsNext(string home)
    {
        var folder = DaorisHome.Entry(home, HomeEntry.Logs);
        if (!Directory.Exists(folder)) return (null, null);
        var days = new Dictionary<DateTime, long>();
        var counts = new Dictionary<DateTime, int>();
        foreach (var path in Directory.EnumerateFiles(folder, "*.jsonl"))
        {
            var name = Path.GetFileName(path);
            if (name.Length < 10 || !DateTime.TryParseExact(name[..10], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day)) continue;
            long bytes;
            try
            {
                bytes = new FileInfo(path).Length;
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                bytes = 0;
            }

            days[day] = days.GetValueOrDefault(day) + bytes;
            counts[day] = counts.GetValueOrDefault(day) + 1;
        }

        if (days.Count == 0) return (null, null);
        var oldest = days.Keys.Min();
        var at = new DateTimeOffset(DateTime.SpecifyKind(oldest.AddDays(MachineLog.KeptDays), DateTimeKind.Utc));
        return (new JsonObject { ["at"] = Moments.Write(at), ["count"] = counts[oldest], ["bytes"] = days[oldest] }, null);
    }

    /// <summary>The earliest day anything goes: how many go that UTC day, and their bytes where every one of them was measured.</summary>
    private static (JsonObject? Next, string? Why) Earliest(IReadOnlyList<(DateTimeOffset At, string Folder)> goes, IReadOnlyDictionary<string, long>? sizes)
    {
        if (goes.Count == 0) return (null, null);
        var first = goes.Min(each => each.At);
        var day = first.UtcDateTime.Date;
        var that = goes.Where(each => each.At.UtcDateTime.Date == day).ToList();
        long? bytes = sizes is null ? null : 0;
        foreach (var (_, folder) in that)
        {
            if (sizes is null || !sizes.TryGetValue(folder, out var size))
            {
                bytes = null;
                break;
            }

            bytes += size;
        }

        return (new JsonObject { ["at"] = Moments.Write(first), ["count"] = that.Count, ["bytes"] = bytes }, null);
    }
}
