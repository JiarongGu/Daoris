import { describe, expect, it, vi } from 'vitest';
import { render } from '@testing-library/react';
import '../../locales/i18n';
import { type DrawnNode, PositionedWorld } from './PositionedWorld';

// What a zoom step redraws: a card whose words stand at a fixed size is drawn once, whatever the scale and whatever new closures
// and records the drawing hands the world, since a hundred cards redrawn on every step overran the canvas's frame.

const drawn = vi.hoisted(() => ({ cards: 0 }));
vi.mock('../../lib/cn', async (original) => {
  const real = await original<typeof import('../../lib/cn')>();
  return { ...real, cn: (...inputs: Parameters<typeof real.cn>) => { drawn.cards += 1; return real.cn(...inputs); } };
});

const nodes = (): DrawnNode[] => ['a', 'b', 'c'].map((id, index) => ({
  id, title: `Step ${id}`, box: { x: index * 200, y: 0, w: 180, h: 60 }, at: index + 1, selected: false, dim: false,
}));

const world = (k: number, tier: 'full' | 'chip', list: DrawnNode[]) => (
  <PositionedWorld
    size={{ w: 600, h: 100 }} total={3} k={k} tier={tier} label="Graph" nodes={list} groups={[]} paths={[]} aggregates={[]} active="a"
    onPress={() => undefined} onOpen={() => undefined} onFold={() => undefined} onKeyDown={() => undefined} onFocus={() => undefined}
  />
);

describe('the positioned world', () => {
  it('draws no full card again when only the scale changes', () => {
    const view = render(world(1, 'full', nodes()));
    expect(drawn.cards).toBe(3);
    view.rerender(world(1.25, 'full', nodes()));
    view.rerender(world(1.5, 'full', nodes()));
    expect(drawn.cards).toBe(3);
  });

  it('draws a chip again when the scale moves its counter-scaled word', () => {
    const view = render(world(0.5, 'chip', nodes()));
    drawn.cards = 0;
    view.rerender(world(0.6, 'chip', nodes()));
    expect(drawn.cards).toBe(3);
  });

  it('draws only the card whose selection changed', () => {
    const view = render(world(1, 'full', nodes()));
    drawn.cards = 0;
    view.rerender(world(1, 'full', nodes().map((node) => (node.id === 'b' ? { ...node, selected: true } : node))));
    expect(drawn.cards).toBe(1);
  });
});
