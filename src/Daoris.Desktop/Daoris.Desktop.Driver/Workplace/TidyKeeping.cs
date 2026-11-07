using System.Text.Json;
using static Daoris.Driver.BranchTidying;

namespace Daoris.Driver;

/// <summary>What the look keeps beside the branches: the set-aside folders, the downloads, the machine log, finished history and the free space's floor, said as the look's lines.</summary>
internal sealed class TidyKeeping(BranchTidying tidy, string home)
{
    private readonly object _gate = new();

    private Func<DateTimeOffset> Clock => tidy.Clock;
    private Func<string, KeepPolicy> Policy => tidy.Policy;
    private TimeSpan Budget => tidy.Budget;
    private string? ConfigPath => tidy.ConfigPath;
    private SpaceSizes? Sizes => tidy.Sizes;
    private Func<ServiceClient, CancellationToken, Task<LiveSessions>>? Sessions => tidy.Sessions;
    private SessionProcesses? Processes => tidy.Processes;
    private ISessionEventRemoval? Events => tidy.Events;
    private IBrowserCaches? Caches => tidy.Caches;
    private SpaceFloors Floors => tidy.Floors;
    private Func<string, VolumeReading?>? Volume => tidy.Volume;
    private LookSaid Look => tidy.Look;

    private bool First(HashSet<string> met, string repository, string branch, string why) => tidy.First(met, repository, branch, why);

    private IReadOnlyList<string> SayPurged(IReadOnlyList<SetAside> folders, MachineLog? log, HashSet<string> met) => tidy.SayPurged(folders, log, met);

    // When the downloads' rule last ran its daily removal in this process, and under which days, so a changed rule runs at once.
    private DateTimeOffset? _downloadsDaily;
    private TimeSpan? _downloadsDays;

    // The same for the machine log's daily prune at the person's days.
    private DateTimeOffset? _logsDaily;
    private TimeSpan? _logsDays;

    // The same for finished history's daily clear at the person's age.
    private DateTimeOffset? _historyDaily;
    private TimeSpan? _historyDays;

    private DepsKeeping? _deps;

    private DepsKeeping Deps => _deps ??= new DepsKeeping(Clock);

