import { useState } from 'react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import '../../locales/i18n';
import { SuppliedCanvas, type SuppliedWorld } from './SuppliedCanvas';

// CANVAS1e (D224; the canvas design §2.2, §3.3, trap 15): a positioned world the feature supplies. jsdom lays nothing out, so unless a
// width is stood in for the canvas is below its floor and the world is drawn as a pane; the viewport is `Canvas.test.tsx`'s.

const measured = vi.hoisted(() => ({ width: undefined as number | undefined }));
vi.mock('../useWidth', async (original) => ({ ...(await original<typeof import('../useWidth')>()), useWidth: () => measured.width }));

const box = (x: number, y: number) => ({ x, y, w: 100, h: 40 });
const WORLD: SuppliedWorld = {
  frame: { x: -20, y: -20, w: 460, h: 180 },
  boxes: new Map([['a', box(0, 0)], ['b', box(300, 0)], ['c', box(300, 100)], ['$', box(0, 100)]]),
  order: ['a', 'b', 'c', '$'],
  statics: new Set(['$']),
  lines: [{ id: 'a>b', from: 'a', to: 'b' }, { id: 'a>c', from: 'a', to: 'c' }, { id: '$>a', from: '$', to: 'a' }],
};

function Host({ selected: first = null, onChange = () => undefined, zoom = { min: 0.25, max: 2.5 }, findFrom = 40, findItems }: {
  selected?: string | null; onChange?: (id: string | null) => void; zoom?: { min: number; max: number } | 'off'; findFrom?: number;
  findItems?: { id: string; title: string; stands?: string }[];
}) {
  const [selected, setSelected] = useState<string | null>(first);
  return (
    <Tooltip.Provider>
      <SuppliedCanvas
        world={WORLD} label="a world" moves="a world: drag to move" fit="Show the whole world" feature="test" storeKey="world"
        selected={selected} onSelect={(id) => { setSelected(id); onChange(id); }} zoom={zoom} opening={0.8}
        nodeLabel={(id) => (id === '$' ? 'the source' : `card ${id}`)} titleOf={(id) => `card ${id}`} findLabel="find a card" findFrom={findFrom} findItems={findItems}
        renderNode={(id, state) => <span data-tier={state.tier}>{id}</span>}
        renderEdges={(context) => WORLD.lines.map((line) => (
          <g key={line.id} {...context.line(line.id)} aria-label={`line ${line.id}`}>
            <path d={`M0 0L${line.id.length * 10} 10`} stroke="currentColor" />
          </g>
        ))}
      />
      <output>{selected ?? 'none'}</output>
    </Tooltip.Provider>
  );
}

let user: ReturnType<typeof userEvent.setup>;
beforeEach(() => { user = userEvent.setup(); measured.width = undefined; });
afterEach(() => localStorage.clear());

