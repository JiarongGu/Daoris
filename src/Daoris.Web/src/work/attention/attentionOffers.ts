import type { Attention } from './AttentionRow';
import { CLOSE_SENDER, endOffersStop, type HeldAttempt, heldWords } from '../head/milestone';
import { opinionState } from '../gates/opinion';
import type { GitHeld, QuietWait } from './attentionPushed';
import { isTaskAct, type TaskAct } from '../../workflow/task/TaskPress';

/** An act a row offers, named as the row's press. */
export type AttentionActId =
  | 'publish' | 'choose' | 'retry' | 'approve' | 'refuse' | 'trust' | 'accept-departure' | 'accept-rule' | 'decline-rule'
  | 'sign-in' | 'sign-in-new' | 'read' | 'reviewed' | 'not-yet' | 'show-again' | 'opinion-again' | 'opinion-anyway'
  | 'let-ask' | 'not-now'
  // The person's yes or no to an agent's ask to hand its session over: *Hand over to {agent}*, *Not now*.
  | 'hand-over' | 'keep-agent'
  // The person's answer to a tool call held for them, one per choice its agent offered.
  | 'allow-once' | 'refuse-once' | 'refuse-always'
  // The person's word on a third answer alone held for them: *Go on with this answer*, *Not now*.
  | 'go-on-answer' | 'keep-answer'
  // The person's word on a plugin step's attempt held for them.
  | 'happened' | 'did-not' | 'stop-step'
  // The person's word on Daoris's own close held for them: its recoveries, and the rest of the work stopped apart.
  | 'record-close' | 'resend-close' | 'stop-close'
  // The folder a node record that does not read is kept in, opened for the person to mend it.
  | 'open-records'
  // The person's new attempt on a plugin step's failure kept for them.
  | 'again-step'
  // A held verification's *Run again* and *Not now*: a check's words, never a step's.
  | 'again-check' | 'stop-check'
  // A failed verification's *Ask for a fix…*: one ask for its failed gate, with the gate's log.
  | 'ask-fix-check'
  // The stop of a close found closed only in part, its node waiting again.
  | 'stop-partial'
  // A git remote held because it could not authenticate, tried again on the person's press.
  | 'git-try-again'
  // A remote nothing serves, led to Settings → Git to set an identity up or to keep the person's own sign-ins.
  | 'git-set-up' | 'git-keep-own'
  // What mends a held remote by its class: Settings → Git on its host (the key to copy, a sign-in again, a host to
  // compare and trust, the person's own sign-ins), the host's key page, the repository's git remote row, and the identity marked read only.
  | 'git-copy-key' | 'git-key-page' | 'git-sign-in' | 'git-open' | 'git-compare' | 'git-choose' | 'git-use-own' | 'git-read-only'
  // A git go-ahead allowed or refused.
  | 'git-allow' | 'git-refuse'
  // A quiet session's presses: *End and carry on*, *Hand over…*, *Stop…*, *Leave it running*.
  | 'quiet-carry-on' | 'quiet-hand-over' | 'quiet-stop' | 'quiet-leave'
  // The run's own skip of its `work.requests` node, with the person's words.
  | 'pass-over'
  // A run's own declined request, asked again or gone on without, each the run's node word.
  | 'ask-again' | 'go-on'
  // A person's task's presses, each the run's node word with the person's note.
  | 'task-done' | 'task-approve' | 'task-reject'
  // The person's word on an end the service's sweep wrote, which no session's page holds: approved or declined in its row.
  | 'approve-end' | 'decline-end';

/** The run's node word a person's task's press is (`task-done` is `done`); null for any other act. */
export const taskActOf = (act: AttentionActId): TaskAct | null => (act.startsWith('task-') ? act.slice('task-'.length) as TaskAct : null);

