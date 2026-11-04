namespace Daoris.Knowledge;

/// <summary>The kind and workflow of a quest: the person's choice on a quest with no ask, and an intake's proposal at publish.</summary>
internal sealed class QuestWorkflowKinds(QuestStore quests, AskStore? asks)
{
    /// <summary>
    /// The person sets the kind and workflow of a quest published with no ask, on its page before it starts (D157; the
    /// workflow design §4.1 row 1, §4.3): a kind, a workflow, both, or neither to clear them, with any words they give, kept with when
    /// in a <see cref="QuestOperationKind.Chosen"/> operation; the latest stands. A run is bound at its first start (§4.5), so a choice
    /// reaches the quest's work only while it is open.
    /// </summary>
    /// <remarks>
    /// <b>The person's door</b> (D156 §3.2), a local host's alone, as a verdict is: no connector tool reaches it, and an agent's kind is
    /// a proposal. A quest an ask published follows its ask's choice, which is set on the ask. Whether the quest's workspace
    /// declares the kind and the workflow reads is the driving machine's to say; the service holds neither. It travels from here as an
    /// operation (D68), so the machine that starts the quest reads it against its own workspace's kinds.
    /// </remarks>
    public async Task<QuestRespondOutcome> ChooseWorkflowAsync(
        string id, string? kind, string? workflow, string? words, DateTimeOffset now, CancellationToken ct = default)
    {
        var name = id.TrimStart('#');
        if (await quests.FindAsync(name, ct).ConfigureAwait(false) is not { } quest)
        {
            return new(QuestRespondRefusal.NotFound, $"No quest `#{name}`. Ids come from `quest_list`.", Quest: null);
        }

        if (AskDesk.AskOf(quest.From) is { } ask)
        {
            return new(QuestRespondRefusal.AskChooses,
                $"Quest `#{quest.Id}` was asked by ask `#{ask}`, whose kind and workflow are its work's: choose them on the ask. Nothing was kept.",
                Quest: null);
        }

        if (quest.Status != QuestStatus.Open) return new(Started(quest), WorkflowStarted(quest), Quest: null);

        var namedKind = string.IsNullOrWhiteSpace(kind) ? null : kind.Trim();
        var named = string.IsNullOrWhiteSpace(workflow) ? null : workflow.Trim();
        if (WorkflowChoices.Judge(namedKind, named) is { } unfit)
        {
            return new(QuestRespondRefusal.BadWorkflow, $"The quest's {unfit}. Nothing was kept.", Quest: null);
        }

        var said = string.IsNullOrWhiteSpace(words) ? null : words.Trim();
        if (said is { Length: > WorkflowChoices.WordsLimit })
        {
            return new(QuestRespondRefusal.BadWorkflow,
                $"Your words with a kind or a workflow are at most {WorkflowChoices.WordsLimit} characters; these are {said.Length}. Nothing was kept.",
                Quest: null);
        }

        var move = await quests.ChooseAsync(quest.Id, new QuestWorkflowChoice(namedKind, named) { Words = said }, now, ct).ConfigureAwait(false);
        if (move.Quest is null) return new(QuestRespondRefusal.NotFound, $"No quest `#{quest.Id}`. Ids come from `quest_list`.", Quest: null);
        if (!move.Moved) return new(Started(move.Quest), WorkflowStarted(move.Quest), Quest: null);

        var chose = (namedKind, named) switch
        {
            (null, null) => $"Quest `#{quest.Id}`'s kind and workflow are cleared: its work follows what its repository and workspace choose.",
            ({ } k, null) => $"Quest `#{quest.Id}` is of kind `{k}`: its work follows the workflow its repository or workspace maps it to.",
            (null, { } w) => $"Quest `#{quest.Id}` follows workflow `{w}` from its start.",
            ({ } k, { } w) => $"Quest `#{quest.Id}` is of kind `{k}` and follows workflow `{w}` from its start.",
        };
        return new(QuestRespondRefusal.None, chose + (said is null ? "" : " Your words are kept with it."), move.Quest);

        static QuestRespondRefusal Started(Quest standing) =>
            standing.Status == QuestStatus.Taken ? QuestRespondRefusal.AlreadyTaken : QuestRespondRefusal.Closed;

        static string WorkflowStarted(Quest standing) => standing.Status == QuestStatus.Taken
            ? $"Quest `#{standing.Id}` is Taken: its run was bound to a workflow at its start, so a choice now reaches nothing. Nothing was kept."
            : $"Quest `#{standing.Id}` is {standing.Status}: nothing of it waits for a workflow. Nothing was kept.";
    }

    /// <summary>
    /// Why an intake's kind proposal at publish is refused, or null where there is none or it stands (D157; the workflow
    /// design §4.3): a kind that is not a kind's id, a reason that is missing or past its bound, one on no ask held here, or a reason
    /// that is not the person's words, verbatim, as a review choice's words are. The exchange only judges; the ask's
    /// desk keeps the proposal and the person's press applies it. Version 1 only proposes: whether a kind lowers the person's part
    /// is the driving machine's table, which the service does not hold.
    /// </summary>
    public async Task<(QuestPublishRefusal Refusal, string Message)?> JudgeProposalAsync(string from, QuestAsk ask, CancellationToken ct)
    {
        if (ask.WorkflowProposal is not { } proposed) return null;

        var reason = proposed.Reason?.Trim() ?? "";
        if (WorkflowChoices.JudgeProposal(proposed.Kind?.Trim(), reason) is { } unfit)
        {
            return (QuestPublishRefusal.BadWorkflow, $"{unfit}. Nothing was published.");
        }

        if (AskDesk.AskOf(from) is not { } askId || asks is null || await asks.FindAsync(askId, ct).ConfigureAwait(false) is not { } held)
        {
            return (QuestPublishRefusal.BadWorkflow,
                $"A kind proposal quotes the person's words on the ask a quest is asked by, and this quest is asked by `{from}`, "
                + "on no ask held here — so there are no words of theirs to quote. Nothing was published.");
        }

        return held.Words.Any(word => QuestRequirement.QuotedIn(reason, word.Text))
            ? null
            : (QuestPublishRefusal.NotQuoted,
                $"The kind `{proposed.Kind?.Trim()}` is proposed on words the person never said on ask `#{held.Id}`: \"{Clip(reason)}\". "
                + "The reason is their own words, verbatim (whitespace and case aside); the person decides on a proposal. Nothing was published.");
    }
}
