import type { TFunction } from 'i18next';
import { type GraphRun, knownState, type RunRequest, type RunStep, standsAt, type WorkflowRun, type WorkflowRunAnswer } from './run';

// What a run says in the reader's language (WORKFLOW1c, the workflow design §5.2's *Said*, §7): each step's state and the row
// of its table, composed from the step's typed fields, so it cannot drift from what the driver read. A name from a record (an
// environment, a branch, a plugin, a commit) is set as code; a person's words and a session's are shown as written.

/** A commit as a person reads it: its first eight characters, as the driver says one. */
const short = (commit: string) => (commit.length > 8 ? commit.slice(0, 8) : commit);

/** The details each kind words; a detail a newer driver names is said as recorded. */
const DETAILS: Record<string, readonly string[]> = {
  // A closure graph's work finished at its submissions, and a submission the person reopened (FABRIC-WORKFLOW2d); made ready again, its
  // started step waits on their go-on (FABRIC-CLOSE1c7).
  work: [
    'queued', 'agent', 'answered', 'park', 'go-ahead', 'held', 'awaits', 'unclosed', 'failed', 'stopped', 'declined', 'finished', 'submitted',
    'reopened', 'go-on', 'talk', 'land', 'landed',
  ],
  // The second opinion's gate's own states (XAGENT1f, `OpinionGateStates`), and a landing that let it go.
  opinion: [
    'none', 'waits-chain', 'not-asked', 'reading', 'read-again', 'with-session', 'disputed', 'commits-since', 'unavailable', 'settled',
    'anyway', 'myself', 'answered', 'landed', 'unread', 'cannot-start', 'chat-at-accept',
  ],
  look: ['not-shown', 'being-set-up', 'shown', 'not-yet', 'not-held', 'reviewed', 'skip', 'off', 'unread'],
  landing: ['accept', 'automatic', 'branch', 'merge', 'already', 'nothing', 'refused', 'gone', 'elsewhere'],
  'pull-request': ['merge', 'merged', 'abandoned', 'not-pushed', 'ask-failed', 'no-branch', 'nothing'],
};

/** The code a `work.requests` node carries while a quest the work's sessions published stays declined (`WorkRequests.DeclinedCode`). */
export const DECLINED = 'declined';

/** The states a request's own state is worded by; another is said as the driver spelled it. */
const REQUEST_STATES = ['open', 'taken', 'done', 'declined', 'working', 'failed', 'stopped'];

/** What one request inside the work says: its kind and where it was asked, and its state as the driver read it (INTEGRATE1o part o2). */
export function requestSaid(t: TFunction, request: RunRequest): { what: string; state: string; awaits: string | null } {
  const what = request.kind === 'agent'
    ? (request.to ? t('workflow.run.request.agent', { to: request.to }) : t('workflow.run.request.agentAnyone'))
    : t('workflow.run.request.quest', { id: request.id, to: request.to });
  const state = REQUEST_STATES.includes(request.state)
    ? t(`workflow.run.request.state.${request.state}`)
    : t('workflow.run.request.state.other', { state: request.state });
  return { what, state, awaits: request.awaits ? t('workflow.run.request.awaits') : null };
}

/** A hold's codes this page words (D133, D144); another is said as the departure's. */
const HOLDS = ['departed', 'evidence-unread', 'evidence-missing'];

/** A landing try's codes that can stand in the way of an automatic landing (LAND2b). */
const TRIES = ['uncommitted', 'exists', 'completed', 'refused'];

/** Why no second opinion could be had, the codes this page words (`ReviewerUnavailable`, a failed pass's); another is the general. */
const OPINION_WHY = ['no-reviewer', 'cooling', 'signed-out', 'not-independent', 'ended', 'out-of-time'];

/** A state's word, a status: the driver's own spelling where the page has none. */
export function stateSaid(t: TFunction, state: string): string {
  return knownState(state) ? t(`workflow.run.state.${state}`) : t('workflow.run.state.unknown', { state });
}

