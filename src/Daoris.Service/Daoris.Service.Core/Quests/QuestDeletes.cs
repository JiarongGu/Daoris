namespace Daoris.Knowledge;

/// <summary>Deleting a quest made by mistake, and what stands in a delete's way.</summary>
internal sealed class QuestDeletes(
    KnowledgeService service, QuestStore quests, QuestFiles? files, SessionStore? sessions, AskStore? asks, QuestRemoteClaims claims)
{
    /// <summary>
    /// Delete a quest nobody has started on: open, no session record naming it, and no taken quest
    /// waiting on it. One that may have left this machine is tombstoned, and deleted by push when its
    /// circle's remote answers, as a take claims by push; any other simply goes. The files this
    /// machine kept for it go with it.
    /// </summary>
    /// <param name="askGoesToo">
    /// The ask that asked it is being deleted with it, so its answer says nothing of what stands of the ask.
    /// Otherwise a delete that leaves its ask with no quest says what the ask now is.
    /// </param>
    public async Task<QuestDeleteOutcome> DeleteAsync(string id, DateTimeOffset now, CancellationToken ct, bool askGoesToo)
    {
        var quest = await quests.FindAsync(id.TrimStart('#'), ct).ConfigureAwait(false);
        if (quest is null)
        {
            return new(QuestDeleteRefusal.NotFound, $"No quest `#{id.TrimStart('#')}`. Ids come from `quest_list`.", Quest: null);
        }

        if (await KeptAsync(quest, ct).ConfigureAwait(false) is { } kept)
        {
            return new(QuestDeleteRefusal.Kept, $"Quest `#{quest.Id}` {kept.Stands}, so it stays. {kept.Instead}", quest);
        }

        var remote = await claims.SharedAtAsync(quest, ct).ConfigureAwait(false);
        var deletion = await quests.DeleteAsync(quest.Id, now, travels: remote is not null, ct).ConfigureAwait(false);
        if (!deletion.Deleted)
        {
            // It moved between the look and the write: the store judged it again, inside the write.
            return deletion.Quest is { } moved && await KeptAsync(moved, ct).ConfigureAwait(false) is { } since
                ? new(QuestDeleteRefusal.Kept, $"Quest `#{quest.Id}` {since.Stands}, so it stays. {since.Instead}", moved)
                : new(QuestDeleteRefusal.NotFound, $"No quest `#{quest.Id}`. Ids come from `quest_list`.", Quest: null);
        }

        if (remote is null)
        {
            files?.Forget(quest.Id);
            return new(
                QuestDeleteRefusal.None,
                (deletion.Tombstoned
                    ? $"Deleted quest `#{quest.Id}`. It had reached a remote, so the delete stays in its history, where a sync can carry it."
                    : $"Deleted quest `#{quest.Id}` — it never left this machine, so nothing else holds a copy.")
                + await AskLeftAsync(quest, askGoesToo, ct).ConfigureAwait(false),
                quest);
        }

        // Deleted by push: the pass runs before the answer returns, so a delete that lost to
        // another machine's take is known now, not at a tick nobody watches.
        var pass = await QuestSync.RunAsync(quests, service, remote, quest.Workspace, ct).ConfigureAwait(false);
        if (await quests.FindAsync(quest.Id, ct).ConfigureAwait(false) is { } back)
        {
            return new(
                QuestDeleteRefusal.TakenElsewhere,
                $"Quest `#{quest.Id}` was taken on another machine first — its take reached the remote before this "
                + $"delete, so the quest stays, {back.Status}. Decline it instead, with the reason, if it should not be done.",
                back);
        }

        files?.Forget(quest.Id);
        var confirmed = (await quests.HistoryAsync(quest.Id, ct).ConfigureAwait(false))
            .LastOrDefault(operation => operation.Kind == QuestOperationKind.Deleted && operation.Machine == quests.Machine)
            ?.Number is not null;
        return new(
            QuestDeleteRefusal.None,
            (confirmed
                ? $"Deleted quest `#{quest.Id}` — the remote confirmed it, and every machine drops it on its next sync."
                : $"Deleted quest `#{quest.Id}` here, UNCONFIRMED: "
                  + (pass.Problem ?? pass.Refused.FirstOrDefault(r => r.Quest == quest.Id)?.Reason
                      ?? "the remote has not taken the delete yet")
                  + ". It travels on the next sync — and if another machine takes the quest first, it comes back, taken.")
            + await AskLeftAsync(quest, askGoesToo, ct).ConfigureAwait(false),
            quest)
        {
            Unconfirmed = !confirmed,
        };
    }

    /// <summary>
    /// Remove a closed quest (the one remover, <see cref="QuestClosedRemoval"/>): tombstoned and carried to its remote when it may have
    /// travelled, simply gone otherwise, and its kept files forgotten. A quest that is not closed is refused, as <see cref="DeleteAsync"/>
    /// refuses a closed one.
    /// </summary>
    public async Task<QuestDeleteOutcome> RemoveClosedAsync(string id, DateTimeOffset now, CancellationToken ct)
    {
        var quest = await quests.FindAsync(id.TrimStart('#'), ct).ConfigureAwait(false);
        if (quest is null) return new(QuestDeleteRefusal.NotFound, $"No quest `#{id.TrimStart('#')}`.", Quest: null);

        var remote = await claims.SharedAtAsync(quest, ct).ConfigureAwait(false);
        var removal = await quests.RemoveClosedAsync(quest.Id, now, travels: remote is not null, ct).ConfigureAwait(false);
        if (!removal.Deleted)
        {
            return new(QuestDeleteRefusal.Kept, $"Quest `#{quest.Id}` is {quest.Status}, not closed, so it stays.", removal.Quest ?? quest);
        }

        files?.Forget(quest.Id);
        if (remote is not null) await QuestSync.RunAsync(quests, service, remote, quest.Workspace, ct).ConfigureAwait(false);
        return new(
            QuestDeleteRefusal.None,
            removal.Tombstoned
                ? $"Removed closed quest `#{quest.Id}`; the removal is in its history, where a sync carries it."
                : $"Removed closed quest `#{quest.Id}`; it never left this machine.",
            quest);
    }

    /// <summary>
    /// What the ask that asked a deleted quest now stands as, when this is its last quest: its own sentence
    /// after a blank line, so a person who deleted the quest is told, not left to find the ask changed. Empty for a quest no
    /// ask asked, an ask that goes too, one still carrying another quest, and a closed one, already ended.
    /// </summary>
    private async Task<string> AskLeftAsync(Quest deleted, bool askGoesToo, CancellationToken ct)
    {
        if (askGoesToo || asks is null || AskDesk.AskOf(deleted.From) is not { } askId) return "";
        if (await asks.FindAsync(askId, ct).ConfigureAwait(false) is not { State: AskState.Published } held) return "";
        return (await quests.FromAsync(AskDesk.SenderOf(held.Id), ct: ct).ConfigureAwait(false)).Count == 0
            ? $"\n\n{AskDesk.NoQuestLeft(held)}"
            : "";
    }

    /// <summary>
    /// Which of <paramref name="candidates"/> may be deleted — the judgement <see cref="DeleteAsync"/>
    /// runs, answered for a list so a page offers the verb only where the service would take it.
    /// </summary>
    public async Task<IReadOnlySet<string>> DeletableAsync(IEnumerable<Quest> candidates, CancellationToken ct = default)
    {
        var open = candidates.Where(quest => quest.Status == QuestStatus.Open).ToList();
        if (open.Count == 0) return new HashSet<string>();

        var started = sessions is null
            ? new HashSet<string>()
            : await sessions.QuestsNamedAsync(ct).ConfigureAwait(false);
        var awaited = await quests.AwaitedAsync(ct).ConfigureAwait(false);
        return open.Select(quest => quest.Id)
            .Where(id => !started.Contains(id) && !awaited.Contains(id))
            .ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>
    /// Why a quest's record must stay, in a phrase that follows its name, with what to do instead
    /// — null when nothing stands on it and it may go.
    /// </summary>
    public async Task<(string Stands, string Instead)?> KeptAsync(Quest quest, CancellationToken ct)
    {
        const string Decline = "Decline it instead, with the reason, and the asker hears why.";
        const string AlreadyGone = "A closed quest already leaves the list, and shows again only with closed ones included.";

        switch (quest.Status)
        {
            case QuestStatus.Taken:
                return ("is Taken — someone is working it", Decline);
            case QuestStatus.Done:
                return ("is Done — it is the record of work that was answered", AlreadyGone);
            case QuestStatus.Declined:
                return ("is Declined — the decline is the trace of a decision, and its reason is the asker's to read", AlreadyGone);
        }

        if (sessions is not null && await sessions.AnyForQuestAsync(quest.Id, ct).ConfigureAwait(false) is { } session)
        {
            return ($"is open, but session `{session.Id}` was started for it and its record names the quest", Decline);
        }

        if ((await quests.WaitingOnAsync(quest.Id, ct).ConfigureAwait(false)).FirstOrDefault() is { } waiting)
        {
            return ($"is the question quest `#{waiting.Id}` waits on, and deleted, that quest would wait on nothing",
                "Decline it instead, with the reason, and its taker is resumed with it.");
        }

        return null;
    }
}
