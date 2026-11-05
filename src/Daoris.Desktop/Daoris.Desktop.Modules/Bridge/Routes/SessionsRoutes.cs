using System.Text.Json;
using Daoris.Driver;
using Shenora.Core.Ipc;
using static Daoris.Desktop.DriverArguments;

namespace Daoris.Desktop;

/// <summary>
/// A session as the rail and its drawer act on it, the page's `bridge/sessions.ts`: a stop, the
/// person's answer to a parked session, and what sessions opened with and said, read off this
/// machine's own record.
/// </summary>
public sealed class SessionsRoutes(DriverContext context)
{
    private readonly DriverLoop _loop = context.Loop;
    private readonly OpenFolder? _openFolder = context.OpenFolder;
    private readonly SessionsAskDesk _asks = new(context);
    private readonly SessionsGroupsDesk _groups = new(context);
    private readonly SessionsWaitDesk _wait = new(context);
    private readonly SessionsRecordDesk _records = new(context);
    private readonly SessionsMoveDesk _moves = new(context);

    // What a session's own presses change: its record among the sessions, and the driver's state its loop,
    // nudged, reads again; a resolve moves its quest too.
    [DriverRoute("STOP_SESSION", PipeClass.Person, Drain = PipeDrain.Passes)]
    [Owner("STOP_SESSION", Session = "id")]
    [GroundSession("STOP_SESSION", "id")]
    [Says("STOP_SESSION", "id:session")]
    [Publishes("STOP_SESSION", StateScopes.Sessions | StateScopes.Driver, Session = "id")]
    private Task<object?> StopSessionAsync(StopSession press, CancellationToken cancellationToken) => _moves.StopAsync(press, cancellationToken);

    /// <summary>
    /// The person's answer to a parked session: `completed`, `declined` or `stopped`, with what the record should say. It goes
    /// through the driver because the process goes first: a record saying `completed` while this machine holds the process is the
    /// lie the observed lifecycle prevents. Whether the move is legal stays the ledger's; declining needs a reason; a move with no
    /// note still writes one, since the store keeps the previous note and would read beside the parked analysis.
    /// </summary>
    [DriverRoute("RESOLVE_SESSION", PipeClass.Person, Drain = PipeDrain.Passes)]
    [Owner("RESOLVE_SESSION", Session = "id")]
    [GroundSession("RESOLVE_SESSION", "id")]
    [Says("RESOLVE_SESSION", "id:session", "note:words")]
    [Publishes("RESOLVE_SESSION", StateScopes.Sessions | StateScopes.Quests | StateScopes.Driver, Session = "id")]
    private Task<object?> ResolveAsync(ResolveSession press, CancellationToken cancellationToken) => _moves.ResolveAsync(press, cancellationToken);

    // *Go on in a new session*: words a resume holds while the account their session ran on cools go on now
    // in a new session, handed them, without that conversation. The driver's door judges and keeps the person's choice
    //, the same door `daoris session go-on-new` calls; the loop is nudged so its next look carries
    // them on. What cannot is `sent: false` with its code, for the page to word, and the driver's sentence beside it. The
    // page's press is a later row.
    [DriverRoute("SESSION_GO_ON_NEW", PipeClass.Person, Drain = PipeDrain.Waits)]
    [Owner("SESSION_GO_ON_NEW", Session = "id")]
    [GroundSession("SESSION_GO_ON_NEW", "id")]
    [Says("SESSION_GO_ON_NEW", "id:session")]
    [Publishes("SESSION_GO_ON_NEW", StateScopes.Sessions | StateScopes.Driver, Session = "id")]
    private Task<object?> SessionGoOnNewAsync(SessionGoOnNew press, CancellationToken cancellationToken) => _moves.GoOnNewAsync(press, cancellationToken);

    // *Ask another agent…*: the person's own ask of a helper for a session,
    // the file `daoris session helper` writes (`HelperFiles.WriteAsk`, asked by the person, so the `helpers` rule's `ask`
    // and `off` do not hold it), judged as that verb judges: a question within the bound, an agent of one word, and a session of
    // this machine's that may ask. Everything else is the driver's, at its next look, which the nudge brings sooner. What cannot is
    // `asked: null` with its code for the page to word, and the verb's sentence beside it; the page never reads a path.
    [DriverRoute("SESSION_HELPER_ASK", PipeClass.Person, Drain = PipeDrain.Waits)]
    [Owner("SESSION_HELPER_ASK", Session = "id")]
    [GroundSession("SESSION_HELPER_ASK", "id")]
    [Says("SESSION_HELPER_ASK", "id:session", "agent:agent", "question:words")]
    [Publishes("SESSION_HELPER_ASK", StateScopes.Sessions | StateScopes.Driver, Session = "id")]
    private Task<object?> SessionHelperAskAsync(SessionHelperAsk press, CancellationToken cancellationToken) => _asks.HelperAskAsync(press, cancellationToken);