/** Why a check is not known, the codes the page words (`VerifyResult`'s `changed`, `bound`, `stopped`, `busy`); another is said as the general. */
const CHECK_WHY = ['changed', 'bound', 'stopped', 'busy'];

/**
 * A check's verdict as a status word (FABRIC-PIPELINE1d, the pipeline design §4.4): *verified*, *failed: `<gate>`*, *not known: `<why>`*,
 * *waiting for `<what>`*, *not now*; null for a step that is no check, or a check standing where no verdict word applies.
 */
export function checkWord(t: TFunction, step: RunStep): string | null {
  if (step.node !== 'check') return null;
  const why = t(`workflow.run.check.why.${CHECK_WHY.includes(step.code ?? '') ? step.code : 'other'}`);
  switch (step.detail) {
    case 'reached': return t('workflow.run.check.verified');
    case 'failed': return step.gate ? t('workflow.run.check.failed', { gate: step.gate }) : t('workflow.run.check.failedNone');
    case 'unknown': return step.held?.resolution === 'stop' ? t('workflow.run.check.notNow') : t('workflow.run.check.notKnown', { why });
    case 'stopped': return t('workflow.run.check.notNow');
    case 'ready': return t('workflow.run.check.waiting', { what: t('workflow.run.check.turn') });
    case 'active': return t('workflow.run.check.waiting', { what: t('workflow.run.check.gates') });
    default: return null;
  }
}

/** A step's status word: a check's verdict where it has one, else its state's. */
export const stepWord = (t: TFunction, step: RunStep): string => checkWord(t, step) ?? stateSaid(t, step.state);

/** What a check says under its status word, where it says more (FABRIC-PIPELINE1d); null where the word says it all. */
function checkSaid(t: TFunction, step: RunStep): string | null {
  switch (step.detail) {
    case 'reached': return t('workflow.run.check.said.verified');
    case 'failed': return t('workflow.run.check.said.failed');
    case 'unknown':
      if (step.held?.resolution === 'stop') return t('workflow.run.check.said.notNow');
      if (step.held?.resolution === 'again') return t('workflow.run.check.said.again');
      return t(step.held && step.held.words.length > 0 ? 'workflow.run.check.said.held' : 'workflow.run.check.said.unknown');
    case 'stopped': return t('workflow.run.check.said.notNow');
    case 'ready':
    case 'active': return t('workflow.run.check.said.waiting');
    default: return null;
  }
}

/**
 * What a step's state says (design §5.2's *Said*), or null where the state's word says it all (not reached). A state or a
 * detail the page has no words for is said as recorded, never dropped.
 */
