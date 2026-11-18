import { describe, expect, it } from 'vitest';
import '../../locales/i18n';
import type { Attention } from './AttentionRow';
import { ASKS_ONCE, attentionOffers } from './attention';

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
});
