namespace Daoris.Knowledge;

/// <summary>The graph's close door: every quest of a chain Done in one transaction, and the answer to a close sent before.</summary>
internal sealed class QuestGraphCloses(QuestStore quests)
{
    /// <summary>
    /// The graph's close door (§E5): every quest of the chain this repository holds, Done in one transaction,
    /// each on the submission named.
    /// </summary>
    /// <remarks>
    /// <para>The driver's door, never an agent's or a shared host's: it runs under the unit's record lock, which the driver
    /// holds across the whole close (the fence's steps 1 and 3), and this is step 2, one SQLite transaction over every quest
    /// named. A remote judges the Done it pushes quest by quest (D68), where a take lost meanwhile leaves the chain partially
    /// closed, which the driver shows.</para>
    ///
    /// <para><paramref name="id"/> is the quest the route names, which must be among those named: the close is the unit's.</para>
    /// </remarks>
    public async Task<QuestCloseOutcome> CloseAsync(
        string id, string? key, string? token, IReadOnlyList<(string Id, QuestOperationRef Submission)> named,
        DateTimeOffset now, CancellationToken ct = default)
    {
        var (malformed, close, listed, route) = Call(id, key, token, named);
        if (malformed is not null) return malformed;
        return Answered(await quests.CloseAsync(route, listed, close!, now, ct).ConfigureAwait(false), close!, route);
    }

    /// <summary>
    /// What became of a close sent with <paramref name="key"/> (§E5 step 4): the driver's question, at the close
    /// door's side, before it would ever send a close it holds no answer to again. Answered from what the store keeps under the
    /// key, in the words the close door answers that close with, never by judging or writing a Done; a key never taken is
    /// withdrawn as it is answered, so its close never runs (<see cref="QuestStore.CloseOutcomeAsync"/>).
    /// </summary>
    /// <remarks>The same call as the close names (its key, token and every quest with its submission), checked alike.</remarks>
    public async Task<QuestCloseOutcome> CloseOutcomeAsync(
        string id, string? key, string? token, IReadOnlyList<(string Id, QuestOperationRef Submission)> named,
        DateTimeOffset now, CancellationToken ct = default)
    {
        var (malformed, close, listed, route) = Call(id, key, token, named);
        if (malformed is not null) return malformed;
        return Answered(await quests.CloseOutcomeAsync(listed, close!, now, ct).ConfigureAwait(false), close!, route);
    }

    /// <summary>A close's call as the door reads it, or why it is not one: a malformed call names no key and keeps none.</summary>
    private static (QuestCloseOutcome? Malformed, QuestClose? Close, List<(string Id, QuestOperationRef Submission)> Listed, string Route) Call(
        string id, string? key, string? token, IReadOnlyList<(string Id, QuestOperationRef Submission)> named)
    {
        var route = id.TrimStart('#');
        var listed = named.Select(each => (Id: each.Id.TrimStart('#'), each.Submission)).ToList();
        (QuestCloseOutcome?, QuestClose?, List<(string Id, QuestOperationRef Submission)>, string) Malformed(string why) =>
            (new(QuestCloseRefusal.Malformed, why + " Nothing was closed.", []), null, listed, route);

        if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(token))
        {
            return Malformed("A close names its key, which a second send of the same close repeats, and its close token.");
        }

        if (listed.Count == 0) return Malformed("A close names each quest it closes, with the submission it judged.");
        if (listed.Any(each => string.IsNullOrWhiteSpace(each.Submission.Machine) || each.Submission.Sequence < 1))
        {
            return Malformed("Each quest a close names comes with its submission's machine and sequence.");
        }

        if (listed.Select(each => each.Id).Distinct(StringComparer.Ordinal).Count() != listed.Count)
        {
            return Malformed("A close names each quest once.");
        }

        if (!listed.Any(each => string.Equals(each.Id, route, StringComparison.Ordinal)))
        {
            return Malformed($"A close at `#{route}` names `#{route}` among the quests it closes.");
        }

