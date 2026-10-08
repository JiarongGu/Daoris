namespace Daoris.Knowledge;

/// <summary>
/// What a set-up posted at its door carries (REVIEWENV1b, design §2.6, §3.3): the commit Daoris read, how the environment is
/// reached, and either the session whose said set-ups it posts or the person's own set-up.
/// </summary>
/// <param name="Commit">The commit read: the step's tree <c>HEAD</c> for a session's, the chain's tip for the person's own.</param>
/// <param name="Kind">The environment's kind, as the review rule declares it: <c>local</c> or <c>deployed</c>.</param>
/// <param name="Session">The session whose said set-ups to post; null for the person's own.</param>
/// <param name="Look">The person's own: where to look.</param>
/// <param name="Shows">The person's own: their words on what it shows, where they give any.</param>
/// <param name="Again">The person's own: how to show it again, where they give it.</param>
public sealed record QuestSetUpPost(
    string? Commit, string? Kind, string? Session = null, string? Look = null, string? Shows = null, string? Again = null);

/// <summary>
/// The review on the record (REVIEWENV1b, D154; the review environment design §1.4–§1.5, §2.1, §2.5–§2.6, §3.5): the
/// chain's choice judged at publish, a set-up step published after its chain's close, a set-up posted with the commit Daoris
/// read, and the person's verdict. The gate that reads them, and the step's driving, are the driver's (REVIEWENV1c).
/// </summary>
public sealed partial class QuestExchange
{
    /// <summary>
    /// A chain's review choice as a quest keeps it, or why not (REVIEWENV1b, design §1.5): <c>off</c>, <c>on</c> or an
    /// environment's name, the words trimmed and bounded. The person's own door sets it with any words they give. A session
    /// sets it only on the person's words, which must stand in what they said on the ask that asks the quest, checked as a
    /// requirement's quote is (DRIFT1c) — otherwise it proposes one, which only the person's press applies.
    /// </summary>
    private async Task<(QuestReview? Review, (QuestPublishRefusal Refusal, string Message)? Refused)> JudgeReviewAsync(
        string from, QuestAsk ask, CancellationToken ct)
    {
        if (ask.Review is not { } given) return (null, null);

        var choice = given.Choice?.Trim() ?? "";
        if (Reviews.JudgeChoice(choice) is { } unfit) return (null, (QuestPublishRefusal.BadReview, $"The chain's {unfit}. Nothing was published."));

        var words = string.IsNullOrWhiteSpace(given.Words) ? null : given.Words.Trim();
        if (words is { Length: > Reviews.WordsLimit })
        {
            return (null, (QuestPublishRefusal.BadReview,
                $"The words a review choice is set on are at most {Reviews.WordsLimit} characters; these are {words.Length}. Nothing was published."));
        }

        // The person's own door: their choice, with whatever words they gave.
        if (ask.PublishedBy is null) return (new QuestReview(choice, words), null);

        const string Instead = "Without the person's words, propose the choice with your reason instead; only their press applies it.";
        if (words is null)
        {
            return (null, (QuestPublishRefusal.BadReview,
                $"A session sets a chain's review choice only on the person's own words: quote them in `words`, copied exactly from "
                + $"what they asked or said since. {Instead} Nothing was published."));
        }

        if (AskDesk.AskOf(from) is not { } askId || asks is null || await asks.FindAsync(askId, ct).ConfigureAwait(false) is not { } held)
        {
            return (null, (QuestPublishRefusal.BadReview,
                $"A review choice quotes the person's words on the ask a quest is asked by, and this quest is asked by `{from}`, "
                + "on no ask held here — so there are no words of theirs to quote. Nothing was published."));
        }

        if (!held.Words.Any(word => QuestRequirement.QuotedIn(words, word.Text)))
        {
            return (null, (QuestPublishRefusal.NotQuoted,
                $"The review choice `{choice}` quotes words the person never said on ask `#{held.Id}`: \"{Clip(words)}\". A choice "
                + $"is set on their own words, verbatim (whitespace and case aside). {Instead} Nothing was published."));
        }

        return (new QuestReview(choice, words), null);
    }