    /// <summary>
    /// The Keeping pass, in the tidy's task after its repositories, where the purge ran,
    /// so it holds no repository's hold and a start never waits on it: it tries the home's run lock, and held elsewhere removes nothing
    /// this look and names the holder, once; else the set-aside kind's removal, the purge at the policy's days then the slims due, within
    /// the pass's budget; then <c>keeping.json</c>'s <c>last</c> for the kind, where it removed something or newly kept a path. Then the
    /// downloads' rule (<see cref="DownloadKeeping"/>): each version's unused stretch each pass, and once a day, or at once after
    /// the rule changed, the versions past their days. Then the machine log's daily prune at the person's days, and finished
    /// history's daily clear where the person set an age (<see cref="HistoryKeeping"/>), and the dependency store's daily eviction
    /// (<see cref="DepsKeeping"/>). First of all, the free-space floor
    /// (<see cref="SpaceFloor"/>): each volume read, and below its floor the floor's three acts at once, at most hourly.
    /// </summary>
    internal async Task<IReadOnlyList<string>> KeepAsync(SessionTrees trees, ServiceClient service, MachineLog? log, HashSet<string> met, CancellationToken ct)
    {
        var lines = new List<string>();
        lines.AddRange(await KeepFloorAsync(service, log, met, ct).ConfigureAwait(false));
        var setAside = trees.SetAsideRoots.Any(Directory.Exists);
        var logs = Policy(home).Logs;
        bool logsDue;
        lock (_gate) logsDue = _logsDaily is not { } pruned || Clock() - pruned >= DownloadsPace || _logsDays != logs.Days;
        logsDue = logsDue && MachineLog.HoldsOlder(home, Clock(), (int)logs.Days.TotalDays);
        var history = Policy(home).History;
        bool historyDue;
        lock (_gate) historyDue = history.Days is not null && (_historyDaily is not { } cleared || Clock() - cleared >= DownloadsPace || _historyDays != history.Days);
        var depsDue = Deps.Due(home);
        // Nothing set aside, nothing downloaded, no log's day, no history's clear and no store's eviction due, nothing to keep: the run
        // lock and its holder's file are not written every pace for nothing.
        if (!setAside && !Downloaded() && !logsDue && !historyDue && !depsDue) return lines;
        using var run = KeepingRun.TryTake(home, KeepingRun.Look);
        if (run is null)
        {
            var holder = KeepingRun.Holder(home);
            lock (_gate)
            {
                if (First(met, "", "keeping", "held"))
                {
                    lines.Add(Look.Retry("tidy", $"tidy  {(setAside ? "the folders moved aside" : "the downloads")} were not cleaned this look: {holder?.Said ?? "another run"} "
                        + "holds Daoris's keeping, and a later look tries again."));
                }
            }

            return lines;
        }

        // One budget for the pass's removals: what the set-aside folders spent, the downloads do not spend again.
        var spent = System.Diagnostics.Stopwatch.StartNew();
        if (setAside)
        {
            // The registry read once for the record pass and the purge's holding checks; the record pass within the pass's
            // budget, between folders, and the purge within what it left.
            var roots = SessionTrees.RootsOf(await RegistryAsync(service, ct).ConfigureAwait(false));
            lines.AddRange(await RecordSetAsideAsync(trees, service, roots, met, () => spent.Elapsed >= Budget, ct).ConfigureAwait(false));
            var rest = Budget - spent.Elapsed;
            lines.AddRange(await KeepSetAsideAsync(trees, run, log, met, roots, rest > TimeSpan.Zero ? rest : TimeSpan.Zero, ct).ConfigureAwait(false));
        }

        var left = Budget - spent.Elapsed;
        lines.AddRange(await KeepDownloadsAsync(service, run, left > TimeSpan.Zero ? left : TimeSpan.Zero, log, met, ct).ConfigureAwait(false));
        if (logsDue) lines.AddRange(KeepLogs(logs, run, met));
        if (historyDue) lines.AddRange(await KeepHistoryAsync(service, history, run, log, met, ct).ConfigureAwait(false));
        if (depsDue) lines.AddRange(KeepDeps(run, met));
        if (Sizes is not null && (setAside || Downloaded())) lines.AddRange(await KeepLimitsAsync(service, run, log, ct).ConfigureAwait(false));

        // Each kind counted at most once a day, and the kinds this run wrote a `last` for at
        // its end, in the background, so the card's sizes follow the rules without a page's open walking anything.
        if (Sizes is { } sizes)
        {
            var ran = KeepingRecord.Read(home).Last.Where(each => each.Value.Run == run.Id).Select(each => each.Key);
            sizes.StartDue(home, Clock(), ran, WiringBudget.Default);
        }

        return lines;
    }

    /// <summary>
    /// Each kind's size limit, holding the home's run: once a day, or at once after a limit changed, a kind over its limit
    /// lets go of its oldest eligible units through the press's removal (<see cref="SpaceLimits"/>); what went is said in the report.
    /// </summary>
    private async Task<IReadOnlyList<string>> KeepLimitsAsync(ServiceClient service, KeepingRun run, MachineLog? log, CancellationToken ct)
    {
        var lines = new List<string>();
        var limits = Policy(home).Limits;
        var now = Clock();
        bool due;
        lock (_gate) due = _limitsDaily is not { } last || now - last >= DownloadsPace || _limits != limits;
        if (!due) return lines;
        var markers = DaorisHome.Entry(home, HomeEntry.SessionPids);
        var world = new SpaceFreeWorld(home)
        {
            Sizes = Sizes!,
            Judge = () => DownloadJudge.Of(
                home, HarnessSettings.ResolvePath(home), ConfigPath ?? DriverConfig.ResolvePath(home),
                token => Sessions is { } stand ? stand(service, token) : LiveSessions.ReadAsync(service, markers, token), Clock),
            Caches = Caches,
            Log = log,
            Now = Clock,
            Policy = Policy,
        }.WithService(service);
        var pass = await SpaceLimits.PassAsync(world, run.Door, run.Id, ct).ConfigureAwait(false);
        if (!pass.Uncounted)
        {
            lock (_gate)
            {
                _limitsDaily = now;
                _limits = limits;
            }
        }

        foreach (var kind in pass.Kinds.Where(each => each.Removed > 0))
        {
            var what = kind.Id switch { "set-aside" => "the folders moved aside", "browser-cache" => "the browser's caches", _ => "the downloads" };
            lines.Add(Look.Progress("tidy", $"tidy  {what} were over their size limit: removed "
                + $"{kind.Removed} {(kind.Id == "browser-cache" ? "caches" : "of the oldest")} ({Size(kind.Bytes)}); each is made again, or only a copy of what the repository holds."));
        }

        return lines;
    }

