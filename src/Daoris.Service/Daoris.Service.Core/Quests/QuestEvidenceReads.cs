namespace Daoris.Knowledge;

/// <summary>Daoris's reading of a done's evidence: the verdict kept, and what it means for whoever posted it.</summary>
internal sealed class QuestEvidenceReads(QuestStore quests, QuestReviews reviews)
{
    /// <summary>
    /// Daoris read a done's evidence (D144 §3): the driver's verdict at the end of the session that made the done,
    /// the orphan sweep's, or the person's check at the terminal. The quest must be done and waiting on its evidence, and
    /// the verdict must name the commit read and read every item of every met requirement, and nothing else. Found, what
    /// the done held goes on — its next step published in the same transaction; missing, it waits for the person.
    /// </summary>
    /// <remarks>
    /// <para>The driver's door, never an agent's: no connector tool reaches it, since the session a requirement judges
    /// never writes its verdict (D144 §1). It commits here and travels like any verb (D68).</para>
    ///
    /// <para><b>A fact, with no model</b> (D24, D54): the exchange cannot read a commit, so it judges only that the verdict
    /// reads what the done waits on, in codes, and keeps it. Whether the commit is the done's or follows it on the same
    /// history is the reader's to settle, which has the tree.</para>
    /// </remarks>
    public async Task<QuestRespondOutcome> EvidenceAsync(
        string id, QuestEvidenceVerdict verdict, DateTimeOffset now, CancellationToken ct = default)
    {
        var name = id.TrimStart('#');
        if (await quests.FindAsync(name, ct).ConfigureAwait(false) is not { } quest)
        {
            return new(QuestRespondRefusal.NotFound, $"No quest `#{name}`. Ids come from `quest_list`.", Quest: null);
        }

        // A verdict reading nothing, where nothing waits on evidence, is the commit a submission's session's end read alone.
        var alone = verdict.Items.Count == 0 && !quest.AwaitsEvidence;
        if ((alone ? NoCommitWanted(quest) : NotAwaiting(quest)) is { } why) return new(QuestRespondRefusal.NotAwaitingEvidence, why, Quest: null);
        if ((alone ? Shape(verdict) : JudgeVerdict(quest, verdict)) is { } unfit)
        {
            return new(QuestRespondRefusal.BadVerdict, unfit + " Nothing was kept.", Quest: null);
        }

        if (Unasked(quest, verdict) is { } other) return new(QuestRespondRefusal.NotAwaitingEvidence, other, Quest: null);

        var judged = verdict with { Commit = verdict.Commit.ToLowerInvariant() };
        var move = await quests.EvidenceAsync(name, judged, now, ct).ConfigureAwait(false);
        if (move.Quest is null)
        {
            return new(QuestRespondRefusal.NotFound, $"No quest `#{name}`. Ids come from `quest_list`.", Quest: null);
        }

        // Judged again inside the write: another verdict, the person's yes or a later ready may have come first.
        if (!move.Moved)
        {
            return new(QuestRespondRefusal.NotAwaitingEvidence, (alone ? NoCommitWanted(move.Quest) : NotAwaiting(move.Quest))
                ?? Unasked(move.Quest, judged) ?? $"Quest `#{name}` no longer waits on what this verdict reads.", Quest: null);
        }

        return alone
            ? new(QuestRespondRefusal.None,
                $"Kept the commit of quest `#{name}`'s submission `{move.Quest.Submission!.Machine}` {move.Quest.Submission.Sequence}: "
                + $"`{QuestWords.Short(judged.Commit)}`, read at the end of the session that made it. It waits on no evidence, so nothing else moved.",
                move.Quest)
            : new(QuestRespondRefusal.None, await EvidencedAsync(move.Quest, judged, ct).ConfigureAwait(false) + QuestWords.Then(move.FollowUp), move.Quest);
    }

    /// <summary>
    /// Why a commit read alone is not kept on <paramref name="quest"/>, as a state, or null while it keeps one: a taken
    /// quest's current submission that waits on no evidence and whose commit no verdict gave yet.
    /// </summary>
    private static string? NoCommitWanted(Quest quest) => quest switch
    {
        { Status: QuestStatus.Taken, Submission: { } current, SubmissionCommit: { } known } =>
            $"Quest `#{quest.Id}`'s submission `{current.Machine}` {current.Sequence} was read at `{QuestWords.Short(known)}` already: its commit is "
            + "known, and nothing more is kept.",
        { Status: QuestStatus.Taken, Submission: not null } => null,
        _ => $"Quest `#{quest.Id}` is {quest.Status} with no submission standing: a commit is read alone for a submission, at the end of the "
             + "session that said its `ready`.",
    };

