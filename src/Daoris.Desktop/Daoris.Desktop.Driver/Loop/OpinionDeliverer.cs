namespace Daoris.Driver;

/// <summary>
/// The second opinion delivered: a first pass's findings handed once
/// to the working session as its next turn, through the local host's door, which keeps them on its record as another agent's
/// claims in Daoris's fixed words; its answers read as its turn ends, each fix's commit checked; one recheck where a finding is
/// fixed, whose findings go to the person; and the person instead wherever the working session cannot take them.
/// </summary>
/// <remarks>
/// <para><b>The session goes on in its own conversation.</b> The words wait on its record with <c>by</c>, and the
/// next look resumes it as it resumes the person's words (<see cref="Driver.ContinueAsync"/>): its record reopens, its account is its
/// own, its quest is unmoved. They are never handed to a new session as the person's words.</para>
///
/// <para><b>Idempotent, and read from facts.</b> <see cref="DeliverAsync"/> reads the host's opinion, the working session's
/// record and what it kept (<see cref="OpinionDeliveries"/>), and does the next thing owed, once: the look calls it at
/// each look for the opinion on a landing until it says the opinion is answered, or with the person.</para>
/// </remarks>
/// <param name="opinions">
/// The pass a recheck runs through (<see cref="LoopContext.Passes"/>, unless a test hands in a stand-in): the flow is one way,
/// the gate delivers and the delivery passes.
/// </param>
internal sealed class OpinionDeliverer(LoopContext context, DriverOpinions opinions)
{
    private readonly LoopContext _context = context;

    /// <summary>Told each answer the course gave a driven record's findings: a test's, no person's.</summary>
    internal Action<LineCourse>? Decided { get; set; }

    private readonly DriverOpinions _opinions = opinions;

    // What this machine's driver did with each opinion, beside its packet.
    private readonly OpinionDeliveries _deliveries = new(context.Home);

    private OpinionDeliveries Deliveries => _deliveries;

    private ServiceClient Service => _context.Service;

