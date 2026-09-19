namespace Daoris.Knowledge;

/// <summary>Why a publish did not produce a quest — or <see cref="None"/> when it did.</summary>
public enum QuestPublishRefusal
{
    None,

    /// <summary>Addressed to the repository doing the asking. Its own backlog is the place for that.</summary>
    SelfAddressed,

    /// <summary>The target has not adopted, so it has no client to see the quest (D32, D34).</summary>
    NotAddressable,

    /// <summary>
    /// The receiver is joined to a remote that did not answer, so the quest could not be given its one
    /// home (D47 §5). Nothing was published anywhere — a half-published quest would be two opinions.
    /// </summary>
    HomeUnreachable,
}

/// <param name="Refusal"><see cref="QuestPublishRefusal.None"/> when the quest was published.</param>
/// <param name="Message">The full answer, phrased once here so no two hosts can drift apart on it.</param>
/// <param name="Quest">The published quest, when there is one.</param>
/// <param name="Addressable">Who can be asked — the actionable half of a refusal.</param>
public sealed record QuestPublishOutcome(
    QuestPublishRefusal Refusal,
    string Message,
    Quest? Quest,
    IReadOnlyList<string> Addressable);

/// <summary>Why a response did not move a quest — or <see cref="None"/> when it did.</summary>
public enum QuestRespondRefusal
{
    None,

    /// <summary>Not take, done, or decline.</summary>
    UnknownAction,

    /// <summary>Declining needs a reason: it is the part the asker can act on.</summary>
    MissingReason,

    /// <summary>No quest under that id.</summary>
    NotFound,

    /// <summary>Somebody got there first — the losing side of the race stands down (D47 §5).</summary>
    AlreadyTaken,

    /// <summary>Done and Declined are terminal: one title is one quest forever (D46 §3).</summary>
    Closed,

    /// <summary>
    /// The quest lives at a remote that did not answer. Nothing was changed and nothing was queued —
    /// a transition either writes through or fails plainly (D47 §2).
    /// </summary>
    HomeUnreachable,
}

/// <param name="Refusal"><see cref="QuestRespondRefusal.None"/> when the status moved.</param>
/// <param name="Message">The full answer, phrased once here.</param>
/// <param name="Quest">The quest as it now stands, when the status moved.</param>
public sealed record QuestRespondOutcome(QuestRespondRefusal Refusal, string Message, Quest? Quest);

/// <summary>
/// The judgement half of the quest system: who may be addressed, what a refusal says, what a response
/// requires. The stores hold state; this decides.
/// </summary>
/// <remarks>
/// <para>It exists because there are two doors — MCP for an agent beside the repositories, HTTP for a
/// remote deployment — and the rules were about to be written twice. Two copies would drift, and the
/// same ask would then be deliverable through one door and refused at the other, which for a quest
/// system is the worst available bug: it looks like the sibling ignoring you.</para>
///
/// <para>The messages are composed here too, not only the verdicts. They are what an agent acts on, so
/// two hosts phrasing them differently is two behaviours in all the ways that matter.</para>
/// </remarks>
public sealed class QuestExchange(KnowledgeService service, QuestStore quests, IRemoteQuestClient? remote = null)
{
    /// <summary>
    /// Publish a quest to another repository. Refuses a self-addressed quest and a target that has not
    /// adopted; warns when the target has declared nothing about itself (D34). A quest for a JOINED
    /// receiver homes at the remote (D47 §5) — the publish writes through and the mirror keeps a copy.
    /// </summary>
    public async Task<QuestPublishOutcome> PublishAsync(
        string from, string to, string title, string body, DateTimeOffset now, CancellationToken ct = default)
    {
        if (string.Equals(from, to, StringComparison.OrdinalIgnoreCase))
        {
            return new(
                QuestPublishRefusal.SelfAddressed,
                "That is the repository you are in — a quest is work for someone else. Use its own backlog.",
                Quest: null, Addressable: []);
        }

        var registered = await service.RegistryAsync(ct).ConfigureAwait(false);
        var addressable = registered.Where(r => r.Adopted).Select(r => r.Repository).ToList();
        var target = registered.FirstOrDefault(r =>
            string.Equals(r.Repository, to, StringComparison.OrdinalIgnoreCase));

        if (target is null || !target.Adopted)
        {
            // A quest for a repository with no client has nobody to read it, so it would sit in a queue
            // that is never opened. Saying so now beats letting it look delivered.
            return new(
                QuestPublishRefusal.NotAddressable,
                $"`{to}` has not adopted Daoris, so it has no way to see a quest. Addressable: "
                + $"{string.Join(", ", addressable)}.",
                Quest: null, addressable);
        }

        // Home follows the receiver, decided at publish and never migrated (D47 §5): a joined
        // receiver's quests live at the remote, because other machines may be drivable for it and the
        // one lock must sit where every taker can reach it.
        if (remote is not null && target.Joined)
        {
            var answer = await remote.PublishAsync(from, to, title, body, ct).ConfigureAwait(false);
            if (answer.Status == 0)
            {
                return new(
                    QuestPublishRefusal.HomeUnreachable,
                    $"`{target.Repository}` is joined to a remote that is not answering ({answer.Message}) — "
                    + "nothing was published. Publish again when it is reachable.",
                    Quest: null, addressable);
            }

            if (answer.Status != 200 || answer.Quest is null)
            {
                // The remote's own judgement said no — its registry, not ours, knows who is
                // addressable there. Its message travels verbatim so the two doors cannot drift.
                return new(QuestPublishRefusal.NotAddressable, answer.Message, Quest: null, addressable);
            }

            var homed = answer.Quest with { Home = "remote" };
            await quests.MirrorAsync(homed, ct).ConfigureAwait(false);
            return new(QuestPublishRefusal.None, answer.Message, homed, addressable);
        }

        var quest = await quests.PublishAsync(from, to, title, body, now, ct).ConfigureAwait(false);

        var caution = target.Registered
            ? ""
            : $"\n\n⚠ `{target.Repository}` has not declared what it owns or accepts, so this may not be "
              + "its problem. Worth checking before you rely on it.";

        return new(
            QuestPublishRefusal.None,
            $"Published quest `#{quest.Id}` to `{quest.To}` — {quest.Status}.{caution}\n\n"
            + "It is held by the service, not written into that repository. Its agent will see it and "
            + "decide. Do not make the change yourself.",
            quest, addressable);
    }

