import { useMemo, useState } from 'react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import '../../locales/i18n';
import { CanvasFind } from './CanvasFind';
import { CanvasList } from './CanvasList';
import { syntheticScene } from './canvasFixtures';
import { layoutScene } from './layoutScene';
import { PositionedCanvas } from './PositionedCanvas';
import { buildModel, displayOf, edgesAmong, GROUP } from './sceneModel';

// The reader's side of a large world. jsdom lays nothing out, so the canvas
// is below its floor here and a graph is its grouped list; the viewport, the cull and the tiers are `measured/large.spec.ts`'s.

afterEach(() => localStorage.clear());

describe('the scene as the canvas shows it', () => {
  const grouped = buildModel(syntheticScene(5000, { groups: 50 }), layoutScene(syntheticScene(5000, { groups: 50 })));
  const flat = buildModel(syntheticScene(300), layoutScene(syntheticScene(300)));

  it('shows a card per group past 1,000 nodes and opens one group at a time', () => {
    expect(grouped.overview).toBe(true);
    const closed = displayOf(grouped, null);
    expect(closed.ids).toHaveLength(50);
    expect(closed.ids.every((id) => id.startsWith(GROUP))).toBe(true);
    expect(closed.aggregates.reduce((sum, line) => sum + line.count, 0)).toBeGreaterThan(0);
    const open = displayOf(grouped, 'g3');
    expect(open.ids).toHaveLength(49 + 100);
    expect(open.frames.map((frame) => [frame.group.id, frame.opened])).toEqual([['g3', true]]);
    // Opening another closes the first: one region at a time.
    expect(displayOf(grouped, 'g4').boxes.has(GROUP + 'g3')).toBe(true);
  });

  it('shows every node of a world to 1,000, each with its place in the whole', () => {
    expect(flat.overview).toBe(false);
    const shown = displayOf(flat, null);
    expect(shown.ids).toHaveLength(300);
    expect(shown.at.get(shown.ids[299]!)).toBe(300);
  });

  it('counts the edges in view among the shown nodes and stops past its cap', () => {
    const shown = displayOf(flat, null);
    expect(edgesAmong(flat, shown, shown.ids, 1_000_000).size).toBe(flat.scene.edges.length);
    expect(edgesAmong(flat, shown, shown.ids, 10).size).toBe(11);
    // An overview draws no node-to-node edge for a closed group.
    expect(edgesAmong(grouped, displayOf(grouped, null), ['n0', 'n1'], 100).size).toBe(0);
  });
});

describe('the graph as a list', () => {
  const groups = [
    { id: 'a', title: 'Alpha', items: [{ id: '1', title: 'One' }, { id: '2', title: 'Two' }] },
    { id: 'b', title: 'Beta', items: [{ id: '3', title: 'Three' }] },
  ];

  it('is whole: every group with its count, every item with its place in its group (trap 11)', () => {
    render(<CanvasList label="Steps" groups={groups} selected="2" onPick={() => undefined} />);
    expect(screen.getByRole('button', { name: /Alpha/ })).toHaveAttribute('aria-expanded', 'true');
    const items = screen.getAllByRole('listitem');
    expect(items.map((item) => [item.textContent, item.getAttribute('aria-posinset'), item.getAttribute('aria-setsize')])).toEqual([
      ['One', '1', '2'], ['Two', '2', '2'], ['Three', '1', '1'],
    ]);
    expect(screen.getByRole('button', { name: 'Two' })).toHaveAttribute('aria-pressed', 'true');
  });

  it('draws only the open group of a very large list, and opens another on a press', async () => {
    const big = Array.from({ length: 30 }, (_, at) => ({ id: `g${at}`, title: `Group ${at}`, items: Array.from({ length: 10 }, (_, i) => ({ id: `g${at}-${i}`, title: `Item ${at}-${i}` })) }));
    const picked = vi.fn();
    render(<CanvasList label="Steps" groups={big} selected="g2-1" onPick={picked} />);
    expect(screen.getAllByRole('listitem')).toHaveLength(10);
    await userEvent.click(screen.getByRole('button', { name: /Group 5/ }));
    expect(screen.getAllByRole('listitem')).toHaveLength(20);
    await userEvent.click(screen.getByRole('button', { name: 'Item 5-3' }));
    expect(picked).toHaveBeenCalledWith('g5-3');
  });
});

describe('a large world below the floor', () => {
  function Host() {
    const scene = useMemo(() => syntheticScene(60, { groups: 3 }), []);
    const [selected, setSelected] = useState<string | null>(null);
    return (
      <Tooltip.Provider>
        <PositionedCanvas scene={scene} label="Sixty steps" feature="test" storeKey="sixty" selected={selected} onSelect={setSelected} />
        <output>{selected ?? 'none'}</output>
      </Tooltip.Provider>
    );
  }

  it('is laid out, then the grouped list, and a press selects', async () => {
    render(<Host />);
    const list = await screen.findByRole('group', { name: 'Sixty steps' });
    expect(within(list).getAllByRole('button', { expanded: true })).toHaveLength(3);
    await userEvent.click(within(list).getByRole('button', { name: 'Step 7' }));
    await waitFor(() => expect(screen.getByRole('status')).toHaveTextContent('n6'));
    expect(within(list).getByRole('button', { name: 'Step 7' })).toHaveAttribute('aria-pressed', 'true');
  });
});

describe('find', () => {
  const matches = [{ id: 'a', title: 'Build' }, { id: 'b', title: 'Build again' }];

  function Field(props: { at?: number; steps?: (back: boolean) => void; onDown?: () => void; onClear?: () => void }) {
    const [query, setQuery] = useState('bui');
    return (
      <CanvasFind label="Find in the graph" query={query} onQuery={setQuery} matches={matches} at={props.at ?? -1} onStep={props.steps ?? (() => undefined)}
        onClear={props.onClear ?? (() => undefined)} onDown={props.onDown ?? (() => undefined)} onPick={() => undefined} />
    );
  }

  it('says n of m, steps on Enter and Shift+Enter with the focus kept in the field, and types Shift+1 (traps 13 and 14)', async () => {
    const steps = vi.fn();
    render(<Field at={0} steps={steps} />);
    const field = screen.getByRole('searchbox', { name: 'Find in the graph' });
    expect(screen.getByRole('status')).toHaveTextContent('1 of 2');
    await userEvent.click(field);
    await userEvent.keyboard('{Enter}{Shift>}{Enter}{/Shift}');
    expect(steps.mock.calls).toEqual([[false], [true]]);
    expect(field).toHaveFocus();
    await userEvent.keyboard('{Shift>}1{/Shift}');
    // The field keeps the key (user-event does not shift a digit into a symbol): no canvas act took it.
    expect(field).toHaveValue('bui1');
  });

  it('hands the focus down on the arrow, clears on Escape, and lists the matches by name', async () => {
    const down = vi.fn();
    const clear = vi.fn();
    render(<Field at={1} onDown={down} onClear={clear} />);
    await userEvent.click(screen.getByRole('searchbox'));
    await userEvent.keyboard('{ArrowDown}{Escape}');
    expect(down).toHaveBeenCalledTimes(1);
    expect(clear).toHaveBeenCalledTimes(1);
    const results = screen.getByRole('list', { name: 'Matches' });
    expect(within(results).getByRole('button', { name: 'Build again' })).toHaveAttribute('aria-current', 'true');
  });
});
