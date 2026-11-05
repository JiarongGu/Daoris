import { useEffect, useState } from 'react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { cleanup, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

// AGENTSPLACE1e (the Agents-place design §3): the Setup mode's tree of places and each place's head, read from PLACES, lit by its flag.

const { invoke, notifyReady, eventHandlers } = vi.hoisted(() => ({
  invoke: vi.fn(),
  notifyReady: vi.fn(() => Promise.resolve()),
  eventHandlers: new Map<string, (payload: unknown) => void>(),
}));

vi.mock('@shenora/react', () => ({
  isShenoraAvailable: () => true,
  getBridge: () => ({ isAvailable: true, invoke, notifyReady }),
  useShenora: () => ({ isAvailable: true, bridge: { notifyReady } }),
  useShenoraEvent: (module: string, type: string, handler: (payload: unknown) => void) => {
    eventHandlers.set(`${module}.${type}`, handler);
  },
}));

import i18n from '../../locales/i18n';
import { DRIVER_STATE, respond, show, WIRING } from '../../test/shellHarness';
import { useListPanes } from '../../work/frame/listPanes';
import { ViewFrame } from '../../work/frame/ViewFrame';
import { useAgentsMode, useAgentsView } from '../AgentsView';
import type { StructurePlaceAsk } from '../../data/wiring';
import { CATALOGUE, CHAIN, PLACES } from './setupFixtures';

/** A section's body as the application hands it: it says when it is drawn, which only an open section does. */
const mounted: string[] = [];
function Mount({ name }: { name: string }) {
  useEffect(() => { mounted.push(name); }, [name]);
  return <p>{name} body</p>;
}

function Place() {
  const lists = useListPanes();
  const modes = useAgentsMode(lists);
  const [chosen, setChosen] = useState<string | null>(null);
  const [over, setOver] = useState(false);
  const layout = useAgentsView({
    active: true, modes, chosen, onChoose: setChosen, notify: () => {},
    bodies: () => ({ workflow: <Mount name="workflow" />, sessions: <Mount name="sessions" /> }),
  });
  return <ViewFrame layout={layout} lists={lists} over={over} onOver={setOver} />;
}

type Asked = { limit?: number; search?: string; needsLook?: boolean; fold?: string; foldAfter?: string };
const asked = (): Asked[] => invoke.mock.calls
  .filter(([, type]) => type === 'PLACES')
  .map(([, , options]) => (options as { payload: Asked }).payload);
const asks = (type: string): Record<string, unknown>[] => invoke.mock.calls
  .filter(([, called]) => called === type)
  .map(([, , options]) => (options as { payload: Record<string, unknown> }).payload);

