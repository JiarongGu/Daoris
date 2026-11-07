namespace Daoris.Driver;

/// <summary>
/// The record the person's words wait on goes on itself where its harness conversation resumes: an answer continues its own session,
/// and so do words to a session that had ended. Otherwise a park ends and the words are handed on in a new session, saying why, which
/// walks the role's order again when the record began in the tool's own home. Asked after every hold between (access, tree, replay,
/// trust), so a held start leaves no word on a limited record: Daoris's word is kept on it only here, just before it goes on.
/// </summary>
[RunStage("continue", RunPhase.Cleared, "trust")]
internal sealed class GoOnRecord(IRunWiring wiring) : IRunStage
{
    public async ValueTask<StartRun?> RunAsync(RunRound run, CancellationToken ct)
    {
        var context = wiring.Context;
        var state = run.State;
        var quest = run.Quest;
        var fresh = run.Fresh;
        var start = run.Start;
        var resume = state.Resume;
        var limitResume = state.LimitResume;
        ContinueReason? fellBack = null;
        var answerKept = false;

        // The person's words, where a go-on carries any, are already on a limited record, so theirs go first and Daoris's last. A word
        // the door would not keep leaves the start as the carry-on planned.
        var planned = start;
        string? limitWord = null;
        if (limitResume && await wiring.GoOns.KeepLimitWordAsync(start.Resumes!, ct).ConfigureAwait(false) is { } keptWord)
        {
            limitWord = keptWord.Word;
            start = start with { Resumes = keptWord.Record, GoesOn = true };
            if (keptWord.Record.Limit) wiring.LimitResumed[quest.Id] = keptWord.Record.Session;
        }
        else if (limitResume && !start.GoesOn)
        {
            state.LimitResume = false;
            state.Resume = null;
            resume = null;
        }

        var written = start.Resumes;
        // Whether the roster's switch moved the words off a cooling own account to a new part.
        var switched = false;
        if (start.Resumes is { } park
            && start.GoesOn
            && !state.Tree.Resuming)
        {
            // Where its own account could not carry the words, the walk's pick did, and the reason says why.
            (var continued, fellBack, answerKept) = await wiring.GoOns.ContinueAsync(
                    start, park, state.Selection, run.Registry, state.Starting, run.OnOpened, ct, state.Agent, account: resume?.Elsewhere,
                    inSync: state.Tree.InSync?.Told, permissions: state.Permissions,
                    treeStands: state.Tree.PriorStands is null or { Stand: TreeStand.Stands or TreeStand.Remade })
                .ConfigureAwait(false);
            // The fresh tree is used by none of the returns that follow, so each lets it go by the clean path, on no token: a continuation
            // returns as the driver closes, and a removal on the closed token would leave it to the sweep.
            if (continued is not null)
            {
                await fresh.LetGoAsync().ConfigureAwait(false);
                return continued;
            }

            // Its own conversation refused, Daoris's word comes off by its id and the line goes on in this same start as the carry-on
            // planned, said with the refusal's own code. Another client holding the conversation keeps the word, so the line
            // is held for the person and never spins.
            if (limitWord is not null && !fellBack!.Holds)
            {
                await wiring.GoOns.TakeLimitWordsOffAsync(park.Session, [limitWord], ct).ConfigureAwait(false);
                wiring.LimitResumed.TryRemove(quest.Id, out _);
                var refusedOwn = LimitResume.Refused(fellBack);
                context.Output?.Append(park.Session, refusedOwn.Note);
                context.Events.Keep(
                    park.Session, new SessionEvent { Kind = SessionEventKind.Note, Text = refusedOwn.Note, Parts = refusedOwn.Parts }, say: null);
            }

            // The words as the record holds them now, not as the look read them, so a word taken back since never goes to the new
            // session; none left, nothing is sent. A limit's intent carries the line on all the same.
            park = await wiring.GoOns.HandedOnRecordAsync(park, ct).ConfigureAwait(false);
            if (!park.Parked && !park.Running && !park.WordsWaiting && limitWord is null)
            {
                await fresh.LetGoAsync().ConfigureAwait(false);
                return DriverContinuations.TakenBackFirst(quest, park.Session);
            }

            written = park;

            // Nothing carries the words on by itself from a closed quest's session, nor from a never, nor where another client holds
            // its conversation, since a second writer in its tree could redo its work: they stay waiting as said, marked so a later look
            // leaves them there. Where the roster's switch moved its words off a cooling own account, Daoris's own words that state a
            // fact go on in the new part with the person's; every other fall-back carries the person's alone.
            switched = resume?.Answer is LineAnswer.Elsewhere { Switched: true } && fellBack!.Code == ContinueWhy.Account;
            bool Goes(SaidWordView word) => switched ? DaorisWords.NewPartCarries(word) : word.Persons;
            if (fellBack!.Never || fellBack.Holds || quest.Status is not ("Open" or "Taken"))
            {
                await fresh.LetGoAsync().ConfigureAwait(false);
                // A closed quest's session whose own conversation is proven lost, or whose own account cools while another of the line's
                // is ready, goes on by itself as a conversation in its tree through the shell's chat runner, handed the brief and the
                // person's words. Anything else, and a host with no runner, waits for the person.
                if (!fellBack.Never && !fellBack.Holds && (fellBack.Lost || switched) && context.GoesOnLost is { } goesOn
                    && await wiring.GoOns.GoesOnLostAsync(goesOn, quest, park, fellBack, ct).ConfigureAwait(false) is { } moved)
                {
                    return moved;
                }

                return await CannotGoOnAsync(quest, park, fellBack, ct).ConfigureAwait(false);
            }

            // Another agent's findings, a helper's answer and Daoris's own words go on only in their own session: where they are all
            // that waits, nothing is carried on and they go to the person. The person's words beside them are carried on as ever.
            if (park.Waiting.Any(word => !Goes(word)) && !park.Waiting.Any(Goes))
            {
                await fresh.LetGoAsync().ConfigureAwait(false);
                return await CannotGoOnAsync(quest, park, fellBack, ct).ConfigureAwait(false);
            }

            // A taken quest is carried on, an open one started, handed the words, in the tree the session worked in. A limited line with
            // none of the person's words left goes on as the carry-on planned for it.
            start = start with
            {
                Resumes = park.Parked || park.Said is null ? park with { State = "completed" } : park,
                GoesOn = limitWord is not null && !park.WordsWaiting ? planned.GoesOn : start.GoesOn,
            };

            // Only the record that began in the tool's own home goes on there. Its words carried to a new record are a new start, which
            // takes the walk and holds where the walk holds, before the record opens: never a new session in the person's home.
            if (resume is not null)
            {
                var resumed = state.Selection;
                try
                {
                    var (picked, passed) = await context.Harnesses
                        .CarriedOnAsync(resume, state.Agent, context.Config, start.Workspace, state.HandedQuest?.Account, ct, state.CarriedModel(start.Resumes))
                        .ConfigureAwait(false);
                    state.Selection = picked;
                    state.PassedAccount ??= passed;
                }
                catch (DriverException error)
                {
                    await fresh.LetGoAsync().ConfigureAwait(false);
                    return run.Hold(error.Message);
                }

                var selection = state.Selection;
                if (!selection.Allowed)
                {
                    await fresh.LetGoAsync().ConfigureAwait(false);
                    return run.Hold(selection.Refusal!) with { Cooling = selection.Cooling, SignedOut = selection.SignedOut };
                }

                if (!ReferenceEquals(resumed, selection) && TrustAsk.Untrusted(wiring, run, selection) is { } walkedUntrusted)
                {
                    await fresh.LetGoAsync().ConfigureAwait(false);
                    return walkedUntrusted;
                }
            }
        }

        state.GoneOn = new GoOnFound(start, fellBack, answerKept, written, switched);
        return null;
    }

    /// <summary>
    /// A park whose own conversation cannot resume and that nothing carries on; a supervisor's decision on it goes stale first, its word
    /// taken back, so the park is a wait on the owner again.
    /// </summary>
    private async Task<StartRun> CannotGoOnAsync(QuestView quest, PriorSession park, ContinueReason why, CancellationToken ct)
    {
        var context = wiring.Context;
        if (context.Supervisor.Moving(park.Session))
        {
            await context.Supervisor.CannotGoOnAsync(
                park.Session, async (session, word, token) => (await context.Service.TakeBackAsync(session, word, ct: token).ConfigureAwait(false)).Done,
                DateTimeOffset.UtcNow, ct).ConfigureAwait(false);
        }

        return wiring.GoOns.CannotGoOn(quest, park, why);
    }
}
