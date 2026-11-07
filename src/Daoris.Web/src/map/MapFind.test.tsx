import { useState } from 'react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { cleanup, fireEvent, render as rtlRender, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import type { ReactElement } from 'react';
import i18n from '../locales/i18n';
import { CodeMapWorld } from './code/CodeMap';
import { MapWorld } from './MapWorld';
import type { MapNode, Topology } from './topology';

// Find reaches the level shown from 13 items on both levels, by a repository's name and then what it
// says it owns and accepts, by a module's id and then its folder's path, saying so in the result; Enter while an input method composes
// steps nothing, so a Chinese name can be typed in the field.

const render = (node: ReactElement) => rtlRender(<Tooltip.Provider>{node}</Tooltip.Provider>);
const node = (id: string, extra: Partial<MapNode> = {}): MapNode => ({ id, owns: [], accepts: [], open: 0, working: false, parked: false, sessions: 0, ...extra });
const ids = (count: number, prefix = 'repo') => Array.from({ length: count }, (_, at) => `${prefix}-${String(at).padStart(2, '0')}`);

const topology = (count: number): Topology => ({
  nodes: [
    ...ids(count - 2).map((id) => node(id)),
    node('ledger', { owns: ['Payments', '支付流程'] }),
    node('portal', { accepts: ['refund requests'] }),
  ],
  quests: [], asks: [], chains: [], depends: [], knowledge: [], outside: 0, circles: 1,
});

const world = (count: number, onSelect = vi.fn()) => (
  <MapWorld topology={topology(count)} selected={null} onSelect={onSelect} storeKey="find" />
);

beforeEach(() => i18n.changeLanguage('en'));
afterEach(() => { cleanup(); localStorage.clear(); });

describe('find on the workspace map', () => {
  it('is offered from 13 repositories, and not on a ring of 12', () => {
    render(world(13));
    expect(screen.getByRole('searchbox')).toBeInTheDocument();
    cleanup();
    render(world(12));
    expect(screen.queryByRole('searchbox')).toBeNull();
  });

  it('matches what a repository says it owns, ignoring case, and says so in the result', async () => {
    render(world(14));
    await userEvent.type(screen.getByRole('searchbox'), 'payments');
    const results = screen.getByRole('list', { name: 'Matches' });
    expect(within(results).getAllByRole('button').map((each) => each.textContent)).toEqual(['ledger — owns Payments']);
  });

  it('matches what it says it accepts, and a name before either', async () => {
    render(world(14));
    const field = screen.getByRole('searchbox');
    await userEvent.type(field, 'refund');
    expect(within(screen.getByRole('list', { name: 'Matches' })).getByRole('button')).toHaveTextContent('portal — accepts refund requests');
    await userEvent.clear(field);
    await userEvent.type(field, 'por');
    expect(within(screen.getByRole('list', { name: 'Matches' })).getByRole('button')).toHaveTextContent(/^portal$/);
  });

  it('says it in Chinese, matching an authored declaration as written', async () => {
    await i18n.changeLanguage('zh');
    render(world(14));
    await userEvent.type(screen.getByRole('searchbox'), '支付');
    expect(within(screen.getByRole('list', { name: '匹配项' })).getByRole('button')).toHaveTextContent('ledger — 负责 支付流程');
  });

  it('steps nothing on Enter while an input method composes, and steps on the Enter after it', async () => {
    const onSelect = vi.fn();
    render(world(14, onSelect));
    const field = screen.getByRole('searchbox');
    await userEvent.type(field, 'ledger');
    fireEvent.keyDown(field, { key: 'Enter', keyCode: 229, isComposing: true });
    expect(onSelect).not.toHaveBeenCalled();
    fireEvent.keyDown(field, { key: 'Enter', keyCode: 13 });
    expect(onSelect).toHaveBeenLastCalledWith({ kind: 'node', id: 'ledger' });
  });
});

const MODULES = [
  ...ids(12, 'mod').map((id, at) => ({ id, path: `src/part-${at % 3}/${id}`, summary: '' })),
  { id: 'session', path: 'src/Billing/session', summary: '' },
  { id: 'invoice', path: 'src/Billing/invoice', summary: '' },
];

function Code({ count = MODULES.length }: { count?: number }) {
  const [selected, setSelected] = useState<string | null>(null);
  return (
    <CodeMapWorld repository="engine" modules={MODULES.slice(0, count)} dependencies={[]} selected={selected} onSelect={setSelected} />
  );
}

describe('find on the code map', () => {
  it('is offered from 13 modules, as on the workspace', () => {
    render(<Code />);
    expect(screen.getByRole('searchbox')).toBeInTheDocument();
    cleanup();
    render(<Code count={12} />);
    expect(screen.queryByRole('searchbox')).toBeNull();
  });

  it('matches a module by its id, and by its folder\'s path, which the result says', async () => {
    render(<Code />);
    const field = screen.getByRole('searchbox');
    await userEvent.type(field, 'billing');
    expect(within(screen.getByRole('list', { name: 'Matches' })).getAllByRole('button').map((each) => each.textContent))
      .toEqual(['session — in src/Billing', 'invoice — in src/Billing']);
    await userEvent.clear(field);
    await userEvent.type(field, 'inv');
    expect(within(screen.getByRole('list', { name: 'Matches' })).getByRole('button')).toHaveTextContent(/^invoice$/);
  });

  it('says it in Chinese, the path as written', async () => {
    await i18n.changeLanguage('zh');
    render(<Code />);
    await userEvent.type(screen.getByRole('searchbox'), 'billing');
    expect(within(screen.getByRole('list', { name: '匹配项' })).getAllByRole('button')[0]).toHaveTextContent('session — 位于 src/Billing');
  });

  it('steps nothing on Enter while an input method composes', async () => {
    render(<Code />);
    const field = screen.getByRole('searchbox');
    await userEvent.type(field, 'session');
    fireEvent.keyDown(field, { key: 'Enter', keyCode: 229, isComposing: true });
    expect(screen.queryByRole('button', { pressed: true })).toBeNull();
    fireEvent.keyDown(field, { key: 'Enter', keyCode: 13 });
    expect(screen.getByRole('button', { pressed: true, name: /^session/ })).toBeInTheDocument();
  });
});
