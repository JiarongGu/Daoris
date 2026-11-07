namespace Daoris.Knowledge;

/// <summary>Where a quest leaves this machine for, and the claim by push a take or a bring-forward makes there.</summary>
internal sealed class QuestRemoteClaims(KnowledgeService service, QuestStore quests, IRemotes? remotes)
{
    /// <summary>
    /// The remote a quest leaves this machine for — its circle's, when its receiver is joined there
    /// — or null for a quest that never leaves: no remote for its circle, or a receiver
    /// nobody shares.
    /// </summary>
    public async Task<IRemote?> SharedAtAsync(Quest quest, CancellationToken ct)
    {
        if (remotes?.For(quest.Workspace) is not { } remote) return null;

        var registered = await service.RegistryAsync(ct: ct).ConfigureAwait(false);
        return registered.Any(r => r.Joined && Workspaces.Same(r.InWorkspace, quest.Workspace)
                                   && string.Equals(r.Repository, quest.To, StringComparison.OrdinalIgnoreCase))
            ? remote
            : null;
    }

    /// <summary>
    /// A take on a shared quest claims by push: committed here a moment ago, it is pushed now and
    /// its answer awaited, so a take that lost is known before any work starts. Null for a quest this
    /// machine does not share — its take is the local one, and the answer the ordinary one.
    /// </summary>
    /// <returns>
    /// Taken when the remote numbered it. <see cref="QuestRespondRefusal.AlreadyTaken"/> when another
    /// machine's take reached it first — the take is a conflict on the quest now, and the session stands
    /// down on the same sentence it always has. Taken, UNCONFIRMED, when the remote did not answer or did
    /// not take it: offline work is allowed, and the next sync decides.
    /// </returns>
    public async Task<QuestRespondOutcome?> ClaimByPushAsync(Quest quest, CancellationToken ct)
    {
        // Only a quest whose receiver is joined in its circle ever leaves this machine; a
        // take on any other is complete the moment it commits.
        if (await SharedAtAsync(quest, ct).ConfigureAwait(false) is not { } remote) return null;

        var pass = await QuestSync.RunAsync(quests, service, remote, quest.Workspace, ct).ConfigureAwait(false);
        var claim = await quests.ClaimAsync(quest.Id, ct).ConfigureAwait(false);
        var reopened = claim == QuestClaim.Lost
                       && QuestLog.LostToTheReopen(await quests.HistoryAsync(quest.Id, ct).ConfigureAwait(false), quests.Machine);
        return claim switch
        {
            QuestClaim.Held => new(
                QuestRespondRefusal.None,
                $"Quest `#{quest.Id}` is now Taken — the remote confirmed the claim.",
                await quests.FindAsync(quest.Id, ct).ConfigureAwait(false)),
            // A take lost to the person's reopen is said as that, under the lost take's refusal, which every reader of a take's answer already stands down on.
            QuestClaim.Lost when reopened
                => new(
                    QuestRespondRefusal.AlreadyTaken,
                    $"Quest `#{quest.Id}` moved under this take: the person's reopen of the work it grew from reached the remote first, "
                    + "so this take lost and is kept on the quest as a conflict. Stand down: the step is open for the work as it stands "
                    + "now, and is taken anew on it.",
                    Quest: null),
            QuestClaim.Lost => new(
                QuestRespondRefusal.AlreadyTaken,
                $"Quest `#{quest.Id}` was taken on another machine first — its take reached the remote before this "
                + "one, which is kept on the quest as a conflict. Stand down rather than doubling the work.",
                Quest: null),
            _ => new(
                QuestRespondRefusal.None,
                $"Quest `#{quest.Id}` is now Taken, UNCONFIRMED: "
                + (pass.Problem ?? pass.Refused.FirstOrDefault(r => r.Quest == quest.Id)?.Reason
                    ?? "the remote has not taken the claim yet")
                + ". The take stands on this machine until a sync reaches the remote. Carry on — and if "
                + "another machine's take reached it first, this session will be stopped.",
                await quests.FindAsync(quest.Id, ct).ConfigureAwait(false)),
        };
    }
}
