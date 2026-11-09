namespace Daoris.Knowledge;

/// <summary>Publishing a quest: who may be addressed, what the ask carries, and what is kept — judged in order, before anything is written.</summary>
internal sealed class QuestPublishing(
    KnowledgeService service, QuestStore quests, IRemotes? remotes, AskStore? asks, QuestReviews reviews, QuestCarry carry,
    QuestRequirementJudge requirements, QuestWorkflowKinds kinds)
{
    /// <summary>
    /// Publish a quest to another repository, or to lanes of a repository (`repository:lane+lane`),
    /// its own included. Refuses a self-addressed quest that names no lane, a target nothing
    /// could answer, and a lane the target does not declare; warns when the target has not adopted, or
    /// has declared nothing about itself. It is published HERE,
    /// whoever the receiver is, and a joined receiver's quest reaches the remote on the next sync.
    /// What it carries is judged before anything is written anywhere, and its files are kept HERE —
    /// under this machine's home — wherever the record travels.
    /// </summary>
    public async Task<QuestPublishOutcome> PublishAsync(QuestAsk ask, DateTimeOffset now, CancellationToken ct)
    {
        // `repository:lane+lane` is split once, here: from now on `to` is the repository,
        // and the lanes ride beside it to the store.
        var address = QuestAddress.Parse(ask.To);
        var (from, to, title, body) = (ask.From, address.Repository, ask.Title, ask.Body);

        // Unscoped deliberately: the clause below needs BOTH sides' workspaces, and a registry already
        // narrowed to one would answer the cross-workspace case as "no such repository" — a refusal
        // that sends the asker looking for a name they can see perfectly well.
        var registered = await service.RegistryAsync(ct: ct).ConfigureAwait(false);
        var sender = registered.Named(from);

        // Work for another lane is work for another session, so a repository may ask one of its own
        // lanes. To itself whole is still refused, and the refusal says
        // how it asks itself: its lanes, or the file that would give it some.
        if (!address.Named && string.Equals(from, to, StringComparison.OrdinalIgnoreCase))
        {
            const string whole = "That is the repository you are in — a quest to it whole is work for someone else.";
            var declared = sender?.DeclaredLanes ?? [];
            return new(
                QuestPublishRefusal.SelfAddressed,
                declared.Count > 0
                    ? $"{whole} Its own lanes are how it asks itself, each lane's quest work for another session: "
                      + $"{QuestLanes.LaneList(declared)}. {QuestLanes.HowToAddress(sender!.Repository, declared)}"
                    : $"{whole} Use its own backlog. {QuestLanes.NoLanesWay(sender?.Repository ?? to)}",
                Quest: null, Addressable: []);
        }
        // A registered sender's row decides its circle. A sender with none — an ask — names
        // its own, and silence places it where an unregistered sender always is.
        var home = sender?.InWorkspace ?? Workspaces.Normalize(ask.Workspace);

        // Who can be asked is who shares the asker's circle. A refusal that listed the whole machine
        // would be offering repositories this one may not address.
        var addressable = registered
            .Where(r => r.Addressable && Workspaces.Same(r.InWorkspace, home))
            .Select(r => r.Repository)
            .ToList();
        var target = registered.Named(to);

        if (target is null || !target.Addressable)
        {
            // A quest nothing could answer would sit in a queue that is never opened. Saying so now
            // beats letting it look delivered.
            return new(
                QuestPublishRefusal.NotAddressable,
                (target is null
                    ? $"`{to}` is not registered here, so nothing could see a quest to it."
                    : $"`{to}` has not adopted Daoris and has no root on this machine, so it has no client "
                      + "to see a quest and no tree a session could be started in.")
                + $" Addressable: {string.Join(", ", addressable)}.",
                Quest: null, addressable);
        }

        to = target.Repository; // the registry ignored case; every key below reads the registered spelling

        if (!Workspaces.Same(target.InWorkspace, home))
        {
            // The workspace is the unit of sharing, so this is not a permission failure — it is
            // two circles that were never joined. The sentence names BOTH sides, because "no" that
            // does not say which side is where leaves the asker with the question it should settle:
            // the answer is either to re-wire one of them, or to carry the request by hand.
            return new(
                QuestPublishRefusal.CrossWorkspace,
                $"`{from}` is in workspace `{home}` and `{target.Repository}` is in `{target.InWorkspace}` — "
                + "a quest does not cross workspaces, because the workspace is the unit of sharing. "
                + $"Either wire one of them to the other's workspace on this machine, or carry the request "
                + $"across yourself. Addressable from `{home}`: {string.Join(", ", addressable)}.",
                Quest: null, addressable);
        }

        // The registration's lanes are what an address is judged by: the line's file is
        // the authority for what runs, and this is what askers read.
        var (lanes, unfitLane) = QuestLanes.Judge(address, target);
        if (unfitLane is not null)
        {
            return new(QuestPublishRefusal.UnknownLane, unfitLane, Quest: null, addressable);
        }

        // What it carries is judged here, before any door writes anything: a refused ask must leave
        // nothing behind — not a record, and not a file no record names.
        var carried = carry.Judge(ask);
        if (carried.Refusal is { } unfit)
        {
            return new(unfit, carried.Message, Quest: null, addressable);
        }

        // The short title is the publisher's words, judged here so every door refuses the same one.
        var (shortTitle, unfitShort) = QuestTitles.Judge(ask.Short);
        if (unfitShort is not null)
        {
            return new(QuestPublishRefusal.BadShortTitle, unfitShort, Quest: null, addressable);
        }

        var (weight, unfitWeight) = QuestWeights.Judge(ask.Weight);
        if (unfitWeight is not null)
        {
            return new(QuestPublishRefusal.BadWeight, unfitWeight, Quest: null, addressable);
        }

        var (paths, unfitPaths) = QuestPaths.Judge(ask.Paths);
        if (unfitPaths is not null)
        {
            return new(QuestPublishRefusal.BadPaths, unfitPaths, Quest: null, addressable);
        }

        var (request, unfitRequest) = QuestRequests.Judge(ask.Request);
        if (unfitRequest is not null)
        {
            return new(QuestPublishRefusal.BadRequest, unfitRequest, Quest: null, addressable);
        }

        // A chain is judged now, while the person or the intake composing it can still act on the
        // answer — not at a close nobody is watching. Whether this circle shares with a team
        // is this machine's wiring; whether a receiver is shared is its registration.
        var circleWired = remotes?.For(home) is not null;
        if (QuestChains.Judge(ask, to, registered, home, circleWired, circleWired && target.Joined, addressable) is { } unfitChain)
        {
            return new(QuestPublishRefusal.BadChain, unfitChain, Quest: null, addressable);
        }

        // The chain's review choice: its shape, then, from an agent, the person's words it quotes; an
        // agent's `off` is a proposal for the person's press, never the chain's choice.
        var (review, proposedOff, unfitReview) = await reviews.JudgeReviewAsync(from, ask, now, ct).ConfigureAwait(false);
        if (unfitReview is { } refusedReview)
        {
            return new(refusedReview.Refusal, refusedReview.Message, Quest: null, addressable);
        }

        // An intake's kind proposal: its reason must quote the person's words on the ask. It is kept by the desk.
        if (await kinds.JudgeProposalAsync(from, ask, ct).ConfigureAwait(false) is { } unfitKind)
        {
            return new(unfitKind.Refusal, unfitKind.Message, Quest: null, addressable);
        }

        // What the person requires is judged against what they said, here, so every door
        // refuses the same quote: its shape first, then whether they said it.
        var (required, unfitRequirement) = QuestRequirementJudge.JudgeShape(ask.Requirements);
        if (unfitRequirement is not null)
        {
            return new(QuestPublishRefusal.BadRequirement, unfitRequirement, Quest: null, addressable);
        }

        if (await requirements.JudgeQuotesAsync(from, required, ct).ConfigureAwait(false) is { } unquoted)
        {
            return new(unquoted.Refusal, unquoted.Message, Quest: null, addressable);
        }

        // A request key makes a cleared quest's id again, which the remote still holds closed and would refuse on every pass, so
        // it is answered as a closed quest is. Plain words take the next id (`~again<n>`) instead.
        // Where the quest simply went (no remote ever numbered it), nothing is marked and the words make it again.
        if (request is not null && await quests.ForgottenIdAsync(from, to, title, lanes, ct, request).ConfigureAwait(false) is { } cleared)
        {
            return new(
                QuestPublishRefusal.Cleared,
                $"Quest `#{cleared}` was cleared from this machine; the remote for `{home}` holds it closed. A new ask is a new title.",
                Quest: null, addressable);
        }

        var (quest, held) = await quests.PublishHeldAsync(
            from, to, title, body, now, home, carried.Links, carried.Attachments, ask.Then, ct,
            ask.PublishedBy, lanes, required, shortTitle, review, setUpIn: null, parent: null, weight, paths, request)
            .ConfigureAwait(false);

        var caution = !target.Adopted
            // Registered is addressable; adopted is disciplined. Said at publish, because it is
            // the one fact that decides whether the quest is ever answered: a person working there by
            // hand has no connector, and a machine driving over the pipe door holds it.
            ? $"\n\n⚠ `{target.Repository}` has not adopted Daoris, so only a session the driver starts "
              + "in it over the protocol door can answer this — it is handed a connector there, while "
              + "a person working in it by hand has none to see the quest."
            : target.Registered
                ? ""
                : $"\n\n⚠ `{target.Repository}` has not declared what it owns or accepts, so this may not be "
                  + "its problem. Worth checking before you rely on it.";

        // Said of the quest as it stands: a publish already held answers with the requirements it is kept with.
        var carrying = quest.Requirements.Count switch
        {
            0 => "",
            1 => " It carries 1 requirement, in the person's own words.",
            var count => $" It carries {count} requirements, each in the person's own words.",
        };
        var reviewed = quest.Review is { } chosen ? $" Its chain's review choice is `{chosen.Choice}`, and each step inherits it." : "";
        if (proposedOff is not null && asks is not null && AskDesk.AskOf(from) is { } offAsk)
        {
            await asks.RecordReviewProposalAsync(offAsk, proposedOff with { Quest = quest.Id }, now, ct).ConfigureAwait(false);
            reviewed += $" It sets no review choice: `off` on the person's words is kept on ask `#{offAsk}` as a proposal, since `off` "
                        + "is only the person's own press, and the review stands as the ask or the repository sets it until they press it.";
        }

        var said = held
            ? $"Quest `#{quest.Id}` to `{QuestAddress.Spell(quest.To, quest.Lanes)}` was already published and is still {quest.Status}: "
              + "this is that quest, answered again as a retry, and nothing new was asked. Its words stay as they were first published."
            : $"Published quest `#{quest.Id}` to `{QuestAddress.Spell(quest.To, quest.Lanes)}` — {quest.Status}.";
        return new(
            QuestPublishRefusal.None,
            $"{said}{carrying}{reviewed}{caution}\n\n"
            + "It is held by the service, not written into that repository. Its agent will see it and "
            + "decide. Do not make the change yourself."
            + await carry.KeepAsync(quest, ask, carried, ct).ConfigureAwait(false),
            quest, addressable);
    }
}
