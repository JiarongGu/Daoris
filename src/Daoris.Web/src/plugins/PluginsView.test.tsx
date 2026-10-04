import { useState } from 'react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { act, cleanup, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import * as Tooltip from '@radix-ui/react-tooltip';

// The Plugins view on the frame (PLUGUI1b, D119 §3), over a mocked shell: the list, the page, the offer's page and the
// kit's drawer, each act landing on the driver's routes, and what the view remembers. The whole window, at its foot,
// for the doors into the view: the activity bar's place and the Daoris menu.

const { invoke, notifyReady } = vi.hoisted(() => ({
  invoke: vi.fn(),
  notifyReady: vi.fn(() => Promise.resolve()),
}));

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

import '../i18n';
import { App } from '../App';
import { AT_START } from '../setupGuide';
import { WorkspaceScopeProvider } from '../scope';
import { useListPanes } from '../work/listPanes';
import { ViewFrame } from '../work/ViewFrame';
import { useDoor } from '../test/door';
import { usePluginsView } from './PluginsView';

const GATE = {
  id: 'acme.gate', name: 'Acme gate', version: '1.2.0', description: 'Holds quests overnight.',
  enabled: true, problem: null, harnesses: ['acme-agent'], points: ['quest/consider'], running: true,
  folder: 'C:/somewhere/data/plugins/acme.gate', data: 'C:/somewhere/data/plugins/.data/acme.gate',
  source: { kind: 'folder', folder: 'C:/somewhere/checkouts/plugins/acme.gate' },
};
const FUTURE = {
  id: 'future', name: 'Future', version: '', description: '', enabled: true,
  problem: 'needs plugin API 99, and this build speaks 1.', harnesses: [], points: [], running: false,
  folder: 'C:/somewhere/data/plugins/future', data: 'C:/somewhere/data/plugins/.data/future',
};
const BROWSER_OFFER = {
  id: 'in-app-browser', name: 'In-app browser', version: '0.1.0', description: 'Daoris\'s browser for every session.',
  harnesses: [], points: [], servers: ['browser'], needs: [], installed: false,
};
const BROWSER = {
  id: 'in-app-browser', name: 'In-app browser', version: '0.1.0', description: 'Daoris\'s browser for every session.',
  enabled: true, problem: null, harnesses: [], points: [], running: false,
  folder: 'C:/somewhere/data/plugins/in-app-browser', data: 'C:/somewhere/data/plugins/.data/in-app-browser',
  source: { kind: 'offer', offer: 'in-app-browser' },
};
const KIT = { points: [{ name: 'quest/consider', kind: 'decision' }, { name: 'session/ended', kind: 'observation' }] };

/** What `PLUGINS` answers now; a test changes it as the machine changes. */
let catalog: Record<string, unknown>;
const answer = (plugins: unknown[], offers: unknown[] = [BROWSER_OFFER]) => {
  catalog = { folder: 'C:/somewhere/data/plugins', plugins, kit: KIT, offers };
};

/** The driver's routes, as the mocked shell answers them; `routes` overrides one. */
let routes: Record<string, (payload: Record<string, unknown>) => unknown>;
function wire() {
  invoke.mockImplementation(async (module: string, type: string, args?: { payload?: Record<string, unknown> }) => {
    if (module === 'DAORIS.DRIVER' && type === 'PLUGINS') return catalog;
    const route = routes[type];
    if (route) return route(args?.payload ?? {});
    if (module === 'DAORIS.REMOTES') return { path: 'remotes.json', fromEnvironment: false, remotes: [] };
    if (module === 'DAORIS.DRIVER' && type === 'STATE') {
      return { drivable: [], holds: [], trees: [], running: [], notify: false, strikes: 3, forgiven: {} };
    }
    return {};
  });
}

const calls = (type: string) => invoke.mock.calls.filter((call) => call[1] === type).map((call) => (call[2] as { payload?: unknown })?.payload);

/** The view alone, on the browser's frame of list and main area, with the application's list memory. */
function Plugins({ notify, onAsk, door }: {
  notify: (text: string, kind?: 'ok' | 'error') => void;
  onAsk?: (message: string) => void;
  /** The plugin a door names as it opens the view (`useDoor`). */
  door?: string;
}) {
  const lists = useListPanes();
  const [over, setOver] = useState(false);
  useDoor(lists, 'plugins', door);
  const layout = usePluginsView({
    active: true, chosen: lists.pane('plugins').chosen, onChoose: (item) => lists.choose('plugins', item), notify, onAsk,
  });
  return <ViewFrame layout={layout} lists={lists} over={over} onOver={setOver} />;
}

