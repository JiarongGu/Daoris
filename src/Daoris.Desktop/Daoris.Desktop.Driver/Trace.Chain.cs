using System.Globalization;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>The stores a trace reads (TRACE1, D143), each a code the screen words and the terminal names in its sentence.</summary>
public static class TraceStores
{
    /// <summary>The service's session records.</summary>
    public const string Sessions = "sessions";

    /// <summary>The service's quests, as their operations replay.</summary>
    public const string Quests = "quests";

    /// <summary>The service's ask record.</summary>
    public const string Asks = "asks";

    /// <summary>This machine's record of a session (D76).</summary>
    public const string Events = "events";

    /// <summary>The rules file the driver wrote under the home for a session (PERM1).</summary>
    public const string Rules = "rules";

    /// <summary>This machine's <c>landings.json</c> (WSR5).</summary>
    public const string Landings = "landings";

    /// <summary>This machine's <c>driver.json</c>, for the standing answers (D135 §3).</summary>
    public const string Config = "config";

    /// <summary>This machine's due list (LAND2b, D145), each session due to land automatically and each try.</summary>
    public const string AutoLandings = "auto-landings";
}

// Why a link is missing (D143 point 3), one family per kind of link, each a code the screen words where the link would
// have been. A family each, so each is a catalogue family of its own that a test holds to it (`work/trace.test.ts`).

/// <summary>Why an ask or a quest the chain names is missing.</summary>
public static class TraceLinkGaps
{
    /// <summary>Its store did not answer.</summary>
    public const string Unread = "unread";

    /// <summary>Its store holds none by the id the chain names.</summary>
    public const string NotFound = "not-found";
}

/// <summary>Why this machine's record of a session is missing (D76).</summary>
public static class TraceEventGaps
{
    /// <summary>A session's id names no record on this machine.</summary>
    public const string NotAnId = "not-an-id";

    /// <summary>This machine holds no record of the session.</summary>
    public const string NoneHere = "none-here";

    /// <summary>The record here does not read.</summary>
    public const string Unread = "unread";
}

/// <summary>Why the rules a session was handed are missing (PERM1).</summary>
public static class TraceRuleGaps
{
    /// <summary>A running session: no file, since its agent takes none or there was nothing to hand.</summary>
    public const string NoneWhileRunning = "none-while-running";

    /// <summary>The file goes when its session ends, and a parked session's when its run does.</summary>
    public const string GoneWithRun = "gone-with-run";

    /// <summary>The file under the home does not read.</summary>
    public const string Unread = "unread";
}

/// <summary>Why no branch landing is named for a session (WSR5).</summary>
public static class TraceLandingGaps
{
    /// <summary><c>landings.json</c> does not read.</summary>
    public const string Unread = "unread";

    /// <summary>No branch landing names it, and its record keeps no acceptance of its work.</summary>
    public const string None = "none";

    /// <summary>No branch landing names it, and no record of it here could say whether its work was accepted.</summary>
    public const string Unknown = "unknown";

    /// <summary>A landing into the line: its record keeps the person's acceptance, and the merge's own commit is not kept.</summary>
    public const string MergeAccepted = "merge-accepted";
}

/// <summary>Why what stood at a start is not read.</summary>
public static class TraceStoodGaps
{
    /// <summary>Its record says no moment it opened, so nothing is read against one.</summary>
    public const string NoMoment = "no-moment";
}

/// <summary>What names a link nothing holds (<see cref="TraceNamedBy"/>).</summary>
public static class TraceNamers
{
    /// <summary>A quest's sender names its ask.</summary>
    public const string Quest = "quest";

    /// <summary>An intake session's record names its ask.</summary>
    public const string Intake = "intake";

    /// <summary>A session's record names its quest.</summary>
    public const string Session = "session";

    /// <summary>A landing names its quest.</summary>
    public const string Landing = "landing";

    /// <summary>The trace itself names it.</summary>
    public const string Trace = "trace";
}

/// <summary>How a quest's done answered one requirement (DRIFT1d, D133 §4), as a trace reads it.</summary>
public static class TraceAnswers
{
    public const string Met = "met";
    public const string Departed = "departed";

    /// <summary>Its done carries no answer to it.</summary>
    public const string Unanswered = "unanswered";

    /// <summary>Not answered yet: a done answers it.</summary>
    public const string NotYet = "not-yet";
}

/// <summary>Whether a standing answer stood when a session started (D135 §3), by the moments kept and nothing else.</summary>
public static class TraceStandings
{
    /// <summary>None set now, and one cleared since is not kept.</summary>
    public const string None = "none";

    /// <summary>Set at a moment <c>driver.json</c> does not say, so whether it stood then is not known.</summary>
    public const string MomentUnknown = "moment-unknown";

    public const string Before = "before";

    /// <summary>Set after it started: what stood before is not kept, since <c>driver.json</c> keeps the latest.</summary>
    public const string After = "after";
}

/// <summary>Where a go-ahead stood when a session started (D135 §2), by the moments kept and nothing else.</summary>
public static class TraceGoAheadStands
{
    public const string AskedAfter = "asked-after";
    public const string Waiting = "waiting";
    public const string ApprovedBefore = "approved-before";
    public const string RefusedBefore = "refused-before";

    /// <summary>Answered after it started: whether an earlier answer stood then is not kept, since an answer replaces the one before.</summary>
    public const string AnsweredAfter = "answered-after";
}

/// <summary>Whether a session ran in the same tree as the record before it on its quest.</summary>
public static class TraceTrees
{
    public const string Same = "same";
    public const string Other = "other";

    /// <summary>Either record names no tree.</summary>
    public const string Unknown = "unknown";
}

/// <summary>
/// One trace, as data (TRACE1b, D143, D50): the reading both doors word. The terminal prints it in the driver's English
/// (<see cref="TraceWords"/>), and the screen words its codes in the reader's language. Each link names the store it was read
/// from, and a link nothing keeps carries why it is missing.
/// </summary>
/// <remarks>
/// <b>Nothing here is a machine path on the wire</b> (D47 §4): a session's tree is its folder's name, a file under the home is
/// named by its store, and what a failed read said (an exception's message, which may name a file) is the terminal's alone,
/// held by <see cref="JsonIgnoreAttribute"/>.
/// </remarks>
/// <param name="Kind">The kind the id named, one of <see cref="TraceEntry"/>.</param>
public sealed record TraceChain(string Kind, string Id)
{
    /// <summary>For a commit: each evidence line and each landing that names it.</summary>
    public IReadOnlyList<TraceFoundBy> Found { get; init; } = [];

