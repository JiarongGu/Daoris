using System.Text.Json;
using static Daoris.Driver.Json;

namespace Daoris.Driver;

/// <summary>
/// One branch a landing made: the repository, the branch, the line it grew from, the commit it is
/// made at, and the session, quest and title it is made for — what the clean-up judges and the hand-off
/// tells a plugin.
/// </summary>
/// <param name="Line">The line the work grew from when it landed, or null where git named none.</param>
/// <param name="Tip">The commit the landing made the branch at. A branch whose history does not hold it is not the landing's.</param>
public sealed record LandedBranch(
    string Repository, string Workspace, string Branch, string? Line, string Tip,
    string Session, string? Quest, string? Title, DateTimeOffset LandedAt)
{
    /// <summary>The plugin that last pushed it — at the landing or a hand-off after it — or null.</summary>
    public string? Plugin { get; init; }

    /// <summary>Whether a plugin answered that it pushed the branch.</summary>
    public bool Pushed { get; init; }

    /// <summary>The pull request that plugin answered with, or null.</summary>
    public string? PullRequest { get; init; }

    /// <summary>The branch's commit when the plugin pushed it: a hand-off at the same commit would push nothing new.</summary>
    public string? PushedTip { get; init; }

    /// <summary>
    /// The commit its work grew from: the start of the session branch it is made from, where that is
    /// recorded, else null. Bringing it up to date cuts here, so only its own commits are replayed.
    /// </summary>
    public string? From { get; init; }

    /// <summary>
    /// When this machine found the branch gone, or no longer the landing's, or removed it; null while it
    /// stands. A trace is never judged, handed on or moved again: it is kept so the review of the session that landed it
    /// can still say where its work went.
    /// </summary>
    public DateTimeOffset? GoneAt { get; init; }

    /// <summary>What the clean-up proved when it removed the branch (<see cref="LandedKind"/>: on the line, merged, inside another); null where Daoris did not remove it.</summary>
    public string? RemovedAs { get; init; }

    /// <summary>Where that proof held: the form of the line, or the landed branch it is inside.</summary>
    public string? RemovedOn { get; init; }

    /// <summary>
    /// Why this machine found it gone where Daoris did not remove it (<see cref="LandedGone"/>): git said its branch is missing,
    /// another branch holding its recorded tip or not, or a branch of its name stands that is not the landing's. Null where Daoris removed
    /// it, while it stands, and for a trace from
    /// before it is kept, which the look asks git about once (<see cref="LandedCleanups.MarkAbsentAsync"/>).
    /// </summary>
    public string? GoneAs { get; init; }

    /// <summary>
    /// Where <see cref="GoneAs"/> is <see cref="LandedGone.Moved"/>: the local branch that held its recorded tip
    /// when git said its own branch is missing. The look asks again once that branch is gone too.
    /// </summary>
    public string? MovedTo { get; init; }

    /// <summary>
    /// The commit <see cref="MovedTo"/> stood at when it is found holding the recorded tip: while it stands there it
    /// still holds it, read from the refs the look holds; once it moved, or its name is made again elsewhere, the look asks again.
    /// </summary>
    public string? MovedToAt { get; init; }

    /// <summary>
    /// Where <see cref="GoneAs"/> is <see cref="LandedGone.Missing"/> and no ref held its recorded tip: the recovery
    /// ref Daoris kept that commit on before the record is retired, so the record never goes as the only breadcrumb to it.
    /// </summary>
    public string? KeptOn { get; init; }

    /// <summary>
    /// Where <see cref="GoneAs"/> is <see cref="LandedGone.Withdrawn"/>: why the person or the steward said the line will never
    /// hold this landing's work, in their words, kept with the trace. Null everywhere else, and in a record from before it is kept.
    /// </summary>
    public string? GoneWhy { get; init; }

    /// <summary>
    /// Who accepted it, one of <see cref="Daoris.Driver.AcceptedBy"/>: the person's press, or the rule's switch at
    /// the quest's done. Null for a landing recorded before it is kept, which a reader says is not kept.
    /// </summary>
    public string? AcceptedBy { get; init; }

    /// <summary>The landing rule it is made under, as it stood then; null for one recorded before it is kept.</summary>
    public LandedRule? Rule { get; init; }

    /// <summary>
    /// The acceptance's provenance: that <c>integrate</c> stood when its first landing is
    /// accepted, its <c>request</c> there, and whether it is the rule's step or the bound version's. Null is a record that says neither:
    /// none stood, or it is written before the field or by a build that rewrote the file without it, which can only under-admit.
    /// </summary>
    public LandedIntegrate? Integrate { get; init; }

    /// <summary>
    /// The review that let its first landing go: <c>reviewed</c> with the environment and
    /// the set-up's commit, or <c>skipped</c> with the person's words. Null where no review is asked, and for one recorded before.
    /// </summary>
    public LandingReview? Review { get; init; }

    /// <summary>
    /// The second opinion that let its first landing go: settled, or the
    /// person's answer, with the reviewer, the candidate, the passes and the findings counted; or why it landed with none. Null
    /// where none is asked, and for one recorded before.
    /// </summary>
    public LandingOpinion? Opinion { get; init; }

    /// <summary>
    /// The named workflow its first landing went by: its id, the version its run kept, the
    /// level that chose it and the task's kind. Null under Current, and for one recorded before.
    /// </summary>
    public LandingWorkflow? Workflow { get; init; }

    /// <summary>
    /// Each time a later done moved it on, oldest first: a chain's later step, or a session that went on
    /// after its landing. <see cref="Tip"/> is the newest one's <see cref="LandedAdvance.To"/>.
    /// </summary>
    public IReadOnlyList<LandedAdvance> Advances { get; init; } = [];

    /// <summary>
    /// The latest answer about its pull request, with the plugin and when it is asked: what a
    /// removal on the platform's word chose by. <c>completed</c> is final. Null where it is never answered.
    /// </summary>
    public PullRequestState? PullRequestState { get; init; }

    /// <summary>The latest ask that failed, kept while no answer is newer; null where none failed since the last answer.</summary>
    public PullRequestAskFailed? PullRequestAskFailed { get; init; }

    /// <summary>Each session branch removed on its pull request's answer, oldest first.</summary>
    public IReadOnlyList<CarriedBranch> Carried { get; init; } = [];

    /// <summary>
    /// The session's tip the landing squashed into <see cref="Tip"/>, moved on by a
    /// chain's later squash; null where the branch holds the session's own commits, and for one recorded before. Daoris's own
    /// word of what it squashed: while the entry stands, a session branch at this commit or an ancestor of it is landed.
    /// </summary>
    public string? Squashed { get; init; }

    /// <summary>That the line held the branch's work, and the evidence; null where not yet judged held. Written only by <see cref="EndKeeping"/>.</summary>
    public HeldFact? Held { get; init; }

    /// <summary>
    /// The branch work units whose work it carries: the landing's own session's and each advance's, where its
    /// entry had one. A landing carrying several sessions' work is one result of several units, never a unit itself.
    /// </summary>
    public IReadOnlyList<string> Units { get; init; } = [];

    /// <summary>
    /// The quest its own session served: <see cref="Quest"/> names the one it is named for, a
    /// chain's first, which need not be its session's. Null where the session served none, and in a record from before.
    /// </summary>
    public string? Served { get; init; }

    /// <summary>
    /// The submission its own session's acceptance is made on, by its machine and sequence, as the door read it; null
    /// for a done, a conversation, and a record from before.
    /// </summary>
    public QuestSubmissionRef? Submission { get; init; }

    /// <summary>
    /// The sessions a combined Accept brought into this landing, each at the tip it is bound at: carried by its work
    /// and its push, never named as landed. None in a record from before.
    /// </summary>
    public IReadOnlyList<LandedWork> Brought { get; init; } = [];

    /// <summary>What a run's <c>tidy</c> ended of its chain once its pull request merged; null where no tidy ran.</summary>
    public ChainTidied? Tidied { get; init; }

    /// <summary>
    /// The work of each session it carries, oldest first: its own session's, then each advance's,
    /// each with the commit the branch stood at once that work is on it, and the quest, the unit and the submission the record keeps for
    /// it, null where it keeps none. The sessions brought into a landing or an advance follow its own, at its commit.
    /// </summary>
    public IReadOnlyList<LandedWork> Work()
    {
        var first = Advances.Count > 0 ? Advances[0].From : Tip;
        var work = new List<LandedWork>(Advances.Count + 1)
        {
            new(Session, first) { Quest = Served, Unit = OwnUnit(), Submission = Submission },
        };
        work.AddRange(Brought.Select(each => each with { Tip = first }));
        foreach (var advance in Advances)
        {
            work.Add(new(advance.Session, advance.To) { Quest = advance.Quest, Unit = advance.Unit, Submission = advance.Submission });
            work.AddRange(advance.Brought.Select(each => each with { Tip = advance.To }));
        }

        return work;
    }

    /// <summary>
    /// The work no push of it has carried yet: all of it where no plugin answered that it pushed the branch, else what
    /// came after the commit it pushed. A pushed commit that is none of theirs leaves all of it unpushed, the side that judges more.
    /// </summary>
    public IReadOnlyList<LandedWork> Unpushed()
    {
        var work = Work();
        if (!Pushed || PushedTip is not { } pushed) return work;
        var at = work.Count - 1;
        while (at >= 0 && !string.Equals(work[at].Tip, pushed, StringComparison.OrdinalIgnoreCase)) at--;
        return at < 0 ? work : [.. work.Skip(at + 1)];
    }

    /// <summary>
    /// Its own session's branch work unit as the record says it: the first it carries, where every advance names its
    /// own and they account for the rest, in the order each is added. Null where it is not said: an advance from before names none,
    /// another session's advance names that first unit (which the record cannot tell from a landing whose own session had none), or the
    /// units are not the ones its advances added.
    /// </summary>
    private string? OwnUnit()
    {
        if (Units.Count == 0) return null;
        var own = Units[0];
        var added = new List<string> { own };
        foreach (var advance in Advances)
        {
            if (advance.Unit is not { } unit) return null;
            if (string.Equals(unit, own, StringComparison.Ordinal) && !string.Equals(advance.Session, Session, StringComparison.OrdinalIgnoreCase)) return null;
            if (!added.Contains(unit, StringComparer.Ordinal)) added.Add(unit);
        }

        return added.SequenceEqual(Units, StringComparer.Ordinal) ? own : null;
    }

    /// <summary>Whether this landing names <paramref name="session"/>: the session that made it, or one whose done moved it on.</summary>
    public bool Names(string session) =>
        string.Equals(Session, session, StringComparison.OrdinalIgnoreCase)
        || Advances.Any(advance => string.Equals(advance.Session, session, StringComparison.OrdinalIgnoreCase));

    /// <summary>When <paramref name="session"/>'s work is accepted onto it: its newest advance, else the landing itself.</summary>
    public DateTimeOffset AcceptedAt(string session) =>
        Advances.LastOrDefault(advance => string.Equals(advance.Session, session, StringComparison.OrdinalIgnoreCase))?.At ?? LandedAt;

    /// <summary>Who accepted <paramref name="session"/>'s work onto it (<see cref="Daoris.Driver.AcceptedBy"/>): its newest advance's, else the landing's.</summary>
    public string? AcceptedByOf(string session) =>
        Advances.LastOrDefault(advance => string.Equals(advance.Session, session, StringComparison.OrdinalIgnoreCase)) is { } advance
            ? advance.AcceptedBy
            : AcceptedBy;

    /// <summary>
    /// The session whose work last moved it: its newest advance's, else the landing's own. Whose acceptance a hand-off tells its plugin,
    /// and whose quest a person's hand-off is held to. A method, so no answer that carries a landing gains a field.
    /// </summary>
    public string LastSession() => Advances.Count > 0 ? Advances[^1].Session : Session;
}