    /// <summary>Why a commit read alone is not one in shape, or null: the verdict's own fields, the one judge's.</summary>
    private static string? Shape(QuestEvidenceVerdict verdict) => verdict.JudgeShape() is { } fault ? Worded(fault, verdict) : null;

    /// <summary>Why a verdict on <paramref name="quest"/> is not taken, as a state — or null while its done waits on evidence.</summary>
    private static string? NotAwaiting(Quest quest) => quest switch
    {
        { AwaitsEvidence: true } => null,
        { Status: not QuestStatus.Done } =>
            $"Quest `#{quest.Id}` is {quest.Status}: evidence is read on a done, once the session that closed it ends.",
        { Accepted: { } at } =>
            $"Quest `#{quest.Id}`'s done was accepted as it stood, {QuestWords.When(at)}: nothing waits on its evidence.",
        { Evidence: { Found: true } found } =>
            $"Quest `#{quest.Id}`'s evidence was already found, at `{QuestWords.Short(found.Commit)}`: nothing waits on it.",
        _ => $"Quest `#{quest.Id}`'s done names no evidence on a requirement it met: nothing waits on it.",
    };

    /// <summary>
    /// Why <paramref name="verdict"/> was not asked for what <paramref name="quest"/> stands on (§E5), or null
    /// when it was: a submission's verdict names that submission, and a done's names none.
    /// </summary>
    private static string? Unasked(Quest quest, QuestEvidenceVerdict verdict) => verdict.AskedFor(quest)
        ? null
        : (quest.Submission, verdict.Submission) switch
        {
            ({ } current, { } asked) =>
                $"This verdict was read for submission `{asked.Machine}` {asked.Sequence} of quest `#{quest.Id}`, and its current "
                + $"submission is `{current.Machine}` {current.Sequence}: a later `ready` replaced the one read, so read the current "
                + "one's evidence. Nothing was kept.",
            ({ } current, null) =>
                $"Quest `#{quest.Id}` waits on its submission's evidence, and a verdict names the submission it read: "
                + $"`{current.Machine}` {current.Sequence} is current. Nothing was kept.",
            (null, { } asked) =>
                $"This verdict was read for submission `{asked.Machine}` {asked.Sequence}, and quest `#{quest.Id}` has none: "
                + "a done's verdict names no submission. Nothing was kept.",
            _ => null,
        };

    /// <summary>
    /// Why <paramref name="verdict"/> does not read exactly what <paramref name="quest"/> waits on, naming the first fault
    /// and every item left unread — or null when it does. Its shape is the one judge's
    /// (<see cref="QuestEvidenceVerdict.JudgeShape"/>), which the wire calls too; what it reads is judged here, against the
    /// quest, as the replay's <see cref="QuestEvidenceVerdict.Covers"/> judges it.
    /// </summary>
    private static string? JudgeVerdict(Quest quest, QuestEvidenceVerdict verdict)
    {
        if (verdict.JudgeShape() is { } fault) return Worded(fault, verdict);

        var wanted = quest.EvidenceWanted;
        var read = new HashSet<(int, string?, string?)>();
        foreach (var (item, index) in verdict.Items.Select((item, index) => (item, index + 1)))
        {
            var which = $"Item {index}";
            var named = item.Path ?? item.Gate!;
            if (!wanted.Any(each => each.Requirement == item.Requirement && item.Names(each.Item)))
            {
                return item.Requirement > quest.Requirements.Count
                    ? $"{which} reads requirement {item.Requirement}, and quest `#{quest.Id}` carries {quest.Requirements.Count}."
                    : $"{which} reads `{Clip(named)}` for requirement {item.Requirement}, which its done does not wait on: "
                      + "a verdict reads each item of each requirement the done met, as the requirement names it.";
            }

            if (!read.Add((item.Requirement, item.Path, item.Gate)))
            {
                return $"{which} reads `{Clip(named)}` for requirement {item.Requirement}, which is read twice: read each once.";
            }
        }

        var unread = wanted.Where(each => !read.Contains((each.Requirement, each.Item.Path, each.Item.Gate))).ToList();
        return unread.Count == 0
            ? null
            : $"Quest `#{quest.Id}`'s done waits on {QuestWords.Count(wanted.Count, "item")} of evidence, and this verdict leaves "
              + $"{(unread.Count == 1 ? "one" : unread.Count.ToString(System.Globalization.CultureInfo.InvariantCulture))} unread:\n\n"
              + string.Join("\n", unread.Select(each => $"- requirement {each.Requirement}: `{each.Item.Named}`"))
              + "\n\nRead each in the commit, and say what each was found to be.";
    }