/** The act each word offered on a held attempt is pressed by: a close's stop is not a step's. */
export function heldActs(held: HeldAttempt): AttentionActId[] {
  const close = held.plugin === CLOSE_SENDER;
  const check = held.plugin === 'check';
  return heldWords(held).map((word): AttentionActId => {
    switch (word) {
      case 'record': return 'record-close';
      case 'resend': return 'resend-close';
      case 'again': return check ? 'again-check' : 'again-step';
      case 'ask-fix': return 'ask-fix-check';
      case 'stop': return close ? 'stop-close' : check ? 'stop-check' : 'stop-step';
      default: return word;
    }
  });
}

/**
 * An act a row offers, and for an account's act which account it is for: a row may sign two in; for a held tool call's,
 * the agent's option it answers with.
 */
export type AttentionOffer = { act: AttentionActId; account?: string; option?: string };

/** The act each kind of choice the person may make on a held tool call is pressed by; any other kind is no act here. */
const CHOICE_ACTS: Readonly<Record<string, AttentionActId>> = {
  allow_once: 'allow-once',
  reject_once: 'refuse-once',
  reject_always: 'refuse-always',
};

/**
 * What a held tool call's row offers: a press per choice its agent offered, in the agent's order, each
 * answering with that choice's id. A kind the page cannot name is left to the session's page, which says it by the agent's name; and
 * where no allow is offered, none is.
 */
function permissionOffers(item: Attention): AttentionOffer[] {
  return (item.permission?.choices ?? [])
    .filter((choice) => CHOICE_ACTS[choice.kind] !== undefined)
    .map((choice) => ({ act: CHOICE_ACTS[choice.kind]!, option: choice.id }));
}

/**
 * The acts that ask once under the row before they do anything: a go-ahead's yes or no, which every
 * session on its ask is handed; a folder's trust and a widening of the rules, which widen what Daoris may do; and a choice of
 * receiver, which is a choice before it is a press.
 */
export const ASKS_ONCE: ReadonlySet<AttentionActId> = new Set([
  'choose', 'approve', 'refuse', 'trust', 'accept-rule',
  // A *not yet* asks for the person's words, which are what the set-up step's session acts on.
  'not-yet',
  // *Go on anyway…* says what stays unsettled and takes the person's words.
  'opinion-anyway',
  // Ending a quiet session asks once, saying what it does and where the person's waiting words go.
  'quiet-carry-on', 'quiet-stop',
  // Passing a declined request over goes on without it, and keeps the person's words with the skip.
  'pass-over',
  // Asking again publishes a quest anew, and going on without it keeps the person's words.
  'ask-again', 'go-on',
  // A task's press sends the run on its route, with a note the person may give.
  'task-done', 'task-approve', 'task-reject',
]);

/** How many acts an account's row offers beside its door: three controls a row at most. */
const ACCOUNT_ACTS = 2;

/**
 * What an account's row offers, each for one account: *Sign in* to each account the start passed
 * signed out, in the order it names them, and *Read* for one it names that no read answered (a reading only on the
 * press). No *Let … run …*: every account of Daoris's serves every workspace. A
 * signed-out account's own row signs it in. Never the tool's own sign-in, which is the person's, nor where Daoris runs no
 * sign-in for the agent.
 * Where no Daoris account is signed in, *Sign in for Daoris* leads: it names no account, and runs
 * the add flow's sign-in in place. Two at most: the rest are on the agent's page, the row's door, and its line says them.
 */
function accountOffers(item: Attention): AttentionOffer[] {
  const facts = item.account;
  if (!facts) return [];
  const signIns: AttentionOffer[] = facts.signsIn
    ? [
      ...(facts.noAccount ? [{ act: 'sign-in-new' } as const] : []),
      ...facts.named.filter((one) => one.state === 'out' && one.id).map((one) => ({ act: 'sign-in' as const, account: one.id! })),
    ]
    : [];
  if (item.kind === 'signed-out') return signIns.slice(0, 1);
  return [
    ...signIns,
    ...facts.named.filter((one) => one.state === 'unknown' && one.id && !one.read).map((one) => ({ act: 'read' as const, account: one.id! })),
  ].slice(0, ACCOUNT_ACTS);
}