    // When the size limits were last judged in this process, and under which limits, so a changed limit is judged at once.
    private DateTimeOffset? _limitsDaily;
    private LimitsPolicy? _limits;

    /// <summary>
    /// The free-space floor, at each pass: each volume read for its own entries, and below its floor the
    /// three acts that free what can be made again, at once and at most hourly, each with its own guards, under the run lock as the
    /// <c>floor</c>'s door. What they freed is said as they free it; a volume still below after them is said once, until it is above its
    /// floor by the margin again.
    /// </summary>
    private async Task<IReadOnlyList<string>> KeepFloorAsync(ServiceClient service, MachineLog? log, HashSet<string> met, CancellationToken ct)
    {
        var lines = new List<string>();
        if (Volume is not { } volume) return lines;
        var markers = DaorisHome.Entry(home, HomeEntry.SessionPids);
        var world = new FloorWorld(home)
        {
            Volume = volume,
            Now = Clock,
            Policy = Policy,
            Judge = () => DownloadJudge.Of(
                home, HarnessSettings.ResolvePath(home), ConfigPath ?? DriverConfig.ResolvePath(home),
                token => Sessions is { } stand ? stand(service, token) : LiveSessions.ReadAsync(service, markers, token), Clock),
            Caches = Caches,
            Log = log,
            Floors = Floors,
            Budget = Budget,
            Could = Could,
        };
        var pass = await SpaceFloor.PassAsync(world, ct).ConfigureAwait(false);
        lock (_gate)
        {
            if (pass.WasHeld && First(met, "", "keeping", "floor-held"))
            {
                lines.Add(Look.Retry("tidy", $"tidy  the free space is below its floor, and nothing was freed early this look: {pass.Held?.Said ?? "another run"} "
                    + "holds Daoris's keeping, and a later look tries again."));
            }

            foreach (var act in pass.Acts)
            {
                if (act.Removed > 0)
                {
                    lines.Add(Look.Progress("tidy", $"tidy  the free space is below its floor: removed {act.Removed} of {FloorWhat(act.Kind)} early ({Size(act.Bytes)}); "
                        + "each is made again when it is next needed."));
                }

                foreach (var (why, _) in act.Kept.Where(each => each.Key is SpaceKept.Running or SpaceKept.NoShell or "never"))
                {
                    if (!First(met, "", $"floor/{act.Kind}", "kept-" + why)) continue;
                    lines.Add(Look.Progress("tidy", $"tidy  the free space is below its floor, and {FloorWhat(act.Kind)} stay{(act.Kind == "browser-cache" ? "s" : "")}: {FloorKept(why)}."));
                }
            }

            foreach (var low in pass.Low)
            {
                var read = low.Read!;
                lines.Add(Look.Progress("tidy", $"tidy  {read.Read.Root} has {Size(read.Read.Free)} free, below its floor of {Size(read.Floor ?? 0)}: Daoris tells you and "
                    + "frees nothing by it; the rest is yours to free"
                    + (low.Could is { } could and > 0 ? $" (Free up space… on This Daoris lists about {Size(could)} you could remove)" : " (Free up space… on This Daoris lists what could go)")
                    + "."));
            }

            foreach (var ok in pass.Ok)
            {
                lines.Add(Look.Progress("tidy", $"tidy  {ok.Read.Root} has {Size(ok.Read.Free)} free again, above its floor of {Size(ok.Floor ?? 0)}."));
            }
        }

        return lines;
    }

    /// <summary>
    /// What the person's press could free now, in bytes, of what its list measures: every unit the list names whose size it
    /// knows. Read only when a volume is first said below its floor; null where the list does not read.
    /// </summary>
    private long? Could()
    {
        try
        {
            var plan = SpaceFree.Plan(new SpaceFreeWorld(home) { Sizes = Sizes ?? new SpaceSizes(), Policy = Policy, Now = Clock });
            return plan.Units.Sum(unit => unit.Bytes ?? 0);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or DriverException or HomeEntryTwiceException)
        {
            return null;
        }
    }

