import type { TFunction } from 'i18next';
import type { RunLine, RunStep, WorkflowRun } from '../run';
import { checkWhy } from '../runSaid';

/**
 * A verification's verdict as its status word, in a check's own words: *verified*,
 * *failed: `<gate>`*, *not known: `<why>`*, *not now*, *waiting for its gates* while they run and *waiting for its turn* before; a verdict
 * the page does not know is said as the driver spelled it.
 */
export function lineWord(t: TFunction, line: RunLine): string {
  switch (line.verdict) {
    case 'verified': return t('workflow.run.check.verified');
    case 'failed': return line.gate ? t('workflow.run.check.failed', { gate: line.gate }) : t('workflow.run.check.failedNone');
    case 'not-now': return t('workflow.run.check.notNow');
    case 'unknown': return t('workflow.run.check.notKnown', { why: checkWhy(t, line.why) });
    case 'running': return t('workflow.run.check.waiting', { what: t('workflow.run.check.gates') });
    case 'waiting': return t('workflow.run.check.waiting', { what: t('workflow.run.check.turn') });
    default: return line.verdict;
  }
}

/** What a landed run judged: what landed on its line since the base, or the line's first verification; null for a person's run. */
export function lineBatch(t: TFunction, line: RunLine): string | null {
  if (!line.line) return null;
  return line.base
    ? t('workflow.run.line.batch', { line: line.line, base: line.base.slice(0, 7) })
    : t('workflow.run.line.first', { line: line.line });
}

/** The check a person may answer now: held unknown for them, with words to say and none said yet. */
export const heldCheck = (run: WorkflowRun): RunStep | null =>
  run.steps.find((step) => step.node === 'check' && step.held && !step.held.resolution && step.held.words.length > 0) ?? null;

/**
 * A verdict's pill: verified in done's hue, failed in red, an unknown the person may answer in open's hue (theirs alone, platform-ux §3),
 * every other neutral.
 */
export function lineTone(run: WorkflowRun): 'open' | 'done' | 'declined' | 'neutral' {
  switch (run.line?.verdict) {
    case 'verified': return 'done';
    case 'failed': return 'declined';
    case 'unknown': return heldCheck(run) ? 'open' : 'neutral';
    default: return 'neutral';
  }
}
