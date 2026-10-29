using System.Text.Json;
using static Daoris.Driver.Json;

namespace Daoris.Driver;

/// <summary>
/// One branch a landing made (WSR5): the repository, the branch, the line it grew from, the commit it was
/// made at, and the session, quest and title it was made for — what the clean-up judges and the hand-off
/// tells a plugin.
/// </summary>
/// <param name="Line">The line the work grew from when it landed (D86), or null where git named none.</param>
/// <param name="Tip">The commit the landing made the branch at. A branch whose history does not hold it is not the landing's.</param>
public sealed record LandedBranch(
    string Repository, string Workspace, string Branch, string? Line, string Tip,
    string Session, string? Quest, string? Title, DateTimeOffset LandedAt)
{
    /// <summary>The plugin that last pushed it — at the landing or a hand-off after it (D100) — or null.</summary>
    public string? Plugin { get; init; }

    /// <summary>Whether a plugin answered that it pushed the branch.</summary>
    public bool Pushed { get; init; }

    /// <summary>The pull request that plugin answered with, or null.</summary>
    public string? PullRequest { get; init; }

    /// <summary>The branch's commit when the plugin pushed it: a hand-off at the same commit would push nothing new.</summary>
    public string? PushedTip { get; init; }

    /// <summary>
    /// The commit its work grew from (WSR6): the start of the session branch it was made from, where that is
    /// recorded, else null. Bringing it up to date cuts here, so only its own commits are replayed.
    /// </summary>
    public string? From { get; init; }

    /// <summary>
    /// When this machine found the branch gone, or no longer the landing's, or removed it (REVIEW2, D113); null while it
    /// stands. A trace is never judged, handed on or moved again: it is kept so the review of the session that landed it
    /// can still say where its work went.
    /// </summary>
    public DateTimeOffset? GoneAt { get; init; }

    /// <summary>What the clean-up proved when it removed the branch (<see cref="LandedKind"/>: on the line, merged, inside another); null where Daoris did not remove it.</summary>
    public string? RemovedAs { get; init; }

    /// <summary>Where that proof held: the form of the line, or the landed branch it was inside.</summary>
    public string? RemovedOn { get; init; }

    /// <summary>
    /// Why this machine found it gone where Daoris did not remove it (CLEANUP1, <see cref="LandedGone"/>): git said its branch is missing,
    /// another branch holding its recorded tip or not, or a branch of its name stands that is not the landing's. Null where Daoris removed
    /// it, while it stands, and for a trace from
    /// before it was kept, which the look asks git about once (<see cref="LandedCleanups.MarkAbsentAsync"/>).
    /// </summary>
    public string? GoneAs { get; init; }

    /// <summary>
    /// Where <see cref="GoneAs"/> is <see cref="LandedGone.Moved"/> (CLEANUP1, after review): the local branch that held its recorded tip
    /// when git said its own branch is missing. The look asks again once that branch is gone too.
    /// </summary>
    public string? MovedTo { get; init; }

    /// <summary>
    /// The commit <see cref="MovedTo"/> stood at when it was found holding the recorded tip (CLEANUP1, re-check): while it stands there it
    /// still holds it, read from the refs the look holds; once it moved, or its name was made again elsewhere, the look asks again.
    /// </summary>
    public string? MovedToAt { get; init; }

    /// <summary>
    /// Where <see cref="GoneAs"/> is <see cref="LandedGone.Missing"/> and no ref held its recorded tip (CLEANUP1, after review): the recovery
    /// ref Daoris kept that commit on before the record was retired, so the record never goes as the only breadcrumb to it.
    /// </summary>
    public string? KeptOn { get; init; }

    /// <summary>
    /// Who accepted it (LAND2b, D145 point 6), one of <see cref="Daoris.Driver.AcceptedBy"/>: the person's press, or the rule's switch at
    /// the quest's done. Null for a landing recorded before it was kept, which a reader says is not kept (D143 point 3).
    /// </summary>
    public string? AcceptedBy { get; init; }

    /// <summary>The landing rule it was made under, as it stood then (LAND2b, design §8); null for one recorded before it was kept.</summary>
    public LandedRule? Rule { get; init; }

    /// <summary>
    /// The acceptance's provenance (INTEGRATE1e, the integration design §17.4): that <c>integrate</c> stood when its first landing was
    /// accepted, its <c>request</c> there, and whether it was the rule's step or the bound version's. Null is a record that says neither:
    /// none stood, or it was written before the field or by a build that rewrote the file without it, which can only under-admit.
    /// </summary>
    public LandedIntegrate? Integrate { get; init; }

    /// <summary>
    /// The review that let its first landing go (REVIEWENV1c, D154 point 9; design §3.5): <c>reviewed</c> with the environment and
    /// the set-up's commit, or <c>skipped</c> with the person's words. Null where no review was asked, and for one recorded before.
    /// </summary>
    public LandingReview? Review { get; init; }

    /// <summary>
    /// The second opinion that let its first landing go (XAGENT1f, D155 point 11; the second-agent design §8.6): settled, or the
    /// person's answer, with the reviewer, the candidate, the passes and the findings counted; or why it landed with none. Null
    /// where none was asked, and for one recorded before.
    /// </summary>
    public LandingOpinion? Opinion { get; init; }

    /// <summary>
    /// The named workflow its first landing went by (WORKFLOW1f, the workflow design §5.5): its id, the version its run kept, the
    /// level that chose it and the task's kind. Null under Current, and for one recorded before.
    /// </summary>
    public LandingWorkflow? Workflow { get; init; }

    /// <summary>
    /// Each time a later done moved it on (LAND2c, D149 point 2), oldest first: a chain's later step, or a session that went on
    /// after its landing. <see cref="Tip"/> is the newest one's <see cref="LandedAdvance.To"/>.
    /// </summary>
    public IReadOnlyList<LandedAdvance> Advances { get; init; } = [];

    /// <summary>
    /// The latest answer about its pull request (PLUGHOOK1a, D148 point 6), with the plugin and when it was asked: what a
    /// removal on the platform's word chose by. <c>completed</c> is final. Null where it was never answered.
    /// </summary>
    public PullRequestState? PullRequestState { get; init; }

    /// <summary>The latest ask that failed, kept while no answer is newer (design §2.4); null where none failed since the last answer.</summary>
    public PullRequestAskFailed? PullRequestAskFailed { get; init; }

    /// <summary>Each session branch removed on its pull request's answer, oldest first (design §2.5).</summary>
    public IReadOnlyList<CarriedBranch> Carried { get; init; } = [];

    /// <summary>
    /// The session's tip the landing squashed into <see cref="Tip"/> (PROCESS1b-ii, D164's PROCESS1b-ii note point 3), moved on by a
    /// chain's later squash; null where the branch holds the session's own commits, and for one recorded before. Daoris's own
    /// word of what it squashed: while the entry stands, a session branch at this commit or an ancestor of it is landed.
    /// </summary>
    public string? Squashed { get; init; }

    /// <summary>That the line held the branch's work, and the evidence (FABRIC-END2, design §B5, §C7); null where not yet judged held. Written only by <see cref="EndKeeping"/>.</summary>
    public HeldFact? Held { get; init; }

    /// <summary>
    /// The branch work units whose work it carries (FABRIC-END2, design §D2): the landing's own session's and each advance's, where its
    /// entry had one. A landing carrying several sessions' work is one result of several units, never a unit itself.
    /// </summary>
    public IReadOnlyList<string> Units { get; init; } = [];

    /// <summary>
    /// The quest its own session served (FABRIC-CLOSE1h11, D173's CLOSELAND1 note): <see cref="Quest"/> names the one it is named for, a
    /// chain's first (WSR5), which need not be its session's. Null where the session served none, and in a record from before.
    /// </summary>
    public string? Served { get; init; }

    /// <summary>
    /// The submission its own session's acceptance was made on (FABRIC-CLOSE1h13), by its machine and sequence, as the door read it; null
    /// for a done, a conversation, and a record from before.
    /// </summary>
    public QuestSubmissionRef? Submission { get; init; }

    /// <summary>
    /// The work of each session it carries, oldest first (FABRIC-UNITLINK1, FABRIC-CLOSE1h11): its own session's, then each advance's,
    /// each with the commit the branch stood at once that work was on it, and the quest, the unit and the submission the record keeps for
    /// it, null where it keeps none.
    /// </summary>
    public IReadOnlyList<LandedWork> Work()
    {
        var work = new List<LandedWork>(Advances.Count + 1)
        {
            new(Session, Advances.Count > 0 ? Advances[0].From : Tip) { Quest = Served, Unit = OwnUnit(), Submission = Submission },
        };
        foreach (var advance in Advances)
        {
            work.Add(new(advance.Session, advance.To) { Quest = advance.Quest, Unit = advance.Unit, Submission = advance.Submission });
        }

        return work;
    }

    /// <summary>
    /// The work no push of it has carried yet (FABRIC-CLOSE1h11): all of it where no plugin answered that it pushed the branch, else what
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
    /// The sessions besides <paramref name="session"/> whose work a push of it carries unpushed (FABRIC-CLOSE1h11), each by its newest
    /// work, in the order they first carried any: what a push is judged on beside the session it pushes for.
    /// </summary>
    public IReadOnlyList<LandedWork> UnpushedBesides(string session) =>
        [.. Unpushed()
            .Where(work => !string.Equals(work.Session, session, StringComparison.OrdinalIgnoreCase))
            .GroupBy(work => work.Session, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.Last())];

    /// <summary>
    /// Its own session's branch work unit as the record says it (FABRIC-UNITLINK1): the first it carries, where every advance names its
    /// own and they account for the rest, in the order each was added. Null where it is not said: an advance from before names none,
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

    /// <summary>Whether this landing names <paramref name="session"/>: the session that made it, or one whose done moved it on (LAND2c).</summary>
    public bool Names(string session) =>
        string.Equals(Session, session, StringComparison.OrdinalIgnoreCase)
        || Advances.Any(advance => string.Equals(advance.Session, session, StringComparison.OrdinalIgnoreCase));

    /// <summary>When <paramref name="session"/>'s work was accepted onto it: its newest advance, else the landing itself.</summary>
    public DateTimeOffset AcceptedAt(string session) =>
        Advances.LastOrDefault(advance => string.Equals(advance.Session, session, StringComparison.OrdinalIgnoreCase))?.At ?? LandedAt;

    /// <summary>Who accepted <paramref name="session"/>'s work onto it (<see cref="Daoris.Driver.AcceptedBy"/>): its newest advance's, else the landing's.</summary>
    public string? AcceptedByOf(string session) =>
        Advances.LastOrDefault(advance => string.Equals(advance.Session, session, StringComparison.OrdinalIgnoreCase)) is { } advance
            ? advance.AcceptedBy
            : AcceptedBy;

    /// <summary>
    /// The session whose work last moved it: its newest advance's, else the landing's own. Whose acceptance a hand-off tells its plugin
    /// (LAND2c), and whose quest a person's hand-off is held to (FABRIC-CLOSE1h8). A method, so no answer that carries a landing gains a field.
    /// </summary>
    public string LastSession() => Advances.Count > 0 ? Advances[^1].Session : Session;
}