    /// <summary>
    /// Why a set-up step may not close done yet, or null when it may, or is no set-up step (design §2.6): a set-up step's done
    /// always has a set-up, a session's said through its connector or one already posted on the quest.
    /// </summary>
    private async Task<string?> NotShownAsync(Quest quest, CancellationToken ct)
    {
        if (quest.SetUpIn is null || quest.SetUps.Count > 0) return null;
        if (sessions is not null && await sessions.SetUpsSaidForAsync(quest.Id, ct).ConfigureAwait(false) > 0) return null;
        return $"Set-up step `#{quest.Id}` closes done once it has said what it showed in `{quest.SetUpIn}`: say the set-up first "
               + "(review_ready: where to look, what it shows and how to show it again), then close it. The person records their own "
               + "set-up at the quest's set-up door. Nothing moved.";
    }

    /// <summary>
    /// What a set-up step's review holds, said beneath a done, a yes or a verdict found (design §2.6): the step waits for the
    /// person's review in its environment, and what follows it waits until they say it is reviewed. Nothing for a quest whose
    /// review waits on nobody.
    /// </summary>
    private async Task<string> ReviewWaitsAsync(Quest quest, CancellationToken ct) =>
        QuestReviewing.Waits(quest)
            ? $" It waits for the person's review in `{quest.SetUpIn}`: "
              + $"{string.Join("; and ", await HoldsAsync(quest, "they say it is reviewed or skip the review", ct).ConfigureAwait(false))}."
            : "";

    /// <summary>
    /// Post a set-up on a set-up step (REVIEWENV1b, D154 point 9; design §2.6, §3.3): every set-up a session said and its
    /// driver has not yet posted, each with the commit the driver read from the step's tree, or the person's own set-up with
    /// the commit Daoris read at their press. The commit is read, never reported: no connector tool reaches this door.
    /// </summary>
    /// <remarks>
    /// <para><b>The driver's door, and the person's</b>, a local host's alone, as the evidence door is: the tree is on the machine
    /// that holds it, and the set-up travels from there as an operation (D68).</para>
    ///
    /// <para><b>Once each.</b> A set-up kept is marked posted on the session's record, and the same set-up posted again is no
    /// move, so the driver may post at every session end and every later turn.</para>
    /// </remarks>
    public async Task<QuestRespondOutcome> PostSetUpAsync(string id, QuestSetUpPost post, DateTimeOffset now, CancellationToken ct = default)
    {
        var name = id.TrimStart('#');
        if (await quests.FindAsync(name, ct).ConfigureAwait(false) is not { } quest)
        {
            return new(QuestRespondRefusal.NotFound, $"No quest `#{name}`. Ids come from `quest_list`.", Quest: null);
        }

        if (quest.SetUpIn is null)
        {
            return new(QuestRespondRefusal.NotSetUpStep,
                $"Quest `#{quest.Id}` is no set-up step: only a set-up step shows work for the person's review. Nothing was kept.", Quest: null);
        }

        var commit = post.Commit?.Trim().ToLowerInvariant() ?? "";
        string? unfit =
            !QuestEvidenceCodes.IsObjectId(commit) ? $"A set-up names the commit Daoris read by its full id (40 or 64 hex characters), and `{Clip(post.Commit ?? "")}` is not one."
            : !Reviews.Kinds.Contains(post.Kind ?? "") ? $"A set-up says the environment's kind, `local` or `deployed`, and `{Clip(post.Kind ?? "")}` is neither."
            : (post.Session is null) == (post.Look is null) ? "A set-up is posted for a session, whose said set-ups it posts, or as the person's own, with where to look: name exactly one."
            : null;
        if (unfit is not null) return new(QuestRespondRefusal.BadSetUp, unfit + " Nothing was kept.", Quest: null);

        var local = post.Kind == "local";
        return post.Session is { } session
            ? await PostSaidAsync(quest, session.Trim(), commit, local, now, ct).ConfigureAwait(false)
            : await PostOwnAsync(quest, post, commit, local, now, ct).ConfigureAwait(false);
    }

