import type { TFunction } from 'i18next';
import { knownState, type RunStep, standsAt, type WorkflowRun } from './run';

// What a run says in the reader's language (WORKFLOW1c, the workflow design §5.2's *Said*, §7): each step's state and the row
// of its table, composed from the step's typed fields, so it cannot drift from what the driver read. A name from a record (an
// environment, a branch, a plugin, a commit) is set as code; a person's words and a session's are shown as written.

/** A commit as a person reads it: its first eight characters, as the driver says one. */
const short = (commit: string) => (commit.length > 8 ? commit.slice(0, 8) : commit);

/** The details each kind words; a detail a newer driver names is said as recorded. */
const DETAILS: Record<string, readonly string[]> = {
  work: ['queued', 'agent', 'answered', 'park', 'go-ahead', 'held', 'awaits', 'unclosed', 'failed', 'stopped', 'declined', 'finished'],
  opinion: ['declared', 'agent', 'answering', 'given', 'failed', 'unread'],
  look: ['not-shown', 'being-set-up', 'shown', 'not-yet', 'not-held', 'reviewed', 'skip', 'off', 'unread'],
  landing: ['accept', 'automatic', 'branch', 'merge', 'already', 'nothing', 'refused', 'gone', 'elsewhere'],
  'pull-request': ['merge', 'merged', 'abandoned', 'not-pushed', 'ask-failed', 'no-branch', 'nothing'],
};

/** A hold's codes this page words (D133, D144); another is said as the departure's. */
const HOLDS = ['departed', 'evidence-unread', 'evidence-missing'];

/** A landing try's codes that can stand in the way of an automatic landing (LAND2b). */
const TRIES = ['uncommitted', 'exists', 'completed', 'refused'];

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
    case 'work.declined': return t(at, { quest: step.quest ?? '' });
    case 'work.finished': return t(at, { count: step.count ?? 0 });
    case 'opinion.agent': return t(at, { agent: step.agent ?? '' });
    case 'opinion.answering': return t(at, { count: step.count ?? 0, of: step.of ?? 0 });
    case 'opinion.given': return t(at, { agent: step.agent ?? '', count: step.count ?? 0 });
    case 'opinion.failed':
      return step.code === 'ended' || step.code === 'out-of-time' ? t(`${at}.${step.code}`) : t(at, { code: step.code ?? '—' });
    case 'opinion.unread':
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

/** Why a step Current does not draw is in this run (design §4.6): a look the work's own choice added, or a reading nothing declares. */
export function addedSaid(t: TFunction, step: RunStep): string | null {
  if (!step.added) return null;
  if (step.step.kind === 'opinion') return t('workflow.run.added.opinion');
  return ['chain', 'ask', 'set-up-step'].includes(step.added) ? t(`workflow.run.added.${step.added}`) : t('workflow.run.added.other');
}

/**
 * The run in a few words, for a page's head (design §7: *Workflow: waits for your look*): where it stands, by the step and its
 * state, or how far it went where it finished. A quest done is not a run done (design §5.2): *the agent finished*, *landed*,
 * *pull request open* and *merged* are said apart.
 */
export function runShort(t: TFunction, run: WorkflowRun): string {
  const at = standsAt(run);
  if (!at) {
    const last = [...run.steps].reverse().find((step) => step.state === 'done');
    if (last?.step.kind === 'pull-request') return t('workflow.run.short.finished.merged');
    if (last?.step.kind === 'landing') {
      return t(last.detail === 'nothing' ? 'workflow.run.short.finished.nothing' : 'workflow.run.short.finished.landed');
    }
    return t('workflow.run.short.finished.done');
  }

  const kind = at.step.kind;
  if (kind === 'work' && at.state === 'waiting-on-you' && ['park', 'go-ahead', 'held', 'unclosed'].includes(at.detail)) {
    return t(`workflow.run.short.work.${at.detail}`);
  }

  const key = `workflow.run.short.${kind}.${at.state}`;
  const known = SHORT[kind]?.includes(at.state) ?? false;
  return known ? t(key) : stateSaid(t, at.state);
}

/** The short phrases each kind words, by state; any other is the state's word. */
const SHORT: Record<string, readonly string[]> = {
  work: ['working', 'waiting-on-agent', 'failed', 'stopped'],
  opinion: ['working', 'waiting-on-agent', 'failed'],
  look: ['waiting-on-you', 'working', 'waiting-on-agent'],
  landing: ['waiting-on-you', 'working', 'cannot-start', 'not-known', 'stopped'],
  'pull-request': ['waiting-on-you', 'cannot-start', 'not-known', 'stopped'],
};
