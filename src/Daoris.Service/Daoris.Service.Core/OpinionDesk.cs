using System.Globalization;

namespace Daoris.Knowledge;

/// <summary>Why a door to a second opinion refused (XAGENT1c) — or <see cref="None"/> when it did what was asked.</summary>
public enum OpinionRefusal
{
    None,

    /// <summary>A shared host: an opinion is kept on the machine whose work it read, and a shared host keeps none (design §6.2).</summary>
    Shared,

    /// <summary>No such opinion, no such session of this machine's, or a connector that speaks for no session.</summary>
    NotFound,

    /// <summary>A part that is not one: its shape, a bound of the rule, or a session that cannot be what it is named as.</summary>
    BadShape,

    /// <summary>A bound (design §8.3): one pass the rule starts per working session, one recheck a pass, from its tip, once its findings went.</summary>
    Bound,

    /// <summary>The reviewer's tree is held by another session: one session per working tree (D51).</summary>
    Busy,

    /// <summary>The session that said one reads no work for a second opinion: only an opinion's own reviewer gives it.</summary>
    NotAReviewer,

    /// <summary>The opinion was said already: a pass says it once.</summary>
    AlreadyGiven,

    /// <summary>The pass's minutes, the rule's bound, ran out before it said its opinion.</summary>
    OutOfTime,

    /// <summary>The reviewer's session ended: an ended pass says nothing more.</summary>
    Ended,

    /// <summary>Nothing to hand or answer: no findings yet or none at all, a recheck, handed already, or not handed to this session.</summary>
    NotHanded,

    /// <summary>The working session cannot take words now: running, parked, stood down, a teammate's, an intake or a reviewer.</summary>
    CannotTake,

    /// <summary>The recheck already read the answers as they stood, so no more are taken.</summary>
    Closed,
}

/// <param name="Refusal"><see cref="OpinionRefusal.None"/> when the door did what was asked.</param>
/// <param name="Message">The whole answer, phrased once here for every door.</param>
/// <param name="Opinion">The opinion as it now stands, when the door did it.</param>
public sealed record OpinionOutcome(OpinionRefusal Refusal, string Message, Opinion? Opinion = null)
{
    /// <summary>The reviewer's record a pass opened, or the working session the findings were handed to.</summary>
    public Session? Session { get; init; }

    /// <summary>The word the findings wait in on the working session's record, when they were handed.</summary>
    public SaidWord? Word { get; init; }
}

/// <summary>
/// The judgement over second opinions (XAGENT1c, D155 points 5, 7, 8, 10 and 11; the second agent design §5.4, §6.1–§6.4,
/// §8.3): one implementation for the local host's doors and the connector's two tools, as <see cref="SessionLedger"/> is for
/// sessions. It never gives the person's answer to a dispute, and nothing here lets an agent settle one.
/// </summary>
/// <remarks>
/// <para><b>Machine-local by construction.</b> Composed on a shared host, every door refuses with one sentence: the candidate
/// is commits only this machine holds until they land, and the reviewer names an account (D47 §4).</para>
///
/// <para><b>Read, judged and written as one step</b> (REV3), inside the session store's write lock: the opinions are kept on
/// the same connection, so a pass's record and its reviewer's session are made together or not at all.</para>
/// </remarks>
/// <param name="local">Whether this is a local host's desk; a shared host's refuses every door.</param>
public sealed class OpinionDesk(OpinionStore opinions, SessionStore sessions, bool local = true)
{
    /// <summary>Whether this desk keeps opinions: a local host's does, a shared host's never.</summary>
    public bool Local => local;

    private const string NoSession =
        "This connector speaks for no session the driver started here, so there is no second opinion to say it for";