    // The asks a session made of helpers, oldest first, each as the driver's look left it: its state and code, the
    // helper's session once started and the agent it started on. Read off the home's `helpers` folder, the driver's own files;
    // what the state keeps for the driver alone (the last words, the word it was handed as) is not the page's.
    [DriverRoute("SESSION_HELPERS", PipeClass.Open)]
    private Task<object?> SessionHelpersAsync(SessionHelpers press, CancellationToken cancellationToken) => _asks.HelpersAsync(press, cancellationToken);

    // *Ask another agent to work on a piece…*: the person's own ask of
    // a piece for a session, the file `daoris session partner` writes, by the one judge and writer both share
    // (`PartnerPersonAsk`): the piece within its bound, an agent named in one word, the paths inside, a session of this machine's that
    // may lead one, and its tree's commit read as the piece's exact base. Everything else is the driver's, at its next look, which the
    // nudge brings sooner. What cannot is `asked: null` with its code for the page to word, and the verb's sentence beside it.
    [DriverRoute("SESSION_PARTNER_ASK", PipeClass.Person, Drain = PipeDrain.Waits)]
    [Owner("SESSION_PARTNER_ASK", Session = "id")]
    [GroundSession("SESSION_PARTNER_ASK", "id")]
    [Says("SESSION_PARTNER_ASK", "id:session", "agent:agent", "piece:words")]
    [Publishes("SESSION_PARTNER_ASK", StateScopes.Sessions | StateScopes.Driver, Session = "id")]
    private Task<object?> SessionPartnerAskAsync(SessionPartnerAsk press, CancellationToken cancellationToken) => _asks.PartnerAskAsync(press, cancellationToken);

    // The pieces a session asked, oldest first, each as the driver's look left
    // it: the piece and its paths, the agent, where it stands and why, its partner's record once started, its frozen commit and ref
    // once imported, and the files Daoris read in it with those beyond its paths. Read off the home's `partners` folder, the driver's
    // own files; the partner's own words stay its answer's, which reaches the session as a word.
    [DriverRoute("SESSION_PARTNERS", PipeClass.Open)]
    private Task<object?> SessionPartnersAsync(SessionPartners press, CancellationToken cancellationToken) => _asks.PartnersAsync(press, cancellationToken);

    // What of the agents' asks waits on the person's word across this machine's sessions, for
    // *What needs you*: each the driver's look holds `waiting` with no code under the `helpers` rule's `ask`, the one state the yes
    // and the no answer, oldest first, with the session that asked. A read off the home's `helpers` folder: it declares nothing,
    // and sits under the sessions' key on the page, which every tick asks again; the look says a new one in its report.
    [DriverRoute("HELPER_WAITS", PipeClass.Open)]
    private Task<object?> HelperWaitsAsync(NoPayload _, CancellationToken cancellationToken) => _asks.HelperWaitsAsync();

    // The person's yes to an agent's ask under the `helpers` rule's `ask`: the `approved`
    // state the driver's look reads (`HelperJudge`), written where the ask waits on that yes: no state yet, or `waiting` with no
    // code. A yes again is the same yes. A person's own ask needs none, one past its start is past it, one waiting on a slot or an
    // agent waits on no yes, and one this session does not hold is gone: each `approved: false` with its code, its state untouched.
    [DriverRoute("SESSION_HELPER_APPROVE", PipeClass.Person, Drain = PipeDrain.Waits)]
    [Owner("SESSION_HELPER_APPROVE", Session = "session")]
    [GroundRead("SESSION_HELPER_APPROVE", nameof(PipeReads.HelperWait))]
    [Says("SESSION_HELPER_APPROVE", "session:session")]
    [Publishes("SESSION_HELPER_APPROVE", StateScopes.Sessions | StateScopes.Driver, Session = "session")]
    private Task<object?> SessionHelperApproveAsync(SessionHelperApprove press, CancellationToken cancellationToken) => _asks.HelperApproveAsync(press, cancellationToken);

    // The person's no to an agent's ask under the `helpers` rule's `ask`, beside the yes: the ask
    // refused `declined` at once, with the coded note the look writes on the asker's record for every refusal (`helper.refused`), so
    // the look reads it as past judging and its timeline says it as it says the rule's `off`. Written only where the look holds the
    // ask waiting on the person's word, `waiting` with no code: one it has not read yet may be starting under `on`, so it waits on no
    // word yet. A no again is the same no, with no second note. A person's own ask needs none, one with the person's yes waits on the
    // look, one past its start is past it, one waiting on a slot or an agent waits on none, and one this session does not hold is
    // gone: each `declined: false` with its code, its state untouched.
    [DriverRoute("SESSION_HELPER_DECLINE", PipeClass.Person, Drain = PipeDrain.Passes)]
    [Owner("SESSION_HELPER_DECLINE", Session = "session")]
    [GroundRead("SESSION_HELPER_DECLINE", nameof(PipeReads.HelperWait))]
    [Says("SESSION_HELPER_DECLINE", "session:session")]
    [Publishes("SESSION_HELPER_DECLINE", StateScopes.Sessions | StateScopes.Driver, Session = "session")]
    private Task<object?> SessionHelperDeclineAsync(SessionHelperDecline press, CancellationToken cancellationToken) => _asks.HelperDeclineAsync(press, cancellationToken);

