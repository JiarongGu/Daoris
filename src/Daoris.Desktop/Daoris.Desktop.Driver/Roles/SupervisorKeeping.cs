namespace Daoris.Driver;

/// <summary>
/// Keeping a decision (§3.1, §3.6 <c>held</c> and <c>left</c>): the judgement of an answer or a defer against the park the handler read,
/// then the decision written and kept in the <see cref="SupervisorBook"/> under the session's lock. It holds no state of its own.
/// </summary>
internal sealed class SupervisorKeeping(SupervisorBook book, IDecisionAdmission admission, MachineLog? log)
{
    /// <summary>
    /// Keep a decision for <paramref name="answer"/> on the park the handler read (§3.1's checks marked 1a-i, then the generation rechecked
    /// under the decision's lock), held for <see cref="QuestionSupervisorDecisions.Window"/>. A retry of the same form while it is held
    /// answers kept with the same decision; nothing is kept on any refusal.
    /// </summary>
    public Task<SupervisorKept> KeepAsync(SupervisorAnswer answer, SupervisedPark park, DateTimeOffset now, CancellationToken ct = default) =>
        book.UnderAsync(answer.Session, () =>
        {
            var refused = Judge(answer, park);
            if (refused is not null)
            {
                log?.Info("supervisor.refused", ("why", refused));
                return Task.FromResult(new SupervisorKept(false, null, null, refused));
            }

            // Memory first: a decision already on this question answers for it.
            if (book.Live.Values.FirstOrDefault(each => each.Session == answer.Session && each.Question.Id == answer.Question) is { } held)
            {
                if (held.State == SupervisorState.Held && Same(held, answer)) return Task.FromResult(new SupervisorKept(true, held.Id, held.GoesOnAt, null));
                var why = SupervisorStates.OwnersAgain(held.State) ? SupervisorWhy.Overruled : SupervisorWhy.Answered;
                log?.Info("supervisor.refused", ("why", why));
                return Task.FromResult(new SupervisorKept(false, null, null, why));
            }

            if (book.ClosedWhy(answer.Session, answer.Question) is { } closed)
            {
                log?.Info("supervisor.refused", ("why", closed));
                return Task.FromResult(new SupervisorKept(false, null, null, closed));
            }

            // The generation rechecked at the keeping, under the decision's lock: a revoke between the judgement and here keeps nothing.
            var admitted = admission.Admit(park.Repository);
            if (admitted is not { Why: null, Generation: { } generation } || generation != answer.Generation)
            {
                log?.Info("supervisor.refused", ("why", SupervisorWhy.RoleChanged));
                return Task.FromResult(new SupervisorKept(false, null, null, SupervisorWhy.RoleChanged));
            }

            // SUPERVISOR1e (§6.3): held for the window the effective appointment set, read with its generation.
            var goesOnAt = now + QuestionSupervisorDecisions.WindowOf(admitted);
            var id = Guid.NewGuid().ToString("N")[..12];
            var decision = new SupervisorLiveDecision
            {
                Id = id, Session = answer.Session, Repository = park.Repository, Question = park.Filed!, Choice = answer.Choice,
                Reasons = answer.Reasons, Holder = answer.Holder, Generation = generation, KeptAt = now, GoesOnAt = goesOnAt,
                Written = SupervisorDecisionFiles.DecisionJson(id, answer.Session, answer.Choice, null, answer.Reasons, answer.Holder, park.Filed!, generation, now, goesOnAt, park.Counts),
                StateAt = now,
            };
            Directory.CreateDirectory(book.Files.FolderOf(answer.Session));
            AtomicFile.WriteText(book.Files.FileOf(answer.Session, id), decision.Written);
            book.Append(decision, SupervisorState.Held, now, why: null);
            AtomicFile.WriteText(book.Files.PointerOf(answer.Session), SupervisorDecisionFiles.PointerJson(answer.Question, id));
            book.Live[id] = decision;
            log?.Info("supervisor.answered", ("session", answer.Session), ("question", answer.Question), ("holder", answer.Holder.Kind),
                ("reasons", answer.Reasons.Count));
            return Task.FromResult(new SupervisorKept(true, id, decision.GoesOnAt, null));
        }, ct);