    /// <summary>The person's own set-up (design §3.3, <i>I set it up myself…</i>): where to look, their words, and the commit.</summary>
    private async Task<QuestRespondOutcome> PostOwnAsync(
        Quest quest, QuestSetUpPost post, string commit, bool local, DateTimeOffset now, CancellationToken ct)
    {
        var setUp = new QuestSetUp(commit)
        {
            Look = post.Look?.Trim(),
            Shows = string.IsNullOrWhiteSpace(post.Shows) ? null : post.Shows.Trim(),
            Again = string.IsNullOrWhiteSpace(post.Again) ? null : post.Again.Trim(),
            Local = local,
        };
        if (Reviews.JudgeSetUp(setUp) is { } unfit) return new(QuestRespondRefusal.BadSetUp, $"{Capital(unfit)}. Nothing was kept.", Quest: null);

        var move = await quests.SetUpAsync(quest.Id, setUp, now, ct).ConfigureAwait(false);
        if (SetUpRefused(quest, move) is { } refused) return refused;
        return new(QuestRespondRefusal.None,
            (move.Moved ? $"Kept your own set-up on set-up step `#{quest.Id}` at `{Short(commit)}`: <{setUp.Look}>."
                        : $"Set-up step `#{quest.Id}` already holds this set-up.")
            + await ReviewWaitsAsync(move.Quest!, ct).ConfigureAwait(false),
            move.Quest);
    }

    /// <summary>A session's said set-ups, each posted with the commit its driver read and marked posted on its record.</summary>
    private async Task<QuestRespondOutcome> PostSaidAsync(
        Quest quest, string session, string commit, bool local, DateTimeOffset now, CancellationToken ct)
    {
        if (sessions is null || await sessions.FindAsync(session, ct).ConfigureAwait(false) is not { Origin: null } record
            || !string.Equals(record.Quest, quest.Id, StringComparison.Ordinal))
        {
            return new(QuestRespondRefusal.NotFound,
                $"No session `{Clip(session)}` of this machine's works set-up step `#{quest.Id}`. Nothing was kept.", Quest: null);
        }

        // Read, posted and marked in one transaction, so two posts at once never both post one set-up (REV3).
        var (posted, refused, standing) = await sessions.ExclusiveAsync<(int, QuestRespondOutcome?, Quest)>(async inside =>
        {
            var review = await sessions.ReviewOfAsync(record.Id, inside).ConfigureAwait(false);
            var count = 0;
            var stands = quest;
            var said = review.Said.ToList();
            foreach (var (each, index) in review.Said.Select((each, index) => (each, index)).Where(each => !each.each.Posted))
            {
                var setUp = new QuestSetUp(commit)
                {
                    Look = each.Look, Shows = each.Shows, Again = each.Again, Served = each.Served, Run = each.Run,
                    Session = record.Id, Local = local,
                };
                if (Reviews.JudgeSetUp(setUp) is not null) continue;

                var move = await quests.SetUpAsync(quest.Id, setUp, now, inside).ConfigureAwait(false);
                if (SetUpRefused(quest, move) is { } stop) return (count, stop, stands);
                stands = move.Quest ?? stands;
                if (move.Moved) count++;
                said[index] = each with { Posted = true };
            }

            await sessions.KeepReviewAsync(record.Id, review with { Said = said }, inside).ConfigureAwait(false);
            return (count, null, stands);
        }, ct).ConfigureAwait(false);

        if (refused is not null) return refused;
        return new(QuestRespondRefusal.None,
            (posted switch
            {
                0 => $"Session `{record.Id}` said no set-up on set-up step `#{quest.Id}` that is not posted already: nothing new.",
                1 => $"Posted session `{record.Id}`'s set-up on set-up step `#{quest.Id}` at `{Short(commit)}`.",
                _ => $"Posted session `{record.Id}`'s {posted} set-ups on set-up step `#{quest.Id}` at `{Short(commit)}`.",
            })
            + await ReviewWaitsAsync(standing, ct).ConfigureAwait(false),
            standing);
    }

    /// <summary>Why a set-up the store did not keep is refused, or null where it was kept or was already there.</summary>
    private static QuestRespondOutcome? SetUpRefused(Quest quest, QuestMove move) =>
        move.Quest is null ? new(QuestRespondRefusal.NotFound, $"No quest `#{quest.Id}`. Ids come from `quest_list`.", Quest: null)
        : move.ClaimLost ? new(QuestRespondRefusal.AlreadyTaken, LostTake(quest.Id, "so its set-up is not this machine's to post. Nothing was kept."), Quest: null)
        : move.Quest.Status == QuestStatus.Declined
            ? new(QuestRespondRefusal.Closed, $"Set-up step `#{quest.Id}` is Declined: nothing is shown on it now. Nothing was kept.", Quest: null)
        : null;

