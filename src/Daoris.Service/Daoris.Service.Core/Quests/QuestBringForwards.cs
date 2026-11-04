namespace Daoris.Knowledge;

/// <summary>The person's bring-forward of this machine's lost take: judged, written, and claimed by push on a shared quest.</summary>
internal sealed class QuestBringForwards(QuestStore quests, KnowledgeService service, SessionStore? sessions, QuestRemoteClaims claims)
{
    /// <summary>What the kept session goes on from once its take holds again (D178 point 3), said wherever it does.</summary>
    private const string GoesOnFrom =
        "Its kept session goes on from here: bring its branch up to date with the step's source first. No result the old take submitted "
        + "comes back: the step is judged from its next ready.";

    /// <summary>
    /// The person brings this machine's lost take forward (D178 point 3): the store writes a fresh take of the step as it stands
    /// and the conflict's dismissal marked with it, in one write, and on a shared quest the take is pushed and its answer awaited before
    /// anything goes on, as every take is (D69). Confirmed, offline (unconfirmed, D68 rule 1), or lost again, each said in words a person
    /// and a session read; a loss is never said as brought forward. The person's own, at a local host's door: no connector tool reaches it.
    /// </summary>
    public async Task<QuestRespondOutcome> BringForwardAsync(
        string id, QuestOperationRef conflict, DateTimeOffset now, CancellationToken ct = default)
    {
        var quest = id.TrimStart('#');
        var named = $"`{conflict.Machine}:{conflict.Sequence}`";
        // Read before the write, as the session store keeps its own; a session the driver starts between the two finds the
        // step taken by the fresh take, as any start after the press does.
        var running = sessions is null ? null : await sessions.RunningForQuestAsync(quest, ct).ConfigureAwait(false);
        var bringing = await quests.BringForwardAsync(quest, conflict, now, ct, running: running is not null).ConfigureAwait(false);
        return bringing.Refusal switch
        {
            QuestForwardRefusal.None => await ForwardByPushAsync(bringing.Quest!, conflict, ct).ConfigureAwait(false)
                                        ?? new(
                                            QuestRespondRefusal.None,
                                            $"Quest `#{quest}` is brought forward: this machine's fresh take is Taken, and conflict {named} is "
                                            + "dismissed as brought forward. The quest leaves this machine through no remote, so the take is "
                                            + "complete here. " + GoesOnFrom,
                                            bringing.Quest),
            QuestForwardRefusal.NotFound => new(QuestRespondRefusal.NotFound, $"No quest `#{quest}`. Ids come from `quest_list`.", null),
            QuestForwardRefusal.NotOurs => new(
                QuestRespondRefusal.NoLostTake,
                $"Quest `#{quest}` carries no take of this machine's that lost under {named}: only the machine whose take lost brings it "
                + "forward, and only while its conflict is on the quest. Nothing moved.",
                null),
            QuestForwardRefusal.Settled => new(
                QuestRespondRefusal.NoLostTake,
                $"Quest `#{quest}`'s conflict {named} is settled already: it was dismissed or brought forward, here or on another machine, "
                + "and a settled conflict authorizes no take. Nothing moved.",
                null),
            QuestForwardRefusal.TakenHere => new(
                QuestRespondRefusal.NoLostTake,
                $"Quest `#{quest}` is Taken by this machine already, by a take since the one that lost, so nothing is left to bring "
                + $"forward. Nothing moved; dismiss conflict {named} instead.",
                null),
            QuestForwardRefusal.TakenElsewhere => new(
                QuestRespondRefusal.AlreadyTaken,
                $"Quest `#{quest}` is Taken on another machine: the step is not this machine's while that take stands, so its lost take "
                + $"cannot be brought forward. Nothing moved; dismiss conflict {named}, or bring it forward if the step is open again.",
                null),
            QuestForwardRefusal.Closed => new(
                QuestRespondRefusal.Closed,
                $"Quest `#{quest}` is {bringing.Quest!.Status}: a closed step takes no take, so its lost take cannot be brought forward. "
                + $"Nothing moved; dismiss conflict {named}.",
                null),
            QuestForwardRefusal.StillRunning => new(
                QuestRespondRefusal.StillRunning,
                $"Quest `#{quest}`'s session `{running!.Id}` is still {running.StateName} on this machine: the lost take is brought forward "
                + "only once its process has ended, so nothing the old session does late can stand on the fresh take. Nothing moved; stop "
                + "the session, or bring it forward when it ends.",
                null),
            _ => new(
                QuestRespondRefusal.SourceReopened,
                $"Quest `#{quest}` is withdrawn: the person's reopen holds the work it grows from, and nobody takes it until that work is "
                + "ready again. Nothing moved; bring it forward then.",
                null),
        };
    }