    /// <summary>What a floor's act frees, in words.</summary>
    private static string FloorWhat(string kind) => kind switch
    {
        "set-aside" => "the package folders of the trees moved aside",
        "downloads" => "the downloaded versions nothing uses",
        _ => "the browser's cache",
    };

    /// <summary>Why a floor's act kept what it would free, in a clause.</summary>
    private static string FloorKept(string why) => why switch
    {
        SpaceKept.Running => "Daoris's browser is running, and its cache is cleared once it is closed",
        SpaceKept.NoShell => "no Daoris window runs to say whether its browser does",
        _ => "you keep them until you remove them (`never`)",
    };

    /// <summary>Whether anything is downloaded here to keep: either folder of the downloads, or both places of the layout holding one.</summary>
    private bool Downloaded()
    {
        try
        {
            return Directory.Exists(DaorisHome.Entry(home, HomeEntry.Toolchain)) || Directory.Exists(DaorisHome.Entry(home, HomeEntry.ToolVersions));
        }
        catch (HomeEntryTwiceException)
        {
            return true;
        }
    }

    /// <summary>
    /// The downloads' rule, holding the home's run: what it removed said in the report, and each version it kept past its days, and a
    /// record it could not write, said the first look it is met. A day's removal the budget cut short is taken up by the next pass.
    /// </summary>
    private async Task<IReadOnlyList<string>> KeepDownloadsAsync(
        ServiceClient service, KeepingRun run, TimeSpan budget, MachineLog? log, HashSet<string> met, CancellationToken ct)
    {
        var lines = new List<string>();
        var policy = Policy(home).Downloads;
        var now = Clock();
        bool daily;
        lock (_gate) daily = _downloadsDaily is not { } last || now - last >= DownloadsPace || _downloadsDays != policy.Days;
        var markers = DaorisHome.Entry(home, HomeEntry.SessionPids);
        var judge = DownloadJudge.Of(
            home, HarnessSettings.ResolvePath(home), ConfigPath ?? DriverConfig.ResolvePath(home),
            token => Sessions is { } stand ? stand(service, token) : LiveSessions.ReadAsync(service, markers, token), Clock);
        var pass = await DownloadKeeping.PassAsync(home, policy, judge, daily, run.Door, run.Id, log, ct, budget).ConfigureAwait(false);
        lock (_gate)
        {
            if (daily && pass.Unwritten is null && !pass.Cut)
            {
                _downloadsDaily = now;
                _downloadsDays = policy.Days;
            }

            foreach (var each in pass.Cleared)
            {
                var row = each.Version;
                var named = $"{each.Asked.Name} {each.Asked.Version}";
                switch (each.Outcome)
                {
                    case "removed":
                        lines.Add(Look.Progress("tidy", $"tidy  removed the download {named}, which nothing had used for {policy.Days?.TotalDays:0} days ({Size(row?.Bytes ?? 0)}); "
                            + "its next pin or use downloads it again."));
                        break;
                    case "unfinished":
                        lines.Add(Look.Retry("tidy", $"tidy  moved the download {named} aside, unused for {policy.Days?.TotalDays:0} days, and part of it could not be removed yet; "
                            + "nothing runs it, and a later clearing takes the rest."));
                        break;
                    case "kept" when row?.Keeps is { } why && First(met, "", $"download/{named}", "kept-" + why):
                        lines.Add(Look.Progress("tidy", $"tidy  the download {named} stays past its days, since {DownloadKept(why, row.By)}."));
                        break;
                }
            }

            if (pass.Unwritten is { } unwritten && First(met, "", KeepingRecord.PathOf(home), "unwritten"))
            {
                lines.Add(Look.Retry("tidy", $"tidy  {Clause(unwritten)}."));
            }
        }

        return lines;
    }

    /// <summary>Why the downloads' rule kept a version past its days, in a clause (<see cref="DownloadedVersion.Keeps"/>).</summary>
    private static string DownloadKept(string why, IReadOnlyList<string> by) => why switch
    {
        "running" => $"a running session was handed it ({string.Join(", ", by.Select(each => each.Replace("session:", "", StringComparison.Ordinal)))})",
        "pinned" => "it was pinned since",
        "used" => "Tools runs it now",
        "named" => $"{by.FirstOrDefault()} names a file inside it",
        "in-use" => "a program is running from it",
        "unread" => $"what uses it cannot be read now ({by.FirstOrDefault()})",
        _ => why,
    };