    /// <summary>
    /// The person's verdict on a set-up step (REVIEWENV1b, D154 point 8; design §3.3–§3.6): <c>reviewed</c> or <c>not-yet</c>
    /// on its newest set-up, or <c>skipped</c> for the work, on a set-up step or on the chain's last quest in a repository with
    /// none. A <c>reviewed</c> or a skip lets go what the step's review held, publishing the chain's next step; a
    /// <c>not-yet</c> keeps it held, its words kept for the step's session. Their words are kept on the ask, each as its own
    /// kind (design §3.5).
    /// </summary>
    /// <remarks>
    /// <b>The person's alone</b>: no connector tool and no Ask Daoris card reaches it, since the verdict is a look only they
    /// have taken. A local host's door, as the yes is; it travels from there as an operation (D68).
    /// </remarks>
    /// <param name="setUp">The set-up a <c>reviewed</c> or a <c>not-yet</c> answers; null for the newest.</param>
    public async Task<QuestRespondOutcome> ReviewAsync(
        string id, string? verdict, string? words, QuestOperationRef? setUp, DateTimeOffset now, CancellationToken ct = default)
    {
        var name = id.TrimStart('#');
        if (await quests.FindAsync(name, ct).ConfigureAwait(false) is not { } quest)
        {
            return new(QuestRespondRefusal.NotFound, $"No quest `#{name}`. Ids come from `quest_list`.", Quest: null);
        }

        var said = verdict?.Trim().ToLowerInvariant() ?? "";
        var kept = string.IsNullOrWhiteSpace(words) ? null : words.Trim();
        if (!Reviews.Verdicts.Contains(said))
        {
            return new(QuestRespondRefusal.BadReviewVerdict,
                $"A verdict is `{Reviews.Reviewed}`, `{Reviews.NotYet}` or `{Reviews.Skipped}`, and `{Clip(verdict ?? "")}` is none of them. Nothing was kept.",
                Quest: null);
        }

        if (kept is { Length: > Reviews.WordsLimit })
        {
            return new(QuestRespondRefusal.BadReviewVerdict,
                $"Your words are at most {Reviews.WordsLimit} characters; these are {kept.Length}. Nothing was kept.", Quest: null);
        }

        var (given, refused) = said == Reviews.Skipped ? Skip(quest, kept) : Look(quest, said, kept, setUp);
        if (refused is not null) return refused;

        var move = await quests.VerdictAsync(quest.Id, given!, now, ct).ConfigureAwait(false);
        if (move.Quest is null) return new(QuestRespondRefusal.NotFound, $"No quest `#{quest.Id}`. Ids come from `quest_list`.", Quest: null);
        if (!move.Moved)
        {
            return new(QuestRespondRefusal.ReviewRefused,
                $"Quest `#{quest.Id}`'s review moved while this verdict was given: it no longer takes a `{said}`. Nothing was kept.", Quest: null);
        }

        var onAsk = kept is not null && await KeepWordAsync(move.Quest, said, kept, now, ct).ConfigureAwait(false) is { } askId
            ? $" Your words are kept on ask `#{askId}`, as the ask's other words are."
            : "";
        return new(QuestRespondRefusal.None, await VerdictSaidAsync(move, given!, ct).ConfigureAwait(false) + onAsk, move.Quest);
    }

    /// <summary>A skip, or why the review's state refuses one: once, and on a set-up step only before its <c>reviewed</c>.</summary>
    private static (QuestReviewVerdict? Verdict, QuestRespondOutcome? Refused) Skip(Quest quest, string? words)
    {
        if (quest.Status == QuestStatus.Declined)
        {
            return (null, new(QuestRespondRefusal.Closed, $"Quest `#{quest.Id}` is Declined: its work lands nowhere, so there is no review to skip.", Quest: null));
        }

        if (QuestReviewing.Skipped(quest))
        {
            return (null, new(QuestRespondRefusal.ReviewRefused, $"Quest `#{quest.Id}`'s review was already skipped: nothing waits for it.", Quest: null));
        }

        if (quest.SetUpIn is not null && QuestReviewing.Reviewed(quest))
        {
            return (null, new(QuestRespondRefusal.ReviewRefused,
                $"Set-up step `#{quest.Id}` was already reviewed: nothing waits for a skip.", Quest: null));
        }

        return (new QuestReviewVerdict(Reviews.Skipped) { Words = words }, null);
    }