export function runSaid(t: TFunction, step: RunStep): string | null {
  const { kind } = step.step;
  if (step.state === 'not-reached') return null;
  if (step.node) return nodeSaid(t, step, step.node);
  if (step.state === 'cannot-start' && step.words) return t('workflow.run.said.cannotStart', { why: step.words });
  if (!knownState(step.state) || !(DETAILS[kind] ?? []).includes(step.detail)) {
    return t('workflow.run.said.recorded', { state: step.state, detail: step.detail || '—' });
  }

  const at = `workflow.run.said.${kind}.${step.detail}`;
  switch (`${kind}.${step.detail}`) {
    case 'work.queued': return t(at, { quest: step.quest ?? '' });
    case 'work.agent': return step.agent ? t(at, { agent: step.agent }) : t('workflow.run.said.work.agentAnyone');
    case 'work.go-ahead': return t(at, { number: step.goAhead ?? '', act: step.words ?? '' });
    case 'work.held': return t(`${at}.${HOLDS.includes(step.code ?? '') ? step.code : 'departed'}`);
    case 'work.awaits':
    case 'work.unclosed':
    case 'work.declined':
    case 'work.reopened':
    case 'work.go-on': return t(at, { quest: step.quest ?? '' });
    case 'work.finished':
    case 'work.submitted':
    // CHATFLOW1c: a chat run's commits beyond the line, which wait for the person's Accept.
    case 'work.land': return t(at, { count: step.count ?? 0 });
    case 'opinion.reading': return step.agent ? t(at, { agent: step.agent }) : t('workflow.run.said.opinion.readingAnyone');
    case 'opinion.read-again': return step.agent ? t(at, { agent: step.agent }) : t('workflow.run.said.opinion.readingAnyone');
    case 'opinion.with-session':
    case 'opinion.disputed':
    case 'opinion.commits-since': return t(at, { count: step.count ?? 0 });
    case 'opinion.unavailable': {
      const why = t(`workflow.run.opinionWhy.${OPINION_WHY.includes(step.code ?? '') ? step.code : 'other'}`);
      // Required, it waits on the person; not, it is passed over and holds nothing (the second-agent design §8.4).
      return t(step.state === 'waiting-on-you' ? at : 'workflow.run.said.opinion.unavailableOptional', { why });
    }
    case 'opinion.settled': return step.agent ? t(at, { agent: step.agent }) : t('workflow.run.said.opinion.settledAnyone');
    // CHATFLOW1d: a chat run's opinion, asked by the person's Accept and naming the reviewer it asks.
    case 'opinion.chat-at-accept': return step.agent ? t(at, { agent: step.agent }) : t(`${at}Anyone`);
    case 'opinion.answered': return t(step.code === 'reviewed' ? 'workflow.run.said.opinion.answeredReviewed' : at);
    case 'opinion.waits-chain': return t(at, { quest: step.quest ?? '' });
    // WORKFLOW1f: a named workflow's opinion step naming reviewers its repository does not declare.
    case 'opinion.unread':
    case 'opinion.cannot-start':
    case 'look.unread': return step.words ? t(`${at}Why`, { why: step.words }) : t(at);
    case 'look.not-shown':
    case 'look.being-set-up': return t(at, { environment: step.environment ?? '', quest: step.quest ?? '' });
    case 'look.shown':
    case 'look.reviewed':
      return step.commit
        ? t(`${at}At`, { environment: step.environment ?? '', commit: short(step.commit) })
        : t(at, { environment: step.environment ?? '' });
    case 'look.off': return t(step.code === 'chain' ? 'workflow.run.said.look.offChain' : 'workflow.run.said.look.offAsk');
    case 'landing.branch':
      return t(step.code === 'auto' ? 'workflow.run.said.landing.branchAuto' : at, { branch: step.branch ?? '' });
    case 'landing.refused':
      return t(at, { why: t(`workflow.run.try.${TRIES.includes(step.code ?? '') ? step.code : 'refused'}`) });
    case 'pull-request.merge':
      return t(step.code === 'open' ? `${at}.open` : step.code ? `${at}.unknown` : `${at}.unread`, { plugin: step.plugin ?? '' });
    case 'pull-request.not-pushed':
    case 'pull-request.ask-failed': return t(at, { plugin: step.plugin ?? '', code: step.code ?? '' });
    default: return t(at);
  }
}

/**
 * The details a graph run's node is said by (FABRIC-CUTOVER1e, the driver's `WorkflowGraphDetails`): its record's state. FABRIC-CUTOVER1d took
 * `not-begun` away with the guards it named, so an answer from a driver before it says that node as recorded.
 */
export const GRAPH_DETAILS = [
  'blocked', 'ready', 'active', 'reached', 'failed', 'rejected', 'unknown', 'skipped', 'stopped', 'invalidated',
] as const;

/** What a graph run's node observes, named (the cutover design §2.2): its quest, its branch at its tip, or its landing there; null for the run. */
function onSaid(t: TFunction, step: RunStep): string | null {
  if (step.on === 'quest' && step.quest) return t('workflow.run.graph.on.quest', { quest: step.quest });
  const where = step.on === 'branch' ? 'branch' : step.on === 'landing' ? 'landing' : null;
  if (!where || !step.branch) return null;
  return step.commit
    ? t(`workflow.run.graph.on.${where}At`, { branch: step.branch, commit: short(step.commit) })
    : t(`workflow.run.graph.on.${where}`, { branch: step.branch });
}