    /// <summary>
    /// Answer a quest: take, done, or decline. Declining without a reason is refused — a bare refusal
    /// gives the asker nothing to act on.
    /// </summary>
    public async Task<QuestRespondOutcome> RespondAsync(
        string id, string action, string? reason, DateTimeOffset now, CancellationToken ct = default)
    {
        var status = action.ToLowerInvariant() switch
        {
            "take" => QuestStatus.Taken,
            "done" => QuestStatus.Done,
            "decline" => QuestStatus.Declined,
            _ => (QuestStatus?)null,
        };

        if (status is null)
        {
            return new(
                QuestRespondRefusal.UnknownAction,
                $"Unknown action '{action}' — one of: take, done, decline.",
                Quest: null);
        }

        if (status == QuestStatus.Declined && string.IsNullOrWhiteSpace(reason))
        {
            return new(
                QuestRespondRefusal.MissingReason,
                "Declining needs a reason: it is the part the asker can act on.",
                Quest: null);
        }

        // A verb on a remote-homed quest writes through to its home's judgement — the one lock — and
        // the mirror takes the result (D47 §5). An id we do not hold at all is also tried remotely
        // when a remote exists: the mirror may simply not have caught up to a quest that lives there.
        var local = await quests.FindAsync(id.TrimStart('#'), ct).ConfigureAwait(false);
        if (local is { Home: not null } || (local is null && remote is not null))
        {
            if (remote is null)
            {
                return new(
                    QuestRespondRefusal.HomeUnreachable,
                    $"Quest `#{id.TrimStart('#')}` lives at a remote, and this machine has none configured — "
                    + "nothing was changed.",
                    Quest: null);
            }

            var answer = await remote.RespondAsync(id.TrimStart('#'), action, reason, ct).ConfigureAwait(false);
            switch (answer.Status)
            {
                case 0:
                    return new(
                        QuestRespondRefusal.HomeUnreachable,
                        $"Quest `#{id.TrimStart('#')}` lives at a remote that is not answering "
                        + $"({answer.Message}) — nothing was changed. A transition writes through or fails; it never queues.",
                        Quest: null);
                case 200 when answer.Quest is not null:
                    var mirrored = answer.Quest with { Home = "remote" };
                    await quests.MirrorAsync(mirrored, ct).ConfigureAwait(false);
                    return new(QuestRespondRefusal.None, answer.Message, mirrored);
                case 404:
                    return new(QuestRespondRefusal.NotFound, answer.Message, Quest: null);
                case 409:
                    // The race, resolved at the home. For a take that reads as "someone got there
                    // first"; for anything else the quest is closed. Either way the remote's own
                    // message travels verbatim.
                    return new(
                        status == QuestStatus.Taken ? QuestRespondRefusal.AlreadyTaken : QuestRespondRefusal.Closed,
                        answer.Message, Quest: null);
                default:
                    return new(QuestRespondRefusal.UnknownAction, answer.Message, Quest: null);
            }
        }

        var move = await quests.MoveAsync(id.TrimStart('#'), status.Value, reason, now, ct)
            .ConfigureAwait(false);

        if (move.Quest is null)
        {
            return new(QuestRespondRefusal.NotFound, $"No quest `#{id.TrimStart('#')}`. Ids come from `quest_list`.", Quest: null);
        }

        if (move.Moved)
        {
            return new(QuestRespondRefusal.None, $"Quest `#{move.Quest.Id}` is now {move.Quest.Status}.", move.Quest);
        }

        // The store refused the transition; the quest comes back unchanged so the answer can name the
        // state that refused it. For a take that is the race resolving (D47 §5) — the same message a
        // session racing an outside session already acts on.
        return move.Quest.Status == QuestStatus.Taken
            ? new(
                QuestRespondRefusal.AlreadyTaken,
                $"Quest `#{move.Quest.Id}` is already taken — someone is working it. Stand down rather than doubling the work.",
                Quest: null)
            : new(
                QuestRespondRefusal.Closed,
                $"Quest `#{move.Quest.Id}` is {move.Quest.Status} — a closed quest does not move; a new ask is a new title.",
                Quest: null);
    }
}
