import { describe, expect, it } from 'vitest';
import { buildTree, layoutCode } from './codeClusters';
import { createRouter, crosses } from './codeRoute';

// The code map's lines routed around its cards, the direct shape kept where it crosses none.

const card = (x: number, y: number) => ({ x, y, w: 136, h: 36 });
const clear = (points: readonly { x: number; y: number }[], box: { x: number; y: number; w: number; h: number }) =>
  points.every((point, at) => at === 0 || !crosses(points[at - 1]!, point, box));

describe('a line between two cards', () => {
  it('keeps its direct shape where nothing stands between them', () => {
    const boxes = new Map([['a', card(0, 0)], ['b', card(200, 100)]]);
    const line = createRouter(boxes).route('a', 'b', 0);
    expect(line.d).toContain(' C ');
    expect(line.d.startsWith('M 68 36')).toBe(true);
  });

  it('runs around a card that stands between them, leaving and entering where the direct shape would', () => {
    const boxes = new Map([['a', card(0, 0)], ['in the way', card(0, 64)], ['b', card(0, 128)]]);
    const line = createRouter(boxes).route('a', 'b', 0);
    expect(line.d).not.toContain(' C ');
    expect(line.points[0]).toEqual({ x: 68, y: 36 });
    expect(line.points.at(-1)).toEqual({ x: 68, y: 126 });
    expect(clear(line.points, boxes.get('in the way')!)).toBe(true);
    expect(clear(line.points, boxes.get('a')!)).toBe(true);
    // Its count stands on the line, outside the card it went round.
    expect(crosses({ x: line.mid.x - 0.5, y: line.mid.y }, { x: line.mid.x + 0.5, y: line.mid.y }, boxes.get('in the way')!)).toBe(false);
  });

  it('goes round a row of cards to a card beside it on the far side', () => {
    const boxes = new Map([['a', card(0, 0)], ['wall-1', card(160, -40)], ['wall-2', card(160, 0)], ['wall-3', card(160, 40)], ['b', card(320, 0)]]);
    const line = createRouter(boxes).route('a', 'b', 0);
    for (const wall of ['wall-1', 'wall-2', 'wall-3']) expect(clear(line.points, boxes.get(wall)!)).toBe(true);
  });
});

describe('a line in a crowd', () => {
  it('is still routed round the card in its way where more than 64 cards stand within its reach', () => {
    const boxes = new Map([['a', card(0, 0)], ['in the way', card(0, 64)], ['b', card(0, 128)]]);
    // 80 cards beside the way, within reach, farther from the line than the one in it.
    for (let at = 0; at < 80; at += 1) boxes.set(`crowd-${at}`, card(200 + (at % 8) * 150, -100 + Math.floor(at / 8) * 50));
    const line = createRouter(boxes).route('a', 'b', 0);
    expect(line.d).not.toContain(' C ');
    for (const [id, box] of boxes) if (id !== 'a' && id !== 'b') expect(clear(line.points, box)).toBe(true);
  });

  it('is routed in each of more than 200 lines, since a short route is cheap', () => {
    const boxes = new Map<string, { x: number; y: number; w: number; h: number }>();
    for (let at = 0; at < 250; at += 1) {
      const x = at * 400;
      boxes.set(`a${at}`, card(x, 0)); boxes.set(`m${at}`, card(x, 64)); boxes.set(`b${at}`, card(x, 128));
    }
    const router = createRouter(boxes);
    for (let at = 0; at < 250; at += 1) expect(router.route(`a${at}`, `b${at}`, 0).d).not.toContain(' C ');
  });
});

describe('the code map\'s lines', () => {
  it('pass no card between two layers apart: what a top module uses at the foot is drawn around the middle one', () => {
    const tree = buildTree(
      [{ id: 'top', path: 'src/x/top', summary: '' }, { id: 'middle', path: 'src/x/middle', summary: '' }, { id: 'foot', path: 'src/x/foot', summary: '' }],
      [{ from: 'top', to: 'middle', kind: 'k' }, { from: 'middle', to: 'foot', kind: 'k' }, { from: 'top', to: 'foot', kind: 'k' }],
    );
    const scene = layoutCode(tree, new Set(tree.groups.keys()));
    const around = scene.lines.find((line) => line.from === 'top' && line.to === 'foot')!;
    expect(around.d).not.toContain(' C ');
    expect(scene.lines.find((line) => line.from === 'top' && line.to === 'middle')!.d).toContain(' C ');
  });
});