/// <summary>
/// One fast-forward of a landed branch: from the commit it stood at to the one it moves to,
/// when, the session whose done moved it, and who accepted that done.
/// </summary>
public sealed record LandedAdvance(string From, string To, DateTimeOffset At, string Session)
{
    /// <summary>One of <see cref="Daoris.Driver.AcceptedBy"/>, or null in a record that did not keep it.</summary>
    public string? AcceptedBy { get; init; }

    /// <summary>
    /// This advance's own acceptance provenance, as it keeps its own <see cref="AcceptedBy"/>; null where
    /// no <c>integrate</c> stood, and in a record from before.
    /// </summary>
    public LandedIntegrate? Integrate { get; init; }

    /// <summary>The review that let this advance go; null where none is asked, and in a record from before.</summary>
    public LandingReview? Review { get; init; }

    /// <summary>The second opinion that let this advance go; null where none is asked, and in a record from before.</summary>
    public LandingOpinion? Opinion { get; init; }

    /// <summary>The named workflow this advance went by; null under Current, and in a record from before.</summary>
    public LandingWorkflow? Workflow { get; init; }

    /// <summary>
    /// The session's tip this advance squashed into <see cref="To"/>, which the entry's
    /// <see cref="LandedBranch.Squashed"/> moves to; null for a fast-forward, and in a record from before.
    /// </summary>
    public string? Squashed { get; init; }