/// <summary>
/// One fast-forward of a landed branch (LAND2c, D145 point 6, design §8): from the commit it stood at to the one it moves to,
/// when, the session whose done moved it, and who accepted that done.
/// </summary>
public sealed record LandedAdvance(string From, string To, DateTimeOffset At, string Session)
{
    /// <summary>One of <see cref="Daoris.Driver.AcceptedBy"/>, or null in a record that did not keep it.</summary>
    public string? AcceptedBy { get; init; }

    /// <summary>
    /// This advance's own acceptance provenance (INTEGRATE1e, design §17.4), as it keeps its own <see cref="AcceptedBy"/>; null where
    /// no <c>integrate</c> stood, and in a record from before.
    /// </summary>
    public LandedIntegrate? Integrate { get; init; }

    /// <summary>The review that let this advance go (REVIEWENV1c, design §3.5); null where none was asked, and in a record from before.</summary>
    public LandingReview? Review { get; init; }

    /// <summary>The second opinion that let this advance go (XAGENT1f, design §8.6); null where none was asked, and in a record from before.</summary>
    public LandingOpinion? Opinion { get; init; }

    /// <summary>The named workflow this advance went by (WORKFLOW1f, design §5.5); null under Current, and in a record from before.</summary>
    public LandingWorkflow? Workflow { get; init; }

