using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Daoris.Driver;

/// <summary>
/// One volume as the operating system reads it (KEEP1f; the cleanup design §3.2): its identity, the free bytes this account may use and
/// its size. Two entries on one volume read the same <see cref="Id"/>.
/// </summary>
/// <param name="Id">The volume's identity as the system gives it: <c>\\?\Volume{…}\</c> on Windows, the mount's root elsewhere.</param>
/// <param name="Root">Where the volume is mounted, as a person reads it: a drive's root, or a mounted folder.</param>
public sealed record VolumeReading(string Id, string Root, long Free, long Total);

/// <summary>
/// The free-space read (KEEP1f; the cleanup design §3.2, Fable F4.3): the volume that holds a folder, read for that folder's own path, so
/// a folder reached through a junction, or a folder a volume is mounted at, answers for its own volume, never its drive letter's. Never a
/// walk; one read took 0.3 ms on the install.
/// </summary>
public static class SpaceVolumes
{
    /// <summary>
    /// The volume holding <paramref name="path"/>, through any junction or mount on the way; the nearest folder that is there is read where
    /// the path is not yet. Null where it cannot be read: a failed read runs no floor act (§6 point 7).
    /// </summary>
    public static VolumeReading? Read(string path)
    {
        try
        {
            var at = Path.GetFullPath(path);
            while (!Directory.Exists(at))
            {
                var up = Path.GetDirectoryName(at);
                if (string.IsNullOrEmpty(up) || up == at) return null;
                at = up;
            }

            return OperatingSystem.IsWindows() ? Windows(at) : Mounted(at);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or ExternalException)
        {
            return null;
        }
    }

    /// <summary>
    /// Windows: the folder opened and its final path asked by the volume's GUID, which follows every junction and mount on the way, then
    /// <c>GetDiskFreeSpaceExW</c> on that volume's root.
    /// </summary>
    private static VolumeReading? Windows(string folder)
    {
        using var handle = CreateFileW(folder, ReadAttributes, ShareAll, IntPtr.Zero, OpenExisting, BackupSemantics, IntPtr.Zero);
        if (handle.IsInvalid) return null;
        var guid = FinalPath(handle, VolumeNameGuid);
        var dos = FinalPath(handle, VolumeNameDos);
        if (guid is null || !guid.StartsWith(@"\\?\Volume{", StringComparison.OrdinalIgnoreCase)) return null;
        var end = guid.IndexOf('}', StringComparison.Ordinal);
        if (end < 0) return null;
        var id = guid[..(end + 1)] + "\\";
        if (!GetDiskFreeSpaceExW(id, out var free, out var total, out _)) return null;
        var root = dos is not null && GetVolumePathNameW(dos, out var mount) ? Plain(mount) : id;
        return new VolumeReading(id, root, (long)free, (long)total);
    }

