using System.Collections.Concurrent;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Daoris.Driver;

/// <summary>
/// One landing of one session's tree at a time, whichever door lands it (LANDRACE1, D145 point 2, D173's FABRIC-END7 note): the
/// person's press, a terminal's <c>trees land</c>, an entry's Accept and the look's automatic landing each take the tree's hold
/// around the whole landing (<c>TreeLands.LandAsync</c>), its plugin and its tidy included.
/// </summary>
/// <remarks>
/// <para><b>Why.</b> Git's ref writes keep two landings of a made branch apart (a branch made once, an advance by compare-and-swap),
/// but a tree on a branch of its own (TREEFOLLOW1) has no ref made for it, so both would land it and its plugin would push twice; and
/// a press that comes while the other's plugin pushes is owed what happened, not git's refusal.</para>
///
/// <para><b>In this process, then across processes.</b> A semaphore per tree keeps this process's doors apart without polling. A file
/// under the home's <c>locks/landings/</c>, opened alone, keeps this process apart from another (a terminal's press beside the
/// desktop's look): the handle is the lock, as <see cref="TreeLock"/>'s and <see cref="RecordLock"/>'s are, so a process that dies
/// lets go with it.</para>
///
/// <para><b>When the landings it waited behind began.</b> The holder writes the moment it began into the file, and one that took
/// the hold after waiting keeps what it found there, so a chain of waits keeps the first one's moment. A landing that waited reads it
/// as <see cref="LandingHold.Since"/>: a landing of its session recorded since then is the one it waited behind, which it says rather
/// than land again (<c>TreeLands.LandAsync</c>). A moment it cannot read is the earliest, so any recorded landing counts: the safe
/// side, which a second press undoes.</para>
///
/// <para><b>Waited for, within a bound.</b> A landing waits while another holds the tree, up to <see cref="Wait"/> (longer than a
/// plugin's two minutes), and then takes nothing: "cannot tell" is never "land anyway".</para>
/// </remarks>
internal static class LandingHolds
{
    /// <summary>How long a landing waits for another of the same tree: past a plugin's own patience, and its tidy.</summary>
    public static readonly TimeSpan Wait = LandingPlugins.DefaultPatience + TimeSpan.FromMinutes(3);

    private static readonly TimeSpan Poll = TimeSpan.FromMilliseconds(50);

    private static readonly ConcurrentDictionary<string, SemaphoreSlim> Gates = new(StringComparer.Ordinal);

    private static readonly ConcurrentDictionary<string, int> Waiters = new(StringComparer.Ordinal);

    /// <summary>
    /// Hold the tree for one landing, waiting while another landing of it holds it, here or in another process.
    /// </summary>
    /// <returns>The hold, which knows whether it waited; null where it was held for the whole of <paramref name="within"/>.</returns>
    public static async Task<LandingHold?> TakeAsync(string home, string tree, CancellationToken ct = default, TimeSpan? within = null)
    {
        var key = Key(home, tree);
        var until = DateTime.UtcNow + (within ?? Wait);
        var gate = Gates.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        var waited = false;
        if (!gate.Wait(0))
        {
            waited = true;
            Waiters.AddOrUpdate(key, 1, (_, count) => count + 1);
            try
            {
                if (!await gate.WaitAsync(Left(until), ct).ConfigureAwait(false)) return null;
            }
            finally
            {
                Waiters.AddOrUpdate(key, 0, (_, count) => count - 1);
            }
        }

        try
        {
            var path = PathOf(home, tree);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var file = Open(path);
            if (file is null)
            {
                waited = true;
                Waiters.AddOrUpdate(key, 1, (_, count) => count + 1);
                try
                {
                    while ((file = Open(path)) is null)
                    {
                        if (DateTime.UtcNow >= until)
                        {
                            gate.Release();
                            return null;
                        }

                        await Task.Delay(Poll, ct).ConfigureAwait(false);
                    }
                }
                finally
                {
                    Waiters.AddOrUpdate(key, 0, (_, count) => count - 1);
                }
            }

            var found = Read(file);
            // A chain of waits keeps the first one's moment; a hold that waited for nothing begins its own.
            if (!waited) Write(file, DateTimeOffset.UtcNow);
            return new LandingHold(waited ? found ?? DateTimeOffset.MinValue : null, gate, file);
        }
        catch
        {
            gate.Release();
            throw;
        }
    }

    /// <summary>How many landings of the tree wait for its hold in this process now.</summary>
    public static int Waiting(string home, string tree) => Waiters.GetValueOrDefault(Key(home, tree));

    /// <summary>
    /// Where the tree's hold is across processes: one file per tree under the home's <c>locks/landings/</c>, named by its full path's
    /// hash whatever its case, since a tree's path names no file safely.
    /// </summary>
    public static string PathOf(string home, string tree) =>
        DaorisHome.Entry(home, HomeEntry.Locks, "landings", $"{Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(Normal(tree))))[..16]}.lock");

    private static string Key(string home, string tree) => $"{Homes.Full(home)}|{Normal(tree)}";

    private static string Normal(string tree) => Path.TrimEndingDirectorySeparator(Path.GetFullPath(tree)).ToLowerInvariant();

    private static TimeSpan Left(DateTime until)
    {
        var left = until - DateTime.UtcNow;
        return left > TimeSpan.Zero ? left : TimeSpan.Zero;
    }

    /// <remarks>A sharing violation is another landing holding it: null. A folder that cannot be made is thrown.</remarks>
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
    }

    private static DateTimeOffset? Read(FileStream file)
    {
        file.Position = 0;
        var buffer = new byte[64];
        var read = file.Read(buffer, 0, buffer.Length);
        return DateTimeOffset.TryParse(Encoding.UTF8.GetString(buffer, 0, read).Trim(), CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind, out var at) ? at : null;
    }

    private static void Write(FileStream file, DateTimeOffset at)
    {
        var bytes = Encoding.UTF8.GetBytes(at.ToString("O", CultureInfo.InvariantCulture));
        file.SetLength(0);
        file.Position = 0;
        file.Write(bytes, 0, bytes.Length);
        file.Flush();
    }
}

/// <summary>A landing's hold of its tree (<see cref="LandingHolds"/>), let go when disposed.</summary>
internal sealed class LandingHold(DateTimeOffset? since, SemaphoreSlim gate, FileStream file) : IDisposable
{
    private int _gone;

    /// <summary>
    /// Where the landing waited for another of its tree: when the landings it waited behind began, the earliest moment where that could
    /// not be read. Null where it waited for none.
    /// </summary>
    public DateTimeOffset? Since { get; } = since;

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _gone, 1) == 1) return;
        file.Dispose();
        gate.Release();
    }
}