    /// <summary>
    /// The session's tip this advance squashed into <see cref="To"/> (PROCESS1b-ii, point 4), which the entry's
    /// <see cref="LandedBranch.Squashed"/> moves to; null for a fast-forward, and in a record from before.
    /// </summary>
    public string? Squashed { get; init; }

    /// <summary>
    /// The quest its session served (FABRIC-UNITLINK1, D173's CLOSELAND1 note), which a hand-off and a push judge it on and its unit is
    /// linked to; null where the session served none, and in a record from before.
    /// </summary>
    public string? Quest { get; init; }

    /// <summary>
    /// Its session's branch work unit (FABRIC-UNITLINK1), which <see cref="LandedBranch.Units"/> gains where it is new; null where the
    /// session's tree had none, and in a record from before.
    /// </summary>
    public string? Unit { get; init; }

    /// <summary>
    /// The submission its acceptance was made on (FABRIC-CLOSE1h13), by its machine and sequence: one made again since holds a person's
    /// hand-off of its work back. Null for a done, and in a record from before, whose session is judged only on its result being in.
    /// </summary>
    public QuestSubmissionRef? Submission { get; init; }
}


/// <summary>
/// One session's work a landed branch carries (FABRIC-UNITLINK1, FABRIC-CLOSE1h11; <see cref="LandedBranch.Work"/>): the session, the
/// commit the branch stood at once its work was on it, and what the record keeps of it, each null where it keeps none.
/// </summary>
public sealed record LandedWork(string Session, string Tip)
{
    /// <summary>The quest the session served; null where it served none, or the record does not say.</summary>
    public string? Quest { get; init; }

