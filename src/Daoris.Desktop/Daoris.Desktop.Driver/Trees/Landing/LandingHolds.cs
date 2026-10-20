using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// One landing of one session's tree at a time, and one push of one landed branch, whichever door (LANDRACE1, D145 point 2, D173's
/// FABRIC-END7 note and its review). The person's press, a terminal's <c>trees land</c>, an entry's Accept and the look's automatic
/// landing each take the tree's hold around the whole landing (<c>TreeLands.LandAsync</c>); the landing's plugin step and a hand-off
/// (<c>TreeHandOffs.HandAsync</c>) each take the branch's around the push.
/// </summary>
/// <remarks>
/// <para><b>Why.</b> Git's ref writes keep two landings of a made branch apart (a branch made once, an advance by compare-and-swap),
/// but a tree on a branch of its own (TREEFOLLOW1) has no ref made for it, so both would land it and its plugin would push twice; a
/// hand-off of a branch whose landing is still pushing would push it again and open a second pull request; and a door that comes
/// while another pushes is owed what happened, not git's refusal.</para>
///
/// <para><b>What is held is the folder, not its name.</b> A tree is keyed by its folder's identity (<see cref="FolderIdentity"/>). A
/// branch is keyed by its repository's common git folder's identity and its name: the common folder is read from git's own files
/// (<see cref="TreeTips.CommonDirectory"/>, no process), so the main checkout and any linked worktree of it, under any spelling, name
/// one repository. A junction, a <c>subst</c> drive, a short name or a long-path prefix takes the same hold. A folder with no identity
/// (gone, unreadable, or off Windows) is keyed by its spelling.</para>
///
/// <para><b>In this process, then across processes.</b> A semaphore per key keeps this process's doors apart without polling; it is
/// dropped once nobody holds or waits for it, so they never accumulate. A file under the home's <c>locks/landings/</c>, opened alone,
/// keeps this process apart from another (a terminal's press beside the desktop's look): the handle is the lock, as
/// <see cref="TreeLock"/>'s and <see cref="RecordLock"/>'s are, so a process that dies lets go with it.</para>
///
/// <para><b>Swept once nobody can be owed them</b> (LANDHOLDSWEEP1). A hold stamps its file's written time when it is taken and when
/// it is let go, so the time says when a door last held it. A file no door has held for longer than <see cref="Wait"/> is owed to
/// nobody: a door waiting behind its last holder took it within <see cref="Poll"/> of the release, and one that came later waited for
/// nothing and clears it. <see cref="Sweep"/> removes such a file under its own handle, opened alone, after reading the time again
/// through it, so a door that took and let go of it since is kept; a held file does not open, and stays. A taken hold sweeps its home
/// at most once per <see cref="Wait"/> in a process. The age is the wall clock's, as the file's time is: a clock stepped forward past
/// the bound can sweep a file a waiting door would have read, and that door judges the work as it stands, as before LANDRACE1.</para>
///
/// <para><b>What the one before came to, not when.</b> The holder keeps what its attempt came to in the file
/// (<see cref="LandingHold.Keep"/>): the branch, the commit, who accepted it, and its plugin's answer once it has one. A hold that
/// waited reads it as <see cref="LandingHold.Before"/>, and compares commits, never moments, so a clock that steps changes nothing.
/// One that waited for nothing clears it, so an older attempt's outcome is never read as the one waited behind; one that waited and
/// kept nothing of its own leaves it for the next. A write reaches the system's cache, which every process reads, and is not forced to
/// the disk: it matters only to a door waiting now.</para>
///
/// <para><b>Waited for, within a bound.</b> A door waits while another holds the key, up to <see cref="Wait"/> (longer than a
/// plugin's two minutes) by a monotonic clock, and then takes nothing: "cannot tell" is never "land anyway". A plugin that calls
/// back into the landing that started it waits on that landing's own hold, and is ended by the landing's patience with it.</para>
/// </remarks>
internal static class LandingHolds
{
    /// <summary>How long a door waits for another holding the same tree or branch: past a plugin's own patience, and a tidy.</summary>
    public static readonly TimeSpan Wait = LandingPlugins.DefaultPatience + TimeSpan.FromMinutes(3);