describe('The Agents place, Setup mode', () => {
  beforeEach(() => {
    window.localStorage.clear();
    window.localStorage.setItem('daoris.list.agents.mode', 'setup');
    i18n.changeLanguage('en');
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => respond(String(input))));
    mounted.length = 0;
    invoke.mockImplementation(async (module: string, type: string, options?: { payload?: Asked & { chain?: StructurePlaceAsk } }) => {
      if (module !== 'DAORIS.DRIVER') return WIRING;
      if (type === 'STATE') return DRIVER_STATE;
      if (type === 'HARNESSES') return { settingsPath: 'x', adapter: 'claude-code', harnesses: [] };
      if (type === 'PLACES') return PLACES(options?.payload ?? {});
      if (type === 'ROLE_STRUCTURE') return CHAIN(options?.payload?.chain ?? {});
      if (type === 'ROLES') return CATALOGUE;
      return undefined;
    });
  });
  afterEach(() => {
    cleanup();
    window.localStorage.clear();
    vi.unstubAllGlobals();
    invoke.mockReset();
  });

  it('lists this machine, each workspace with its driven repositories, and the rest folded into one row', async () => {
    show(<Place />);
    const tree = await screen.findByRole('tree', { name: 'Places' });
    expect(within(tree).getByRole('treeitem', { name: /This machine/ })).toHaveAttribute('aria-level', '1');
    const workspace = within(tree).getByRole('treeitem', { name: /aurora\s*2 driven/ });
    expect(workspace).toHaveAttribute('aria-level', '1');
    expect(workspace).toHaveAttribute('aria-expanded', 'true');
    const engine = within(tree).getByRole('treeitem', { name: /engine/ });
    expect(engine).toHaveAttribute('aria-level', '2');
    // A repository's second line says how it lands.
    expect(engine).toHaveTextContent(/Merge after you accept, 2 options of its own/);
    expect(within(tree).getByRole('treeitem', { name: /41 imported, not driven/ })).toHaveAttribute('aria-expanded', 'false');
    // The repository that needs a look says why, from the driver's first reason, after a warning mark.
    expect(within(tree).getByRole('treeitem', { name: /site.*⚠ No checkout on this machine/ })).toBeTruthy();
    // Exactly one row is in the tab order.
    expect(within(tree).getAllByRole('treeitem').filter((each) => each.tabIndex === 0)).toHaveLength(1);
  });

  it("shows a repository's page: its head, and three sections folded to lines naming their values and where each stands", async () => {
    show(<Place />);
    await userEvent.click(await screen.findByRole('treeitem', { name: /engine/ }));
    expect(await screen.findByRole('heading', { level: 1, name: 'engine' })).toBeTruthy();
    expect(screen.getByText('Driven')).toBeTruthy();
    expect(screen.getByText(/Merge after you accept, 2 options of its own · its own tree per session/)).toBeTruthy();
    // Workflow first on a repository, open on arrival; the others folded to their lines.
    expect(screen.getByRole('button', { name: 'Workflow' })).toHaveAttribute('aria-expanded', 'true');
    const roles = screen.getByRole('button', { name: 'Roles' });
    expect(roles).toHaveAttribute('aria-expanded', 'false');
    expect(roles).toHaveAccessibleDescription('Building: Engine build · 8 more as aurora has');
    const sessions = screen.getByRole('button', { name: 'Sessions' });
    expect(sessions).toHaveAttribute('aria-expanded', 'false');
    expect(sessions).toHaveAccessibleDescription('Pauses as today (default) · English (from this machine) · helpers ask you (from aurora)');
  });

  it('says a place that follows the defaults, and what it needs a look for', async () => {
    show(<Place />);
    await userEvent.click(await screen.findByRole('treeitem', { name: /site/ }));
    expect(await screen.findByText(/Integrated into the line/)).toBeTruthy();
    expect(screen.getByText(/⚠ No checkout on this machine/)).toBeTruthy();
    expect(screen.getByRole('button', { name: 'Roles' })).toHaveAccessibleDescription('All 9 works as aurora has');
  });

  it('reads nothing for a folded section: its body is not drawn, so it asks no route until it is opened', async () => {
    show(<Place />);
    await userEvent.click(await screen.findByRole('treeitem', { name: /engine/ }));
    await screen.findByRole('heading', { level: 1, name: 'engine' });
    expect(mounted).toEqual(['workflow']);
    expect(asks('ROLE_STRUCTURE')).toEqual([]);
    await userEvent.click(screen.getByRole('button', { name: 'Roles' }));
    await waitFor(() => expect(asks('ROLE_STRUCTURE').length).toBeGreaterThan(0));
    expect(asks('ROLE_STRUCTURE').at(-1)).toMatchObject({ chain: { workspace: 'aurora', repository: 'engine' } });
    expect(mounted).toEqual(['workflow']);
    await userEvent.click(screen.getByRole('button', { name: 'Sessions' }));
    await waitFor(() => expect(mounted).toEqual(['workflow', 'sessions']));
  });

  it('keeps the sections as the person left them, across a visit to another place', async () => {
    show(<Place />);
    await userEvent.click(await screen.findByRole('treeitem', { name: /engine/ }));
    await userEvent.click(await screen.findByRole('button', { name: 'Roles' }));
    await userEvent.click(screen.getByRole('button', { name: 'Workflow' }));
    expect(screen.getByRole('button', { name: 'Roles' })).toHaveAttribute('aria-expanded', 'true');
    expect(screen.getByRole('button', { name: 'Workflow' })).toHaveAttribute('aria-expanded', 'false');
    await userEvent.click(screen.getByRole('treeitem', { name: /site/ }));
    await waitFor(() => expect(screen.getByRole('heading', { level: 1, name: 'site' })).toBeTruthy());
    expect(screen.getByRole('button', { name: 'Workflow' })).toHaveAttribute('aria-expanded', 'true');
    await userEvent.click(screen.getByRole('treeitem', { name: /engine/ }));
    await waitFor(() => expect(screen.getByRole('heading', { level: 1, name: 'engine' })).toBeTruthy());
    expect(screen.getByRole('button', { name: 'Roles' })).toHaveAttribute('aria-expanded', 'true');
    expect(screen.getByRole('button', { name: 'Workflow' })).toHaveAttribute('aria-expanded', 'false');
  });

  it("opens the parent's page through the head's door, and Back returns to the page it came from", async () => {
    show(<Place />);
    await userEvent.click(await screen.findByRole('treeitem', { name: /engine/ }));
    await userEvent.click(await screen.findByRole('button', { name: /aurora ›/ }));
    expect(await screen.findByRole('heading', { level: 1, name: 'aurora' })).toBeTruthy();
    expect(screen.getByText(/2 of 43 driven/)).toBeTruthy();
    await userEvent.click(screen.getByRole('button', { name: '← Back to engine · Workflow' }));
    expect(await screen.findByRole('heading', { level: 1, name: 'engine' })).toBeTruthy();
    expect(screen.queryByRole('button', { name: /← Back/ })).toBeNull();
  });

  it('opens an imported repository on its detail, as a page not driven, with the same three sections', async () => {
    show(<Place />);
    await userEvent.click(await screen.findByRole('treeitem', { name: /41 imported, not driven/ }));
    await userEvent.click(await screen.findByRole('treeitem', { name: /archive-1/ }));
    expect(await screen.findByRole('heading', { level: 1, name: 'archive-1' })).toBeTruthy();
    expect(await screen.findByText('Not driven · checkout here')).toBeTruthy();
    expect(asks('PLACES').at(-1)).toMatchObject({ detail: { workspace: 'aurora', repository: 'archive-1' } });
    for (const name of ['Workflow', 'Roles', 'Sessions']) expect(screen.getByRole('button', { name })).toBeTruthy();
    expect(screen.queryByRole('button', { name: 'Hold' })).toBeNull();
  });

  it("opens a workspace's imported repositories as a paged read", async () => {
    show(<Place />);
    await userEvent.click(await screen.findByRole('treeitem', { name: /41 imported, not driven/ }));
    expect(await screen.findByRole('treeitem', { name: /archive-1/ })).toBeTruthy();
    expect(asked().at(-1)).toMatchObject({ fold: 'aurora' });
    expect(screen.getByText('2 of 41')).toBeTruthy();

    await userEvent.click(screen.getByRole('button', { name: 'Next page' }));
    expect(await screen.findByRole('treeitem', { name: /archive-3/ })).toBeTruthy();
    expect(asked().at(-1)).toMatchObject({ fold: 'aurora', foldAfter: 'aurora/archive-2' });
    await userEvent.click(screen.getByRole('button', { name: 'First page' }));
    await waitFor(() => expect(screen.queryByRole('treeitem', { name: /archive-3/ })).toBeNull());

    await userEvent.click(screen.getByRole('treeitem', { name: /archive-1/ }));
    expect(await screen.findByText('Not driven · checkout here')).toBeTruthy();
  });

  it('narrows by a search and by Needs a look, asked of the driver', async () => {
    show(<Place />);
    await screen.findByRole('tree', { name: 'Places' });
    // The chip counts the places in hand that need a look: here, the one repository.
    expect(screen.getByRole('button', { name: 'Needs attention 1' })).toHaveAttribute('aria-pressed', 'false');
    await userEvent.type(screen.getByRole('searchbox', { name: 'Search places' }), 'sit');
    await waitFor(() => expect(asked().at(-1)).toMatchObject({ search: 'sit', foldSearch: 'sit' }));
    await userEvent.click(screen.getByRole('button', { name: /Needs attention/ }));
    await waitFor(() => expect(asked().at(-1)).toMatchObject({ search: 'sit', needsLook: true }));
    await waitFor(() => expect(screen.queryByRole('treeitem', { name: /engine/ })).toBeNull());
    expect(screen.getByRole('treeitem', { name: /site/ })).toBeTruthy();
    expect(screen.getByRole('button', { name: /Needs attention/ })).toHaveAttribute('aria-pressed', 'true');
  });

  it('is walked by the keys of the WAI tree pattern, and a workspace is opened apart from being chosen', async () => {
    show(<Place />);
    const tree = await screen.findByRole('tree', { name: 'Places' });
    const item = (name: RegExp) => within(tree).getByRole('treeitem', { name });
    const machine = item(/This machine/);
    machine.focus();
    await userEvent.keyboard('{ArrowDown}');
    expect(item(/aurora\s*2 driven/)).toHaveFocus();
    await userEvent.keyboard('{ArrowRight}');
    expect(item(/engine/)).toHaveFocus();
    await userEvent.keyboard('{End}');
    expect(item(/41 imported, not driven/)).toHaveFocus();
    await userEvent.keyboard('{ArrowLeft}');
    expect(item(/aurora\s*2 driven/)).toHaveFocus();
    // Left on an open workspace closes it, and chooses nothing.
    await userEvent.keyboard('{ArrowLeft}');
    expect(item(/aurora\s*2 driven/)).toHaveAttribute('aria-expanded', 'false');
    expect(within(tree).queryByRole('treeitem', { name: /engine/ })).toBeNull();
    expect(screen.queryByRole('heading', { level: 1, name: 'aurora' })).toBeNull();
    await userEvent.keyboard('{ArrowRight}{ArrowRight}{Enter}');
    expect(await screen.findByRole('heading', { level: 1, name: 'engine' })).toBeTruthy();
    expect(item(/engine/)).toHaveAttribute('aria-selected', 'true');
    await userEvent.keyboard('{Home}');
    expect(machine).toHaveFocus();
    // The fold opens with Right, as an expanded row, and Enter on it toggles it rather than choosing a place.
    await userEvent.keyboard('{End}{ArrowRight}');
    expect(await screen.findByRole('treeitem', { name: /archive-1/ })).toHaveAttribute('aria-level', '3');
    expect(item(/41 imported, not driven/)).toHaveAttribute('aria-expanded', 'true');
  });

  it("opens a workspace by its chevron without choosing it", async () => {
    show(<Place />);
    const tree = await screen.findByRole('tree', { name: 'Places' });
    const workspace = within(tree).getByRole('treeitem', { name: /aurora\s*2 driven/ });
    await userEvent.click(workspace.querySelector('span[aria-hidden]')!);
    expect(workspace).toHaveAttribute('aria-expanded', 'false');
    expect(screen.queryByRole('heading', { level: 1, name: 'aurora' })).toBeNull();
  });

  it('is named in Chinese', async () => {
    i18n.changeLanguage('zh');
    show(<Place />);
    expect(await screen.findByRole('radio', { name: '配置' })).toBeChecked();
    expect(await screen.findByRole('treeitem', { name: /41 个已导入，未驱动/ })).toBeTruthy();
    expect(within(screen.getByRole('tree', { name: '位置' })).getByRole('treeitem', { name: /^本机$/ })).toBeTruthy();
  });
});