    /// <summary>Its branch work unit; null where the record does not say which.</summary>
    public string? Unit { get; init; }

    /// <summary>The submission its acceptance was made on; null for a done, or where the record does not say.</summary>
    public QuestSubmissionRef? Submission { get; init; }
}


/// <summary>Who accepted a landing (LAND2b, D145 point 6): the person's press, or the rule's switch at the quest's done.</summary>
public static class AcceptedBy
{
    /// <summary>The person pressed Accept: the review's, a terminal's <c>trees land</c>, or an Ask Daoris card.</summary>
    public const string Person = "person";

    /// <summary>The rule accepts automatically (<c>autoAccept</c>), and the quest's done landed it at a look.</summary>
    public const string Auto = "auto";
}


/// <summary>The landing rule a landing was made under, as it stood then (LAND2b, D145 point 6, design §8).</summary>
/// <param name="Plugin">The plugin the rule named, whether or not it pushed; null where the person pushes.</param>
/// <param name="Source">Where the rule came from, one of <see cref="LandingSource"/>.</param>
public sealed record LandedRule(string? Plugin, bool AutoAccept, string Source)
{
    /// <summary>
    /// Where the access setup that turned the rule's switch on was set (AUTONOMY1c2, D184 point 3; <see cref="Landing.Access"/>): with
    /// <see cref="AcceptedBy.Auto"/>, the setup accepted it, not the rule's own switch. Null where the rule's own switch stood, and in a
    /// record from before: the file keeps it as <c>access</c> beside <c>source</c>, only where it was set.
    /// </summary>
    public string? Access { get; init; }
}


/// <summary>
/// An acceptance's provenance (INTEGRATE1e; the integration design §17.4, F40 and C30): that <c>integrate</c> stood where a landing or an
/// advance was accepted, with the <c>request</c> it said there and whose step it was. Written beside <c>acceptedBy</c> and the rule, as
/// <c>integrate</c> <c>{ request, source }</c>, only where one stood; an older reader passes the key over, and a build that rewrites the
/// file without it leaves a record that says neither. The door that admits a request reads it with <c>acceptedBy</c>; nothing judges it
/// here.
/// </summary>
/// <param name="Request">The <c>request</c> that stood (<c>owner</c>, <c>done</c> or <c>steward</c>), kept as written for its reader to judge.</param>
/// <param name="Source">Whose step it was: <see cref="Rule"/>, Current's under a rule carrying <c>integrate</c>, or <see cref="Version"/>.</param>
public sealed record LandedIntegrate(string Request, string Source)
{
    /// <summary>The effective landing rule's <c>integrate</c>, under Current.</summary>
    public const string Rule = "rule";

    /// <summary>The bound named version's own <c>integrate</c> step (row n).</summary>
    public const string Version = "version";

    /// <summary>
    /// What a landing under <paramref name="integrate"/> keeps: its <c>request</c> through the design's <i>Absent</i> column, the rule
    /// its source; none where the rule carries no <c>integrate</c>. Under a named version the rule's <c>request</c> is not the unit's
    /// process (§4.1), its own <c>integrate</c> step is, which no version carries until row n: so none.
    /// </summary>
    public static LandedIntegrate? Of(LandingIntegrate? integrate, bool named) =>
        named || integrate is null ? null : new(integrate.Resolved().Request ?? LandingIntegrate.Owner, Rule);
}


/// <summary>One try to land a due session, and the facts it was made at (design §8): never a sentence, a path or a word of anyone's.</summary>
/// <param name="Code">One of <see cref="AutoLandingCode"/>.</param>
public sealed record AutoTry(DateTimeOffset At, string Code)
{
    /// <summary>The tree's HEAD when it was tried, or null where none was read.</summary>
    public string? Tip { get; init; }

    /// <summary>The tree's status when it was tried (<see cref="AutoLandingRules.Fingerprint"/>), or null where none was read.</summary>
    public string? Status { get; init; }