    /// <summary>
    /// The machine log's rule, holding the home's run: once a day, or at once after the person's
    /// days changed, each day's file older than those days goes, today's never; <c>last.logs</c> written where one went. Each process's own
    /// prune at its open keeps its thirty, the ceiling of the person's days.
    /// </summary>
    private IReadOnlyList<string> KeepLogs(LogsPolicy policy, KeepingRun run, HashSet<string> met)
    {
        var lines = new List<string>();
        var read = KeepingRecord.Read(home);
        var days = (int)policy.Days.TotalDays;
        var (removed, bytes) = new MachineLog(home, "keeping", Clock).Prune(days);
        lock (_gate)
        {
            _logsDaily = Clock();
            _logsDays = policy.Days;
        }

        if (removed == 0) return lines;
        lines.Add(Look.Of(LookClass.Progress, "tidy", $"tidy  removed {removed} file(s) of the machine log older than {days} days ({Size(bytes)})."));
        if (KeepingRecord.WriteLast(home, read, KeepKinds.Logs.Id, new KeepingLast(Clock(), run.Door, run.Id, removed, bytes, new Dictionary<string, int>())) is { } refused)
        {
            lock (_gate)
            {
                if (First(met, "", KeepingRecord.PathOf(home), "unwritten")) lines.Add(Look.Of(LookClass.Retry, "tidy", $"tidy  {Clause(refused)}."));
            }
        }

        return lines;
    }

    /// <summary>The dependency store's daily eviction, holding the home's run; what went said, and <c>last.deps</c> written where one went.</summary>
    private IReadOnlyList<string> KeepDeps(KeepingRun run, HashSet<string> met)
    {
        var lines = new List<string>();
        var read = KeepingRecord.Read(home);
        var policy = Policy(home);
        var (removed, bytes) = Deps.Keep(home, policy);
        if (removed == 0) return lines;
        lines.Add(Look.Of(LookClass.Progress, "tidy", $"tidy  removed {removed} dependency store entr{(removed == 1 ? "y" : "ies")} not placed in {(int)policy.DepsDays.TotalDays} days or over its limit ({Size(bytes)})."));
        if (KeepingRecord.WriteLast(home, read, KeepKinds.Deps.Id, new KeepingLast(Clock(), run.Door, run.Id, removed, bytes, new Dictionary<string, int>())) is { } refused)
        {
            lock (_gate)
            {
                if (First(met, "", KeepingRecord.PathOf(home), "unwritten")) lines.Add(Look.Of(LookClass.Retry, "tidy", $"tidy  {Clause(refused)}."));
            }
        }

        return lines;
    }