    /// <summary>Elsewhere: the mount whose root is the longest that holds the folder's real path, read through <see cref="DriveInfo"/>.</summary>
    private static VolumeReading? Mounted(string folder)
    {
        var real = new DirectoryInfo(folder).ResolveLinkTarget(returnFinalTarget: true)?.FullName ?? folder;
        var drive = DriveInfo.GetDrives()
            .Where(each => real.StartsWith(Path.TrimEndingDirectorySeparator(each.RootDirectory.FullName) + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                           || real == Path.TrimEndingDirectorySeparator(each.RootDirectory.FullName) || each.RootDirectory.FullName == "/")
            .OrderByDescending(each => each.RootDirectory.FullName.Length)
            .FirstOrDefault();
        return drive is { IsReady: true } ? new VolumeReading(drive.RootDirectory.FullName, drive.RootDirectory.FullName, drive.AvailableFreeSpace, drive.TotalSize) : null;
    }

    private static string? FinalPath(SafeFileHandle handle, uint flags)
    {
        var buffer = new char[1024];
        var length = GetFinalPathNameByHandleW(handle, buffer, (uint)buffer.Length, flags);
        if (length == 0 || length >= buffer.Length) return null;
        return new string(buffer, 0, (int)length);
    }

    private static bool GetVolumePathNameW(string path, out string mount)
    {
        var buffer = new char[1024];
        var read = GetVolumePathNameW(path, buffer, (uint)buffer.Length);
        mount = read ? new string(buffer).TrimEnd('\0') : "";
        return read && mount.Length > 0;
    }

    /// <summary>A path the system answered with its long prefix, as a person reads it.</summary>
    private static string Plain(string path) => path.StartsWith(@"\\?\", StringComparison.Ordinal) && !path.StartsWith(@"\\?\Volume{", StringComparison.OrdinalIgnoreCase)
        ? path[4..]
        : path;

    private const uint ReadAttributes = 0x80;
    private const uint ShareAll = 0x7;
    private const uint OpenExisting = 3;
    private const uint BackupSemantics = 0x02000000;
    private const uint VolumeNameDos = 0x0;
    private const uint VolumeNameGuid = 0x1;

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFileW(
        string name, uint access, uint share, IntPtr security, uint disposition, uint flags, IntPtr template);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint GetFinalPathNameByHandleW(SafeFileHandle file, [Out] char[] path, uint length, uint flags);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetDiskFreeSpaceExW(string directory, out ulong freeToCaller, out ulong total, out ulong totalFree);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetVolumePathNameW(string path, [Out] char[] mount, uint length);
}

/// <summary>
/// A volume the floor judges (KEEP1f): its read, its floor by the person's rule and its own size, whether it is below, and the entries of
/// Daoris's that lie on it (<c>folder</c> for the Daoris folder itself, then <c>trees</c>, <c>toolchain</c>, <c>tool-versions</c>,
/// <c>browser-engine</c>).
/// </summary>
public sealed record FloorVolume(VolumeReading Read, long? Floor, IReadOnlyList<string> Entries)
{
    /// <summary>Below its floor now.</summary>
    public bool Below => Floor is { } floor && Read.Free < floor;
}

/// <summary>
/// One stretch below the floor on one volume (KEEP1f; the cleanup design §3.2): from the first pass that finds it below to the one that
/// finds it above its floor by the margin. What the floor's acts freed in it, when they last ran, whether the browser's caches were cleared
/// in it, and whether it was said: <c>space.low</c> and the row in <i>What needs you</i>, once.
/// </summary>
public sealed record FloorStretch(string Volume, DateTimeOffset Since)
{
    /// <summary>When the floor's acts last ran for it: at most once an hour while below.</summary>
    public DateTimeOffset? Acted { get; init; }

    /// <summary>The browser's caches were cleared, or could never be asked, in this stretch: once a stretch (Fable F4.1).</summary>
    public bool Caches { get; init; }

    /// <summary>What the floor's acts freed in this stretch, in bytes.</summary>
    public long Freed { get; init; }

    /// <summary>When it last freed anything.</summary>
    public DateTimeOffset? FreedAt { get; init; }

    /// <summary>Said: still below once its acts ran, so <c>space.low</c> was written and the row stands until <c>space.ok</c>.</summary>
    public DateTimeOffset? Said { get; init; }

    /// <summary>The volume as it was read when it was said: its root, free space, size and floor.</summary>
    public FloorVolume? Read { get; init; }

    /// <summary>What the person's press could free when it was said, in bytes, of what the list could measure; null where none was read.</summary>
    public long? Could { get; init; }
}

/// <summary>
/// The stretches below the floor this process knows, per home (KEEP1f): what the look's pass keeps across passes, and what the
/// <i>Disk space</i> card and <i>What needs you</i> read. Kept for the process, as <see cref="SpaceRuns"/> keeps the press's runs; a new
/// process meets a disk still low as a new stretch, and says it once again.
/// </summary>
public sealed class SpaceFloors
{
    /// <summary>This process's.</summary>
    public static SpaceFloors Shared { get; } = new();

    private readonly ConcurrentDictionary<string, IReadOnlyDictionary<string, FloorStretch>> _homes = new(StringComparer.Ordinal);

    /// <summary>Told whenever a stretch is said or ends, so <i>What needs you</i> and the card are judged again.</summary>
    public event Action<string>? Changed;

    /// <summary>The stretches under way for <paramref name="home"/>, by volume.</summary>
    public IReadOnlyDictionary<string, FloorStretch> Of(string home) =>
        _homes.TryGetValue(Key(home), out var stretches) ? stretches : new Dictionary<string, FloorStretch>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Every said stretch of every home this process keeps: what <i>What needs you</i> says.</summary>
    public IReadOnlyList<FloorStretch> Said() => [.. _homes.Values.SelectMany(each => each.Values).Where(each => each.Said is not null)];

    internal void Set(string home, IReadOnlyDictionary<string, FloorStretch> stretches, bool told)
    {
        _homes[Key(home)] = stretches;
        if (told) Changed?.Invoke(home);
    }

    private static string Key(string home)
    {
        var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(home));
        return OperatingSystem.IsWindows() ? full.ToUpperInvariant() : full;
    }
}

/// <summary>What the floor's pass is handed (<see cref="SpaceFloor.PassAsync"/>): the home, and the reads and seams a test stands in for.</summary>
public sealed record FloorWorld(string Home)
{
    /// <summary>A folder's volume; the system's by default (<see cref="SpaceVolumes.Read"/>).</summary>
    public Func<string, VolumeReading?> Volume { get; init; } = SpaceVolumes.Read;

    /// <summary>What time it is.</summary>
    public Func<DateTimeOffset> Now { get; init; } = () => DateTimeOffset.UtcNow;

    /// <summary>The rules kept by; the home's by default.</summary>
    public Func<string, KeepPolicy> Policy { get; init; } = KeepPolicy.Of;

    /// <summary>How the downloads are judged right before each goes (KEEP1b's act).</summary>
    public Func<DownloadJudge>? Judge { get; init; }

    /// <summary>The shell's browser host; null with no shell to say whether the browser runs, which keeps its caches.</summary>
    public IBrowserCaches? Caches { get; init; }

    /// <summary>The machine log: <c>space.low</c>, <c>space.ok</c>, <c>space.freed</c> and <c>space.kept</c>.</summary>
    public MachineLog? Log { get; init; }

    /// <summary>A rename aside; a test's refuses, as a program holding the folder makes Windows refuse.</summary>
    public Action<string, string>? Move { get; init; }

    /// <summary>The stretches kept across passes.</summary>
    public SpaceFloors Floors { get; init; } = SpaceFloors.Shared;

    /// <summary>How long the acts remove for, at most (the cleanup design §3.1).</summary>
    public TimeSpan Budget { get; init; } = SetAsideKeeping.DefaultBudget;

    /// <summary>What the person's press could free, in bytes, of what its list measures; null where it does not read.</summary>
    public Func<long?>? Could { get; init; }
}

/// <summary>One of the floor's acts, as its pass ran it: its kind, what it removed and what it kept, by why.</summary>
public sealed record FloorAct(string Kind, int Removed, long Bytes, IReadOnlyDictionary<string, int> Kept);

/// <summary>What one pass of the floor did (<see cref="SpaceFloor.PassAsync"/>), for the look's report.</summary>
public sealed record FloorPass(IReadOnlyList<FloorAct> Acts, IReadOnlyList<FloorStretch> Low, IReadOnlyList<FloorVolume> Ok)
{
    /// <summary>Nothing read, nothing done: the floor is off, or no volume read.</summary>
    public static FloorPass Nothing { get; } = new([], [], []);

    /// <summary>Another run held the home's run, so the acts due were not run; who, where it is named.</summary>
    public KeepingHolder? Held { get; init; }

    /// <summary>The acts were due but held elsewhere.</summary>
    public bool WasHeld { get; init; }

    /// <summary>A volume that could not be read, by the entry it holds: nothing of its acts ran.</summary>
    public IReadOnlyList<string> Unread { get; init; } = [];
}

/// <summary>
/// The free-space floor (KEEP1f, D195; the cleanup design §3.2): below it, Daoris frees early what it can make again, and says the rest.
/// </summary>
/// <remarks>
/// <para><b>Measured per volume</b> (Fable F4.3): the Daoris folder's own path and each entry the acts free from (the trees home, the
/// downloads, the browser's profile) are read for their own volume, since the trees home may sit on another through a junction; each
/// volume's floor is computed from its own size, each act runs where its own entry's volume is below, and a volume two entries share is
/// said once.</para>
/// <para><b>Below it</b>, at most once an hour while below, these and only these, each through its own act with every guard of its own
/// (§3.5): (1) the slims due (<see cref="SetAsideKeeping"/>: the hour's settle and nothing written since the move still hold); (2) unused
/// downloads past the hour's settling, their days ignored, never one under <c>never</c> (<see cref="DownloadClearing.ClearAsync"/>: every
/// use guard holds); (3) the browser's caches, once in each stretch below, only while the shell says its browser does not run
/// (<see cref="BrowserCacheClearing"/>). Each runs holding the home's run lock, door <c>floor</c>.</para>
/// <para><b>Still below after them, it is said, never taken</b>: <c>space.low</c> once, and the row in <i>What needs you</i>, until the
/// free space is above the floor by the smaller of 5 GB and 5% of the volume (<c>space.ok</c>).</para>
/// </remarks>
public static class SpaceFloor
{
    /// <summary>How often the acts run while a volume stays below, at most.</summary>
    public static readonly TimeSpan Hourly = TimeSpan.FromHours(1);

    /// <summary>The entries the floor reads, in its order: the Daoris folder itself, then what each act frees from.</summary>
    public static IReadOnlyList<string> Entries { get; } = ["folder", HomeEntry.Trees.Name, HomeEntry.Toolchain.Name, HomeEntry.ToolVersions.Name, HomeEntry.BrowserEngine.Name];

    /// <summary>
    /// Each entry's volume, read now, a volume two entries share read once; and the entries whose volume did not read. A floor that is off
    /// reads every volume all the same, so the card says the free space.
    /// </summary>
    public static (IReadOnlyList<FloorVolume> Volumes, IReadOnlyList<string> Unread) Read(string home, FloorPolicy policy, Func<string, VolumeReading?> volume)
    {
        var volumes = new List<(VolumeReading Read, List<string> Entries)>();
        var unread = new List<string>();
        foreach (var entry in Entries)
        {
            string? path;
            try
            {
                path = entry == "folder" ? DaorisHome.FolderOf(home).Folder : DaorisHome.Entry(home, HomeEntry.Named(entry)!);
            }
            catch (HomeEntryTwiceException)
            {
                path = null;
            }

            if (path is null || volume(path) is not { } read)
            {
                unread.Add(entry);
                continue;
            }

            var at = volumes.FindIndex(each => string.Equals(each.Read.Id, read.Id, StringComparison.OrdinalIgnoreCase));
            if (at < 0) volumes.Add((read, [entry]));
            else volumes[at].Entries.Add(entry);
        }

        return ([.. volumes.Select(each => new FloorVolume(each.Read, policy.Of(each.Read.Total), each.Entries))], unread);
    }

    /// <summary>
    /// One pass of the floor, from the look's Keeping pass: the volumes read, each stretch begun or ended, the acts due run in order holding
    /// the home's run lock, and each volume still below said once. Never throws for a read that fails: that volume runs nothing.
    /// </summary>
    public static async Task<FloorPass> PassAsync(FloorWorld world, CancellationToken ct = default)
    {
        var home = world.Home;
        var now = world.Now();
        var policy = world.Policy(home);
        var before = world.Floors.Of(home);
        var stretches = new Dictionary<string, FloorStretch>(before, StringComparer.OrdinalIgnoreCase);
        var told = false;
        if (policy.Floor.Off)
        {
            // No floor: every stretch is forgotten, said nowhere; the card says the floor is off.
            world.Floors.Set(home, new Dictionary<string, FloorStretch>(StringComparer.OrdinalIgnoreCase), told: before.Count > 0);
            return FloorPass.Nothing;
        }

        var (volumes, unread) = Read(home, policy.Floor, world.Volume);
        var ok = new List<FloorVolume>();
        foreach (var volume in volumes)
        {
            var id = volume.Read.Id;
            if (volume.Below && !stretches.ContainsKey(id)) stretches[id] = new FloorStretch(id, now);
            else if (!volume.Below && stretches.TryGetValue(id, out var stretch)
                     && volume.Read.Free >= volume.Floor!.Value + FloorPolicy.Margin(volume.Read.Total))
            {
                // Above the floor by the margin: the stretch ends, said where it was said.
                stretches.Remove(id);
                if (stretch.Said is not null)
                {
                    world.Log?.Info("space.ok", ("free", volume.Read.Free), ("floor", volume.Floor));
                    ok.Add(volume);
                    told = true;
                }
            }
        }

        // A volume that did not read keeps its stretch as it stands: a failed read runs nothing and ends nothing.
        var acts = new List<FloorAct>();
        var due = volumes.Where(volume => volume.Below && (stretches[volume.Read.Id].Acted is not { } acted || now - acted >= Hourly)).ToList();
        var slims = due.Any(volume => volume.Entries.Contains(HomeEntry.Trees.Name));
        var downloads = due.Any(volume => volume.Entries.Contains(HomeEntry.Toolchain.Name) || volume.Entries.Contains(HomeEntry.ToolVersions.Name));
        var cachesOn = volumes.FirstOrDefault(volume => volume.Entries.Contains(HomeEntry.BrowserEngine.Name));
        var caches = cachesOn is not null && due.Contains(cachesOn) && !stretches[cachesOn.Read.Id].Caches;
        var held = false;
        KeepingHolder? holder = null;
        if (slims || downloads || caches)
        {
            using var run = KeepingRun.TryTake(home, KeepingRun.Floor);
            if (run is null)
            {
                held = true;
                holder = KeepingRun.Holder(home);
            }
            else
            {
                var clock = System.Diagnostics.Stopwatch.StartNew();
                bool Spent() => clock.Elapsed >= world.Budget || ct.IsCancellationRequested;
                if (slims) acts.Add(Slims(world, policy, now, run, Spent, ct));
                if (downloads) acts.Add(await DownloadsAsync(world, policy, run, Spent, ct).ConfigureAwait(false));
                if (caches)
                {
                    var cleared = await CachesAsync(world, run, ct).ConfigureAwait(false);
                    acts.Add(cleared);
                    // Cleared, or never to be asked with no shell: once a stretch. A running browser's are tried at the next hour's acts.
                    if (!cleared.Kept.ContainsKey(SpaceKept.Running)) stretches[cachesOn!.Read.Id] = stretches[cachesOn.Read.Id] with { Caches = true };
                }

                var freed = acts.Sum(act => act.Bytes);
                foreach (var volume in due)
                {
                    var stretch = stretches[volume.Read.Id];
                    stretches[volume.Read.Id] = stretch with
                    {
                        Acted = now,
                        Freed = stretch.Freed + freed,
                        FreedAt = freed > 0 ? now : stretch.FreedAt,
                    };
                }
            }
        }

        // Still below after the acts, said once: read again where an act ran, so what they freed counts.
        var after = acts.Count > 0 ? Read(home, policy.Floor, world.Volume).Volumes : volumes;
        var low = new List<FloorStretch>();
        if (!held)
        {
            long? could = null;
            var couldRead = false;
            foreach (var volume in after.Where(volume => volume.Below && stretches.ContainsKey(volume.Read.Id)))
            {
                var stretch = stretches[volume.Read.Id];
                if (stretch.Said is not null)
                {
                    stretches[volume.Read.Id] = stretch with { Read = volume };
                    continue;
                }

                if (!couldRead)
                {
                    could = world.Could?.Invoke();
                    couldRead = true;
                }

                var said = stretch with { Said = now, Read = volume, Could = could };
                stretches[volume.Read.Id] = said;
                world.Log?.Warn("space.low", ("free", volume.Read.Free), ("floor", volume.Floor));
                low.Add(said);
                told = true;
            }
        }

        // A stretch whose volume no entry lies on any more is gone with it; while an entry does not read, none is judged gone.
        if (unread.Count == 0)
        {
            foreach (var gone in stretches.Keys.Where(id => !volumes.Any(volume => string.Equals(volume.Read.Id, id, StringComparison.OrdinalIgnoreCase))).ToList())
            {
                stretches.Remove(gone);
                told = true;
            }
        }

        world.Floors.Set(home, stretches, told || acts.Any(act => act.Bytes > 0));
        return new FloorPass(acts, low, ok) { Held = holder, WasHeld = held, Unread = unread };
    }

    /// <summary>The first act: the slims due, the purge left to its own days. The hour's settle and nothing written since still hold.</summary>
    private static FloorAct Slims(FloorWorld world, KeepPolicy policy, DateTimeOffset now, KeepingRun run, Func<bool> spent, CancellationToken ct)
    {
        var trees = new SessionTrees(world.Home);
        if (!Directory.Exists(trees.SetAsideRoot) || policy.SetAside.Whole) return new FloorAct(KeepKinds.SetAside.Id, 0, 0, new Dictionary<string, int>());
        var done = trees.KeepSetAside(policy, now, world.Budget, ct, purge: _ => false);
        var removed = done.Slims.Sum(slim => slim.Removed.Count);
        var bytes = done.Slims.Sum(slim => slim.Bytes);
        var kept = done.Slims.SelectMany(slim => slim.Kept).Select(path => path.Why ?? SlimKept.Gone)
            .GroupBy(why => why, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
        var act = new FloorAct(KeepKinds.SetAside.Id, removed, bytes, kept);
        Said(world, act, run);
        return act;
    }

    /// <summary>
    /// The second act: every downloaded version nothing uses, its days ignored, through CACHECLEAR1's act, each judged again under the use
    /// files' locks with a running session's versions kept and a version downloaded within the hour kept (<c>recent</c>). None under
    /// <c>never</c>, which is the press alone, below the floor too (Codex 3.1); none while <c>keeping.json</c> does not read.
    /// </summary>
    private static async Task<FloorAct> DownloadsAsync(FloorWorld world, KeepPolicy policy, KeepingRun run, Func<bool> spent, CancellationToken ct)
    {
        var kept = new Dictionary<string, int>(StringComparer.Ordinal);
        if (world.Judge is not { } judging)
        {
            return new FloorAct(KeepKinds.Downloads.Id, 0, 0, kept);
        }

        var judge = judging();
        var listing = DownloadClearing.List(world.Home, judge.Uses(), judge.Now(), ct, measure: false);
        var unused = listing.Versions.Where(row => row.Keeps is null).ToList();
        if (listing.Why is not null || unused.Count == 0) return new FloorAct(KeepKinds.Downloads.Id, 0, 0, kept);
        if (policy.Downloads.Days is null)
        {
            kept["never"] = unused.Count;
            var never = new FloorAct(KeepKinds.Downloads.Id, 0, 0, kept);
            Said(world, never, run, writeLast: false);
            return never;
        }

        var record = KeepingRecord.Read(world.Home);
        if (record.Problem is not null)
        {
            kept["unread"] = unused.Count;
            return new FloorAct(KeepKinds.Downloads.Id, 0, 0, kept);
        }

        var cleared = await DownloadClearing.ClearAsync(
            world.Home, [.. unused.Select(row => new DownloadAsked(row.Kind, row.Name, row.Version, row.Unfinished))],
            judge with { FirstUnused = key => record.Unused.TryGetValue(key, out var since) ? since : null },
            world.Log, world.Move, ct, KeepingRun.Floor, spent).ConfigureAwait(false);
        var went = cleared.Where(each => each.Outcome is "removed" or "unfinished").ToList();
        foreach (var each in cleared.Where(each => each.Outcome == "kept"))
        {
            var why = each.Version?.Keeps ?? "unread";
            kept[why] = kept.GetValueOrDefault(why) + 1;
        }

        var act = new FloorAct(KeepKinds.Downloads.Id, went.Count, went.Sum(each => each.Version?.Bytes ?? 0), kept);
        Said(world, act, run);
        return act;
    }

    /// <summary>
    /// The third act: the browser's caches, the table's paths alone, while the shell's host says its browser is not running, holding its
    /// start gate through the clear. With no shell, kept (<c>no-shell</c>); while it runs, kept (<c>running</c>).
    /// </summary>
    private static async Task<FloorAct> CachesAsync(FloorWorld world, KeepingRun run, CancellationToken ct)
    {
        var kept = new Dictionary<string, int>(StringComparer.Ordinal);
        FloorAct act;
        if (world.Caches is not { } caches)
        {
            kept[SpaceKept.NoShell] = 1;
            act = new FloorAct("browser-cache", 0, 0, kept);
        }
        else
        {
            var cleared = await caches.WhileStoppedAsync(() => BrowserCacheClearing.Clear(world.Home, SpaceKinds.BrowserCaches, world.Move), ct).ConfigureAwait(false);
            if (cleared is null)
            {
                kept[SpaceKept.Running] = 1;
                act = new FloorAct("browser-cache", 0, 0, kept);
            }
            else
            {
                // A path the profile does not hold is no keep: there was nothing to clear.
                foreach (var why in cleared.Kept.Values.Where(why => why != SpaceKept.Gone)) kept[why] = kept.GetValueOrDefault(why) + 1;
                act = new FloorAct("browser-cache", cleared.Removed.Count, cleared.Removed.Values.Sum(), kept);
            }
        }

        Said(world, act, run);
        return act;
    }

    /// <summary>An act's end said: its kind's <c>last</c> in <c>keeping.json</c> (door <c>floor</c>), and <c>space.freed</c> and <c>space.kept</c>.</summary>
    private static void Said(FloorWorld world, FloorAct act, KeepingRun run, bool writeLast = true)
    {
        if (act.Removed == 0 && act.Kept.Count == 0) return;
        if (writeLast)
        {
            KeepingRecord.WriteLast(world.Home, KeepingRecord.Read(world.Home), act.Kind,
                new KeepingLast(world.Now(), run.Door, run.Id, act.Removed, act.Bytes, act.Kept));
        }

        if (act.Removed > 0)
        {
            world.Log?.Info("space.freed", ("kind", act.Kind), ("count", act.Removed), ("bytes", act.Bytes), ("door", run.Door), ("early", true), ("run", run.Id));
        }

        foreach (var (why, count) in act.Kept.OrderBy(each => each.Key, StringComparer.Ordinal))
        {
            world.Log?.Info("space.kept", ("kind", act.Kind), ("why", why), ("count", count));
        }
    }
}
