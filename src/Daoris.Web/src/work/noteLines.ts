import type { NotePart } from '../data/api';
import { figure, list, moment } from '../locales/format';
import { windowName } from '../settings/accounts/accounts';
import type { AccountNames } from './accountNames';
import { reasonOf } from './say';

// A session's note as the page words it (LANG1b, D142; the language design §2, §5, §6): each coded part from its code and
// values in the reader's language, each words part as written, and what the page cannot word as its record keeps it,
// marked. Pure, so every part a writer could send is an argument; `Note.tsx` draws what this answers.

type Translate = (key: string, options?: Record<string, unknown>) => string;

/**
 * A family of reasons a `why` names: MSG1f's (`work.say.why.*`, by `reasonOf`), a cool-off's (`harness.cooling.why.*`), or
 * the step that chose a conversation's account (`note.opening.why.*`, UX7d-1), or whose words a resumed conversation goes on
 * with (`note.resumed.why.*`, CONVNOTE2a), or why the person's words never reached it (`note.words.why.*`, CONVNOTE2d1).
 */
type Why = 'continue' | 'cooling' | 'opening' | 'resumed' | 'unreached' | 'handed' | 'helper' | 'handover' | 'partner';

/**
 * A code the page words: the values its sentence says, its key where it is not `note.<code>`, the reasons a `why` names, and
 * the values that are accounts on this code though their name is said another way elsewhere (`to`, `from`: SWITCH1c-i).
 */
export type NoteCode = { values: readonly string[]; key?: string; why?: Why; accounts?: readonly string[] };

/**
 * Every code a writer declares, as the page words it: the page's half of the twin (D142 point 4; the language design §5;
 * `.claude/knowledge/twins.md`). `locales/note.test.ts` parses the driver's `NoteCodes` and the service's `LedgerNoteCodes`
 * and holds this map to them code for code, with the same values, key and family, so a code a writer adds fails until it is
 * mapped here and worded in both catalogues.
 */
