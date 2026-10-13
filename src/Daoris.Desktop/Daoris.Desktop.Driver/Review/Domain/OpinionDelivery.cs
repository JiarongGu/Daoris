namespace Daoris.Driver;

/// <summary>
/// One finding as the driver read its answer when the working session's turn ended (XAGENT1e, D155 point 7; design §6.4): what
/// the session said, and what counts. A fix counts only where its commit is one the turn made, read from git; a finding nobody
/// answered counts as <c>unresolved</c>, said as not answered.
/// </summary>
/// <param name="Counts"><c>fixed</c> (its commit checked), <c>rejected</c>, or <c>unresolved</c>.</param>
public sealed record OpinionAnswerRead(int Finding, string Weight, string Counts)
{
    /// <summary>What the session said: <c>fixed</c>, <c>rejected</c> or <c>unresolved</c>; null where it said nothing.</summary>
    public string? Said { get; init; }

    /// <summary>A fix's commit, as the session named it.</summary>
    public string? Commit { get; init; }

    /// <summary>A fix's commit by its full id, where git read it as one the turn made; null otherwise.</summary>
    public string? Fix { get; init; }

    /// <summary>Why it does not count as said, a code of <see cref="OpinionAnswerWhy"/>; null where it counts as said.</summary>
    public string? Why { get; init; }

    /// <summary>
    /// A <c>must</c> the working session did not fix (design §6.6): the person's to answer. A recheck may withdraw it
    /// (<see cref="OpinionDisputes"/>).
    /// </summary>
    public bool Disputed => Weight == OpinionViews.Must && Counts != OpinionViews.Fixed;
}

/// <summary>
/// The working session's answers as the driver read them when its turn ended (XAGENT1e; design §6.4): one row per finding, the
/// tree's tip they were read at, and when. The facts a recheck and the gate (XAGENT1f) read; the answers themselves stay the
/// local host's.
/// </summary>
/// <param name="Tip">The working tree's tip as the turn ended, by its full id; null where git could not read it.</param>
public sealed record OpinionReading(string Session, string? Tip, DateTimeOffset At, IReadOnlyList<OpinionAnswerRead> Findings)
{
    /// <summary>Whether any finding counts as fixed: what a recheck reads (design §6.5).</summary>
    public bool AnyFixed => Findings.Any(finding => finding.Counts == OpinionViews.Fixed);
}

/// <summary>Why an answer does not count as said (XAGENT1e; design §6.4), each with the line the driver says.</summary>
public static class OpinionAnswerWhy
{
    /// <summary>The working session's turn ended and it gave this finding no answer: unresolved.</summary>
    public const string NotAnswered = "not-answered";

    /// <summary>The fix named a commit its tree does not hold.</summary>
    public const string FixUnknown = "fix-unknown";

    /// <summary>The fix named a commit that is not after the work the other agent read.</summary>
    public const string FixNotAfter = "fix-not-after";

    /// <summary>The fix named a commit that is not on the working session's branch, at or before its tip.</summary>
    public const string FixOffBranch = "fix-off-branch";

    /// <summary>git could not say: its tree is gone, or git did not answer.</summary>
    public const string FixUnread = "fix-unread";

    /// <summary>The line for <paramref name="code"/>, in the driver's English.</summary>
    public static string Said(string code) => code switch
    {
        NotAnswered => "not answered",
        FixUnknown => "fixed, its commit said, but its tree holds no commit by that name",
        FixNotAfter => "fixed, its commit said, but that commit is not after the work the other agent read",
        FixOffBranch => "fixed, its commit said, but that commit is not on the session's branch",
        FixUnread => "fixed, its commit said, but git could not read its tree to check it",
        _ => code,
    };
}

/// <summary>Where an opinion stands as the driver delivers it (XAGENT1e; design §6.3–§6.7, §8.5), one of these words.</summary>
public static class OpinionDeliveryStates
{
    /// <summary>The reviewer is still reading.</summary>
    public const string Reading = "reading";

