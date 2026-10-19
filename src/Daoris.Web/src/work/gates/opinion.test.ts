import { describe, expect, it } from 'vitest';
import {
  acceptAnswers, findingPlace, findingsOf, type OpinionGate, opinionAsked, opinionHolds, opinionPresses, opinionState,
  opinionTone, reviewerName,
} from './opinion';

const gate = (state: string, more: Partial<OpinionGate> = {}): OpinionGate => ({ state, holds: true, ...more });

describe('the second opinion’s gate, as the page reads it (XAGENT1g)', () => {
  it('words a state it does not know as unread, which holds', () => {
    expect(opinionState('disputed')).toBe('disputed');
    expect(opinionState('a-newer-state')).toBe('unread');
    expect(opinionState(null)).toBe('unread');
  });

  it('reads the gate on a landing’s plan only where a level asks one, and holds only where it says so', () => {
    expect(opinionAsked(null)).toBeNull();
    expect(opinionAsked({})).toBeNull();
    expect(opinionAsked({ opinion: gate('none', { holds: false }) })).toBeNull();
    const settled = gate('settled', { holds: false });
    expect(opinionAsked({ opinion: settled })).toBe(settled);
    expect(opinionHolds({ opinion: settled })).toBeNull();
    const reading = gate('reading');
    expect(opinionHolds({ opinion: reading })).toBe(reading);
  });

  it('lets Accept… answer a dispute or commits nobody read only with the token it sends back', () => {
    expect(acceptAnswers(gate('disputed', { answers: 'o1:abc:disputed:1:0' }))).toBe(true);
    expect(acceptAnswers(gate('commits-since', { answers: 'o1:abc:commits-since:0:2' }))).toBe(true);
    expect(acceptAnswers(gate('disputed'))).toBe(false);
    expect(acceptAnswers(gate('reading', { answers: 'x' }))).toBe(false);
    expect(acceptAnswers(gate('disputed', { holds: false, answers: 'x' }))).toBe(false);
    expect(acceptAnswers(null)).toBe(false);
  });

  /** §8.5's table, row by row. */
  it('offers each state’s presses as the design’s table does', () => {
    expect(opinionPresses('waits-chain')).toEqual(['askNow', 'anyway']);
    expect(opinionPresses('not-asked')).toEqual(['askNow']);
    expect(opinionPresses('reading')).toEqual(['stop']);
    expect(opinionPresses('with-session')).toEqual([]);
    expect(opinionPresses('read-again')).toEqual([]);
    // A dispute is answered by the press that comes next anyway; with none coming, Go on anyway… is offered.
    expect(opinionPresses('disputed', { pressComing: true })).toEqual(['sendBack', 'askAgain']);
    expect(opinionPresses('disputed')).toEqual(['anyway', 'sendBack', 'askAgain']);
    expect(opinionPresses('unavailable', { required: true })).toEqual(['tryAgain', 'sameAgent', 'myself', 'anyway']);
    expect(opinionPresses('unavailable')).toEqual([]);
    expect(opinionPresses('commits-since', { pressComing: true })).toEqual(['askAgain']);
    expect(opinionPresses('commits-since')).toEqual(['anyway', 'askAgain']);
    for (const state of ['settled', 'anyway', 'myself', 'answered', 'unread', 'none'] as const) expect(opinionPresses(state)).toEqual([]);
  });

  it('wears the person’s hue where it waits on them, taken’s while an agent works, and never hue alone', () => {
    expect(opinionTone('disputed')).toBe('open');
    expect(opinionTone('reading')).toBe('taken');
    expect(opinionTone('unavailable')).toBe('open');
    expect(opinionTone('unavailable', { holds: false })).toBe('neutral');
    expect(opinionTone('settled')).toBe('done');
    expect(opinionTone('anyway')).toBe('neutral');
  });

  it('names the reviewer by its product and maker, else its adapter', () => {
    expect(reviewerName({ product: 'Codex', reviewer: 'codex-acp', maker: 'OpenAI' })).toBe('Codex (OpenAI)');
    expect(reviewerName({ product: null, reviewer: 'my-agent', maker: null })).toBe('my-agent');
  });

  it('opens a finding’s path at its line, and says a commit or general as it is', () => {
    expect(findingPlace('src/a.ts:12')).toEqual({ path: 'src/a.ts', lines: { from: 12, to: 12 } });
    expect(findingPlace('src/a.ts:12-18')).toEqual({ path: 'src/a.ts', lines: { from: 12, to: 18 } });
    expect(findingPlace('docs/notes.md')).toEqual({ path: 'docs/notes.md' });
    expect(findingPlace('general')).toBeNull();
    expect(findingPlace('abc1234')).toBeNull();
    expect(findingPlace('')).toBeNull();
  });

  it('lists the first pass’s findings, then the recheck’s own', () => {
    const finding = (number: number) => ({ number, weight: 'must', where: 'general', claim: `c${number}`, beside: {} });
    expect(findingsOf({ tier: 'agent', first: { id: 'o1', findings: [finding(1), finding(2)] }, recheck: { id: 'o2', findings: [finding(1)] } })
      .map(({ pass, finding: { number } }) => `${pass}:${number}`)).toEqual(['first:1', 'first:2', 'recheck:1']);
    expect(findingsOf(null)).toEqual([]);
  });
});