    /// <summary>
    /// Finished history's rule, holding the home's run: once a day, or at once after the
    /// person's age changed, each workspace's finished units past it go through the clear itself, every refusal holding; what went said in
    /// the report and in <c>history.cleared</c> with <c>door: rule</c>, each unit past its days that stayed said once while it stays, and
    /// <c>last.history</c> written where one went. A service that did not answer is said once and tried at the next pass.
    /// </summary>
    private async Task<IReadOnlyList<string>> KeepHistoryAsync(
        ServiceClient service, HistoryPolicy policy, KeepingRun run, MachineLog? log, HashSet<string> met, CancellationToken ct)
    {
        var lines = new List<string>();
        var read = KeepingRecord.Read(home);
        var days = policy.Days!.Value.TotalDays;
        var world = new HistoryWorld(
            service, home, ConfigPath ?? DriverConfig.ResolvePath(home), Processes ?? new SessionProcesses(DaorisHome.Entry(home, HomeEntry.SessionPids)))
        {
            Events = Events,
            Log = log,
            Clock = Clock,
        };

        HistoryKeepingPass pass;
        try
        {
            pass = await HistoryKeeping.PassAsync(world, policy, ct).ConfigureAwait(false);
        }
        catch (Exception error) when (error is DriverException or HttpRequestException or JsonException
                                      || (error is TaskCanceledException && !ct.IsCancellationRequested))
        {
            lock (_gate)
            {
                if (First(met, "", "history", "unread"))
                {
                    lines.Add(Look.Retry("tidy", $"tidy  finished history past {days:0} days was not cleared this look, since the service did not answer: "
                        + $"{Clause(error.Message)}; a later look tries again."));
                }
            }

            return lines;
        }

        // The finished trees' records no session names age by their own `at` under the
        // same age; a service that does not answer is tried again at the next day's clear.
        try
        {
            await service.AgeWorkRecordsAsync(Clock() - policy.Days!.Value, ct).ConfigureAwait(false);
        }
        catch (Exception error) when (error is DriverException or HttpRequestException or JsonException
                                      || (error is TaskCanceledException && !ct.IsCancellationRequested))
        {
            // Said by nothing: the next day's clear asks again.
        }

        var newly = 0;
        lock (_gate)
        {
            _historyDaily = Clock();
            _historyDays = policy.Days;
            if (pass.Cleared > 0)
            {
                lines.Add(Look.Progress("tidy", $"tidy  cleared {pass.Cleared} unit(s) of finished history whose last move was over {days:0} days ago "
                    + $"({Size(pass.Bytes)}), by the age you set."));
            }

            foreach (var unit in pass.Kept)
            {
                var keep = unit.Keep ?? new HistoryKeep(HistoryWords.Unknown, "it could not be judged again");
                if (!First(met, "", $"history/{unit.Kind}/{unit.Id}", "kept-" + keep.Word)) continue;
                newly++;
                lines.Add(Look.Progress("tidy", $"tidy  {(unit.Kind == HistoryKinds.Ask ? "ask" : "quest")} #{unit.Id}'s finished history stays past "
                    + $"{days:0} days: {Clause(keep.Message)}."));
            }
        }

        if (pass.Cleared == 0 && newly == 0) return lines;
        var last = new KeepingLast(Clock(), run.Door, run.Id, pass.Cleared, pass.Bytes, pass.KeptBy);
        if (KeepingRecord.WriteLast(home, read, KeepKinds.History.Id, last) is { } refused)
        {
            lock (_gate)
            {
                if (First(met, "", KeepingRecord.PathOf(home), "unwritten")) lines.Add(Look.Retry("tidy", $"tidy  {Clause(refused)}."));
            }
        }

        return lines;
    }

    /// <summary>
    /// The finished trees' steps after the hold, before the purge reads their classes: each stopped or
    /// refused folder resumed, each record due composed from its manifest and posted, each recorded folder's holding check at its hour. Each
    /// folder that waits is said the first look it is met, with why.
    /// </summary>
    private async Task<IReadOnlyList<string>> RecordSetAsideAsync(
        SessionTrees trees, ServiceClient service, Func<string, string, string?>? roots, HashSet<string> met, Func<bool> spent, CancellationToken ct)
    {
        var lines = new List<string>();
        var steps = await trees.RecordSetAsideAsync(roots, service.PostWorkRecordAsync, Clock, ct, spent: spent).ConfigureAwait(false);
        lock (_gate)
        {
            // A record kept is no line of the look's: the folder's removal at its hour says it.
            foreach (var step in steps)
            {
                if (step.Why is { } why && First(met, step.Folder, "record", why)) lines.Add(Look.Retry("tidy", $"tidy  {Path.GetFileName(step.Folder)} waits: {Clause(why)}."));
            }
        }

        return lines;
    }

    /// <summary>The registry's repositories; null where the service did not say, so no step needing a checkout runs.</summary>
    private static async Task<IReadOnlyList<RepoView>?> RegistryAsync(ServiceClient service, CancellationToken ct)
    {
        try
        {
            return await service.RegistryAsync(ct).ConfigureAwait(false);
        }
        catch (Exception error) when (error is DriverException or HttpRequestException or System.Text.Json.JsonException)
        {
            return null;
        }
    }

