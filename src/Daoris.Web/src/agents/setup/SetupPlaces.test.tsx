import { useState } from 'react';
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
import { PLACES } from './setupFixtures';

function Place() {
  const lists = useListPanes();
  const modes = useAgentsMode(lists);
  const [chosen, setChosen] = useState<string | null>(null);
  const [over, setOver] = useState(false);
  const layout = useAgentsView({ active: true, modes, chosen, onChoose: setChosen, notify: () => {} });
  return <ViewFrame layout={layout} lists={lists} over={over} onOver={setOver} />;
}

type Asked = { limit?: number; search?: string; needsLook?: boolean; fold?: string; foldAfter?: string };
const asked = (): Asked[] => invoke.mock.calls
  .filter(([, type]) => type === 'PLACES')
  .map(([, , options]) => (options as { payload: Asked }).payload);

describe('The Agents place, Setup mode', () => {
  beforeEach(() => {
    window.localStorage.clear();
    window.localStorage.setItem('daoris.agents.setup', 'on');
    window.localStorage.setItem('daoris.list.agents.mode', 'setup');
    i18n.changeLanguage('en');
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => respond(String(input))));
    invoke.mockImplementation(async (module: string, type: string, options?: { payload?: Asked }) => {
      if (module !== 'DAORIS.DRIVER') return WIRING;
      if (type === 'STATE') return DRIVER_STATE;
      if (type === 'HARNESSES') return { settingsPath: 'x', adapter: 'claude-code', harnesses: [] };
      if (type === 'PLACES') return PLACES(options?.payload ?? {});
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
    const list = await screen.findByRole('list', { name: 'Places' });
    expect(within(list).getByRole('button', { name: /This machine/ })).toBeTruthy();
    const workspace = within(list).getByRole('list', { name: 'aurora' });
    expect(within(workspace).getByRole('button', { name: /aurora\s*2 driven/ })).toBeTruthy();
    expect(within(workspace).getByRole('button', { name: /engine/ })).toBeTruthy();
    expect(within(workspace).getByRole('button', { name: /41 imported, not driven/ })).toHaveAttribute('aria-expanded', 'false');
    // The repository that needs a look says why, from the driver's reasons.
    expect(within(workspace).getByRole('button', { name: /site.*No checkout on this machine/ })).toBeTruthy();
  });

  it("shows a repository's head: driving, held, trees, and the changes of its own", async () => {
    show(<Place />);
    await userEvent.click(await screen.findByRole('button', { name: /engine/ }));
    const facts = await screen.findByText('Driving');
    const head = facts.closest('dl')!;
    expect(within(head).getByText('Driving').nextSibling).toHaveTextContent('Yes');
    expect(within(head).getByText('Held').nextSibling).toHaveTextContent('No');
    expect(within(head).getByText('Trees').nextSibling).toHaveTextContent('Its own tree for each session');
    expect(within(head).getByText('Set here').nextSibling).toHaveTextContent('2 changes of your own');
    expect(screen.getByRole('heading', { name: 'engine' })).toBeTruthy();
    expect(within(head).getByText('Workflow follows').nextSibling).toHaveTextContent('Merge after you accept differing in accept `automatic`, method `squash`');
  });

  it('says a place that follows the defaults, and what a place needs a look for', async () => {
    show(<Place />);
    await userEvent.click(await screen.findByRole('button', { name: /site/ }));
    const head = (await screen.findByText('Driving')).closest('dl')!;
    expect(within(head).getByText('Set here').nextSibling).toHaveTextContent('Follows the defaults');
    expect(within(head).getByText('Workflow follows').nextSibling).toHaveTextContent('Integrated into the line with nothing different');
    expect(within(head).getByText('Needs a look').nextSibling).toHaveTextContent('No checkout on this machine');
  });

  it("opens a workspace's imported repositories as a paged read", async () => {
    show(<Place />);
    await userEvent.click(await screen.findByRole('button', { name: /41 imported, not driven/ }));
    expect(await screen.findByRole('button', { name: /archive-1/ })).toBeTruthy();
    expect(asked().at(-1)).toMatchObject({ fold: 'aurora' });
    expect(screen.getByText('2 of 41')).toBeTruthy();

    await userEvent.click(screen.getByRole('button', { name: 'Next page' }));
    expect(await screen.findByRole('button', { name: /archive-3/ })).toBeTruthy();
    expect(asked().at(-1)).toMatchObject({ fold: 'aurora', foldAfter: 'aurora/archive-2' });
    await userEvent.click(screen.getByRole('button', { name: 'First page' }));
    await waitFor(() => expect(screen.queryByRole('button', { name: /archive-3/ })).toBeNull());

    await userEvent.click(screen.getByRole('button', { name: /archive-1/ }));
    const head = (await screen.findByText('Driving')).closest('dl')!;
    expect(within(head).getByText('Driving').nextSibling).toHaveTextContent('No');
  });

  it('narrows by a search and by Needs a look, asked of the driver', async () => {
    show(<Place />);
    await screen.findByRole('list', { name: 'Places' });
    await userEvent.type(screen.getByRole('searchbox', { name: 'Search places' }), 'sit');
    await waitFor(() => expect(asked().at(-1)).toMatchObject({ search: 'sit', foldSearch: 'sit' }));
    await userEvent.click(screen.getByRole('checkbox', { name: 'Needs a look' }));
    await waitFor(() => expect(asked().at(-1)).toMatchObject({ search: 'sit', needsLook: true }));
    await waitFor(() => expect(screen.queryByRole('button', { name: /engine/ })).toBeNull());
    expect(screen.getByRole('button', { name: /site/ })).toBeTruthy();
  });

  it('is named in Chinese', async () => {
    i18n.changeLanguage('zh');
    show(<Place />);
    expect(await screen.findByRole('radio', { name: '配置' })).toBeChecked();
    expect(await screen.findByRole('button', { name: /41 个已导入，未驱动/ })).toBeTruthy();
    expect(within(screen.getByRole('list', { name: '位置' })).getByRole('button', { name: /^本机$/ })).toBeTruthy();
  });

  it('stays dark until lit: the mode shows the Roles sections', async () => {
    window.localStorage.removeItem('daoris.agents.setup');
    show(<Place />);
    expect(await screen.findByRole('list', { name: 'Roles' })).toBeTruthy();
    expect(screen.queryByRole('list', { name: 'Places' })).toBeNull();
    expect(asked()).toEqual([]);
  });
});
