using System.Globalization;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// The coded line of a note (<see cref="Noted"/>, whose shape is the service's): a note composed from one of the driver's
/// codes (<see cref="NoteCode"/>) is the events' own member of it, so every caller keeps <c>Noted.Of</c> as spelled.
/// </summary>
public static class NotedCodes
{
    extension(Noted)
    {
        /// <summary>One coded line, its English the text.</summary>
        public static Noted Of(NoteCode code, string text, params (string Name, object? Value)[] values) =>
            new(text, [code.Part(text, values)]);
    }
}

/// <summary>
/// A code a line of a session's note is worded by: the page's key, and the names of the facts its line carries, each a
/// placeholder in its entry in both catalogues.
/// </summary>
/// <param name="Code">The code, as the record and the page spell it.</param>
/// <param name="Values">The facts its line carries.</param>
/// <param name="Key">The page's key where it is not <c>note.&lt;code&gt;</c>: a sentence the page already words.</param>
public sealed record NoteCode(string Code, IReadOnlyList<string> Values, string? Key = null)
{
    /// <summary>Where <c>why</c> is a reason's code, the reasons it may name, each worded with its own values.</summary>
    public NoteReasons? Why { get; init; }

    /// <summary>The page's key for this code.</summary>
    public string CatalogueKey => Key ?? $"note.{Code}";

    /// <summary>
    /// The part, with its values: each named among this code's, or among its reasons' own; a null value is not written, and
    /// the page then shows the part's text.
    /// </summary>
    /// <exception cref="ArgumentException">A value this code does not declare: its entry could not say it.</exception>
    public NotePart Part(string text, params (string Name, object? Value)[] values)
    {
        var kept = new List<KeyValuePair<string, object>>();
        foreach (var (name, value) in values)
        {
            if (!Values.Contains(name) && Why?.Carries(name) != true)
            {
                throw new ArgumentException($"`{Code}` declares no value `{name}`, so no entry of it can say it", nameof(values));
            }

            if (value is not null) kept.Add(new(name, value));
        }

        return new NotePart { Code = Code, Values = kept, Text = text };
    }
}

/// <summary>One reason a <c>why</c> may name, its own values, and its entry's key.</summary>
public sealed record NoteReason(string Code, IReadOnlyList<string> Values, string Key);

/// <summary>A family of reasons a <c>why</c> names.</summary>
public sealed record NoteReasons(IReadOnlyList<NoteReason> All)
{
    /// <summary>Whether a value is one a reason here carries beside its code.</summary>
    public bool Carries(string value) => All.Any(reason => reason.Values.Contains(value));
}

/// <summary>
/// Every code the driver writes a line of a session's note by. A twin (<c>.claude/knowledge/twins.md</c>): the page's
/// <c>note.json</c> in both languages words each, the service's <c>LedgerNoteCodes</c> declares the ledger's own, and the
/// web's <c>locales/note.test.ts</c> parses these declarations, one per line as written here, to hold the catalogue to them.
/// </summary>
/// <remarks>
/// <b>A site writes a part only through its code here</b>, and <c>NoteCodesTests</c> reads both catalogues, so a new line
/// fails until both languages word it. A comment on a code says what it means to a reader of the note.
/// </remarks>
public static class NoteCodes
{
    /// <summary>Reason codes for why a conversation went on as it did (<see cref="ContinueCodes"/>), each with its own values, as the page words them (<c>work.say.why.*</c>).</summary>
    public static readonly NoteReasons Continue = new(
    [
        new(ContinueCodes.Account, [], "work.say.why.account"),
        new(ContinueCodes.Adapter, ["from", "to"], "work.say.why.adapter"),
        new(ContinueCodes.Unkept, [], "work.say.why.unkept"),
        new(ContinueCodes.Tree, [], "work.say.why.tree"),
        new(ContinueCodes.Unable, ["adapter"], "work.say.why.unable"),
        new(ContinueCodes.Offered, [], "work.say.why.offered"),
        new(ContinueCodes.Gone, [], "work.say.why.gone"),
        new(ContinueCodes.Refused, [], "work.say.why.refused"),
        new(ContinueCodes.Ended, [], "work.say.why.ended"),
        new(ContinueCodes.Elsewhere, ["agent"], "work.say.why.elsewhere"),
        new(ContinueCodes.Teammate, [], "work.say.why.teammate"),
        new(ContinueCodes.Intake, [], "work.say.why.intake"),
        new(ContinueCodes.StoodDown, [], "work.say.why.stoodDown"),
        new(ContinueCodes.Started, [], "work.say.why.started"),
        new(ContinueCodes.Exited, [], "work.say.why.exited"),
        new(ContinueCodes.Resumed, [], "work.say.why.resumed"),
    ]);

    /// <summary>Why a cool-off lasts until then, as the page words it (<c>harness.cooling.why.*</c>).</summary>
    public static readonly NoteReasons Cooling = new(
    [
        new(CoolingWhy.Stated, [], "harness.cooling.why.stated"),
        new(CoolingWhy.Assumed, [], "harness.cooling.why.assumed"),
        new(CoolingWhy.Default, [], "harness.cooling.why.default"),
        new(CoolingWhy.NotBelieved, [], "harness.cooling.why.notBelieved"),
    ]);

    /// <summary>
    /// Why a start ran on its account, a step of the walk as a conversation's first line says it
    /// (<see cref="RotationWords.Steps"/>): each with its own values, the other account it names always <c>over</c>, and a
    /// moment, a percent or a window as a fact, never a clause.
    /// </summary>
    public static readonly NoteReasons Opening = new(
    [
        new(OpeningWhy.Cooling, ["agent", "over", "until", "cooling"], "note.opening.why.cooling"),
        new(OpeningWhy.Refused, ["agent", "over"], "note.opening.why.refused"),
        new(OpeningWhy.SignedOut, ["agent", "over"], "note.opening.why.signed-out"),
        new(OpeningWhy.Locked, ["agent", "over"], "note.opening.why.locked"),
        new(OpeningWhy.KeptSelf, [], "note.opening.why.kept-self"),
        new(OpeningWhy.KeptLast, ["over"], "note.opening.why.kept-last"),
        new(OpeningWhy.Kept, ["over"], "note.opening.why.kept"),
        new(OpeningWhy.NearReached, ["over", "window"], "note.opening.why.near-reached"),
        new(OpeningWhy.NearWord, ["over", "window"], "note.opening.why.near-word"),
        new(OpeningWhy.NearCredits, ["over"], "note.opening.why.near-credits"),
        new(OpeningWhy.NearUsed, ["over", "used", "window", "near"], "note.opening.why.near-used"),
        new(OpeningWhy.OverCurve, ["over", "window", "used", "curve"], "note.opening.why.over-curve"),
        new(OpeningWhy.Near, ["over"], "note.opening.why.near"),
        new(OpeningWhy.Room, ["over", "used", "against"], "note.opening.why.room"),
        new(OpeningWhy.Fewest, [], "note.opening.why.fewest"),
        new(OpeningWhy.Lapsing, ["at"], "note.opening.why.lapsing"),
        new(OpeningWhy.Lapses, ["window", "unused", "at", "seen"], "note.opening.why.lapses"),
        new(OpeningWhy.MostRoom, ["room", "window"], "note.opening.why.most-room"),
        new(OpeningWhy.Behind, ["used", "gone"], "note.opening.why.behind"),
        new(OpeningWhy.Ahead, ["over", "used", "gone"], "note.opening.why.ahead"),
        new(OpeningWhy.NotStarted, [], "note.opening.why.not-started"),
        new(OpeningWhy.LeastRecent, [], "note.opening.why.least-recent"),
        new(OpeningWhy.Default, ["workspace"], "note.opening.why.default"),
        new(OpeningWhy.DefaultHere, [], "note.opening.why.default-here"),
        new(OpeningWhy.First, ["workspace"], "note.opening.why.first"),
        new(OpeningWhy.FirstHere, [], "note.opening.why.first-here"),
        new(OpeningWhy.FirstOrder, [], "note.opening.why.first-order"),
    ]);