    /// <summary>The pass ended without an opinion, or ran out of time: <i>Try again</i>, with the reason. Never <i>no issues</i>.</summary>
    public const string TryAgain = "try-again";

    /// <summary>The reviewer raised nothing in what it read: said with what it read, never as <i>no issues</i>.</summary>
    public const string RaisedNothing = "raised-nothing";

    /// <summary>The working session runs or waits on the person: the findings reach it at its turn's end, never mid-step.</summary>
    public const string Waiting = "waiting";

    /// <summary>The findings were handed to the working session, which goes on with them at the driver's next look.</summary>
    public const string Handed = "handed";

    /// <summary>The working session has them: it is answering, or its turn is about to take them.</summary>
    public const string WithSession = "with-session";

    /// <summary>Its turn ended, and its answers were read.</summary>
    public const string Answered = "answered";

    /// <summary>The findings went to the person: the working session could not take them, or they are a recheck's.</summary>
    public const string ToPerson = "to-person";

    /// <summary>The service holds no such opinion.</summary>
    public const string Unknown = "unknown";
}

/// <summary>
/// Why findings went to the person instead of the working session (XAGENT1e; design §6.7), beside <see cref="ContinueWhy"/>'s
/// codes for a session that could not go on.
/// </summary>
public static class OpinionToPerson
{
    /// <summary>No record of this machine's by the working session's id.</summary>
    public const string NotFound = "not-found";

    /// <summary>A recheck's findings go to the person, never back to the working session by themselves (design §6.5).</summary>
    public const string Recheck = "recheck";

    /// <summary>
    /// The work was a conversation's (design §12.2): a chat lands nothing by the driver's gate, and its runner would take another
    /// agent's claims as the person's words, so they go to the person.
    /// </summary>
    public const string Conversation = "conversation";

    /// <summary>
    /// The line for <paramref name="code"/>, this list's or <see cref="ContinueWhy"/>'s for a session that could not go on, in
    /// the driver's English.
    /// </summary>
    public static string Said(string code) => code switch
    {
        NotFound => "there is no session of this machine's by its id",
        Recheck => "a recheck's findings go to the person, never back to the working session by themselves",
        Conversation => "the work was a conversation's, which another agent's findings do not reach by themselves",
        // A reason whose line names an adapter is said by its code here: the session's own note says it whole.
        ContinueWhy.Adapter or ContinueWhy.Unable => $"its session could not go on ({code})",
        _ => Fixed(code),
    };

    private static string Fixed(string code)
    {
        try
        {
            return ContinueWhy.Of(code).Sentence;
        }
        catch (ArgumentOutOfRangeException)
        {
            return code;
        }
    }
}

/// <summary>What the driver did with one opinion, and what it learned (XAGENT1e): kept beside the opinion's packet.</summary>
/// <param name="Person">Why its findings went to the person, a code; null where they did not.</param>
public sealed record OpinionDelivered(string? Person, DateTimeOffset? PersonAt, OpinionReading? Answered)
{
    /// <summary>The recheck the driver asked of it, by its opinion's id; null where none was asked.</summary>
    public string? Recheck { get; init; }

    /// <summary>Why no recheck was asked, a code of <see cref="OpinionRechecks"/> or the choice's; null where one was, or none judged yet.</summary>
    public string? RecheckWhy { get; init; }

    /// <summary>Nothing done yet.</summary>
    public static OpinionDelivered None { get; } = new(null, null, null);
}

/// <summary>
/// The working session's answers, read when its turn ends (XAGENT1e, D155 point 7; design §6.4): each finding's latest answer,
/// a fix's commit checked against the tree's history, and a finding with none read as <c>unresolved</c>, not answered. What a
/// fix fixes is the session's claim; that its commit is one the turn made is a fact git answers.
/// </summary>
/// <remarks>
/// The lines are here; the reading of the tree, <c>ReadAsync</c> and <c>CheckFixAsync</c>, is Review's Infrastructure's, named
/// through this type as before (<see cref="OpinionAnswerGit"/>; STRUCT3c).
/// </remarks>
public static class OpinionAnswers
{
    /// <summary>
    /// The driver's line in the working session's conversation once its answers are read: whose findings they were, and how
    /// each counts. The person's view of the answers beside the claims is the page's (XAGENT1g); this is the record's.
    /// </summary>
    public static string Line(OpinionView opinion, OpinionReading reading) => Told(opinion, reading).Note;