    /// <summary>The set-aside kind's removal, holding the home's run, a recorded folder's holding check in the root <paramref name="roots"/> gives.</summary>
    private async Task<IReadOnlyList<string>> KeepSetAsideAsync(
        SessionTrees trees, KeepingRun run, MachineLog? log, HashSet<string> met, Func<string, string, string?>? roots, TimeSpan budget, CancellationToken ct)
    {
        var lines = new List<string>();
        var read = KeepingRecord.Read(home);
        var done = await trees.KeepSetAsideAsync(Policy(home), Clock(), budget, ct, roots).ConfigureAwait(false);
        lines.AddRange(SayPurged(done.Purged, log, met));
        lines.AddRange(SaySlimmed(done.Slims, log, met));
        var removed = done.Purged.Count(folder => folder.Purged) + done.Slims.Sum(slim => slim.Removed.Count);
        if (removed == 0 && done.Slims.All(slim => slim.Kept.Count == 0)) return lines;
        var kept = done.Purged.Where(folder => !folder.Purged).Select(folder => folder.Code ?? SetAsideKept.Unread)
            .Concat(done.Slims.SelectMany(slim => slim.Kept).Select(path => path.Why ?? SlimKept.Gone))
            .GroupBy(why => why, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
        var bytes = done.Purged.Sum(folder => folder.Bytes) + done.Slims.Sum(slim => slim.Bytes);
        if (KeepingRecord.WriteLast(home, read, KeepKinds.SetAside.Id, new KeepingLast(Clock(), run.Door, run.Id, removed, bytes, kept)) is { } refused)
        {
            lock (_gate)
            {
                if (First(met, "", KeepingRecord.PathOf(home), "unwritten")) lines.Add(Look.Retry("tidy", $"tidy  {Clause(refused)}."));
            }
        }

        return lines;
    }

    /// <summary>
    /// What the slim says: each folder's package folders dropped, with <c>tidied.slimmed</c>; and each
    /// folder or path it kept, and each removal that failed, said the first look it is met. The log names the folder from the home, as
    /// <c>tidied.purged</c> does, and counts; never a path inside it.
    /// </summary>
    private IReadOnlyList<string> SaySlimmed(IReadOnlyList<SlimResult> slims, MachineLog? log, HashSet<string> met)
    {
        var lines = new List<string>();
        lock (_gate)
        {
            foreach (var slim in slims)
            {
                if (slim.Removed.Count > 0)
                {
                    lines.Add(Look.Progress("tidy", $"tidy  dropped the package folders of the folder moved aside to {slim.Folder}: {string.Join(", ", slim.Removed)} "
                        + $"({Size(slim.Bytes)}); `npm ci` beside each `package.json` makes them again, and the rest of it waits its days."));
                }

                if (slim.Said is { } said && First(met, "", slim.Folder, "slim-" + SlimKept.Written))
                {
                    lines.Add(Look.Progress("tidy", $"tidy  the folder moved aside to {slim.Folder} keeps its package folders for its days, since {Clause(said)}."));
                }

                foreach (var path in slim.Kept.Where(path => slim.Said is null))
                {
                    if (!First(met, "", $"{slim.Folder}/{path.Path}", "slim-" + path.Why)) continue;
                    lines.Add(Look.Progress("tidy", $"tidy  `{path.Path}` in the folder moved aside to {slim.Folder} stays for its days, since {KeptWhy(path.Why)}."));
                }

                foreach (var path in slim.Failed)
                {
                    if (!First(met, "", $"{slim.Folder}/{path.Path}", "slim-failed")) continue;
                    lines.Add(Look.Retry("tidy", $"tidy  `{path.Path}` in the folder moved aside to {slim.Folder} could not be removed yet ({Clause(path.Why ?? "")}); "
                        + "a later look tries again."));
                }

                if (slim.Unread is { } unread && First(met, "", slim.Folder, "slim-" + SetAsideKept.Unread))
                {
                    lines.Add(Look.Retry("tidy", $"tidy  the folder moved aside to {slim.Folder} keeps its package folders this look, since {Clause(unread)}."));
                }

                if (slim.Removed.Count > 0 || slim.Kept.Count > 0)
                {
                    log?.Info("tidied.slimmed",
                        ("folder", Path.GetRelativePath(home, slim.Folder).Replace('\\', '/')), ("removed", slim.Removed.Count), ("bytes", slim.Bytes),
                        ("kept", slim.Kept.Count));
                }
            }
        }

        return lines;
    }

    /// <summary>Why the slim kept a path, in a clause (<see cref="SlimKept"/>).</summary>
    private static string KeptWhy(string? why) => why switch
    {
        SlimKept.Linked => "a link lies on the way to it, or it is one",
        SlimKept.Refused => "its path does not name a package folder inside the folder",
        SlimKept.Gone => "it is no longer there as a folder",
        _ => "something in the folder was written after it was moved aside",
    };
}
