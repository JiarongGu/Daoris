/**
 * A family of reasons a `why` names: the person's words' (`work.say.why.*`, by `reasonOf`), a cool-off's (`harness.cooling.why.*`), or
 * the step that chose a conversation's account (`note.opening.why.*`), or whose words a resumed conversation goes on
 * with (`note.resumed.why.*`), or why the person's words never reached it (`note.words.why.*`).
 */
type Why = 'continue' | 'cooling' | 'opening' | 'resumed' | 'unreached' | 'handed' | 'helper' | 'handover' | 'partner' | 'deps';

/**
 * A code the page words: the values its sentence says, its key where it is not `note.<code>`, the reasons a `why` names, and
 * the values that are accounts on this code though their name is said another way elsewhere (`to`, `from`).
 */
export type NoteCode = { values: readonly string[]; key?: string; why?: Why; accounts?: readonly string[] };

/**
 * Every code a writer declares, as the page words it: the page's half of the twin (`.claude/knowledge/twins.md`). `locales/note.test.ts` parses the driver's `NoteCodes` and the service's `LedgerNoteCodes`
 * and holds this map to them code for code, with the same values, key and family, so a code a writer adds fails until it is
 * mapped here and worded in both catalogues.
 */
export const NOTE_CODES: Readonly<Record<string, NoteCode>> = {
  // A session's end.
  'ended.awaits': { values: ['awaits'] },
  'ended.exit': { values: ['exit'] },
  'ended.turn-failed-taken': { values: [] },
  'ended.turn-failed-open': { values: [] },
  // Its model's provider refused its turn; a failed run, never a park.
  'ended.provider-refused': { values: [] },
  // Its provider kept failing past a transient episode's bound; no strike, held for the person's word.
  'ended.transient': { values: ['count', 'minutes'] },
  // Said once at an episode's first failure, its tries and minutes the schedule's, said as up to.
  'agent.transient-retrying': { values: ['agent', 'count', 'minutes', 'at'] },
  // A conversation's turn whose provider kept failing past every try ends here; the next word is a turn of the same conversation.
  'chat.transient-stopped': { values: ['agent'] },
  // Its agent refused the choice at the open, before any prompt; no strike, planned again.
  'ended.choice-not-offered': { values: ['agent', 'model'] },
  'ended.parked-asked': { values: [] },
  'ended.parked-transcript': { values: [] },
  'ended.parked-short': { values: [] },
  // Its agent cut its turn (cancelled, nobody stopped it), after a refused permission or none: a park that asked nothing.
  'ended.parked-cut': { values: ['call'] },
  'ended.parked-cancelled': { values: [] },
  // It ended its turn on its own helper's ask, and waits on that helper, not on the person.
  'ended.awaits-helper': { values: [] },
  'ended.answered-unfinished': { values: ['awaits', 'exit'] },
  'ended.carried-unfinished': { values: ['exit'] },
  'ended.done': { values: [] },
  // A bound session ended at its `ready`, the result in; worded by its status's hint.
  'ended.submitted': { values: [], key: 'statusHint.Submitted' },
  'ended.declined': { values: [] },
  'ended.stood-down': { values: [] },
  'ended.source-reopened': { values: ['quest'] },
  'ended.resolve-ask': { values: [] },
  // The native door's harness ended its own background work with its turn.
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
  // A run ended at its turn's end because its permission prompts changed, and its held words go on under the new setup.
  'ended.setup-changed': { values: ['from', 'to'] },
  'ended.words-setup': { values: [] },
  'ended.timeout': { values: ['minutes'] },
  // State E's strays ended, the person's end of a quiet run, and the watcher's own lines.
  'ended.gone': { values: [] },
  'ended.quiet': { values: ['minutes'] },
  'ended.quiet-pressed': { values: ['minutes'] },
  'watch.quiet': { values: ['minutes'] },
  'watch.quiet-call': { values: ['minutes', 'call'] },
  'watch.quiet-work': { values: ['minutes', 'works'] },
  'watch.left': { values: [] },
  // A move a live Daoris keeps and has not moved for `minutes`, said once on its line.
  'move.quiet': { values: ['minutes'] },
  'ended.stopped': { values: [] },
  // The steward's sibling of the person's stop, its own record.
  'ended.stopped-steward': { values: [] },
  'ended.lost-claim': { values: [] },
  'ended.lost-claim-reopened': { values: [] },
  'ended.handed-over': { values: ['agent'] },
  'ended.driver-closed': { values: [] },
  // An account's line. `account.cooling` is the shape notes from before carry; since, a limit's line names whose
  // accounts and the window its reset named, or has a line of its own where it named none or a model family's limit.
  'account.cooling': { values: ['until', 'why'], why: 'cooling' },
  'account.cooling-window': { values: ['until', 'why', 'owner', 'window'], why: 'cooling' },
  'account.cooling-no-window': { values: ['until', 'why', 'owner'], why: 'cooling' },
  'account.cooling-model': { values: ['until', 'why', 'owner', 'model'], why: 'cooling' },
  'account.refused': { values: ['owner'] },
  'account.refused-own': { values: ['owner'] },
  'account.signed-out': { values: ['owner'] },
  'account.signed-out-own': { values: ['owner'] },
  // The account refused the model its actor asked; the account's, never a strike.
  'account.refused-model': { values: ['agent', 'model'] },
  // Why the account a resume asked for could not carry the words, after a line whose reason is `account`.
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
  // A tree its checkout's dependencies were copied into, and why a project's were not.
  'started.tree-copied': { values: ['branch', 'basedOn', 'copied'] },
  'started.uncopied-manager': { values: ['modules'] },
  'started.uncopied-moved': { values: ['modules'] },
  'started.uncopied-uninstalled': { values: ['modules'] },
  'started.uncopied-workspace': { values: ['modules', 'workspace'] },
  'started.uncopied-link': { values: ['modules'] },
  'started.uncopied-unignored': { values: ['modules'] },
  'started.uncopied-failed': { values: ['modules'] },
  // What the files stage did with each dependency and the tree's other files, after the record opens.
  'started.deps-kept': { values: ['path'] },
  'started.deps-placed': { values: ['path', 'install'] },
  'started.deps-installed': { values: ['path', 'install'] },
  'started.deps-unstored': { values: ['path', 'why'], why: 'deps' },
  'started.deps-unplaced': { values: ['path', 'why'], why: 'deps' },
  'started.deps-left': { values: ['path', 'why'], why: 'deps' },
  // An install that changed git's own hooks or configuration for the repository, named.
  'started.deps-common': { values: ['path', 'changed'] },
  'started.setup-failed': { values: ['path', 'command', 'why'] },
  'started.files-waiting': { values: ['count', 'repository'] },
  'started.files-unread': { values: ['repository', 'why'] },
  'started.files-stopped': { values: [] },
  'started.files-written-back': { values: ['written'] },
  'started.files-failed': { values: ['why'] },
  // The machine-local files a repository's gates read, copied into its tree read-only, and why a declared one is not.
  'started.local-copied': { values: ['inputs'] },
  'started.local-missing': { values: ['input'] },
  'started.local-refused': { values: ['input'] },
  'started.local-failed': { values: ['input'] },
  'started.local-held': { values: ['input'] },
  'started.local-denied': { values: ['input'] },
  // The paths linked into a tree to one place every tree uses, why one is not, and a share whose link the line replaced.
  'started.tree-shared': { values: ['shared'] },
  'started.share-declined': { values: ['path', 'why'], why: 'deps' },
  'tree.share-lost': { values: ['path'] },
  'started.tree-base': { values: ['bases', 'runners'] },
  'started.tree-base-unguarded': { values: ['bases'] },
  'tree.base-trimmed': { values: ['path', 'project', 'count'] },
  'tree.base-host': { values: ['path'] },
  'tree.remade': { values: ['branch'] },
  'tree.gone': { values: ['branch'] },
  'tree.fresh': { values: ['branch'] },
  'tree.unmade': { values: [] },
  'started.joined': { values: ['session'] },
  'started.resumes-answered': { values: ['quest', 'answered'] },
  'started.in-asking-tree': { values: ['session'] },
  'started.words-open': { values: ['quest', 'session'] },
  'started.words-taken': { values: ['quest', 'session'] },
  'started.answer': { values: ['quest', 'session'] },
  'started.released': { values: ['quest', 'session'] },
  'started.released-steward': { values: ['quest', 'session'] },
  'started.cut-off': { values: ['quest', 'session'] },
  // An open quest carried on in the tree whose session committed work and failed before it took it.
  'started.carried-work': { values: ['quest', 'session'] },
  // A stand-down carried on once the person brought its lost take forward.
  'started.brought-forward': { values: ['quest', 'session'] },
  // A quest handed to another agent: where it came from, and the account it named passed.
  'started.handed-over': { values: ['quest', 'session', 'agent'] },
  'started.handed-walked': { values: [] },
  'started.other-account': { values: [] },
  'started.same-tree': { values: [] },
  'started.fell-back': { values: ['why'], why: 'continue' },
  'working.resumes-answer': { values: [] },
  'working.goes-on': { values: [] },
  // A park going on with none of the owner's words, its authors named, never "your answer".
  'working.resumes-others': { values: ['authors'] },
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
  'stopped.paused-quest-steward': { values: ['quest'] },
  'stopped.abandoned-ask': { values: ['ask'] },
  'stopped.abandoned-quest': { values: ['quest'] },
  'stopped.checkpoint-finished': { values: [] },
  'stopped.checkpoint-stopped': { values: [] },
  'stopped.checkpoint-finished-steward': { values: [] },
  'stopped.checkpoint-stopped-steward': { values: [] },
  'stopped.checkpoint-moved': { values: [] },
  'stopped.orphan': { values: [] },
  'chat.closed': { values: [] },
  'chat.ended-by-person': { values: [] },
  'chat.ended': { values: [] },
  'chat.cancelled': { values: [] },
  'chat.not-kept': { values: [] },
  'chat.restarted': { values: [] },
  'chat.cut': { values: [] },
  'update.closed': { values: ['build'] },
  // Ask Daoris's start, where the machine's reading for its room did not answer in time.
  'help.room-as-written': { values: ['seconds'] },
  'help.room-unread': { values: ['seconds'] },
  // Ask Daoris started at once on the facts the judges keep, some of the machine not read yet.
  'help.room-not-read-yet': { values: [] },
  // A landing at the quest's done, under a rule that accepts automatically.
  'landing.accepted': { values: ['branch'] },
  'landing.plugin-unready': { values: ['branch', 'plugin'] },
  'landing.plugin-failed': { values: ['branch', 'plugin'] },
  // Its push held back after the branch is made, handed on at the look after the release.
  'landing.push-held': { values: ['branch', 'plugin'] },
  // Held back there by its landing's gate, the gate's sentence beneath it; a note from before it says `landing.push-held`.
  'landing.push-gated': { values: ['branch', 'plugin'] },
  'landing.nothing': { values: [] },
  'landing.held': { values: [] },
  'landing.uncommitted': { values: ['paths'] },
  'landing.exists': { values: ['branch'] },
  'landing.refused': { values: [] },
  // Its replay onto the line conflicts and its session is told to rebase in its own tree, the refusal beneath it.
  'landing.rebase': { values: [] },
  'landing.root-dirty': { values: [] },
  'landing.line-holds': { values: [] },
  'landing.queued': { values: [] },
  // The person's Accept carried a session on: its tree's uncommitted work, or the replay's conflict beneath it, and
  // the yes that stays on it; carried on twice and still not clean it waits for review; and the landing that yes makes once it is clean.
  'landing.carried-dirty': { values: ['paths'] },
  'landing.carried-conflict': { values: [] },
  'landing.carry-bound': { values: [] },
  'landing.accepted-carried': { values: ['branch'] },
  // Held by the review's gate, the gate's sentence beneath it; a note from before it says `landing.refused`.
  'landing.unreviewed': { values: [] },
  // Held by the second opinion's gate, the gate's sentence beneath it; a note from before it says `landing.refused`.
  'landing.opinion': { values: [] },
  'landing.no-tree': { values: [] },
  'landing.not-done': { values: [] },
  // The access setup lands work by its rule, and the rule merges, so Accept stays; `access` is the level that set it.
  'landing.merges': { values: ['access'] },
  // The service's own lines.
  'ledger.answered': { values: [] },
  'ledger.parked': { values: [] },
  'ledger.went-on': { values: ['at'] },
  // Another agent's findings alone, or beside the person's words.
  'ledger.went-on-findings': { values: ['at'] },
  'ledger.went-on-both': { values: ['at'] },
  // A helper's answer alone, or beside the person's words.
  'ledger.went-on-helper': { values: ['at'] },
  'ledger.went-on-helper-both': { values: ['at'] },
  // Daoris's own words alone: a reopen told, a step's go-on, never "your words".
  'ledger.went-on-daoris': { values: ['at'] },
  // The supervisor's answer alone: on the owner's behalf, never "your words".
  'ledger.went-on-supervisor': { values: ['at'] },
  // A quest's note: the person marked it done, their words a part beneath it.
  'quest.person-done': { values: [] },
  // A conversation's first line: the account it opened on and the step that chose it, then what each account said.
  'opening.opened': { values: ['account', 'why'], why: 'opening' },
  'opening.carried-on': { values: ['session', 'account', 'why'], why: 'opening' },
  'opening.rest': { values: ['why'], why: 'opening' },
  'opening.turn-refused': { values: ['turn'] },
  'opening.turn-context': { values: ['turn', 'tokens'] },
  'opening.unsaid': { values: [] },
  'opening.said-at': { values: ['account', 'seen'] },
  'opening.said-nothing': { values: ['account'] },
  'opening.said-reached': { values: ['window'] },
  // The agent's pace warning, never near; a line written before says `opening.said-near`, worded as before.
  'opening.said-near': { values: ['window'] },
  'opening.said-warned': { values: ['window'] },
  'opening.said-credits': { values: [] },
  'opening.said-used': { values: ['used', 'window'] },
  // The scope's one number, only in a line written before.
  'opening.said-near-at': { values: ['near'] },
  'opening.said-over': { values: ['window', 'curve'] },
  // A window's use projected from its reading and Daoris's own turns since, never near or over.
  'opening.said-projected': { values: ['projected', 'window'] },
  'opening.said-clear': { values: [] },
  // A later choice of an actor's ran, each choice passed said in the walk's own words.
  'opening.actor': { values: ['actor', 'n', 'entry', 'passed'] },
  // The role's policy chose the choice that ran; the sentence is the judge's, in English, as `passed` is the walk's.
  'opening.chosen': { values: ['actor', 'said'] },
  // The part moved here from another choice of the role; the choice it left is said as a choice, the why is the judge's, in English.
  'opening.moved-choice': { values: ['from', 'said'] },
  // A quest marked design ran on the order of the role taking design, Build's while none does; the role's name as written.
  'opening.design': { values: ['role'] },
  'opening.exact': { values: ['role'] },
  // The account a start is refused for its sign-in and moved off, as named.
  'opening.moved-off': { values: ['account'] },
  // The agent and the model a start is refused and moved off; the account is not named.
  'opening.moved-off-model': { values: ['agent', 'model'] },
  // A chat run's binding, said once in the conversation that grew its tree: the named workflow and
  // the repository or workspace that chose it (`owner`, set as code), the one bound with why it cannot land, or a bind the home did not keep.
  'chat.workflow': { values: ['workflow', 'version', 'owner'] },
  'chat.workflow-unread': { values: ['workflow', 'owner', 'problem'] },
  'chat.workflow-failed': { values: ['problem'] },
  // A resumed conversation's first line: the door and whose words, a version that moved riding inside it.
  'resumed.opening': { values: ['door', 'why'], why: 'resumed' },
  'resumed.moved': { values: ['now', 'then'] },
  'resumed.native': { values: ['door'] },
  // The native door going on with Daoris's own words alone: never "what you said".
  'resumed.native-daoris': { values: ['door'] },
  // A permission the rules refused: the call's title rides between the two as the agent's words.
  'refused.lead': { values: [] },
  'refused.governs': { values: [] },
  // A permission held for the person under asking: the call's title rides after the lead as the agent's words; a
  // refusal leads with `refused.lead`, and its close says who or what refused it, the bound's minutes a fact.
  'asking.held': { values: [] },
  'asking.allowed': { values: [] },
  'asking.denied': { values: [] },
  'asking.cancelled': { values: [] },
  'asking.ended': { values: [] },
  'asking.bound': { values: ['minutes'] },
  // A press's note and a chat's settings: a landing's and a preface's sentence follow their lead as a program's words.
  'landing.person-accepted': { values: [] },
  'landing.person-handed': { values: [] },
  // A part the person left out of its entry's Accept: the branch its line of work landed on, or the line.
  'landing.left-out': { values: ['branch'] },
  // A member a combined Accept brought into its head's landing: where its work went and the operation.
  'landing.brought-into': { values: ['branch', 'operation'] },
  'landing.brought-into-line': { values: ['branch', 'operation'] },
  'chat.option-set': { values: ['option', 'chosen'] },
  'chat.told-where': { values: [] },
  // Words that did not reach an agent or were not taken: the agent's or the service's words follow a lead.
  'words.refused': { values: [] },
  'words.unreached': { values: ['count', 'why'], why: 'unreached' },
  'words.not-taken': { values: [] },
  // The chat runner's and the modules' sites: a program's or the service's words follow each lead but `not-sent`.
  'words.not-reached': { values: [] },
  'words.not-sent': { values: [] },
  'words.start-not-taken': { values: ['session'] },
  'words.not-kept': { values: [] },
  // A server that drives Daoris's own browser, withheld: why follows as a code of the host's or an exception's words.
  'browser.withheld': { values: ['servers'] },
  'browser.no-host': { values: [] },
  'browser.none-up': { values: [] },
  // The line a chat's agent is told of the browser it is handed, and of the place it works in: each follows as a program's words.
  'chat.told-browser': { values: [] },
  'chat.told-place': { values: [] },
  // The repository's process a chat in a tree of its own is told: its commit rule's mode and where it is set,
  // the words following as a program's.
  'chat.told-process': { values: ['mode', 'source'] },
  // The standing block a conversation's agent is told with its first message: the block follows as a program's words.
  'chat.told-block': { values: [] },
  // The workflow draft open in the editor a chat's agent is told: what it is handed follows as a program's words.
  'chat.told-draft': { values: [] },
  // A conversation handed to another agent: where the old one went, and where the new one came from and why.
  'chat.handed-over': { values: ['agent', 'session'] },
  'chat.taken-over': { values: ['agent', 'session', 'why'], why: 'handed' },
  'chat.briefing-unsaid': { values: [] },
  // A session's move to a new part: on the part it left and on the new part, each with why by the hand-over's reasons.
  'moved.on': { values: ['session', 'agent', 'account', 'why', 'tokens', 'count'], why: 'handed' },
  'moved.from': { values: ['session', 'agent', 'account', 'why', 'tokens', 'count'], why: 'handed' },
  // A move onto another account of the same agent that carried its own conversation, whole or for its agent to compact, or could not.
  'moved.carried-on': { values: ['session', 'agent', 'account', 'why', 'tokens'], why: 'handed' },
  'moved.carried-from': { values: ['session', 'agent', 'account', 'why', 'tokens'], why: 'handed' },
  'moved.compacted-from': { values: ['session', 'agent', 'account', 'why', 'tokens', 'bound'], why: 'handed' },
  'moved.not-carried': { values: ['reason'] },
  // A conversation moved to another account of its agent by the usage curve: where it went and where it came from,
  // each account by the name the person gave it, and how far over its curve the account it left is.
  'chat.moved': { values: ['to', 'from', 'window', 'used', 'curve'], accounts: ['to', 'from'] },
  'chat.moved-from': { values: ['from', 'window', 'used', 'curve'], accounts: ['from'] },
  // A conversation whose account's limit refused its turn, whose move to the next account did not open.
  'chat.limit-not-moved': { values: [] },
  // A conversation its account's limit cut off with no account to go on on: it waits, and the person's *Stop waiting*.
  'chat.limit-waits': { values: ['owner', 'until', 'why'], why: 'cooling' },
  'chat.limit-stopped-waiting': { values: [] },
  // Why a conversation whose agent has no account left does not go on with another: the person's pick, or no other choice.
  'chat.limit-picked': { values: ['owner'] },
  'chat.limit-no-other': { values: ['owner'] },
  // What a part left running when the next part starts in its tree: ended and waited for, or still there after the wait.
  'chat.left-ended': { values: ['count'] },
  'chat.left-running': { values: ['count', 'seconds'] },
  // The limit's wake: its own conversation goes on, or cannot be resumed and goes on briefed.
  'limit.resumed': { values: ['until'] },
  'limit.resume-refused': { values: ['why'], why: 'continue' },
  // A helper on another agent: on the asker, asked, answered or refused, with the agent and why; on the helper,
  // whose session it helps.
  'helper.asked': { values: ['agent', 'session'] },
  'helper.answered': { values: ['agent', 'session'] },
  'helper.answered-last-words': { values: ['agent', 'session'] },
  'helper.refused': { values: ['why'], why: 'helper' },
  'helper.refused-by': { values: ['agent', 'why'], why: 'helper' },
  'helper.helping': { values: ['session'] },
  // A helper an account's limit cut off, which the session no longer waits on, with when its account is ready again.
  'helper.limited': { values: ['agent', 'until'] },
  'helper.limited-no-reset': { values: ['agent'] },
  // The person's own ask of a helper, kept on the session whose next turn its answer is.
  'helper.person-asked': { values: [] },
  // A partner's piece: on the lead its course, on the partner whose session it works for. Notes, never words.
  'partner.asked': { values: ['agent'] },
  'partner.started': { values: ['agent', 'session'] },
  'partner.answered': { values: ['agent', 'session'] },
  'partner.refused': { values: ['why'], why: 'partner' },
  'partner.stopped': { values: ['agent'] },
  'partner.out-of-time': { values: ['agent'] },
  'partner.limited': { values: ['agent'] },
  // A pass's provider kept failing past its tries or its minutes: the agent and the tries made, then the provider's words.
  'partner.transient': { values: ['agent'] },
  'helper.transient': { values: ['agent', 'count'] },
  'opinion.transient': { values: ['agent', 'count'] },
  'intake.transient': { values: ['agent', 'count'] },
  'voice.transient': { values: ['agent', 'count'] },
  'partner.uncertain': { values: ['why'], why: 'partner' },
  'partner.beyond': { values: ['count'] },
  'partner.kept': { values: ['why'], why: 'partner' },
  'partner.working': { values: ['session'] },
  // The answers-alone bound: a third answer in a row with nobody between held for the person, by the agent that
  // answered; their *Go on with this answer*, which counts as their word; their *Not now*.
  'answers.held': { values: ['agent', 'n'] },
  'answers.passed': { values: [] },
  'answers.kept': { values: [] },
  // A rule that asks the supervisor: the supervisor let the answer go on, or none is started for the place and it waits for the person.
  'answers.supervised': { values: [] },
  // The same on a quest its failures parked or its failed choice asked about: the supervisor made the quest's own *Try again*.
  'supervisor.retried': { values: [] },
  'supervisor.moved': { values: [] },
  'supervisor.waited': { values: [] },
  // A rule of the person's acted on the line: the count and what it stood at, the rule's number, its place and its act.
  'supervisor.rule': { values: ['counted', 'n', 'bound', 'place', 'do'] },
  'supervisor.unreferred': { values: [] },
  // An agent's ask to hand its session over: waiting on the person with the agent it would go on with, going with it at
  // the turn's end, or refused and why.
  'handover.asked': { values: ['agent'] },
  'handover.taking': { values: ['agent'] },
  'handover.refused': { values: ['why'], why: 'handover' },
  // The person's own ask names the path that made it; a hand-over let go that could not be taken is not taken, never refused.
  'handover.asked-terminal': { values: ['agent'] },
  'handover.asked-window': { values: ['agent'] },
  'handover.asked-steward': { values: ['agent'] },
  'handover.not-taken': { values: ['agent', 'why'], why: 'handover' },
  'handover.not-taken-ends': { values: ['agent', 'why'], why: 'handover' },
  // A session's own background work: the work's names as code, a state as the harness said it; a program's words
  // follow `unread`.
  'background.unread': { values: [] },
  'background.waits': { values: ['works'] },
  'background.work-ended': { values: ['work', 'state'] },
  'background.goes-on': { values: [] },
  // The quest read again no longer waits on the work, so the session ends and the work still running ends with it.
  'background.wait-over': { values: ['works'] },
  // The quest no longer waits, so the session ends rather than wait for a turn its agent's own wakeup starts, or is about to.
  'background.turn-left': { values: [] },
  'background.quest-closed': { values: [] },
  // A done quest's session carried on for the uncommitted work in its tree, and the bound that ends it as before.
  'background.carried-on': { values: ['paths'] },
  'background.carry-bound': { values: [] },
  // A conversation's door that failed: a program's words follow each lead but the resume's; a refused resume is
  // its reason by the continue family, the agent's words after it where it said any, then an answer carried on.
  'door.not-open': { values: [] },
  'door.turn-not-taken': { values: [] },
  'door.failed': { values: [] },
  'door.resume-refused': { values: ['why'], why: 'continue' },
  'door.answer-carried-on': { values: [] },
  // A review set-up's record: the environment's name and repository, the commit's short id as facts; the service's
  // message and the served sentence are a program's words after `posted` and `not-posted`.
  'review.no-rule': { values: ['named', 'to'] },
  'review.no-commit': { values: [] },
  'review.posted': { values: ['commit', 'named'] },
  'review.not-posted': { values: [] },
  'review.posts-next': { values: [] },
  // A second opinion's answers as the working session's turn ended: whose findings and how many, then each count.
  'opinion.answers': { values: ['who', 'count'] },
  'opinion.fixed': { values: ['count'] },
  'opinion.unproven': { values: ['count'] },
  'opinion.rejected': { values: ['count'] },
  'opinion.unresolved': { values: ['count'] },
  'opinion.unanswered': { values: ['count'] },
  'opinion.disputed': { values: ['count'] },
  // How the reviewer's own session ended; a failed turn's words follow as the program's.
  'opinion.reviewer-failed': { values: ['opinion'] },
  'opinion.reviewer-read': { values: ['opinion'] },
  'opinion.reviewer-exited': { values: ['opinion', 'exit'] },
  // A conversation's words waiting for its own account's reset: the account by its owner and its id, why by a
  // cool-off's reason; the tool's own sign-in and its refresh; each door out, the session its command names a value.
  'waits.account': { values: ['owner', 'account', 'until', 'why'], why: 'cooling' },
  'waits.own': { values: ['owner', 'until', 'why'], why: 'cooling' },
  'waits.read-again': { values: [] },
  'waits.door-new-session': { values: ['session'] },
  'waits.door-chat': { values: ['session'] },
  'waits.door-help': { values: [] },
  // A conversation's words held for another reason: a reason of Daoris's own is one line, its repository or
  // conversation a value; a program's message is words between `held.lead` (or `config-unread`) and `held.tail`.
  'held.no-checkout': { values: ['repository'] },
  'held.replaying': { values: ['repository'] },
  // What else holds the repository alone while a start waits for it.
  'held.tidying': { values: ['repository'] },
  'held.promoting': { values: ['repository'] },
  'held.writing': { values: ['repository'] },
  'held.reserving': { values: ['repository'] },
  'held.reserved': { values: ['run', 'tree', 'repository', 'until'] },
  'held.answering': { values: ['session'] },
  'held.config-unread': { values: [] },
  'held.lead': { values: [] },
  'held.tail': { values: [] },
  // The agent its words would go on with is no longer one this machine has; the hand-over is the way on.
  'held.agent-gone': { values: ['agent', 'session'] },
  // The roster's refusal as a held note's reason: an account by its owner and its id, the sign-in's profile the
  // id as a shell spells it, the tool's own sign-in's refresh `waits.read-again`.
  'held.cooling-account': { values: ['owner', 'account', 'until', 'why'], why: 'cooling' },
  'held.cooling-own': { values: ['owner', 'until', 'why'], why: 'cooling' },
  'held.cooling-model': { values: ['owner', 'account', 'model', 'until', 'why'], why: 'cooling' },
  'held.cooling-model-own': { values: ['owner', 'model', 'until', 'why'], why: 'cooling' },
  'held.limit-waits': { values: ['owner', 'account', 'until'], why: 'cooling' },
  'held.limit-waits-own': { values: ['owner', 'until'], why: 'cooling' },
  // The same wait where some accounts need the person's sign-in, named apart from those cooling.
  'held.limit-waits-sign-in': { values: ['owner', 'account', 'until', 'accounts'], why: 'cooling' },
  'held.signed-out': { values: ['owner', 'account', 'profile'] },
  'held.key-missing': { values: ['owner', 'account', 'profile'] },
  // No Daoris account of the agent's is signed in, and its own home is never a start's.
  'held.no-account': { values: ['owner'] },
  'held.no-key': { values: ['owner'] },
  'held.not-installed': { values: ['agent'] },
  'held.pin-not-installed': { values: ['agent', 'version'] },
  // The roster's other refusals: a wait over a list is its lead, a clause for each account, the first reset and
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
  'held.key-sooner': { values: ['logins'] },
  'held.key': { values: ['logins'] },
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