/**
 * What a graph run's node says (FABRIC-CUTOVER1e): its record's state on the subject it observes, named; the person's words are shown under
 * it, never in it. A state or a detail this page has no words for is said as recorded.
 */
function nodeSaid(t: TFunction, step: RunStep, node: string): string | null {
  if (!knownState(step.state) || !(GRAPH_DETAILS as readonly string[]).includes(step.detail)) {
    return t('workflow.run.said.recorded', { state: step.state, detail: step.detail || '—' });
  }
  if (node === 'check') {
    const said = checkSaid(t, step);
    if (said) return said;
  }
  const on = onSaid(t, step);
  switch (step.detail) {
    case 'ready': return t('workflow.run.graph.said.ready');
    case 'active':
      if (node === 'go-ahead') {
        return step.words ? t('workflow.run.graph.said.goAhead', { act: step.words }) : t('workflow.run.graph.said.goAheadNone');
      }
      if (node === 'human') return t('workflow.run.graph.said.task');
      // INTEGRATE1o part o2: a decline holds the work's requests active and waiting on you; the driver reads it from the quests, as its code.
      if (node === 'work.requests') {
        return t(step.code === DECLINED ? 'workflow.run.graph.said.requestsDeclined' : 'workflow.run.graph.said.requests');
      }
      return on ? t('workflow.run.graph.said.active', { on }) : t('workflow.run.graph.said.activeRun');
    case 'reached': return on ? t('workflow.run.graph.said.reached', { on }) : t('workflow.run.graph.said.reachedRun');
    case 'skipped': return t(step.code === 'person' ? 'workflow.run.graph.said.skippedByYou' : 'workflow.run.graph.said.skippedByRecord');
    // INTEGRATE1p (C33): a declined request is the person's to ask again or go on without.
    case 'failed':
      if (node === 'request' && step.code === DECLINED) return t('workflow.run.graph.said.requestDeclined');
      return step.code ? t('workflow.run.graph.said.failed', { code: step.code }) : t('workflow.run.graph.said.failedNone');
    case 'rejected': return t('workflow.run.graph.said.rejected');
    case 'stopped': return t('workflow.run.graph.said.stopped');
    case 'unknown': return t('workflow.run.graph.said.unknown');
    case 'invalidated': return t('workflow.run.graph.said.invalidated');
    default: return t('workflow.run.said.recorded', { state: step.state, detail: step.detail || '—' });
  }
}

/**
 * What a run bound to a graph version says of itself, above its nodes (FABRIC-CUTOVER1e; the cutover design §2.3, §3): what its record says
 * where it says something, the subjects its nodes observe, and that its branches are its graph's; its landing doors read its record since
 * FABRIC-CUTOVER1d, so no line says they hold it. The driver's `WorkflowRunWords.GraphSaid`, line for line.
 */
export function graphSaid(t: TFunction, graph: GraphRun): string[] {
  const said: string[] = [];
  if (graph.unread) said.push(t('workflow.run.graph.unread'));
  else if (graph.ended) said.push(t('workflow.run.graph.ended', { quest: graph.ended }));
  else if (!graph.judged) said.push(t('workflow.run.graph.unjudged'));
  if (graph.quest) {
    said.push(graph.branch
      ? t('workflow.run.graph.subjects', { quest: graph.quest, branch: graph.branch })
      : t('workflow.run.graph.subjectsNoBranch', { quest: graph.quest }));
  }
  said.push(t('workflow.run.graph.branches'));
  return said;
}

/** The codes of a read that names no run (CHATFLOW1a, `WorkflowNoRun`) this page words; another is the driver's sentence. */
export const NO_RUN_CODES: readonly string[] = [
  'no-session', 'no-quest', 'ask-no-quest', 'chat-checkout', 'chat-own-tree', 'chat-graph-later', 'chat-tree-gone', 'ask-daoris', 'reviewer',
  'helper', 'partner', 'teammate', 'unanswered',
];

