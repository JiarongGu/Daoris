namespace Daoris.Driver;

/// <summary>
/// Where one opinion stands as the driver delivers it (XAGENT1e, D155 point 7; the second-agent design §6.3–§6.7): its state,
/// one of <see cref="OpinionDeliveryStates"/>, and the line a terminal and a look say, opening with that state.
/// </summary>
/// <param name="Line">What a terminal and a look say, one line. Never <i>no issues</i>: a pass that read nothing says so.</param>
public sealed record OpinionDelivery(string Opinion, string State, string Line)
{
    /// <summary>Why, a code: a failed pass's (<c>ended</c>, <c>out-of-time</c>) or why its findings went to the person.</summary>
    public string? Why { get; init; }

    /// <summary>The working session the findings are for.</summary>
    public string? Session { get; init; }

    /// <summary>The working session's answers as read when its turn ended; null until then.</summary>
    public OpinionReading? Reading { get; init; }

    /// <summary>What waits for the person (design §6.6): null until the answers are read, or the findings went to the person.</summary>
    public OpinionDisputes? Disputes { get; init; }

    /// <summary>The one recheck, where this delivery asked it (design §6.5).</summary>
    public OpinionPassRun? Recheck { get; init; }
}

/// <summary>
/// The second opinion delivered (XAGENT1e, D155 point 7; the second-agent design §6.3–§6.7): a first pass's findings handed once
/// to the working session as its next turn, through the local host's door, which keeps them on its record as another agent's
/// claims in Daoris's fixed words; its answers read as its turn ends, each fix's commit checked; one recheck where a finding was
/// fixed, whose findings go to the person; and the person instead wherever the working session cannot take them.
/// </summary>
/// <remarks>
/// <para><b>The session goes on in its own conversation, by D137's door.</b> The words wait on its record with <c>by</c>, and the
/// next look resumes it as it resumes the person's words (<see cref="ContinueAsync"/>): its record reopens, its account is its
/// own, its quest is unmoved. They are never handed to a new session as the person's words (design §6.7).</para>
///
/// <para><b>Idempotent, and read from facts.</b> <see cref="DeliverAsync"/> reads the host's opinion, the working session's
/// record and what it kept (<see cref="OpinionDeliveries"/>), and does the next thing owed, once: the gate (XAGENT1f) calls it at
/// each look for the opinion on a landing until it says the opinion is answered, or with the person.</para>
/// </remarks>
public sealed partial class Driver
{
    /// <summary>
    /// How a pass is run: <see cref="PassAsync"/>, unless a test hands in a stand-in, since a real pass spawns git and an agent,
    /// which the suite's fast half may not (MOD8). The recheck is run through it (XAGENT1e).
    /// </summary>
    internal Func<OpinionPassAsk, ReviewerChoice, CancellationToken, Task<OpinionPassRun>>? Passes { get; init; }

    /// <summary>
    /// How a fix's commit is read from git when the working session's turn ends (XAGENT1e, design §6.4): the review's seam,
    /// <see cref="WorkingTree.ReadGitAsync"/> unless a test hands in its stand-in.
    /// </summary>
    internal WorkingTree.GitRead OpinionGit { get; init; } = WorkingTree.ReadGitAsync;

    // What this machine's driver did with each opinion, beside its packet (XAGENT1e).
    private readonly OpinionDeliveries _deliveries = new(home);

    private OpinionDeliveries Deliveries => _deliveries;