    /// <summary>
    /// The quest its session served, which a hand-off and a push judge it on and its unit is
    /// linked to; null where the session served none, and in a record from before.
    /// </summary>
    public string? Quest { get; init; }

    /// <summary>
    /// Its session's branch work unit, which <see cref="LandedBranch.Units"/> gains where it is new; null where the
    /// session's tree had none, and in a record from before.
    /// </summary>
    public string? Unit { get; init; }

    /// <summary>
    /// The submission its acceptance is made on, by its machine and sequence: one made again since holds a person's
    /// hand-off of its work back. Null for a done, and in a record from before, whose session is judged only on its result being in.
    /// </summary>
    public QuestSubmissionRef? Submission { get; init; }

    /// <summary>The sessions a combined Accept brought into this advance, each at its bound tip; none from before.</summary>
    public IReadOnlyList<LandedWork> Brought { get; init; } = [];
}


/// <summary>
/// One session's work a landed branch carries (<see cref="LandedBranch.Work"/>): the session, the
/// commit the branch stood at once its work is on it, and what the record keeps of it, each null where it keeps none.
/// </summary>
public sealed record LandedWork(string Session, string Tip)
{
    /// <summary>The quest the session served; null where it served none, or the record does not say.</summary>
    public string? Quest { get; init; }