export const NOTE_CODES: Readonly<Record<string, NoteCode>> = {
  // A session's end.
  'ended.awaits': { values: ['awaits'] },
  'ended.exit': { values: ['exit'] },
  'ended.turn-failed-taken': { values: [] },
  'ended.turn-failed-open': { values: [] },
  // APIREFUSAL1: its model's provider refused its turn; a failed run, never a park.
  'ended.provider-refused': { values: [] },
  // MODELROLE1c2 (D205 §3.5(c)): its agent refused the choice at the open, before any prompt; no strike, planned again.
  'ended.choice-not-offered': { values: ['agent', 'model'] },
  'ended.parked-asked': { values: [] },
  'ended.parked-transcript': { values: [] },
  'ended.parked-short': { values: [] },
  // HELPERWAIT1: it ended its turn on its own helper's ask, and waits on that helper, not on the person.
  'ended.awaits-helper': { values: [] },
  'ended.answered-unfinished': { values: ['awaits', 'exit'] },
  'ended.carried-unfinished': { values: ['exit'] },
  'ended.done': { values: [] },
  // CLOSELAND1: a bound session ended at its `ready`, the result in; worded by its status's hint.
  'ended.submitted': { values: [], key: 'statusHint.Submitted' },
  'ended.declined': { values: [] },
  'ended.stood-down': { values: [] },
  'ended.source-reopened': { values: ['quest'] },
  // BGWAIT1c: the native door's harness ended its own background work with its turn.
  'ended.background-cut': { values: ['works'] },
  'ended.taken-exit': { values: ['exit'] },
  'ended.untouched': { values: [] },
  'ended.untouched-exit': { values: ['exit'] },
  'ended.went-on': { values: [] },
  'ended.went-on-exit': { values: ['exit'] },
  'ended.went-on-failed': { values: [] },
  'ended.words-wait': { values: [] },
  'ended.words-wait-you': { values: [] },
  'ended.limit-words-wait': { values: [] },
  'limit.not-lifted': { values: ['hit'] },
  // ACCESSSWAP1b: a run ended at its turn's end because its permission prompts changed, and its held words go on under the new setup.
  'ended.setup-changed': { values: ['from', 'to'] },
  'ended.words-setup': { values: [] },
  'ended.timeout': { values: ['minutes'] },
  // SESSWATCH1a (D200): state E's strays ended, the person's end of a quiet run, and the watcher's own lines.
  'ended.gone': { values: [] },
  'ended.quiet': { values: ['minutes'] },
  'ended.quiet-pressed': { values: ['minutes'] },
  'watch.quiet': { values: ['minutes'] },
  'watch.quiet-call': { values: ['minutes', 'call'] },
  'watch.quiet-work': { values: ['minutes', 'works'] },
  'watch.left': { values: [] },
  'ended.stopped': { values: [] },
  'ended.lost-claim': { values: [] },
  'ended.lost-claim-reopened': { values: [] },
  'ended.handed-over': { values: ['agent'] },
  'ended.driver-closed': { values: [] },
  // An account's line. `account.cooling` is the shape notes from before AGT3d carry; since, a limit's line names whose
  // accounts and the window its reset named, or has a line of its own where it named none.
  'account.cooling': { values: ['until', 'why'], why: 'cooling' },
  'account.cooling-window': { values: ['until', 'why', 'owner', 'window'], why: 'cooling' },
  'account.cooling-no-window': { values: ['until', 'why', 'owner'], why: 'cooling' },
  'account.refused': { values: ['owner'] },
  'account.refused-own': { values: ['owner'] },
  'account.signed-out': { values: ['owner'] },
  'account.signed-out-own': { values: ['owner'] },
  // MODELROLE1c2 (D205 §3.5(a)): the account refused the model its actor asked; the account's, never a strike.
  'account.refused-model': { values: ['agent', 'model'] },
  // Why the account a resume asked for could not carry the words (MSG1g), after a line whose reason is `account`.
  'account.resume-signed-out': { values: [] },
  'account.resume-refused': { values: [] },
  'account.resume-gone': { values: [] },
  'account.resume-outside': { values: [] },
  'account.resume-kept': { values: [] },
  'account.resume-new-session': { values: [] },
  'account.resume-not-offered': { values: [] },
  // While it starts and works.
  'started.tree': { values: ['branch', 'basedOn'] },
  'started.tree-unrecorded': { values: [] },
  // TREEDEPS1: a tree its checkout's dependencies were copied into, and why a project's were not.
  'started.tree-copied': { values: ['branch', 'basedOn', 'copied'] },
  'started.uncopied-manager': { values: ['modules'] },
  'started.uncopied-moved': { values: ['modules'] },
  'started.uncopied-uninstalled': { values: ['modules'] },
  'started.uncopied-workspace': { values: ['modules', 'workspace'] },
  'started.uncopied-link': { values: ['modules'] },
  'started.uncopied-unignored': { values: ['modules'] },
  'started.uncopied-failed': { values: ['modules'] },
  'tree.remade': { values: ['branch'] },
  'tree.gone': { values: ['branch'] },
  'tree.unmade': { values: [] },
  'started.joined': { values: ['session'] },
  'started.resumes-answered': { values: ['quest', 'answered'] },
  'started.in-asking-tree': { values: ['session'] },
  'started.words-open': { values: ['quest', 'session'] },
  'started.words-taken': { values: ['quest', 'session'] },
  'started.answer': { values: ['quest', 'session'] },
  'started.released': { values: ['quest', 'session'] },
  'started.cut-off': { values: ['quest', 'session'] },
  // An open quest carried on in the tree whose session committed work and failed before it took it (RETRYWORK1).
  'started.carried-work': { values: ['quest', 'session'] },
  // A stand-down carried on once the person brought its lost take forward (SYNCMERGE1b).
  'started.brought-forward': { values: ['quest', 'session'] },
  // A quest handed to another agent (MIXAGENT1a-ii): where it came from, and the account it named passed.
  'started.handed-over': { values: ['quest', 'session', 'agent'] },
  'started.handed-walked': { values: [] },
  'started.other-account': { values: [] },
  'started.same-tree': { values: [] },
  'started.fell-back': { values: ['why'], why: 'continue' },
  'working.resumes-answer': { values: [] },
  'working.goes-on': { values: [] },
  // Where the person's words went.
  'went.cannot': { values: ['why'], key: 'work.say.cannot', why: 'continue' },
  'went.new-session': { values: ['why'], why: 'continue' },
  'went.carried-on': { values: ['why'], why: 'continue' },
  // An intake.
  'intake.ask-deleted': { values: [] },
  'intake.published': { values: ['quests', 'ask'] },
  'intake.ask-closed': { values: ['ask'] },
  'intake.ask-answered': { values: ['ask', 'quests'] },
  'intake.turn-failed': { values: ['ask'] },
  'intake.asks': { values: ['ask'] },
  'intake.exit': { values: ['ask', 'exit'] },
  'intake.person-answered': { values: ['ask', 'quests'] },
  'intake.person-closed': { values: ['ask'] },
  'intake.person-deleted': { values: ['ask'] },
  // A stop.
  'stopped.paused-ask': { values: ['ask'] },
  'stopped.paused-quest': { values: ['quest'] },
  'stopped.abandoned-ask': { values: ['ask'] },
  'stopped.abandoned-quest': { values: ['quest'] },
  'stopped.checkpoint-finished': { values: [] },
  'stopped.checkpoint-stopped': { values: [] },
  'stopped.checkpoint-moved': { values: [] },
  'stopped.orphan': { values: [] },
  'standing.terminal': { values: [] },
  'chat.closed': { values: [] },
  'chat.ended-by-person': { values: [] },
  'chat.ended': { values: [] },
  'chat.cancelled': { values: [] },
  'chat.not-kept': { values: [] },
  'chat.restarted': { values: [] },
  'update.closed': { values: ['build'] },
  // Ask Daoris's start, where the machine's reading for its room did not answer in time (HELPSTUCK1).
  'help.room-as-written': { values: ['seconds'] },
  'help.room-unread': { values: ['seconds'] },
  // Ask Daoris started at once on the facts the judges keep, some of the machine not read yet (PARALLEL1a).
  'help.room-not-read-yet': { values: [] },
  // A landing at the quest's done, under a rule that accepts automatically (LAND2b).
  'landing.accepted': { values: ['branch'] },
  'landing.plugin-unready': { values: ['branch', 'plugin'] },
  'landing.plugin-failed': { values: ['branch', 'plugin'] },
  // Its push held back after the branch was made (FABRIC-CLOSE1h2), handed on at the look after the release.
  'landing.push-held': { values: ['branch', 'plugin'] },
  // Held back there by its landing's gate (FABRIC-CLOSE1h14), the gate's sentence beneath it; a note from before it says `landing.push-held`.
  'landing.push-gated': { values: ['branch', 'plugin'] },
  'landing.nothing': { values: [] },
  'landing.held': { values: [] },
  'landing.uncommitted': { values: ['paths'] },
  'landing.exists': { values: ['branch'] },
  'landing.refused': { values: [] },
  // Its replay onto the line conflicts and its session was told to rebase in its own tree (LANDREBASE1), the refusal beneath it.
  'landing.rebase': { values: [] },
  // Held by the review's gate (REVIEWENV1c), the gate's sentence beneath it; a note from before it says `landing.refused`.
  'landing.unreviewed': { values: [] },
  // Held by the second opinion's gate (XAGENT1g), the gate's sentence beneath it; a note from before it says `landing.refused`.
  'landing.opinion': { values: [] },
  'landing.no-tree': { values: [] },
  'landing.not-done': { values: [] },
  // The access setup lands work by its rule, and the rule merges, so Accept stays (ACCESSWORDS1); `access` is the level that set it.
  'landing.merges': { values: ['access'] },
  // The service's own lines.
  'ledger.answered': { values: [] },
  'ledger.parked': { values: [] },
  'ledger.went-on': { values: ['at'] },
  // Another agent's findings alone, or beside the person's words (XAGENT1e2), worded since XAGENT1g.
  'ledger.went-on-findings': { values: ['at'] },
  'ledger.went-on-both': { values: ['at'] },
  // A helper's answer alone, or beside the person's words (MIXAGENT2a-ii).
  'ledger.went-on-helper': { values: ['at'] },
  'ledger.went-on-helper-both': { values: ['at'] },
  // Daoris's own words alone (COWORK1b): a reopen told, a step's go-on, never "your words".
  'ledger.went-on-daoris': { values: ['at'] },
  // A quest's note (QUESTCLOSE1b1): the person marked it done, their words a part beneath it.
  'quest.person-done': { values: [] },
  // A conversation's first line (UX7d-1): the account it opened on and the step that chose it, then what each account said.
  'opening.opened': { values: ['account', 'why'], why: 'opening' },
  'opening.carried-on': { values: ['session', 'account', 'why'], why: 'opening' },
  'opening.rest': { values: ['why'], why: 'opening' },
  'opening.turn-refused': { values: ['turn'] },
  'opening.turn-context': { values: ['turn', 'tokens'] },
  'opening.unsaid': { values: [] },
  'opening.said-at': { values: ['account', 'seen'] },
  'opening.said-nothing': { values: ['account'] },
  'opening.said-reached': { values: ['window'] },
  // The agent's pace warning, never near (USAGEREAD3a); a line written before says `opening.said-near`, worded as it was.
  'opening.said-near': { values: ['window'] },
  'opening.said-warned': { values: ['window'] },
  'opening.said-credits': { values: [] },
  'opening.said-used': { values: ['used', 'window'] },
  // The scope's one number, no longer written (SWITCH1b-ii); a line written before keeps it. Over its curve since.
  'opening.said-near-at': { values: ['near'] },
  'opening.said-over': { values: ['window', 'curve'] },
  // A window's use projected from its reading and Daoris's own turns since, never near or over (USAGEPROBE1).
  'opening.said-projected': { values: ['projected', 'window'] },
  'opening.said-clear': { values: [] },
  // MODELROLE1c2 (D205 §4.6 item 1): a later choice of an actor's ran, each choice passed said in the walk's own words.
  'opening.actor': { values: ['actor', 'n', 'entry', 'passed'] },
  // A resumed conversation's first line (CONVNOTE2a): the adapter and whose words, a version that moved riding inside it.
  'resumed.opening': { values: ['adapter', 'why'], why: 'resumed' },
  'resumed.moved': { values: ['now', 'then'] },
  'resumed.native': { values: ['adapter'] },
  // The native door going on with Daoris's own words alone (COWORK1b): never "what you said".
  'resumed.native-daoris': { values: ['adapter'] },
  // A permission the rules refused (CONVNOTE2b): the call's title rides between the two as the agent's words.
  'refused.lead': { values: [] },
  'refused.governs': { values: [] },
  // A permission held for the person under asking (AUTONOMY1b3a, D184): the call's title rides after the lead as the agent's words; a
  // refusal leads with `refused.lead`, and its close says who or what refused it, the bound's minutes a fact.
  'asking.held': { values: [] },
  'asking.allowed': { values: [] },
  'asking.denied': { values: [] },
  'asking.cancelled': { values: [] },
  'asking.ended': { values: [] },
  'asking.bound': { values: ['minutes'] },
  // A press's note and a chat's settings (CONVNOTE2c): a landing's and a preface's sentence follow their lead as a program's words.
  'landing.person-accepted': { values: [] },
  'landing.person-handed': { values: [] },
  // A part the person left out of its entry's Accept (ACCEPTALL1c): the branch its line of work landed on, or the line.
  'landing.left-out': { values: ['branch'] },
  'chat.option-set': { values: ['option', 'chosen'] },
  'chat.told-where': { values: [] },
  // Words that did not reach an agent or were not taken (CONVNOTE2d1): the agent's or the service's words follow a lead.
  'words.refused': { values: [] },
  'words.unreached': { values: ['count', 'why'], why: 'unreached' },
  'words.not-taken': { values: [] },
  // The chat runner's and the modules' sites (CONVNOTE2d2): a program's or the service's words follow each lead but `not-sent`.
  'words.not-reached': { values: [] },
  'words.not-sent': { values: [] },
  'words.start-not-taken': { values: ['session'] },
  'words.not-kept': { values: [] },
  // A server that drives Daoris's own browser, withheld: why follows as a code of the host's or an exception's words.
  'browser.withheld': { values: ['servers'] },
  'browser.no-host': { values: [] },
  'browser.none-up': { values: [] },
  // The line a chat's agent was told of the browser it was handed (BRWHINT1a): the line follows as a program's words.
  'chat.told-browser': { values: [] },
  // The repository's process a chat in a tree of its own was told (PROCESS1d-ii): its commit rule's mode and where it was set,
  // the words following as a program's.
  'chat.told-process': { values: ['mode', 'source'] },
  // A conversation handed to another agent (MIXAGENT1a-i): where the old one went, and where the new one came from and why.
  'chat.handed-over': { values: ['agent', 'session'] },
  'chat.taken-over': { values: ['agent', 'session', 'why'], why: 'handed' },
  'chat.briefing-unsaid': { values: [] },
  // A session's move to a new part (SESSCTX1c): on the part it left and on the new part, each with why by the hand-over's reasons.
  'moved.on': { values: ['session', 'agent', 'account', 'why', 'tokens', 'count'], why: 'handed' },
  'moved.from': { values: ['session', 'agent', 'account', 'why', 'tokens', 'count'], why: 'handed' },
  // A conversation moved to another account of its agent by the usage curve (SWITCH1c-i): where it went and where it came from,
  // each account by the name the person gave it, and how far over its curve the account it left was.
  'chat.moved': { values: ['to', 'from', 'window', 'used', 'curve'], accounts: ['to', 'from'] },
  'chat.moved-from': { values: ['from', 'window', 'used', 'curve'], accounts: ['from'] },
  // A conversation whose account's limit refused its turn, whose move to the next account did not open (LIMITGOON1).
  'chat.limit-not-moved': { values: [] },
  // A conversation its account's limit cut off with no account to go on on (LIMITRESUME1c): it waits, and the person's *Stop waiting*.
  'chat.limit-waits': { values: ['owner', 'until', 'why'], why: 'cooling' },
  'chat.limit-stopped-waiting': { values: [] },
  // The limit's wake (LIMITRESUME1, D206 §3.7): its own conversation went on, or could not be resumed and went on briefed.
  'limit.resumed': { values: ['until'] },
  'limit.resume-refused': { values: ['why'], why: 'continue' },
  // A helper on another agent (MIXAGENT2a-ii): on the asker, asked, answered or refused, with the agent and why; on the helper,
  // whose session it helps.
  'helper.asked': { values: ['agent', 'session'] },
  'helper.answered': { values: ['agent', 'session'] },
  'helper.answered-last-words': { values: ['agent', 'session'] },
  'helper.refused': { values: ['why'], why: 'helper' },
  'helper.refused-by': { values: ['agent', 'why'], why: 'helper' },
  'helper.helping': { values: ['session'] },
  // LIMITRESUME1h: a helper an account's limit cut off, which the session no longer waits on, with when its account is ready again.
  'helper.limited': { values: ['agent', 'until'] },
  'helper.limited-no-reset': { values: ['agent'] },
  // A partner's piece (COAGENT1b, D199 point 8): on the lead its course, on the partner whose session it works for. Notes, never words.
  'partner.asked': { values: ['agent'] },
  'partner.started': { values: ['agent', 'session'] },
  'partner.answered': { values: ['agent', 'session'] },
  'partner.refused': { values: ['why'], why: 'partner' },
  'partner.stopped': { values: ['agent'] },
  'partner.out-of-time': { values: ['agent'] },
  'partner.limited': { values: ['agent'] },
  'partner.uncertain': { values: ['why'], why: 'partner' },
  'partner.beyond': { values: ['count'] },
  'partner.kept': { values: ['why'], why: 'partner' },
  'partner.working': { values: ['session'] },
  // The answers-alone bound (COWORK1e, D188 point 4): a third answer in a row with nobody between held for the person, by the agent that
  // answered; their *Go on with this answer*, which counts as their word; their *Not now*.
  'answers.held': { values: ['agent'] },
  'answers.passed': { values: [] },
  'answers.kept': { values: [] },
  // An agent's ask to hand its session over (MIXAGENT1c): waiting on the person with the agent it would go on with, going with it at
  // the turn's end, or refused and why.
  'handover.asked': { values: ['agent'] },
  'handover.taking': { values: ['agent'] },
  'handover.refused': { values: ['why'], why: 'handover' },
  // HANDOVERSAID1: the person's own ask names the path that made it; a hand-over let go that could not be taken is not taken, never refused.
  'handover.asked-terminal': { values: ['agent'] },
  'handover.asked-window': { values: ['agent'] },
  'handover.not-taken': { values: ['agent', 'why'], why: 'handover' },
  'handover.not-taken-ends': { values: ['agent', 'why'], why: 'handover' },
  // A session's own background work (CONVNOTE2f): the work's names as code, a state as the harness said it; a program's words
  // follow `unread`.
  'background.unread': { values: [] },
  'background.waits': { values: ['works'] },
  'background.work-ended': { values: ['work', 'state'] },
  'background.goes-on': { values: [] },
  // DONEEND1: the quest read again no longer waits on the work, so the session ends and the work still running ends with it.
  'background.wait-over': { values: ['works'] },
  // A conversation's door that failed (CONVNOTE2e): a program's words follow each lead but the resume's; a refused resume is
  // its reason by MSG1f's family, the agent's words after it where it said any, then an answer carried on.
  'door.not-open': { values: [] },
  'door.turn-not-taken': { values: [] },
  'door.failed': { values: [] },
  'door.resume-refused': { values: ['why'], why: 'continue' },
  'door.answer-carried-on': { values: [] },
  // A review set-up's record (CONVNOTE2h): the environment's name and repository, the commit's short id as facts; the service's
  // message and the served sentence are a program's words after `posted` and `not-posted`.
  'review.no-rule': { values: ['named', 'to'] },
  'review.no-commit': { values: [] },
  'review.posted': { values: ['commit', 'named'] },
  'review.not-posted': { values: [] },
  'review.posts-next': { values: [] },
  // A second opinion's answers as the working session's turn ended (CONVNOTE2h): whose findings and how many, then each count.
  'opinion.answers': { values: ['who', 'count'] },
  'opinion.fixed': { values: ['count'] },
  'opinion.unproven': { values: ['count'] },
  'opinion.rejected': { values: ['count'] },
  'opinion.unresolved': { values: ['count'] },
  'opinion.unanswered': { values: ['count'] },
  'opinion.disputed': { values: ['count'] },
  // How the reviewer's own session ended (XAGENT1g4a); a failed turn's words follow as the program's.
  'opinion.reviewer-failed': { values: ['opinion'] },
  'opinion.reviewer-read': { values: ['opinion'] },
  'opinion.reviewer-exited': { values: ['opinion', 'exit'] },
  // A conversation's words waiting for its own account's reset (CONVNOTE2g): the account by its owner and its id, why by a
  // cool-off's reason; the tool's own sign-in and its refresh; each door out, the session its command names a value.
  'waits.account': { values: ['owner', 'account', 'until', 'why'], why: 'cooling' },
  'waits.own': { values: ['owner', 'until', 'why'], why: 'cooling' },
  'waits.read-again': { values: [] },
  'waits.door-new-session': { values: ['session'] },
  'waits.door-chat': { values: ['session'] },
  'waits.door-help': { values: [] },
  // A conversation's words held for another reason (CONVNOTE2g2): a reason of Daoris's own is one line, its repository or
  // conversation a value; a program's message is words between `held.lead` (or `config-unread`) and `held.tail`.
  'held.no-checkout': { values: ['repository'] },
  'held.replaying': { values: ['repository'] },
  'held.answering': { values: ['session'] },
  'held.config-unread': { values: [] },
  'held.lead': { values: [] },
  'held.tail': { values: [] },
  // SESSCTX1c: the agent its words would go on with is no longer one this machine has; the hand-over is the way on.
  'held.agent-gone': { values: ['agent', 'session'] },
  // The roster's refusal as a held note's reason (ROSTERREFUSE1): an account by its owner and its id, the sign-in's profile the
  // id as a shell spells it, the tool's own sign-in's refresh `waits.read-again`.
  'held.cooling-account': { values: ['owner', 'account', 'until', 'why'], why: 'cooling' },
  'held.cooling-own': { values: ['owner', 'until', 'why'], why: 'cooling' },
  'held.limit-waits': { values: ['owner', 'account', 'until'], why: 'cooling' },
  'held.limit-waits-own': { values: ['owner', 'until'], why: 'cooling' },
  'held.signed-out': { values: ['owner', 'account', 'profile'] },
  // OWNSPACE1a: no Daoris account of the agent's is signed in, and its own home is never a start's.
  'held.no-account': { values: ['owner'] },
  'held.not-installed': { values: ['agent'] },
  'held.pin-not-installed': { values: ['agent', 'version'] },
  // The roster's other refusals (ROSTERREFUSE2): a wait over a list is its lead, a clause for each account, the first reset and
  // the sign-in; a pick refused adds who is ready; an agent with its own tooling has its command as words between its two lines.
  'held.wait-every': { values: ['agent'] },
  'held.wait-some': { values: ['agent'] },
  'held.clause-cooling': { values: ['account', 'until'] },
  'held.clause-refused': { values: ['account'] },
  'held.clause-signed-out': { values: ['account'] },
  'held.clause-ready': { values: ['account'] },
  'held.wait-first': { values: ['account', 'until', 'why'], why: 'cooling' },
  'held.sign-in-sooner': { values: ['logins'] },
  'held.sign-in': { values: ['logins'] },
  'held.picked-ready': { values: ['accounts'] },
  'held.picked-none': { values: ['agent'] },
  'held.refused-account': { values: ['owner', 'account'] },
  'held.refused-own': { values: ['owner'] },
  'held.sign-in-refused-own': { values: ['owner'] },
  'held.not-installed-own': { values: ['agent'] },
  'held.never-installs': { values: [] },
  'held.kept-aside': { values: ['account'] },
  'held.locked': { values: ['owner', 'accounts'] },
  'held.outside-machine': { values: ['accounts', 'first', 'door'] },
  'held.outside-workspace': { values: ['workspace', 'accounts', 'first', 'door'] },
};