    /// <summary>
    /// Who spoke the words a resumed conversation goes on with (<see cref="Continuations.Opening"/>): a reason per way the
    /// English says it, with no value of its own.
    /// </summary>
    public static readonly NoteReasons Resumed = new(
    [
        new(ResumedWhy.Answer, [], "note.resumed.why.answer"),
        new(ResumedWhy.Words, [], "note.resumed.why.words"),
        new(ResumedWhy.Findings, [], "note.resumed.why.findings"),
        new(ResumedWhy.AnswerAndFindings, [], "note.resumed.why.answer-and-findings"),
        new(ResumedWhy.WordsAndFindings, [], "note.resumed.why.words-and-findings"),
        new(ResumedWhy.Helper, [], "note.resumed.why.helper"),
        new(ResumedWhy.WordsAndHelper, [], "note.resumed.why.words-and-helper"),
        new(ResumedWhy.Daoris, [], "note.resumed.why.daoris"),
        new(ResumedWhy.Supervisor, [], "note.resumed.why.supervisor"),
    ]);

    /// <summary>Why the person's words never reached the session (<see cref="UnreachedWhy"/>): the page's <c>note.words.why.*</c>.</summary>
    public static readonly NoteReasons Unreached = new(
    [
        new(UnreachedWhy.EndedFirst, [], "note.words.why.ended-first"),
        new(UnreachedWhy.Unkept, [], "note.words.why.unkept"),
        new(UnreachedWhy.Unmarked, [], "note.words.why.unmarked"),
    ]);

    // ——— A session's end.
    public static readonly NoteCode EndedAwaits = new("ended.awaits", ["awaits"]);
    public static readonly NoteCode EndedExit = new("ended.exit", ["exit"]);
    public static readonly NoteCode EndedTurnFailedTaken = new("ended.turn-failed-taken", []);
    public static readonly NoteCode EndedTurnFailedOpen = new("ended.turn-failed-open", []);

    /// <summary>
    /// Its model's provider refused a call and the turn ended on it: a failed run and a strike, never a park, followed by the
    /// provider's words with their request id.
    /// </summary>
    public static readonly NoteCode EndedProviderRefused = new("ended.provider-refused", []);

    /// <summary>
    /// Its provider kept failing after every try a transient episode made, with the tries made and the minutes actually waited,
    /// followed by the provider's words: failed, no strike, no cool-off, its quest held for the person's word.
    /// </summary>
    public static readonly NoteCode EndedTransient = new("ended.transient", ["count", "minutes"]);

    /// <summary>
    /// Said once at a transient episode's first failure, with the schedule's tries and minutes as <i>up to</i> and when the
    /// first try goes, followed by the provider's words. No line per try.
    /// </summary>
    public static readonly NoteCode AgentTransientRetrying = new("agent.transient-retrying", ["agent", "count", "minutes", "at"]);

    /// <summary>
    /// A conversation's turn ended <c>error</c> because its provider kept failing after every try, followed by the provider's
    /// words. The conversation stays, and the person's next word is a turn of it.
    /// </summary>
    public static readonly NoteCode ChatTransientStopped = new("chat.transient-stopped", ["agent"]);

    /// <summary>
    /// Its agent did not offer, or refused, the model its actor asked at the open, before any prompt: failed, no strike, its quest
    /// planned again. Names no account, since the note travels.
    /// </summary>
    public static readonly NoteCode EndedChoiceNotOffered = new("ended.choice-not-offered", ["agent", "model"]);
    public static readonly NoteCode EndedParkedAsked = new("ended.parked-asked", []);
    public static readonly NoteCode EndedParkedTranscript = new("ended.parked-transcript", []);
    public static readonly NoteCode EndedParkedShort = new("ended.parked-short", []);

    /// <summary>
    /// Its agent ended its turn <c>cancelled</c> though nobody stopped it, after the permission it asked for (<c>call</c>, as
    /// the agent titled it) was refused: a park on the person that says it asked them nothing.
    /// </summary>
    public static readonly NoteCode EndedParkedCut = new("ended.parked-cut", ["call"]);

    /// <summary>As <see cref="EndedParkedCut"/>, with no refused call behind the agent's cancel.</summary>
    public static readonly NoteCode EndedParkedCancelled = new("ended.parked-cancelled", []);

    /// <summary>
    /// It ended its turn cleanly on its own open helper ask, so it waits on that helper and not on the person. A twin: the
    /// service's ledger reads this spelling to bring the session back to the person when the helper ends with no answer.
    /// </summary>
    public static readonly NoteCode EndedAwaitsHelper = new("ended.awaits-helper", []);
    public static readonly NoteCode EndedAnsweredUnfinished = new("ended.answered-unfinished", ["awaits", "exit"]);
    public static readonly NoteCode EndedCarriedUnfinished = new("ended.carried-unfinished", ["exit"]);
    public static readonly NoteCode EndedDone = new("ended.done", []);

    // A closure graph's quest is never done before the graph's end, so a session ends with the quest still taken, at its submission.
    public static readonly NoteCode EndedSubmitted = new("ended.submitted", [], Key: "statusHint.Submitted");
    public static readonly NoteCode EndedDeclined = new("ended.declined", []);
    public static readonly NoteCode EndedStoodDown = new("ended.stood-down", []);

    /// <summary>
    /// It stopped with its quest still taken, held because the person reopened the work it grew from (<c>quest</c>). A good
    /// ending: the quest goes on in the same tree. A twin: the service's ledger reads this spelling to open that carry-on.
    /// </summary>
    public static readonly NoteCode EndedSourceReopened = new("ended.source-reopened", ["quest"]);

    /// <summary>
    /// On the native door its turn ended while its own background work ran (<c>works</c>, as its agent named them), and its
    /// harness ends such work with the turn, so it was cut off: carried on in the same tree.
    /// </summary>
    public static readonly NoteCode EndedBackgroundCut = new("ended.background-cut", ["works"]);
    public static readonly NoteCode EndedTakenExit = new("ended.taken-exit", ["exit"]);
    public static readonly NoteCode EndedUntouched = new("ended.untouched", []);
    public static readonly NoteCode EndedUntouchedExit = new("ended.untouched-exit", ["exit"]);
    public static readonly NoteCode EndedWentOn = new("ended.went-on", []);
    public static readonly NoteCode EndedWentOnExit = new("ended.went-on-exit", ["exit"]);
    // A closed quest's session whose turn failed going on: the door's reason follows, in the agent's words.
    public static readonly NoteCode EndedWentOnFailed = new("ended.went-on-failed", []);
    // The person's words a failed turn carried wait to go again, or, failing twice, wait for the person.
    public static readonly NoteCode EndedWordsWait = new("ended.words-wait", []);
    public static readonly NoteCode EndedWordsWaitYou = new("ended.words-wait-you", []);
    // The person's words a turn an account's limit refused carried wait for the limit to lift, never failed twice.
    public static readonly NoteCode EndedLimitWordsWait = new("ended.limit-words-wait", []);
    // An attempt refused by the same kind of limit (`hit`) just after its account's cool-off ended.
    public static readonly NoteCode LimitNotLifted = new("limit.not-lifted", ["hit"]);

    // Daoris ended a run at its turn's end because its repository's permission prompts changed (`from`, `to`); the words it held go on
    // in the same conversation under the new setup.
    public static readonly NoteCode EndedSetupChanged = new("ended.setup-changed", ["from", "to"]);
    public static readonly NoteCode EndedWordsSetup = new("ended.words-setup", []);
    public static readonly NoteCode EndedTimeout = new("ended.timeout", ["minutes"]);
    // Its agent's process exited but something it started held its output, so Daoris ended what was left and the run concluded by its exit.
    public static readonly NoteCode EndedGone = new("ended.gone", []);
    // The watcher's own end of a quiet run (`minutes` unheard).
    public static readonly NoteCode EndedQuiet = new("ended.quiet", ["minutes"]);
    // The person ended a quiet run to carry it on.
    public static readonly NoteCode EndedQuietPressed = new("ended.quiet-pressed", ["minutes"]);
    public static readonly NoteCode WatchQuiet = new("watch.quiet", ["minutes"]);
    // The same mark naming what it was doing: the call its agent was inside, or the background work (`works`, a list) it waited on that showed nothing.
    public static readonly NoteCode WatchQuietCall = new("watch.quiet-call", ["minutes", "call"]);
    public static readonly NoteCode WatchQuietWork = new("watch.quiet-work", ["minutes", "works"]);
    public static readonly NoteCode WatchLeft = new("watch.left", []);
    // A move to a new part that a live Daoris keeps and has not moved for `minutes`, said once: never taken over while that Daoris runs.
    public static readonly NoteCode MoveQuiet = new("move.quiet", ["minutes"]);
    public static readonly NoteCode EndedStopped = new("ended.stopped", []);
    // The steward's own stop, a behavioural sibling of the person's.
    public static readonly NoteCode EndedStoppedSteward = new("ended.stopped-steward", []);
    public static readonly NoteCode EndedLostClaim = new("ended.lost-claim", []);
    // A take lost to the person's reopen of the work its step grew from, not to another machine's take.
    public static readonly NoteCode EndedLostClaimReopened = new("ended.lost-claim-reopened", []);
    // A quest's session stopped to go on with another agent: the agent it went to, never an account or a path.
    public static readonly NoteCode EndedHandedOver = new("ended.handed-over", ["agent"]);
    public static readonly NoteCode EndedDriverClosed = new("ended.driver-closed", []);