    private static readonly TimeSpan Poll = TimeSpan.FromMilliseconds(50);

    // This process's gate per key, while anyone holds or waits for it; guarded by itself.
    private static readonly Dictionary<string, KeyGate> Gates = new(StringComparer.Ordinal);

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    // LANDHOLDSWEEP1: when this process last swept each home, by the monotonic clock; guarded by itself.
    private static readonly Dictionary<string, long> Swept = new(StringComparer.Ordinal);

    /// <summary>Hold the tree for one landing, waiting while another landing of it holds it, here or in another process.</summary>
    /// <returns>The hold, which knows whether it waited and what the one before kept; null where it was held for the whole of <paramref name="within"/>.</returns>
    public static Task<LandingHold?> TakeAsync(string home, string tree, CancellationToken ct = default, TimeSpan? within = null) =>
        HoldAsync(home, TreeKey(tree), ct, within);

    /// <summary>Hold a landed branch for one push, waiting while another push of it holds it, here or in another process.</summary>
    /// <param name="root">A checkout of the repository, the main one or a linked worktree: its common git folder names the repository.</param>
    public static Task<LandingHold?> BranchAsync(string home, string root, string branch, CancellationToken ct = default, TimeSpan? within = null) =>
        HoldAsync(home, BranchKey(root, branch), ct, within);

    /// <summary>How many landings of the tree wait for its hold in this process now.</summary>
    public static int Waiting(string home, string tree) => WaitingFor(Gate(home, TreeKey(tree)));

    /// <summary>How many pushes of the branch wait for its hold in this process now.</summary>
    public static int WaitingOn(string home, string root, string branch) => WaitingFor(Gate(home, BranchKey(root, branch)));

    /// <summary>Whether this process keeps a gate for the tree now: only while a door holds or waits for it.</summary>
    public static bool Gated(string home, string tree)
    {
        lock (Gates) return Gates.ContainsKey(Gate(home, TreeKey(tree)));
    }

    /// <summary>Where the tree's hold is across processes: one file per key under the home's <c>locks/landings/</c>, named by its hash.</summary>
    public static string PathOf(string home, string tree) => FileOf(home, TreeKey(tree));

    private static async Task<LandingHold?> HoldAsync(string home, string key, CancellationToken ct, TimeSpan? within)
    {
        var patience = within ?? Wait;
        var waiting = Stopwatch.StartNew();
        var named = Gate(home, key);
        var gate = Enter(named);
        var waited = false;
        var held = false;
        var said = false;
        // LANDHOLD2: said once before the first wait, and unsaid when the wait ends, whatever it came to.
        void Say()
        {
            if (said) return;
            said = true;
            LandingHoldNotice.Waiting(true);
        }

        FileStream? file = null;
        LandingHold? hold = null;
        try
        {
            if (!gate.Semaphore.Wait(0))
            {
                waited = true;
                Say();
                Interlocked.Increment(ref gate.Waiting);
                try
                {
                    if (!await gate.Semaphore.WaitAsync(Left(patience, waiting), ct).ConfigureAwait(false)) return null;
                }
                finally
                {
                    Interlocked.Decrement(ref gate.Waiting);
                }
            }

            held = true;
            var path = FileOf(home, key);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            file = Open(path);
            if (file is null)
            {
                waited = true;
                Say();
                Interlocked.Increment(ref gate.Waiting);
                try
                {
                    while ((file = Open(path)) is null)
                    {
                        if (waiting.Elapsed >= patience) return null;
                        await Task.Delay(Poll, ct).ConfigureAwait(false);
                    }
                }
                finally
                {
                    Interlocked.Decrement(ref gate.Waiting);
                }
            }

            // A hold that waited reads what the one before kept; one that waited for nothing begins a chain of its own.
            var before = waited ? Read(file) : null;
            if (!waited) file.SetLength(0);
            Stamp(file);
            SweepDue(home);
            hold = new LandingHold(
                () =>
                {
                    gate.Semaphore.Release();
                    Leave(named, gate);
                },
                file) { Waited = waited, Before = before };
            return hold;
        }
        finally
        {
            if (said) LandingHoldNotice.Waiting(false);
            // Anything but a hold handed out lets go of all it took: a bound passed, a wait cancelled, a file that would not read.
            if (hold is null)
            {
                file?.Dispose();
                if (held) gate.Semaphore.Release();
                Leave(named, gate);
            }
        }
    }