    /// <summary>
    /// <see cref="Line"/> by code (CONVNOTE2h), the English byte for byte: a lead for whose findings and how many, then a part for
    /// each way they count that is above nought, in the English's order, and the disputed <c>must</c> findings last.
    /// </summary>
    public static Noted Told(OpinionView opinion, OpinionReading reading)
    {
        int Count(Func<OpinionAnswerRead, bool> which) => reading.Findings.Count(which);
        var fixedCount = Count(row => row.Counts == OpinionViews.Fixed);
        var rejected = Count(row => row.Counts == OpinionViews.Rejected);
        var unresolved = Count(row => row.Counts == OpinionViews.Unresolved && row.Why is null);
        var unanswered = Count(row => row.Why == OpinionAnswerWhy.NotAnswered);
        var notMade = Count(row => row.Said == OpinionViews.Fixed && row.Fix is null);
        var disputed = Count(row => row.Disputed);
        var counts = new List<Noted>();
        void Add(NoteCode code, int count, string text)
        {
            if (count > 0) counts.Add(Noted.Of(code, text, ("count", count)));
        }

        Add(NoteCodes.OpinionFixed, fixedCount, $"{fixedCount} fixed, each commit read from git as one this turn made");
        Add(NoteCodes.OpinionUnproven, notMade, $"{notMade} said fixed whose commit git did not read as one this turn made, read as unresolved");
        Add(NoteCodes.OpinionRejected, rejected, $"{rejected} rejected with its evidence");
        Add(NoteCodes.OpinionUnresolved, unresolved, $"{unresolved} unresolved");
        Add(NoteCodes.OpinionUnanswered, unanswered, $"{unanswered} not answered, read as unresolved");

        var noted = new Noted("— ", []).Then(
            "",
            Noted.Of(
                NoteCodes.OpinionAnswersLead, $"the answers to {opinion.Who}'s {Plural(reading.Findings.Count, "finding")}, as its turn ended:",
                ("who", opinion.Who), ("count", reading.Findings.Count)));
        for (var each = 0; each < counts.Count; each++) noted = noted.Then(each == 0 ? " " : "; ", counts[each]);
        // The English has always closed an empty list as ": ." (a space with nothing after it).
        noted = noted.Then(counts.Count == 0 ? " ." : ".");
        return disputed > 0
            ? noted.Then(
                " ",
                Noted.Of(
                    NoteCodes.OpinionDisputed, $"{Plural(disputed, "`must` finding")} not fixed {(disputed == 1 ? "waits" : "wait")} for the person",
                    ("count", disputed))).Then(".")
            : noted;
    }

    /// <summary>
    /// How the reviewer's own run ended, by code (XAGENT1g4a, D142): the English as <c>Driver.Read</c> has always worded it, a
    /// failed turn's own message beneath as the program's words (it may hold a path).
    /// </summary>
    public static Noted Concluded(string opinion, int exitCode, string? turnFailed) =>
        turnFailed is { Length: > 0 } failed
            ? Noted.Of(
                    NoteCodes.OpinionReviewerFailed, $"the reviewer's turn failed as it read the work for second opinion `{opinion}`:", ("opinion", opinion))
                .Then(" ", Noted.Said(failed, NoteBy.Program))
            : exitCode == 0
                ? Noted.Of(NoteCodes.OpinionReviewerRead, $"it read the work and ended; what it said is second opinion `{opinion}`'s.", ("opinion", opinion))
                : Noted.Of(
                    NoteCodes.OpinionReviewerExited,
                    $"it ended with exit {exitCode} as it read the work; what it said, if anything, is second opinion `{opinion}`'s.",
                    ("opinion", opinion), ("exit", exitCode));