/**
 * What a row offers, in the order it shows them: an act only where one press is safe and the row says
 * what it does, and nothing where the answer needs reading first (a park's question, an intake's, a review), whose door is
 * the row's one control. **One rule per kind**, read by every row and the tests, so two rows of a kind cannot differ, and
 * each mirrors the record's own: a quest's page offers *Try again* on the planner's park and *Accept the departure* on a
 * held done; an ask's page publishes to what its declarations proposed and to any receiver its workspace can ask; an
 * agent's page signs an account in and reads it on its row.
 */
export function attentionOffers(item: Attention): AttentionOffer[] {
  const plain = (...acts: AttentionActId[]) => acts.map((act): AttentionOffer => ({ act }));
  switch (item.kind) {
    case 'proposal': return (item.publishTo?.length ?? 0) > 0 ? plain('publish', 'choose') : plain('choose');
    case 'parked-quest': return plain('retry');
    // The person's own *Try again*, the parked quest's press.
    case 'steward-stopped': return plain('retry');
    // *Try again* is the person's yes to one move (the retry door approves it, once).
    case 'policy-hold': return plain('retry');
    // The run's existing *Pass over…*, offered where the node it is said on is named.
    case 'request-declined': return !item.declined ? [] : item.declined.request ? plain('ask-again', 'go-on') : plain('pass-over');
    // The presses the run's task waits for, as the driver names them.
    case 'task': return plain(...(item.task?.acts ?? []).filter(isTaskAct).map((act): AttentionActId => `task-${act}`));
    // Nothing on a stale one's ask could use a yes but whatever starts there next, which no session asked it for, so
    // its refusal is the one answer offered.
    case 'go-ahead': return item.stale ? plain('refuse') : plain('approve', 'refuse');
    // A hold on the person's own sign-in's file offers no grant: Daoris writes nothing there.
    case 'trust': return item.trust && !item.trust.own ? plain('trust') : [];
    case 'departure': return plain('accept-departure');
    // A set-up shown and waiting: the strip's chip's and the step's page's own presses, *Show it
    // again* only for a local set-up, which Daoris serves; *Skip…* is the step's page's, where the work is read.
    case 'set-up': return item.setUp ? plain('reviewed', 'not-yet', ...(item.local ? ['show-again' as const] : [])) : [];
    case 'rule': return plain('accept-rule', 'decline-rule');
    // An agent's ask for a helper: its session's page's own yes and no, each one press, as they are there.
    case 'helper': return plain('let-ask', 'not-now');
    // An agent's ask to hand its session over: its session's foot's own yes and no, each one press, where the look named
    // the agent it goes to.
    case 'hand-over': return item.agent ? plain('hand-over', 'keep-agent') : [];
    // A tool call held for the person: its session's card's own presses, one each, as `PermissionAsks` says why.
    case 'permission': return permissionOffers(item);
    // A third answer alone held for the person: its session's page's own two presses, each one press.
    case 'answer': return item.answer ? plain('go-on-answer', 'keep-answer') : [];
    // A second opinion that waits on the person: *Go on anyway…* only where no press of theirs is coming (the
    // work lands by itself, or none could be had and the rule requires one), and a pass asked again; the rest are its session's.
    case 'opinion': return opinionOffers(item);
    // A plugin step's attempt held for the person: their three words, until one is recorded and the driver's next
    // look applies it; a held close its own, as the driver offers them. A unit only the clean-up's press settles has
    // that list, on its workspace's Branches, for its door, and no act of its own here.
    // A node record that does not read: its folder opened, where the person mends it; nothing else of Daoris's moves it.
    // A close found closed only in part offers its node's stop alone, while it is offered and no word of theirs waits.
    case 'work': return item.unreadable ? plain('open-records')
      : item.held && !item.held.resolution ? plain(...heldActs(item.held))
        : item.ended && endOffersStop(item.ended) ? plain('stop-partial') : [];
    case 'account-wait':
    case 'signed-out': return accountOffers(item);
    // A held git remote: *Try again*, one press, which lifts its holds and connects nothing itself.
    // A remote nothing serves once the home has moved is set up in Settings → Git, or kept on the person's own sign-ins
    // there, confirmed in place; *Try again* would change nothing until one of them is done. Every other class offers what mends it,
    // then *Try again*.
    case 'git-held': return item.git ? plain(...gitHeldOffers(item.git)) : [];
    // A git go-ahead's two answers, as its session's head offers them.
    case 'git-ask': return item.gitAsk ? plain('git-allow', 'git-refuse') : [];
    // *End and carry on* for a quiet quest session alone, never a busy one; *Hand
    // over…* for a quest session; *Stop…* and *Leave it running* for every one, an intake's among them.
    case 'quiet': return item.quiet ? quietOffers(item.quiet) : [];
    // A session's memory is decided on its page, where each row's words are read first; a sweep's end has no page, so its row decides it.
    case 'memory': return item.memory?.end ? plain('approve-end', 'decline-end') : [];
    default: return [];
  }
}