/**
 * Whose words a resumed conversation goes on with (CONVNOTE2a; the driver's `NoteCodes.Resumed`), each its entry; none carries
 * a value of its own. `locales/note.test.ts` holds this map to the driver's declaration key for key.
 */
export const RESUMED_REASONS: Readonly<Record<string, { key: string; values: readonly string[] }>> = {
  answer: { key: 'note.resumed.why.answer', values: [] },
  words: { key: 'note.resumed.why.words', values: [] },
  findings: { key: 'note.resumed.why.findings', values: [] },
  'answer-and-findings': { key: 'note.resumed.why.answer-and-findings', values: [] },
  'words-and-findings': { key: 'note.resumed.why.words-and-findings', values: [] },
  helper: { key: 'note.resumed.why.helper', values: [] },
  'words-and-helper': { key: 'note.resumed.why.words-and-helper', values: [] },
  // Daoris's own words alone (COWORK1b): a reopen told, a step's go-on.
  daoris: { key: 'note.resumed.why.daoris', values: [] },
};

/**
 * Why the person's words never reached the session (CONVNOTE2d1; the driver's `NoteCodes.Unreached`), each its entry; none
 * carries a value of its own. `locales/note.test.ts` holds this map to the driver's declaration key for key.
 */
export const UNREACHED_REASONS: Readonly<Record<string, { key: string; values: readonly string[] }>> = {
  'ended-first': { key: 'note.words.why.ended-first', values: [] },
  unkept: { key: 'note.words.why.unkept', values: [] },
};