    /// <summary>
    /// A bring-forward on a shared quest claims by push (D69), as a take does: held when the remote numbered
    /// the fresh take, unconfirmed while it has not, and lost again when another machine's take, the person's reopen, or a dismissal of the
    /// same conflict reached the remote first, whose rebase lost the mark with the take (D178 point 5). Null for a quest that never leaves
    /// this machine.
    /// </summary>
    private async Task<QuestRespondOutcome?> ForwardByPushAsync(Quest quest, QuestOperationRef conflict, CancellationToken ct)
    {
        if (await claims.SharedAtAsync(quest, ct).ConfigureAwait(false) is not { } remote) return null;

        var pass = await QuestSync.RunAsync(quests, service, remote, quest.Workspace, ct).ConfigureAwait(false);
        var claim = await quests.ClaimAsync(quest.Id, ct).ConfigureAwait(false);
        var named = $"`{conflict.Machine}:{conflict.Sequence}`";
        if (claim == QuestClaim.Held)
        {
            return new(
                QuestRespondRefusal.None,
                $"Quest `#{quest.Id}` is brought forward: this machine's fresh take is Taken, the remote confirmed it, and conflict {named} is "
                + "dismissed as brought forward. " + GoesOnFrom,
                await quests.FindAsync(quest.Id, ct).ConfigureAwait(false));
        }

        if (claim != QuestClaim.Lost)
        {
            return new(
                QuestRespondRefusal.None,
                $"Quest `#{quest.Id}` is brought forward, UNCONFIRMED: "
                + (pass.Problem ?? pass.Refused.FirstOrDefault(r => r.Quest == quest.Id)?.Reason ?? "the remote has not taken the claim yet")
                + $". The fresh take and conflict {named}'s dismissal stand on this machine until a sync reaches the remote; if another "
                + $"machine's take or a dismissal of it reached the remote first, both are lost then, and conflict {named} stays unresolved.",
                await quests.FindAsync(quest.Id, ct).ConfigureAwait(false));
        }

        // Lost again. The conflict gone from the quest while the mark went with the take: a plain dismissal of it reached the remote first.
        var standing = await quests.FindAsync(quest.Id, ct).ConfigureAwait(false);
        if (standing is not null && !standing.Conflicts.Any(c => c.Machine == conflict.Machine && c.Sequence == conflict.Sequence))
        {
            return new(
                QuestRespondRefusal.NoLostTake,
                $"Quest `#{quest.Id}`'s conflict {named} was dismissed on another machine before this bring-forward reached the remote, and "
                + "a settled conflict authorizes no take: the fresh take lost and is kept on the quest as a conflict of its own. The step is "
                + "not this machine's, and nothing goes on.",
                Quest: null);
        }

        var why = QuestLog.LostToTheReopen(await quests.HistoryAsync(quest.Id, ct).ConfigureAwait(false), quests.Machine)
            ? "the person's reopen of the work it grew from reached the remote before this bring-forward"
            : "it was taken on another machine before this bring-forward reached the remote";
        return new(
            QuestRespondRefusal.AlreadyTaken,
            $"Quest `#{quest.Id}` moved under this machine again: {why}, so the fresh take lost too and is kept on the quest as a new "
            + $"conflict. Conflict {named} stays unresolved, the step is not this machine's, and nothing goes on.",
            Quest: null);
    }
}