        return (null, new QuestClose(key.Trim(), token.Trim()), listed, route);
    }

    /// <summary>What the close door answers for <paramref name="closing"/>, the close made or refused now, or its first outcome.</summary>
    private static QuestCloseOutcome Answered(QuestClosing closing, QuestClose close, string route)
    {
        var on = closing.On ?? route;

        // A key never taken before the driver asked after it is withdrawn, whether this is the question's answer or a
        // send of it arriving after: said alike, since that close never ran.
        if (closing.Refusal == QuestCloseRefusal.Withdrawn)
        {
            return new(closing.Refusal,
                $"Key `{close.Key}` was not taken by this door before Daoris asked what became of it, so its close never ran, and the key "
                + "is withdrawn: it closes nothing, now or later. The close is judged again and sent with a new key. Nothing was closed.",
                closing.Quests);
        }

        if (closing.Replayed && closing.Refusal != QuestCloseRefusal.None)
        {
            // The first outcome, though what refused it may be gone since: said as it was, not as it now stands.
            return new(closing.Refusal,
                $"This close was refused when it was first sent with key `{close.Key}`: {RefusedThen(closing.Refusal, on)} The same "
                + "close sent again answers its first outcome, so a new attempt is sent with a new key. Nothing was closed.",
                closing.Refusal == QuestCloseRefusal.NotFound ? [] : closing.Quests);
        }

        return closing.Refusal switch
        {
            QuestCloseRefusal.None => new(QuestCloseRefusal.None, closing.Replayed
                ? $"This close was made already: {Closed(closing.Quests)}. Nothing more was written."
                : $"Closed {Closed(closing.Quests)}, each on the submission named.", closing.Quests),
            QuestCloseRefusal.NotFound => new(closing.Refusal, $"No quest `#{on}`. Ids come from `quest_list`. Nothing was closed.", []),
            QuestCloseRefusal.KeyReused => new(closing.Refusal,
                $"Key `{close.Key}` was sent with another close already, with another token or other submissions: a key answers "
                + "only the close it was sent with. A new attempt is sent with a new key. Nothing was closed.", closing.Quests),
            _ => new(closing.Refusal,
                $"{Refused(closing.Refusal, closing.Quest!, closing.Quests.FirstOrDefault(quest => quest.Id == route))} Nothing was closed.",
                closing.Quests),
        };
    }

    /// <summary>Which check refused a close when it was first sent, in words that hold however the quest has moved since.</summary>
    private static string RefusedThen(QuestCloseRefusal refusal, string on) => refusal switch
    {
        QuestCloseRefusal.NotFound => $"no quest `#{on}` was found.",
        QuestCloseRefusal.Closed => $"quest `#{on}` was closed already.",
        QuestCloseRefusal.NotBound => $"quest `#{on}` was in no graph that closes it.",
        QuestCloseRefusal.NotHeld => $"quest `#{on}` was not this machine's.",
        QuestCloseRefusal.Stale => $"quest `#{on}`'s current submission was not the one named.",
        QuestCloseRefusal.Held => $"quest `#{on}`'s submission was held for the person.",
        QuestCloseRefusal.Unsealed => $"quest `#{on}`'s next step was not published from its submission.",
        QuestCloseRefusal.OtherRepository => $"quest `#{on}` asked another repository than the quest the close was at.",
        QuestCloseRefusal.NotInChain => $"quest `#{on}` was not of the chain the quest the close was at grew from.",
        QuestCloseRefusal.OtherGraph => $"quest `#{on}` was bound to another graph than the quest the close was at.",
        _ => $"quest `#{on}` was refused.",
    };

    /// <summary>The quests a close wrote Done, as an answer names them.</summary>
    private static string Closed(IReadOnlyList<Quest> quests) =>
        string.Join(", ", quests.Select(quest => $"`#{quest.Id}` is {quest.Status}"));

    /// <summary>
    /// Which of the close's checks refused it, on which quest (§E5 step 2). A scope check also names the
    /// quest the close is at, <paramref name="at"/>, whose repository, chain and graph are the close's.
    /// </summary>
    private static string Refused(QuestCloseRefusal refusal, Quest quest, Quest? at) => refusal switch
    {
        QuestCloseRefusal.OtherRepository => $"Quest `#{quest.Id}` asks `{quest.To}`, and the close at `#{at?.Id}` closes its chain in "
                                             + $"`{at?.To}`: a close names only its own repository's quests, and another repository closes its own.",
        QuestCloseRefusal.NotInChain => $"Quest `#{quest.Id}` is not of the chain `#{at?.Id}` grew from: a close at a quest names only that "
                                        + "quest and the ones before it in its chain.",
        QuestCloseRefusal.OtherGraph => $"Quest `#{quest.Id}` closes through graph `{quest.Closure}`, and the close at `#{at?.Id}` closes "
                                        + $"through `{at?.Closure}`: a close names only quests bound to its own graph.",
        QuestCloseRefusal.Closed => $"Quest `#{quest.Id}` is {quest.Status} already, by another close, the person or another machine.",
        QuestCloseRefusal.NotBound => $"Quest `#{quest.Id}` is in no graph that closes it: its taker's driver never bound it.",
        QuestCloseRefusal.NotHeld => $"Quest `#{quest.Id}` is not this machine's: it was never taken here, or another machine's take "
                                     + "reached the remote first.",
        QuestCloseRefusal.Stale => quest.Submission is null
            ? $"Quest `#{quest.Id}` has no submission: its taker has not said `ready`."
            : $"Quest `#{quest.Id}`'s current submission is not the one named: the close's binding is stale, so judge it again.",
        QuestCloseRefusal.Held => $"Quest `#{quest.Id}`'s submission is held for the person ({QuestEvidenceCodes.Spell(quest.Hold!.Value)}).",
        QuestCloseRefusal.Unsealed => $"Quest `#{quest.Id}`'s next step is not published from its submission, so the submission is not sealed.",
        _ => $"Quest `#{quest.Id}` was refused.",
    };
}