    // ——— An account's line, appended to a failure. A limit's cooling line names whose accounts they are, never which, and the window
    // its reset named. `account.cooling` is no longer written but notes on machines carry it, and the page words a line only with every
    // value its entry says, so a code that gained a value would show each of them as recorded: a limit that named no window has its own line.
    public static readonly NoteCode AccountCooling = new("account.cooling", ["until", "why"]) { Why = Cooling };
    public static readonly NoteCode AccountCoolingWindow = new("account.cooling-window", ["until", "why", "owner", "window"]) { Why = Cooling };
    public static readonly NoteCode AccountCoolingNoWindow = new("account.cooling-no-window", ["until", "why", "owner"]) { Why = Cooling };
    public static readonly NoteCode AccountRefused = new("account.refused", ["owner"]);
    public static readonly NoteCode AccountRefusedOwn = new("account.refused-own", ["owner"]);
    public static readonly NoteCode AccountSignedOut = new("account.signed-out", ["owner"]);
    public static readonly NoteCode AccountSignedOutOwn = new("account.signed-out-own", ["owner"]);

    // The account a start ran on did not offer, or refused, the model its actor asked, before any prompt: the account's fault,
    // never the quest's, and no account is named, since the note travels.
    public static readonly NoteCode AccountRefusedModel = new("account.refused-model", ["agent", "model"]);

    /// <summary>
    /// The account's lines that say its agent or its provider refused the account a start ran on: its sign-in, its credential or
    /// the model asked. The fault is the account's, never the quest's, so a failure carrying one is no strike
    /// (<see cref="ServiceClient.ReadStrikes"/>), read by the code the driver wrote, never by the words.
    /// </summary>
    public static IReadOnlyList<NoteCode> AccountsOwn { get; } =
        [AccountRefused, AccountRefusedOwn, AccountSignedOut, AccountSignedOutOwn, AccountRefusedModel];

    // ——— Why the account a resume asked for could not carry the person's words, after a line whose reason is `account`: no account
    // is named, since the note travels.
    public static readonly NoteCode AccountResumeSignedOut = new("account.resume-signed-out", []);
    public static readonly NoteCode AccountResumeRefused = new("account.resume-refused", []);
    public static readonly NoteCode AccountResumeGone = new("account.resume-gone", []);
    public static readonly NoteCode AccountResumeOutside = new("account.resume-outside", []);
    public static readonly NoteCode AccountResumeKept = new("account.resume-kept", []);
    public static readonly NoteCode AccountResumeNewSession = new("account.resume-new-session", []);
    public static readonly NoteCode AccountResumeNotOffered = new("account.resume-not-offered", []);

    // ——— While it starts and works, replaced by its end.
    public static readonly NoteCode StartedTree = new("started.tree", ["branch", "basedOn"]);
    public static readonly NoteCode StartedTreeUnrecorded = new("started.tree-unrecorded", []);
    // A tree its checkout's dependencies were copied into (`started.tree` stays as recorded); and why a project's were not, each
    // reason its own line, after the opening.
    public static readonly NoteCode StartedTreeCopied = new("started.tree-copied", ["branch", "basedOn", "copied"]);
    public static readonly NoteCode StartedUncopiedManager = new("started.uncopied-manager", ["modules"]);
    public static readonly NoteCode StartedUncopiedMoved = new("started.uncopied-moved", ["modules"]);
    public static readonly NoteCode StartedUncopiedUninstalled = new("started.uncopied-uninstalled", ["modules"]);
    public static readonly NoteCode StartedUncopiedWorkspace = new("started.uncopied-workspace", ["modules", "workspace"]);
    public static readonly NoteCode StartedUncopiedLink = new("started.uncopied-link", ["modules"]);
    public static readonly NoteCode StartedUncopiedUnignored = new("started.uncopied-unignored", ["modules"]);
    public static readonly NoteCode StartedUncopiedFailed = new("started.uncopied-failed", ["modules"]);
    // The machine-local files a repository's gates read, copied into its tree read-only, after the opening; and why one was not.
    public static readonly NoteCode StartedLocalCopied = new("started.local-copied", ["inputs"]);
    public static readonly NoteCode StartedLocalMissing = new("started.local-missing", ["input"]);
    public static readonly NoteCode StartedLocalRefused = new("started.local-refused", ["input"]);
    public static readonly NoteCode StartedLocalFailed = new("started.local-failed", ["input"]);
    // A session's tree asked for before it goes on: made again on its branch, gone with its branch, or not made.
    public static readonly NoteCode TreeRemade = new("tree.remade", ["branch"]);
    public static readonly NoteCode TreeGone = new("tree.gone", ["branch"]);
    // Its tree and branch gone with nothing beyond the line, its part goes on in a fresh tree.
    public static readonly NoteCode TreeFresh = new("tree.fresh", ["branch"]);
    public static readonly NoteCode TreeUnmade = new("tree.unmade", []);
    public static readonly NoteCode StartedJoined = new("started.joined", ["session"]);
    public static readonly NoteCode StartedResumesAnswered = new("started.resumes-answered", ["quest", "answered"]);
    public static readonly NoteCode StartedInAskingTree = new("started.in-asking-tree", ["session"]);
    public static readonly NoteCode StartedWordsOpen = new("started.words-open", ["quest", "session"]);
    public static readonly NoteCode StartedWordsTaken = new("started.words-taken", ["quest", "session"]);
    public static readonly NoteCode StartedAnswer = new("started.answer", ["quest", "session"]);
    public static readonly NoteCode StartedReleased = new("started.released", ["quest", "session"]);
    // A quest carried on after the steward's stop of its session was released.
    public static readonly NoteCode StartedReleasedSteward = new("started.released-steward", ["quest", "session"]);
    public static readonly NoteCode StartedCutOff = new("started.cut-off", ["quest", "session"]);
    // An open quest carried on in the tree whose session committed work and failed before it took it: never rebuilt.
    public static readonly NoteCode StartedCarriedWork = new("started.carried-work", ["quest", "session"]);
    // A stand-down carried on once the person brought its lost take forward: the press, never a cut-off.
    public static readonly NoteCode StartedBroughtForward = new("started.brought-forward", ["quest", "session"]);
    // A quest handed to another agent: the carry-on's first line says which agent it came from, and where the account the hand-over
    // named could not run it, that another of the named agent's did. Facts only, no account's name.
    public static readonly NoteCode StartedHandedOver = new("started.handed-over", ["quest", "session", "agent"]);
    public static readonly NoteCode StartedHandedWalked = new("started.handed-walked", []);
    public static readonly NoteCode StartedOtherAccount = new("started.other-account", []);
    public static readonly NoteCode StartedSameTree = new("started.same-tree", []);
    public static readonly NoteCode StartedFellBack = new("started.fell-back", ["why"]) { Why = Continue };
    public static readonly NoteCode WorkingResumesAnswer = new("working.resumes-answer", []);
    public static readonly NoteCode WorkingGoesOn = new("working.goes-on", []);
    // A park going on with none of the owner's words, the authors that are there in a fixed order (`supervisor`, `helper`, `opinion`,
    // `daoris`), never "your answer".
    public static readonly NoteCode WorkingResumesOthers = new("working.resumes-others", ["authors"]);