/**
 * Why a conversation was handed to another agent (MIXAGENT1a-i; the driver's `NoteCodes.Handed`), each its entry; none carries
 * a value of its own. `locales/note.test.ts` holds this map to the driver's declaration key for key.
 */
export const HANDED_REASONS: Readonly<Record<string, { key: string; values: readonly string[] }>> = {
  press: { key: 'note.handed.why.press', values: [] },
  near: { key: 'note.handed.why.near', values: [] },
  limit: { key: 'note.handed.why.limit', values: [] },
  agent: { key: 'note.handed.why.agent', values: [] },
  // Daoris moved it by the usage curve (SWITCH1c-i, D165 point 3).
  curve: { key: 'note.handed.why.curve', values: [] },
  // SESSCTX1b: every other continuation names its link: its conversation lost, a carry-on, a driven record's fallback.
  resume: { key: 'note.handed.why.resume', values: [] },
  'carry-on': { key: 'note.handed.why.carry-on', values: [] },
  retry: { key: 'note.handed.why.retry', values: [] },
};

/**
 * Why a piece was refused, held or uncertain (COAGENT1b; the driver's `NoteCodes.Partner`), each its entry; none carries a value of
 * its own. `locales/note.test.ts` holds this map to the driver's declaration key for key.
 */
export const PARTNER_REASONS: Readonly<Record<string, { key: string; values: readonly string[] }>> = {
  'asker-unfit': { key: 'note.partner.why.asker-unfit', values: [] },
  'asker-gone': { key: 'note.partner.why.asker-gone', values: [] },
  blank: { key: 'note.partner.why.blank', values: [] },
  'path-outside': { key: 'note.partner.why.path-outside', values: [] },
  'no-commit': { key: 'note.partner.why.no-commit', values: [] },
  'base-missing': { key: 'note.partner.why.base-missing', values: [] },
  'too-many': { key: 'note.partner.why.too-many', values: [] },
  'not-listed': { key: 'note.partner.why.not-listed', values: [] },
  'no-connector': { key: 'note.partner.why.no-connector', values: [] },
  'not-measured': { key: 'note.partner.why.not-measured', values: [] },
  'copy-failed': { key: 'note.partner.why.copy-failed', values: [] },
  'no-job': { key: 'note.partner.why.no-job', values: [] },
  'state-unreadable': { key: 'note.partner.why.state-unreadable', values: [] },
  'job-unknown': { key: 'note.partner.why.job-unknown', values: [] },
  'ref-moved': { key: 'note.partner.why.ref-moved', values: [] },
  'import-refused': { key: 'note.partner.why.import-refused', values: [] },
  'no-work': { key: 'note.partner.why.no-work', values: [] },
  'hand-cut': { key: 'note.partner.why.hand-cut', values: [] },
};