    private static string Plural(int count, string noun) => count == 1 ? $"1 {noun}" : $"{count} {noun}s";
}

/// <summary>
/// The one recheck (XAGENT1e, D155 point 7; design §6.5): whether it is due, and what it is handed. One pass reads the commits
/// since the first pass's tip, once, and only where the working session answered a finding fixed with a commit its turn made;
/// its findings go to the person, never back to the working session by themselves.
/// </summary>
public static class OpinionRechecks
{
    /// <summary>The rule turns the recheck off.</summary>
    public const string Off = "off";

    /// <summary>A recheck is never rechecked.</summary>
    public const string NotFirst = "not-first";

    /// <summary>The working session's turn has not ended, so nothing is read yet.</summary>
    public const string NotAnswered = "not-answered";

    /// <summary>No finding was answered fixed with a commit its turn made: nothing new to read for it.</summary>
    public const string NoFix = "no-fix";

    /// <summary>The pass that would have read it again held or was refused before it opened, for the reason its line said.</summary>
    public const string Refused = "refused";

    /// <summary>
    /// Null where a recheck is due (design §6.5): a first pass whose working session's answers were read, one of them a fix
    /// whose commit the turn made, under a rule that keeps the recheck. Else why not, by code.
    /// </summary>
    public static string? WhyNot(OpinionView first, OpinionReading? reading, OpinionRule rule) =>
        !first.First ? NotFirst
        : !rule.Recheck ? Off
        : reading is null ? NotAnswered
        : !reading.AnyFixed || reading.Tip is null ? NoFix
        : null;

    /// <summary>The line for <paramref name="code"/>, in the driver's English.</summary>
    public static string Said(string code) => code switch
    {
        Off => "the rule turns the recheck off",
        NotFirst => "a recheck is never rechecked",
        NotAnswered => "the working session has not answered yet",
        NoFix => "no finding was answered fixed with a commit its turn made, so there is nothing new to read",
        Refused => "the pass that would have read it again did not open",
        // The reviewer choice's codes (§3.3): nobody could read it again, said by the code the choice gave.
        _ => $"no reviewer could read it again ({code})",
    };
}

/// <summary>
/// What waits for the person once the opinion and its recheck are read (design §6.6, §8.2): a first-pass <c>must</c> the
/// working session did not fix and the recheck did not withdraw, and every <c>must</c> the recheck raised. A <c>should</c> or a
/// <c>note</c> is shown and waits for nothing; two agents agreeing establish nothing.
/// </summary>
/// <param name="First">The first pass's disputed findings, by number.</param>
/// <param name="Raised">The recheck's own <c>must</c> findings, by number.</param>
public sealed record OpinionDisputes(IReadOnlyList<int> First, IReadOnlyList<int> Raised)
{
    public int Count => First.Count + Raised.Count;

    /// <summary>The disputes of <paramref name="reading"/>, with <paramref name="recheck"/>'s word where one was given.</summary>
    public static OpinionDisputes Of(OpinionReading reading, OpinionView? recheck)
    {
        var said = recheck is { Given: true } given ? given : null;
        return new OpinionDisputes(
            [.. reading.Findings.Where(row => row.Disputed && said?.Rechecked.GetValueOrDefault(row.Finding) != OpinionViews.Withdrawn).Select(row => row.Finding)],
            [.. (said?.Findings ?? []).Where(finding => finding.Weight == OpinionViews.Must).Select(finding => finding.Number)]);
    }

    /// <summary>
    /// The disputes of findings that never reached the working session (design §6.7): each stands unanswered, and each
    /// <c>must</c> is disputed.
    /// </summary>
    public static OpinionDisputes Unanswered(OpinionView opinion) =>
        new([.. (opinion.Findings ?? []).Where(finding => finding.Weight == OpinionViews.Must).Select(finding => finding.Number)], []);
}