    // A session's agent's ask to hand it over, as the driver's look left it, for the session's page: its
    // note (the agent's claim), when, its state, code and the agent it goes to; null where its agent asked none. A read off the home's
    // sessions folder, the driver's own files: it declares nothing, and the look says a change in its report.
    [DriverRoute("SESSION_HAND_OVER_ASK", PipeClass.Open)]
    private Task<object?> SessionHandOverAskAsync(SessionHandOverAsk press, CancellationToken cancellationToken) => _asks.HandOverAskAsync(press);

    // A parked session's question to the person, as its agent asked it through `question_ask`
    // and the driver kept it for the park its last turn ended in: its words, the answers it offers with what it does on each, the one
    // it recommends with why and by when, and its mark that the question is the owner's alone, each the
    // agent's own; beside them the supervisor's defer on it (`left`: its bound and sourced reasons, SUPERVISOR1b), Daoris's record of
    // the holder's act; null where none is kept, and for a record that is not
    // parked (Fable's read, Q7), whatever a file says. A read off the home's sessions folder, the files leaving the window's thread
    // first: it declares nothing, and the page asks it only of a park of this machine's.
    [DriverRoute("SESSION_QUESTION", PipeClass.Open)]
    private Task<object?> SessionQuestionAsync(SessionQuestion press, CancellationToken cancellationToken) => _asks.QuestionAsync(press, cancellationToken);

    // The person's yes to their session's agent's ask to hand it over under the `handOver` rule's `ask`:
    // the judge the terminal's `sessions hand-over <id> --approve` shares (`HandOverAskWord`). A conversation this shell's chat runner
    // holds is handed over at once, through the runner's own hand-over (taken where it is not talking, else at its turn's end), the
    // state written `taken` first so no look keeps a second box; anything else is `approved` with the agent the look named, for the
    // look, nudged, to take as the rule's `on` would. `ask` names the one the page showed, else the session's own. A word that waits on
    // nothing is `approved: false` with its code, its state untouched.
    [DriverRoute("SESSION_HAND_OVER_APPROVE", PipeClass.Person, Drain = PipeDrain.Waits)]
    [Owner("SESSION_HAND_OVER_APPROVE", Session = "session")]
    [GroundRead("SESSION_HAND_OVER_APPROVE", nameof(PipeReads.HandOverAsk))]
    [Says("SESSION_HAND_OVER_APPROVE", "session:session")]
    [Publishes("SESSION_HAND_OVER_APPROVE", StateScopes.Sessions | StateScopes.Driver, Session = "session")]
    private Task<object?> SessionHandOverApproveAsync(SessionHandOverApprove press, CancellationToken cancellationToken) => _asks.HandOverApproveAsync(press, cancellationToken);

    // The person's no to their session's agent's ask to hand it over, beside the yes: refused `declined` at
    // once, with the coded note the look writes for every refusal (`handover.refused`), where the look holds it waiting on their word.
    // One not read yet, one with their yes, one past it and one gone are `declined: false` with its code, its state untouched.
    [DriverRoute("SESSION_HAND_OVER_DECLINE", PipeClass.Person, Drain = PipeDrain.Passes)]
    [Owner("SESSION_HAND_OVER_DECLINE", Session = "session")]
    [GroundRead("SESSION_HAND_OVER_DECLINE", nameof(PipeReads.HandOverAsk))]
    [Says("SESSION_HAND_OVER_DECLINE", "session:session")]
    [Publishes("SESSION_HAND_OVER_DECLINE", StateScopes.Sessions | StateScopes.Driver, Session = "session")]
    private Task<object?> SessionHandOverDeclineAsync(SessionHandOverDecline press, CancellationToken cancellationToken) => _asks.HandOverDeclineAsync(press, cancellationToken);

    // The person's *Leave it running* on a quiet session's row: the watcher stands down
    // for it until it is heard again, its episode ends `left` with its coded note, and the row goes. `{ left, why, message }`; a session
    // that brings no row is `left: false`, `why: not-quiet`. A move of the watcher's memory alone, which wakes the attention slice.
    [DriverRoute("SESSION_QUIET_LEAVE", PipeClass.Person, Drain = PipeDrain.Passes)]
    [Owner("SESSION_QUIET_LEAVE", Session = "id")]
    [GroundSession("SESSION_QUIET_LEAVE", "id")]
    [Says("SESSION_QUIET_LEAVE", "id:session")]
    [Publishes("SESSION_QUIET_LEAVE", StateScopes.Sessions, Session = "id")]
    private Task<object?> SessionQuietLeaveAsync(SessionQuietLeave press, CancellationToken cancellationToken) => _wait.QuietLeaveAsync(press);