    /// <summary>
    /// Deliver <paramref name="opinion"/> as far as the facts let it go now: a pass still reading waits; a failed one is
    /// <i>Try again</i> with why; one that raised nothing says what it read; a first pass's findings go once to the working
    /// session at its turn's end, or to the person where it cannot take them; its answers, once read, settle whether the one
    /// recheck is due, which runs here; a recheck's findings go to the person.
    /// </summary>
    /// <param name="asked">What the first pass was asked with (<see cref="PassAsync"/>): the rule, the checkout, the workspace and
    /// what was asked, which its recheck reads again from the working tree's new tip.</param>
    public async Task<OpinionDelivery> DeliverAsync(string opinion, OpinionPassAsk asked, CancellationToken ct = default)
    {
        var view = await service.ReadOpinionAsync(opinion, ct).ConfigureAwait(false);
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

        // A recheck's findings go to the person, never back to the working session by themselves (design §6.5).
        if (!view.First)
        {
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

        // Raised nothing: said with what it read, never as no issues (design §6.1).
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
        if (view.HandedTo is null) return await HandAsync(view, named, ct).ConfigureAwait(false);
        if (kept.Answered is { } answered) return await AnsweredAsync(view, named, answered, kept, asked, ct).ConfigureAwait(false);

        // Handed: its record has the words, or its turn on them runs, until the turn ends and its answers are read.
        var record = await service.RecordAsync(view.HandedTo, ct).ConfigureAwait(false);
        if (record is null) return ToPersonKept(view, named, OpinionToPerson.NotFound);
        if (view.HandedWord is { } word && new GoOnMarks(home).Read(record.Session) is { } mark && mark.Said.Contains(word, StringComparer.Ordinal))
        {
            // Marked as words its session could not go on with (design §6.7): the look that marked them said why.
            return ToPersonKept(view, named, mark.Why);
        }

        if (record.Running || record.Said?.Any(each => each.Id == view.HandedWord) == true)
        {
            return new OpinionDelivery(
                view.Id, OpinionDeliveryStates.WithSession,
                $"with-session  {named}: session {record.Session} {(record.Running ? "is answering" : "goes on with")} its "
                + $"{Plural(view.Findings.Count, "finding")}{(record.Running ? "" : " at the driver's next look")}.")
            {
                Session = record.Session,
            };
        }

        // Its turn ended, and nothing read its answers as it did (a run that failed before its end): read them now.
        var reading = await ReadAnswersAsync(view, record.Session, record.Tree, ct).ConfigureAwait(false);
        return await AnsweredAsync(view, named, reading, Deliveries.Read(view.Id), asked, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Hand a first pass's findings to the working session (design §6.3): once, through the local host's door, and only to an
    /// ended record of this machine's that takes words. A running or parked one takes them at its turn's end, so they wait; a
    /// record that never goes on sends them to the person (design §6.7).
    /// </summary>
    private async Task<OpinionDelivery> HandAsync(OpinionView view, string named, CancellationToken ct)
    {
        var record = await service.RecordAsync(view.Working, ct).ConfigureAwait(false);
        var never = record switch
        {
            null => OpinionToPerson.NotFound,
            { Teammate: true } => ContinueWhy.Teammate,
            { Ask.Length: > 0 } => ContinueWhy.Intake,
            { Kind: "chat" } => OpinionToPerson.Conversation,
            _ when string.Equals(record.State, "stood-down", StringComparison.OrdinalIgnoreCase) => ContinueWhy.StoodDown,
            _ => null,
        };
        if (never is not null) return ToPersonKept(view, named, never);

        if (record!.Running || record.Parked)
        {
            // 🔴 Never into a step in flight (D136): a running turn takes another agent's claims at its end, and a park after the
            // person's answer. The next look hands them, once the record has ended.
            return Waiting(view, named, record);
        }

        var (handed, message, _) = await service.HandOpinionAsync(view.Id, ct).ConfigureAwait(false);
        if (!handed)
        {
            // The record moved between the read and the hand, or the host said why not: tried again at the next look.
            return new OpinionDelivery(view.Id, OpinionDeliveryStates.Waiting, $"waiting  {named}: {message}") { Session = view.Working };
        }

        var line = $"— {view.Who}'s {Plural(view.Findings!.Count, "finding")} on this work are its next turn, as another agent's claims "
                   + "to check, at the driver's next look.";
        output?.Append(view.Working, line);
        return new OpinionDelivery(
            view.Id, OpinionDeliveryStates.Handed,
            $"handed  {named}: its {Plural(view.Findings.Count, "finding")} went to session {view.Working} as another agent's claims, "
            + "its next turn at the driver's next look.")
        {
            Session = view.Working,
        };
    }

    /// <summary>
    /// The working session answered (design §6.4–§6.6): the one recheck asked where it is due and not yet asked, and what then
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

        var recheck = kept.Recheck is { } id ? await service.ReadOpinionAsync(id, ct).ConfigureAwait(false) : null;
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
    /// The one recheck (design §6.5): a fresh pass by the same reviewer, walked again for its account, over the commits from the
    /// first pass's tip to the working tree's tip as its turn ended, handed the first pass's findings and the answers.
    /// </summary>
    private async Task<OpinionPassRun> RecheckAsync(OpinionView first, OpinionReading reading, OpinionPassAsk asked, CancellationToken ct)
    {
        var working = await service.RecordAsync(first.Working, ct).ConfigureAwait(false);
        IReadOnlyCollection<string> wrote = working?.Adapter is { Length: > 0 } adapter ? [adapter] : [];
        var choice = await ReviewerChoice.ChooseAsync(
                _harnesses, asked.Rule with { Reviewers = [first.Adapter] }, wrote, config, asked.Workspace, ct)
            .ConfigureAwait(false);
        var ask = asked with
        {
            Occasion = first.Occasion,
            Working = first.Working,
            Base = first.Tip,
            Tip = reading.Tip!,
            Rechecks = new OpinionRecheckOf(first, reading),
            // A recheck is no ask of the gate's: it is found through what the delivery keeps (XAGENT1f).
            Opened = null,
        };
        return await (Passes ?? PassAsync)(ask, choice, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// The answers as the working session's turn ends (design §6.4), kept beside the opinion and said in its conversation: a fix
    /// counts where git reads its commit as one the turn made, and a finding it left unanswered is unresolved.
    /// </summary>
    private async Task<OpinionReading> ReadAnswersAsync(OpinionView view, string session, string? tree, CancellationToken ct)
    {
        var reading = await OpinionAnswers.ReadAsync(view, session, tree, OpinionGit, DateTimeOffset.UtcNow, ct).ConfigureAwait(false);
        Deliveries.Answered(view.Id, reading);
        // The machine log's line for it (XAGENT1f, design §8.6): counts only.
        service.LandingSaid(OpinionLines.Answered(view.Id, reading));
        var line = OpinionAnswers.Line(view, reading);
        output?.Append(session, line);
        _events.Keep(session, new SessionEvent { Kind = SessionEventKind.Note, Text = line, Opinion = view.Id }, say: null);
        return reading;
    }

    /// <summary>
    /// A resumed run that carried another agent's findings ended (design §6.4): its answers read for each opinion whose words it
    /// took, before its record concludes, so a look never reads the record ended with nothing read.
    /// </summary>
    private async Task OpinionsAnsweredAsync(ResumeAsk resume, string session, string tree, CancellationToken ct)
    {
        if (!resume.Prompted) return;
        foreach (var id in resume.Opinions)
        {
            try
            {
                if (await service.ReadOpinionAsync(id, ct).ConfigureAwait(false) is { First: true, Findings: not null } view)
                {
                    await ReadAnswersAsync(view, session, tree, ct).ConfigureAwait(false);
                }
            }
            catch (Exception error) when (error is HttpRequestException or DriverException or IOException
                                              || (error is OperationCanceledException && !ct.IsCancellationRequested))
            {
                // The look that delivers it reads them instead, once the record has ended (DeliverAsync).
                output?.Append(session, $"— the answers to second opinion `{id}` could not be read as its turn ended: {error.Message}");
            }
        }
    }

    /// <summary>
    /// Another agent's findings that cannot go on in their session (design §6.7): they go to the person instead, unanswered,
    /// each <c>must</c> disputed. Kept once per opinion, the first reason standing.
    /// </summary>
    private void FindingsToPerson(PriorSession record, ContinueReason why)
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
    /// A pass that gave no opinion (design §8.4, §10): <i>Try again</i>, with why. Never <i>no issues</i>: no agent's reading
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