    /// <summary>A tree by its folder's identity, else its spelling.</summary>
    private static string TreeKey(string tree) => $"tree|{Identity(tree)}";

    /// <summary>A branch by its repository's common git folder's identity, else the checkout's spelling, and its name.</summary>
    private static string BranchKey(string root, string branch) =>
        $"branch|{Identity(TreeTips.CommonDirectory(root) ?? root)}|{branch.Trim().ToLowerInvariant()}";

    /// <remarks>
    /// Windows names a folder by its volume and file index, whatever path reaches it. Elsewhere <see cref="FolderIdentity"/> is a
    /// creation time, which two folders may share, so the spelling is the key there.
    /// </remarks>
    private static string Identity(string folder) =>
        (OperatingSystem.IsWindows() ? FolderIdentity.Of(folder) : null) ?? Normal(folder);

    private static string Gate(string home, string key) => $"{Homes.Full(home)}|{key}";

    private static string FileOf(string home, string key) =>
        DaorisHome.Entry(home, HomeEntry.Locks, "landings", $"{Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(key)))[..16]}.lock");

    private static string Normal(string path) => Path.TrimEndingDirectorySeparator(Path.GetFullPath(path)).ToLowerInvariant();

    private static TimeSpan Left(TimeSpan patience, Stopwatch waiting)
    {
        var left = patience - waiting.Elapsed;
        return left > TimeSpan.Zero ? left : TimeSpan.Zero;
    }

    /// <summary>This process's gate for a key, counted as one more door holding or waiting for it.</summary>
    private static KeyGate Enter(string named)
    {
        lock (Gates)
        {
            if (!Gates.TryGetValue(named, out var gate)) Gates[named] = gate = new KeyGate();
            gate.Users++;
            return gate;
        }
    }

    /// <summary>One door fewer for a key: the gate goes once none holds or waits for it.</summary>
    private static void Leave(string named, KeyGate gate)
    {
        lock (Gates)
        {
            if (--gate.Users == 0) Gates.Remove(named);
        }
    }

    private static int WaitingFor(string named)
    {
        lock (Gates) return Gates.TryGetValue(named, out var gate) ? Volatile.Read(ref gate.Waiting) : 0;
    }

    /// <remarks>
    /// A sharing violation is another door holding it, or a sweep reading it: null. A refusal is a sweep's removal not yet complete,
    /// where the system keeps the name until its handle closes: null too, so a door asks again within its bound. A folder that cannot be
    /// made is thrown.
    /// </remarks>
    private static FileStream? Open(string path)
    {
        try
        {
            return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        }
        catch (IOException) when (File.Exists(path))
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>Mark the file as held now, through the hold's own handle; never throws, since the mark only delays a sweep.</summary>
    internal static void Stamp(FileStream file)
    {
        try
        {
            File.SetLastWriteTimeUtc(file.SafeFileHandle, DateTime.UtcNow);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ObjectDisposedException)
        {
            // Unmarked: the file's last write stands as its time.
        }
    }

    /// <summary>Sweep the home's holds once per <see cref="Wait"/> in this process, beside a hold just taken.</summary>
    private static void SweepDue(string home)
    {
        var named = Homes.Full(home);
        var now = Stopwatch.GetTimestamp();
        lock (Swept)
        {
            if (Swept.TryGetValue(named, out var last) && Stopwatch.GetElapsedTime(last, now) < Wait) return;
            Swept[named] = now;
        }

        Sweep(home, DateTime.UtcNow);
    }

    /// <summary>
    /// Remove the home's landing holds no door has held for longer than <see cref="Wait"/> before <paramref name="now"/> (LANDHOLDSWEEP1).
    /// One held does not open and stays; one held since it was listed reads as recent through the sweep's own handle and stays.
    /// </summary>
    /// <returns>How many it removed. Never throws: a file that will not go is swept next time.</returns>
    internal static int Sweep(string home, DateTime now)
    {
        var folder = DaorisHome.Entry(home, HomeEntry.Locks, "landings");
        var removed = 0;
        try
        {
            foreach (var path in Directory.EnumerateFiles(folder, "*.lock"))
            {
                try
                {
                    if (now - File.GetLastWriteTimeUtc(path) <= Wait) continue;
                    // Opened alone but for removal, so no door opens it meanwhile, and the name goes while the sweep still holds it.
                    using var file = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.Delete);
                    if (now - File.GetLastWriteTimeUtc(file.SafeFileHandle) <= Wait) continue;
                    File.Delete(path);
                    removed++;
                }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException)
                {
                    // Held, gone already, or refused: swept next time.
                }
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // No folder yet, or one that does not list: nothing to sweep.
        }

        return removed;
    }