    /// <summary>Why a conversation was handed to another agent (<see cref="HandOverWhy"/>): the page's <c>note.handed.why.*</c>.</summary>
    public static readonly NoteReasons Handed = new(
    [
        new(HandOverWhy.Press, [], "note.handed.why.press"),
        new(HandOverWhy.Near, [], "note.handed.why.near"),
        new(HandOverWhy.Limit, [], "note.handed.why.limit"),
        new(HandOverWhy.Agent, [], "note.handed.why.agent"),
        new(HandOverWhy.Curve, [], "note.handed.why.curve"),
        new(HandOverWhy.Resume, [], "note.handed.why.resume"),
        new(HandOverWhy.CarryOn, [], "note.handed.why.carry-on"),
        new(HandOverWhy.Retry, [], "note.handed.why.retry"),
        new(HandOverWhy.NotOffered, [], "note.handed.why.not-offered"),
        new(HandOverWhy.Transient, [], "note.handed.why.transient"),
        new(HandOverWhy.Failed, [], "note.handed.why.failed"),
        new(HandOverWhy.Quiet, [], "note.handed.why.quiet"),
        new(HandOverWhy.Budget, [], "note.handed.why.budget"),
    ]);

    /// <summary>Why no helper answered a question (<see cref="HelperWhy"/>): the page's <c>note.helper.why.*</c>.</summary>
    public static readonly NoteReasons Helper = new(
    [
        new(HelperWhy.Off, [], "note.helper.why.off"),
        new(HelperWhy.AskerGone, [], "note.helper.why.asker-gone"),
        new(HelperWhy.AskerUnfit, [], "note.helper.why.asker-unfit"),
        new(HelperWhy.TooMany, [], "note.helper.why.too-many"),
        new(HelperWhy.Unreadable, [], "note.helper.why.unreadable"),
        new(HelperWhy.NotStarted, [], "note.helper.why.not-started"),
        new(HelperWhy.NoAnswer, [], "note.helper.why.no-answer"),
        new(HelperWhy.Conversation, [], "note.helper.why.conversation"),
        new(HelperWhy.CannotTake, [], "note.helper.why.cannot-take"),
        new(HelperWhy.Declined, [], "note.helper.why.declined"),
        new(HelperWhy.CaptureMerge, [], "note.helper.why.capture-merge"),
        new(HelperWhy.CaptureLink, [], "note.helper.why.capture-link"),
        new(HelperWhy.CaptureSubmodule, [], "note.helper.why.capture-submodule"),
        new(HelperWhy.CaptureUnreadable, [], "note.helper.why.capture-unreadable"),
        new(HelperWhy.CaptureBound, [], "note.helper.why.capture-bound"),
        new(HelperWhy.CaptureMoved, [], "note.helper.why.capture-moved"),
        new(HelperWhy.HandUncertain, [], "note.helper.why.hand-uncertain"),
        new(HelperWhy.Limited, [], "note.helper.why.limited"),
        new(HelperWhy.NotSetUp, [], "note.helper.why.not-set-up"),
        new(HelperWhy.NoneSetUp, [], "note.helper.why.none-set-up"),
    ]);

    /// <summary>Why a piece was refused, held or uncertain (<see cref="PartnerWhy"/>): the page's <c>note.partner.why.*</c>.</summary>
    public static readonly NoteReasons Partner = new(
    [
        new(PartnerWhy.AskerUnfit, [], "note.partner.why.asker-unfit"),
        new(PartnerWhy.AskerGone, [], "note.partner.why.asker-gone"),
        new(PartnerWhy.Blank, [], "note.partner.why.blank"),
        new(PartnerWhy.PathOutside, [], "note.partner.why.path-outside"),
        new(PartnerWhy.NoCommit, [], "note.partner.why.no-commit"),
        new(PartnerWhy.BaseMissing, [], "note.partner.why.base-missing"),
        new(PartnerWhy.TooMany, [], "note.partner.why.too-many"),
        new(PartnerWhy.NotListed, [], "note.partner.why.not-listed"),
        new(PartnerWhy.NoConnector, [], "note.partner.why.no-connector"),
        new(PartnerWhy.NotMeasured, [], "note.partner.why.not-measured"),
        new(PartnerWhy.CopyFailed, [], "note.partner.why.copy-failed"),
        new(PartnerWhy.NoJob, [], "note.partner.why.no-job"),
        new(PartnerWhy.StateUnreadable, [], "note.partner.why.state-unreadable"),
        new(PartnerWhy.JobUnknown, [], "note.partner.why.job-unknown"),
        new(PartnerWhy.RefMoved, [], "note.partner.why.ref-moved"),
        new(PartnerWhy.ImportRefused, [], "note.partner.why.import-refused"),
        new(PartnerWhy.NoWork, [], "note.partner.why.no-work"),
        new(PartnerWhy.HandCut, [], "note.partner.why.hand-cut"),
    ]);

    /// <summary>Why an agent's ask to hand its session over came to nothing (<see cref="HandOverAskWhy"/>): the page's <c>note.handover.why.*</c>.</summary>
    public static readonly NoteReasons HandOver = new(
    [
        new(HandOverAskWhy.Off, [], "note.handover.why.off"),
        new(HandOverAskWhy.Declined, [], "note.handover.why.declined"),
        new(HandOverAskWhy.JustHanded, [], "note.handover.why.just-handed"),
        new(HandOverAskWhy.NoAgent, [], "note.handover.why.no-agent"),
        new(HandOverAskWhy.AskerGone, [], "note.handover.why.asker-gone"),
        new(HandOverAskWhy.AskerUnfit, [], "note.handover.why.asker-unfit"),
        new(HandOverAskWhy.Cannot, [], "note.handover.why.cannot"),
        new(HandOverAskWhy.QuestClosed, [], "note.handover.why.quest-closed"),
        new(HandOverAskWhy.QuestElsewhere, [], "note.handover.why.quest-elsewhere"),
        new(HandOverAskWhy.QuestReopened, [], "note.handover.why.quest-reopened"),
        new(HandOverAskWhy.NotLastRun, [], "note.handover.why.not-last-run"),
        new(HandOverAskWhy.UnknownAgent, [], "note.handover.why.unknown-agent"),
        new(HandOverAskWhy.SameAgent, [], "note.handover.why.same-agent"),
        new(HandOverAskWhy.NoDoor, [], "note.handover.why.no-door"),
        new(HandOverAskWhy.Running, [], "note.handover.why.running"),
        new(HandOverAskWhy.TreeGone, [], "note.handover.why.tree-gone"),
        new(HandOverAskWhy.Waiting, [], "note.handover.why.waiting"),
    ]);

    // ——— An agent's ask to hand its session over: on its session, waiting for the person's word with the agent it would go on with,
    // going with that agent at its turn's end, or refused and why. Facts only: never an account's name or a path.
    public static readonly NoteCode HandOverAsked = new("handover.asked", ["agent"]);
    public static readonly NoteCode HandOverTaking = new("handover.taking", ["agent"]);
    public static readonly NoteCode HandOverRefused = new("handover.refused", ["why"]) { Why = HandOver };

    // The person's own ask names the path that made it, and a hand-over let go that Daoris could not take is said as not taken, with
    // its reason and what the session does next, never as refused.
    public static readonly NoteCode HandOverAskedTerminal = new("handover.asked-terminal", ["agent"]);
    public static readonly NoteCode HandOverAskedWindow = new("handover.asked-window", ["agent"]);
    public static readonly NoteCode HandOverNotTaken = new("handover.not-taken", ["agent", "why"]) { Why = HandOver };
    public static readonly NoteCode HandOverNotTakenEnds = new("handover.not-taken-ends", ["agent", "why"]) { Why = HandOver };

    // ——— A helper: on the asker, a helper asked, answered or refused, with the agent and why; on the helper, whose session it helps.
    // Facts only: never an account's name or a path.
    public static readonly NoteCode HelperAsked = new("helper.asked", ["agent", "session"]);
    public static readonly NoteCode HelperAnswered = new("helper.answered", ["agent", "session"]);
    public static readonly NoteCode HelperAnsweredLastWords = new("helper.answered-last-words", ["agent", "session"]);
    public static readonly NoteCode HelperRefused = new("helper.refused", ["why"]) { Why = Helper };
    public static readonly NoteCode HelperRefusedBy = new("helper.refused-by", ["agent", "why"]) { Why = Helper };
    public static readonly NoteCode HelperHelping = new("helper.helping", ["session"]);
    // On the asker, a helper an account's limit cut off and that it no longer waits on, with when that account is ready again, or
    // with none where no cool-off this machine holds names it.
    public static readonly NoteCode HelperLimited = new("helper.limited", ["agent", "until"]);
    public static readonly NoteCode HelperLimitedNoReset = new("helper.limited-no-reset", ["agent"]);