    /// <summary>Each store that did not answer: its links below say they could not be read.</summary>
    public IReadOnlyList<TraceUnread> Unread { get; init; } = [];

    /// <summary>The chain, in the order it is read: each quest's ask, the quest, its sessions; then the sessions on no quest.</summary>
    public IReadOnlyList<TraceLink> Links { get; init; } = [];
}

/// <summary>How a commit was found: in a session's evidence, or as a landing's tip or the commit a plugin pushed of it.</summary>
/// <param name="How"><c>evidence</c>, <c>tip</c> or <c>pushed</c>.</param>
public sealed record TraceFoundBy(string How, string Session)
{
    public const string Evidence = "evidence";
    public const string Tip = "tip";
    public const string Pushed = "pushed";

    /// <summary>The evidence's line that names it.</summary>
    public string? Line { get; init; }

    /// <summary>The branch whose landing names it.</summary>
    public string? Branch { get; init; }
}

/// <summary>A store that did not answer, one of <see cref="TraceStores"/>.</summary>
public sealed record TraceUnread(string Store)
{
    /// <summary>What the read said, the terminal's: it may name a file under the home.</summary>
    [JsonIgnore]
    public string? Problem { get; init; }
}

/// <summary>One link: an ask, a quest, a session, or a session only a landing names.</summary>
/// <param name="Kind"><c>ask</c>, <c>quest</c>, <c>session</c> or <c>unrecorded</c>; the one link of that kind is set.</param>
public sealed record TraceLink(string Kind)
{
    public const string AskKind = "ask";
    public const string QuestKind = "quest";
    public const string SessionKind = "session";
    public const string UnrecordedKind = "unrecorded";

    public TraceAskLink? Ask { get; init; }
    public TraceQuestLink? Quest { get; init; }
    public TraceSessionLink? Session { get; init; }
    public TraceUnrecordedLink? Unrecorded { get; init; }
}

/// <summary>What names a link the chain reached: a quest's sender, an intake's record, a session's record, a landing, or the trace.</summary>
/// <param name="Kind"><c>quest</c>, <c>intake</c>, <c>session</c>, <c>landing</c> or <c>trace</c>.</param>
public sealed record TraceNamedBy(string Kind, string? Id = null);

/// <summary>A record's state, with what its flags add to it.</summary>
/// <param name="State">The record's state, or empty where it says none.</param>
public sealed record TraceState(string State)
{
    /// <summary>A failure an account's limit made (TOOL4c).</summary>
    public bool Limit { get; init; }

    /// <summary>A stop that was not the person's: the driver closed, or the sweep found it orphaned (D104).</summary>
    public bool Interrupted { get; init; }

    /// <summary>A park the person answered: it goes on at the driver's next look (STANDDOWN2).</summary>
    public bool Answered { get; init; }
}

/// <summary>What ran a session, as its record names it; each null where unsaid.</summary>
public sealed record TraceAgent
{
    public string? Adapter { get; init; }

    /// <summary>The tool's version the record opened on (D49 §4).</summary>
    public string? Harness { get; init; }

    /// <summary>The account it ran as; null on this machine's record is the tool's own sign-in, and unsaid on a teammate's.</summary>
    public string? Account { get; init; }

    /// <summary>A teammate's record: its account stays on the machine that ran it.</summary>
    public bool Teammate { get; init; }
}

/// <summary>The ask a chain names (D133, D135), from the service's ask record.</summary>
public sealed record TraceAskLink(string Id)
{
    /// <summary>Its store: <see cref="TraceStores.Asks"/>.</summary>
    public string Source => TraceStores.Asks;

    /// <summary>Why it could not be read, <see cref="TraceLinkGaps.Unread"/> or <see cref="TraceLinkGaps.NotFound"/>; null where it was.</summary>
    public string? Missing { get; init; }

    [JsonIgnore]
    public string? Problem { get; init; }

    public TraceNamedBy? NamedBy { get; init; }

    public string? Workspace { get; init; }

    /// <summary>Open, Proposed, Published or Closed, as the record spells it.</summary>
    public string? State { get; init; }

    /// <summary>Which tier answered it (D24).</summary>
    public string? Tier { get; init; }

    public TraceIntake? Intake { get; init; }

    /// <summary>The quests it became.</summary>
    public IReadOnlyList<string> Quests { get; init; } = [];

    /// <summary>What its close said, as written.</summary>
    public string? Note { get; init; }

    /// <summary>Its sentence, where the service answers no words: the person's own, as written.</summary>
    public string? Sentence { get; init; }

    /// <summary>The person's words, oldest first, verbatim; null where the service answers none.</summary>
    public IReadOnlyList<AskWordView>? Words { get; init; }

    /// <summary>For an ask from before its words were kept: from when they are.</summary>
    public DateTimeOffset? WordsKeptFrom { get; init; }

    /// <summary>Its go-aheads, oldest first; null where the service answers none.</summary>
    public IReadOnlyList<TraceGoAhead>? GoAheads { get; init; }
}

/// <summary>The intake session an ask names, and its record where the service holds one.</summary>
public sealed record TraceIntake(string Session)
{
    /// <summary>Its record's state, or null where the service holds no record of it.</summary>
    public TraceState? State { get; init; }

    public TraceAgent? Agent { get; init; }
}

/// <summary>A go-ahead on an ask (KNOWUSE1a): its act, the person's answer, and who first asked it.</summary>
/// <param name="Kind">write, release, push, sign-in or run, as the service spells it.</param>
/// <param name="On">Where it lands, in the session's words.</param>
/// <param name="Act">What it touches, in the session's words.</param>
public sealed record TraceGoAhead(int Number, string Kind, string On, string Act)
{
    /// <summary>The act in one line, as the service names it: the terminal's.</summary>
    [JsonIgnore]
    public string Named => $"{Kind} on {On}: \"{Act}\"";

    /// <summary>The person's answer, the latest; null while it waits on them.</summary>
    public GoAheadAnswerView? Answer { get; init; }