    // The person's *End and carry on* on a quiet quest session's row: their
    // stop released in the same press, *Stop* and *Try again* at once. The release is written first, then the run ends as the person's
    // stop ends one, concluding `stopped` with `ended.quiet-pressed` and no strike; the look its ending wakes carries the line on in a new
    // part handed the brief, and the person's words that waited for it go to that part, never to a resume of the run that hung.
    // `{ stopped, why, message }`: refused `not-quiet` where it brings no quiet row (a busy one is offered no end), `not-quest` for an
    // intake, `gone` where its run ended first, each with nothing changed.
    [DriverRoute("SESSION_QUIET_CARRY_ON", PipeClass.Person, Drain = PipeDrain.Passes)]
    [Owner("SESSION_QUIET_CARRY_ON", Session = "id")]
    [GroundSession("SESSION_QUIET_CARRY_ON", "id")]
    [Says("SESSION_QUIET_CARRY_ON", "id:session")]
    [Publishes("SESSION_QUIET_CARRY_ON", StateScopes.Sessions | StateScopes.Driver, Session = "id")]
    private Task<object?> SessionQuietCarryOnAsync(SessionQuietCarryOn press, CancellationToken cancellationToken) => _wait.QuietCarryOnAsync(press);

    // What this machine's driven sessions hold for the person under their repository's asking permission prompts (AUTONOMY1b3a, D184
    // point 3), oldest first: each request with its session, its call as the agent named it, and what the person may choose (never a
    // standing allow), and when its bound releases it (`until`, PERMUNTIL1: the page's one source for the bound). A read off the
    // driver's memory (`HeldPermissions`), which a restart empties with the agents that asked; the attention slice's own projection,
    // so the pushed rows and the route say each hold alike.
    [DriverRoute("PERMISSION_WAITS", PipeClass.Open)]
    private Task<object?> PermissionWaitsAsync(NoPayload _, CancellationToken cancellationToken) => _wait.PermissionWaitsAsync();

    // The person's answer to a request a session holds for them: an option the agent offered, by its id, or none to
    // refuse it, which takes the offered one-time rejection, else none. The allow is the person's press for this call alone, and
    // Daoris never answers one by itself; a standing allow is a rule's to give. What cannot is `answered: false` with
    // its code (`unheld`, `unoffered`, `standing`) and the registry's sentence, the hold left as it was. Its session hears the answer
    // at once, off its own reader, and the hold's end is told through the registry's `Changed`; the route publishes nothing.
    [DriverRoute("SESSION_PERMISSION_ANSWER", PipeClass.Person, Drain = PipeDrain.Passes)]
    [Owner("SESSION_PERMISSION_ANSWER", Session = "session")]
    [GroundRead("SESSION_PERMISSION_ANSWER", nameof(PipeReads.PermissionWait))]
    [Says("SESSION_PERMISSION_ANSWER", "session:session")]
    private Task<object?> SessionPermissionAnswerAsync(SessionPermissionAnswer press, CancellationToken cancellationToken) => _wait.PermissionAnswerAsync(press);

    // What the answers-alone bound holds for the person across this machine's sessions (COWORK1e, D188 point 4; the message-urgency
    // design §8.3), oldest first: each session whose answers would take its line on a third time in a row with nobody between, the agent
    // that answered, the answers' ids and since when. A read off the home's marks (`GoOnMarks`), which the look and the conversations
    // write; it declares nothing, and sits under the sessions' key on the page, which every tick asks again.
    [DriverRoute("ANSWER_WAITS", PipeClass.Open)]
    private Task<object?> AnswerWaitsAsync(NoPayload _, CancellationToken cancellationToken) => _wait.AnswerWaitsAsync();

    // The person's answer to a third answer alone held for them: `goOn` true is *Go on with this answer*, their press
    // kept as their word and the mark taken away, so the driver's next look, nudged, or the conversation's own go-on takes the answer on;
    // false is *Not now*, the answer kept for them, going on only beside a word of theirs. Where nothing holds an answer of the session
    // for them, `answered: false` with its code (`unheld`), and nothing is written.
    [DriverRoute("SESSION_ANSWER_ALONE", PipeClass.Person, Drain = PipeDrain.Waits)]
    [Owner("SESSION_ANSWER_ALONE", Session = "session")]
    [GroundRead("SESSION_ANSWER_ALONE", nameof(PipeReads.AnswerAloneWait))]
    [Says("SESSION_ANSWER_ALONE", "session:session")]
    [Publishes("SESSION_ANSWER_ALONE", StateScopes.Sessions | StateScopes.Driver, Session = "session")]
    private Task<object?> SessionAnswerAloneAsync(SessionAnswerAlone press, CancellationToken cancellationToken) => _wait.AnswerAloneAsync(press);

