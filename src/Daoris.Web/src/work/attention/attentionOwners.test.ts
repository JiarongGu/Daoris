import { describe, expect, it } from 'vitest';
import {
  attentionGroup, attentionGroups, attentionInOrder, attentionOf, bellCount, needsListed, obligationOf, ownersListed, placeCounts, type PushedAttention,
} from './attention';

// The window counts only what is the owner's, as .NET's census classes each row (`AttentionOwnership`): an agents' row is listed
// under *With the agents* and counted nowhere, not on the bell, at no place; a row no census reached is counted rather than hidden.

const at = '2026-09-21T09:00:00Z';
const row = (kind: PushedAttention['kind'], id: string, over: Partial<PushedAttention> = {}): PushedAttention => ({ kind, id, since: at, where: 'engine', ...over });

describe('what the window counts', () => {
  it('counts the owner’s rows alone and lists the agents’ apart, under their own group', () => {
    const rows = [
      ...Array.from({ length: 34 }, (_, n) => row('work', `daoris/s${n}`, { class: 'agents' })),
      row('unanswerable', 'q1', { class: 'owner', waitKind: 'question' }),
      row('space', 'D:', { class: 'owner', waitKind: 'go-ahead' }),
      row('departure', 'q2'),
    ];

    expect(needsListed(rows)).toHaveLength(37);
    expect(ownersListed(rows).map(obligationOf)).toEqual(['unanswerable:q1', 'space:D:', 'departure:q2']);
    expect(bellCount([], rows)).toBe(3);
    expect(placeCounts(rows)).toEqual({ quests: 2, settings: 1 });
    expect(attentionGroups(attentionOf(attentionInOrder(rows))).map(({ group, items }) => [group, items.length]))
      .toEqual([['word', 3], ['agents', 34]]);
  });

  it('keeps an agents’ row of any kind in the agents’ group, last in the band’s order', () => {
    const rows = attentionInOrder([row('parked', 'p1', { class: 'agents' }), row('review', 'r1'), row('signed-out', 's1', { class: 'owner' })]);
    expect(rows.map(obligationOf)).toEqual(['signed-out:s1', 'review:r1', 'parked:p1']);
    expect(attentionOf(rows).map((each) => each.class ?? null)).toEqual(['owner', null, 'agents']);
  });

  it('lists a vacancy under its own group, before the agents’, and never counts it as the owner’s', () => {
    const rows = [row('step', 'q7/sign', { class: 'vacant' }), row('work', 'daoris/s1', { class: 'agents' }), row('departure', 'q2')];

    expect(ownersListed(rows).map(obligationOf)).toEqual(['departure:q2']);
    expect(bellCount([], rows)).toBe(1);
    expect(attentionInOrder(rows).map((each) => attentionGroup(each))).toEqual(['word', 'vacant', 'agents']);
  });
});
