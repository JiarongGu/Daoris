namespace Daoris.Driver;

/// <summary>One record ended because nothing on this machine was running it.</summary>
public sealed record OrphanEnded(string Id, string Repository)
{
    /// <summary>The quest it served, or null: whether its done lands at the next look (LAND2b, design §2).</summary>
    public string? Quest { get; init; }

    /// <summary>The tree its record names, or null.</summary>
    public string? Tree { get; init; }
}

/// <summary>
/// Records that say a session runs when nothing on this machine runs it (2026-09-25): left by a crash,
/// a kill, a power cut — anything no shutdown order can reach. Found and ended, never guessed at.
/// </summary>
/// <remarks>
/// <para><b>An orphan is proven, not presumed.</b> This machine's record (never a teammate's, which
/// the service client already leaves out), in a state that claims a process, with no live process
/// for it here — not in this driver's registry and not marked by any other driver sharing the home
/// (<see cref="SessionProcesses.AliveOnThisMachine"/>).</para>
///
/// <para><b>The sweep takes only <c>working</c>.</b> A record is <c>starting</c> between its opening and
/// its spawn, which another driver here may be in the middle of, and a parked one waits on the person
/// by design. The person's own stop on one record also takes <c>starting</c>: they asked about that
/// one, and a start that never spawned is exactly what would otherwise hold its repository forever.</para>
///
/// <para><b>One home per service is the assumption</b>, and it is the deployed shape: a driver drives
/// its machine's local host (a shared host is reached through the sync, never driven directly — D47
/// §9), and the markers are that home's. A second home driving the same host would see the first's
/// sessions as unmarked, which is why nothing but a driver's own loop and a person's stop ever asks.</para>
///
/// <para><b>Ended <c>stopped</c></b>, as the driven path records a session the driver was closed under:
/// what ended it is not known, only that nothing runs it, and the note says exactly that. The sweep's stop
/// is <b>interrupted</b> (D104), not the person's, so a take it ended is carried on; the person's stop on
/// one record is theirs, since they asked about that one, and never is.</para>
/// </remarks>
public static class Orphans
{
    public const string Note =
        "nothing on this machine was running it any more — its process ended with the application that "
        + "started it, and the record had not been told.";

    /// <summary><see cref="Note"/> with its code (LANG1a).</summary>
    public static Noted Noted => Noted.Of(NoteCodes.StoppedOrphan, Note);

    /// <param name="only">One record the person asked to stop, or null for the sweep.</param>
    /// <returns>The records ended, in the order the service listed them.</returns>
    public static async Task<IReadOnlyList<OrphanEnded>> EndAsync(
        ServiceClient service, SessionProcesses processes, string? only = null, CancellationToken ct = default)
    {
        var ended = new List<OrphanEnded>();
        foreach (var session in await service.ActiveSessionsAsync(ct).ConfigureAwait(false))
        {
            if (only is not null && !string.Equals(session.Id, only, StringComparison.OrdinalIgnoreCase)) continue;
            var claimsProcess = session.State == "working" || (only is not null && session.State == "starting");
            if (!claimsProcess || processes.AliveOnThisMachine(session.Id)) continue;

            try
            {
                await service.AdvanceAsync(session.Id, "stopped", Noted, ct: ct, interrupted: only is null).ConfigureAwait(false);
                ended.Add(new OrphanEnded(session.Id, session.Repository) { Quest = session.Quest, Tree = session.Tree });
            }
            catch (DriverException)
            {
                // Moved by someone else between the list and the write — its record says how.
            }
        }

        return ended;
    }
}
