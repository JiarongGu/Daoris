namespace Daoris.Knowledge;

/// <param name="Pushed">How many of this machine's own records went up.</param>
/// <param name="Fetched">How many of the team's records came down.</param>
/// <param name="Problem">The wall, when the remote could not be reached or refused the feed; null otherwise.</param>
public sealed record SessionSyncReport(int Pushed, int Fetched, string? Problem);

/// <summary>
/// One pass of the session records for one workspace (SYNC4): this machine's own records up, by the
/// cursor of what it last pushed; the team's down, by the remote's revision cursor — so a record is
/// sent when it changes, not every tick, and every machine sees the team's.
/// </summary>
/// <remarks>
/// <para><b>What goes up</b> is this machine's own records of JOINED repositories in the circle, and
/// nothing a record may not carry: the wire has no field for a transcript, a tree or a profile (D47 §4).
/// A record fed down from a teammate never goes back up — it is theirs to feed.</para>
///
/// <para><b>What comes down</b> is filed in THIS sync's circle, by this machine's wiring, and keeps its
/// `origin/id`: read-only here, and never this machine's lock (D47 §6).</para>
/// </remarks>
public static class SessionSync
{
    public static async Task<SessionSyncReport> RunAsync(
        SessionStore store, KnowledgeService service, IRemote remote, string workspace, CancellationToken ct = default)
    {
        var circle = Workspaces.Normalize(workspace);
        var joined = (await service.RegistryAsync(ct: ct).ConfigureAwait(false))
            .Where(r => r.Joined && Workspaces.Same(r.InWorkspace, circle))
            .Select(r => r.Repository)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var (pushedThrough, fetchedThrough) = await store.CursorAsync(circle, ct).ConfigureAwait(false);
        var pushed = 0;
        var fetched = 0;
        try
        {
            var changed = await store.OwnChangedSinceAsync(pushedThrough, circle, ct).ConfigureAwait(false);
            var feed = changed
                .Where(c => joined.Contains(c.Session.Repository))
                .Select(c => new FedSessionRecord(
                    c.Session.Id, c.Session.Quest, c.Session.Repository, c.Session.Adapter, c.Session.StateName,
                    // Free text the driver wrote, cleaned of what is machine-local in it (REV3).
                    SessionNote.ForAnotherMachine(c.Session.Note, c.Session), c.Session.Evidence,
                    c.Session.Created, c.Session.Updated,
                    c.Session.Kind.ToString(), c.Session.HarnessVersion))
                .ToList();
            if (feed.Count > 0) await remote.PushSessionsAsync(feed, ct).ConfigureAwait(false);

            // Past everything examined, sent or not: a record of a repository that was not joined when
            // it was written stays home, as it was when it was made.
            if (changed.Count > 0)
            {
                await store.AdvanceCursorAsync(circle, pushed: changed[^1].Revision, ct: ct).ConfigureAwait(false);
            }

            pushed = feed.Count;

            var since = fetchedThrough;
            while (true)
            {
                var page = await remote.FetchSessionsAsync(since, ct).ConfigureAwait(false);
                foreach (var record in page.Records)
                {
                    await store.MirrorAsync(record with { Workspace = circle }, ct).ConfigureAwait(false);
                }

                fetched += page.Records.Count;
                since = Math.Max(since, page.Through);
                if (!page.More) break;
            }

            await store.AdvanceCursorAsync(circle, fetched: since, ct: ct).ConfigureAwait(false);
            return new(pushed, fetched, null);
        }
        catch (RemoteException wall)
        {
            return new(pushed, fetched, wall.Message);
        }
    }
}