    /// <summary>A verdict's shape fault as the evidence door answers it: which field, on which item, and why.</summary>
    private static string Worded(QuestVerdictFault fault, QuestEvidenceVerdict verdict)
    {
        if (fault.Item is not { } number)
        {
            return fault.Field switch
            {
                QuestVerdictField.Commit =>
                    $"A verdict names the commit it read by its full id (40 or 64 hex characters), and `{Clip(verdict.Commit ?? "")}` is not one.",
                QuestVerdictField.How =>
                    $"A verdict says how its commit was chosen: `session-end`, `sweep` or `terminal`, and `{Clip(verdict.How ?? "")}` is none of them.",
                QuestVerdictField.Submission =>
                    "A verdict names the submission it was read for by the machine and the sequence that made it, as the quest answers them.",
                _ => $"A verdict names the session whose end it read by its id, and `{Clip(verdict.Session ?? "")}` is not one.",
            };
        }

        var item = verdict.Items[number - 1];
        var which = $"Item {number}";
        var named = item.Path ?? item.Gate ?? "";
        return fault.Field switch
        {
            QuestVerdictField.Requirement => $"{which} reads requirement {item.Requirement}, and requirements are numbered from 1.",
            QuestVerdictField.PathOrGate => $"{which} names exactly one of `path` or `gate`, as the requirement's evidence does.",
            QuestVerdictField.Path => $"{which}, `{Clip(named)}`, is not a repository-relative path: {fault.Why}.",
            QuestVerdictField.Gate => $"{which}, `{Clip(named)}`, is not a gate's name: {fault.Why}.",
            QuestVerdictField.Result =>
                $"{which} says `{Clip(item.Result ?? "")}` of `{Clip(named)}`, and a {(item.Path is not null ? "path" : "gate")} is read as one of "
                + string.Join(", ", (item.Path is not null ? QuestEvidenceCodes.PathResults : QuestEvidenceCodes.GateResults).Select(code => $"`{code}`"))
                + ".",
            QuestVerdictField.Object =>
                $"{which} names the object found at `{Clip(named)}`, and `{Clip(item.Object ?? "")}` is not a full object id.",
            _ => $"{which} says the commit spells `{Clip(named)}` as `{Clip(item.Spelled ?? "")}`, read `{Clip(item.Result ?? "")}`: {fault.Why}.",
        };
    }

    /// <summary>
    /// What a kept verdict means for whoever posted it: found, and what the done held goes on; missing, which item
    /// was not there and the person's two doors; or found beside a departure, which still waits for their yes.
    /// </summary>
    private async Task<string> EvidencedAsync(Quest quest, QuestEvidenceVerdict verdict, CancellationToken ct)
    {
        var at = $"Read the evidence of quest `#{quest.Id}` at `{QuestWords.Short(verdict.Commit)}`";
        if (verdict.Found)
        {
            if (quest.Hold == QuestHold.Departed)
            {
                return $"{at}: each of its {QuestWords.Count(verdict.Items.Count, "item")} is there. It still departs from a requirement, "
                       + $"so it waits for the person's yes: `daoris quest accept {quest.Id}`.";
            }

            if (quest.Hold == QuestHold.Unreviewed)
            {
                return $"{at}: each of its {QuestWords.Count(verdict.Items.Count, "item")} is there."
                       + await reviews.ReviewWaitsAsync(quest, ct).ConfigureAwait(false);
            }

            return $"{at}: each of its {QuestWords.Count(verdict.Items.Count, "item")} is there, so what it held goes on."
                   + QuestWords.Resumes(await quests.WaitingOnAsync(quest.Id, ct).ConfigureAwait(false));
        }

        var missing = verdict.Items.Where(item => item.Result != QuestEvidenceCodes.Found).ToList();
        return $"{at}: {missing.Count} of its {QuestWords.Count(verdict.Items.Count, "item")} {(missing.Count == 1 ? "is" : "are")} not "
               + $"there, so it stays held for the person:\n\n"
               + string.Join("\n", missing.Select(item => $"- requirement {item.Requirement}: `{item.Path ?? item.Gate}` {item.Result}"
                   + (item.Spelled is { } spelled ? $" (the commit holds `{spelled}`)" : "")))
               + $"\n\nThe person accepts the done as it stands with `daoris quest accept {quest.Id}`, or reads it again "
               + $"at a later commit on the same history with `daoris quest check {quest.Id} --commit <sha>`.";
    }
}
