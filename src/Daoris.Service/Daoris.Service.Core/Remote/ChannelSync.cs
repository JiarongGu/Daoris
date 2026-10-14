using System.Collections.Concurrent;

namespace Daoris.Knowledge;

/// <param name="Pushed">How many of this machine's own messages went up.</param>
/// <param name="Fetched">How many of the team's messages came down.</param>
/// <param name="Problem">The wall, or the one-time word that the remote has no channel; null otherwise.</param>
public sealed record ChannelSyncReport(int Pushed, int Fetched, string? Problem);

/// <summary>
/// One pass of the ask channels for one workspace (SESSONE2d; D167 point 8; the co-working design §2.5), beside
/// <see cref="SessionSync"/> and in its shape: this machine's own messages up by the cursor of what it last pushed, the team's down by
/// the remote's number, each keyed `origin/id` and read-only here.
/// </summary>
/// <remarks>
/// <para><b>What goes up</b> is this machine's own messages of the workspace: the person's, and a session's of a JOINED repository, as
/// a session record goes. Their words are cleaned of machine paths first, by the rule a session's note is (<see cref="SessionNote"/>), with
/// the posting session's own tree and transcript named where it is held. A message fed down from a teammate never goes back up.
/// Daoris's lines are never stored, so they never travel: each machine reads them from its own quests.</para>
///
/// <para><b>A remote from before the channel</b> answers 404 to its doors. That is not a wall: the channel stays on this machine, whole,
/// and the sync says so once per circle for the life of the process, then says nothing.</para>
/// </remarks>
public static class ChannelSync
{
    private static readonly ConcurrentDictionary<string, bool> OlderSaid = new(StringComparer.OrdinalIgnoreCase);

    public static async Task<ChannelSyncReport> RunAsync(
        ChannelStore store, SessionStore sessions, KnowledgeService service, IRemote remote, string workspace,
        CancellationToken ct = default)
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
            var feed = new List<ChannelMessage>();
            foreach (var (message, _) in changed.Where(c => c.Message.Repository is null || joined.Contains(c.Message.Repository)))
            {
                var poster = message.Session is null ? null : await sessions.FindAsync(message.Session, ct).ConfigureAwait(false);
                feed.Add(message with { Text = SessionNote.ForAnotherMachine(message.Text, poster)! });
            }

            if (feed.Count > 0) await remote.PushChannelAsync(feed, ct).ConfigureAwait(false);

            // Past everything examined, sent or not: a message of a repository that was not joined when it was written stays home.
            if (changed.Count > 0) await store.AdvanceCursorAsync(circle, pushed: changed[^1].Seq, ct: ct).ConfigureAwait(false);
            pushed = feed.Count;

            var since = fetchedThrough;
            while (true)
            {
                var page = await remote.FetchChannelAsync(since, ct).ConfigureAwait(false);
                foreach (var message in page.Messages)
                {
                    await store.MirrorAsync(message with { Workspace = circle }, ct).ConfigureAwait(false);
                    fetched++;
                }

                since = Math.Max(since, page.Through);
                if (!page.More) break;
            }

            await store.AdvanceCursorAsync(circle, fetched: since, ct: ct).ConfigureAwait(false);
            return new(pushed, fetched, null);
        }
        catch (RemoteException wall) when (wall.Status == 404)
        {
            return new(pushed, fetched, OlderSaid.TryAdd(circle, true)
                ? "the remote has no channel doors yet (an older build); an ask's channel stays on this machine"
                : null);
        }
        catch (RemoteException wall)
        {
            return new(pushed, fetched, wall.Message);
        }
    }
}
