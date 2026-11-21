import { describe, expect, it } from 'vitest';
import i18n from '../../locales/i18n';
import type { Attention } from './AttentionRow';
import { ASKS_ONCE, attentionOffers } from './attention';
import { stepRead } from './taskRead';

describe('what a row asks once', () => {
  it('asks once before widening what Daoris may do, choices, end', () => {
    expect([...ASKS_ONCE].sort())
      .toEqual('accept-rule approve ask-again choose go-on not-yet opinion-anyway pass-over quiet-carry-on quiet-stop refuse step-again step-outside stop-node task-approve task-done task-reject trust'.split(' '));
  });

  /** The step a run ended failed at offers its *Try again*, its *Done outside Daoris…* where its kind reads the proof, and its stop; a held attempt says its own. */
  it('offers the step a run ended failed at its try again, done outside and stop', () => {
    const left = { kind: 'step', step: { run: 'q7', node: 'push', why: 'left', role: 'lead', holder: 'person', presses: ['again', 'done-outside', 'stop'] } } as unknown as Attention;
    expect(attentionOffers(left).map((offer) => offer.act)).toEqual(['step-again', 'step-outside', 'stop-node']);
    const held = { ...left, step: { ...left.step!, held: { unit: 'run:q7', attempt: 1, words: ['again', 'done-outside'] } } } as Attention;
    expect(attentionOffers(held).map((offer) => offer.act)).toEqual(['stop-node']);
  });

  it('words a step at the final exit by its kind and offers its answers', () => {
    const step = { run: 'q7', node: 'check', why: 'left', role: 'supervisor', holder: 'person', presses: ['again', 'stop'], exit: { key: 'exit:q7/check#3', kind: 'spent' as const } };
    const read = stepRead(step);
    expect(read.detail).toBe(i18n.t('work.attention.stepExit.spent', { node: 'check', role: 'supervisor' }));
    expect(read.detail).toContain('final exit');
    expect(stepRead({ ...step, exit: { key: 'exit:q7/check#3', kind: 'vacant' } }).detail).toContain('Vacant');
    expect(attentionOffers({ kind: 'step', step } as unknown as Attention).map((offer) => offer.act)).toEqual(['step-again', 'stop-node']);
  });
});
