namespace Daoris.Knowledge;

/// <summary>Recording an ask, becoming quests, and closing it.</summary>
internal sealed class AskPublishing(
    KnowledgeService service, AskStore asks, QuestExchange exchange, QuestFiles? askFiles, AskStandings standings, AskQuests askQuests,
    AskPublishGuard guard)
{
    /// <summary>
    /// Record an ask, and answer it: a named receiver is published to at once; otherwise the
    /// declarations tier proposes and SAYS SO, and nothing is published on a guess.
    /// </summary>
    public async Task<AskOutcome> AskAsync(AskRequest request, DateTimeOffset now, CancellationToken ct)
    {
        var sentence = request.Sentence.Trim();
        if (sentence.Length == 0)
        {
            return new(AskRefusal.Empty, "An ask needs its words — what is wanted, and why.", Ask: null);
        }

        var registered = await service.RegistryAsync(ct: ct).ConfigureAwait(false);
        var workspace = Workspaces.Normalize(request.Workspace);
        var held = registered.Select(r => r.InWorkspace).Distinct(StringComparer.OrdinalIgnoreCase).Order().ToList();
        if (!held.Any(circle => Workspaces.Same(circle, workspace)))
        {
            return new(
                AskRefusal.UnknownWorkspace,
                $"`{workspace}` is not a workspace this machine holds — an ask is made in a workspace, and the "
                + $"workspace is whom it can reach. Held here: {string.Join(", ", held)}.",
                Ask: null);
        }

        var (unfit, why, links) = exchange.JudgeCarry(request.Links, request.Uploads);
        if (unfit is not null) return new(AskRefusal.BadCarry, why, Ask: null);

        // The asker's short title, judged by the quest's own rule so every door refuses the same, before anything is kept.
        var (shortTitle, unfitShort) = QuestTitles.Judge(request.Short);
        if (unfitShort is not null) return new(AskRefusal.BadShort, unfitShort, Ask: null);

        // The asker's declared paths, judged by the quest's own rule before anything is kept.
        var (paths, unfitPaths) = QuestPaths.Judge(request.Paths);
        if (unfitPaths is not null) return new(AskRefusal.BadPaths, unfitPaths, Ask: null);

        // The person's review choice from the composer, judged before anything is kept.
        AskReviewChoice? chosen = null;
        if (request.Review is not null)
        {
            var (choice, unfitChoice) = AskChoices.JudgeChoice(request.Review, request.ReviewWords, now);
            if (unfitChoice is not null) return new(AskRefusal.BadReview, unfitChoice, Ask: null);
            chosen = choice;
        }

        // The person's kind and workflow from the composer, judged as the ask's own door judges them; none kept for none.
        AskWorkflowChoice? workflowChosen = null;
        if (!string.IsNullOrWhiteSpace(request.Kind) || !string.IsNullOrWhiteSpace(request.Workflow))
        {
            var (choice, unfitChoice) = AskChoices.JudgeWorkflowChoice(request.Kind, request.Workflow, request.WorkflowWords, now);
            if (unfitChoice is not null) return new(AskRefusal.BadWorkflow, unfitChoice, Ask: null);
            workflowChosen = choice;
        }

        // The same words in the same circle are the same ask — a retry, or a person repeating
        // themselves, is answered with what became of the first, never a second copy. 🔴 Unless the
        // person CLOSED it: they ended that one, so the same words afterwards ask anew,
        // and the closed record stays unchanged. A DONE
        // ask ended as surely: its work finished, and the default list no longer shows it.
        var id = AskStore.MakeId(workspace, sentence);
        for (var again = 1; await standings.FindAsync(id, ct).ConfigureAwait(false) is { } existing; again++)
        {
            if (existing.State is not (AskState.Closed or AskState.Done))
            {
                return new(AskRefusal.None, $"Ask `#{id}` was already asked in `{workspace}` — {Describe(existing)}", existing);
            }

            id = AskStore.MakeId(workspace, sentence, again);
        }

        var attachments = new List<QuestAttachment>();
        foreach (var upload in request.Uploads)
        {
            attachments.Add(await askFiles!.KeepAsync(id, upload, ct).ConfigureAwait(false));
        }

        var ask = new Ask(id, workspace, sentence, AskState.Open, AskDesk.ByDeclarations, now, now, request.Asker)
        {
            Links = links,
            Short = shortTitle,
            Paths = paths,
            Attachments = attachments.DistinctBy(a => a.Sha256).ToList(),
            Proposal = DeclarationsTier.Rank(sentence, registered, workspace),
            ReviewChoices = chosen is null ? [] : [chosen],
            WorkflowChoices = workflowChosen is null ? [] : [workflowChosen],
        };

        if (request.To is { Length: > 0 } to)
        {
            var published = await askQuests.PublishAsync(ask, to, now, ct).ConfigureAwait(false);
            if (published.Quest is not null)
            {
                var took = ask with { State = AskState.Published, Tier = AskDesk.ByName, Quests = [published.Quest.Id] };
                if (await LostAsync(took, published.Quest.Id, workspace, now, ct).ConfigureAwait(false) is { } first) return first;
                return new(AskRefusal.None, $"Asked as `#{id}` in `{workspace}`.\n\n{published.Message}{AskPublishGuard.Unrequired}", took, published.Quest);
            }

            // The receiver the person named could not be asked. The ask is still what they meant, so
            // it is kept — proposed by declarations, so the refusal arrives with somewhere to go.
            var kept = ask with { State = AskState.Proposed, Note = published.Message };
            if (await LostAsync(kept, null, workspace, now, ct).ConfigureAwait(false) is { } earlier) return earlier;
            return new(
                AskRefusal.QuestRefused,
                $"{published.Message}\n\nThe ask is kept as `#{id}` — {Describe(kept)}",
                kept);
        }

        var proposed = ask with { State = AskState.Proposed };
        if (await LostAsync(proposed, null, workspace, now, ct).ConfigureAwait(false) is { } already) return already;
        return new(AskRefusal.None, $"Asked as `#{id}` in `{workspace}` — {Describe(proposed)}", proposed);
    }

    /// <summary>Keeps a new ask by insert only: null when kept, else the repeat's answer for the identical ask that holds the id, which a door may have moved since the check.</summary>
    private async Task<AskOutcome?> LostAsync(Ask ask, string? quest, string workspace, DateTimeOffset now, CancellationToken ct)
    {
        if (await asks.InsertAsync(ask, ct).ConfigureAwait(false)) return null;

        if (quest is not null) await asks.RecordPublishedAsync(ask.Id, quest, tier: null, now, ct).ConfigureAwait(false);
        var existing = await standings.FindAsync(ask.Id, ct).ConfigureAwait(false) ?? ask;
        return new(AskRefusal.None, $"Ask `#{ask.Id}` was already asked in `{workspace}` — {Describe(existing)}", existing);
    }

    /// <summary>
    /// A person turns an ask into a quest to <paramref name="to"/> — a proposal accepted, or a receiver
    /// they chose themselves — or the ask's intake session does, in its own words. An ask may
    /// become several quests; each is recorded on it.
    /// </summary>
    /// <param name="draft">The quest's words when they are not the ask's — an intake's, which read the ticket.</param>
    /// <param name="session">
    /// The session publishing, when it is one. Only the ask's OWN intake moves its tier to
    /// <see cref="AskDesk.ByIntake"/> — a session naming itself is a claim, and the ask is what can check it.
    /// </param>
    /// <param name="byAgent">
    /// An agent publishes, whether or not it names a session: a connector always does. A session named is an
    /// agent too, so only a publish with neither carries the person's authority.
    /// </param>
    public async Task<AskOutcome> PublishAsync(
        string id, string to, DateTimeOffset now, CancellationToken ct, AskDraft? draft, string? session, bool byAgent)
    {
        var ask = await asks.FindAsync(id, ct).ConfigureAwait(false);
        if (ask is null)
        {
            return new(AskRefusal.NotFound, $"No ask `#{id.TrimStart('#')}`.", Ask: null);
        }

        if (ask.State == AskState.Closed)
        {
            return new(AskRefusal.Closed, $"Ask `#{ask.Id}` is closed ({ask.Note}) — it becomes nothing more.", ask);
        }

        if (await guard.JudgeAsync(ask, to, session, ct).ConfigureAwait(false) is { } once) return once;

        // An intake's review proposal, judged before anything is published, so a refused one leaves nothing behind.
        var agent = byAgent || !string.IsNullOrWhiteSpace(session);
        var (proposed, unfitProposal) = AskChoices.JudgeProposal(draft, session, agent, now);
        if (unfitProposal is not null) return new(AskRefusal.BadReview, unfitProposal, ask);

        var (kindProposed, unfitKind) = AskChoices.JudgeWorkflowProposal(draft, session, agent, now);
        if (unfitKind is not null) return new(AskRefusal.BadWorkflow, unfitKind, ask);

        var published = await askQuests.PublishAsync(ask, to, now, ct, draft, session, agent).ConfigureAwait(false);
        if (published.Quest is null) return new(AskRefusal.QuestRefused, published.Message, ask);

        var byIntake = session is { Length: > 0 } && string.Equals(session, ask.Intake, StringComparison.Ordinal);
        await asks.RecordPublishedAsync(ask.Id, published.Quest.Id, byIntake ? AskDesk.ByIntake : null, now, ct)
            .ConfigureAwait(false);
        var message = published.Quest.Requirements.Count == 0 ? published.Message + AskPublishGuard.Unrequired : published.Message;
        if (proposed is not null)
        {
            await asks.RecordReviewProposalAsync(ask.Id, proposed with { Quest = published.Quest.Id }, now, ct).ConfigureAwait(false);
            message += $"\n\nProposed review `{proposed.Choice}` for this chain, kept on ask `#{ask.Id}` with your reason: a proposal, which "
                       + "only the person's press applies.";
        }

        if (kindProposed is not null)
        {
            await asks.RecordWorkflowProposalAsync(ask.Id, kindProposed with { Quest = published.Quest.Id }, now, ct).ConfigureAwait(false);
            message += $"\n\nProposed kind `{kindProposed.Kind}` for this chain, kept on ask `#{ask.Id}` with your reason: a proposal, which "
                       + "only the person's press applies.";
        }

        var took = await standings.FindAsync(ask.Id, ct).ConfigureAwait(false) ?? ask;
        return new(AskRefusal.None, message, took, published.Quest);
    }

    /// <summary>The person answers their own ask: closed, with the reason, and final.</summary>
    public async Task<AskOutcome> CloseAsync(string id, string reason, DateTimeOffset now, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return new(AskRefusal.Empty, "Closing an ask needs a reason — it is the record of what became of it.", Ask: null);
        }

        var ask = await asks.FindAsync(id, ct).ConfigureAwait(false);
        if (ask is null) return new(AskRefusal.NotFound, $"No ask `#{id.TrimStart('#')}`.", Ask: null);

        if (ask.State == AskState.Closed)
        {
            return new(AskRefusal.Closed, $"Ask `#{ask.Id}` is already closed ({ask.Note}) — its reason stands.", ask);
        }

        await asks.RecordClosedAsync(ask.Id, reason.Trim(), now, ct).ConfigureAwait(false);
        var closed = await standings.FindAsync(ask.Id, ct).ConfigureAwait(false) ?? ask;
        return new(AskRefusal.None, $"Ask `#{ask.Id}` is closed: {closed.Note}", closed);
    }

    /// <summary>What became of an ask, in a sentence — which tier answered, and what it said.</summary>
    private static string Describe(Ask ask) => ask.State switch
    {
        AskState.Published => $"it became {string.Join(", ", ask.Quests.Select(q => $"`#{q}`"))}.",
        AskState.Done => $"done: it became {string.Join(", ", ask.Quests.Select(q => $"`#{q}`"))}, and every quest it asked has closed.",
        AskState.Closed => $"closed: {ask.Note}",
        // The honest sentence once a harness is on it — "no intake harness ran" would be untrue now.
        _ when ask.Intake is { } intake =>
            $"intake session `{intake}` is answering it: it publishes where the declarations settle it, "
            + "and asks you where they do not.",
        _ when ask.Proposal.Count == 0 =>
            "by declarations only; no intake agent ran — and no repository's declarations share its words. Where Intake is set on this "
            + "machine, the intake publishes it at the driver's next look, or asks you where it cannot tell; where none is, name the "
            + "receiver, or declare what the owner owns and ask again.",
        _ => "by declarations only; no intake agent ran — proposed, best first: "
             + string.Join("; ", ask.Proposal.Select(m => $"`{m.Repository}` ({string.Join(", ", m.Matched)})"))
             + ". Where Intake is set on this machine, the intake publishes it at the driver's next look, so leave it to the intake; "
             + "where none is, a proposal is a person's to accept.",
    };
}