    /// <summary>
    /// Deliver <paramref name="opinion"/> as far as the facts let it go now: a pass still reading waits; a failed one is
    /// <i>Try again</i> with why; one that raised nothing says what it read; a first pass's findings go once to the working
    /// session at its turn's end, or to the person where it cannot take them; its answers, once read, settle whether the one
    /// recheck is due, which runs here; a recheck's findings go to the person.
    /// </summary>
    /// <param name="asked">What the first pass is asked with (<see cref="DriverOpinions.PassAsync"/>): the rule, the checkout, the workspace and
    /// what is asked, which its recheck reads again from the working tree's new tip.</param>
    public async Task<OpinionDelivery> DeliverAsync(string opinion, OpinionPassAsk asked, CancellationToken ct = default)
    {
        var view = await Service.Opinions.ReadAsync(opinion, ct).ConfigureAwait(false);
        if (view is null)
        {
            return new OpinionDelivery(opinion, OpinionDeliveryStates.Unknown, $"unknown  there is no second opinion `{opinion}` on this machine.");
        }

        var named = $"second opinion {view.Id} ({view.Who})";
        switch (view.State)
        {
            case OpinionViews.Failed:
                return TryAgain(view, named);
            case OpinionViews.GivenState when view.Findings is not null:
                break;
            default:
                return new OpinionDelivery(view.Id, OpinionDeliveryStates.Reading, $"reading  {named}: it is still reading the work.")
                {
                    Session = view.Working,
                };
        }

        // A recheck's findings go to the person, never back to the working session by themselves.
        if (!view.First)
        {
            Deliveries.ToPerson(view.Id, OpinionToPerson.Recheck, DateTimeOffset.UtcNow);
            return new OpinionDelivery(
                view.Id, OpinionDeliveryStates.ToPerson,
                $"to-person  {named}, the recheck: {Plural(view.Findings!.Count, "finding")} of its own, and "
                + $"{Plural(view.Rechecked.Count(word => word.Value == OpinionViews.Withdrawn), "first-pass finding")} withdrawn. "
                + $"{OpinionToPerson.Said(OpinionToPerson.Recheck)}.")
            {
                Why = OpinionToPerson.Recheck,
                Session = view.Working,
            };
        }

        // Raised nothing: said with what it read, never as no issues.
        if (view.Findings!.Count == 0)
        {
            return new OpinionDelivery(
                view.Id, OpinionDeliveryStates.RaisedNothing, $"raised-nothing  {view.Who} raised nothing in what it read: {view.Read}")
            {
                Session = view.Working,
            };
        }

        var kept = Deliveries.Read(view.Id);
        if (kept.Person is { } toPerson) return ToPerson(view, named, toPerson);
        if (view.HandedTo is null) return await HandAsync(view, named, asked, ct).ConfigureAwait(false);
        if (kept.Answered is { } answered) return await AnsweredAsync(view, named, answered, kept, asked, ct).ConfigureAwait(false);

        // Handed: its record has the words, or its turn on them runs, until the turn ends and its answers are read.
        var record = await Service.RecordAsync(view.HandedTo, ct).ConfigureAwait(false);
        if (record is null) return ToPersonKept(view, named, OpinionToPerson.NotFound);

        // Findings a hand-over's record took before it answered are handed again, once, to its line's
        // newest record in the same tree, which then answers them; where the newest cannot take them, they go to the person.
        var chatRun = ChatRunOf(record, asked);
        if (chatRun && !record.Running && view.Answers.Count == 0
            && await LineOfAsync(view.HandedTo, ct).ConfigureAwait(false) is { Count: > 0 } handedLine
            && !string.Equals(handedLine[^1].Id, record.Session, StringComparison.OrdinalIgnoreCase))
        {
            var again = await SendBackAsync(view, named, asked, handedLine, ct).ConfigureAwait(false);
            return again.State == OpinionDeliveryStates.Waiting && again.Why == SendBackRefused
                ? ToPersonKept(view, named, WordsNever.Continued)
                : again;
        }


        // Findings the answers-alone bound holds wait for the person's choice, still handed; kept (*Not now*), they are theirs.
        if (view.HandedWord is { } word && new GoOnMarks(_context.Home).Read(record.Session) is { } mark && mark.Why != GoOnMarks.AnswersAlone
            && mark.Said.Contains(word, StringComparer.Ordinal))
        {
            // Marked as words its session could not go on with: the look that marked them said why.
            return ToPersonKept(view, named, mark.Why);
        }

        // A chat run's live record stays `working` between turns, so it is read by its runner's turn state; one no runner
        // here holds is read by its record. Its answering turn's own completion kept its reading, read above.
        var running = record.Running;
        if (chatRun && _context.SendsBack?.TurnEnded(record.Session) is { } between) running = !between;
        if (running || record.Said?.Any(each => each.Id == view.HandedWord) == true)
        {
            return new OpinionDelivery(
                view.Id, OpinionDeliveryStates.WithSession,
                $"with-session  {named}: session {record.Session} {(running ? "is answering" : "goes on with")} its "
                + $"{Plural(view.Findings.Count, "finding")}{(running ? "" : " at the driver's next look")}.")
            {
                Session = record.Session,
            };
        }

        // Its turn ended, and nothing read its answers as it did (a run that failed before its end): read them now.
        var reading = await ReadAnswersAsync(view, record.Session, record.Tree, ct).ConfigureAwait(false);
        return await AnsweredAsync(view, named, reading, Deliveries.Read(view.Id), asked, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Hand a first pass's findings to the working session: once, through the local host's door, and only to an
    /// ended record of this machine's that takes words. A running or parked one takes them at its turn's end, so they wait; a
    /// record that never goes on sends them to the person. Through the host's
    /// one door for solicited answers, the record read is the head of the working session's line, as that door walks it, and a park the person
    /// answered takes them beside their answer; the hand asks a settled answer under the opinion's key, and a settled or waiting one is handed.
    /// </summary>
    private async Task<OpinionDelivery> HandAsync(OpinionView view, string named, OpinionPassAsk asked, CancellationToken ct)
    {
        var working = await Service.RecordAsync(view.Working, ct).ConfigureAwait(false);
        // A chat run's findings go back into its own conversation, only between turns. A chat with no chat
        // run, one in the checkout or whose tree is gone, sends them to the person below.
        if (working is not null && ChatRunOf(working, asked) && await LineOfAsync(view.Working, ct).ConfigureAwait(false) is { Count: > 0 } chatLine)
        {
            return await SendBackAsync(view, named, asked, chatLine, ct).ConfigureAwait(false);
        }

        var record = working is null ? null : SolicitedHand.Walk(await Service.RecordsAsync(ct).ConfigureAwait(false), view.Working).Head ?? working;

        // What never takes findings is the course's answer for a solicited answer; a conversation is the chat runner's,
        // which the line above sent it to where it has a run, and nothing else is the course's to say.
        var never = record switch
        {
            null => OpinionToPerson.NotFound,
            _ => DrivenCourse.SendBack(record, Decided) switch
            {
                LineCourse.Never { Why: CourseNeverWhy.Teammate } => ContinueWhy.Teammate,
                LineCourse.Never { Why: CourseNeverWhy.Intake } => ContinueWhy.Intake,
                _ when record.Kind == "chat" => OpinionToPerson.Conversation,
                LineCourse.Never { Why: CourseNeverWhy.StoodDown } => ContinueWhy.StoodDown,
                _ => null,
            },
        };
        if (never is not null) return ToPersonKept(view, named, never);

        // 🔴 Never into a step in flight: a running turn takes another agent's claims at its end, and a park after the person's answer,
        // which through the one door is beside it. The next look hands them, once the record has ended.
        if (!SolicitedHand.TakesNow(record!)) return Waiting(view, named, record!);

        // A settled answer is handed: the findings' word is already kept and taken, so nothing is said for it again.
        var hand = await Service.Opinions.HandWordAsync(view.Id, ct).ConfigureAwait(false);
        if (!hand.Handed)
        {
            // The record moved between the read and the hand, or the host said why not: tried again at the next look.
            return new OpinionDelivery(view.Id, OpinionDeliveryStates.Waiting, $"waiting  {named}: {hand.Message}") { Session = record.Session };
        }

        // Where the door kept them, or found them waiting: the head of the line, never a record the line has left.
        var to = hand.Answer.Holder?.Session ?? hand.Opinion?.HandedTo ?? record.Session;
        if (hand.Answer.Fate != SolicitedFate.Settled)
        {
            _context.Output?.Append(to, $"— {view.Who}'s {Plural(view.Findings!.Count, "finding")} on this work are its next turn, as another agent's claims "
                                        + "to check, at the driver's next look.");
        }

        return new OpinionDelivery(
            view.Id, OpinionDeliveryStates.Handed,
            $"handed  {named}: its {Plural(view.Findings!.Count, "finding")} went to session {to} as another agent's claims, "
            + "its next turn at the driver's next look.")
        {
            Session = to,
        };
    }

    /// <summary>Why a send-back that reserved the conversation took nothing: the service's hand refused, which <see cref="Waiting"/> says.</summary>
    private const string SendBackRefused = "hand-refused";

    /// <summary>
    /// Whether a record is a chat run's conversation whose findings go back into it: a chat of this machine's, asked on a chat run,
    /// in a tree that stands. A chat in the checkout has no chat run, and one whose tree is gone is the person's.
    /// </summary>
    private static bool ChatRunOf(PriorSession record, OpinionPassAsk asked) =>
        ChatRuns.IsChatRun(asked.Run) && record is { Kind: "chat", Teammate: false, Tree.Length: > 0 } && !TreeFolders.TreeGone(record.Tree!);

    /// <summary>A record's line in its tree, oldest first, as the service answers it now; empty where it does not hold the record.</summary>
    private async Task<IReadOnlyList<TracedSession>> LineOfAsync(string session, CancellationToken ct)
    {
        var sessions = Trace.ParseSessions(await Service.SessionRecordsJsonAsync(ct).ConfigureAwait(false));
        return sessions.FirstOrDefault(each => string.Equals(each.Id, session, StringComparison.OrdinalIgnoreCase)) is { } record
            ? ChatRuns.LineOf(record, sessions)
            : [];
    }

    /// <summary>
    /// A chat run's findings sent back into its conversation, to the line's newest record in
    /// its tree, with three distinct outcomes: <b>to the person</b>, <c>person-went-on</c>, only where the person's turn count rose since the
    /// press; <b>waiting</b> only for activity that is not theirs, a live conversation no runner here holds, or a headless host; <b>reserved</b>
    /// otherwise, the send-back's turn taken at once, or the ended conversation gone on with them in its own conversation.
    /// </summary>
    private async Task<OpinionDelivery> SendBackAsync(
        OpinionView view, string named, OpinionPassAsk asked, IReadOnlyList<TracedSession> line, CancellationToken ct)
    {
        var newest = line[^1];
        // A press that carries no count says nothing of the person's turns, so nothing can show they did not go on.
        if (asked.Turns is not { } pressed) return ToPersonKept(view, named, OpinionToPerson.PersonWentOn);
        var ids = line.Select(each => each.Id).ToList();
        var ask = new ChatSendBackAsk(newest.Id, pressed, ids)
        {
            Hand = async (to, token) =>
            {
                var hand = await Service.Opinions.HandWordAsync(view.Id, token, to).ConfigureAwait(false);
                return hand.Handed ? hand.Word : null;
            },
            Answered = steered => FrozenAsync(view, newest.Id, newest.Tree, steered),
        };

        SentBack sent;
        if (ChatRuns.Live(newest))
        {
            if (_context.SendsBack is not { } runner) return Waiting(view, named, newest.Id, WaitingWhy.Headless);
            sent = await runner.SendBackAsync(ask, ct).ConfigureAwait(false);
        }
        else
        {
            // 🔴 The count first. A person who went on after the press and ended the chat again moved past them.
            var waiting = (await Service.RecordAsync(newest.Id, ct).ConfigureAwait(false))?.Waiting.Count(word => word.Persons) ?? 0;
            if (ChatPresses.TurnsOf(_context.Events, ids, waiting) > pressed) return ToPersonKept(view, named, OpinionToPerson.PersonWentOn);
            if (_context.SendsBack is not { } runner) return Waiting(view, named, newest.Id, WaitingWhy.Headless);
            sent = await runner.GoOnWithFindingsAsync(ask, ct).ConfigureAwait(false);
        }

        switch (sent.Outcome)
        {
            case SendBack.PersonWentOn:
                return ToPersonKept(view, named, OpinionToPerson.PersonWentOn);
            case SendBack.CannotGoOn:
                return ToPersonKept(view, named, sent.Why ?? ContinueWhy.Refused);
            case SendBack.Reserved:
                var line2 = $"— {view.Who}'s {Plural(view.Findings!.Count, "finding")} on this work are its next turn, as another agent's claims to "
                            + "check, taken between turns.";
                _context.Output?.Append(newest.Id, line2);
                return new OpinionDelivery(
                    view.Id, OpinionDeliveryStates.Handed,
                    $"handed  {named}: its {Plural(view.Findings.Count, "finding")} went to session {newest.Id} as another agent's claims, its "
                    + "next turn, taken between turns.")
                {
                    Session = newest.Id,
                };
            case SendBack.Refused:
                return Waiting(view, named, newest.Id, SendBackRefused);
            case SendBack.NotHere:
                return Waiting(view, named, newest.Id, WaitingWhy.NotHere);
            default:
                return Waiting(view, named, newest.Id, sent.Why ?? WaitingWhy.Busy);
        }
    }

    /// <summary>Why a chat run's findings wait: each said in the delivery's line, and tried again at the next look.</summary>
    private static class WaitingWhy
    {
        public const string Headless = "no conversation runs on this host";
        public const string NotHere = "no runner here holds its conversation";
        public const string Busy = "a turn that is not yours runs or opens, so they wait for its end";
    }

    private static OpinionDelivery Waiting(OpinionView view, string named, string session, string why) =>
        new(view.Id, OpinionDeliveryStates.Waiting,
            $"waiting  {named}: session {session}'s {Plural(view.Findings!.Count, "finding")} wait: "
            + (why == SendBackRefused ? "the service did not take them" : why) + "; the next look tries again.")
        {
            Why = why,
            Session = session,
        };

    /// <summary>
    /// The answering turn's frozen reading: its ending tip and the answers kept, read as that turn
    /// ends and before another runs, kept beside the opinion and said in the conversation, as a resumed run's end keeps its own; marked
    /// <c>person-steered</c> where a word of the person's reached that turn.
    /// </summary>
    private async Task FrozenAsync(OpinionView first, string session, string? tree, bool steered)
    {
        if (await Service.Opinions.ReadAsync(first.Id, CancellationToken.None).ConfigureAwait(false) is { First: true, Findings: not null } view)
        {
            await ReadAnswersAsync(view, session, tree, CancellationToken.None, steered).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// The working session answered: the one recheck asked where it is due and not yet asked, and what then
    /// waits for the person.
    /// </summary>
    private async Task<OpinionDelivery> AnsweredAsync(
        OpinionView view, string named, OpinionReading reading, OpinionDelivered kept, OpinionPassAsk asked, CancellationToken ct)
    {
        OpinionPassRun? run = null;
        if (kept.Recheck is null && kept.RecheckWhy is null)
        {
            if (OpinionRechecks.WhyNot(view, reading, asked.Rule) is { } whyNot)
            {
                kept = Deliveries.Rechecked(view.Id, null, whyNot);
            }
            else
            {
                run = await RecheckAsync(view, reading, asked, ct).ConfigureAwait(false);
                kept = Deliveries.Rechecked(view.Id, run.Opinion, run.Opinion is null ? run.Code ?? OpinionRechecks.Refused : null);
            }
        }

        var recheck = kept.Recheck is { } id ? await Service.Opinions.ReadAsync(id, ct).ConfigureAwait(false) : null;
        var disputes = OpinionDisputes.Of(reading, recheck);
        var then = kept.Recheck is not null
            ? $" Recheck {kept.Recheck} reads the commits since."
            : kept.RecheckWhy is { } why ? $" No recheck: {OpinionRechecks.Said(why)}." : "";
        return new OpinionDelivery(
            view.Id, OpinionDeliveryStates.Answered,
            $"answered  {named}: session {reading.Session} answered as its turn ended; "
            + $"{Plural(disputes.Count, "dispute")} for the person.{then}")
        {
            Session = reading.Session,
            Reading = reading,
            Disputes = disputes,
            Recheck = run,
        };
    }

    /// <summary>
    /// The one recheck: a fresh pass by the same reviewer, walked again for its account, over the commits from the
    /// first pass's tip to the working tree's tip as its turn ended, handed the first pass's findings and the answers.
    /// </summary>
    private async Task<OpinionPassRun> RecheckAsync(OpinionView first, OpinionReading reading, OpinionPassAsk asked, CancellationToken ct)
    {
        // Judged against every agent the line ran on, like the first pass.
        var records = OpinionWriters.ById(await Service.RecordsAsync(ct).ConfigureAwait(false));
        var wrote = OpinionWriters.Agents(records, first.Working, asked.Quests.Select(quest => quest.Id));
        // 🔴 The fixes were made by the record that answered, which after a re-hand is a
        // successor `first.Working`'s ancestry misses; its line's agents are the work's too. Where it is of the first reviewer's family, that
        // reviewer is not independent of the fixes, and the recheck goes to another of the rule's reviewers, or to none.
        IReadOnlyList<string> answeredBy = string.Equals(reading.Session, first.Working, StringComparison.OrdinalIgnoreCase)
            ? []
            : OpinionWriters.Agents(records, reading.Session, []);
        wrote = [.. wrote, .. answeredBy.Where(agent => !wrote.Contains(agent, StringComparer.OrdinalIgnoreCase))];
        var firstFamily = AgentFamily.Of(_context.Harnesses.Adapters, first.Door);
        IReadOnlyList<string> reviewers = answeredBy.Any(agent => AgentFamily.Of(_context.Harnesses.Adapters, agent).Same(firstFamily))
            ? [.. asked.Rule.Reviewers.Where(each => !AgentFamily.Of(_context.Harnesses.Adapters, each).Same(firstFamily))]
            : [first.Door];
        // A recheck by the first reviewer is a continuation, so it takes the first pass's record's asked choice, not the actor as it
        // stands now; with the record's asked unread (an older record), or another reviewer chosen above, it reads the actor's entries as the
        // first pass did.
        IReadOnlyList<ActorEffective>? order = reviewers is [var same] && string.Equals(same, first.Door, StringComparison.OrdinalIgnoreCase)
            && records.GetValueOrDefault(first.Session) is { Asked: { } was } record
            ? [new ActorEffective(first.Door, was.NamedModel, was.Effort is { Carried: true, Value: { } effort } ? effort : null, record.Choice ?? "rule-default")]
            : null;
        Task<ReviewerChoice> Walk(IReadOnlyList<ReviewerPassed>? passed) =>
            ReviewerChoice.ChooseAsync(
                _context.Harnesses, asked.Rule with { Reviewers = reviewers }, wrote, _context.Config, asked.Workspace, ct, passed: passed, order: order,
                catalogue: () => RoleCatalog.ReadHome(_context.Harnesses.Home));
        var choice = await Walk(null).ConfigureAwait(false);
        var ask = asked with
        {
            Reask = Walk,
            Occasion = first.Occasion,
            Working = first.Working,
            Base = first.Tip,
            Tip = reading.Tip!,
            Rechecks = new OpinionRecheckOf(first, reading),
            // A recheck is no ask of the gate's: it is found through what the delivery keeps.
            Opened = null,
        };
        return await (_context.Passes ?? _opinions.PassAsync)(ask, choice, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// The answers as the working session's turn ends, kept beside the opinion and said in its conversation: a fix
    /// counts where git reads its commit as one the turn made, and a finding it left unanswered is unresolved.
    /// </summary>
    private async Task<OpinionReading> ReadAnswersAsync(OpinionView view, string session, string? tree, CancellationToken ct, bool steered = false)
    {
        var reading = await OpinionAnswers.ReadAsync(view, session, tree, _context.OpinionGit, DateTimeOffset.UtcNow, ct).ConfigureAwait(false)
            with { Steered = steered };
        Deliveries.Answered(view.Id, reading);
        // The machine log's line for it: counts only.
        Service.LandingSaid(OpinionLines.Answered(view.Id, reading));
        var noted = OpinionAnswers.Told(view, reading);
        _context.Output?.Append(session, noted.Note);
        _context.Events.Keep(
            session,
            new SessionEvent { Kind = SessionEventKind.Note, Text = noted.Note, Parts = noted.Parts, Opinion = view.Id },
            say: null);
        return reading;
    }

    /// <summary>
    /// A resumed run that carried another agent's findings ended: its answers read for each opinion whose words it
    /// took, before its record concludes, so a look never reads the record ended with nothing read.
    /// </summary>
    public async Task OpinionsAnsweredAsync(ResumeAsk resume, string session, string tree, CancellationToken ct)
    {
        if (!resume.Prompted) return;
        foreach (var id in resume.Opinions)
        {
            try
            {
                if (await Service.Opinions.ReadAsync(id, ct).ConfigureAwait(false) is { First: true, Findings: not null } view)
                {
                    await ReadAnswersAsync(view, session, tree, ct).ConfigureAwait(false);
                }
            }
            catch (Exception error) when (error is HttpRequestException or DriverException or IOException
                                              || (error is OperationCanceledException && !ct.IsCancellationRequested))
            {
                // The look that delivers it reads them instead, once the record has ended (DeliverAsync).
                _context.Output?.Append(session, $"— the answers to second opinion `{id}` could not be read as its turn ended: {error.Message}");
            }
        }
    }

    /// <summary>
    /// Another agent's findings that cannot go on in their session: they go to the person instead, unanswered,
    /// each <c>must</c> disputed. Kept once per opinion, the first reason standing.
    /// </summary>
    public void FindingsToPerson(PriorSession record, ContinueReason why)
    {
        foreach (var opinion in record.Findings) Deliveries.ToPerson(opinion, why.Code, DateTimeOffset.UtcNow);
    }

    private OpinionDelivery ToPersonKept(OpinionView view, string named, string why)
    {
        Deliveries.ToPerson(view.Id, why, DateTimeOffset.UtcNow);
        return ToPerson(view, named, why);
    }

    private static OpinionDelivery ToPerson(OpinionView view, string named, string why) =>
        new(view.Id, OpinionDeliveryStates.ToPerson,
            $"to-person  {named}: its {Plural(view.Findings?.Count ?? 0, "finding")} go to the person, unanswered, because "
            + $"{OpinionToPerson.Said(why)}.")
        {
            Why = why,
            Session = view.Working,
            Disputes = OpinionDisputes.Unanswered(view),
        };

    private static OpinionDelivery Waiting(OpinionView view, string named, PriorSession record) =>
        new(view.Id, OpinionDeliveryStates.Waiting,
            $"waiting  {named}: session {record.Session} is {record.State}, so its {Plural(view.Findings!.Count, "finding")} reach it "
            + "at its turn's end, never into a step in flight.")
        {
            Session = record.Session,
        };

    /// <summary>
    /// A pass that gave no opinion: <i>Try again</i>, with why. Never <i>no issues</i>: no agent's reading
    /// stands here.
    /// </summary>
    private static OpinionDelivery TryAgain(OpinionView view, string named) =>
        new(view.Id, OpinionDeliveryStates.TryAgain,
            $"try-again  {named} gave no opinion: "
            + (view.Why == "out-of-time"
                ? $"its {(view.Minutes is { } minutes ? $"{minutes} " : "")}minutes ran out before it said one"
                : "its session ended without saying one")
            + ". No agent's reading stands for this work: try again.")
        {
            Why = view.Why ?? "ended",
            Session = view.Working,
        };

    private static string Plural(int count, string noun) => count == 1 ? $"1 {noun}" : $"{count} {noun}s";
}