    // The supervisor's decisions on a session: those live in this shell's
    // memory as they stand, and every other read from the session's files as history (`earlier`), granting nothing. A read: `{ decisions }`,
    // oldest first, each its question's snapshot, choice, reasons, holder, state, when it goes on and when it last moved.
    [DriverRoute("SESSION_SUPERVISOR", PipeClass.Open)]
    private Task<object?> SessionSupervisorAsync(SessionSupervisor press, CancellationToken cancellationToken) => _wait.SupervisorAsync(press);

    // The owner's *Overrule*: `{ id, decision }`, a press only the person may make,
    // taken under the session's decision lock. While held the decision is overruled with nothing to take back; while released its word
    // is taken back through the take-back door first. From admitted on nothing changes, and the answer says the state. `{ done, state }`,
    // `done` false with `state` null where no decision of the session is live by that id.
    [DriverRoute("SUPERVISOR_OVERRULE", PipeClass.Person, Drain = PipeDrain.Passes)]
    [Owner("SUPERVISOR_OVERRULE", Session = "id")]
    [GroundRead("SUPERVISOR_OVERRULE", nameof(PipeReads.SupervisorDecision))]
    [Says("SUPERVISOR_OVERRULE", "id:session")]
    [Publishes("SUPERVISOR_OVERRULE", StateScopes.Sessions | StateScopes.Driver, Session = "id")]
    private Task<object?> SupervisorOverruleAsync(SupervisorOverrule press, CancellationToken cancellationToken) => _wait.OverruleAsync(press, cancellationToken);

    // The supervisor's answer: `{ id, question, generation, proof, choice, reasons }`, a role's act, `Role` in
    // the pipe's census. Refused on every door but a terminal's the judge's role method covered (`PipeDoor.Role`, never the presses'
    // standing flag); kept as a held decision on the park, its repository the appointment's. `{ kept, decision?, goesOnAt?, why? }`,
    // refused as data and keeping nothing.
    [DriverRoute("SUPERVISOR_ANSWER", PipeClass.Role, Drain = PipeDrain.Passes)]
    [Publishes("SUPERVISOR_ANSWER", StateScopes.Sessions | StateScopes.Driver, Session = "id")]
    private Task<object?> SupervisorAnswerAsync(SupervisorActPress press, CancellationToken cancellationToken) => _wait.AnswerAsync(press, cancellationToken);

    // The supervisor's leaving a question to the owner: `{ id, question, generation, proof, bound, reasons }`,
    // the role's second act, `Role` in the pipe's census and judged as the answer is. Kept as a decision of state `left`, final at once:
    // nothing reaches the session, and the owner's card says *left to you* with the bound and the reasons. `{ kept, decision?, why? }`.
    [DriverRoute("SUPERVISOR_DEFER", PipeClass.Role, Drain = PipeDrain.Passes)]
    [Publishes("SUPERVISOR_DEFER", StateScopes.Sessions | StateScopes.Driver, Session = "id")]
    private Task<object?> SupervisorDeferAsync(SupervisorActPress press, CancellationToken cancellationToken) => _wait.DeferAsync(press, cancellationToken);

    // A role's act: `{ role, act, generation, form, proof }`, `Role` in the pipe's census, judged by the
    // judge's role method as SUPERVISOR_ANSWER is, then admitted once with an id Daoris draws. Its handler checks the act once more
    // under the judge's lock (§3.4 step 8: still admitted, its holder's hash present, its appointment live) and runs its role's table's
    // handler; the steward's table is empty in this build, so every act is refused `not-role` at the judge. `{ done, why?, answer?,
    // roles, brief }`, refused as data.
    [DriverRoute("ROLE_ACT", PipeClass.Role, Drain = PipeDrain.Waits)]
    [Publishes("ROLE_ACT", StateScopes.Sessions | StateScopes.Driver)]
    private Task<object?> RoleActAsync(RoleActPress press, CancellationToken cancellationToken) => _wait.RoleActAsync(press, cancellationToken);

    // What waits for the supervisor's holder: the parked sessions here with a recorded question and no
    // decision on it, in a repository a live appointment names, each with its generation. Told by event: it reads the
    // service and the question files, so an ask is answered from what is held and the verb asks until an answer computed after
    // its own first ask (`at`) comes. A read naming no key:
    // `{ waiting: [{ session, repository, question, asked, generation }], at }`.
    [DriverRoute("SUPERVISOR_WAITING", PipeClass.Open)]
    [ByEvent("SUPERVISOR_WAITING", Untold = "the question files, and the verb waits for an answer made after its own ask")]
    private Task<object?> SupervisorWaitingAsync(NoPayload _, CancellationToken cancellationToken) => _wait.WaitingAsync(cancellationToken);

    // The steward's acts this run, newest first, and its live hold's generation, what its holder's verb
    // reads before it proves an act: from the judge's memory alone, so it answers at once. A read naming no key:
    // `{ generation, acts: [{ id, act, repository, quest, session, at, standing, waiting }] }`, the generation null where none is live.
    [DriverRoute("STEWARD_ACTS", PipeClass.Open)]
    private object? StewardActs(NoPayload _) => _wait.StewardActs();

