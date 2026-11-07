import { useState } from 'react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { cleanup, render as rtlRender, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import type { ReactElement } from 'react';
import i18n from '../../locales/i18n';
import { CodeMapWorld } from '../code/CodeMap';
import { MapTree } from './MapTree';
import { MapWorld } from '../MapWorld';
import type { TreeGroup } from './mapTreeModel';
import type { MapNode, Topology } from '../topology';

// One tree on both levels with one row in the tab order; each row's counts and states; below 36rem
// the list is the reading and the detail opens under its row.

const measured = vi.hoisted(() => ({ width: undefined as number | undefined }));
vi.mock('../../design/useWidth', async (original) => ({ ...(await original<typeof import('../../design/useWidth')>()), useWidth: () => measured.width }));

const render = (node: ReactElement) => rtlRender(<Tooltip.Provider>{node}</Tooltip.Provider>);
const node = (id: string, extra: Partial<MapNode> = {}): MapNode => ({ id, owns: [], accepts: [], open: 0, working: false, parked: false, sessions: 0, ...extra });

const GROUPS: TreeGroup[] = [
  { id: 'repositories', title: 'Repositories', items: [{ id: 'a', title: 'a — 1 open' }, { id: 'b', title: 'b — 0 open' }] },
  { id: 'quests', title: 'Quests', items: [{ id: 'a>b', title: 'a → b' }] },
];

function Host({ detail }: { detail?: boolean }) {
  const [selected, setSelected] = useState<string | null>(null);
  return (
    <MapTree
      label="the list" groups={GROUPS} selected={selected} onPick={(id) => setSelected(id === selected ? null : id)} onRelease={() => setSelected(null)}
      detail={detail ? <a href="#door">Open repository page</a> : undefined}
    />
  );
}

beforeEach(() => { measured.width = undefined; return i18n.changeLanguage('en'); });
afterEach(() => { cleanup(); localStorage.clear(); });

describe('the tree', () => {
  it('has one row in the tab order, and the arrows move it', async () => {
    render(<Host />);
    const user = userEvent.setup();
    const rows = () => screen.getAllByRole('treeitem');
    expect(rows().filter((row) => row.tabIndex === 0)).toHaveLength(1);
    await user.tab();
    expect(rows()[0]).toHaveFocus();
    await user.keyboard('{ArrowDown}{ArrowDown}');
    expect(screen.getByRole('treeitem', { name: /^b/ })).toHaveFocus();
    expect(rows().filter((row) => row.tabIndex === 0)).toHaveLength(1);
    await user.tab();
    expect(screen.getByRole('treeitem', { name: /^b/ })).not.toHaveFocus();
  });

  it('chooses on Enter, releases on Escape, and closes and opens a group with ← and →', async () => {
    render(<Host />);
    const user = userEvent.setup();
    await user.tab();
    await user.keyboard('{ArrowDown}{Enter}');
    expect(screen.getByRole('treeitem', { name: /^a — 1 open/ })).toHaveAttribute('aria-selected', 'true');
    await user.keyboard('{Escape}');
    expect(screen.getByRole('treeitem', { name: /^a — 1 open/ })).toHaveAttribute('aria-selected', 'false');
    await user.keyboard('{ArrowLeft}');
    expect(screen.getByRole('treeitem', { name: /^Repositories/ })).toHaveFocus();
    await user.keyboard('{ArrowLeft}');
    expect(screen.queryByRole('treeitem', { name: /^a — 1 open/ })).toBeNull();
    await user.keyboard('{ArrowRight}');
    expect(screen.getByRole('treeitem', { name: /^a — 1 open/ })).toBeInTheDocument();
  });

  it('opens the detail in place under the chosen row, and its doors follow it in the tab order', async () => {
    render(<Host detail />);
    const user = userEvent.setup();
    await user.click(screen.getByRole('treeitem', { name: /^a — 1 open/ }));
    const row = screen.getByRole('treeitem', { name: /^a — 1 open/ });
    expect(row.nextElementSibling).toContainElement(screen.getByRole('link', { name: 'Open repository page' }));
    row.focus();
    await user.tab();
    expect(screen.getByRole('link', { name: 'Open repository page' })).toHaveFocus();
  });
});