    /// <summary>The session that first asked it, and when.</summary>
    public string? FirstAskedBy { get; init; }

    public DateTimeOffset? FirstAskedAt { get; init; }

    /// <summary>The go-ahead whose words it shared, when that is why it was asked again.</summary>
    public int? Near { get; init; }
}

/// <summary>The quest a chain reaches, as its operations replay on the service.</summary>
public sealed record TraceQuestLink(string Id)
{
    /// <summary>Its store: <see cref="TraceStores.Quests"/>.</summary>
    public string Source => TraceStores.Quests;

    /// <summary>Why it could not be read, <see cref="TraceLinkGaps.Unread"/> or <see cref="TraceLinkGaps.NotFound"/>; null where it was.</summary>
    public string? Missing { get; init; }

    public TraceNamedBy? NamedBy { get; init; }

    /// <summary>Whom it asks as a person reads it: its repository, and its lanes after a colon.</summary>
    public string? Address { get; init; }

    public string? Title { get; init; }

    /// <summary>Open, Taken, Done or Declined, as the record spells it.</summary>
    public string? Status { get; init; }

    public DateTimeOffset? Filed { get; init; }

    public DateTimeOffset? Moved { get; init; }

    /// <summary>The ask it was asked by, or null where no ask asked it.</summary>
    public string? Ask { get; init; }

    /// <summary>Its sender, as the record names it: who asked it where no ask did.</summary>
    public string? From { get; init; }

    /// <summary>The quest whose close published it (D65 §4).</summary>
    public string? Parent { get; init; }

    /// <summary>The session whose connector published it (SESS1).</summary>
    public string? PublishedBy { get; init; }

    /// <summary>The quest its taker waits on (D79).</summary>
    public string? Awaits { get; init; }

    /// <summary>Held: its done departed from what the person required, and waits for their yes.</summary>
    public bool Held { get; init; }

    /// <summary>When the person said yes to its departure.</summary>
    public DateTimeOffset? Accepted { get; init; }

    /// <summary>What its close said, as written.</summary>
    public string? Note { get; init; }

    public IReadOnlyList<TraceRequirement> Requirements { get; init; } = [];

    /// <summary>The steps its close will publish, as written.</summary>
    public IReadOnlyList<TraceStep> Then { get; init; } = [];

    /// <summary>The session records that name it, oldest first; null where the session records could not be read.</summary>
    public IReadOnlyList<TraceRecordRef>? Records { get; init; }
}

/// <summary>One requirement: the person's words, its check, and how the done answered it (one of <see cref="TraceAnswers"/>).</summary>
public sealed record TraceRequirement(int Number, string Quote, string Check, string Answer)
{
    /// <summary>How it was met, in the done's words.</summary>
    public string? Met { get; init; }

    /// <summary>Why the done departed, in its words.</summary>
    public string? Departed { get; init; }

    /// <summary>The person's words a departure relied on.</summary>
    public string? On { get; init; }
}

public sealed record TraceStep(string To, string Title);

public sealed record TraceRecordRef(string Id, string State);

/// <summary>One session a chain reaches: its record's facts, then this machine's, then what stood when it started.</summary>
public sealed record TraceSessionLink(string Id)
{
    /// <summary>Its store: <see cref="TraceStores.Sessions"/>.</summary>
    public string Source => TraceStores.Sessions;

    /// <summary><c>driven</c> or <c>chat</c>, as the record says.</summary>
    public string Kind { get; init; } = "driven";

    public TraceState State { get; init; } = new("");

    public DateTimeOffset? Opened { get; init; }

    public DateTimeOffset? Moved { get; init; }

    public TraceAgent Agent { get; init; } = new();

    /// <summary>A teammate's record: it ran on another machine, which keeps its account, tree, events and rules.</summary>
    public bool Teammate { get; init; }

    /// <summary>Its tree's folder name, never its path; null where the record names none.</summary>
    public string? Tree { get; init; }

    /// <summary>Its tree's path, the terminal's: a machine path never goes to a page (D47 §4).</summary>
    [JsonIgnore]
    public string? TreePath { get; init; }

    /// <summary>The commit its tree grew from; null where the record names none.</summary>
    public string? BaseCommit { get; init; }

    public string? Quest { get; init; }

    /// <summary>The ask an intake answers (D65 §1b).</summary>
    public string? Ask { get; init; }

    /// <summary>The record before it on its quest, or that it is the first; null with no quest or no moment it opened.</summary>
    public TraceBefore? Before { get; init; }

    /// <summary>It took the quest through its own connector (STANDDOWN2).</summary>
    public bool Took { get; init; }

    /// <summary>Its record's note, as written.</summary>
    public string? Note { get; init; }

    /// <summary>The person's answer to its park, as written.</summary>
    public string? Answer { get; init; }

    /// <summary>The commits the driver read off its tree at its end; null where its record keeps none.</summary>
    public TraceEvidence? Evidence { get; init; }

    /// <summary>This machine's record of it; null for a teammate's.</summary>
    public TraceEvents? Events { get; init; }

    /// <summary>The rules it was handed; null for a teammate's.</summary>
    public TraceRules? Rules { get; init; }

    /// <summary>Where its work landed; null for a teammate's.</summary>
    public TraceLanding? Landing { get; init; }

    /// <summary>What stood when it started; null unless it is a driven session on a quest.</summary>
    public TraceStood? Stood { get; init; }
}

/// <summary>The record before a session on its quest, by when each opened.</summary>
public sealed record TraceBefore(bool First)
{
    public string? Session { get; init; }

    public string? State { get; init; }

    /// <summary>Whether it ended (its state is one a record ends in); otherwise it is still in that state.</summary>
    public bool Ended { get; init; }

    /// <summary>When it last moved.</summary>
    public DateTimeOffset? At { get; init; }

    /// <summary>One of <see cref="TraceTrees"/>.</summary>
    public string? Tree { get; init; }
}

/// <summary>A session's evidence (D46 §4): the driver's heading, its lines, and the commits they name.</summary>
public sealed record TraceEvidence(string Said, IReadOnlyList<string> Lines, IReadOnlyList<TraceCommit> Commits);

public sealed record TraceCommit(string Sha, string Line);

/// <summary>This machine's record of a session (D76).</summary>
public sealed record TraceEvents
{
    /// <summary>Its store: <see cref="TraceStores.Events"/>.</summary>
    public string Source => TraceStores.Events;