/**
 * Why no run is read, in the reader's language (CHATFLOW1b, the chat-flow design §10): a code this page knows in its catalogue's words,
 * whose en is the driver's English word for word (the twin table's `none` rows); a code it does not know in the driver's sentence, as
 * a node's refusal is (D182); and a read with no code in the driver's sentence, else the page's own *nothing runs here yet*.
 */
export function noRunSaid(t: TFunction, answer: WorkflowRunAnswer): string {
  if (answer.code && NO_RUN_CODES.includes(answer.code)) {
    return t(`workflow.run.noRun.${answer.code}`, { id: answer.id ?? '', workflow: answer.workflow ?? '' });
  }
  return answer.problem ?? t('workflow.run.none');
}

/**
 * What a chat run says of its checks (CHATFLOW1c, the chat-flow design §4.4): the run's checks, not yet enforced for a conversation, so
 * never a limit its tree holds; null for a quest's run, a chat run whose repository sets none, and one whose checks are enforced. The
 * driver's `WorkflowRunWords.ChatChecks`, word for word in en.
 */
export function chatChecksSaid(t: TFunction, run: WorkflowRun): string | null {
  if (!run.chat?.checks || run.chat.enforced) return null;
  return t('workflow.run.chatChecks', { checks: run.chat.checks });
}

/** Why a step Current does not draw is in this run (design §4.6): a look the work's own choice added. */
export function addedSaid(t: TFunction, step: RunStep): string | null {
  if (!step.added) return null;
  return ['chain', 'ask', 'set-up-step'].includes(step.added) ? t(`workflow.run.added.${step.added}`) : t('workflow.run.added.other');
}

/**
 * The run in a few words, for a page's head (design §7: *Workflow: waits for your look*): where it stands, by the step and its
 * state, or how far it went where it finished. A quest done is not a run done (design §5.2): *the agent finished*, *landed*,
 * *pull request open* and *merged* are said apart.
 */
export function runShort(t: TFunction, run: WorkflowRun): string {
  if (run.problem) return t('workflow.run.short.unread');
  if (run.workflowGate?.holds) return t('workflow.run.short.workflowHeld');
  const at = standsAt(run);
  if (!at) {
    const last = [...run.steps].reverse().find((step) => step.state === 'done');
    if (last?.step.kind === 'pull-request') return t('workflow.run.short.finished.merged');
    if (last?.step.kind === 'landing') {
      return t(last.detail === 'nothing' ? 'workflow.run.short.finished.nothing' : 'workflow.run.short.finished.landed');
    }
    return t('workflow.run.short.finished.done');
  }

  // FABRIC-CUTOVER1e: a graph run's node is its record's state, which the steps' short words do not read; its state's word says it.
  if (at.node) return stateSaid(t, at.state);

  const kind = at.step.kind;
  // CHATFLOW1c: a chat run's conversation, the person's turn to talk or its work waiting for their Accept.
  if (kind === 'work' && at.state === 'waiting-on-you' && ['park', 'go-ahead', 'held', 'unclosed', 'go-on', 'talk', 'land'].includes(at.detail)) {
    return t(`workflow.run.short.work.${at.detail}`);
  }

  // A reopened submission waits on its own agent (FABRIC-WORKFLOW2d), not on another repository as the state's short words say.
  if (kind === 'work' && at.detail === 'reopened') return t('workflow.run.short.work.reopened');

  const key = `workflow.run.short.${kind}.${at.state}`;
  const known = SHORT[kind]?.includes(at.state) ?? false;
  return known ? t(key) : stateSaid(t, at.state);
}

/** The short phrases each kind words, by state; any other is the state's word. */
const SHORT: Record<string, readonly string[]> = {
  work: ['working', 'waiting-on-agent', 'failed', 'stopped'],
  opinion: ['working', 'waiting-on-agent', 'waiting-on-you', 'not-known'],
  look: ['waiting-on-you', 'working', 'waiting-on-agent'],
  landing: ['waiting-on-you', 'working', 'cannot-start', 'not-known', 'stopped'],
  'pull-request': ['waiting-on-you', 'cannot-start', 'not-known', 'stopped'],
};