/**
 * What a second opinion's row offers: a dispute where a press of the person's is
 * coming (*Accept…* on its session's page) asks again only, since that press answers it; where none is (the work lands by
 * itself), *Go on anyway…* too. None to be had where the rule requires one, *Try again* and *Go on anyway…*; commits nobody read
 * where the work lands by itself, the same. The same agent fresh, *I looked myself…* and *Send back…* are on its session's page.
 */
function opinionOffers(item: Attention): AttentionOffer[] {
  const wait = item.opinion;
  if (!wait) return [];
  const anyway = wait.auto || opinionState(wait.opinion.state) === 'unavailable';
  return [...(anyway ? [{ act: 'opinion-anyway' as const }] : []), { act: 'opinion-again' as const }];
}

/** What a quiet row offers, in its order. */
function quietOffers(quiet: QuietWait): AttentionOffer[] {
  const acts: AttentionActId[] = [
    ...(!quiet.busy && !quiet.intake ? ['quiet-carry-on' as const] : []),
    ...(!quiet.intake ? ['quiet-hand-over' as const] : []),
    'quiet-stop', 'quiet-leave',
  ];
  return acts.map((act) => ({ act }));
}

/** The acts a row offers, without the accounts they are for: what each kind's rule says, read by the tests. */
export function attentionActs(item: Attention): AttentionActId[] {
  return attentionOffers(item).map(({ act }) => act);
}

/**
 * What mends a held group, by its class and what serves it, then *Try again*: a refusal of an SSH
 * identity its public key and its host's key page, of an HTTPS one a sign-in again, under the person's own sign-ins Settings → Git; a
 * push refused another identity or the identity marked read only; an own-input hold the person's own sign-ins for it; a host's key under
 * an identity its comparison and trust. The move's row is two presses alone.
 */
export function gitHeldOffers(held: GitHeld): AttentionActId[] {
  if (held.class === 'no-identity') return ['git-set-up', 'git-keep-own'];
  const identity = held.served === 'identity' && held.identityId ? held.identityId : null;
  const mends: AttentionActId[] = [];
  if (held.class === 'refused') {
    if (identity && held.way === 'https') mends.push('git-sign-in');
    else if (identity) mends.push('git-copy-key', ...(held.keyPage ? ['git-key-page' as const] : []));
    else if (held.served === 'own') mends.push('git-open');
  } else if (held.class === 'write-refused') {
    mends.push('git-choose', ...(identity ? ['git-read-only' as const] : []));
  } else if (held.class === 'own-input') {
    mends.push('git-use-own');
  } else if ((held.class === 'host-unknown' || held.class === 'host-changed') && identity) {
    mends.push('git-compare');
  }
  return [...mends, 'git-try-again'];
}