    /// <summary>Why it could not be read: <see cref="TraceEventGaps.NotAnId"/>, <see cref="TraceEventGaps.NoneHere"/> or <see cref="TraceEventGaps.Unread"/>.</summary>
    public string? Missing { get; init; }

    [JsonIgnore]
    public string? Problem { get; init; }

    /// <summary>The driver's notes before its instruction, in the driver's words: they name the account a start chose and why.</summary>
    public IReadOnlyList<string> Starts { get; init; } = [];

    /// <summary>Each instruction it was handed, by its event and size, never its words.</summary>
    public IReadOnlyList<TraceInstruction> Instructions { get; init; } = [];

    /// <summary>Each acceptance of its work a landing's press kept (D100), in the driver's words.</summary>
    public IReadOnlyList<TraceAcceptance> Accepted { get; init; } = [];
}

/// <summary>One instruction: its event, its length, what the record keeps of a cut one, and its account (CONTEXT1).</summary>
public sealed record TraceInstruction(long Seq, int Chars)
{
    /// <summary>Where the record cut it: how many of its characters it keeps.</summary>
    public int? Kept { get; init; }

    /// <summary>What it was handed, section by section; null for one handed before the driver kept an account.</summary>
    public InstructionAccount? Account { get; init; }
}

public sealed record TraceAcceptance(DateTimeOffset At, string Said);

/// <summary>The rules a session was handed (PERM1, D72), from the file the driver wrote for it while that file stands.</summary>
public sealed record TraceRules
{
    /// <summary>Its store: <see cref="TraceStores.Rules"/>.</summary>
    public string Source => TraceStores.Rules;

    /// <summary>Why none are read: <see cref="TraceRuleGaps.NoneWhileRunning"/>, <see cref="TraceRuleGaps.GoneWithRun"/> or <see cref="TraceRuleGaps.Unread"/>.</summary>
    public string? Missing { get; init; }

    [JsonIgnore]
    public string? Problem { get; init; }

    public int Allowed { get; init; }

    public int Asked { get; init; }

    public int Denied { get; init; }

    /// <summary>Hard denials beside the harness's own.</summary>
    public int Hard { get; init; }

    /// <summary>The tree guard holds it to its tree (PERM3).</summary>
    public bool Guarded { get; init; }
}

/// <summary>Where a session's work landed (WSR5): each branch a landing made of it, or why none is named.</summary>
public sealed record TraceLanding
{
    /// <summary>Its store: <see cref="TraceStores.Landings"/>.</summary>
    public string Source => TraceStores.Landings;

    /// <summary>
    /// Where no branch is named: <see cref="TraceLandingGaps.Unread"/>, <see cref="TraceLandingGaps.None"/>, <see cref="TraceLandingGaps.Unknown"/> or
    /// <see cref="TraceLandingGaps.MergeAccepted"/>.
    /// </summary>
    public string? Missing { get; init; }

    public IReadOnlyList<TraceBranch> Branches { get; init; } = [];

    /// <summary>
    /// Its entry on the due list (LAND2b), where it has one: a session never due has none. Read where its landings are, as the
    /// terminal says it beside them.
    /// </summary>
    public TraceDue? Due { get; init; }
}

/// <summary>A branch a landing made, standing or gone.</summary>
public sealed record TraceBranch(string Branch, string Tip, DateTimeOffset At)
{
    /// <summary>The line it landed from, or null where git named none.</summary>
    public string? Line { get; init; }

    /// <summary>The commit its work grew from.</summary>
    public string? From { get; init; }

    public string? Plugin { get; init; }

    public bool Pushed { get; init; }

    public string? PullRequest { get; init; }

    /// <summary>What its plugin last answered about its pull request, with when (PLUGHOOK1c, D148 point 6); null where none is kept.</summary>
    public PullRequestState? PullRequestState { get; init; }

    /// <summary>The latest ask about it that failed since that answer (design §2.4); null where none did.</summary>
    public PullRequestAskFailed? PullRequestAskFailed { get; init; }

    public DateTimeOffset? Gone { get; init; }

    /// <summary>What the clean-up proved when it removed it (<see cref="LandedKind"/>).</summary>
    public string? RemovedAs { get; init; }

    public string? RemovedOn { get; init; }

    /// <summary>
    /// Who accepted it (LAND2b, D145 point 6), one of <see cref="Daoris.Driver.AcceptedBy"/>; null for a landing recorded
    /// before it was kept, which both doors say is not kept (D143 point 3).
    /// </summary>
    public string? AcceptedBy { get; init; }

    /// <summary>The landing rule it was made under, as it stood then; null for one recorded before it was kept.</summary>
    public TraceRule? Rule { get; init; }
}

/// <summary>What stood when a driven session on a quest started, by the moments the stores keep (D143 point 3).</summary>
public sealed record TraceStood
{
    /// <summary><see cref="TraceStoodGaps.NoMoment"/> where its record says no moment it opened.</summary>
    public string? Missing { get; init; }

    /// <summary>The standing answer for its repository; null for a teammate's, whose machine keeps its own.</summary>
    public TraceStanding? Standing { get; init; }

    public IReadOnlyList<TraceGoAheadStood> GoAheads { get; init; } = [];

    /// <summary>How many of the person's words on its ask came before it.</summary>
    public TraceWordsBefore? Words { get; init; }
}

/// <summary>A repository's standing answer at a start: one of <see cref="TraceStandings"/>, when it was set, and its words.</summary>
public sealed record TraceStanding(string Repository, string State)
{
    /// <summary>Its store: <see cref="TraceStores.Config"/>.</summary>
    public string Source => TraceStores.Config;

    public DateTimeOffset? At { get; init; }

    /// <summary>The person's words, where it stood before the start.</summary>
    public string? Says { get; init; }
}

/// <summary>A go-ahead at a start: one of <see cref="TraceGoAheadStands"/>, the answer's moment, and whether this session asked it.</summary>
public sealed record TraceGoAheadStood(int Number, string State)
{
    public DateTimeOffset? At { get; init; }

    public bool Mine { get; init; }
}

public sealed record TraceWordsBefore(string Ask, int Before, int Of);

