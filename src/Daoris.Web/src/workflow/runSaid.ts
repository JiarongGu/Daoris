import type { TFunction } from 'i18next';
import { type GraphRun, knownState, type RunStep, standsAt, type WorkflowRun, type WorkflowRunAnswer } from './run';

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
    'reopened', 'go-on',
  ],
  // The second opinion's gate's own states (XAGENT1f, `OpinionGateStates`), and a landing that let it go.
  opinion: [
    'none', 'waits-chain', 'not-asked', 'reading', 'read-again', 'with-session', 'disputed', 'commits-since', 'unavailable', 'settled',
    'anyway', 'myself', 'answered', 'landed', 'unread', 'cannot-start',
  ],
  look: ['not-shown', 'being-set-up', 'shown', 'not-yet', 'not-held', 'reviewed', 'skip', 'off', 'unread'],
  landing: ['accept', 'automatic', 'branch', 'merge', 'already', 'nothing', 'refused', 'gone', 'elsewhere'],
  'pull-request': ['merge', 'merged', 'abandoned', 'not-pushed', 'ask-failed', 'no-branch', 'nothing'],
};

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
    case 'work.submitted': return t(at, { count: step.count ?? 0 });
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
  const on = onSaid(t, step);
  switch (step.detail) {
    case 'ready': return t('workflow.run.graph.said.ready');
    case 'active':
      if (node === 'go-ahead') {
        return step.words ? t('workflow.run.graph.said.goAhead', { act: step.words }) : t('workflow.run.graph.said.goAheadNone');
      }
      if (node === 'human') return t('workflow.run.graph.said.task');
      return on ? t('workflow.run.graph.said.active', { on }) : t('workflow.run.graph.said.activeRun');
    case 'reached': return on ? t('workflow.run.graph.said.reached', { on }) : t('workflow.run.graph.said.reachedRun');
    case 'skipped': return t(step.code === 'person' ? 'workflow.run.graph.said.skippedByYou' : 'workflow.run.graph.said.skippedByRecord');
    case 'failed': return step.code ? t('workflow.run.graph.said.failed', { code: step.code }) : t('workflow.run.graph.said.failedNone');
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
  if (kind === 'work' && at.state === 'waiting-on-you' && ['park', 'go-ahead', 'held', 'unclosed', 'go-on'].includes(at.detail)) {
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