    // A request at the steward's desk: `{ act, target, why }`, `Open` in the pipe's census, any caller's, kept
    // in the judge's memory and counted against the caller's agent job or nearest long-lived ancestor (ten open each, a hundred in all).
    // It grants nothing, admits no act and reaches no act's handler: the holder reads it as claims and answers it by an act, judged as
    // ROLE_ACT judges every act, or declines it. `{ filed, request?, why? }`, refused as data (`not-staffed` while no holder holds the
    // steward, `form`, `not-role`, `desk-full`).
    [DriverRoute("STEWARD_REQUEST", PipeClass.Open)]
    [Publishes("STEWARD_REQUEST", StateScopes.Driver)]
    private object? StewardRequest(DeskRequestPress press) => _wait.StewardRequest(press);

    // The steward's desk, what its holder's `steward desk` prints, fenced, as the callers' claims: from the
    // judge's memory alone, newest first, never a caller's key. A read: `{ requests: [{ id, act, target, why, filed, state, closed,
    // answeredBy, reason }] }`, none where no pipe is served.
    [DriverRoute("STEWARD_DESK", PipeClass.Open)]
    private object? StewardDeskRead(NoPayload _) => _wait.StewardDesk();

    // What a person first said in each of these sessions: a conversation's identity, from
    // this machine's own record — never the session record, which travels. Told by event: each record's
    // read held the window up to 7.5 s, so an ask is answered from what is held and this runs on a task of its own.
    [DriverRoute("SESSION_OPENINGS", PipeClass.Open)]
    [ByEvent("SESSION_OPENINGS", Untold = "a session's first words, which SESSION_EVENTS tells and no scope or slice does")]
    private Task<object?> SessionOpeningsAsync(SessionOpeningsRead press, CancellationToken cancellationToken) => _records.OpeningsAsync(press);

    /// <summary>
    /// The sessions beside one: every other session of its repository with a tree here,
    /// chats included, with the declared and the changed paths that meet, or <c>not-read</c> where git cannot say. Read-only;
    /// git's reads make it, and a tree's read is kept only for an ended clean one, so it is read again at each ask.
    /// </summary>
    [DriverRoute("SESSION_RELATED", PipeClass.Open)]
    [ByEvent("SESSION_RELATED", Untold = "the sessions' working trees, which git reads at each ask (an uncommitted edit counts)")]
    private Task<object?> SessionRelatedAsync(SessionRelatedRead press, CancellationToken cancellationToken) => _records.RelatedAsync(press, cancellationToken);

    /// <summary>
    /// What each of these sessions spent: the *Parts* strip's cost on each part's own
    /// row, read from this machine's own record as the openings are, so a moved session reads as one. Each id that names a record
    /// answers its turns, those whose door said what they consumed, and the four counts summed, a count none said null; an id
    /// with no turn is left out.
    /// </summary>
    [DriverRoute("SESSION_SPENT", PipeClass.Open)]
    private object? SpentOf(SessionSpentRead press) => _records.SpentOf(press);

    /// <summary>
    /// The record of the finished tree a session ran in, for its page once its tree is let go: what the service kept of its
    /// landing, tip, recovery ref and commits, passed through as it answers. Null where none is kept.
    /// </summary>
    [DriverRoute("SESSION_RECORD", PipeClass.Open)]
    [ByEvent("SESSION_RECORD", Untold = "the service's record of the finished tree, kept when the driver posts it")]
    private Task<object?> SessionRecordAsync(SessionRecordRead press, CancellationToken cancellationToken) => _records.RecordAsync(press, cancellationToken);

    /// <summary>
    /// A session's memory rows for its page, passed through as the service answers
    /// them: its writes, ends and proposals with their states. None from a host with no door.
    /// </summary>
    [DriverRoute("SESSION_MEMORY", PipeClass.Open)]
    [ByEvent("SESSION_MEMORY", Untold = "the service's memory rows, which its applier and a quest's close move")]
    private Task<object?> SessionMemoryAsync(SessionMemoryRead press, CancellationToken cancellationToken) => _records.MemoryAsync(press, cancellationToken);

    // the person's decision on a session's memory, an awaiting end approved or declined by its row
    // (`op`, `approve`), or the ended session's staged changes kept or dropped (`keep`). The service judges; a refusal is answered as data,
    // `refused` with its words.
    [DriverRoute("SESSION_MEMORY_DECIDE", PipeClass.Person, Drain = PipeDrain.Passes)]
    [Owner("SESSION_MEMORY_DECIDE", Session = "session")]
    [GroundSession("SESSION_MEMORY_DECIDE", "session")]
    [Says("SESSION_MEMORY_DECIDE", "session:session")]
    [Publishes("SESSION_MEMORY_DECIDE", StateScopes.Sessions, Session = "session")]
    private Task<object?> SessionMemoryDecideAsync(SessionMemoryDecide press, CancellationToken cancellationToken) => _records.MemoryDecideAsync(press, cancellationToken);

