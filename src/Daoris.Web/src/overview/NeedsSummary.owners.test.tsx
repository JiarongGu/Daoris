import { render, screen } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import type { AttentionRead } from '../bridge/attention';
import i18n from '../locales/i18n';
import type { Confirmation } from '../person/Confirmations';
import {
  attentionGroups, attentionInOrder, attentionOf, bellCount, obligationOf, ownersListed, placeCounts, type PushedAttention,
} from '../work/attention/attention';
import judged from '../work/attention/judged.fixture.json';
import { NeedsSummary } from './NeedsSummary';

// The window counts what .NET's judge pushed, never rows made with a class preset: `judged.fixture.json` is what
// `AttentionStateSlice.Judge` pushes for its facts, held equal to it by `AttentionOwnershipTests`. A park a question supervisor takes is
// the agents'; a parked quest no course supervisor takes is the owner's, said as no role's yet; a quest nobody can be asked for is the owner's.

const ROWS = Object.values(judged) as PushedAttention[];
const NOW = new Date('2026-09-14T12:00:00Z');
const PRESSES: Confirmation[] = [
  { source: 'pipe', id: 'c1', route: 'STOP_SESSION', fields: [], shown: true },
  { source: 'service', id: 'h1', route: 'POST /x', act: 'a verdict', fields: [] },
];

describe('what the judge pushed, through the filter to the bell', () => {
  it('counts the owner’s rows the judge classed, the untaken among them, and lists the agents’ apart', () => {
    expect(ROWS.map((row) => [obligationOf(row), row.class, row.waitKind ?? null, row.untaken ?? false]).sort()).toEqual([
      ['parked-quest:q2', 'owner', 'question', true],
      ['parked:s1a2b3c4', 'agents', null, false],
      ['unanswerable:7a82cc', 'owner', 'question', false],
    ]);
    expect(ownersListed(ROWS).map(obligationOf).sort()).toEqual(['parked-quest:q2', 'unanswerable:7a82cc']);
    expect(bellCount([], ROWS)).toBe(2);
    expect(bellCount(PRESSES, ROWS)).toBe(4);
    expect(placeCounts(ROWS)).toEqual({ quests: 2 });
    expect(attentionGroups(attentionOf(attentionInOrder(ROWS))).map(({ group, items }) => [group, items.map((item) => item.kind)]))
      .toEqual([['holding', ['parked-quest']], ['word', ['unanswerable']], ['agents', ['parked']]]);
  });
});

describe('Overview’s owner lines', () => {
  afterEach(async () => {
    await i18n.changeLanguage('en');
  });

  const known: AttentionRead = { state: 'known', rows: ROWS, working: 0, running: 0, resyncing: false };
  /** Each line's count, by its label, as Overview draws it: a door named by its label, its count and its detail. */
  const counted = (labels: string[]) => Object.fromEntries(labels.map((label) => {
    const line = screen.queryByRole('button', { name: new RegExp(`^${label}(\\d+)`) });
    return [label, line ? Number(new RegExp(`^${label}(\\d+)`).exec(line.textContent ?? '')?.[1]) : 0];
  }));

  /** The presses' line and the owner's groups add up to the bell, the taskbar's and the title's count; the agents' line is apart. */
  it('add up, with the presses, to the bell, and the agents’ line is no part of it', () => {
    render(<NeedsSummary attention={known} presses={PRESSES} onNotifications={vi.fn()} now={NOW.getTime()} />);

    const owners = counted(['Waiting for your confirmation', 'Holding work', 'Waiting for your word', 'Ready for you']);
    expect(owners).toEqual({ 'Waiting for your confirmation': 2, 'Holding work': 1, 'Waiting for your word': 1, 'Ready for you': 0 });
    expect(Object.values(owners).reduce((sum, count) => sum + count, 0)).toBe(bellCount(PRESSES, ROWS));
    expect(counted(['With the agents'])).toEqual({ 'With the agents': 1 });
  });
});