/**
 * Why no helper answered a question (MIXAGENT2a-ii; the driver's `NoteCodes.Helper`), each its entry; none carries a value of its
 * own. `locales/note.test.ts` holds this map to the driver's declaration key for key.
 */
export const HELPER_REASONS: Readonly<Record<string, { key: string; values: readonly string[] }>> = {
  off: { key: 'note.helper.why.off', values: [] },
  'asker-gone': { key: 'note.helper.why.asker-gone', values: [] },
  'asker-unfit': { key: 'note.helper.why.asker-unfit', values: [] },
  'too-many': { key: 'note.helper.why.too-many', values: [] },
  unreadable: { key: 'note.helper.why.unreadable', values: [] },
  'not-started': { key: 'note.helper.why.not-started', values: [] },
  'no-answer': { key: 'note.helper.why.no-answer', values: [] },
  conversation: { key: 'note.helper.why.conversation', values: [] },
  'cannot-take': { key: 'note.helper.why.cannot-take', values: [] },
  // The person's no to an agent's ask (HELPERNO1), added after the nine.
  declined: { key: 'note.helper.why.declined', values: [] },
  // The asker's work in progress that could not be captured whole for its helper (COAGENT1a, D199 point 5).
  'capture-merge': { key: 'note.helper.why.capture-merge', values: [] },
  'capture-link': { key: 'note.helper.why.capture-link', values: [] },
  'capture-submodule': { key: 'note.helper.why.capture-submodule', values: [] },
  'capture-unreadable': { key: 'note.helper.why.capture-unreadable', values: [] },
  'capture-bound': { key: 'note.helper.why.capture-bound', values: [] },
  'capture-moved': { key: 'note.helper.why.capture-moved', values: [] },
  // A hand whose outcome a restart lost (HELPERWAIT1), never made again by itself.
  'hand-uncertain': { key: 'note.helper.why.hand-uncertain', values: [] },
  // LIMITRESUME1h: a limit cut it off and the session no longer waits on it (its line is `helper.limited`, which says when).
  limited: { key: 'note.helper.why.limited', values: [] },
};

/**
 * Why an agent's ask to hand its session over came to nothing (MIXAGENT1c; the driver's `NoteCodes.HandOver`), each its entry;
 * none carries a value of its own. `locales/note.test.ts` holds this map to the driver's declaration key for key.
 */