    /// <summary>Its branch work unit; null where the record does not say which.</summary>
    public string? Unit { get; init; }

    /// <summary>The submission its acceptance is made on; null for a done, or where the record does not say.</summary>
    public QuestSubmissionRef? Submission { get; init; }
}


/// <summary>Who accepted a landing: the person's press, or the rule's switch at the quest's done.</summary>
public static class AcceptedBy
{
    /// <summary>The person pressed Accept: the review's, a terminal's <c>trees land</c>, or an Ask Daoris card.</summary>
    public const string Person = "person";

    /// <summary>The rule accepts automatically (<c>autoAccept</c>), and the quest's done landed it at a look.</summary>
    public const string Auto = "auto";
}


/// <summary>The landing rule a landing is made under, as it stood then.</summary>
/// <param name="Plugin">The plugin the rule named, whether or not it pushed; null where the person pushes.</param>
/// <param name="Source">Where the rule came from, one of <see cref="LandingSource"/>.</param>
public sealed record LandedRule(string? Plugin, bool AutoAccept, string Source)
{
    /// <summary>
    /// Where the access setup that turned the rule's switch on is set (<see cref="Landing.Access"/>): with
    /// <see cref="AcceptedBy.Auto"/>, the setup accepted it, not the rule's own switch. Null where the rule's own switch stood, and in a
    /// record from before: the file keeps it as <c>access</c> beside <c>source</c>, only where it is set.
    /// </summary>
    public string? Access { get; init; }
}


