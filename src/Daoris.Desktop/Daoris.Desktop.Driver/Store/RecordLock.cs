namespace Daoris.Driver;

/// <summary>
/// One writer at a time across processes for a record file of the home (FABRIC-END2, D173's END2 note): a lock file under the home's
/// <c>locks/records/</c>, held alone for each read, change and write of the record. Taken by the loop and by any verb alike.
/// </summary>
/// <remarks>
/// <para><b>Why a lock, when a store's gate already serializes its writers.</b> The gate is a process's: a terminal's verb beside a running
/// instance holds no lease (HomeLease.cs, <see cref="HomeLease.ForVerb"/>), so two processes could each read <c>landings.json</c>, change
/// it and write it whole, and the second write would drop the first's change. RPC1b would make such a verb the instance's client
/// instead; this makes the files safe whoever writes them, with no pipe.</para>
///
/// <para><b>The handle is the lock</b>, as <see cref="TreeLock"/>'s is: opened with no sharing, so a second open fails until the first is
/// let go, in this process or another. A process that dies lets go with its handles, so nothing stale can block. Elsewhere than Windows
/// .NET takes an advisory lock for the same open, which every Daoris process honours.</para>
///
/// <para><b>Waited for, briefly.</b> A record's edit holds it for milliseconds, so a writer that finds it held looks again until
/// <see cref="Wait"/> has passed, then says so as a tool error and writes nothing: "cannot tell" is never "write anyway".</para>
/// </remarks>
internal sealed class RecordLock : IDisposable
{
    /// <summary>How long a writer waits for another's edit before it gives up.</summary>
    public static readonly TimeSpan Wait = TimeSpan.FromSeconds(10);

    private static readonly TimeSpan Poll = TimeSpan.FromMilliseconds(15);

    /// <summary>
    /// Said with the lock's path each time a writer finds it held and waits (TESTCLOCK2): a test holding the lock knows its edit is
    /// waiting on it, rather than guessing from a few hundred milliseconds of the machine's clock.
    /// </summary>
    internal static event Action<string>? Waiting;

    private readonly FileStream _file;

    private RecordLock(FileStream file) => _file = file;

    /// <summary>Where the lock of a record of the home is: one file per record under <c>locks/records/</c> (D172's <c>locks</c> entry).</summary>
    public static string PathOf(string home, string record) => DaorisHome.Entry(home, HomeEntry.Locks, "records", $"{record}.lock");

    /// <summary>Hold the lock, waiting while another edit holds it.</summary>
    /// <exception cref="IOException">It was held for the whole of the wait, or its folder could not be made.</exception>
    public static RecordLock Take(string path, TimeSpan? within = null)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var until = DateTime.UtcNow + (within ?? Wait);
        while (true)
        {
            try
            {
                return new RecordLock(new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None));
            }
            catch (IOException error)
            {
                if (DateTime.UtcNow >= until)
                {
                    throw new IOException(
                        $"the records lock ({path}) was held by another edit for the whole of the wait, so nothing was written: {error.Message}",
                        error);
                }

                Waiting?.Invoke(path);
                Thread.Sleep(Poll);
            }
        }
    }

    public void Dispose() => _file.Dispose();
}