/// <summary>
/// A session's entry on the due list (LAND2b, D145, design §8): when it became due, when a try closed it, and each try by its
/// code. Never its tree, which the entry keeps as a machine path.
/// </summary>
public sealed record TraceDue(DateTimeOffset Since)
{
    /// <summary>Its store: <see cref="TraceStores.AutoLandings"/>.</summary>
    public string Source => TraceStores.AutoLandings;

    /// <summary>When a try closed it, or null while it still waits.</summary>
    public DateTimeOffset? Closed { get; init; }

    /// <summary>Each try, oldest first; none before the first.</summary>
    public IReadOnlyList<TraceTry> Tries { get; init; } = [];
}

/// <summary>One try to land a due session: its code, one of <see cref="AutoLandingCode"/>, and the facts it was made at.</summary>
public sealed record TraceTry(DateTimeOffset At, string Code)
{
    /// <summary>The branch it made, or the one it found standing.</summary>
    public string? Branch { get; init; }

    /// <summary>How many commits it carried, where it landed.</summary>
    public int? Commits { get; init; }

    /// <summary>How many paths were uncommitted, where that is what held it.</summary>
    public int? Uncommitted { get; init; }

    /// <summary>The tree's commit when it was tried.</summary>
    public string? Tip { get; init; }
}

/// <summary>The landing rule a branch was made under, as it stood then (LAND2b, D145 point 6).</summary>
/// <param name="Source">Where the rule came from, one of <see cref="LandingSource"/>.</param>
public sealed record TraceRule(string? Plugin, bool AutoAccept, string Source);

/// <summary>A session a landing names and the service holds no record of: what the landing alone says.</summary>
public sealed record TraceUnrecordedLink(string Session)
{
    public IReadOnlyList<TraceBranch> Branches { get; init; } = [];
}

/// <summary>What a read of an id came to: its chain, or why nothing here names it.</summary>
/// <param name="Problem">The terminal's sentence where nothing, or more than one kind, names the id.</param>
public sealed record TraceRead(TraceChain? Chain, string? Problem, IReadOnlyList<TraceUnread> Unread);

/// <summary>
/// The chain, read from the stores' facts (TRACE1b): each link from the store that keeps it, a link nothing keeps with why, and
/// what stood at each start by the moments kept. Both doors word what this returns.
/// </summary>
internal static partial class TraceChains
{
    /// <summary>The states a record ends in (<see cref="SessionRecord"/>'s list): anything else still runs or waits.</summary>
    private static readonly HashSet<string> Endings = new(StringComparer.Ordinal)
    {
        "completed", "declined", "failed", "stopped", "stood-down",
    };

    /// <summary>The states a session's process runs in, whose rules file stands while it does.</summary>
    private static readonly HashSet<string> Running = new(StringComparer.Ordinal) { "queued", "starting", "working" };

    /// <summary>
    /// What a landing keeps in the session's record (D100), read from the writer itself so a reworded note is read the same: the
    /// person's acceptance, a merge into the line's included, an acceptance at the quest's done (LAND2b), and a hand-off after it.
    /// </summary>
    private static readonly string[] Acceptances =
    [
        LandingRules.PersonAccepted,
        LandingRules.AutoAccepted,
        LandingRules.HandNote(new TreeHand(true, "")).Text!,
    ];

    /// <summary>Each store that did not answer, once: its links are then said not read.</summary>
    internal static IReadOnlyList<TraceUnread> Unread(TraceFacts facts)
    {
        var unread = new List<TraceUnread>();
        if (facts.SessionsUnread is { } sessions) unread.Add(new TraceUnread(TraceStores.Sessions) { Problem = sessions });
        if (facts.QuestsUnread is { } quests) unread.Add(new TraceUnread(TraceStores.Quests) { Problem = quests });
        if (facts.LandingsUnread is { } landings) unread.Add(new TraceUnread(TraceStores.Landings) { Problem = landings });
        return unread;
    }