    // ——— A partner's piece: on the lead, a piece asked of an agent, started, answered, refused, ended by a stop, its minutes or a limit
    // with its work kept, uncertain, its files beyond its paths and its work held; on the partner, whose session it works for. Notes,
    // never words: none reaches a prompt or resets the answers-alone count. Facts only: never an account's name or a path.
    public static readonly NoteCode PartnerAsked = new("partner.asked", ["agent"]);
    public static readonly NoteCode PartnerStarted = new("partner.started", ["agent", "session"]);
    public static readonly NoteCode PartnerAnswered = new("partner.answered", ["agent", "session"]);
    public static readonly NoteCode PartnerRefused = new("partner.refused", ["why"]) { Why = Partner };
    public static readonly NoteCode PartnerStopped = new("partner.stopped", ["agent"]);
    public static readonly NoteCode PartnerOutOfTime = new("partner.out-of-time", ["agent"]);
    public static readonly NoteCode PartnerLimited = new("partner.limited", ["agent"]);
    public static readonly NoteCode PartnerUncertain = new("partner.uncertain", ["why"]) { Why = Partner };
    public static readonly NoteCode PartnerBeyond = new("partner.beyond", ["count"]);
    public static readonly NoteCode PartnerKept = new("partner.kept", ["why"]) { Why = Partner };
    public static readonly NoteCode PartnerWorking = new("partner.working", ["session"]);

    // ——— The answers-alone bound: a third answer in a row with nobody between held for the person, by the agent that answered; their
    // *Go on with this answer*, which counts as their word; their *Not now*, which keeps the answer for them.
    public static readonly NoteCode AnswersHeld = new("answers.held", ["agent"]);
    public static readonly NoteCode AnswersPassed = new("answers.passed", []);
    public static readonly NoteCode AnswersKept = new("answers.kept", []);

    // ——— A conversation handed to another agent: the old one's end says which agent and conversation it went to, the new one's first
    // line where it came from and why. Facts only: never an account's name or a path.
    public static readonly NoteCode ChatHandedOver = new("chat.handed-over", ["agent", "session"]);
    public static readonly NoteCode ChatTakenOver = new("chat.taken-over", ["agent", "session", "why"]) { Why = Handed };
    public static readonly NoteCode ChatBriefingUnsaid = new("chat.briefing-unsaid", []);

    // ——— A session's move to a new part: the part it left says where it went on, the new part where it came from, each the agent and
    // account, why, the context the part stood at (`tokens`) and the brief's characters (`count`). The account by its id, as
    // `chat.moved` names them: a session's events stay on this machine.
    public static readonly NoteCode MovedOn = new("moved.on", ["session", "agent", "account", "why", "tokens", "count"]) { Why = Handed };
    public static readonly NoteCode MovedFrom = new("moved.from", ["session", "agent", "account", "why", "tokens", "count"]) { Why = Handed };

    // ——— A conversation moved to another account of its agent by the usage curve: the old one's last line says where it went and how
    // far over its curve its account was, the new one's first where it came from. They name the accounts, since a conversation's
    // record stays on this machine.
    public static readonly NoteCode ChatMoved = new("chat.moved", ["to", "from", "window", "used", "curve"]);
    public static readonly NoteCode ChatMovedFrom = new("chat.moved-from", ["from", "window", "used", "curve"]);

    // ——— A conversation whose turn its account's limit refused, whose move to the next account did not open: it ended, and the words
    // its refused turns held stay in it. The start's own words follow in the English alone.
    public static readonly NoteCode ChatLimitNotMoved = new("chat.limit-not-moved", []);

    // ——— A conversation its account's limit cut off with no other account to go on on: it waits, its words with it, and goes on by
    // itself when an account is ready; the person's *Stop waiting* ends the wait. Accounts by their ids, since the record stays on
    // this machine.
    public static readonly NoteCode ChatLimitWaits = new("chat.limit-waits", ["owner", "until", "why"]) { Why = Cooling };
    public static readonly NoteCode ChatLimitStoppedWaiting = new("chat.limit-stopped-waiting", []);

    // ——— Why a conversation whose agent has no account left does not go on with its actor's next choice on another agent, said before
    // its wait: it runs on the agent the person picked, or no choice on another agent can run now. The walk's own words follow in
    // the English alone.
    public static readonly NoteCode ChatLimitPicked = new("chat.limit-picked", ["owner"]);
    public static readonly NoteCode ChatLimitNoOther = new("chat.limit-no-other", ["owner"]);

    // ——— The limit's wake: its own account ready again at the margin-added `until`, it went on in its own conversation; or its own
    // conversation could not be resumed, by the refusal's code, and it went on briefed.
    public static readonly NoteCode LimitResumed = new("limit.resumed", ["until"]);
    public static readonly NoteCode LimitResumeRefused = new("limit.resume-refused", ["why"]) { Why = Continue };

    // ——— Where the person's words went, after the record's earlier parts.
    public static readonly NoteCode WentCannot = new("went.cannot", ["why"], Key: "work.say.cannot") { Why = Continue };
    public static readonly NoteCode WentNewSession = new("went.new-session", ["why"]) { Why = Continue };
    public static readonly NoteCode WentCarriedOn = new("went.carried-on", ["why"]) { Why = Continue };

    // ——— An intake.
    public static readonly NoteCode IntakeAskDeleted = new("intake.ask-deleted", []);
    public static readonly NoteCode IntakePublished = new("intake.published", ["quests", "ask"]);
    public static readonly NoteCode IntakeAskClosed = new("intake.ask-closed", ["ask"]);
    public static readonly NoteCode IntakeAskAnswered = new("intake.ask-answered", ["ask", "quests"]);
    public static readonly NoteCode IntakeTurnFailed = new("intake.turn-failed", ["ask"]);
    public static readonly NoteCode IntakeAsks = new("intake.asks", ["ask"]);
    public static readonly NoteCode IntakeExit = new("intake.exit", ["ask", "exit"]);
    public static readonly NoteCode IntakePersonAnswered = new("intake.person-answered", ["ask", "quests"]);
    public static readonly NoteCode IntakePersonClosed = new("intake.person-closed", ["ask"]);
    public static readonly NoteCode IntakePersonDeleted = new("intake.person-deleted", ["ask"]);

    // ——— A stop.
    public static readonly NoteCode StoppedPausedAsk = new("stopped.paused-ask", ["ask"]);
    public static readonly NoteCode StoppedPausedQuest = new("stopped.paused-quest", ["quest"]);
    public static readonly NoteCode StoppedAbandonedAsk = new("stopped.abandoned-ask", ["ask"]);
    public static readonly NoteCode StoppedAbandonedQuest = new("stopped.abandoned-quest", ["quest"]);
    public static readonly NoteCode StoppedCheckpointFinished = new("stopped.checkpoint-finished", []);
    public static readonly NoteCode StoppedCheckpointStopped = new("stopped.checkpoint-stopped", []);
    // The steward's finish and stop of a parked session, siblings of the person's two above.
    public static readonly NoteCode StoppedCheckpointFinishedSteward = new("stopped.checkpoint-finished-steward", []);
    public static readonly NoteCode StoppedCheckpointStoppedSteward = new("stopped.checkpoint-stopped-steward", []);
    public static readonly NoteCode StoppedCheckpointMoved = new("stopped.checkpoint-moved", []);
    public static readonly NoteCode StoppedOrphan = new("stopped.orphan", []);
    // A stop or a finish run under the standing confirmation, after the move's own note.
    public static readonly NoteCode StandingTerminal = new("standing.terminal", []);
    public static readonly NoteCode ChatClosed = new("chat.closed", []);
    public static readonly NoteCode ChatEndedByPerson = new("chat.ended-by-person", []);
    public static readonly NoteCode ChatEnded = new("chat.ended", []);
    public static readonly NoteCode ChatCancelled = new("chat.cancelled", []);
    public static readonly NoteCode ChatNotKept = new("chat.not-kept", []);
    public static readonly NoteCode ChatRestarted = new("chat.restarted", []);
    public static readonly NoteCode UpdateClosed = new("update.closed", ["build"]);

    // ——— Ask Daoris's start, where the machine's reading for its room did not answer within its bound: started on the room as last
    // written, said in its conversation; or, with no room written before, ended saying so.
    public static readonly NoteCode HelpRoomAsWritten = new("help.room-as-written", ["seconds"]);
    public static readonly NoteCode HelpRoomUnread = new("help.room-unread", ["seconds"]);