export const HAND_OVER_ASK_REASONS: Readonly<Record<string, { key: string; values: readonly string[] }>> = {
  off: { key: 'note.handover.why.off', values: [] },
  declined: { key: 'note.handover.why.declined', values: [] },
  'just-handed': { key: 'note.handover.why.just-handed', values: [] },
  'no-agent': { key: 'note.handover.why.no-agent', values: [] },
  'asker-gone': { key: 'note.handover.why.asker-gone', values: [] },
  'asker-unfit': { key: 'note.handover.why.asker-unfit', values: [] },
  cannot: { key: 'note.handover.why.cannot', values: [] },
  'quest-closed': { key: 'note.handover.why.quest-closed', values: [] },
  'quest-elsewhere': { key: 'note.handover.why.quest-elsewhere', values: [] },
  'quest-reopened': { key: 'note.handover.why.quest-reopened', values: [] },
  'not-last-run': { key: 'note.handover.why.not-last-run', values: [] },
  'unknown-agent': { key: 'note.handover.why.unknown-agent', values: [] },
  'same-agent': { key: 'note.handover.why.same-agent', values: [] },
  'no-door': { key: 'note.handover.why.no-door', values: [] },
  running: { key: 'note.handover.why.running', values: [] },
  'tree-gone': { key: 'note.handover.why.tree-gone', values: [] },
  waiting: { key: 'note.handover.why.waiting', values: [] },
};

/**
 * The states background work is known to end in (CONVNOTE2i; the driver's `BackgroundState`), each its entry. Any other state
 * is shown as the harness sent it. `locales/note.test.ts` holds this map to the driver's declaration key for key.
 */
export const BACKGROUND_STATES: Readonly<Record<string, string>> = {
  completed: 'note.background.state.completed',
  failed: 'note.background.state.failed',
  stopped: 'note.background.state.stopped',
  ended: 'note.background.state.ended',
};

/**
 * The steps that chose a conversation's account (UX7d-1, D152's UX7d-1 note; the driver's `NoteCodes.Opening`), each its
 * entry and its own values, which ride beside the line's `why`. `locales/note.test.ts` holds this map to the driver's
 * declaration key for key and value for value.
 */
export const OPENING_REASONS: Readonly<Record<string, { key: string; values: readonly string[] }>> = {
  cooling: { key: 'note.opening.why.cooling', values: ['agent', 'over', 'until', 'cooling'] },
  refused: { key: 'note.opening.why.refused', values: ['agent', 'over'] },
  'signed-out': { key: 'note.opening.why.signed-out', values: ['agent', 'over'] },
  locked: { key: 'note.opening.why.locked', values: ['agent', 'over'] },
  'kept-self': { key: 'note.opening.why.kept-self', values: [] },
  'kept-last': { key: 'note.opening.why.kept-last', values: ['over'] },
  kept: { key: 'note.opening.why.kept', values: ['over'] },
  'near-reached': { key: 'note.opening.why.near-reached', values: ['over', 'window'] },
  // No longer written (USAGEREAD3a: the pace warning is not near); a line written before keeps it.
  'near-word': { key: 'note.opening.why.near-word', values: ['over', 'window'] },
  'near-credits': { key: 'note.opening.why.near-credits', values: ['over'] },
  // No longer written (SWITCH1b-ii: near is the curve, and both near are said by the other's reason); a line written before keeps them.
  'near-used': { key: 'note.opening.why.near-used', values: ['over', 'used', 'window', 'near'] },
  'over-curve': { key: 'note.opening.why.over-curve', values: ['over', 'window', 'used', 'curve'] },
  near: { key: 'note.opening.why.near', values: ['over'] },
  room: { key: 'note.opening.why.room', values: ['over', 'used', 'against'] },
  fewest: { key: 'note.opening.why.fewest', values: [] },
  // No longer written (USAGERESET1: step 4 weighs what would lapse, said by `lapses`); a line written before keeps it.
  lapsing: { key: 'note.opening.why.lapsing', values: ['at'] },
  lapses: { key: 'note.opening.why.lapses', values: ['window', 'unused', 'at', 'seen'] },
  'most-room': { key: 'note.opening.why.most-room', values: ['room', 'window'] },
  // The week's pace, written under `switch: off` only since the curve (SWITCH1b-ii).
  behind: { key: 'note.opening.why.behind', values: ['used', 'gone'] },
  ahead: { key: 'note.opening.why.ahead', values: ['over', 'used', 'gone'] },
  'not-started': { key: 'note.opening.why.not-started', values: [] },
  'least-recent': { key: 'note.opening.why.least-recent', values: [] },
  // No longer written (POOL1c, D197: every start reads the one set, whose first is `first-order`); a line written before keeps
  // them, `default-here` and `first-here` said without the machine's list's name (the account-pool design §4.3).
  default: { key: 'note.opening.why.default', values: ['workspace'] },
  'default-here': { key: 'note.opening.why.default-here', values: [] },
  first: { key: 'note.opening.why.first', values: ['workspace'] },
  'first-here': { key: 'note.opening.why.first-here', values: [] },
  'first-order': { key: 'note.opening.why.first-order', values: [] },
};

/**
 * How each value a code carries is said (the language design §3–§4): an id as the record writes it, a list of quest ids
 * joined the reader's way, a moment in the reader's language and zone, a number in its figures, a fact as it is, a reason
 * by its family, and a limit's window as the agents' screens word it (`harness.window.*`, AGT3d), one this build does not
 * know said as named. A conversation's first line (UX7d-1) adds a count grouped the reader's way, a whole percent, and a
 * cool-off's reason as a value of its own. `locales/note.test.ts` holds that every value a writer declares has a way here.
 */
export const NOTE_VALUES: Readonly<
  Record<
    string,
    | 'id' | 'ids' | 'names' | 'moment' | 'number' | 'count' | 'percent' | 'text' | 'why' | 'cooling' | 'window' | 'account' | 'accounts'
    | 'bgstate' | 'setAt' | 'accessAt' | 'actor'
  >