    /// <summary>
    /// Where each of these sessions' work is now, for the rail in one ask: whether the tree it opened is still
    /// here, and its landing, standing or a trace, so a row says where the work landed rather than naming a tree a
    /// landing tidied away.
    /// </summary>
    /// <remarks>
    /// <para><b>From this machine's own files, and cheap</b>: the landing record and whether a folder is there. No service
    /// is asked and no git is run, so it answers on a cold start, and a landing's branch is read as the record holds it:
    /// the review is where git is asked where that branch stands.</para>
    ///
    /// <para><b>Only this home's trees are looked at.</b> The page sends the tree each record names; one outside the trees
    /// this home opened is a session in a repository's own checkout, which no landing tidies, and nothing is said of
    /// it. Nothing machine-local comes back.</para>
    ///
    /// <para><b>Told by event</b>: the landings' record parsed again per session and a folder listed per
    /// row held the window's thread up to 65.9 s, so an ask is answered from what is held and this runs on a task of its own; the
    /// record is read once per ask.</para>
    /// </remarks>
    [DriverRoute("SESSION_WHERE", PipeClass.Open)]
    [ByEvent("SESSION_WHERE", Reads = StateScopes.Sessions | StateScopes.Landing | StateScopes.Sweep, Slices = new[] { SweepStateSlice.Name })]
    private Task<object?> SessionWhereAsync(SessionWhereRead read, CancellationToken cancellationToken) => _groups.WhereAsync(read, cancellationToken);

    /// <summary>
    /// Where each session is listed by state: its group, its row's word and its second line, read by the driver library's one
    /// reader, which the terminal's <c>sessions</c> prints too. No git at a window's tick: a tree is read as the machine's facts
    /// judged it, so more windows never mean more git. Nothing machine-local comes back but a running call's title, the agent's
    /// own words, transcript-class and answered over this bridge alone.
    /// </summary>
    [DriverRoute("SESSION_GROUPS", PipeClass.Open)]
    private Task<object?> SessionGroupsAsync(SessionGroupsRead press, CancellationToken cancellationToken) => _groups.GroupsReadAsync(press, cancellationToken);

    /// <summary>
    /// Archive these sessions on this machine, or bring them back: the marks as they now stand.
    /// </summary>
    /// <remarks>
    /// <para><b>Each is judged by where the reader places it now</b>: a live one, one waiting on you and one with work to
    /// review are refused, so archive never hides what needs the person, and an id no record has is refused too. Asked of
    /// one session, a refusal is the answer, in the catalogue's words; asked of several, as <i>Archive what ended</i>'s
    /// second press asks, what may go is archived and the rest are kept, each with its code, since a list is a fact about
    /// a moment.</para>
    ///
    /// <para><b>Unarchive needs no service</b>: it takes marks away, and one that was not archived is said, never refused
    ///.</para>
    /// </remarks>
    [DriverRoute("SESSION_ARCHIVE", PipeClass.Person, Drain = PipeDrain.Passes)]
    [Owner("SESSION_ARCHIVE", Sessions = "ids")]
    [GroundSession("SESSION_ARCHIVE", "ids", Many = true)]
    [Says("SESSION_ARCHIVE", "ids:sessions")]
    [Publishes("SESSION_ARCHIVE", StateScopes.Sessions)]
    private Task<object?> SessionArchiveAsync(SessionArchiveMark press, CancellationToken cancellationToken) => _groups.ArchiveAsync(press, cancellationToken);

    /// <summary>
    /// Delete a conversation that served no quest: its record through the ledger, then what this
    /// machine kept of it, by the driver library's one owner, which the terminal's <c>sessions delete</c> calls too.
    /// </summary>
    /// <remarks>
    /// <para><b>The ledger's half is said first</b>: whose the record is, whether it runs, whether it served a quest, what
    /// names it and whether a remote holds it; then this machine's, its tree and a landing. Each refusal is a code in
    /// <see cref="Refusals"/> said in the catalogue's words, read by the ledger's word, never its sentence. The page offers
    /// the act only where <c>SESSION_GROUPS</c> says <c>deletable</c>, so a refusal answers a race.</para>
    ///
    /// <para><b>Nothing machine-local comes back</b>: the id, the names of what went and the names of what the disk would not let
    /// go of (<c>stayed</c>, SESSDEL1, empty when everything went), never a path. <c>session.deleted</c> is written to the machine
    /// log with no word of it.</para>
    /// </remarks>
    [DriverRoute("SESSION_DELETE", PipeClass.Person, Drain = PipeDrain.Waits)]
    [Owner("SESSION_DELETE", Session = "id")]
    [GroundSession("SESSION_DELETE", "id")]
    [Says("SESSION_DELETE", "id:session", "ids:sessions")]
    [Publishes("SESSION_DELETE", StateScopes.Sessions | StateScopes.Help, Session = "id")]
    private Task<object?> SessionDeleteAsync(SessionDelete press, CancellationToken cancellationToken) => _groups.DeleteAsync(press, cancellationToken);