    /// <summary>
    /// Ask a pass (design §6.2): keep it, and open its reviewer's record beside it, a chat that serves no quest and names it,
    /// in the reviewer's own clone at the candidate's tip and as its account. Judged first: the shape, a working session of
    /// this machine's in the candidate's repository that is neither an intake nor a reviewer, a tree that is not the working
    /// session's and that nobody holds, and the bounds (§8.3): the rule starts one first pass per working session, and a
    /// recheck reads, once, the commits since a first pass whose findings went to that session. The person's own asks are
    /// not capped.
    /// </summary>
    public async Task<OpinionOutcome> AskAsync(OpinionAsk ask, DateTimeOffset now, CancellationToken ct = default)
    {
        if (!local) return Shared();
        if (Opinions.JudgeAsk(ask) is { } unfit) return Refused(OpinionRefusal.BadShape, unfit);

        var tree = Trees.Normalize(ask.Tree)!;
        var repository = ask.Candidate.Repository.Trim();
        return await sessions.ExclusiveAsync(async inside =>
        {
            var working = await sessions.FindAsync(ask.Working.Trim(), inside).ConfigureAwait(false);
            if (working is not { Origin: null })
            {
                return Refused(OpinionRefusal.NotFound, $"There is no session `{ask.Working.Trim()}` of this machine's to read the work of");
            }

            string? wrong =
                !string.Equals(working.Repository, repository, StringComparison.Ordinal)
                    ? $"Session `{working.Id}` worked in `{working.Repository}`, and the candidate is in `{repository}`: a pass reads the "
                      + "work of a session in the candidate's repository"
                : working.Opinion is { } reads
                    ? $"Session `{working.Id}` reads another session's work for second opinion `{reads}`: nothing lands from its tree, "
                      + "so nothing of it is read"
                : working.Ask is { } askId ? $"Session `{working.Id}` is the intake for ask `#{askId}`, which does no work in a repository"
                : string.Equals(Trees.Normalize(working.Tree), tree, StringComparison.Ordinal)
                    ? $"`{tree}` is session `{working.Id}`'s own tree: a reviewer reads a clone of its own at the candidate, never the "
                      + "working session's"
                : null;
            if (wrong is not null) return Refused(OpinionRefusal.BadShape, wrong);

            if (await sessions.ActiveForAsync(repository, tree, inside).ConfigureAwait(false) is { } holder)
            {
                return Refused(OpinionRefusal.Busy,
                    $"`{repository}`'s tree `{tree}` already has an active session, `{holder.Id}`: a reviewer's clone is its own");
            }

            if (await BoundAsync(ask, working, repository, inside).ConfigureAwait(false) is { } bound) return bound;

            var id = NewId();
            var reviewer = await sessions.CreateAsync(
                    quest: null, repository, ask.Reviewer.Adapter.Trim(), now, working.Workspace, SessionKind.Chat,
                    harnessVersion: ask.HarnessVersion, profile: ask.Reviewer.Account, tree: tree, baseCommit: ask.Candidate.Tip.ToLowerInvariant(),
                    ct: inside, opinion: id)
                .ConfigureAwait(false);
            var opinion = new Opinion(
                id, ask.Occasion, ask.Pass, working.Id, reviewer.Id,
                new OpinionCandidate(
                    repository, ask.Candidate.Base.ToLowerInvariant(), ask.Candidate.Tip.ToLowerInvariant(),
                    [.. ask.Candidate.Commits.Select(commit => commit.ToLowerInvariant())]),
                new OpinionReviewer(ask.Reviewer.Adapter.Trim(), ask.Reviewer.Label)
                {
                    Product = Blank(ask.Reviewer.Product), Maker = Blank(ask.Reviewer.Maker), Account = Blank(ask.Reviewer.Account),
                },
                now)
            {
                Rechecks = Blank(ask.Rechecks),
                Families = [.. ask.Families.Select(family => family.Trim())],
                Posture = ask.Posture,
                Minutes = ask.Minutes,
            };
            await opinions.CreateAsync(opinion, inside).ConfigureAwait(false);

            return new OpinionOutcome(
                OpinionRefusal.None,
                $"Second opinion `{id}` ({(opinion.Pass == Opinions.Recheck ? $"rechecking `{opinion.Rechecks}`" : opinion.Pass)} pass) asked "
                + $"of {Who(opinion)}: session `{reviewer.Id}` reads `{repository}` at `{Short(opinion.Candidate.Tip)}` in a clone of its own, "
                + $"within {opinion.Minutes} minutes.",
                opinion) { Session = reviewer };
        }, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// The reviewer says its opinion (design §6.1), once, through its connector: its findings, numbered in the order said,
    /// what it read and its limits, and a recheck's word on each first-pass finding. Only the session this machine started
    /// as the opinion's reviewer says it, while it runs and within the rule's minutes.
    /// </summary>
    public async Task<OpinionOutcome> GiveAsync(
        string? sessionId, IReadOnlyList<OpinionFinding?>? findings, string? read, string? limits, IReadOnlyList<OpinionRecheck?>? rechecked,
        DateTimeOffset now, CancellationToken ct = default)
    {
        if (!local) return Shared();
        if (string.IsNullOrWhiteSpace(sessionId)) return Refused(OpinionRefusal.NotFound, NoSession);

        return await sessions.ExclusiveAsync(async inside =>
        {
            var session = await sessions.FindAsync(sessionId.Trim(), inside).ConfigureAwait(false);
            if (session is not { Origin: null }) return Refused(OpinionRefusal.NotFound, NoSession);
            if (session.Opinion is not { } named || await opinions.FindAsync(named, inside).ConfigureAwait(false) is not { } opinion)
            {
                return Refused(OpinionRefusal.NotAReviewer,
                    $"Session `{session.Id}` reads no work for a second opinion: only the session Daoris started as an opinion's reviewer "
                    + "says one");
            }

            if (opinion.Given is { } earlier)
            {
                return Refused(OpinionRefusal.AlreadyGiven,
                    $"Second opinion `{opinion.Id}` was said at {Moment(earlier.At)}: a pass says its opinion once. End your turn");
            }

            if (now > opinion.Due)
            {
                return Refused(OpinionRefusal.OutOfTime,
                    $"Second opinion `{opinion.Id}`'s {opinion.Minutes} minutes ran out at {Moment(opinion.Due)}: a pass says its opinion "
                    + "within the rule's bound");
            }

            if (!session.Active)
            {
                return Refused(OpinionRefusal.Ended, $"Session `{session.Id}` is {Session.Spell(session.State)}: an ended pass says nothing more");
            }

            var (given, unfit) = await JudgedAsync(opinion, findings, read, limits, rechecked, now, inside).ConfigureAwait(false);
            if (unfit is not null) return Refused(OpinionRefusal.BadShape, unfit);

            await opinions.KeepGivenAsync(opinion.Id, given!, inside).ConfigureAwait(false);
            var kept = opinion with { Given = given };
            var whose = opinion.Pass == Opinions.Recheck
                ? " They go to the person, with what you said of the first pass's findings, never back to the working session by themselves."
                : " They go to the working session as another agent's claims, to check against the code, and the person sees its answers "
                  + "beside them.";
            return new OpinionOutcome(
                OpinionRefusal.None,
                given!.Findings.Count == 0
                    ? $"Kept second opinion `{opinion.Id}`. It reads: {Who(opinion)} raised nothing in what it read, beside what you read. "
                      + "End your turn now: a pass takes no words, and nobody can ask it anything."
                    : $"Kept second opinion `{opinion.Id}`: {Count(given.Findings.Count, "finding")} ({Weighed(given.Findings)}), with what you "
                      + $"read.{whose} End your turn now: a pass takes no words, and nobody can ask it anything.",
                kept);
        }, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Hand a first pass's findings to its working session (design §6.3): a word on its record naming the opinion as
    /// <see cref="SaidWord.By"/>, in Daoris's fixed words, which reopens it at the driver's next look (D137). Once, and only
    /// to a record of this machine's that ended and takes words: a running turn takes them at its end, never mid-step, so
    /// the driver hands them then. A recheck's findings go to the person, and a pass that raised nothing has nothing to hand.
    /// </summary>
    public async Task<OpinionOutcome> HandAsync(string id, DateTimeOffset now, CancellationToken ct = default)
    {
        if (!local) return Shared();

        return await sessions.ExclusiveAsync(async inside =>
        {
            var opinion = await opinions.FindAsync(id.Trim(), inside).ConfigureAwait(false);
            if (opinion is null) return Refused(OpinionRefusal.NotFound, $"There is no second opinion `{id.Trim()}` on this machine");

            string? nothing =
                opinion.Pass == Opinions.Recheck
                    ? $"Second opinion `{opinion.Id}` is a recheck: its findings go to the person, never back to the working session by "
                      + "themselves"
                : opinion.Given is null ? $"Second opinion `{opinion.Id}` has said nothing yet: its findings are handed once it says them"
                : opinion.Given.Findings.Count == 0
                    ? $"{Who(opinion)} raised nothing in what it read for second opinion `{opinion.Id}`, so there is nothing to answer"
                : opinion.Handed is { } handed
                    ? $"Second opinion `{opinion.Id}`'s findings went to session `{handed.Session}` at {Moment(handed.At)}: they go once"
                : null;
            if (nothing is not null) return Refused(OpinionRefusal.NotHanded, nothing);

            var working = await sessions.FindAsync(opinion.Working, inside).ConfigureAwait(false);
            if (CannotTake(working, opinion.Working) is { } why) return Refused(OpinionRefusal.CannotTake, why);

            var word = new SaidWord(NewId(), Opinions.Words(opinion), now, [], Reopens: true, By: opinion.Id);
            var kept = await sessions.KeepSaidAsync(working!.Id, word, ct: inside).ConfigureAwait(false);
            var handedTo = new OpinionHanded(working.Id, word.Id, now);
            await opinions.KeepHandedAsync(opinion.Id, handedTo, inside).ConfigureAwait(false);

            return new OpinionOutcome(
                OpinionRefusal.None,
                $"Handed second opinion `{opinion.Id}`'s {Count(opinion.Given!.Findings.Count, "finding")} to session `{working.Id}`: they "
                + "wait on its record as another agent's claims, never the person's words, and it goes on with them at the driver's next look.",
                opinion with { Handed = handedTo }) { Session = kept, Word = word };
        }, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// The working session answers findings handed to it (design §6.4): each <c>fixed</c> with a commit, <c>rejected</c> with
    /// evidence, or <c>unresolved</c> with why. Only the session they were handed to answers them, the newest opinion handed
    /// to it unless one is named, and a later answer to a finding stands over the earlier, until a recheck has read them.
    /// A commit named as a fix is kept as the session said it: the driver checks it is one the turn made.
    /// </summary>
    public async Task<OpinionOutcome> AnswerAsync(
        string? sessionId, string? opinionId, IReadOnlyList<OpinionAnswer?>? answers, DateTimeOffset now, CancellationToken ct = default)
    {
        if (!local) return Shared();
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return Refused(OpinionRefusal.NotFound,
                "This connector speaks for no session the driver started here, so no second opinion was handed to it to answer");
        }

        return await sessions.ExclusiveAsync(async inside =>
        {
            var session = await sessions.FindAsync(sessionId.Trim(), inside).ConfigureAwait(false);
            if (session is not { Origin: null }) return Refused(OpinionRefusal.NotFound, $"There is no session `{sessionId.Trim()}` of this machine's");

            var handed = await opinions.ListAsync(handedTo: session.Id, ct: inside).ConfigureAwait(false);
            var named = Blank(opinionId);
            var opinion = named is null ? handed.LastOrDefault() : handed.FirstOrDefault(each => each.Id == named);
            if (opinion is null)
            {
                return Refused(OpinionRefusal.NotHanded, named is null
                    ? $"No second opinion was handed to session `{session.Id}`: opinion_answer answers another agent's findings once Daoris "
                      + "hands them to you"
                    : $"Second opinion `{named}` was not handed to session `{session.Id}`: only the session its findings went to answers them");
            }

            if ((await opinions.ListAsync(rechecks: opinion.Id, ct: inside).ConfigureAwait(false)).FirstOrDefault() is { } recheck)
            {
                return Refused(OpinionRefusal.Closed,
                    $"Second opinion `{opinion.Id}` is being read again by `{recheck.Id}`, which reads your answers as they stood: no more are taken");
            }

            var numbers = opinion.Given!.Findings.Select(finding => finding.Number).ToList();
            var given = (answers ?? []).ToList();
            if (given.Count == 0)
            {
                return Refused(OpinionRefusal.BadShape,
                    "Answer at least one finding: fixed with its commit, rejected with your evidence, or unresolved with why");
            }

            var kept = new List<OpinionAnswer>();
            foreach (var (answer, place) in given.Select((answer, index) => (answer, index + 1)))
            {
                if (answer is null) return Refused(OpinionRefusal.BadShape, $"Answer {place} is empty: name its finding and say fixed, rejected or unresolved");
                if (!numbers.Contains(answer.Finding))
                {
                    return Refused(OpinionRefusal.BadShape,
                        $"Second opinion `{opinion.Id}` gave no finding {answer.Finding}: it gave {string.Join(", ", numbers)}");
                }

                if (kept.Any(each => each.Finding == answer.Finding)) return Refused(OpinionRefusal.BadShape, $"Finding {answer.Finding} is answered twice in one call");
                var trimmed = answer with
                {
                    Said = answer.Said?.Trim() ?? "",
                    Commit = Blank(answer.Commit)?.ToLowerInvariant(),
                    Evidence = Blank(answer.Evidence),
                    Why = Blank(answer.Why),
                    At = now,
                };
                if (Opinions.JudgeAnswer(trimmed) is { } unfit) return Refused(OpinionRefusal.BadShape, $"Finding {answer.Finding}: {unfit}");
                kept.Add(trimmed);
            }

            var standing = opinion.Answers.Where(each => kept.All(answer => answer.Finding != each.Finding)).Concat(kept).ToList();
            await opinions.KeepAnswersAsync(opinion.Id, standing, inside).ConfigureAwait(false);

            var open = numbers.Where(number => standing.All(answer => answer.Finding != number)).ToList();
            return new OpinionOutcome(
                OpinionRefusal.None,
                $"Kept {Count(kept.Count, "answer")} on second opinion `{opinion.Id}`."
                + (open.Count == 0
                    ? " Every finding is answered."
                    : $" Not answered yet: {(open.Count == 1 ? "finding" : "findings")} {string.Join(", ", open)}; anything unanswered when "
                      + "your turn ends reads as unresolved, not answered.")
                + (kept.Any(answer => answer.Said == Opinions.Fixed)
                    ? " A commit named as a fix is checked when your turn ends: it must be one this turn made."
                    : ""),
                opinion with { Answers = standing });
        }, ct).ConfigureAwait(false);
    }

    /// <summary>The opinion under <paramref name="id"/> and where its pass stands; null where there is none, and on a shared host.</summary>
    public async Task<OpinionStanding?> ReadAsync(string id, DateTimeOffset now, CancellationToken ct = default)
    {
        if (!local) return null;
        return await opinions.FindAsync(id.Trim(), ct).ConfigureAwait(false) is { } opinion
            ? await StandingAsync(opinion, now, ct).ConfigureAwait(false)
            : null;
    }

    /// <summary>
    /// The opinions on <paramref name="working"/>'s work, the one <paramref name="session"/> reads as reviewer, or those of
    /// <paramref name="repository"/>, each filter only where named, oldest first, each with where its pass stands. None on a shared host.
    /// </summary>
    public async Task<IReadOnlyList<OpinionStanding>> ListAsync(
        string? working, string? session, string? repository, DateTimeOffset now, CancellationToken ct = default)
    {
        if (!local) return [];
        var standings = new List<OpinionStanding>();
        foreach (var opinion in await opinions.ListAsync(Blank(working), Blank(session), Blank(repository), ct: ct).ConfigureAwait(false))
        {
            standings.Add(await StandingAsync(opinion, now, ct).ConfigureAwait(false));
        }

        return standings;
    }

    /// <summary>
    /// Where a pass stands, read from its reviewer's record and never kept (design §6.1): given once said; reading while its
    /// session runs within its minutes; failed otherwise, out of time where the minutes ran out first, else ended.
    /// </summary>
    private async Task<OpinionStanding> StandingAsync(Opinion opinion, DateTimeOffset now, CancellationToken ct)
    {
        if (opinion.Given is not null) return new(opinion, Opinions.Given, null);
        var reviewer = await sessions.FindAsync(opinion.Session, ct).ConfigureAwait(false);
        if (reviewer is { Active: true }) return now > opinion.Due ? new(opinion, Opinions.Failed, Opinions.OutOfTime) : new(opinion, Opinions.Reading, null);
        return new(opinion, Opinions.Failed, reviewer is not null && reviewer.Updated > opinion.Due ? Opinions.OutOfTime : Opinions.Ended);
    }

    /// <summary>
    /// The bound of a pass (design §8.3), or null where it holds: the rule starts one first pass per working session; a
    /// recheck reads, once, the commits since a first pass of the same session's work, from its tip, once its findings went.
    /// </summary>
    private async Task<OpinionOutcome?> BoundAsync(OpinionAsk ask, Session working, string repository, CancellationToken inside)
    {
        if (ask.Pass == Opinions.First)
        {
            if (!Opinions.Automatic.Contains(ask.Occasion)) return null;
            var started = (await opinions.ListAsync(working: working.Id, ct: inside).ConfigureAwait(false))
                .FirstOrDefault(each => each.Pass == Opinions.First && Opinions.Automatic.Contains(each.Occasion));
            return started is null
                ? null
                : Refused(OpinionRefusal.Bound,
                    $"The rule already asked second opinion `{started.Id}` of session `{working.Id}`'s work: it starts one pass for a landing, "
                    + "and one recheck. Another is the person's to ask");
        }

        var rechecks = ask.Rechecks!.Trim();
        var first = await opinions.FindAsync(rechecks, inside).ConfigureAwait(false);
        if (first is null) return Refused(OpinionRefusal.NotFound, $"There is no second opinion `{rechecks}` on this machine to recheck");

        string? beyond =
            first.Pass != Opinions.First ? $"Second opinion `{first.Id}` is itself a recheck: a recheck reads a first pass's findings again, once"
            : first.Working != working.Id || first.Candidate.Repository != repository
                ? $"Second opinion `{first.Id}` read session `{first.Working}`'s work: a recheck reads the commits that session made in answer"
            : first.Given is null || first.Handed is null
                ? $"Second opinion `{first.Id}`'s findings have not gone to the working session: a recheck reads the commits made in answer to them"
            : (await opinions.ListAsync(rechecks: first.Id, ct: inside).ConfigureAwait(false)).FirstOrDefault() is { } done
                ? $"Second opinion `{first.Id}` was rechecked already, by `{done.Id}`: one recheck a pass, and what follows is the person's"
            : !string.Equals(first.Candidate.Tip, ask.Candidate.Base, StringComparison.OrdinalIgnoreCase)
                ? $"A recheck reads the commits since its first pass's tip, `{Short(first.Candidate.Tip)}`, and this one starts at "
                  + $"`{Short(ask.Candidate.Base)}`"
            : null;
        return beyond is null ? null : Refused(OpinionRefusal.Bound, beyond);
    }

    /// <summary>What the reviewer said, judged and made what is kept: numbered findings, its reading, its limits and a recheck's words.</summary>
    private async Task<(OpinionGiven? Given, string? Unfit)> JudgedAsync(
        Opinion opinion, IReadOnlyList<OpinionFinding?>? findings, string? read, string? limits, IReadOnlyList<OpinionRecheck?>? rechecked,
        DateTimeOffset now, CancellationToken inside)
    {
        var said = (findings ?? []).ToList();
        if (said.Count > Opinions.MostFindings) return (null, $"A pass gives at most {Opinions.MostFindings} findings, and this gave {said.Count}");

        var kept = new List<OpinionFinding>();
        foreach (var (finding, number) in said.Select((finding, index) => (finding, index + 1)))
        {
            if (finding is null) return (null, $"Finding {number} is empty");
            var trimmed = new OpinionFinding(
                finding.Weight?.Trim() ?? "", finding.Where?.Trim() ?? "", finding.Claim?.Trim() ?? "", finding.Consequence?.Trim() ?? "",
                finding.Reproduce?.Trim() ?? "", finding.Sure?.Trim() ?? "")
            {
                Number = number,
                Proposal = Blank(finding.Proposal),
            };
            if (Opinions.JudgeFinding(trimmed) is { } unfit) return (null, $"Finding {number}: {unfit}");
            kept.Add(trimmed);
        }

        var reading = read?.Trim() ?? "";
        if (reading.Length == 0) return (null, "`read` says what you read: the paths, the commits, and which checks you ran");
        if (reading.Length > Opinions.ReproduceLimit) return (null, $"`read` is at most {Opinions.ReproduceLimit} characters, and this is {reading.Length}");
        var notRead = Blank(limits);
        if (notRead is { Length: > Opinions.ReproduceLimit }) return (null, $"`limits` is at most {Opinions.ReproduceLimit} characters, and this is {notRead.Length}");

        var words = (rechecked ?? []).ToList();
        if (opinion.Pass == Opinions.First && words.Count > 0) return (null, "`rechecked` is a recheck's word on a first pass's findings, and this is a first pass");

        var firstPass = opinion.Pass == Opinions.Recheck && opinion.Rechecks is { } of
            ? (await opinions.FindAsync(of, inside).ConfigureAwait(false))?.Given?.Findings.Select(finding => finding.Number).ToList() ?? []
            : [];
        var rechecks = new List<OpinionRecheck>();
        foreach (var word in words)
        {
            if (word is null || !Opinions.Rechecks.Contains(word.Says?.Trim() ?? ""))
            {
                return (null, "`rechecked` says of a first-pass finding, by its number, that it `stands` or is `withdrawn`");
            }

            if (!firstPass.Contains(word.Finding)) return (null, $"The first pass gave no finding {word.Finding}: it gave {string.Join(", ", firstPass)}");
            if (rechecks.Any(each => each.Finding == word.Finding)) return (null, $"Finding {word.Finding} of the first pass is rechecked twice");
            rechecks.Add(new OpinionRecheck(word.Finding, word.Says!.Trim()));
        }

        return (new OpinionGiven(kept, reading, now) { Limits = notRead, Rechecked = rechecks }, null);
    }

    /// <summary>Why <paramref name="working"/> cannot take another agent's words now, or null when it can: an ended record of this machine's.</summary>
    private static string? CannotTake(Session? working, string id) => working switch
    {
        null or { Origin: not null } => $"There is no session `{id}` of this machine's to hand them to: they go to the person instead",
        { Ask: { } ask } => $"Session `{working.Id}` is the intake for ask `#{ask}`, which is answered through its ask",
        { Opinion: { } reads } => $"Session `{working.Id}` read the work for second opinion `{reads}`: a pass takes no words",
        { State: SessionState.StoodDown } => $"Session `{working.Id}` stood down, so it has nothing to go on with: they go to the person instead",
        { State: SessionState.AwaitingPerson } =>
            $"Session `{working.Id}` waits on the person's answer: another agent's findings reach it at a turn's end, once it is answered",
        { Active: true } =>
            $"Session `{working.Id}` is {Session.Spell(working.State)}: another agent's findings reach a running session at its turn's end, "
            + "never into a step in flight",
        _ => null,
    };

    private static OpinionOutcome Shared() => new(OpinionRefusal.Shared, Opinions.SharedSentence);

    private static OpinionOutcome Refused(OpinionRefusal refusal, string why) => new(refusal, $"{why}. Nothing was kept.");

    /// <summary>Who read it, as an opinion names its reviewer: its product, else its adapter, and its maker where declared.</summary>
    private static string Who(Opinion opinion) =>
        (opinion.Reviewer.Product ?? opinion.Reviewer.Adapter) + (opinion.Reviewer.Maker is { } maker ? $" ({maker})" : "");

    private static string Weighed(IReadOnlyList<OpinionFinding> findings) =>
        string.Join(", ", Opinions.Weights
            .Select(weight => (weight, count: findings.Count(finding => finding.Weight == weight)))
            .Where(each => each.count > 0)
            .Select(each => $"{each.count} {each.weight}"));

    private static string Count(int count, string noun) => count == 1 ? $"1 {noun}" : $"{count} {noun}s";

    private static string Short(string commit) => commit.Length > 7 ? commit[..7] : commit;

    private static string Moment(DateTimeOffset at) => at.UtcDateTime.ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture);

    /// <summary>A new opinion's or word's id: random, as a session's is.</summary>
    private static string NewId() => Guid.NewGuid().ToString("N")[..8];

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