    /// <summary>
    /// A <c>reviewed</c> or a <c>not-yet</c> on a set-up step's newest set-up, or why the review's state refuses it: nothing
    /// shown yet, a set-up that is not the newest, or a review already given or skipped.
    /// </summary>
    private static (QuestReviewVerdict? Verdict, QuestRespondOutcome? Refused) Look(Quest quest, string said, string? words, QuestOperationRef? named)
    {
        QuestRespondOutcome Refuse(string why) => new(QuestRespondRefusal.ReviewRefused, why + " Nothing was kept.", Quest: null);

        if (quest.SetUpIn is null)
        {
            return (null, new(QuestRespondRefusal.NotSetUpStep,
                $"Quest `#{quest.Id}` is no set-up step, so nothing of it was shown to review: `{said}` answers a set-up. Skip the "
                + "review of its work, or set it up for review after it.", Quest: null));
        }

        if (said == Reviews.NotYet && words is null)
        {
            return (null, new(QuestRespondRefusal.BadReviewVerdict,
                "A `not-yet` says what is not right yet, in your words: they are what the step's session acts on. Nothing was kept.", Quest: null));
        }

        if (QuestReviewing.Newest(quest) is not { } newest) return (null, Refuse($"Set-up step `#{quest.Id}` has shown nothing yet in `{quest.SetUpIn}`."));
        var answered = named is null ? newest : quest.SetUps.FirstOrDefault(each => each.Ref == named);
        if (answered is null) return (null, Refuse($"Set-up step `#{quest.Id}` holds no set-up `{named!.Machine}`/{named.Sequence}."));
        if (answered.Ref != newest.Ref)
        {
            return (null, Refuse($"Set-up step `#{quest.Id}` was shown again since that set-up, at `{Short(newest.Commit)}`: review the newest, "
                                 + "since what it holds is the work that lands."));
        }

        if (QuestReviewing.Skipped(quest)) return (null, Refuse($"Set-up step `#{quest.Id}`'s review was skipped: nothing waits for it."));
        if (QuestReviewing.Reviewed(quest)) return (null, Refuse($"Set-up step `#{quest.Id}`'s newest set-up was already reviewed."));
        return (new QuestReviewVerdict(said) { SetUp = newest.Ref, Commit = newest.Commit, Words = words }, null);
    }

    /// <summary>What a kept verdict means, said once here for every door.</summary>
    private async Task<string> VerdictSaidAsync(QuestMove move, QuestReviewVerdict verdict, CancellationToken ct)
    {
        var quest = move.Quest!;
        IReadOnlyList<Quest> waiting = quest.Held ? [] : await quests.WaitingOnAsync(quest.Id, ct).ConfigureAwait(false);
        var resumes = waiting.Count == 0
            ? ""
            : $"\n\n{Listed(waiting)} {(waiting.Count == 1 ? "waits" : "wait")} on it, and {(waiting.Count == 1 ? "resumes" : "resume")} "
              + "at the driver's next look.";
        var stillHeld = quest.Hold switch
        {
            QuestHold.Departed => $" It still departs from a requirement, so it waits for your yes: `daoris-driver quest accept {quest.Id}`.",
            QuestHold.EvidenceUnread or QuestHold.EvidenceMissing => " Its evidence still holds it, until it is found or you accept the done as it stands.",
            _ => "",
        };

        return verdict.Said switch
        {
            Reviews.Reviewed =>
                $"Reviewed set-up step `#{quest.Id}` in `{quest.SetUpIn}` at `{Short(verdict.Commit!)}`"
                + (quest.Held ? "." + stillHeld : ": what its review held goes on.") + resumes + Then(move.FollowUp),
            Reviews.NotYet =>
                $"Not yet, on set-up step `#{quest.Id}`'s set-up at `{Short(verdict.Commit!)}`: your words are kept on the quest. It "
                + $"stays held for your review of the next set-up shown in `{quest.SetUpIn}`.",
            _ => $"Skipped the review of quest `#{quest.Id}`'s work"
                 + (quest.SetUpIn is null ? ": its landing waits for no review." : quest.Held ? "." + stillHeld : ": what its review held goes on.")
                 + resumes + Then(move.FollowUp),
        };
    }