    /// <summary>
    /// *Open folder*: the folder a session worked in, its own tree or its repository's checkout,
    /// opened in the system's file manager through the window kit's launcher, as the log's folder and a plugin's open.
    /// </summary>
    /// <remarks>
    /// <para><b>The module names the folder, never the page</b>: it is read from the session's record, and only a tree this
    /// home opened or the checkout the registry names for its repository is opened. A record naming any other folder, a
    /// teammate's record and a tree a tidy took are one refusal, <c>SESSION_FOLDER_GONE</c>, since each is a folder this
    /// machine does not hold for that session. The page offers the press only where it knows the folder is here, so the
    /// refusal answers a race.</para>
    ///
    /// <para><b>Nothing machine-local comes back</b>: whether it opened, and nothing else.</para>
    /// </remarks>
    [DriverRoute("SESSION_OPEN_FOLDER", PipeClass.Window)]
    [Owner("SESSION_OPEN_FOLDER", None = OwnerNone.ActsOnNothing)]
    private async Task<object?> SessionOpenFolderAsync(SessionOpenFolder press, CancellationToken cancellationToken)
    {
        var id = press.Id;
        var service = _loop.Service ?? throw NotReady();
        var record = (await service.SessionList.OneJsonAsync(id, cancellationToken).ConfigureAwait(false) is { } one ? SessionRecords.One(one) : null)
            ?? throw Refusals.Because(
                Refusals.SessionUnknown, $"No session here is {id}, so there is no folder to open.",
                ("session", id), ("context", "folder"));

        var folder = await FolderOfAsync(service, record, cancellationToken).ConfigureAwait(false)
            ?? throw Refusals.Because(
                Refusals.SessionFolderGone,
                $"The folder {id} worked in is not on this machine: the clean-up took its tree, or it ran somewhere else.",
                ("session", id));

        if (_openFolder is null) return new { Opened = false };
        try
        {
            // Through the start gate: the launcher starts the file manager's process.
            await _openFolder.OpenAsync(folder, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException
            or System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            throw Refusals.Because(
                Refusals.SessionFolderNotOpened,
                $"the folder would not open: {error.Message}",
                ("session", id), ("problem", error.Message));
        }

        return new { Opened = true };
    }

    /// <summary>
    /// The folder a session worked in, where this machine holds it: its own tree under this home's trees, else its
    /// repository's registered checkout when the record names that or no folder at all. Null for anything else.
    /// </summary>
    private async Task<string?> FolderOfAsync(ServiceClient service, SessionRecord record, CancellationToken cancellationToken)
    {
        // A teammate's record ran on their machine: a path it names is theirs, whatever this disk holds there.
        if (record.Teammate) return null;

        var trees = new SessionTrees(_loop.Home);
        if (record.Tree is { } tree && SessionsGroupsDesk.HeldTree(trees, tree)) return Directory.Exists(tree) ? tree : null;

        var root = (await service.RegistryAsync(cancellationToken).ConfigureAwait(false))
            .FirstOrDefault(row => string.Equals(row.Repository, record.Repository, StringComparison.Ordinal))?.Root;
        if (root is null || (record.Tree is { } named && !SamePlace(named, root))) return null;
        return Directory.Exists(root) ? root : null;
    }

    /// <summary>Two paths as one place: separators and a trailing one ignored, and case where the system ignores it.</summary>
    private static bool SamePlace(string left, string right)
    {
        try
        {
            return string.Equals(
                Path.TrimEndingDirectorySeparator(Path.GetFullPath(left)).Replace('\\', '/'),
                Path.TrimEndingDirectorySeparator(Path.GetFullPath(right)).Replace('\\', '/'),
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
        }
        catch (Exception error) when (error is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }

    // What sessions said, searched: the person's words and the agent's, from this machine's
    // own record, bounded and saying so.
    [DriverRoute("SESSION_SEARCH", PipeClass.Open)]
    private async Task<object?> SessionSearchAsync(SessionSearchRead press, CancellationToken cancellationToken)
    {
        var query = press.Q;
        await Task.CompletedTask;
        // Within one session, where the page names it: its words and its calls' titles.
        // A page of sessions, as the help list's is: `offset` counts sessions, `total` is every session
        // with a hit, and `next` is null on the last page. A caller naming no page reads the first, at the rail's old size.
        var found = press.Session is { } one
            ? _loop.Events.Within(one, query)
            : _loop.Events.Search(
                query,
                (int)Math.Clamp(press.Offset ?? 0, 0, int.MaxValue),
                (int)Math.Clamp(press.Limit ?? SessionEvents.SearchSessions, 0, SessionEvents.SearchPageLimit));
        return new
        {
            Query = query,
            Hits = found.Hits.Select(hit => new { hit.Session, hit.Seq, hit.Kind, hit.Snippet }).ToArray(),
            found.Cut,
            found.Total,
            found.Next,
        };
    }
}