    /// <summary>
    /// Keep a defer for <paramref name="defer"/> on the park the handler read: a decision of state <c>left</c>, final at once, its bound
    /// and reasons kept for the owner's card and nothing ever said to the session. Its checks are the form's, the park's and the
    /// generation's; a go-ahead, the owner's words or the question's mark refuse none, since leaving it to the owner is always the safe
    /// side. A retry of the same defer answers kept with the same decision; any other act on a decided question is refused.
    /// </summary>
    public Task<SupervisorKept> LeaveAsync(SupervisorDefer defer, SupervisedPark park, DateTimeOffset now, CancellationToken ct = default) =>
        book.UnderAsync(defer.Session, () =>
        {
            var refused = Refused(defer.Reasons, SupervisorBounds.Known(defer.Bound), park) ?? Parked(defer.Question, park);
            if (refused is null && book.Live.Values.FirstOrDefault(each => each.Session == defer.Session && each.Question.Id == defer.Question) is { } held)
            {
                if (held.State == SupervisorState.Left && held.Bound == defer.Bound && held.Generation == defer.Generation
                    && held.Holder == defer.Holder && held.Reasons.SequenceEqual(defer.Reasons))
                {
                    return Task.FromResult(new SupervisorKept(true, held.Id, null, null));
                }

                refused = SupervisorStates.OwnersAgain(held.State) ? SupervisorWhy.Overruled : SupervisorWhy.Answered;
            }

            refused ??= book.ClosedWhy(defer.Session, defer.Question);
            if (refused is null && (admission.Admit(park.Repository) is not { Why: null, Generation: { } effective } || effective != defer.Generation))
            {
                refused = SupervisorWhy.RoleChanged;
            }

            if (refused is not null)
            {
                log?.Info("supervisor.refused", ("why", refused));
                return Task.FromResult(new SupervisorKept(false, null, null, refused));
            }

            var id = Guid.NewGuid().ToString("N")[..12];
            var decision = new SupervisorLiveDecision
            {
                Id = id, Session = defer.Session, Repository = park.Repository, Question = park.Filed!, Choice = "", Bound = defer.Bound,
                Reasons = defer.Reasons, Holder = defer.Holder, Generation = defer.Generation, KeptAt = now, GoesOnAt = now,
                Written = SupervisorDecisionFiles.DecisionJson(id, defer.Session, null, defer.Bound, defer.Reasons, defer.Holder, park.Filed!, defer.Generation, now, now, park.Counts),
                StateAt = now,
            };
            Directory.CreateDirectory(book.Files.FolderOf(defer.Session));
            AtomicFile.WriteText(book.Files.FileOf(defer.Session, id), decision.Written);
            book.Append(decision, SupervisorState.Left, now, why: null);
            AtomicFile.WriteText(book.Files.PointerOf(defer.Session), SupervisorDecisionFiles.PointerJson(defer.Question, id));
            book.Live[id] = decision;
            log?.Info("supervisor.left", ("session", defer.Session), ("bound", defer.Bound), ("reasons", defer.Reasons.Count));
            return Task.FromResult(new SupervisorKept(true, id, null, null));
        }, ct);

    /// <summary>§3.1's refusals the park and the form answer, before memory is read; null where none applies.</summary>
    private static string? Judge(SupervisorAnswer answer, SupervisedPark park)
    {
        if ((Refused(answer.Reasons, answer.Choice.Length > 0, park) ?? Parked(answer.Question, park)) is { } refused) return refused;
        var filed = park.Filed!;
        // The agent's mark, any at all, makes the question the owner's; checked again at the release.
        if (filed.Owner is not null) return SupervisorWhy.OwnerQuestion;
        if (!filed.Choices.Any(choice => string.Equals(choice.Label, answer.Choice, StringComparison.Ordinal))) return SupervisorWhy.NotOfferedChoice;
        if (park.GoAheadUnread) return SupervisorWhy.GoAheadUnread;
        if (park.GoAheadOpen) return SupervisorWhy.GoAheadOpen;
        if (park.OwnerWaits) return SupervisorWhy.OwnerAnswered;
        // A word that would go on past a hold is never kept only to be held behind it.
        return park.AtBound ? SupervisorWhy.SupervisorBound : park.RuleHolds ? SupervisorWhy.RuleHolds : null;
    }

    /// <summary>
    /// The form's refusals both acts share: every reason naming its source, a count it cites one of those the park read (CONTEXTDRIVE1i),
    /// and their count and bounds.
    /// </summary>
    private static string? Refused(IReadOnlyList<SupervisorReason> reasons, bool named, SupervisedPark park)
    {
        // D135 point 4: a reason with no source is refused by name; Daoris checks that one is named, never what it says.
        if (reasons.Any(reason => string.IsNullOrWhiteSpace(reason.Source) || CountTexts.NamesNone(reason.Source, park.Counts))) return SupervisorWhy.NoSource;
        return reasons.Count is 0 or > QuestionSupervisorDecisions.MostReasons || !named
               || reasons.Any(reason => reason.Says.Trim().Length is 0 or > QuestionSupervisorDecisions.MaxSays || reason.Source.Length > QuestionSupervisorDecisions.MaxSource)
            ? SupervisorWhy.Form
            : null;
    }

    /// <summary>The park's refusals both acts share: parked here, holding a quest, and its question on file the one named.</summary>
    private static string? Parked(string question, SupervisedPark park) =>
        !park.Parked ? SupervisorWhy.NotParked
        : !park.HoldsQuest ? SupervisorWhy.NoQuest
        : park.Filed is not { } filed || filed.Id != question ? SupervisorWhy.QuestionChanged
        : null;

    private static bool Same(SupervisorLiveDecision held, SupervisorAnswer answer) =>
        held.Choice == answer.Choice && held.Generation == answer.Generation && held.Holder == answer.Holder
        && held.Reasons.SequenceEqual(answer.Reasons);
}