    // ——— Ask Daoris's start on the facts the judges keep: started at once, on a room that says what is not read yet, or on the room
    // as last written where no fact was ever judged.
    public static readonly NoteCode HelpRoomNotReadYet = new("help.room-not-read-yet", []);

    // ——— A landing at the quest's done, under a rule that accepts automatically: said in the conversation's record.
    public static readonly NoteCode LandingAccepted = new("landing.accepted", ["branch"]);
    public static readonly NoteCode LandingUnready = new("landing.plugin-unready", ["branch", "plugin"]);
    public static readonly NoteCode LandingPluginFailed = new("landing.plugin-failed", ["branch", "plugin"]);
    // Its push held back after the branch was made, handed on at the look after the release.
    public static readonly NoteCode LandingPushHeld = new("landing.push-held", ["branch", "plugin"]);
    // Held back there by its landing's gate: a code of its own so it never leads with its quest; a note written before it keeps `landing.push-held`.
    public static readonly NoteCode LandingPushGated = new("landing.push-gated", ["branch", "plugin"]);
    public static readonly NoteCode LandingNothing = new("landing.nothing", []);
    public static readonly NoteCode LandingHeld = new("landing.held", []);
    public static readonly NoteCode LandingUncommitted = new("landing.uncommitted", ["paths"]);
    public static readonly NoteCode LandingExists = new("landing.exists", ["branch"]);
    public static readonly NoteCode LandingRefused = new("landing.refused", []);
    // Replaying its work onto the line conflicts, and its session was told to rebase in its own tree; a refusal it could not tell keeps
    // `landing.refused`.
    public static readonly NoteCode LandingRebase = new("landing.rebase", []);
    // The person's Accept on a session whose tree holds uncommitted work or whose replay conflicts: the session was carried on in
    // Daoris's words, and the yes stays on it until its tree is clean. Its own codes, since the work is accepted, not refused.
    public static readonly NoteCode LandingCarriedDirty = new("landing.carried-dirty", ["paths"]);
    public static readonly NoteCode LandingCarriedConflict = new("landing.carried-conflict", []);
    // Carried on twice for one Accept and still not clean: it waits in Review's ready group, the yes taken back.
    public static readonly NoteCode LandingCarryBound = new("landing.carry-bound", []);
    // The work an Accept carried on landed once its tree was clean: the person's press, not the rule's switch, accepted it.
    public static readonly NoteCode LandingAcceptedCarried = new("landing.accepted-carried", ["branch"]);
    // Held by the review's gate, so a look's wait is never said as a refused landing; a note written before it keeps `landing.refused`.
    public static readonly NoteCode LandingUnreviewed = new("landing.unreviewed", []);
    // Held by the second opinion's gate; a note written before it keeps `landing.refused`.
    public static readonly NoteCode LandingOpinion = new("landing.opinion", []);
    public static readonly NoteCode LandingNoTree = new("landing.no-tree", []);
    public static readonly NoteCode LandingNotDone = new("landing.not-done", []);
    // The access setup says `landing: rule` and the rule merges (`access` is where the setup was set: repository, workspace or
    // machine); a merge still waits for Accept.
    public static readonly NoteCode LandingMerges = new("landing.merges", ["access"]);

    // ——— A conversation's first line, where a start's account was chosen: said in the conversation's record, which never leaves the
    // machine, so its values name accounts and a workspace. The line, then the rest's step and a refused turn riding inside it, then
    // what each account said, a line per account and one per thing it said.
    public static readonly NoteCode OpeningOpened = new("opening.opened", ["account", "why"]) { Why = Opening };
    public static readonly NoteCode OpeningCarriedOn = new("opening.carried-on", ["session", "account", "why"]) { Why = Opening };
    public static readonly NoteCode OpeningRest = new("opening.rest", ["why"]) { Why = Opening };
    public static readonly NoteCode OpeningTurnRefused = new("opening.turn-refused", ["turn"]);
    public static readonly NoteCode OpeningTurnContext = new("opening.turn-context", ["turn", "tokens"]);
    public static readonly NoteCode OpeningUnsaid = new("opening.unsaid", []);
    public static readonly NoteCode OpeningSaidAt = new("opening.said-at", ["account", "seen"]);
    public static readonly NoteCode OpeningSaidNothing = new("opening.said-nothing", ["account"]);
    public static readonly NoteCode OpeningSaidReached = new("opening.said-reached", ["window"]);
    // The agent's own pace warning, never near: `opening.said-warned`; a note written before keeps `opening.said-near`, which nothing writes now.
    public static readonly NoteCode OpeningSaidNear = new("opening.said-near", ["window"]);
    public static readonly NoteCode OpeningSaidWarned = new("opening.said-warned", ["window"]);
    public static readonly NoteCode OpeningSaidCredits = new("opening.said-credits", []);
    public static readonly NoteCode OpeningSaidUsed = new("opening.said-used", ["used", "window"]);
    // Over its curve on a window, and the curve's value there now: `opening.said-over`; a note written before keeps
    // `opening.said-near-at`, the scope's one number, which nothing writes now.
    public static readonly NoteCode OpeningSaidNearAt = new("opening.said-near-at", ["near"]);
    public static readonly NoteCode OpeningSaidOver = new("opening.said-over", ["window", "curve"]);
    // A window's use projected to now from its reading and Daoris's own turns since, by the account's measured ratio: what decided the
    // start asked nothing of it, never near or over, which a reading alone says.
    public static readonly NoteCode OpeningSaidProjected = new("opening.said-projected", ["projected", "window"]);
    public static readonly NoteCode OpeningSaidClear = new("opening.said-clear", []);

    // Beside the opening line, the actor line where a later choice ran, in the local conversation's record only, never the record's
    // note: the actor by its id, the choice's number, the entry that ran and each one passed.
    public static readonly NoteCode OpeningActor = new("opening.actor", ["actor", "n", "entry", "passed"]);

    /// <summary>A role's policy chose the choice that ran, by weight, share, budget or work; the sentence is the judge's, in English.</summary>
    public static readonly NoteCode OpeningChosen = new("opening.chosen", ["actor", "said"]);

    /// <summary>A part that began on another choice of its role than the one before, moved by the role's policy; <c>from</c> says the choice it left, <c>said</c> the condition and the count, in English.</summary>
    public static readonly NoteCode OpeningMovedChoice = new("opening.moved-choice", ["from", "said"]);

    // A quest marked design ran on the order of the role taking `design`, Build's while none does; the role by its name as written,
    // since an added role has no id the page knows.
    public static readonly NoteCode OpeningDesign = new("opening.design", ["role"]);

    // A start refused for its account's sign-in went on under another, which says the account it moved off.
    public static readonly NoteCode OpeningMovedOff = new("opening.moved-off", ["account"]);

    // A start whose model its agent refused (or answered with another) went on under the walk's next answer, which says the agent and
    // the model; the account is never named, since it was not at fault.
    public static readonly NoteCode OpeningMovedOffModel = new("opening.moved-off-model", ["agent", "model"]);

    // ——— A chat run's binding, said once in the conversation that grew its tree: the named workflow and the repository or workspace
    // that chose it, the one bound with why it cannot land the work, or a bind the home could not keep. Under Current nothing is said.
    public static readonly NoteCode ChatWorkflow = new("chat.workflow", ["workflow", "version", "owner"]);
    public static readonly NoteCode ChatWorkflowUnread = new("chat.workflow-unread", ["workflow", "owner", "problem"]);
    public static readonly NoteCode ChatWorkflowFailed = new("chat.workflow-failed", ["problem"]);

    // ——— A resumed conversation's first line (<see cref="Continuations.Opening"/>): the adapter it resumed on is a fact, and a version
    // that moved is a part riding inside it.
    public static readonly NoteCode ResumedOpening = new("resumed.opening", ["adapter", "why"]) { Why = Resumed };
    public static readonly NoteCode ResumedMoved = new("resumed.moved", ["now", "then"]);
    public static readonly NoteCode ResumedNative = new("resumed.native", ["adapter"]);
    // The native door going on with Daoris's own words alone: never "what you said".
    public static readonly NoteCode ResumedNativeDaoris = new("resumed.native-daoris", ["adapter"]);

    // ——— A permission the rules refused: the lead and the closing sentence are Daoris's facts; the call's title between them is the
    // agent's words, which may hold a path.
    public static readonly NoteCode RefusedLead = new("refused.lead", []);
    public static readonly NoteCode RefusedGoverns = new("refused.governs", []);