function show(over: { onAsk?: (message: string) => void; door?: string } = {}) {
  const notify = vi.fn();
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  render(
    <QueryClientProvider client={client}>
      <Tooltip.Provider><Plugins notify={notify} {...over} /></Tooltip.Provider>
    </QueryClientProvider>,
  );
  return { notify };
}

const list = () => screen.getByRole('complementary');
// Found afresh each time: the main area is a new element as it goes from loading to a page.
const main = () => screen.getByRole('main');
const page = (name: string) => screen.findByRole('heading', { level: 1, name });

describe('the Plugins view', () => {
  beforeEach(() => {
    answer([GATE, FUTURE]);
    routes = {};
    wire();
  });
  afterEach(() => {
    cleanup();
    invoke.mockReset();
    window.localStorage.clear();
  });

  it('lists the plugins as a catalogue, waiting on you first, and says how to choose with nothing chosen', async () => {
    const onAsk = vi.fn();
    show({ onAsk });

    // The catalogue (D140 §2): the installed, the one waiting on you first, then Daoris's own not installed.
    expect(await within(list()).findByText('Installed (2)')).toBeInTheDocument();
    const rows = within(within(list()).getByText('Installed (2)').closest('section')!).getAllByRole('button');
    expect(rows.map((row) => (row.textContent?.includes('Future') ? 'Future' : 'Acme gate'))).toEqual(['Future', 'Acme gate']);
    expect(within(list()).getByText("Daoris's own plugins (1)")).toBeInTheDocument();
    expect(within(main()).getByText('Choose a plugin')).toBeInTheDocument();

    // Asking comes first: a plugin is made as an ask, with its tests (PLUG9). It opens Ask Daoris on a whole message.
    await userEvent.click(within(main()).getByRole('button', { name: 'Ask Daoris for a plugin' }));
    expect(onAsk).toHaveBeenCalledWith(
      "I'd like a new plugin for Daoris. Ask me what it should do and where it speaks, then propose it as an ask at the repository that holds plugins.");
  });

  it('opens the chosen plugin\'s page, and remembers the choice', async () => {
    show();

    await userEvent.click(await within(list()).findByRole('button', { name: /Acme gate/ }));
    expect(await page('Acme gate')).toBeInTheDocument();
    expect(within(list()).getByRole('button', { name: /Acme gate/ })).toHaveAttribute('aria-current', 'true');
    expect(window.localStorage.getItem('daoris.list.plugins.chosen')).toBe('acme.gate');
  });

  it('opens on the plugin it remembers', async () => {
    window.localStorage.setItem('daoris.list.plugins.chosen', 'acme.gate');
    show();
    expect(await page('Acme gate')).toBeInTheDocument();
  });

  it('switches a plugin off at the driver, says so, and asks for the catalogue again', async () => {
    routes.PLUGIN_ACTION = ({ id, action }) => ({ id, action, data: null });
    window.localStorage.setItem('daoris.list.plugins.chosen', 'acme.gate');
    const { notify } = show();

    await userEvent.click(within(await screenHeader()).getByRole('button', { name: 'Turn off' }));
    await waitFor(() => expect(calls('PLUGIN_ACTION')).toEqual([{ id: 'acme.gate', action: 'disable' }]));
    await waitFor(() => expect(notify).toHaveBeenCalledWith(expect.stringMatching(/^acme\.gate is off/)));
    await waitFor(() => expect(calls('PLUGINS').length).toBeGreaterThan(1));
  });

  it('updates by two presses: Update… shows what would change, and Update now makes it', async () => {
    routes.PLUGIN_UPDATE = ({ id, apply }) => (apply
      ? { id, applied: true, changes: [] }
      : { id, applied: false, changes: [{ what: 'version', was: '1.2.0', now: '1.3.0' }] });
    window.localStorage.setItem('daoris.list.plugins.chosen', 'acme.gate');
    const { notify } = show();

    await userEvent.click(within(await screenHeader()).getByRole('button', { name: 'Update acme.gate' }));
    const plan = await within(main()).findByRole('region', { name: 'What an update changes' });
    expect(plan).toHaveTextContent('1.2.0 → 1.3.0');
    expect(calls('PLUGIN_UPDATE')).toEqual([{ id: 'acme.gate' }]);

    await userEvent.click(within(plan).getByRole('button', { name: 'Update now' }));
    await waitFor(() => expect(calls('PLUGIN_UPDATE')).toEqual([{ id: 'acme.gate' }, { id: 'acme.gate', apply: true }]));
    await waitFor(() => expect(within(main()).queryByRole('region', { name: 'What an update changes' })).toBeNull());
    expect(notify).toHaveBeenCalledWith(expect.stringMatching(/^acme\.gate is updated/));
  });

  it('installs an offer, and the list chooses the plugin it became', async () => {
    routes.PLUGIN_INSTALL = ({ offer }) => {
      answer([GATE, FUTURE, BROWSER], [{ ...BROWSER_OFFER, installed: true }]);
      return { id: offer, name: 'In-app browser', version: '0.1.0' };
    };
    const { notify } = show();

    await userEvent.click(await within(list()).findByRole('button', { name: /In-app browser/ }));
    expect(await page('In-app browser')).toBeInTheDocument();
    expect(within(main()).getByText('not installed')).toBeInTheDocument();

    await userEvent.click(within(main()).getByRole('button', { name: 'Install in-app-browser' }));
    await waitFor(() => expect(calls('PLUGIN_INSTALL')).toEqual([{ offer: 'in-app-browser' }]));
    await waitFor(() => expect(window.localStorage.getItem('daoris.list.plugins.chosen')).toBe('in-app-browser'));
    // The installed plugin's page: its switch, where the offer's had Install.
    await waitFor(() => expect(within(main()).getByRole('button', { name: 'Turn off' })).toBeInTheDocument());
    expect(within(list()).queryByText("Daoris's own plugins (1)")).toBeNull();
    expect(notify).toHaveBeenCalledWith(expect.stringMatching(/^in-app-browser is installed/));
  });

  it('removes by two presses, naming what the plugin kept, and then chooses nothing', async () => {
    routes.PLUGIN_ACTION = ({ id }) => {
      answer([FUTURE]);
      return { id, action: 'remove', data: GATE.data };
    };
    window.localStorage.setItem('daoris.list.plugins.chosen', 'acme.gate');
    const { notify } = show();

    await userEvent.click(within(await screenHeader()).getByRole('button', { name: 'Remove…' }));
    // The first press removes nothing: it asks.
    expect(calls('PLUGIN_ACTION')).toEqual([]);
    const ask = within(main()).getByRole('group', { name: 'remove acme.gate' });
    await userEvent.click(within(ask).getByRole('button', { name: 'Remove plugin' }));

    await waitFor(() => expect(calls('PLUGIN_ACTION')).toEqual([{ id: 'acme.gate', action: 'remove' }]));
    await waitFor(() => expect(notify).toHaveBeenCalledWith(
      `acme.gate is removed from this machine. What it kept is still at ${GATE.data}.`));
    expect(await within(main()).findByText('Choose a plugin')).toBeInTheDocument();
  });

  it('puts the ask away on Never mind, removing nothing', async () => {
    window.localStorage.setItem('daoris.list.plugins.chosen', 'acme.gate');
    show();

    await userEvent.click(within(await screenHeader()).getByRole('button', { name: 'Remove…' }));
    await userEvent.click(within(main()).getByRole('button', { name: 'Never mind' }));
    expect(within(main()).queryByRole('group', { name: 'remove acme.gate' })).toBeNull();
    expect(calls('PLUGIN_ACTION')).toEqual([]);
  });

  /** Gone is read by the code, never by the sentence (D48 §6): `PLUGIN_UNKNOWN` (D119 §3.2). */
  it('says a plugin removed at a terminal is no longer here, by the driver\'s code', async () => {
    routes.PLUGIN_ACTION = () => {
      answer([FUTURE]);
      throw Object.assign(new Error('There is no plugin called acme.gate on this machine.'), { code: 'PLUGIN_UNKNOWN', parameters: { id: 'acme.gate' } });
    };
    window.localStorage.setItem('daoris.list.plugins.chosen', 'acme.gate');
    const { notify } = show();

    await userEvent.click(within(await screenHeader()).getByRole('button', { name: 'Turn off' }));
    expect(await within(main()).findByText('This plugin is no longer here')).toBeInTheDocument();
    expect(notify).not.toHaveBeenCalledWith(expect.stringMatching(/no plugin called/), 'error');
  });

  it('says a plugin a door names that has gone is no longer here', async () => {
    show({ door: 'nobody' });
    expect(await within(main()).findByText('This plugin is no longer here')).toBeInTheDocument();
  });

  // UX6b (design §1 rule 6): a remembered choice ends with what it chose, so a removed plugin opens nothing chosen.
  it('opens with nothing chosen on a remembered plugin that has gone, and forgets it', async () => {
    window.localStorage.setItem('daoris.list.plugins.chosen', 'nobody');
    show();
    await waitFor(() => expect(main()).toHaveTextContent('Choose a plugin'));
    expect(main()).not.toHaveTextContent('This plugin is no longer here');
    expect(window.localStorage.getItem('daoris.list.plugins.chosen')).toBeNull();
  });

  it('tries a plugin where it stands, and keeps the report on its page', async () => {
    routes.PLUGIN_TRY = ({ id }) => ({
      plugin: id, folder: GATE.folder, command: ['node', 'wire.mjs'], passed: true, summary: 'Every check passed.',
      steps: [{ name: 'quest/consider', ok: true, sentence: 'It answered allow in 41 ms.' }], said: [],
    });
    window.localStorage.setItem('daoris.list.plugins.chosen', 'acme.gate');
    show();

    await userEvent.click(within(await screenHeader()).getByRole('button', { name: 'Try acme.gate' }));
    await waitFor(() => expect(calls('PLUGIN_TRY')).toEqual([{ id: 'acme.gate' }]));
    const tests = within(main()).getByRole('region', { name: 'Tests' });
    expect(await within(tests).findByText('It answered allow in 41 ms.')).toBeInTheDocument();

    // Choosing another and coming back keeps it: the page holds it while Daoris is open.
    await userEvent.click(within(list()).getByRole('button', { name: /Future/ }));
    await userEvent.click(within(list()).getByRole('button', { name: /Acme gate/ }));
    expect(await within(main()).findByText('It answered allow in 41 ms.')).toBeInTheDocument();
  });

  it('opens the kit in a drawer from the ＋, and at its trial from the ⋯', async () => {
    show({ onAsk: vi.fn() });
    await within(list()).findByText('Installed (2)');

    within(list()).getByRole('button', { name: 'Add a plugin' }).focus();
    await userEvent.keyboard('{Enter}');
    await userEvent.click(await screen.findByRole('menuitem', { name: 'Make a plugin…' }));
    const drawer = await screen.findByRole('dialog', { name: 'Make a plugin' });
    expect(within(drawer).getByRole('textbox', { name: 'Plugin ID' })).toBeInTheDocument();
    await userEvent.click(within(drawer).getByRole('button', { name: 'Close' }));
    await waitFor(() => expect(screen.queryByRole('dialog')).toBeNull());

    within(list()).getByRole('button', { name: 'More actions' }).focus();
    await userEvent.keyboard('{Enter}');
    await userEvent.click(await screen.findByRole('menuitem', { name: 'Try a folder…' }));
    const trying = await screen.findByRole('dialog', { name: 'Try a folder' });
    await waitFor(() => expect(within(trying).getByRole('textbox', { name: 'Folder to try' })).toHaveFocus());
  });

  it('offers no Ask Daoris where nobody hands it one, and the kit alone', async () => {
    show();
    await within(list()).findByText('Installed (2)');
    // One kind left: the ＋ is that act itself.
    await userEvent.click(within(list()).getByRole('button', { name: 'Add a plugin' }));
    expect(await screen.findByRole('dialog', { name: 'Make a plugin' })).toBeInTheDocument();
  });

  it('says it is empty with nothing on this machine and nothing offered, offering both of the ＋\'s kinds', async () => {
    answer([], []);
    show({ onAsk: vi.fn() });

    expect(await within(list()).findByText('No plugins on this machine')).toBeInTheDocument();
    const empty = within(list()).getByText('No plugins on this machine').parentElement!;
    expect(within(empty).getAllByRole('button').map((button) => button.textContent)).toEqual(['Ask Daoris for a plugin', 'Make a plugin…']);
  });

  it('draws skeleton rows in the list and the main area while the catalogue first loads', async () => {
    window.localStorage.setItem('daoris.list.plugins.chosen', 'acme.gate');
    let release: (value: unknown) => void = () => {};
    invoke.mockImplementation((_module: string, type: string) =>
      (type === 'PLUGINS' ? new Promise((resolve) => { release = resolve; }) : Promise.resolve({})));
    show();

    expect(main()).toHaveAttribute('data-main-state', 'loading');
    expect(within(list()).queryByText('No plugins on this machine')).toBeNull();
    await act(async () => release(catalog));
    expect(await page('Acme gate')).toBeInTheDocument();
  });

  it('says the sentence in place, and once, where the catalogue never answered', async () => {
    invoke.mockImplementation(async () => {
      throw Object.assign(new Error('timeout'), { code: 'TIMEOUT' });
    });
    const { notify } = show();

    const said = /did not answer in time/;
    expect(await within(list()).findByText(said)).toBeInTheDocument();
    expect(within(main()).getByText(said)).toBeInTheDocument();
    await waitFor(() => expect(notify).toHaveBeenCalledWith(expect.stringMatching(said), 'error'));
    expect(notify).toHaveBeenCalledOnce();
  });
});