    /// <summary>The asks the chain names, in the order it reaches them: each quest's sender's, then an intake's own.</summary>
    internal static IReadOnlyList<string> AsksNamed(TraceFound found, TraceFacts facts) =>
    [
        .. found.Quests.Select(id => QuestOf(facts, id)).OfType<TracedQuest>().Select(quest => AskWords.AskOf(quest.From))
            .Concat(found.Sessions.Where(session => session.Quest is null).Select(session => session.Ask))
            .OfType<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase),
    ];

    /// <summary>The chain, whole: each ask, its quests and their sessions, in the order the terminal prints them.</summary>
    internal static TraceChain Of(
        TraceFound found, TraceFacts facts, IReadOnlyDictionary<string, TraceAskRead> asks, TraceSources sources, IReadOnlyList<TraceUnread> unread)
    {
        var links = new List<TraceLink>();
        var reached = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var questId in found.Quests)
        {
            var quest = QuestOf(facts, questId);
            var askId = quest is null ? null : AskWords.AskOf(quest.From);
            if (askId is not null && reached.Add(askId))
            {
                links.Add(new TraceLink(TraceLink.AskKind) { Ask = Ask(asks[askId], new TraceNamedBy(TraceNamers.Quest, quest!.Id), facts) });
            }

            var namedBy = found.Sessions.FirstOrDefault(session => Same(session.Quest, questId)) is { } by ? new TraceNamedBy(TraceNamers.Session, by.Id)
                : facts.Landings.FirstOrDefault(landing => Same(landing.Quest, questId)) is { } landing ? new TraceNamedBy(TraceNamers.Landing, landing.Session)
                : new TraceNamedBy(TraceNamers.Trace);
            links.Add(new TraceLink(TraceLink.QuestKind) { Quest = Quest(questId, quest, askId, namedBy, facts) });

            foreach (var session in found.Sessions.Where(session => Same(session.Quest, questId)))
            {
                links.Add(new TraceLink(TraceLink.SessionKind) { Session = Session(session, facts, askId is null ? null : asks[askId], sources) });
            }

            foreach (var landed in found.Unrecorded.Where(id => facts.Landings.Any(each => Same(each.Session, id) && Same(each.Quest, questId))))
            {
                links.Add(new TraceLink(TraceLink.UnrecordedKind) { Unrecorded = Unrecorded(landed, facts) });
            }
        }

        foreach (var session in found.Sessions.Where(session => session.Quest is null))
        {
            if (session.Ask is { } askId && reached.Add(askId))
            {
                links.Add(new TraceLink(TraceLink.AskKind) { Ask = Ask(asks[askId], new TraceNamedBy(TraceNamers.Intake, session.Id), facts) });
            }

            links.Add(new TraceLink(TraceLink.SessionKind) { Session = Session(session, facts, null, sources) });
        }

        foreach (var landed in found.Unrecorded.Where(id => !facts.Landings.Any(each => Same(each.Session, id) && each.Quest is not null)))
        {
            links.Add(new TraceLink(TraceLink.UnrecordedKind) { Unrecorded = Unrecorded(landed, facts) });
        }

        return new TraceChain(found.Kind, found.Id) { Found = found.Found, Unread = unread, Links = links };
    }

    /// <summary>The ask, from its door: the person's words verbatim with how and when each was given, and its go-aheads.</summary>
    private static TraceAskLink Ask(TraceAskRead read, TraceNamedBy namedBy, TraceFacts facts)
    {
        if (read.Unread is { } why) return new TraceAskLink(read.Id) { Missing = TraceLinkGaps.Unread, Problem = why, NamedBy = namedBy };
        if (read.Ask is not { } ask) return new TraceAskLink(read.Id) { Missing = TraceLinkGaps.NotFound, NamedBy = namedBy };

        return new TraceAskLink(ask.Id)
        {
            NamedBy = namedBy,
            Workspace = ask.Workspace,
            State = ask.State,
            Tier = ask.Tier,
            Intake = ask.Intake is { } intake
                ? SessionOf(facts, intake) is { } record
                    ? new TraceIntake(intake) { State = StateOf(record), Agent = AgentOf(record) }
                    : new TraceIntake(intake)
                : null,
            Quests = ask.Quests,
            Note = ask.Note is { Length: > 0 } note ? note : null,
            Sentence = ask.Words is null ? ask.Sentence : null,
            Words = ask.Words,
            WordsKeptFrom = ask.Words is null ? null : ask.WordsKeptFrom,
            GoAheads = ask.GoAheads?.Select(goAhead =>
            {
                var first = goAhead.Asked.MinBy(request => request.At);
                return new TraceGoAhead(goAhead.Number, goAhead.Kind, goAhead.On, goAhead.Act)
                {
                    Answer = goAhead.Answer,
                    FirstAskedBy = first?.Session,
                    FirstAskedAt = first?.At,
                    Near = goAhead.Near,
                };
            }).ToList(),
        };
    }

    /// <summary>The quest as its operations replay, from its door: where it came from, its requirements and answers, its records.</summary>
    private static TraceQuestLink Quest(string id, TracedQuest? quest, string? askId, TraceNamedBy namedBy, TraceFacts facts)
    {
        var records = RecordsOn(id, facts);
        if (quest is null)
        {
            return new TraceQuestLink(id)
            {
                Missing = facts.QuestsUnread is not null ? TraceLinkGaps.Unread : TraceLinkGaps.NotFound,
                NamedBy = namedBy,
                Records = records,
            };
        }

        return new TraceQuestLink(quest.Id)
        {
            NamedBy = namedBy,
            Address = quest.Address,
            Title = quest.Title,
            Status = quest.Status,
            Filed = quest.Filed,
            Moved = quest.Updated,
            Ask = askId,
            From = quest.From,
            Parent = quest.Parent,
            PublishedBy = quest.PublishedBy,
            Awaits = quest.Awaits,
            Held = quest.Held,
            Accepted = quest.Accepted,
            Note = quest.Note,
            Requirements =
            [
                .. quest.Requirements.Select((requirement, index) =>
                {
                    var number = index + 1;
                    var answer = quest.Answers.FirstOrDefault(each => each.Requirement == number);
                    return answer switch
                    {
                        { Met: { } met } => new TraceRequirement(number, requirement.Quote, requirement.Check, TraceAnswers.Met) { Met = met },
                        { Departed: { } departed } => new TraceRequirement(number, requirement.Quote, requirement.Check, TraceAnswers.Departed)
                        {
                            Departed = departed,
                            On = answer.Quote,
                        },
                        _ => new TraceRequirement(number, requirement.Quote, requirement.Check,
                            quest.Status == "Done" ? TraceAnswers.Unanswered : TraceAnswers.NotYet),
                    };
                }),
            ],
            Then = [.. quest.Then.Select(step => new TraceStep(step.To, step.Title))],
            Records = records,
        };
    }

    /// <summary>The records that name a quest, oldest first, by id and state; null where the session records could not be read.</summary>
    private static IReadOnlyList<TraceRecordRef>? RecordsOn(string questId, TraceFacts facts) =>
        facts.SessionsUnread is not null
            ? null
            : [.. facts.Sessions.Where(session => Same(session.Quest, questId))
                .OrderBy(session => session.Created ?? DateTimeOffset.MaxValue)
                .Select(session => new TraceRecordRef(session.Id, session.State))];

    /// <summary>One session: its record's facts, then this machine's (its events, its rules, its landing), then what stood when it started.</summary>
    private static TraceSessionLink Session(TracedSession session, TraceFacts facts, TraceAskRead? ask, TraceSources sources)
    {
        var events = session.Teammate ? null : Events(session, sources.Home);
        return new TraceSessionLink(session.Id)
        {
            Kind = session.Kind,
            State = StateOf(session),
            Opened = session.Created,
            Moved = session.Updated,
            Agent = AgentOf(session),
            Teammate = session.Teammate,
            Tree = session.Teammate ? null : FolderOf(session.Tree),
            TreePath = session.Teammate ? null : session.Tree,
            BaseCommit = session.Teammate ? null : session.BaseCommit,
            Quest = session.Quest,
            Ask = session.Ask,
            Before = Before(session, facts),
            Took = session.Took,
            Note = session.Note,
            Answer = session.Answer,
            Evidence = Evidence(session),
            Events = events,
            Rules = session.Teammate ? null : Rules(session, sources.Home),
            Landing = session.Teammate ? null : Landing(session, facts, events),
            Stood = Stood(session, ask, sources.Config),
        };
    }

    /// <summary>The record before it on its quest, by when each opened: its ending, and whether it ran in the same tree.</summary>
    private static TraceBefore? Before(TracedSession session, TraceFacts facts)
    {
        if (session.Quest is null || session.Created is not { } opened) return null;
        var before = facts.Sessions
            .Where(each => Same(each.Quest, session.Quest) && each.Created is { } at && at < opened)
            .MaxBy(each => each.Created);
        if (before is null) return new TraceBefore(First: true);

        return new TraceBefore(First: false)
        {
            Session = before.Id,
            State = before.State,
            Ended = Endings.Contains(before.State),
            At = before.Updated,
            Tree = before.Tree is null || session.Tree is null ? TraceTrees.Unknown
                : SamePath(before.Tree, session.Tree) ? TraceTrees.Same
                : TraceTrees.Other,
        };
    }

    /// <summary>The commits the driver read off its tree at its end, as its record keeps them.</summary>
    private static TraceEvidence? Evidence(TracedSession session)
    {
        if (session.Evidence is not { } evidence) return null;
        var lines = evidence.ReplaceLineEndings("\n").Split('\n');
        return new TraceEvidence(
            lines[0],
            [.. lines.Skip(1).Where(line => line.Trim().Length > 0).Select(line => line.Trim())],
            [.. session.Commits.Select(commit => new TraceCommit(commit.Sha, commit.Line))]);
    }

    /// <summary>
    /// This machine's record of it (D76): the driver's notes before its instruction, which name the account a start chose and
    /// why; each instruction it was handed, by its event and size; and each acceptance of its work a landing's press kept.
    /// </summary>
    private static TraceEvents Events(TracedSession session, string home)
    {
        if (!SessionEvents.IsId(session.Id)) return new TraceEvents { Missing = TraceEventGaps.NotAnId };

        var events = new SessionEvents(Path.Combine(home, "sessions"));
        IReadOnlyList<SessionEvent> all;
        try
        {
            if (!File.Exists(events.PathOf(session.Id))) return new TraceEvents { Missing = TraceEventGaps.NoneHere };
            all = events.After(session.Id, 0).Events;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or DriverException)
        {
            return new TraceEvents { Missing = TraceEventGaps.Unread, Problem = error.Message };
        }

        return new TraceEvents
        {
            Starts =
            [
                .. all.TakeWhile(e => e.Kind != SessionEventKind.User)
                    .Where(e => e.Kind == SessionEventKind.Note && e.Text is { Length: > 0 })
                    .Select(note => note.Text!),
            ],
            Instructions = [.. all.Where(e => e.Kind == SessionEventKind.User && e.Origin == "target").Select(Instruction)],
            Accepted =
            [
                .. all.Where(e => e.Kind == SessionEventKind.Note && e.Text is { } said
                                  && Acceptances.Any(start => said.StartsWith(start, StringComparison.Ordinal)))
                    .Select(note => new TraceAcceptance(note.At, note.Text!)),
            ],
        };
    }

    /// <summary>One instruction by its event and its size: the length written, or, where the record cut it, the length it said.</summary>
    private static TraceInstruction Instruction(SessionEvent instruction)
    {
        var said = instruction.Text ?? "";
        if (CutSaid().Match(said) is { Success: true } cut && cut.Groups["kept"].Length == SessionEvents.TextLimit
            && int.TryParse(cut.Groups["chars"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var whole))
        {
            return new TraceInstruction(instruction.Seq, whole) { Kept = SessionEvents.TextLimit, Account = instruction.Account };
        }

        return new TraceInstruction(instruction.Seq, said.Length) { Account = instruction.Account };
    }

    /// <summary>
    /// The rules it was handed (PERM1, D72), from the file the driver wrote under the home for it, while that file stands: it is
    /// the session's own and goes when its run ends.
    /// </summary>
    private static TraceRules Rules(TracedSession session, string home)
    {
        var path = Path.Combine(home, SpawnServers.Folder, $"{session.Id}.settings.json");
        if (!File.Exists(path))
        {
            return new TraceRules { Missing = Running.Contains(session.State) ? TraceRuleGaps.NoneWhileRunning : TraceRuleGaps.GoneWithRun };
        }

        try
        {
            using var document = System.Text.Json.JsonDocument.Parse(File.ReadAllText(path));
            var root = document.RootElement;
            var permissions = root.TryGetProperty("permissions", out var lists) ? lists : default;
            int Listed(System.Text.Json.JsonElement element, string list) =>
                element.ValueKind == System.Text.Json.JsonValueKind.Object && element.TryGetProperty(list, out var items)
                && items.ValueKind == System.Text.Json.JsonValueKind.Array
                    ? items.GetArrayLength()
                    : 0;
            var hard = root.TryGetProperty("autoMode", out var auto) && auto.TryGetProperty("hard_deny", out var denials)
                       && denials.ValueKind == System.Text.Json.JsonValueKind.Array
                ? denials.EnumerateArray().Count(entry => entry.ValueKind == System.Text.Json.JsonValueKind.String && entry.GetString() != SpawnSettings.HarnessDefaults)
                : 0;
            return new TraceRules
            {
                Allowed = Listed(permissions, "allow"),
                Asked = Listed(permissions, "ask"),
                Denied = Listed(permissions, "deny"),
                Hard = hard,
                Guarded = root.TryGetProperty("hooks", out var hooks) && hooks.ValueKind == System.Text.Json.JsonValueKind.Object
                          && hooks.TryGetProperty("PreToolUse", out _),
            };
        }
        catch (Exception error) when (error is System.Text.Json.JsonException or IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return new TraceRules { Missing = TraceRuleGaps.Unread, Problem = error.Message };
        }
    }

    /// <summary>
    /// Where its work landed, from <c>landings.json</c> (WSR5): each branch a landing made of it, standing or gone. A landing
    /// into the line records no branch, so where none is, the acceptance its record keeps is pointed to, or said absent.
    /// </summary>
    private static TraceLanding Landing(TracedSession session, TraceFacts facts, TraceEvents? events)
    {
        if (facts.LandingsUnread is not null) return new TraceLanding { Missing = TraceLandingGaps.Unread };

        var branches = facts.Landings.Where(each => Same(each.Session, session.Id)).Select(Branch).ToList();
        var due = Due(session, facts);
        if (branches.Count > 0) return new TraceLanding { Branches = branches, Due = due };

        return new TraceLanding
        {
            Missing = events is null || events.Missing is not null ? TraceLandingGaps.Unknown
                : events.Accepted.Count == 0 ? TraceLandingGaps.None
                : TraceLandingGaps.MergeAccepted,
            Due = due,
        };
    }

    /// <summary>
    /// Its entry on the due list (LAND2b, design §8), where it has one: when it became due, when a try closed it, and each try by
    /// its code with the branch it made or met. The entry's tree and status fingerprint stay on this machine.
    /// </summary>
    private static TraceDue? Due(TracedSession session, TraceFacts facts) =>
        facts.AutoLandings.FirstOrDefault(each => Same(each.Session, session.Id)) is not { } entry
            ? null
            : new TraceDue(entry.DueAt)
            {
                Closed = entry.Closed,
                Tries =
                [
                    .. entry.Tries.Select(tried => new TraceTry(tried.At, tried.Code)
                    {
                        Branch = tried.Branch,
                        Commits = tried.Commits,
                        Uncommitted = tried.Uncommitted,
                        Tip = tried.Tip,
                    }),
                ],
            };

    /// <summary>A landing whose session the service holds no record of: what the landing alone says.</summary>
    private static TraceUnrecordedLink Unrecorded(string session, TraceFacts facts) =>
        new(session) { Branches = [.. facts.Landings.Where(each => Same(each.Session, session)).Select(Branch)] };

    private static TraceBranch Branch(LandedBranch landing) => new(landing.Branch, landing.Tip, landing.LandedAt)
    {
        Line = landing.Line,
        From = landing.From,
        Plugin = landing.Plugin,
        Pushed = landing.Pushed,
        PullRequest = landing.PullRequest,
        PullRequestState = landing.PullRequestState,
        PullRequestAskFailed = landing.PullRequestAskFailed,
        Gone = landing.GoneAt,
        RemovedAs = landing.GoneAt is null ? null : landing.RemovedAs,
        RemovedOn = landing.GoneAt is null || landing.RemovedAs is null ? null : landing.RemovedOn,
        AcceptedBy = landing.AcceptedBy,
        Rule = landing.Rule is { } rule ? new TraceRule(rule.Plugin, rule.AutoAccept, rule.Source) : null,
    };

    /// <summary>
    /// What stood when a driven session on a quest started: the standing answer for its repository, each go-ahead on its ask,
    /// and how many of the person's words were said before it, each by the moments kept and nothing else.
    /// </summary>
    private static TraceStood? Stood(TracedSession session, TraceAskRead? ask, DriverConfig config)
    {
        if (session.Kind != "driven" || session.Quest is null) return null;
        if (session.Created is not { } opened) return new TraceStood { Missing = TraceStoodGaps.NoMoment };

        TraceStanding? standing = null;
        if (!session.Teammate)
        {
            var held = config.StandingFor(session.Repository);
            standing = held is null ? new TraceStanding(session.Repository, TraceStandings.None)
                : held.At is not { } set ? new TraceStanding(session.Repository, TraceStandings.MomentUnknown)
                : set < opened ? new TraceStanding(session.Repository, TraceStandings.Before) { At = set, Says = held.Says }
                : new TraceStanding(session.Repository, TraceStandings.After) { At = set };
        }

        if (ask?.Ask is not { } read) return new TraceStood { Standing = standing };

        return new TraceStood
        {
            Standing = standing,
            GoAheads =
            [
                .. (read.GoAheads ?? []).Select(goAhead =>
                {
                    var first = goAhead.Asked.MinBy(request => request.At);
                    var mine = goAhead.Asked.Any(request => Same(request.Session, session.Id));
                    return first is null || first.At >= opened ? new TraceGoAheadStood(goAhead.Number, TraceGoAheadStands.AskedAfter) { Mine = mine }
                        : goAhead.Answer is not { } answer ? new TraceGoAheadStood(goAhead.Number, TraceGoAheadStands.Waiting) { Mine = mine }
                        : answer.At < opened
                            ? new TraceGoAheadStood(goAhead.Number, answer.Approved ? TraceGoAheadStands.ApprovedBefore : TraceGoAheadStands.RefusedBefore)
                            {
                                At = answer.At,
                                Mine = mine,
                            }
                        : new TraceGoAheadStood(goAhead.Number, TraceGoAheadStands.AnsweredAfter) { At = answer.At, Mine = mine };
                }),
            ],
            Words = read.Words is { } words ? new TraceWordsBefore(read.Id, words.Count(word => word.At < opened), words.Count) : null,
        };
    }

    /// <summary>A record's state, with what its flags add to it.</summary>
    private static TraceState StateOf(TracedSession session) => new(session.State)
    {
        Limit = session.State == "failed" && session.Limit,
        Interrupted = session.State == "stopped" && session.Interrupted,
        Answered = session.State == "awaiting-person" && session.Answer is not null,
    };

    /// <summary>What ran it, as its record names it.</summary>
    private static TraceAgent AgentOf(TracedSession session) => new()
    {
        Adapter = session.Adapter,
        Harness = session.HarnessVersion,
        Account = session.Teammate ? null : session.Profile,
        Teammate = session.Teammate,
    };

    /// <summary>A tree's last segment: the name Daoris gave it (D51 §2), never the path, which is machine-local.</summary>
    internal static string? FolderOf(string? tree)
    {
        if (tree is null) return null;
        var trimmed = tree.Replace('\\', '/').TrimEnd('/');
        var name = trimmed[(trimmed.LastIndexOf('/') + 1)..];
        return name.Length == 0 ? null : name;
    }

    private static TracedQuest? QuestOf(TraceFacts facts, string id) => facts.Quests.FirstOrDefault(quest => Same(quest.Id, id));

    private static TracedSession? SessionOf(TraceFacts facts, string id) => facts.Sessions.FirstOrDefault(session => Same(session.Id, id));

    private static bool Same(string? a, string? b) => a is not null && b is not null && string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    private static bool SamePath(string a, string b) =>
        string.Equals(a.Replace('\\', '/').TrimEnd('/'), b.Replace('\\', '/').TrimEnd('/'), StringComparison.OrdinalIgnoreCase);

    /// <summary>A field the record cut (<see cref="SessionEvents.Cut"/>): what it kept, then how long the original was.</summary>
    [GeneratedRegex(@"^(?<kept>.*)… \((?<chars>\d+) chars\)$", RegexOptions.Singleline)]
    private static partial Regex CutSaid();
}