> = {
  // A commit rule's mode, its own word set as code by the sentence, and where it was set (PROCESS1d-ii).
  mode: 'text', source: 'setAt',
  // The level whose access setup landed work by its rule (ACCESSWORDS1).
  access: 'accessAt',
  awaits: 'id', answered: 'id', ask: 'id', quest: 'id', session: 'id', opinion: 'id',
  quests: 'ids',
  servers: 'names', works: 'names',
  option: 'text', chosen: 'text', work: 'text', state: 'bgstate', repository: 'text',
  // SESSWATCH1a: the call a quiet session's agent was inside, its title as the record named it.
  call: 'text',
  // LIMITRESUME1b: the limit an attempt just after its reset met again, in the agent's own words.
  hit: 'text',
  // LIMITRESUME1i: the build an update installed when its close cut a run, as `version` is said.
  build: 'text',
  until: 'moment', at: 'moment', seen: 'moment',
  exit: 'number', minutes: 'number', paths: 'number', turn: 'number',
  // How long Ask Daoris's start waited for the machine's reading, in whole seconds (HELPSTUCK1).
  seconds: 'number',
  // An account's room under its usage curve, in whole percent points (SWITCH1b-ii).
  room: 'number',
  tokens: 'count', count: 'count',
  // A curve's value now is a whole percent, as a window's use is (SWITCH1b-ii).
  used: 'percent', near: 'percent', gone: 'percent', against: 'percent', curve: 'percent',
  // The share of a week unused at its reset, as a window's use is (USAGERESET1).
  unused: 'percent',
  // A window's use projected to now, as a window's use is (USAGEPROBE1).
  projected: 'percent',
  adapter: 'text', now: 'text', then: 'text',
  // An account id is worded by the name the person gave it where the page knows it (NOTENAME1); `owner` is the agent.
  account: 'account', over: 'account', first: 'account',
  // A list of account ids, each named so and set as code (ROSTERREFUSE2); `logins` are the sign-in commands, each set as code.
  accounts: 'accounts', logins: 'names',
  profile: 'text', version: 'text', door: 'text',
  // A review set-up's environment and repository, its commit's short id, and whose findings a second opinion's answers are (CONVNOTE2h).
  // `to` and `from` are a code's accounts where it says so (`NoteCode.accounts`: a move by the curve, SWITCH1c-i).
  named: 'text', to: 'text', from: 'text', commit: 'text', who: 'text',
  owner: 'text', branch: 'text', basedOn: 'text', plugin: 'text', agent: 'text', workspace: 'text',
  // The model an actor's choice named, in the tool's own word, the actor by its name, and which of its choices ran (MODELROLE1c2).
  model: 'text', actor: 'actor', n: 'number', entry: 'text', passed: 'text',
  // The dependency folders a tree's open copied from its checkout, each as code, and one it did not copy (TREEDEPS1).
  copied: 'names', modules: 'text',
  why: 'why',
  cooling: 'cooling',
  window: 'window',
};

/** A cool-off's four reasons (TOOL4d), each its `harness.cooling.why.*` entry. */
const COOLING = new Set(['stated', 'assumed', 'default', 'notBelieved']);

/**
 * Where a rule was set (PROCESS1d-ii), worded as the handed account words a section's source: the default is none set. A source
 * this page does not know shows the line as recorded.
 */
const SET_AT: Readonly<Record<string, string>> = {
  repository: 'work.handed.source.repository',
  workspace: 'work.handed.source.workspace',
  default: 'work.handed.none.not-set',
};

/**
 * The level that set the access setup a landing was made under (ACCESSWORDS1), worded whose; one this page does not know shows as
 * recorded. The trace's landing words it by the same map (ACCESSWORDS1b).
 */
export const ACCESS_AT: Readonly<Record<string, string>> = {
  repository: 'settings.access.setupOf.repository',
  workspace: 'settings.access.setupOf.workspace',
  machine: 'settings.access.setupOf.machine',
};

/** The actors' ids (D205 §2.2), each worded by its `work.actor.name.*` entry, as *Details* says it. */
const ACTOR_IDS = new Set(['build', 'intake', 'opinion', 'helper', 'partner', 'brainstorm', 'ask-daoris', 'chat']);

/**
 * One line of a note as the page shows it:
 * - `said`: Daoris's line, worded by the page in the reader's language;
 * - `recorded`: the record's own English, which the page could not word (a code it does not know, a value its sentence
 *   needs that is absent, a record from before parts), shown as kept and marked *shown as recorded*;
 * - `words`: someone's words, shown as written and never marked: the agent's, the person's, a program's.
 */
export type NoteLine =
  | { kind: 'said'; text: string }
  | { kind: 'recorded'; text: string }
  | { kind: 'words'; text: string; by: string };

/** A record's note: its English, and its parts where its writer wrote them. */
export type NoteRecord = { note?: string | null; parts?: readonly NotePart[] | null };

const text = (value: unknown): string | null =>
  typeof value === 'string' && value.length > 0 ? value : typeof value === 'number' && Number.isFinite(value) ? String(value) : null;

/** A value as its sentence says it, or null where it is absent or not the shape its kind is. */
function said(
  t: Translate, name: string, values: Record<string, unknown>, code: NoteCode, language: string, names?: AccountNames,
): string | null {
  const value = values[name];
  switch (code.accounts?.includes(name) ? 'account' : NOTE_VALUES[name] ?? 'text') {
    case 'account': {
      const id = text(value);
      return id === null ? null : names?.get(id) ?? id;
    }
    case 'accounts': {
      if (!Array.isArray(value) || value.length === 0) return null;
      const ids = value.map(text);
      return ids.every((id): id is string => id !== null) ? list(ids.map((id) => `\`${names?.get(id) ?? id}\``), language) : null;
    }
    case 'ids': {
      if (!Array.isArray(value) || value.length === 0) return null;
      const ids = value.map(text);
      return ids.every((id): id is string => id !== null) ? list(ids.map((id) => `#${id}`), language) : null;
    }
    case 'names': {
      // Plugin-declared server names, each as code, joined the reader's way (CONVNOTE2c).
      if (!Array.isArray(value) || value.length === 0) return null;
      const names = value.map(text);
      return names.every((name): name is string => name !== null) ? list(names.map((name) => `\`${name}\``), language) : null;
    }
    case 'moment': {
      const at = typeof value === 'string' ? Date.parse(value) : Number.NaN;
      return Number.isNaN(at) ? null : moment(value as string, language);
    }
    case 'number':
      return typeof value === 'number' && Number.isFinite(value) ? String(value)
        : typeof value === 'string' && /^-?\d+$/.test(value) ? value : null;
    case 'count':
      return typeof value === 'number' && Number.isInteger(value) ? figure(value, language) : null;
    case 'percent':
      return typeof value === 'number' && Number.isInteger(value) ? `${value}%` : null;
    case 'bgstate': {
      const state = text(value);
      return state === null ? null : Object.hasOwn(BACKGROUND_STATES, state) ? t(BACKGROUND_STATES[state]) : state;
    }
    case 'cooling': {
      const why = text(value);
      return why && COOLING.has(why) ? t(`harness.cooling.why.${why}`) : null;
    }
    case 'setAt': {
      const source = text(value);
      return source && Object.hasOwn(SET_AT, source) ? t(SET_AT[source]) : null;
    }
    case 'accessAt': {
      const level = text(value);
      return level && Object.hasOwn(ACCESS_AT, level) ? t(ACCESS_AT[level]) : null;
    }
    case 'actor': {
      // An actor by its id, said by the name the screen gives it (D205 §2.2); one this page does not know as written.
      const actor = text(value);
      return actor === null ? null : ACTOR_IDS.has(actor) ? t(`work.actor.name.${actor}`) : actor;
    }
    case 'why': {
      const why = text(value);
      if (!why) return null;
      if (code.why === 'cooling') return COOLING.has(why) ? t(`harness.cooling.why.${why}`) : null;
      if (code.why === 'opening') return opening(t, why, values, code, language, names);
      if (code.why === 'resumed') return RESUMED_REASONS[why] ? t(RESUMED_REASONS[why].key) : null;
      if (code.why === 'unreached') return UNREACHED_REASONS[why] ? t(UNREACHED_REASONS[why].key) : null;
      if (code.why === 'handed') return HANDED_REASONS[why] ? t(HANDED_REASONS[why].key) : null;
      if (code.why === 'helper') return HELPER_REASONS[why] ? t(HELPER_REASONS[why].key) : null;
      if (code.why === 'partner') return PARTNER_REASONS[why] ? t(PARTNER_REASONS[why].key) : null;
      if (code.why === 'handover') return HAND_OVER_ASK_REASONS[why] ? t(HAND_OVER_ASK_REASONS[why].key) : null;
      return reasonOf(t, why, {
        from: text(values.from), to: text(values.to), adapter: text(values.adapter), agent: text(values.agent),
      });
    }
    case 'window': {
      const window = typeof value === 'string' && value.length > 0 ? value : null;
      return window === null ? null : windowName(window, t);
    }
    default:
      return text(value);
  }
}

