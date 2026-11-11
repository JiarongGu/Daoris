import { useState } from 'react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { act, cleanup, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import * as Tooltip from '@radix-ui/react-tooltip';

// A plugin's commands in the Plugins view over a mocked shell: its own sign-in run from its Set up tab and from the palette's ask,
// round by round over PLUGIN_COMMAND with its token asked hidden, a switch per place over PLUGIN_SWITCH, and its sign-out asked
// before a removal. The plugin is a stand-in shaped as the pull-request plugins sign in.

const { invoke, notifyReady } = vi.hoisted(() => ({ invoke: vi.fn(), notifyReady: vi.fn(() => Promise.resolve()) }));

vi.mock('@shenora/react', async () => {
  const actual = await vi.importActual<typeof import('@shenora/react')>('@shenora/react');
  return {
    ...actual,
    isShenoraAvailable: () => true,
    getBridge: () => ({ isAvailable: true, invoke, notifyReady }),
    useShenora: () => ({ isAvailable: true, bridge: { notifyReady } }),
    useShenoraEvent: () => {},
    useWindowMaximized: () => false,
  };
});

import '../locales/i18n';
import { useListPanes } from '../work/frame/listPanes';
import { ViewFrame } from '../work/frame/ViewFrame';
import { askCommand } from './commandAsks';
import { usePluginsView } from './PluginsView';

const RUN = 'fedcba9876543210fedcba9876543210';

const BOARDS = {
  id: 'acme-boards', name: 'Acme boards', version: '3.0.1', description: 'Opens pull requests on the boards service.',
  enabled: true, problem: null, harnesses: [], points: ['work/perform'], running: false,
  folder: 'C:/somewhere/data/plugins/acme-boards', data: 'C:/somewhere/data/plugins/.data/acme-boards',
  interfaces: {
    commands: [
      { name: 'login', title: 'Sign in', about: 'Keeps a personal access token in its own storage.', interactive: true },
      { name: 'status', title: 'Whether it is signed in', about: 'Says who the token signs in as.', tool: true },
      { name: 'logout', title: 'Sign out', about: 'Forgets the token.', writes: true },
    ],
    storage: { for: 'The token its sign-in keeps.', forget: 'logout' },
    views: [], refused: [], unknown: [],
  },
  switches: [],
};

let rounds: Record<string, unknown>[];
let forgot: 'confirmed' | 'not-confirmed' | null = null;
function wire() {
  invoke.mockImplementation(async (module: string, type: string, args?: { payload?: Record<string, unknown> }) => {
    if (module === 'DAORIS.DRIVER' && type === 'PLUGINS') return { folder: 'C:/somewhere/data/plugins', plugins: [BOARDS], kit: { points: [] }, offers: [] };
    if (type === 'PLUGIN_COMMAND') return rounds.shift() ?? { run: RUN, said: [], exit: 0 };
    if (type === 'PLUGIN_SWITCH') return { ...args?.payload, switches: [] };
    if (type === 'PLUGIN_ACTION') return { id: 'acme-boards', action: 'remove', data: null, forgot };
    return {};
  });
}
const calls = (type: string) => invoke.mock.calls.filter((call) => call[1] === type).map((call) => (call[2] as { payload?: unknown })?.payload);

function Plugins({ notify }: { notify: (text: string, kind?: 'ok' | 'error') => void }) {
  const lists = useListPanes();
  const [over, setOver] = useState(false);
  const layout = usePluginsView({
    active: true, chosen: lists.pane('plugins').chosen, onChoose: (item) => lists.choose('plugins', item), notify,
  });
  return <ViewFrame layout={layout} lists={lists} over={over} onOver={setOver} />;
}

function show() {
  const notify = vi.fn();
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  render(<QueryClientProvider client={client}><Tooltip.Provider><Plugins notify={notify} /></Tooltip.Provider></QueryClientProvider>);
  return { notify };
}

const open = async () => {
  await userEvent.click(await within(screen.getByRole('complementary')).findByRole('button', { name: /Acme boards/ }));
  return screen.findByRole('heading', { level: 1, name: 'Acme boards' });
};

describe("a plugin's commands in the Plugins view", () => {
  beforeEach(() => {
    rounds = [];
    forgot = null;
    wire();
  });
  afterEach(() => {
    cleanup();
    invoke.mockReset();
    window.localStorage.clear();
  });

  it('signs it in from its Set up tab: the token asked hidden, the rounds sent whole, and the catalogue asked again at its end', async () => {
    rounds = [
      { run: RUN, said: ['Paste a personal access token with Code read and write.'], ask: { name: 'token', prompt: 'Token', masked: true }, exit: 0 },
      { run: RUN, said: ['Signed in as the token\'s own identity.'], exit: 0 },
    ];
    show();
    await open();
    expect(screen.getByRole('tab', { name: 'Set up' })).toHaveAttribute('aria-selected', 'true');
    const asked = calls('PLUGINS').length;

    await userEvent.click(screen.getByRole('button', { name: 'Run Sign in' }));
    const card = await screen.findByRole('region', { name: 'Acme boards: Sign in' });
    expect(calls('PLUGIN_COMMAND')[0]).toEqual({ id: 'acme-boards', command: 'login', args: {}, answers: {}, place: 'machine', language: 'en' });
    const field = await within(card).findByLabelText('Token');
    expect(field).toHaveAttribute('type', 'password');
    await userEvent.type(field, 'a-token-value');
    await userEvent.click(within(card).getByRole('button', { name: 'Go on' }));

    await within(card).findByText('Done.');
    expect(calls('PLUGIN_COMMAND')[1]).toEqual({
      id: 'acme-boards', command: 'login', run: RUN, args: {}, answers: { token: 'a-token-value' }, place: 'machine', language: 'en',
    });
    expect(card.textContent).not.toContain('a-token-value');
    await waitFor(() => expect(calls('PLUGINS').length).toBeGreaterThan(asked));
  });

  it('opens the plugin\'s page at its round card from the palette\'s ask', async () => {
    rounds = [{ run: RUN, said: ['Signed in as builder.'], exit: 0, then: 'stay' }];
    show();
    await screen.findByRole('complementary');
    act(() => askCommand('acme-boards', 'status'));
    const card = await screen.findByRole('region', { name: 'Acme boards: Whether it is signed in' });
    await within(card).findByText('Signed in as builder.');
    expect(calls('PLUGIN_COMMAND')[0]).toEqual(expect.objectContaining({ id: 'acme-boards', command: 'status' }));
  });

  it('switches its commands off at this machine over PLUGIN_SWITCH', async () => {
    show();
    await open();
    await userEvent.click(screen.getByRole('checkbox', { name: 'Commands at This machine' }));
    await waitFor(() => expect(calls('PLUGIN_SWITCH')).toEqual([{ id: 'acme-boards', what: 'commands', place: 'machine', on: false }]));
  });

  it('removes it over one press, the driver asking it to sign out, and says whether it confirmed', async () => {
    forgot = 'confirmed';
    const { notify } = show();
    await open();
    await userEvent.click(within(screen.getByRole('main')).getByRole('button', { name: 'Remove…' }));
    await userEvent.click(screen.getByRole('button', { name: 'Remove plugin' }));
    await waitFor(() => expect(calls('PLUGIN_ACTION')).toEqual([{ id: 'acme-boards', action: 'remove' }]));
    expect(calls('PLUGIN_COMMAND')).toEqual([]);
    expect(notify).toHaveBeenCalledWith('acme-boards says it signed out at its service.');
  });
});