/// <summary>
/// An acceptance's provenance: that <c>integrate</c> stood where a landing or an
/// advance is accepted, with the <c>request</c> it said there and whose step it is. Written beside <c>acceptedBy</c> and the rule, as
/// <c>integrate</c> <c>{ request, source }</c>, only where one stood; an older reader passes the key over, and a build that rewrites the
/// file without it leaves a record that says neither. The door's node route reads it with <c>acceptedBy</c> to judge whether the acceptance
/// is the request; nothing judges it here.
/// </summary>
/// <param name="Request">The <c>request</c> that stood (<c>owner</c>, <c>done</c> or <c>steward</c>), kept as written for its reader to judge.</param>
/// <param name="Source">Whose step it is: <see cref="Rule"/>, Current's under a rule carrying <c>integrate</c>, or <see cref="Version"/>.</param>
public sealed record LandedIntegrate(string Request, string Source)
{
    /// <summary>The effective landing rule's <c>integrate</c>, under Current.</summary>
    public const string Rule = "rule";

    /// <summary>The bound named version's own <c>integrate</c> step.</summary>
    public const string Version = "version";

    /// <summary>
    /// What a landing under <paramref name="integrate"/> keeps: its <c>request</c> through the <i>Absent</i> column, the rule
    /// its source; none where the rule carries no <c>integrate</c>. Under a named version the rule's <c>request</c> is not the unit's
    /// process, its own <c>integrate</c> step is: its <paramref name="versionRequest"/> (the node's <c>request</c>), with
    /// <see cref="Version"/> its source, and none where the version holds no such step.
    /// </summary>
    public static LandedIntegrate? Of(LandingIntegrate? integrate, bool named, string? versionRequest = null) =>
        named ? (versionRequest is null ? null : new(versionRequest, Version))
        : integrate is null ? null
        : new(integrate.Resolved().Request ?? LandingIntegrate.Owner, Rule);
}


/// <summary>One try to land a due session, and the facts it is made at: never a sentence, a path or a word of anyone's.</summary>
/// <param name="Code">One of <see cref="LandingCode"/>.</param>
public sealed record AutoTry(DateTimeOffset At, string Code)
{
    /// <summary>The tree's HEAD when it is tried, or null where none is read.</summary>
    public string? Tip { get; init; }

    /// <summary>The tree's status when it is tried (<see cref="LandTries.Fingerprint"/>), or null where none is read.</summary>
    public string? Status { get; init; }

    /// <summary>How many paths were uncommitted, where that is what held it.</summary>
    public int? Uncommitted { get; init; }

    /// <summary>The branch it made, or the one it found standing.</summary>
    public string? Branch { get; init; }

    /// <summary>How many commits it carried, where it landed.</summary>
    public int? Commits { get; init; }
}


/// <summary>
/// A run's work as the performer of its <c>land</c> judges it (<see cref="LandPerformer"/>): its session, its quest, where it ran, the tree
/// it ran in, when it is judged, and the last try kept of it.
/// </summary>
/// <param name="Tree">The tree its record names, under this home's trees: machine-local.</param>
public sealed record LandEntry(string Session, string Quest, string Repository, string Workspace, string Tree, DateTimeOffset DueAt)
{
    public IReadOnlyList<AutoTry> Tries { get; init; } = [];

    /// <summary>Its last try, or null before the first.</summary>
    public AutoTry? Last => Tries.Count == 0 ? null : Tries[^1];
}


/// <summary>
/// A plugin's answer at <see cref="HookPoints.State"/>, and, kept on the landing entry as
/// <c>pullRequestState</c>, the plugin that gave it and when it is asked.
/// </summary>
/// <param name="State">One of <see cref="PullRequestStates"/>.</param>
public sealed record PullRequestState(string State)
{
    /// <summary>The pull request it answered for, as an absolute web address, or null.</summary>
    public string? PullRequest { get; init; }

    /// <summary>The full id of the commit the completion put on its target; set for every completed answer.</summary>
    public string? MergeCommit { get; init; }

    /// <summary>The full id of the source commit the completion merged; set for every completed answer.</summary>
    public string? SourceCommit { get; init; }

    /// <summary>The branch it merged into, without <c>refs/heads/</c>, or null.</summary>
    public string? Target { get; init; }