/**
 * The step that chose a conversation's account (UX7d-1), with its own values beside the line's, or null where it is a step
 * this page does not know or a value its entry says is absent, so the line shows as recorded.
 */
function opening(
  t: Translate, why: string, values: Record<string, unknown>, code: NoteCode, language: string, names?: AccountNames,
): string | null {
  const reason = OPENING_REASONS[why];
  if (!reason) return null;
  const options: Record<string, string> = {};
  for (const name of reason.values) {
    const value = said(t, name, values, code, language, names);
    if (value === null) return null;
    options[name] = value;
  }
  return t(reason.key, options);
}

/** A coded part in the reader's language, or null where the page cannot word it (§5). */
function worded(
  t: Translate, part: { code: string; values?: Record<string, unknown> | null }, language: string, names?: AccountNames,
): string | null {
  const code = NOTE_CODES[part.code];
  if (!code) return null;
  const values = part.values ?? {};
  const options: Record<string, string> = {};
  for (const name of code.values) {
    const value = said(t, name, values, code, language, names);
    if (value === null) return null;
    options[name] = value;
  }
  return t(code.key ?? `note.${part.code}`, options);
}

/**
 * A note's lines as the page shows them (D142 points 1, 4, 5): each part in its writer's order, or, for a record with no
 * parts, its note as kept. Nothing is read out of the English: a record from before is shown, not re-read (§6).
 *
 * @param language The reader's language, which a list and a moment are written in.
 * @param names What the person calls each account, by id (NOTENAME1); an id it lacks is said as the record wrote it.
 */
export function noteLines(t: Translate, { note, parts }: NoteRecord, language: string, names?: AccountNames): NoteLine[] {
  if (!parts || parts.length === 0) return note ? [{ kind: 'recorded', text: note }] : [];
  const lines: NoteLine[] = [];
  for (const part of parts) {
    if (typeof part.code === 'string') {
      const line = worded(t, part, language, names);
      if (line !== null) lines.push({ kind: 'said', text: line });
      else if (part.text) lines.push({ kind: 'recorded', text: part.text });
    } else if (typeof part.words === 'string' && part.words.length > 0) {
      // A note from before parts, carried whole, is Daoris's English as an older driver wrote it: shown as a record from
      // before is, marked, since the page could not word it either.
      lines.push(part.by === 'before' || !part.by ? { kind: 'recorded', text: part.words } : { kind: 'words', text: part.words, by: part.by });
    }
  }
  return lines;
}

/**
 * A working session's note parts as the page reads them (WAITSHOWN1): what the session said of an ending it has since gone on
 * from (`ended.*`) and of the words it went on with (`ledger.went-on*`) are history, and read beside its working they say it
 * ended while it works. The ledger keeps them (D137 §2.3); only this reading leaves them out.
 */
export function liveParts<T extends { code?: string }>(parts: readonly T[] | null | undefined): T[] | null | undefined {
  if (!parts) return parts as null | undefined;
  return parts.filter((part) => !part.code || !(part.code.startsWith('ended.') || part.code.startsWith('ledger.went-on')));
}

/** Two lines joined the catalogue's way: English puts a space after a full stop and Chinese does not (platform language §4). */
export function joinLines(t: Translate, lines: readonly string[]): string {
  return lines.reduce((first, second) => (first ? t('work.note.join', { first, second }) : second), '');
}

/**
 * A note's lines as the blocks `Note` draws (the language design §9): Daoris's lines running together as one paragraph,
 * joined the reader's way, until someone's words or a recorded line sets itself apart beneath them.
 */
export function noteBlocks(t: Translate, lines: readonly NoteLine[]): NoteLine[] {
  const blocks: NoteLine[] = [];
  for (const line of lines) {
    const last = blocks.at(-1);
    if (line.kind === 'said' && last?.kind === 'said') last.text = joinLines(t, [last.text, line.text]);
    else blocks.push({ ...line });
  }
  return blocks;
}

/** A note as one run of words, for a row too narrow for its blocks: every line, joined the reader's way. */
export function noteText(t: Translate, record: NoteRecord, language: string, names?: AccountNames): string {
  return joinLines(t, noteLines(t, record, language, names).map((line) => line.text));
}

/** Whether a record has a note to show at all. */
export function hasNote({ note, parts }: NoteRecord): boolean {
  return Boolean(note) || (parts?.length ?? 0) > 0;
}

/** An ideograph, a kana or a full-width form, which sets about twice as wide as a letter of the same size. */
const WIDE = /[⺀-鿿가-힯豈-﫿︰-﹏＀-｠￠-￦]/u;

/**
 * How many lines texts take at about `measure` letters a line (UX6b, design §2.5): each of their own lines one at least,
 * and one more for each measure it runs past, an ideograph counted as two letters. An estimate, since the page lays
 * nothing out before it draws: near enough to fold a verify report in the timeline and leave a sentence whole.
 */
export function linesTaken(texts: readonly string[], measure: number): number {
  let lines = 0;
  for (const text of texts) {
    for (const line of text.split('\n')) {
      let width = 0;
      for (const glyph of line) width += WIDE.test(glyph) ? 2 : 1;
      lines += Math.max(1, Math.ceil(width / measure));
    }
  }
  return lines;
}