    /// <summary>How many paths were uncommitted, where that is what held it.</summary>
    public int? Uncommitted { get; init; }

    /// <summary>The branch it made, or the one it found standing.</summary>
    public string? Branch { get; init; }

    /// <summary>How many commits it carried, where it landed.</summary>
    public int? Commits { get; init; }
}


/// <summary>
/// A session due to land automatically (LAND2b, D145 point 2): its quest, where it ran, the tree it ran in, when it became
/// due, each try, and when the entry closed.
/// </summary>
/// <param name="Tree">The tree its record names, under this home's trees (D51): machine-local, like the file that keeps it.</param>
public sealed record AutoLanding(string Session, string Quest, string Repository, string Workspace, string Tree, DateTimeOffset DueAt)
{
    public IReadOnlyList<AutoTry> Tries { get; init; } = [];

    /// <summary>When a try closed it (<see cref="AutoLandingCode.Closes"/>), or null while it waits.</summary>
    public DateTimeOffset? Closed { get; init; }

    /// <summary>Its last try, or null before the first.</summary>
    public AutoTry? Last => Tries.Count == 0 ? null : Tries[^1];

    /// <summary>
    /// Whether a new conclusion made it due again after a try that closed it (FABRIC-CLOSE1h6): open, with a last try whose code
    /// closes. Only <see cref="AutoLandings.Due"/> opens a closed entry, so that try was the earlier due's, and this one is judged afresh.
    /// </summary>
    public bool Reopened => Closed is null && Last is { Code: var code } && AutoLandingCode.Closes(code);
}


/// <summary>
/// A plugin's answer at <see cref="HookPoints.State"/> (PLUGHOOK1a, D148 point 3), and, kept on the landing entry as
/// <c>pullRequestState</c>, the plugin that gave it and when it was asked (D143).
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

    /// <summary>When it completed or was abandoned, or null.</summary>
    public DateTimeOffset? At { get; init; }

    /// <summary>The plugin's own sentence, kept to <see cref="PullRequestAnswers.MessageLimit"/> characters, or null.</summary>
    public string? Message { get; init; }

    /// <summary>The plugin that answered.</summary>
    public string? Plugin { get; init; }

    /// <summary>When Daoris asked; <see cref="DateTimeOffset.MinValue"/> where a record did not keep it.</summary>
    public DateTimeOffset AskedAt { get; init; }
}


/// <summary>
/// The latest ask that failed (design §2.4), kept as <c>pullRequestAskFailed</c> while no answer is newer: one of
/// <see cref="PluginEvents"/>' failure kinds, the plugin, and when. A failure never overwrites an answer.
/// </summary>
public sealed record PullRequestAskFailed(string Code, string Plugin, DateTimeOffset At);

/// <summary>A session branch removed on a completed answer (design §2.5): its tip, when, and which occasion removed it.</summary>
/// <param name="By"><see cref="CarriedBy.Tidy"/> or <see cref="CarriedBy.CleanUp"/>.</param>
public sealed record CarriedBranch(string Branch, string Tip, DateTimeOffset At, string By);


/// <summary>What a clear would take now (the design §2.4).</summary>
public sealed record HistoryTakes(int Quests, int Asks, int Sessions, int Teammates, long Bytes)
{
    public static HistoryTakes None { get; } = new(0, 0, 0, 0, 0);
}


/// <summary>
/// What the home keeps of one workspace's finished work, and what a clear would free (the design §2.4). Counts and bytes only:
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

    /// <summary>This machine's conversations in it that served no quest, which only *Delete…* takes (D126 §5.4).</summary>
    public int Conversations { get; init; }

    public long ConversationBytes { get; init; }

    /// <summary>For the whole home: files of records no store has any more (§2.3), which every workspace's clear takes.</summary>
    public int LeftOver { get; init; }

    public long LeftOverBytes { get; init; }

    /// <summary>For the whole home: the machine log's size, which a clear never touches (§7).</summary>
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


/// <summary>A repository's landing rule and what said so.</summary>
public sealed record Landing(LandingRule Rule, string Source)
{
    /// <summary>
    /// Where the repository's access setup said <c>landing: rule</c> (AUTONOMY1c2, D184 point 3), one of <see cref="AccessRules.FromRepository"/>
    /// and the rest, where it changes how this rule lands: a branch rule whose own switch is off, which it accepts automatically
    /// (<see cref="LandingRules.Effective"/>), or a merge rule, which it cannot (D51 rule 6). Null where the setup is the person's, or the
    /// rule accepts by its own switch.
    /// </summary>
    public string? Access { get; init; }
}