    /// <summary>What the one before kept; null where it kept nothing, or what it kept does not read.</summary>
    private static LandingAttempt? Read(FileStream file)
    {
        if (file.Length == 0) return null;
        file.Position = 0;
        try
        {
            return JsonSerializer.Deserialize<LandingAttempt>(file, Json) is { Branch.Length: > 0, Tip.Length: > 0 } kept ? kept : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Keep an attempt's outcome in the hold's own file, through its own handle, as far as the system's cache.</summary>
    internal static void Write(FileStream file, LandingAttempt attempt)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(attempt, Json);
        file.SetLength(0);
        file.Position = 0;
        file.Write(bytes, 0, bytes.Length);
        file.Flush();
    }

    /// <summary>A key's semaphore in this process, and how many doors hold or wait for it.</summary>
    private sealed class KeyGate
    {
        public readonly SemaphoreSlim Semaphore = new(1, 1);
        public int Users;
        public int Waiting;
    }
}

/// <summary>A door's hold of a tree or a branch (<see cref="LandingHolds"/>), let go when disposed.</summary>
/// <param name="release">Lets the key's gate in this process go.</param>
internal sealed class LandingHold(Action release, FileStream file) : IDisposable
{
    private int _gone;

    /// <summary>Whether it waited for another door holding the same tree or branch.</summary>
    public bool Waited { get; init; }

    /// <summary>
    /// What the attempt it waited behind kept (<see cref="Keep"/>); null where it waited for none, or where that one, and those before
    /// it in the same chain of waits, kept nothing.
    /// </summary>
    public LandingAttempt? Before { get; init; }

    /// <summary>
    /// Keep what this attempt came to, for one that waits behind it: once the work is on its branch, and again once its plugin answered,
    /// so a door that ended before its plugin answered is still read as having landed it.
    /// </summary>
    /// <remarks>Never throws: a hold whose file cannot be written keeps nothing, and the one behind it judges the work as it stands.</remarks>
    public void Keep(LandingAttempt attempt)
    {
        try
        {
            LandingHolds.Write(file, attempt);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ObjectDisposedException)
        {
            // Nothing is kept: the one behind judges the work as it stands, as before LANDRACE1.
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _gone, 1) == 1) return;
        // LANDHOLDSWEEP1: held until now, so a sweep keeps it for a door that waits behind this one.
        LandingHolds.Stamp(file);
        file.Dispose();
        release();
    }
}

/// <summary>
/// What one landing or push came to, kept in its hold's file for the door waiting behind it (LANDRACE1's review): the commit, so the
/// one behind compares what it would land or push with what was, and the plugin's answer, so a failure is said rather than hidden.
/// </summary>
/// <param name="Branch">The branch the work is on.</param>
/// <param name="Tip">
/// The commit: a tree's hold keeps the session's tip it landed (the one squashed, where the rule squashes), a branch's the branch's tip
/// pushed.
/// </param>
/// <param name="AcceptedBy">Who accepted it (<see cref="Daoris.Driver.AcceptedBy"/>).</param>
internal sealed record LandingAttempt(string Branch, string Tip, string AcceptedBy)
{
    /// <summary>The plugin the branch is handed to, where the rule names one.</summary>
    public string? Plugin { get; init; }

    /// <summary>What that plugin answered; null while it has not.</summary>
    public PluginLanding? Said { get; init; }

    /// <summary>
    /// Why its push was not tried: the plugin could not land work here (D145 point 2), or its quest was held, taken back or submitted
    /// again just before the plugin was told (FABRIC-CLOSE1h2).
    /// </summary>
    public string? Unready { get; init; }
}