    /// <summary>One of <see cref="MergeHow"/>, or null.</summary>
    public string? How { get; init; }

    /// <summary>When it completed or is abandoned, or null.</summary>
    public DateTimeOffset? At { get; init; }

    /// <summary>The plugin's own sentence, kept to <see cref="PullRequestAnswers.MessageLimit"/> characters, or null.</summary>
    public string? Message { get; init; }

    /// <summary>The plugin that answered.</summary>
    public string? Plugin { get; init; }

    /// <summary>When Daoris asked; <see cref="DateTimeOffset.MinValue"/> where a record did not keep it.</summary>
    public DateTimeOffset AskedAt { get; init; }
}


/// <summary>
/// The latest ask that failed, kept as <c>pullRequestAskFailed</c> while no answer is newer: one of
/// <see cref="PluginEvents"/>' failure kinds, the plugin, and when. A failure never overwrites an answer.
/// </summary>
public sealed record PullRequestAskFailed(string Code, string Plugin, DateTimeOffset At);

/// <summary>A session branch removed on a completed answer: its tip, when, and which occasion removed it.</summary>
/// <param name="By"><see cref="CarriedBy.Tidy"/> or <see cref="CarriedBy.CleanUp"/>.</param>
public sealed record CarriedBranch(string Branch, string Tip, DateTimeOffset At, string By);


/// <summary>What a clear would take now.</summary>
public sealed record HistoryTakes(int Quests, int Asks, int Sessions, int Teammates, long Bytes)
{
    public static HistoryTakes None { get; } = new(0, 0, 0, 0, 0);
}


/// <summary>
/// What the home keeps of one workspace's finished work, and what a clear would free. Counts and bytes only:
/// never a path or a title.
/// </summary>
public sealed record HistoryReading(string Workspace)
{
    /// <summary>Its closed quests held here.</summary>
    public int Quests { get; init; }

    /// <summary>Its asks closed, or whose work closed.</summary>
    public int Asks { get; init; }

    /// <summary>This machine's session records of that work.</summary>
    public int Sessions { get; init; }

    /// <summary>This machine's copies of a teammate's records of that work.</summary>
    public int Teammates { get; init; }

    /// <summary>What that work holds on the disk, by kind.</summary>
    public HistoryBytes Bytes { get; init; } = HistoryBytes.None;

    /// <summary>The intake's room, in bytes.</summary>
    public long Intake { get; init; }

    /// <summary>What a clear would take now, the left-over files and an intake's room it frees included.</summary>
    public HistoryTakes Takes { get; init; } = HistoryTakes.None;

    /// <summary>How many units are kept, by the word that keeps each.</summary>
    public IReadOnlyDictionary<string, int> KeptBy { get; init; } = new Dictionary<string, int>(StringComparer.Ordinal);

    /// <summary>This machine's conversations in it that served no quest, which only *Delete…* takes.</summary>
    public int Conversations { get; init; }

    public long ConversationBytes { get; init; }

    /// <summary>For the whole home: files of records no store has any more, which every workspace's clear takes.</summary>
    public int LeftOver { get; init; }

    public long LeftOverBytes { get; init; }

    /// <summary>For the whole home: the machine log's size, which a clear never touches.</summary>
    public long Log { get; init; }
}


/// <summary>Where a repository's line came from, for the screen and the sentences that name it.</summary>
public static class LineSource
{
    public const string Repository = "repository";
    public const string Workspace = "workspace";
    public const string Checkout = "checkout";
    public const string None = "none";
}


/// <summary>A repository's line and what said so. <see cref="Branch"/> is null only where nothing names one.</summary>
public sealed record Line(string? Branch, string Source);

/// <summary>One repository's line as the screen shows it: which, in which circle, and what said so.</summary>
public sealed record RepositoryLine(string Repository, string Workspace, string? Branch, string Source);

/// <summary>Where a landing rule came from, for the screen and the sentences that name it.</summary>
public static class LandingSource
{
    public const string Repository = "repository";
    public const string Workspace = "workspace";
    public const string Default = "default";
}