const TOPOLOGY: Topology = {
  nodes: [node('engine', { open: 3, parked: true, sessions: 1 }), node('game', { open: 1, working: true, sessions: 1 }), node('tools')],
  quests: [{ from: 'game', to: 'engine', quests: [], open: 0, live: null }],
  asks: [], chains: [], depends: [{ from: 'tools', to: 'engine' }], knowledge: [], outside: 0, circles: 1,
};

function ListOf({ selected = null as null | { kind: 'node'; id: string } }) {
  return <MapWorld topology={TOPOLOGY} selected={selected} onSelect={() => undefined} storeKey="tree" />;
}

describe('the workspace list', () => {
  it('says each repository\'s count and state, and each kind of connection\'s rows, below the floor', () => {
    measured.width = 300;
    render(<ListOf />);
    const tree = screen.getByRole('tree');
    expect(within(tree).getByRole('treeitem', { name: 'engine — 3 open · a session waits on you' })).toBeInTheDocument();
    expect(within(tree).getByRole('treeitem', { name: 'game — 1 open · a session is working there' })).toBeInTheDocument();
    expect(within(tree).getByRole('treeitem', { name: 'tools — 0 open' })).toBeInTheDocument();
    expect(within(tree).getByRole('treeitem', { name: 'game → engine · 0 quests, 0 open' })).toBeInTheDocument();
    // Below the floor the list is the reading: no way back to a graph is offered.
    expect(screen.queryByRole('button', { name: 'Show as graph' })).toBeNull();
    expect(screen.queryByRole('group', { name: 'the workspace map' })).toBeNull();
  });

  it('says the same in Chinese', async () => {
    await i18n.changeLanguage('zh');
    measured.width = 300;
    render(<ListOf />);
    expect(screen.getByRole('treeitem', { name: 'engine — 3 个未完成 · 有会话在等你' })).toBeInTheDocument();
    expect(screen.getByRole('treeitem', { name: 'game → engine · 0 个委托，0 个未完成' })).toBeInTheDocument();
  });

  it('keeps the graph from the floor up', () => {
    measured.width = 700;
    render(<ListOf />);
    expect(screen.queryByRole('tree')).toBeNull();
    expect(screen.getByRole('button', { name: 'Show as list' })).toBeInTheDocument();
  });
});

describe('the code list', () => {
  const MODULES = [
    { id: 'a', path: 'src/core/a', summary: '' }, { id: 'b', path: 'src/core/b', summary: '' },
    { id: 'c', path: 'src/web/c', summary: '' }, { id: 'lone', path: 'src/web/lone', summary: '' },
  ];
  const DEPENDENCIES = [
    { from: 'a', to: 'b', kind: 'calls' }, { from: 'b', to: 'a', kind: 'calls' }, { from: 'c', to: 'a', kind: 'calls' },
  ];

  it('is a group per folder with each module\'s uses and used-by, and a cycle a group of its own under its folder', () => {
    measured.width = 300;
    render(<CodeMapWorld repository="engine" modules={MODULES} dependencies={DEPENDENCIES} selected={null} onSelect={() => undefined} />);
    const tree = screen.getByRole('tree');
    expect(within(tree).getAllByRole('treeitem', { expanded: true }).map((row) => row.getAttribute('aria-level'))).toContain('2');
    expect(within(tree).getByRole('treeitem', { name: /^cycle of 2/ })).toBeInTheDocument();
    expect(within(tree).getByRole('treeitem', { name: 'c — uses 1 · used by 0' })).toBeInTheDocument();
    expect(within(tree).getByRole('treeitem', { name: 'lone — no dependency recorded' })).toBeInTheDocument();
  });

  it('says it in Chinese', async () => {
    await i18n.changeLanguage('zh');
    measured.width = 300;
    render(<CodeMapWorld repository="engine" modules={MODULES} dependencies={DEPENDENCIES} selected={null} onSelect={() => undefined} />);
    expect(screen.getByRole('treeitem', { name: 'lone — 没有记录依赖' })).toBeInTheDocument();
  });
});