    /// <summary>The person's words on a verdict, kept on the ask the quest was asked by as their own kind; the ask's id, or null for none.</summary>
    private async Task<string?> KeepWordAsync(Quest quest, string said, string words, DateTimeOffset now, CancellationToken ct)
    {
        if (asks is null || AskDesk.AskOf(quest.From) is not { } askId) return null;
        var kind = said switch
        {
            Reviews.Reviewed => AskWordKind.Reviewed,
            Reviews.NotYet => AskWordKind.NotYet,
            _ => AskWordKind.Skipped,
        };
        return await asks.RecordWordAsync(askId, new AskWord(kind, words, now, Session: null, Quest: quest.Id), now, ct).ConfigureAwait(false)
            ? askId
            : null;
    }

    /// <summary>
    /// The person's <i>Set it up in <c>environment</c></i> (REVIEWENV1b, D154 point 4; design §2.1, §3.6): a set-up step
    /// published following <paramref name="follows"/>, a done quest, to its repository, in Daoris's fixed words, carrying its
    /// requirements and its chain's choice. A door that sets <c>follows</c> after its chain's close joins D149 point 1's rule,
    /// so the step's tree grows from the work it shows.
    /// </summary>
    /// <remarks>
    /// The environment is judged by its name here; whether the repository's rule declares it is the door's, which reads the
    /// rule, and the planner sits a step whose environment is not declared (REVIEWENV1c). It is the person's later word, so a
    /// chain whose choice was <c>off</c> takes it, as its row in the design's table ranks above the chain's.
    /// </remarks>
    public async Task<QuestRespondOutcome> PublishSetUpStepAsync(
        string follows, string? environment, DateTimeOffset now, CancellationToken ct = default)
    {
        var name = follows.TrimStart('#');
        if (await quests.FindAsync(name, ct).ConfigureAwait(false) is not { } parent)
        {
            return new(QuestRespondRefusal.NotFound, $"No quest `#{name}`. Ids come from `quest_list`.", Quest: null);
        }

        var env = environment?.Trim() ?? "";
        if (Reviews.JudgeEnvironment(env) is { } unfit)
        {
            return new(QuestRespondRefusal.BadSetUp, $"A set-up step names its environment, and {unfit}. Nothing was published.", Quest: null);
        }

        QuestRespondOutcome Refuse(string why) => new(QuestRespondRefusal.ReviewRefused, why + " Nothing was published.", Quest: null);
        if (parent.SetUpIn is not null) return Refuse($"Quest `#{parent.Id}` is a set-up step itself: a set-up step follows the work it shows.");
        if (parent.Status != QuestStatus.Done)
        {
            return Refuse($"Quest `#{parent.Id}` is {parent.Status}: a set-up step shows work that is done, so it follows a done quest.");
        }

        if (parent.Then.FirstOrDefault(step => step.SetUpIn is not null) is { } composed)
        {
            return Refuse($"Quest `#{parent.Id}`'s chain already composes a set-up step, in `{composed.SetUpIn}`.");
        }

        var title = $"Show #{parent.Id} in `{env}` for review";
        if ((await quests.SetUpStepsAfterAsync(parent.Id, ct).ConfigureAwait(false)).FirstOrDefault(step => step.Title != title) is { } other)
        {
            return Refuse($"Set-up step `#{other.Id}` already follows quest `#{parent.Id}`, in `{other.SetUpIn}`: one set-up step per repository per chain.");
        }

        var step = await quests.PublishAsync(
            parent.From, parent.To, title,
            $"Set the work of #{parent.Id} up in `{env}` for the person's review, by the route this repository documents for "
            + $"`{env}`, and show it to them there. Your tree holds that work: add none of your own. Say what you showed, then "
            + "close this quest done; it then waits for the person's review.",
            now, parent.Workspace, ct: ct, requirements: parent.Requirements, review: parent.Review, setUpIn: env, parent: parent.Id)
            .ConfigureAwait(false);

        return new(QuestRespondRefusal.None,
            $"Published set-up step `#{step.Id}` to `{step.To}` on behalf of `{step.From}` — {step.Status}: it shows the work of "
            + $"`#{parent.Id}` in `{env}` for your review, and its done waits for your verdict.",
            step);
    }

    private static string Capital(string text) => text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];
}
