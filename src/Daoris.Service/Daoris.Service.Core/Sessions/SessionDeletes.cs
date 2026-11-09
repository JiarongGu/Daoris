namespace Daoris.Knowledge;

/// <summary>The record's half of deleting a session: whether a record may go, and the delete itself.</summary>
internal sealed class SessionDeletes(QuestStore quests, SessionStore sessions, AskStore? asks)
{
    /// <summary>
    /// Whether this session's record may be deleted, and if not why, in words a person can act on.
    /// Deletes nothing: the driver asks it for a refusal before judging its own half, the tree and a landing.
    /// </summary>
    public async Task<SessionDeleteOutcome> JudgeDeleteAsync(string id, CancellationToken ct = default)
    {
        var session = await sessions.FindAsync(id, ct).ConfigureAwait(false);
        return await JudgeAsync(id, session, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Delete a conversation's record that served no quest: ended, this machine's, named by no ask's
    /// intake and no quest's publisher, and held by no remote. Judged and removed as one step, so a record that
    /// moved since the driver asked is judged as it now stands.
    /// </summary>
    /// <remarks>
    /// <b>Why so narrow.</b> A session that served a quest is that work's record: which agent, which version, which
    /// account and which tree did it, and the quest's strikes and carry-ons are read from it. Archive is
    /// how such a session is cleared. A record pushed to a remote would leave the team's copy, since a session record does
    /// not travel as a deletion.
    /// </remarks>
    public async Task<SessionDeleteOutcome> DeleteAsync(string id, CancellationToken ct = default) =>
        await sessions.ExclusiveAsync(async inside =>
        {
            var session = await sessions.FindAsync(id, inside).ConfigureAwait(false);
            var judged = await JudgeAsync(id, session, inside).ConfigureAwait(false);
            if (judged.Refusal != SessionDeleteRefusal.None) return judged;

            await sessions.DeleteAsync(id, inside).ConfigureAwait(false);
            return judged with { Message = $"Deleted session `{id}`: its record is gone from this machine." };
        }, ct).ConfigureAwait(false);

    /// <summary>
    /// Which of these records <see cref="DeleteAsync"/> would delete, by the same rule: a list's
    /// answer says it per record, so a page offers *Delete…* only where the ledger would take it.
    /// </summary>
    public async Task<IReadOnlySet<string>> DeletableAsync(IEnumerable<Session> candidates, CancellationToken ct = default)
    {
        var named = await NamesAsync(ct).ConfigureAwait(false);
        return candidates
            .Where(session => Judge(session, named).Refusal == SessionDeleteRefusal.None)
            .Select(session => session.Id)
            .ToHashSet(StringComparer.Ordinal);
    }

    private async Task<SessionDeleteOutcome> JudgeAsync(string id, Session? session, CancellationToken ct) =>
        session is null
            ? new(SessionDeleteRefusal.NotFound, $"No session `{id}`.", Session: null)
            : Judge(session, await NamesAsync(ct).ConfigureAwait(false));

    /// <summary>
    /// What names a session: each ask's intake, each quest's publishing session, and each session a later record continues. Read
    /// whole, since a delete is rare and a list asks once for all its records.
    /// </summary>
    private async Task<Names> NamesAsync(CancellationToken ct) =>
        new(asks is null ? [] : await asks.ListAsync(includeClosed: true, ct: ct).ConfigureAwait(false),
            await quests.ListAsync(includeClosed: true, ct: ct).ConfigureAwait(false),
            await sessions.ContinuedIdsAsync(ct).ConfigureAwait(false));

    private sealed record Names(IReadOnlyList<Ask> Asks, IReadOnlyList<Quest> Quests, IReadOnlySet<string> Continued);

    /// <summary>
    /// The record's half of §5.4, in the order a person meets it: whose it is, whether it still runs, whether it served a
    /// quest, what names it, and whether the team holds a copy.
    /// </summary>
    private static SessionDeleteOutcome Judge(Session session, Names named)
    {
        var id = session.Id;
        if (session.Origin is { } origin)
        {
            return new(SessionDeleteRefusal.NotOurs, $"Session `{id}` ran on `{origin}`; its record is theirs — archive it here instead.", session)
            {
                Origin = origin,
            };
        }

        if (session.Active)
        {
            return new(SessionDeleteRefusal.Live, $"Session `{id}` is still running; stop it first.", session);
        }

        if (session.Quest is { } served)
        {
            return new(
                SessionDeleteRefusal.ServedQuest,
                $"Session `{id}` worked on `#{served}`, and its record is that work's; archive it instead.",
                session) { Quest = served };
        }

        // A chat's take names no quest on its record, so its sentence says the take without one.
        if (session.Took)
        {
            return new(
                SessionDeleteRefusal.ServedQuest,
                $"Session `{id}` took a quest, and its record is that work's; archive it instead.",
                session);
        }

        var intakeOf = named.Asks.FirstOrDefault(ask =>
            string.Equals(ask.Intake, id, StringComparison.Ordinal)
            || (session.Ask is { } asked && string.Equals(ask.Id, asked, StringComparison.OrdinalIgnoreCase)));
        if (intakeOf is not null)
        {
            return new(SessionDeleteRefusal.Named, $"Session `{id}` is named by ask `#{intakeOf.Id}` as its intake.", session)
            {
                Ask = intakeOf.Id,
            };
        }

        var publishedHere = named.Quests.FirstOrDefault(quest => string.Equals(quest.PublishedBy, id, StringComparison.Ordinal));
        if (publishedHere is not null)
        {
            return new(SessionDeleteRefusal.Named, $"Session `{id}` published `#{publishedHere.Id}`, which names it.", session)
            {
                Quest = publishedHere.Id,
            };
        }

        // Taking a link out of a line would make the record before it the head again and leave the one after pointing at nothing.
        if (named.Continued.Contains(id))
        {
            return new(SessionDeleteRefusal.Named, $"Session `{id}` is continued by a later record of its line, which names it.", session);
        }

        if (session.Pushed)
        {
            return new(
                SessionDeleteRefusal.OnRemote,
                $"The remote for `{session.Workspace}` holds session `{id}`; a delete here would not reach the team's copy, so "
                + "archive it instead.",
                session) { Workspace = session.Workspace };
        }

        return new(SessionDeleteRefusal.None, $"Session `{id}` may be deleted.", session);
    }
}