    // ——— A permission held for the person under a repository's asking permission prompts: the lead is Daoris's and the call's title
    // after it the agent's words; a refusal leads with `refused.lead`, and its close says who or what refused it: the person, the
    // turn's cancel, the session's end, or the bound, whose minutes are a fact.
    public static readonly NoteCode AskingHeld = new("asking.held", []);
    public static readonly NoteCode AskingAllowed = new("asking.allowed", []);
    public static readonly NoteCode AskingDenied = new("asking.denied", []);
    public static readonly NoteCode AskingCancelled = new("asking.cancelled", []);
    public static readonly NoteCode AskingEnded = new("asking.ended", []);
    public static readonly NoteCode AskingBound = new("asking.bound", ["minutes"]);

    // ——— A press's note and a chat's settings: the lead is Daoris's, what follows a landing's or a preface's lead is a program's words
    // (git's, a plugin's, Daoris's one-copy English to the agent). The option and its value are the agent's own names, never a path.
    public static readonly NoteCode LandingPersonAccepted = new("landing.person-accepted", []);
    public static readonly NoteCode LandingPersonHanded = new("landing.person-handed", []);
    // A part the person left out of its entry's Accept: the branch its line of work landed on, or the line it merged into. Its review
    // reads it back (<c>LandingRules.LeftOutOf</c>) and leads with Discard.
    public static readonly NoteCode LandingLeftOut = new("landing.left-out", ["branch"]);
    // A member whose work a combined Accept brought into its head's landing: the branch it went onto, or the line it merged into, and
    // the operation. The conversation's own landing then reads it as brought in, never landed.
    public static readonly NoteCode LandingBroughtInto = new("landing.brought-into", ["branch", "operation"]);
    public static readonly NoteCode LandingBroughtIntoLine = new("landing.brought-into-line", ["branch", "operation"]);
    public static readonly NoteCode ChatOptionSet = new("chat.option-set", ["option", "chosen"]);
    public static readonly NoteCode ChatToldWhere = new("chat.told-where", []);

    // ——— Words that did not reach an agent, or were not taken (<see cref="WordsNotes"/>): the lead is Daoris's; what follows a refusal
    // is the agent's words, and what follows a failed take is a program's or the service's (it may hold a path). The count and why a
    // send never reached are facts, `why` one of `Unreached`'s.
    public static readonly NoteCode WordsRefused = new("words.refused", []);
    public static readonly NoteCode WordsUnreached = new("words.unreached", ["count", "why"]) { Why = Unreached };
    public static readonly NoteCode WordsNotTaken = new("words.not-taken", []);

    // The chat runner's and the modules' sites: words a conversation never got, a send that found the agent gone, a start's words not
    // taken off their session (the session id a fact), and held words the record would not keep.
    public static readonly NoteCode WordsNotReached = new("words.not-reached", []);
    public static readonly NoteCode WordsNotSent = new("words.not-sent", []);
    public static readonly NoteCode WordsStartNotTaken = new("words.start-not-taken", ["session"]);
    public static readonly NoteCode WordsNotKept = new("words.not-kept", []);

    // ——— A server that drives Daoris's own browser, withheld: the servers are plugin-declared names; why it was not handed is a fact
    // of the host's, or an exception's words riding after the lead.
    public static readonly NoteCode BrowserWithheld = new("browser.withheld", ["servers"]);
    public static readonly NoteCode BrowserNoHost = new("browser.no-host", []);
    public static readonly NoteCode BrowserNoneUp = new("browser.none-up", []);
    // The line a chat's agent was told of the browser it was handed: the lead by its code, the line Daoris's one-copy English.
    public static readonly NoteCode ChatToldBrowser = new("chat.told-browser", []);
    // The repository's process a chat in a tree of its own was told at its start: its commit rule's mode and where it was set are
    // facts, the words after the lead Daoris's one-copy English.
    public static readonly NoteCode ChatToldProcess = new("chat.told-process", ["mode", "source"]);
    // The standing block a conversation's agent was told with its first message: the lead by its code, the block after it Daoris's
    // one-copy English.
    public static readonly NoteCode ChatToldBlock = new("chat.told-block", []);
    // The workflow draft open in the editor a chat's agent was told beside the person's words: the lead by its code, then what the
    // agent was handed, the draft's JSON last and whole, which the draft door finds it by again.
    public static readonly NoteCode ChatToldDraft = new("chat.told-draft", []);

    // ——— A session's own background work (<see cref="BackgroundNotes"/>): the work's names are the agent's own descriptions of it,
    // said as code; a state is the harness's own word for how it ended, said as it came. Why a wait could not be read is a program's
    // words, which may hold a path.
    public static readonly NoteCode BackgroundUnread = new("background.unread", []);
    public static readonly NoteCode BackgroundWaits = new("background.waits", ["works"]);
    public static readonly NoteCode BackgroundWorkEnded = new("background.work-ended", ["work", "state"]);
    public static readonly NoteCode BackgroundGoesOn = new("background.goes-on", []);
    // The quest, read again while the session waits, no longer waits on the work, so the session ends and the work still running ends
    // with it.
    public static readonly NoteCode BackgroundWaitOver = new("background.wait-over", ["works"]);
    // The quest no longer waits, and the session ends rather than wait for what its agent scheduled for itself: a turn its harness
    // started that said nothing, or the turn its harness was about to wake.
    public static readonly NoteCode BackgroundTurnLeft = new("background.turn-left", []);
    public static readonly NoteCode BackgroundQuestClosed = new("background.quest-closed", []);
    // A turn that ended with its quest done and its tree uncommitted is carried on in Daoris's words, and the bound says when the
    // session ends; the person's Accept is no part of either (`landing.carried-dirty` is that one's).
    public static readonly NoteCode BackgroundCarriedOn = new("background.carried-on", ["paths"]);
    public static readonly NoteCode BackgroundCarryBound = new("background.carry-bound", []);

    // ——— A conversation's door that failed (<see cref="DoorNotes"/>): the protocol session that could not open or failed, and a turn
    // that could not be taken, each a lead with a program's message after it (it may hold a path). A resume the agent refused is
    // Daoris's own sentence, so `why` is the `Continue` family's, then the agent's words where it said any; the door adds that the
    // answer is carried on in a new session.
    public static readonly NoteCode DoorNotOpen = new("door.not-open", []);
    public static readonly NoteCode DoorTurnNotTaken = new("door.turn-not-taken", []);
    public static readonly NoteCode DoorFailed = new("door.failed", []);
    public static readonly NoteCode DoorResumeRefused = new("door.resume-refused", ["why"]) { Why = Continue };
    public static readonly NoteCode DoorAnswerCarriedOn = new("door.answer-carried-on", []);

    // ——— A review set-up's record (<see cref="ReviewNotes"/>): the environment's name and the repository a fact; the commit is the
    // 8-character short id. The service's message and the served sentence are a program's words, which may hold a path.
    public static readonly NoteCode ReviewNoRule = new("review.no-rule", ["named", "to"]);
    public static readonly NoteCode ReviewNoCommit = new("review.no-commit", []);
    public static readonly NoteCode ReviewPosted = new("review.posted", ["commit", "named"]);
    public static readonly NoteCode ReviewNotPosted = new("review.not-posted", []);
    public static readonly NoteCode ReviewPostsNext = new("review.posts-next", []);

    // ——— A second opinion's answers as the working session's turn ended (<see cref="OpinionAnswers.Told"/>): whose findings and how
    // many, then how many each way, each only above nought; a count the page says "(N in all)".
    public static readonly NoteCode OpinionAnswersLead = new("opinion.answers", ["who", "count"]);
    public static readonly NoteCode OpinionFixed = new("opinion.fixed", ["count"]);
    public static readonly NoteCode OpinionUnproven = new("opinion.unproven", ["count"]);
    public static readonly NoteCode OpinionRejected = new("opinion.rejected", ["count"]);
    public static readonly NoteCode OpinionUnresolved = new("opinion.unresolved", ["count"]);
    public static readonly NoteCode OpinionUnanswered = new("opinion.unanswered", ["count"]);
    public static readonly NoteCode OpinionDisputed = new("opinion.disputed", ["count"]);