/** The page's header, once the remembered plugin's page is drawn. */
async function screenHeader() {
  const heading = await screen.findByRole('heading', { level: 1 });
  return heading.closest('header')!;
}

/** The whole window over the mocked shell, landing on Overview, for the doors into the view. */
function start() {
  vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => {
    const url = String(input);
    if (url.startsWith('/api/status')) return Response.json({ semantic: false, tier: 'lexical', note: '' });
    if (url.startsWith('/api/sync')) return Response.json({ workspace: 'default', wired: false, ahead: 0, behind: [], conflicts: [] });
    return Response.json([]);
  }));
  window.localStorage.setItem(AT_START, 'off');
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={client}>
      <Tooltip.Provider>
        <WorkspaceScopeProvider initial={null}>
          <App />
        </WorkspaceScopeProvider>
      </Tooltip.Provider>
    </QueryClientProvider>,
  );
}

describe('the doors into the Plugins view', () => {
  beforeEach(() => {
    answer([GATE, FUTURE]);
    routes = {};
    wire();
  });
  afterEach(() => {
    cleanup();
    vi.unstubAllGlobals();
    invoke.mockReset();
    window.localStorage.clear();
  });

  const bar = () => screen.getByRole('navigation', { name: 'Views' });

  it('is a place on the activity bar after Search, which opens the view and its list', async () => {
    start();

    // A shell-only place is on the bar once the driver has answered.
    await within(bar()).findByRole('button', { name: 'Plugins' });
    const names = within(bar()).getAllByRole('button').map((place) => place.getAttribute('aria-label') ?? place.textContent);
    expect(names.indexOf('Plugins')).toBe(names.indexOf('Search') + 1);
    await userEvent.click(within(bar()).getByRole('button', { name: 'Plugins' }));
    expect(await screen.findByText('Installed (2)')).toBeInTheDocument();
    // The list's doors are named for it (D118 §3a).
    expect(screen.getByRole('button', { name: 'show or hide the plugin list (Ctrl+B)' })).toBeInTheDocument();
    // Fetched only once the view is in front: no other view asks for the catalogue.
    expect(calls('PLUGINS').length).toBeGreaterThan(0);
  });

  it('asks the driver for nothing of plugins while another view is in front', async () => {
    start();
    await within(bar()).findByRole('button', { name: 'Plugins' });
    expect(calls('PLUGINS')).toEqual([]);
  });

  it('opens from the Daoris menu', async () => {
    start();
    const user = userEvent.setup();
    (await screen.findByRole('button', { name: 'Daoris' })).focus();
    await user.keyboard('{Enter}');
    await user.click(await screen.findByRole('menuitem', { name: /^Plugins/ }));
    expect(await screen.findByText('Installed (2)')).toBeInTheDocument();
  });

  /** "Its report stays here until Daoris closes": the page holds a trial across a change of view. */
  it('keeps a trial\'s report on the page across a change of view', async () => {
    routes.PLUGIN_TRY = ({ id }) => ({
      plugin: id, folder: GATE.folder, command: ['node', 'wire.mjs'], passed: true, summary: 'Every check passed.',
      steps: [{ name: 'quest/consider', ok: true, sentence: 'It answered allow in 41 ms.' }], said: [],
    });
    window.localStorage.setItem('daoris.list.plugins.chosen', 'acme.gate');
    start();

    await userEvent.click(await within(bar()).findByRole('button', { name: 'Plugins' }));
    await userEvent.click(await screen.findByRole('button', { name: 'Try acme.gate' }));
    expect(await screen.findByText('It answered allow in 41 ms.')).toBeInTheDocument();

    await userEvent.click(within(bar()).getByRole('button', { name: 'Overview' }));
    await waitFor(() => expect(screen.queryByText('It answered allow in 41 ms.')).toBeNull());
    await userEvent.click(within(bar()).getByRole('button', { name: 'Plugins' }));
    expect(await screen.findByText('It answered allow in 41 ms.')).toBeInTheDocument();
  });
});