describe('a supplied world below the canvas floor', () => {
  it('is drawn whole, every card and line a button, with one stop for the Tab key (trap 15)', () => {
    render(<Host />);
    expect(screen.getByRole('group', { name: 'a world' })).toBeInTheDocument();
    const cards = screen.getAllByRole('button', { name: /^card / });
    expect(cards).toHaveLength(3);
    const lines = screen.getAllByRole('button', { name: /^line / });
    expect(lines).toHaveLength(3);
    expect([...cards, ...lines].filter((each) => each.getAttribute('tabindex') === '0')).toEqual([cards[0]]);
    // A source that is no place is drawn and named, and is no button.
    expect(screen.getByRole('img', { name: 'the source' })).toBeInTheDocument();
  });

  it('chooses a card or a line on a press, and releases it on a second press or Escape', async () => {
    const chosen = vi.fn();
    render(<Host onChange={chosen} />);
    await user.click(screen.getByRole('button', { name: 'card b' }));
    expect(chosen).toHaveBeenLastCalledWith('b');
    expect(screen.getByRole('button', { name: 'card b' })).toHaveAttribute('aria-pressed', 'true');
    await user.click(screen.getByRole('button', { name: 'card b' }));
    expect(chosen).toHaveBeenLastCalledWith(null);
    await user.click(screen.getByRole('button', { name: 'line a>c' }));
    expect(chosen).toHaveBeenLastCalledWith('a>c');
    expect(screen.getByRole('button', { name: 'line a>c' })).toHaveAttribute('aria-pressed', 'true');
    await user.keyboard('{Escape}');
    expect(chosen).toHaveBeenLastCalledWith(null);
  });

  it('walks from a card to its line and the card beyond with →, back with ←, and chooses a line on Enter', async () => {
    render(<Host />);
    screen.getByRole('button', { name: 'card a' }).focus();
    await user.keyboard('{ArrowRight}');
    expect(screen.getByRole('button', { name: 'line a>b' })).toHaveFocus();
    expect(screen.getByRole('button', { name: 'line a>b' })).toHaveAttribute('tabindex', '0');
    await user.keyboard('{ArrowDown}');
    expect(screen.getByRole('button', { name: 'line a>c' })).toHaveFocus();
    await user.keyboard('{ArrowRight}');
    expect(screen.getByRole('button', { name: 'card c' })).toHaveFocus();
    await user.keyboard('{ArrowLeft}');
    expect(screen.getByRole('button', { name: 'line a>c' })).toHaveFocus();
    await user.keyboard('{Enter}');
    expect(screen.getByText('a>c', { selector: 'output' })).toBeInTheDocument();
    // A line out of a source that is no card is reached from the card it ends on.
    screen.getByRole('button', { name: 'card a' }).focus();
    await user.keyboard('{ArrowLeft}');
    expect(screen.getByRole('button', { name: 'line $>a' })).toHaveFocus();
  });

  it('keeps the selection and the focus when the world is drawn another way', async () => {
    const { rerender } = render(<Host selected="b" />);
    const card = screen.getByRole('button', { name: 'card b' });
    card.focus();
    rerender(<Host selected="b" zoom="off" />);
    expect(screen.getByRole('button', { name: 'card b' })).toBe(card);
    expect(card).toHaveFocus();
    expect(card).toHaveAttribute('aria-pressed', 'true');
  });
});

describe('find in a supplied world', () => {
  it('is offered from the number of cards the feature gives, lists the matches, dims the rest and chooses the match shown', async () => {
    const chosen = vi.fn();
    render(<Host findFrom={3} onChange={chosen} />);
    const field = screen.getByRole('searchbox', { name: 'find a card' });
    await user.type(field, 'card b');
    expect(screen.getByRole('list', { name: 'Matches' })).toBeInTheDocument();
    const drawn = (id: string) => document.querySelector<HTMLElement>(`[data-flow-node="${id}"]`)!;
    expect(drawn('a').className).toContain('opacity-25');
    expect(drawn('b').className).not.toContain('opacity-25');
    await user.keyboard('{Enter}');
    expect(chosen).toHaveBeenLastCalledWith('b');
    expect(field).toHaveFocus();
  });

  it('searches the items the feature folds away, selects a match with no card, and keeps the card that holds it undimmed', async () => {
    const chosen = vi.fn();
    const items = [{ id: 'a', title: 'card a' }, { id: 'b', title: 'card b' }, { id: 'c', title: 'card c' }, { id: 'deep', title: 'deep inside', stands: 'c' }];
    render(<Host findFrom={4} onChange={chosen} findItems={items} />);
    await user.type(screen.getByRole('searchbox', { name: 'find a card' }), 'deep');
    const drawn = (id: string) => document.querySelector<HTMLElement>(`[data-flow-node="${id}"]`)!;
    expect(drawn('c').className).not.toContain('opacity-25');
    expect(drawn('a').className).toContain('opacity-25');
    await user.keyboard('{Enter}');
    expect(chosen).toHaveBeenLastCalledWith('deep');
  });

  it('is not offered to a world with fewer cards', () => {
    render(<Host />);
    expect(screen.queryByRole('searchbox')).toBeNull();
  });
});

describe('a supplied world in the viewport', () => {
  beforeEach(() => { measured.width = 900; });

  it('is a named region with the feature\'s words, a Tab stop, and the size beside the tools', () => {
    render(<Host />);
    const region = screen.getByRole('region', { name: 'a world' });
    expect(region).toHaveAttribute('aria-roledescription', 'a world: drag to move');
    expect(region).toHaveAttribute('tabindex', '0');
    expect(screen.getByRole('button', { name: /^Sizing: \d+%$/ })).toBeInTheDocument();
    expect(screen.getByRole('group', { name: 'a world' })).toBeInTheDocument();
  });

  it('offers no sizing to a world fitted with zoom off', () => {
    render(<Host zoom="off" />);
    expect(screen.queryByRole('button', { name: /^Sizing/ })).toBeNull();
  });
});