    // ——— How the reviewer's own session ended (<see cref="OpinionAnswers.Concluded"/>): its turn failed (the failure follows as the
    // program's words), it read the work and ended, or it exited otherwise. The opinion's id is a short id.
    public static readonly NoteCode OpinionReviewerFailed = new("opinion.reviewer-failed", ["opinion"]);
    public static readonly NoteCode OpinionReviewerRead = new("opinion.reviewer-read", ["opinion"]);
    public static readonly NoteCode OpinionReviewerExited = new("opinion.reviewer-exited", ["opinion", "exit"]);

    // ——— A conversation's words waiting for its own account's reset (<see cref="ResumeWords"/>): the account by its owner and its id,
    // never by the name the person gave it, since a conversation's events never leave the machine; until when a moment, why a
    // cool-off's reason. The tool's own sign-in has a line of its own and its refresh another. Each door out of the wait is a line, the
    // session its command names a value; the command itself is the catalogue's.
    public static readonly NoteCode WaitsAccount = new("waits.account", ["owner", "account", "until", "why"]) { Why = Cooling };
    public static readonly NoteCode WaitsOwn = new("waits.own", ["owner", "until", "why"]) { Why = Cooling };
    public static readonly NoteCode WaitsReadAgain = new("waits.read-again", []);
    public static readonly NoteCode WaitsDoorNewSession = new("waits.door-new-session", ["session"]);
    public static readonly NoteCode WaitsDoorChat = new("waits.door-chat", ["session"]);
    public static readonly NoteCode WaitsDoorHelp = new("waits.door-help", []);

    // ——— A conversation's words held for another reason (<see cref="HeldWords"/>): a reason of Daoris's own sentence is one line, its
    // repository or conversation a value; a program's message (driver.json unread, the roster's refusal, the ledger's) rides as its
    // words between `lead` (or the reason `config-unread`) and `tail`, never a value, since it may hold a path.
    public static readonly NoteCode HeldNoCheckout = new("held.no-checkout", ["repository"]);
    public static readonly NoteCode HeldReplaying = new("held.replaying", ["repository"]);
    // What else holds the repository alone while a start waits for it; its branches brought up to date stay `replaying`.
    public static readonly NoteCode HeldTidying = new("held.tidying", ["repository"]);
    public static readonly NoteCode HeldPromoting = new("held.promoting", ["repository"]);
    public static readonly NoteCode HeldWriting = new("held.writing", ["repository"]);
    public static readonly NoteCode HeldAnswering = new("held.answering", ["session"]);
    public static readonly NoteCode HeldConfigUnread = new("held.config-unread", []);
    public static readonly NoteCode HeldLead = new("held.lead", []);
    public static readonly NoteCode HeldTail = new("held.tail", []);
    // The agent its words would go on with is no longer one this machine has; the hand-over is the way on.
    public static readonly NoteCode HeldAgentGone = new("held.agent-gone", ["agent", "session"]);

    // ——— The roster's refusal as a held note's reason (<see cref="HeldWords.Refused"/>): an account by its owner and its id, never the
    // name the person gave it (the page names it), until when a moment, why a cool-off's reason; the tool's own sign-in is a line of
    // its own, its refresh `waits.read-again`. `profile` is the id as a shell spells it in the sign-in command.
    public static readonly NoteCode HeldCoolingAccount = new("held.cooling-account", ["owner", "account", "until", "why"]) { Why = Cooling };
    public static readonly NoteCode HeldCoolingOwn = new("held.cooling-own", ["owner", "until", "why"]) { Why = Cooling };
    // A line a limit cut off, every account it may run on at its limit, goes on by itself once the earliest is ready.
    public static readonly NoteCode HeldLimitWaits = new("held.limit-waits", ["owner", "account", "until"]) { Why = Cooling };
    public static readonly NoteCode HeldLimitWaitsOwn = new("held.limit-waits-own", ["owner", "until"]) { Why = Cooling };
    // The same wait where some accounts need the person's sign-in, named apart from those cooling; `accounts` a list.
    public static readonly NoteCode HeldLimitWaitsSignIn = new("held.limit-waits-sign-in", ["owner", "account", "until", "accounts"]) { Why = Cooling };
    public static readonly NoteCode HeldSignedOut = new("held.signed-out", ["owner", "account", "profile"]);
    // Every account the start could run on was locked past its patience; the accounts by id, a list.
    public static readonly NoteCode HeldLocked = new("held.locked", ["owner", "accounts"]);
    // No Daoris account of the agent's is signed in, and its own home is never a start's.
    public static readonly NoteCode HeldNoAccount = new("held.no-account", ["owner"]);
    public static readonly NoteCode HeldNotInstalled = new("held.not-installed", ["agent"]);
    public static readonly NoteCode HeldPinNotInstalled = new("held.pin-not-installed", ["agent", "version"]);

    // ——— The roster's other refusals. A wait over a list: its lead, a clause for each account (by its id, the page names it), the
    // first reset, then the sign-in that would start it sooner, `logins` each command into an account, its id spelled for any shell. A
    // pick refused adds who is ready, `accounts` a list. An agent with its own tooling names that command as a program's words between
    // `not-installed-own` and `never-installs`, never a value, since it may hold a path. An account its provider refused, and the
    // clauses a wait adds (kept for conversations, and the accounts outside the list with the command that adds the first).
    public static readonly NoteCode HeldWaitEvery = new("held.wait-every", ["agent"]);
    public static readonly NoteCode HeldWaitSome = new("held.wait-some", ["agent"]);
    public static readonly NoteCode HeldClauseCooling = new("held.clause-cooling", ["account", "until"]);
    public static readonly NoteCode HeldClauseRefused = new("held.clause-refused", ["account"]);
    public static readonly NoteCode HeldClauseSignedOut = new("held.clause-signed-out", ["account"]);
    public static readonly NoteCode HeldClauseReady = new("held.clause-ready", ["account"]);
    public static readonly NoteCode HeldWaitFirst = new("held.wait-first", ["account", "until", "why"]) { Why = Cooling };
    public static readonly NoteCode HeldSignInSooner = new("held.sign-in-sooner", ["logins"]);
    public static readonly NoteCode HeldSignIn = new("held.sign-in", ["logins"]);
    public static readonly NoteCode HeldPickedReady = new("held.picked-ready", ["accounts"]);
    public static readonly NoteCode HeldPickedNone = new("held.picked-none", ["agent"]);
    public static readonly NoteCode HeldRefusedAccount = new("held.refused-account", ["owner", "account"]);
    public static readonly NoteCode HeldRefusedOwn = new("held.refused-own", ["owner"]);
    public static readonly NoteCode HeldSignInRefusedOwn = new("held.sign-in-refused-own", ["owner"]);
    public static readonly NoteCode HeldNotInstalledOwn = new("held.not-installed-own", ["agent"]);
    public static readonly NoteCode HeldNeverInstalls = new("held.never-installs", []);
    public static readonly NoteCode HeldKeptAside = new("held.kept-aside", ["account"]);
    public static readonly NoteCode HeldOutsideMachine = new("held.outside-machine", ["accounts", "first", "door"]);
    public static readonly NoteCode HeldOutsideWorkspace = new("held.outside-workspace", ["workspace", "accounts", "first", "door"]);

    /// <summary>Every code above, read off the declarations so none escapes the catalogue test.</summary>
    public static IReadOnlyList<NoteCode> All { get; } =
        [.. typeof(NoteCodes).GetFields().Where(field => field.FieldType == typeof(NoteCode)).Select(field => (NoteCode)field.GetValue(null)!)];

    /// <summary>
    /// A reason's values for a <c>why</c>: its code, then its own, read from what it says; <c>elsewhere</c>'s agent is
    /// the adapter the record ran on, which the page may say by its product's name.
    /// </summary>
    /// <param name="ranOn">The adapter the record ran on, or null where it names none.</param>
    public static (string Name, object? Value)[] Reason(IContinueReason why, string? ranOn = null)
    {
        var values = new List<(string Name, object? Value)> { ("why", why.Code) };
        values.AddRange(why.Values.Select(value => (value.Name, (object?)value.Value)));
        if (why.Code == ContinueCodes.Elsewhere && why.Values.All(value => value.Name != "agent")) values.Add(("agent", ranOn));
        return [.. values];
    }

    /// <summary>A moment as a value: ISO 8601 in UTC, to the second, which the page formats in the reader's language and zone.</summary>
    public static string Moment(DateTimeOffset at) =>
        at.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
}
