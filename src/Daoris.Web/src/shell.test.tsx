import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { act, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import * as Tooltip from '@radix-ui/react-tooltip';

// Settings in SHELL mode — the bridge mocked as present, so the machine's domains, which only a desktop
// may render, actually render, and land on the shell's modules. Browser mode needs no twin suite: every
// other test in this project runs with no transport, and the absence of these controls there is
// asserted by their queries never firing (an unstubbed fetch throws). Projects, Quests, the push channel
// and the bridge's own hooks moved beside their code with MOD3; these follow Settings' domains in MOD4.

const { invoke, notifyReady, eventHandlers } = vi.hoisted(() => ({
  invoke: vi.fn(),
  notifyReady: vi.fn(() => Promise.resolve()),
  // The push channel's seam: handlers land here by "module.type", and a test fires them as the host.
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

import { SettingsView } from './SettingsView';
import { HarnessRuns } from './harnessRuns';
import { DRIVER_STATE, REGISTRY, respond, serviceCalls, show, STATUS, WIRING } from './test/shellHarness';

/**
 * The machine's wiring (D48 §5, D50): which deployment serves each workspace here. Shell-only and
 * more strictly than the rest — the service has no route onto this at all, so every call must land on
 * the bridge and none on the API.
 */
describe('the machine settings surface', () => {
  beforeEach(() => {
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => respond(String(input))));
    invoke.mockImplementation(async () => WIRING);
  });
  afterEach(() => {
    vi.unstubAllGlobals();
    invoke.mockReset();
  });

  it('reads the wiring over the bridge and never over the service', async () => {
    show(<SettingsView notify={() => {}} section="workspace" />);

    expect(await screen.findByText('aurora')).toBeTruthy();
    expect(screen.getByText('https://aurora.example.com')).toBeTruthy();
    expect(invoke).toHaveBeenCalledWith('DAORIS.REMOTES', 'STATE', {});
    // Not a single call toward the service: a keyed remote's browser must never learn, or change,
    // where a machine syncs.
    expect(serviceCalls()).toEqual([]);
  });

  /**
   * Where this machine's Daoris lives (D63), and what the start did about it. The notice rides the
   * STATE rather than only the one-time event, because the page subscribes after the host answers
   * and a toast raised before that reached nobody — measured on the first migrated start.
   */
  it('shows where this machine\'s Daoris lives, and what establishing it did', async () => {
    invoke.mockImplementation(async (module: string) => (module === 'DAORIS.DRIVER'
      ? {
        ...DRIVER_STATE,
        home: 'D:/somewhere/Daoris/data',
        homeNotice: 'Daoris home: D:/somewhere/Daoris/data — moved in from D:/somewhere/.daoris: driver.json.',
        hostNotice: 'the host at http://localhost:5177 was already running and serves a different page than this install carries.',
      }
      : WIRING));
    show(<SettingsView notify={() => {}} section="driver" />);

    expect(await screen.findByText('D:/somewhere/Daoris/data')).toBeTruthy();
    expect(screen.getByText(/moved in from/)).toBeTruthy();
    // The adopted host's page is a standing fact and gets a standing line, not only a toast.
    expect(screen.getByText(/serves a different page/)).toBeTruthy();
  });

  it('says nothing about the home on a shell that has never heard of one', async () => {
    invoke.mockImplementation(async (module: string) => (module === 'DAORIS.DRIVER' ? DRIVER_STATE : WIRING));
    show(<SettingsView notify={() => {}} section="driver" />);
    await screen.findByLabelText('Park a quest after this many failed sessions');

    // What is absent is the path a newer shell answers.
    expect(screen.queryByText(/somewhere\/Daoris\/data/)).toBeNull();
    expect(screen.queryByText(/moved in from/)).toBeNull();
  });

  /**
   * A setting is a row (2026-09-23): the control sits beside its label, the terminal's door is the
   * one-line hint, and the paragraph that motivated the setting is on the glyph rather than on the
   * page — so the first control is where the eye lands rather than a screen below the title.
   */
  it('lays each setting out as a row, with its why one hover away rather than on the page', async () => {
    invoke.mockImplementation(async (module: string) => (
      module === 'DAORIS.DRIVER' ? { ...DRIVER_STATE, notify: true, strikes: 3 } : WIRING));
    show(<SettingsView notify={() => {}} section="driver" />);
    await screen.findByRole('checkbox', { checked: true });

    // The number dial is reachable by its label, and the why is a note, not a paragraph.
    expect(screen.getByLabelText('Park a quest after this many failed sessions')).toBeTruthy();
    expect(screen.getByRole('note', { name: /Nobody should have to watch a driver/ })).toBeTruthy();
    expect(screen.queryByText(/Nobody should have to watch a driver/)).toBeNull();
  });

  /**
   * REV3: passing through the field was a write. `Number(null)` and `Number('')` are both 0, which is
   * the one value that means "never park" — and a keyboard user tabbing through the card sent it, with
   * a toast that read like a confirmation.
   */
  it('writes the strikes only when the person changed them — focus, blur and a cleared box write nothing', async () => {
    invoke.mockImplementation(async (module: string) => (
      module === 'DAORIS.DRIVER' ? { ...DRIVER_STATE, strikes: 3 } : WIRING));
    show(<SettingsView notify={() => {}} section="driver" />);
    const field = await screen.findByLabelText('Park a quest after this many failed sessions');

    await userEvent.click(field);
    await userEvent.tab();
    await userEvent.clear(field);
    await userEvent.tab();
    await userEvent.clear(field);
    await userEvent.type(field, '3');
    await userEvent.tab();
    expect(invoke.mock.calls.some(([, type]) => type === 'SET_STRIKES')).toBe(false);
    expect((field as HTMLInputElement).value).toBe('3');

    await userEvent.clear(field);
    await userEvent.type(field, '5');
    await userEvent.tab();
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SET_STRIKES', { payload: { strikes: 5 } });
  });

  it('keeps the wiring form one press away, not open on every visit', async () => {
    invoke.mockImplementation(async (module: string) => (
      module === 'DAORIS.DRIVER' ? DRIVER_STATE : WIRING));
    show(<SettingsView notify={() => {}} section="workspace" />);

    await userEvent.click(await screen.findByRole('button', { name: 'Wire a workspace' }));
    expect(screen.getByLabelText('deployment')).toBeTruthy();
  });

  /**
   * D75: one page, its domains in a list, one shown at a time. Every way in names its domain, so the
   * page is the caller's to open at one; the list is how the person moves between them.
   */
  it('lists its domains, shows only the one chosen, and asks for another by name', async () => {
    invoke.mockImplementation(async (module: string) => (
      module === 'DAORIS.DRIVER' ? DRIVER_STATE : WIRING));
    const onSection = vi.fn();
    show(<SettingsView notify={() => {}} section="driver" onSection={onSection} />);

    const domains = await screen.findByRole('navigation', { name: 'Settings domains' });
    await waitFor(() => expect(within(domains).getAllByRole('button')).toHaveLength(10));
    expect(within(domains).getByRole('button', { name: 'Driver' })).toHaveAttribute('aria-current', 'page');
    // The setup guide leads (SETUP1a, D97).
    expect(within(domains).getAllByRole('button')[0]?.textContent).toBe('Get started');
    // Daoris's browser (CHR5, CHR7) and the machine log (LOG1c) are a machine's domains, last in the list.
    expect(within(domains).getAllByRole('button').slice(-2).map((button) => button.textContent)).toEqual(['Browser', 'Logs']);
    expect(await screen.findByLabelText('Park a quest after this many failed sessions')).toBeTruthy();
    // A card alone in its domain does not say the domain's name again: the list already has.
    expect(screen.getAllByText('Driver')).toHaveLength(1);
    // Another domain's cards are not on the page at all.
    expect(screen.queryByRole('button', { name: 'Wire a workspace' })).toBeNull();
    expect(screen.queryByText('Theme')).toBeNull();

    await userEvent.click(within(domains).getByRole('button', { name: 'Plugins' }));
    expect(onSection).toHaveBeenCalledWith('plugins');
  });

  it('wiring a workspace edits the map and clears the key out of the form', async () => {
    show(<SettingsView notify={() => {}} section="workspace" />);
    await screen.findByText('aurora');

    await userEvent.click(screen.getByRole('button', { name: 'Wire a workspace' }));
    await userEvent.type(screen.getByLabelText('workspace'), 'tools');
    await userEvent.type(screen.getByLabelText('deployment'), 'https://tools.example.com');
    await userEvent.type(screen.getByLabelText('key'), 'dk_toolskey0000');
    await userEvent.click(screen.getByRole('button', { name: 'wire it' }));

    expect(invoke).toHaveBeenCalledWith('DAORIS.REMOTES', 'SET', {
      payload: { workspace: 'tools', url: 'https://tools.example.com', key: 'dk_toolskey0000' },
    });
    // The form closes on success, and the key does not linger in it once it has landed in the file:
    // opened again, it is empty.
    expect(screen.queryByLabelText('key')).toBeNull();
    await userEvent.click(screen.getByRole('button', { name: 'Wire a workspace' }));
    expect((screen.getByLabelText('key') as HTMLInputElement).value).toBe('');
  });

  /** A key goes in and never comes out: what is rendered is the prefix the module chose to answer. */
  it('renders only the audit prefix a key was reduced to', async () => {
    const { container } = show(<SettingsView notify={() => {}} section="workspace" />);
    await screen.findByText('aurora');

    expect(screen.getByText('dk_abcd1234…')).toBeTruthy();
    expect(container.textContent).not.toContain('dk_abcd1234wxyz');
  });

  /**
   * Notifications (SURF5b): the desktop's door onto the same `driver.json` field
   * `daoris driver notify on|off` edits. It is ON until somebody says otherwise — a driver nobody
   * has to watch is the point of one — and the surface names the other door so a person on a
   * machine they reach over ssh does not go looking for a second setting.
   */
  it('the notification switch reads the machine and writes the same file a terminal does', async () => {
    invoke.mockImplementation(async (module: string) => (
      module === 'DAORIS.DRIVER' ? { ...DRIVER_STATE, notify: true } : WIRING));

    show(<SettingsView notify={() => {}} section="driver" />);

    // Found by its loaded STATE rather than by its label, because the switch renders before the
    // machine has answered and its default is on — a bare label query would pass either way.
    const check = await screen.findByRole('checkbox', { checked: true });
    expect(screen.getByText(/daoris driver notify on\|off/)).toBeTruthy();

    await userEvent.click(check);
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SET_NOTIFY', { payload: { notify: false } });
  });

  it('a machine that has turned notifications off says so', async () => {
    invoke.mockImplementation(async (module: string) => (
      module === 'DAORIS.DRIVER' ? { ...DRIVER_STATE, notify: false } : WIRING));

    show(<SettingsView notify={() => {}} section="driver" />);

    expect(await screen.findByRole('checkbox', { checked: false })).toBeTruthy();
  });

  it('unwiring says what it did not do, and touches no deployment', async () => {
    const notify = vi.fn();
    show(<SettingsView notify={notify} section="workspace" />);
    await screen.findByText('aurora');

    await userEvent.click(screen.getByRole('button', { name: 'unwire' }));

    expect(invoke).toHaveBeenCalledWith('DAORIS.REMOTES', 'REMOVE', { payload: { workspace: 'aurora' } });
    expect(notify).toHaveBeenCalledWith(expect.stringContaining('Nothing at the deployment changed'));
    expect(serviceCalls()).toEqual([]);
  });

  /**
   * With the environment pair set no loader reads the file, so the surface must say which source
   * decided — otherwise it reports its own last edit as though it were the machine's wiring.
   */
  it('says when the environment, not the file, is the answer', async () => {
    invoke.mockImplementation(async () => ({ ...WIRING, fromEnvironment: true }));
    show(<SettingsView notify={() => {}} section="workspace" />);

    expect(await screen.findByText(/environment names this machine's remote/i)).toBeTruthy();
  });
});

/**
 * This machine's plugins (D64): one row per folder under the home's `plugins/`, the switch a row in
 * the same file `daoris plugin enable|disable` edits, and Remove naming what the plugin kept. Every
 * call lands on the bridge — a plugin's folder is a machine path — and none on the service.
 */
describe('the plugins card', () => {
  const PLUGINS = {
    folder: 'C:/somewhere/data/plugins',
    plugins: [
      {
        id: 'acme.gate', name: 'Acme gate', version: '1.2.0', description: 'Holds quests overnight.',
        enabled: true, problem: null, harnesses: ['acme-agent'], points: ['quest/consider'],
        running: true, folder: 'C:/somewhere/data/plugins/acme.gate', data: 'C:/somewhere/data/plugins/.data/acme.gate',
      },
      {
        id: 'future', name: 'future', version: '', description: '',
        enabled: true, problem: 'needs plugin API 99, and this build speaks 1 — update Daoris, or use a plugin written for 1.',
        harnesses: [], points: [], running: false,
        folder: 'C:/somewhere/data/plugins/future', data: 'C:/somewhere/data/plugins/.data/future',
      },
    ],
  };

  beforeEach(() => {
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => respond(String(input))));
    invoke.mockImplementation(async (_module: string, type: string) => (type === 'PLUGINS' ? PLUGINS : WIRING));
  });
  afterEach(() => {
    vi.unstubAllGlobals();
    invoke.mockReset();
  });

  it('lists each plugin with what it declares, what it speaks on, and the driver\'s sentence for a refused one', async () => {
    show(<SettingsView notify={() => {}} section="plugins" />);

    expect(await screen.findByText('Acme gate')).toBeTruthy();
    expect(screen.getByText(/declares acme-agent; speaks on quest\/consider/)).toBeTruthy();
    expect(screen.getByText('running')).toBeTruthy();
    // The refused plugin is listed WITH the driver's own sentence, verbatim.
    expect(screen.getByText(/needs plugin API 99/)).toBeTruthy();
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'PLUGINS', {});
    expect(serviceCalls()).toEqual([]);
  });

  it('the switch and Remove land on the bridge as the actions a terminal has', async () => {
    const notify = vi.fn();
    show(<SettingsView notify={notify} section="plugins" />);
    const row = (await screen.findByText('Acme gate')).closest('div')!.parentElement!.parentElement!;

    await userEvent.click(within(row as HTMLElement).getByRole('button', { name: 'Turn off' }));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'PLUGIN_ACTION', { payload: { id: 'acme.gate', action: 'disable' } });

    await userEvent.click(within(row as HTMLElement).getByRole('button', { name: 'Remove' }));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'PLUGIN_ACTION', { payload: { id: 'acme.gate', action: 'remove' } });
  });

  it('a machine with no plugins says where one would go', async () => {
    invoke.mockImplementation(async (_module: string, type: string) =>
      (type === 'PLUGINS' ? { folder: 'C:/somewhere/data/plugins', plugins: [] } : WIRING));
    show(<SettingsView notify={() => {}} section="plugins" />);

    expect(await screen.findByText(/daoris plugin add/)).toBeTruthy();
    expect(screen.getByText('C:/somewhere/data/plugins')).toBeTruthy();
  });

  /**
   * PLUG9 (c): each row says where the plugin came from; one with a record is updated by two presses, the
   * first asking what would change and the second making it; one with none says so and offers no Update.
   */
  it('a row says where its plugin came from, and Update asks what changes before it replaces anything', async () => {
    const notify = vi.fn();
    const SOURCED = {
      ...PLUGINS,
      plugins: [
        { ...PLUGINS.plugins[0]!, source: { kind: 'folder', folder: 'C:/checkouts/house-plugins/gate', offer: null, problem: null } },
        { ...PLUGINS.plugins[1]!, source: { kind: 'none', folder: null, offer: null, problem: null } },
      ],
    };
    const PLAN = {
      id: 'acme.gate', applied: false, refusal: null, source: 'C:/checkouts/house-plugins/gate', from: 'C:/checkouts/house-plugins/gate',
      changes: [{ what: 'version', was: '1.2.0', now: '1.3.0' }],
    };
    invoke.mockImplementation(async (_module: string, type: string, request?: { payload?: { apply?: boolean } }) => {
      if (type === 'PLUGINS') return SOURCED;
      if (type === 'PLUGIN_UPDATE') return request?.payload?.apply ? { ...PLAN, applied: true } : PLAN;
      return WIRING;
    });
    show(<SettingsView notify={notify} section="plugins" />);

    expect(await screen.findByText(/Added from/)).toHaveTextContent('C:/checkouts/house-plugins/gate');
    expect(screen.getByText(/No record of where it came from/)).toBeTruthy();
    // No record, nothing to update from: the refused plugin's row has no Update.
    expect(screen.queryByRole('button', { name: 'Update future' })).toBeNull();

    await userEvent.click(screen.getByRole('button', { name: 'Update acme.gate' }));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'PLUGIN_UPDATE', { payload: { id: 'acme.gate' } });
    const plan = await screen.findByRole('region', { name: 'What an update changes' });
    expect(within(plan).getByText('1.3.0', { selector: 'code' })).toBeTruthy();
    expect(invoke).not.toHaveBeenCalledWith('DAORIS.DRIVER', 'PLUGIN_UPDATE', { payload: { id: 'acme.gate', apply: true } });

    await userEvent.click(within(plan).getByRole('button', { name: 'Update now' }));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'PLUGIN_UPDATE', { payload: { id: 'acme.gate', apply: true } });
    await waitFor(() => expect(notify).toHaveBeenCalledWith(expect.stringContaining('acme.gate is updated')));
    expect(screen.queryByRole('region', { name: 'What an update changes' })).toBeNull();
    expect(serviceCalls()).toEqual([]);
  });

  it('an update the driver refuses shows its sentence under the row, with nothing to press but Not now', async () => {
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'PLUGINS') {
        return { ...PLUGINS, plugins: [{ ...PLUGINS.plugins[0]!, source: { kind: 'folder', folder: 'C:/gone/gate' } }] };
      }
      if (type === 'PLUGIN_UPDATE') {
        return { id: 'acme.gate', applied: false, refusal: 'the folder `acme.gate` was added from is not there any more: C:/gone/gate.', changes: [] };
      }
      return WIRING;
    });
    show(<SettingsView notify={() => {}} section="plugins" />);

    await userEvent.click(await screen.findByRole('button', { name: 'Update acme.gate' }));

    const plan = await screen.findByRole('region', { name: 'What an update changes' });
    expect(within(plan).getByText(/is not there any more/)).toBeTruthy();
    expect(within(plan).queryByRole('button', { name: 'Update now' })).toBeNull();
    await userEvent.click(within(plan).getByRole('button', { name: 'Not now' }));
    expect(screen.queryByRole('region', { name: 'What an update changes' })).toBeNull();
  });

  /**
   * PLUG9 (d): the install's own plugins, in their own group beneath the catalogue, each with what it
   * needs; Install is `daoris plugin add --offer`'s copy. An older shell answers no offers, and gets no group.
   */
  it('the install\'s own plugins are offered in their own group, and Install copies one in', async () => {
    const notify = vi.fn();
    const OFFERS = [
      {
        id: 'github-pull-request', name: 'GitHub pull request', version: '1.0.0', description: '', problem: null,
        harnesses: [], points: ['work/land'], servers: [], needs: ['gh, signed in: `gh auth login`.'], installed: false,
      },
      {
        id: 'acme.gate', name: 'Acme gate', version: '1.2.0', description: '', problem: null,
        harnesses: [], points: ['quest/consider'], servers: [], needs: [], installed: true,
      },
    ];
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'PLUGINS') return { ...PLUGINS, offers: OFFERS, offersFolder: 'C:/somewhere/app/plugin-offers' };
      if (type === 'PLUGIN_INSTALL') return { id: 'github-pull-request', name: 'GitHub pull request', version: '1.0.0' };
      return WIRING;
    });
    show(<SettingsView notify={notify} section="plugins" />);

    const group = (await screen.findByText('Daoris\'s own plugins')).closest('article')!;
    expect(within(group).getByText('GitHub pull request')).toBeTruthy();
    expect(within(group).getByText('gh auth login', { selector: 'code' })).toBeTruthy();
    // Installed already, it is a row of the catalogue above, never offered again.
    expect(within(group).queryByText('Acme gate')).toBeNull();

    await userEvent.click(within(group).getByRole('button', { name: 'Install github-pull-request' }));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'PLUGIN_INSTALL', { payload: { offer: 'github-pull-request' } });
    await waitFor(() => expect(notify).toHaveBeenCalledWith(expect.stringContaining('github-pull-request is installed')));
    expect(serviceCalls()).toEqual([]);
  });

  it('an older shell answers no offers and no source, and the screen draws neither', async () => {
    show(<SettingsView notify={() => {}} section="plugins" />);

    expect(await screen.findByText('Acme gate')).toBeTruthy();
    expect(screen.queryByText('Daoris\'s own plugins')).toBeNull();
    expect(screen.queryByText(/No record of where it came from/)).toBeNull();
    expect(screen.queryByRole('button', { name: 'Update acme.gate' })).toBeNull();
  });

  it('speaks 中文 on the plugins screen, the driver\'s and the README\'s words left as they are', async () => {
    const { default: i18n } = await import('./i18n');
    invoke.mockImplementation(async (_module: string, type: string) => (type === 'PLUGINS'
      ? {
        ...PLUGINS,
        plugins: [{ ...PLUGINS.plugins[0]!, source: { kind: 'offer', offer: 'acme.gate' } }],
        offers: [{
          id: 'github-pull-request', name: 'GitHub pull request', version: '1.0.0', description: '', problem: null,
          harnesses: [], points: ['work/land'], servers: [], needs: ['gh, signed in: `gh auth login`.'], installed: false,
        }],
      }
      : WIRING));
    await i18n.changeLanguage('zh');
    try {
      show(<SettingsView notify={() => {}} section="plugins" />);

      expect(await screen.findByText('Daoris 自带的插件')).toBeTruthy();
      expect(screen.getByText(/安装自 Daoris 自带的插件/)).toBeTruthy();
      expect(screen.getByRole('button', { name: '更新 acme.gate' })).toBeTruthy();
      expect(screen.getByRole('button', { name: '安装 github-pull-request' })).toBeTruthy();
      expect(screen.getByText('gh auth login', { selector: 'code' })).toBeTruthy();
    } finally {
      await i18n.changeLanguage('en');
    }
  });

  /** PLUG8 (D101): the kit a plugin is made with, and a Try beside every plugin that speaks. */
  const KIT = {
    points: [
      { name: 'quest/consider', kind: 'decision' },
      { name: 'session/ended', kind: 'observation' },
      { name: 'work/land', kind: 'act' },
    ],
  };
  const TRIAL = {
    plugin: 'acme.gate', folder: 'C:/somewhere/data/plugins/acme.gate', command: ['node', 'plugin.mjs'],
    passed: true, summary: '`acme.gate` answered as the driver reads it.', said: [],
    steps: [{ name: 'handshake', ok: true, sentence: 'speaks hook wire 1 and listens on quest/consider.' }],
  };

  it('an older shell\'s catalogue carries no kit, and gets no kit card and no Try', async () => {
    show(<SettingsView notify={() => {}} section="plugins" />);

    expect(await screen.findByText('Acme gate')).toBeTruthy();
    expect(screen.queryByText('Make a plugin')).toBeNull();
    expect(screen.queryByRole('button', { name: 'Try acme.gate' })).toBeNull();
  });

  it('a plugin that speaks is tried where it stands, and the report sits under its row', async () => {
    invoke.mockImplementation(async (_module: string, type: string) =>
      (type === 'PLUGINS' ? { ...PLUGINS, kit: KIT } : type === 'PLUGIN_TRY' ? TRIAL : WIRING));
    show(<SettingsView notify={() => {}} section="plugins" />);

    await userEvent.click(await screen.findByRole('button', { name: 'Try acme.gate' }));

    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'PLUGIN_TRY', { payload: { id: 'acme.gate' } });
    expect(await screen.findByText(/answered as the driver reads it/)).toBeTruthy();
    // A refused plugin speaks nowhere, so there is nothing of it to try.
    expect(screen.queryByRole('button', { name: 'Try future' })).toBeNull();
    expect(serviceCalls()).toEqual([]);
  });

  it('the kit makes a plugin in the folder picked, and offers that folder to Try', async () => {
    const notify = vi.fn();
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'PLUGINS') return { ...PLUGINS, kit: KIT };
      if (type === 'PICK_FOLDER') {
        return {
          path: 'C:/work/plugins', name: 'plugins', exists: true, adopted: false, git: true,
          owns: [], accepts: [], packs: [], join: false, shareKnowledge: false,
        };
      }
      if (type === 'PLUGIN_NEW') {
        return { id: 'acme.new', folder: 'C:/work/plugins/acme.new', points: ['work/land'], files: ['plugin.json'] };
      }
      if (type === 'PLUGIN_TRY') return { ...TRIAL, plugin: 'acme.new', folder: 'C:/work/plugins/acme.new' };
      return WIRING;
    });
    show(<SettingsView notify={notify} section="plugins" />);
    const kit = await screen.findByRole('group', { name: 'New' });

    await userEvent.type(within(kit).getByRole('textbox', { name: 'Plugin id' }), 'acme.new');
    await userEvent.click(within(kit).getByRole('checkbox', { name: /work\/land/ }));
    await userEvent.click(within(kit).getByRole('button', { name: 'Choose…' }));
    expect(await within(kit).findByDisplayValue('C:/work/plugins')).toBeTruthy();
    await userEvent.click(within(kit).getByRole('button', { name: 'New' }));

    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'PLUGIN_NEW',
      { payload: { id: 'acme.new', points: ['work/land'], folder: 'C:/work/plugins' } });
    await waitFor(() => expect(notify).toHaveBeenCalledWith(expect.stringContaining('Made acme.new in C:/work/plugins/acme.new')));
    expect(screen.getByRole('textbox', { name: 'Folder to try' })).toHaveValue('C:/work/plugins/acme.new');

    await userEvent.click(within(screen.getByRole('group', { name: 'Try a folder' })).getByRole('button', { name: 'Try' }));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'PLUGIN_TRY', { payload: { folder: 'C:/work/plugins/acme.new' } });
    expect(await screen.findByRole('region', { name: 'What try found' })).toBeTruthy();
  });
});

/**
 * Daoris's browser on Settings (CHR5, CHR7): the favorites it shows on its bookmarks bar and the
 * extensions setting, over the files `daoris browser` edits (D50). A machine domain: the bridge, never
 * the service.
 */
describe('the browser domain', () => {
  const BROWSER = {
    favoritesPath: 'C:/somewhere/data/browser/favorites.json',
    favorites: [{ url: 'https://site.example/board', title: 'Board' }],
    favoritesProblem: null,
    settingsPath: 'C:/somewhere/data/browser/settings.json',
    extensions: 'offer',
    browser: 'daoris',
    edgeFound: true,
    edgeProfile: 'C:/somewhere/data/browser/edge',
    settingsProblem: null,
  };

  beforeEach(() => {
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => respond(String(input))));
    invoke.mockImplementation(async (module: string) => (module === 'DAORIS.BROWSER' ? BROWSER : DRIVER_STATE));
  });
  afterEach(() => {
    vi.unstubAllGlobals();
    invoke.mockReset();
  });

  it('shows the favorites and the extensions setting from the machine, and nothing over the service', async () => {
    show(<SettingsView notify={() => {}} section="browser" />);

    expect(await screen.findByText('Board')).toBeTruthy();
    expect(screen.getByText('https://site.example/board')).toBeTruthy();
    expect(screen.getByText('C:/somewhere/data/browser/favorites.json')).toBeTruthy();
    expect(screen.getByRole('radio', { name: 'offer' }).getAttribute('aria-checked')).toBe('true');
    expect(screen.getByText(/next time Daoris's browser starts/)).toBeTruthy();
    expect(invoke).toHaveBeenCalledWith('DAORIS.BROWSER', 'STATE', {});
    expect(serviceCalls()).toEqual([]);
  });

  it('keeping and removing a page land as the verbs a terminal has', async () => {
    const notify = vi.fn();
    show(<SettingsView notify={notify} section="browser" />);
    await screen.findByText('Board');

    await userEvent.type(screen.getByPlaceholderText('https://…'), 'site.example/new');
    await userEvent.type(screen.getByPlaceholderText("the page's host"), 'New');
    await userEvent.click(screen.getByRole('button', { name: 'keep it' }));
    expect(invoke).toHaveBeenCalledWith('DAORIS.BROWSER', 'ADD_FAVORITE', { payload: { address: 'site.example/new', title: 'New' } });
    await waitFor(() => expect(notify).toHaveBeenCalledWith('New is a favorite.'));

    await userEvent.click(screen.getByRole('button', { name: 'remove' }));
    expect(invoke).toHaveBeenCalledWith('DAORIS.BROWSER', 'REMOVE_FAVORITE', { payload: { address: 'https://site.example/board' } });
  });

  it('refusing other software\'s extensions says it holds from the next start', async () => {
    const notify = vi.fn();
    show(<SettingsView notify={notify} section="browser" />);

    await userEvent.click(await screen.findByRole('radio', { name: 'refuse' }));

    expect(invoke).toHaveBeenCalledWith('DAORIS.BROWSER', 'SET_EXTENSIONS', { payload: { extensions: 'refuse' } });
    await waitFor(() => expect(notify).toHaveBeenCalledWith(
      "Other software's extensions will be refused from the browser's next start."));
  });

  /** BRW12: the person's Edge, as an option, saying what it brings before it is chosen. */
  it('choosing Edge lands as the verb a terminal has, and says what Edge brings', async () => {
    const notify = vi.fn();
    show(<SettingsView notify={notify} section="browser" />);

    expect(await screen.findByRole('radio', { name: "Daoris's own" })).toHaveAttribute('aria-checked', 'true');
    await userEvent.click(screen.getByRole('radio', { name: 'your Edge' }));

    expect(invoke).toHaveBeenCalledWith('DAORIS.BROWSER', 'SET_BROWSER', { payload: { browser: 'edge' } });
    await waitFor(() => expect(notify).toHaveBeenCalledWith('your Edge, from the next time the browser is opened.'));
  });

  /** BRW7: where the page's links open — set here or by `daoris browser links`, and holding at once. */
  it("sending links to Daoris's browser lands as the verb a terminal has, and says it holds at once", async () => {
    const notify = vi.fn();
    show(<SettingsView notify={notify} section="browser" />);

    // A shell that sends no `links` is the system's, as a link always was.
    expect(await screen.findByRole('radio', { name: "the system's browser" })).toHaveAttribute('aria-checked', 'true');
    expect(screen.getByText(/daoris browser links system\|daoris/)).toBeTruthy();
    await userEvent.click(screen.getByRole('radio', { name: "Daoris's browser" }));

    expect(invoke).toHaveBeenCalledWith('DAORIS.BROWSER', 'SET_LINKS', { payload: { links: 'daoris' } });
    await waitFor(() => expect(notify).toHaveBeenCalledWith("Links on the page open in Daoris's browser, from the next click."));
  });

  /** BRW8: whose hands are on it, in its own domain too — each session a door into Sessions. */
  it('names who is driving it, each opening its session, and says so when nobody is', async () => {
    const onAttend = vi.fn();
    const { unmount } = show(
      <SettingsView
        notify={() => {}}
        section="browser"
        browserDrivers={[{ id: 's1a2b3c4', name: 'engine · Read the ticket' }, { id: 'c0ffee00', name: 'game · conversation' }]}
        onAttend={onAttend}
      />,
    );

    expect(await screen.findByText('Driving it now')).toBeTruthy();
    await userEvent.click(screen.getByRole('button', { name: 'open game · conversation' }));
    expect(onAttend).toHaveBeenCalledWith('c0ffee00');
    unmount();

    show(<SettingsView notify={() => {}} section="browser" browserDrivers={[]} />);
    expect(await screen.findByText('No session is driving it.')).toBeTruthy();
  });

  it('with Edge chosen, says its profile, its account and its default profile, and whose favorites these are', async () => {
    invoke.mockImplementation(async (module: string) => (module === 'DAORIS.BROWSER' ? { ...BROWSER, browser: 'edge' } : DRIVER_STATE));
    show(<SettingsView notify={() => {}} section="browser" />);

    expect(await screen.findByText(/C:\/somewhere\/data\/browser\/edge.*Microsoft account.*default Edge profile cannot be driven/)).toBeTruthy();
    expect(screen.getByText(/Edge keeps its own/)).toBeTruthy();
  });

  it('a machine with no Edge says so before it is chosen', async () => {
    invoke.mockImplementation(async (module: string) => (module === 'DAORIS.BROWSER' ? { ...BROWSER, edgeFound: false } : DRIVER_STATE));
    show(<SettingsView notify={() => {}} section="browser" />);

    expect(await screen.findByText(/No Edge is installed on this machine/)).toBeTruthy();
  });

  it('a file that could not be read is said, with where it is, and no list is guessed', async () => {
    invoke.mockImplementation(async (module: string) => (module === 'DAORIS.BROWSER'
      ? { ...BROWSER, favorites: [], favoritesProblem: 'C:/somewhere/data/browser/favorites.json is not a JSON object' }
      : DRIVER_STATE));
    show(<SettingsView notify={() => {}} section="browser" />);

    expect(await screen.findByText(/could not be read: .*is not a JSON object/)).toBeTruthy();
    expect(screen.queryByText('No favorites yet.')).toBeNull();
  });
});

/**
 * Settings → Logs (LOG1c, D94): the machine log read back over `DAORIS.LOG`, with the filters the
 * terminal's `daoris-driver logs` takes applied by the shell, and the folder opened by the shell. None
 * of it goes over the service: the log is the machine's (D47 §4).
 */
describe('the logs domain', () => {
  const READING = {
    folder: 'C:/somewhere/data/logs',
    lines: [
      { time: '2026-09-30T11:30:00.000Z', source: 'desktop', level: 'error', event: 'page.error', data: { where: 'window' } },
      { time: '2026-09-30T09:00:00.000Z', source: 'desktop', level: 'info', event: 'view.opened', data: { view: 'sessions' } },
    ],
    total: 2,
    counts: { info: 1, warn: 0, error: 1 },
    events: ['page.error', 'view.opened'],
    skipped: 0,
  };

  beforeEach(() => {
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => respond(String(input))));
    invoke.mockImplementation(async (module: string, type: string) => {
      if (module !== 'DAORIS.LOG') return DRIVER_STATE;
      return type === 'OPEN_FOLDER' ? { opened: true, folder: READING.folder } : READING;
    });
  });
  afterEach(() => {
    vi.unstubAllGlobals();
    invoke.mockReset();
  });

  it('is a domain of a desktop, reading the last day of every source from the machine, and nothing over the service', async () => {
    show(<SettingsView notify={() => {}} section="logs" />);

    expect(await within(screen.getByRole('navigation', { name: 'Settings domains' })).findByRole('button', { name: 'Logs' }))
      .toHaveAttribute('aria-current', 'page');
    expect(await screen.findByRole('listitem', { name: 'page.error' })).toBeInTheDocument();
    expect(screen.getByText('C:/somewhere/data/logs')).toBeInTheDocument();
    expect(invoke).toHaveBeenCalledWith('DAORIS.LOG', 'LINES', { payload: { since: '1d', limit: 200 } });
    expect(serviceCalls()).toEqual([]);
  });

  it('asks the shell again with each filter, as the terminal would take the flag', async () => {
    show(<SettingsView notify={() => {}} section="logs" />);
    await screen.findByRole('listitem', { name: 'page.error' });

    await userEvent.click(screen.getByRole('radio', { name: 'errors' }));
    await waitFor(() => expect(invoke).toHaveBeenLastCalledWith(
      'DAORIS.LOG', 'LINES', { payload: { since: '1d', level: 'error', limit: 200 } }));

    await userEvent.click(screen.getByRole('radio', { name: 'last 7 days' }));
    await waitFor(() => expect(invoke).toHaveBeenLastCalledWith(
      'DAORIS.LOG', 'LINES', { payload: { since: '7d', level: 'error', limit: 200 } }));
  });

  it('opens the folder through the shell, naming no path', async () => {
    show(<SettingsView notify={() => {}} section="logs" />);

    await userEvent.click(await screen.findByRole('button', { name: 'Open the folder' }));

    expect(invoke).toHaveBeenCalledWith('DAORIS.LOG', 'OPEN_FOLDER', {});
  });

  it('says where the log is when the shell could not open a folder', async () => {
    invoke.mockImplementation(async (module: string, type: string) => {
      if (module !== 'DAORIS.LOG') return DRIVER_STATE;
      return type === 'OPEN_FOLDER' ? { opened: false, folder: READING.folder } : READING;
    });
    const notify = vi.fn();
    show(<SettingsView notify={notify} section="logs" />);

    await userEvent.click(await screen.findByRole('button', { name: 'Open the folder' }));

    await waitFor(() => expect(notify).toHaveBeenCalledWith('This window cannot open a folder. The log is in C:/somewhere/data/logs.'));
  });
});

/**
 * What agents may do (PERM1, D72): the machine's `permissions.json` over the bridge, and every change
 * the screen's half of `daoris agent rules` (D50). Shaped as the wire carries it — the machine's scope
 * with no `name` and a clean file with no `problem`, because the bridge leaves a null out.
 */
describe('the rules card', () => {
  const RULES = {
    path: 'C:/somewhere/data/permissions.json',
    defaults: [
      { id: 'connector', list: 'allow', rules: ['mcp__daoris-knowledge__quest_respond'], why: 'The connector.', on: true },
      { id: 'no-push', list: 'deny', rules: ['Bash(git push:*)'], why: "A push stays the person's (D37).", on: true },
    ],
    scopes: [
      { scope: 'machine', allow: [], ask: [], deny: [] },
      { scope: 'repository', name: 'engine', allow: ['Bash(make:*)'], ask: [], deny: [] },
    ],
  };

  beforeEach(() => {
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => respond(String(input))));
    invoke.mockImplementation(async (_module: string, type: string) =>
      (type === 'RULES' || type === 'RULE_ACTION' ? RULES : WIRING));
  });
  afterEach(() => {
    vi.unstubAllGlobals();
    invoke.mockReset();
  });

  it('shows the defaults and each scope from the machine\'s own file, asking no service', async () => {
    show(<SettingsView notify={() => {}} section="permissions" />);

    expect(await screen.findByText('C:/somewhere/data/permissions.json')).toBeTruthy();
    expect(screen.getByRole('listitem', { name: 'no-push' })).toBeTruthy();
    expect(within(screen.getByRole('list', { name: 'repository engine' })).getByText('Bash(make:*)')).toBeTruthy();
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'RULES', {});
  });

  it('a default switched and a rule removed land on the bridge as the actions a terminal has', async () => {
    const notify = vi.fn();
    show(<SettingsView notify={notify} section="permissions" />);

    const push = await screen.findByRole('listitem', { name: 'no-push' });
    await userEvent.click(within(push).getByRole('checkbox'));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'RULE_ACTION', { payload: { action: 'default', id: 'no-push', on: false } });
    await waitFor(() => expect(notify).toHaveBeenCalledWith('The default no-push is off on this machine.'));

    await userEvent.click(screen.getByRole('button', { name: 'remove Bash(make:*)' }));
    expect(invoke).toHaveBeenCalledWith(
      'DAORIS.DRIVER', 'RULE_ACTION', { payload: { action: 'remove', rule: 'Bash(make:*)', scope: 'repository', name: 'engine' } });
  });

  /** PERM2 (D74): the person's answer to an agent's proposal lands as `daoris agent rules accept|decline` would. */
  it('a proposal accepted on the screen lands on the bridge by its id, and says what changed', async () => {
    const proposed = {
      ...RULES,
      proposals: [{
        id: 'p0000002', state: 'waiting', action: 'add', scope: 'machine', list: 'allow', rule: 'WebFetch',
        why: 'The docs it needs are on the web.', session: 'i9n8t7k6', proposed: '2026-09-24T11:00:00Z',
      }],
    };
    invoke.mockImplementation(async (_module: string, type: string) =>
      (type === 'RULES' || type === 'RULE_PROPOSAL' ? proposed : WIRING));
    const notify = vi.fn();
    show(<SettingsView notify={notify} section="permissions" />);

    const row = await screen.findByRole('listitem', { name: 'proposal #p0000002' });
    await userEvent.click(within(row).getByRole('button', { name: 'accept' }));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'RULE_PROPOSAL', { payload: { id: 'p0000002', accept: true } });
    await waitFor(() => expect(notify).toHaveBeenCalledWith(
      'Accepted: allow WebFetch for every session on this machine. Sessions started from now on are handed it.'));
  });

  /** A shell older than the rules answers something else to a question it never heard: no card, no blank page. */
  it('an older shell gets no card', async () => {
    invoke.mockImplementation(async (_module: string, type: string) => (type === 'RULES' ? undefined : WIRING));
    show(<SettingsView notify={() => {}} section="permissions" />);

    // The machine's domains appear once the shell has answered, and Permissions is the one open.
    await waitFor(() => expect(screen.getByRole('button', { name: 'Permissions' })).toHaveAttribute('aria-current', 'page'));
    expect(screen.queryByText('The rules file')).toBeNull();
  });
});

/**
 * Daoris's own AI (AGT6): the jobs it may use a model for, the tier answering each, and how to change
 * it. The search tier is the service's answer, over HTTP like every browser's; the intake is this
 * machine's `driver.json`, and its control is the screen's half of `daoris driver intake` (D50).
 */
describe("Daoris's own AI on Settings", () => {
  const ROSTER = {
    settingsPath: 'C:/somewhere/.daoris/harnesses.json',
    adapter: 'claude-code',
    harnesses: [
      {
        harness: 'claude-code', product: 'Claude Code', maker: 'Anthropic', wire: 'pipe',
        present: true, version: 'claude 9.9.9', problem: null, machineDefault: 'personal',
        pinned: null, managed: null, pinnable: true, ownLogin: 'in', workspaceDefaults: [],
        profiles: [{ name: 'personal', home: 'C:/somewhere/.daoris/harnesses/claude-code/personal', login: 'in' }],
      },
      {
        harness: 'claude-code-acp', product: 'Claude Code', maker: 'Anthropic', wire: 'acp', accountOf: 'claude-code',
        present: true, version: '0.9.1', problem: null, machineDefault: 'personal',
        pinned: null, managed: null, pinnable: true, ownLogin: 'in', workspaceDefaults: [], profiles: [],
      },
      {
        harness: 'codex', product: 'Codex', maker: 'OpenAI', present: false, version: null,
        problem: '`codex` is not on this machine\'s PATH', machineDefault: null,
        pinned: null, managed: null, pinnable: false, profiles: [],
      },
    ],
  };
  const start = (job: 'work' | 'intake', adapter: string) => ({
    job, workspace: 'default', adapter, owner: 'claude-code', product: 'Claude Code',
    profile: 'personal', profileFrom: 'machine', version: '0.9.1', versionFrom: 'unset',
    commanded: false, refusal: null,
  });

  let intakeAdapter: string | null = null;
  beforeEach(() => {
    intakeAdapter = null;
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => respond(String(input))));
    invoke.mockImplementation(async (module: string, type: string, request?: { payload?: { adapter?: string | null } }) => {
      if (module !== 'DAORIS.DRIVER') return WIRING;
      if (type === 'SET_INTAKE') intakeAdapter = request?.payload?.adapter ?? null;
      // As the wire carries it: off is "", never null — the bridge leaves a null out, and a missing
      // field is how the page knows a shell older than the intake (AGT6, seen on the window).
      if (type === 'STATE' || type === 'SET_INTAKE') return { ...DRIVER_STATE, intakeAdapter: intakeAdapter ?? '' };
      if (type === 'HARNESSES') return ROSTER;
      if (type === 'STARTS') {
        return {
          adapter: 'claude-code',
          starts: [start('work', 'claude-code'), ...(intakeAdapter ? [start('intake', intakeAdapter)] : [])],
        };
      }
      return undefined;
    });
  });
  afterEach(() => {
    vi.unstubAllGlobals();
    invoke.mockReset();
  });

  it("states the service's own tier, verbatim, and how the model is chosen", async () => {
    show(<SettingsView notify={() => {}} section="ai" />);

    expect(await screen.findByText('lexical only')).toBeTruthy();
    expect(screen.getByText(STATUS.note)).toBeTruthy();
    // The variable is code, as every row's terminal door is (UX5 U55).
    expect(screen.getByText('DAORIS_EMBED_MODEL').tagName).toBe('CODE');
  });

  /**
   * 🔴 Seen on the window (AGT6): with the intake off, the card offered no intake control at all —
   * the bridge left the null out, and the page read the missing field as a shell older than the
   * intake. Off is "" on the wire now, and reads as Off.
   */
  it('offers the intake control while it is off, reading as Off', async () => {
    show(<SettingsView notify={() => {}} section="ai" />);

    const trigger = await screen.findByRole('combobox', { name: 'the intake agent' });
    expect(trigger).toHaveTextContent('Off — declarations only');
  });

  /** A shell that answers no field has never heard of the intake: no control, not a disabled one. */
  it('offers no intake control on a shell older than the intake', async () => {
    invoke.mockImplementation(async (module: string, type: string) => {
      if (module !== 'DAORIS.DRIVER') return WIRING;
      if (type === 'STATE') return DRIVER_STATE;
      if (type === 'HARNESSES') return ROSTER;
      if (type === 'STARTS') return { adapter: 'claude-code', starts: [start('work', 'claude-code')] };
      return undefined;
    });
    show(<SettingsView notify={() => {}} section="ai" />);

    expect(await screen.findByText('lexical only')).toBeTruthy();
    // The machine's domains appear once the shell has answered: the shell is here, and old.
    await screen.findByRole('button', { name: 'Permissions' });
    expect(screen.queryByRole('combobox', { name: 'the intake agent' })).toBeNull();
  });

  /**
   * D50: the screen's half of `daoris driver intake <agent>|off`. It offers the ways in this machine
   * HAS — an agent that is not installed is not a choice — and lands on the same file.
   */
  it('names an agent this machine has for the intake, over SET_INTAKE, and says what that does', async () => {
    const notify = vi.fn();
    show(<SettingsView notify={notify} section="ai" />);

    const user = userEvent.setup();
    const trigger = await screen.findByRole('combobox', { name: 'the intake agent' });
    trigger.focus();
    await user.keyboard('{Enter}');
    expect(screen.queryByRole('option', { name: /codex/i })).toBeNull();
    await user.click(await screen.findByRole('option', { name: /claude-code-acp/ }));

    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SET_INTAKE', { payload: { adapter: 'claude-code-acp' } });
    await waitFor(() => expect(notify).toHaveBeenCalledWith(expect.stringContaining('intake session on claude-code-acp')));
  });

  /**
   * As whom: the driver's own answer for the intake, per circle — the same rows *What a start runs
   * on* draws, so the two cards cannot disagree about one account.
   */
  it('says which account an intake runs as, from the driver\'s answer, in both places it is drawn', async () => {
    intakeAdapter = 'claude-code-acp';
    // Two domains since D75, Daoris's own AI and Workspace: one answer read in each.
    const ai = show(<SettingsView notify={() => {}} section="ai" />);
    expect(await screen.findByText(/opens a session on claude-code-acp/)).toBeTruthy();
    const here = await screen.findByRole('listitem', { name: 'an intake in default' });
    expect(within(here).getByText('personal')).toBeTruthy();
    ai.unmount();

    show(<SettingsView notify={() => {}} section="workspace" />);
    const there = await screen.findByRole('listitem', { name: 'an intake in default' });
    expect(within(there).getByText('personal')).toBeTruthy();
  });

  it('turns the intake off as the terminal does — no agent named', async () => {
    intakeAdapter = 'claude-code-acp';
    const notify = vi.fn();
    show(<SettingsView notify={notify} section="ai" />);

    const user = userEvent.setup();
    const trigger = await screen.findByRole('combobox', { name: 'the intake agent' });
    await waitFor(() => expect(trigger).toHaveTextContent('claude-code-acp'));
    trigger.focus();
    await user.keyboard('{Enter}');
    await user.click(await screen.findByRole('option', { name: 'Off — declarations only' }));

    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SET_INTAKE', { payload: { adapter: null } });
    await waitFor(() => expect(notify).toHaveBeenCalledWith(expect.stringContaining('declarations only')));
  });
});

/**
 * The toolchain roster (D49 §4, D50): which harnesses this machine has, and which accounts they hold.
 *
 * Shell-only for the sharpest reason yet — a profile HOME is a filesystem path (D47 §4) — so, like
 * the wiring above, every call must land on the bridge and none on the API.
 *
 * The property that matters most here is a negative one: **no credential is anywhere on this
 * surface.** There is nowhere to type one, nothing that reads one, and logging in spawns the
 * harness's own flow. The last test in this block is what fails if that ever stops being true.
 */
describe('the harness roster', () => {
  const ROSTER = {
    settingsPath: 'C:/somewhere/.daoris/harnesses.json',
    adapter: 'claude-code',
    harnesses: [
      {
        harness: 'claude-code', product: 'Claude Code', maker: 'Anthropic',
        present: true, version: 'claude 9.9.9', problem: null,
        machineDefault: 'personal', pinned: null, managed: null, pinnable: true,
        ownLogin: 'in',
        workspaceDefaults: [{ workspace: 'orbit', profile: 'work' }],
        profiles: [
          { name: 'personal', home: 'C:/somewhere/.daoris/harnesses/claude-code/personal', login: 'in' },
          { name: 'work', home: 'C:/somewhere/.daoris/harnesses/claude-code/work', login: 'out' },
        ],
      },
      {
        harness: 'codex', present: false, version: null,
        problem: '`codex` is not on this machine\'s PATH', machineDefault: null,
        pinned: null, managed: null, pinnable: false, profiles: [],
      },
    ],
  };

  beforeEach(() => {
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => respond(String(input))));
    invoke.mockImplementation(async (_module: string, type: string) =>
      (type === 'HARNESSES' ? ROSTER : WIRING));
  });
  afterEach(() => {
    vi.unstubAllGlobals();
    invoke.mockReset();
  });

  it('reads the roster over the bridge and never over the service', async () => {
    show(<SettingsView notify={() => {}} section="agents" />);

    expect(await screen.findByText('claude 9.9.9')).toBeTruthy();
    // AGT1: a tool is named as a person knows it, with whose it is; one that says neither keeps its id.
    expect(screen.getByText('Claude Code')).toBeTruthy();
    expect(screen.getByText('Anthropic')).toBeTruthy();
    expect(screen.getAllByText('codex').length).toBeGreaterThan(0);
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'HARNESSES', {});
    expect(serviceCalls()).toEqual([]);
  });

  /**
   * MAP1b: what a start in each workspace would run on, beside the agents. The circles are the
   * registry's, the answer is the driver's, and the account is named the way the roster names it.
   */
  it('says what a start in each workspace would run on, asking the driver for the registry\'s circles', async () => {
    const STARTS = {
      adapter: 'claude-code',
      starts: [{
        job: 'work', workspace: 'default', adapter: 'claude-code', owner: 'claude-code', product: 'Claude Code',
        profile: 'personal', profileFrom: 'machine', version: 'claude 9.9.9', versionFrom: 'unset',
        commanded: false, refusal: null,
      }],
    };
    invoke.mockImplementation(async (_module: string, type: string) =>
      (type === 'HARNESSES' ? ROSTER : type === 'STARTS' ? STARTS : WIRING));
    show(<SettingsView notify={() => {}} section="workspace" />);

    const row = await screen.findByRole('listitem', { name: 'a start in default' });
    expect(within(row).getByText('personal')).toBeTruthy();
    expect(within(row).getByText("this machine's default")).toBeTruthy();
    expect(within(row).getByText('from PATH')).toBeTruthy();
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'STARTS', { payload: { workspaces: ['default'] } });
  });

  /** An absent harness names what it is and offers the action, rather than leaving a blank row. */
  it('an absent tool says so and offers its own installer', async () => {
    show(<SettingsView notify={() => {}} section="agents" />);

    // 🔴 Twice over, and on purpose: the TOOL says whether a person has it, and the way in says
    // whether that particular door is installed. They were one line when a door was a tool.
    expect(await screen.findAllByText('not installed')).not.toHaveLength(0);
    expect(screen.getByText(/is not on this machine's PATH/)).toBeTruthy();

    await userEvent.click(screen.getByRole('button', { name: 'install' }));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'HARNESS_ACTION', {
      payload: { harness: 'codex', action: 'install' },
    });
  });

  /**
   * 🔴 A login outlives its request (2026-09-23): the host answers `started`, the sign-in sits on the
   * row while the person is in the browser, and the end arrives as news — the row then says what
   * the person can do with the account, and the panel goes.
   */
  it('a login that has started stays on its row until its end arrives as news', async () => {
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'HARNESS_ACTION') return { harness: 'claude-code', action: 'login', started: true };
      return type === 'HARNESSES' ? ROSTER : WIRING;
    });
    const notify = vi.fn();
    show(<SettingsView notify={notify} section="agents" />);

    const logins = await screen.findAllByRole('button', { name: /^Log in/ });
    await userEvent.click(logins[1]!);

    expect(await screen.findByText('Signing in to work')).toBeTruthy();
    expect(notify).not.toHaveBeenCalled();

    await act(async () => {
      eventHandlers.get('DAORIS.HARNESS_ENDED')!({
        harness: 'claude-code', action: 'login', profile: 'work', exitCode: 0, problem: null,
      });
    });

    expect(notify).toHaveBeenCalledWith('work is signed in — sessions can run as it.');
    await waitFor(() => expect(screen.queryByText('Signing in to work')).toBeNull());
  });

  /**
   * SIGNIN1: a sign-in outlives leaving the Agents domain. Its running action was that domain's own
   * state, and its end was heard only while it was on screen: leaving mid-login lost the code panel
   * and the sentence saying how it ended. The application holds it above every view now.
   */
  it('a sign-in outlives leaving the Agents domain: its panel is there on the way back, and its end is said away from it', async () => {
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'HARNESS_ACTION') return { harness: 'claude-code', action: 'login', started: true };
      return type === 'HARNESSES' ? ROSTER : WIRING;
    });
    const notify = vi.fn();
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    const page = (section: 'agents' | 'appearance') => (
      <QueryClientProvider client={client}>
        <Tooltip.Provider>
          <HarnessRuns notify={notify}><SettingsView notify={notify} section={section} /></HarnessRuns>
        </Tooltip.Provider>
      </QueryClientProvider>
    );
    const { rerender } = render(page('agents'));

    await userEvent.click((await screen.findAllByRole('button', { name: /^Log in/ }))[1]!);
    expect(await screen.findByText('Signing in to work')).toBeTruthy();

    // Away, and back: the sign-in is still running, so its panel is still on its row.
    rerender(page('appearance'));
    expect(screen.queryByText('Signing in to work')).toBeNull();
    rerender(page('agents'));
    expect(await screen.findByText('Signing in to work')).toBeTruthy();

    // Away again when it ends: the person is told wherever they are.
    rerender(page('appearance'));
    await act(async () => {
      eventHandlers.get('DAORIS.HARNESS_ENDED')!({
        harness: 'claude-code', action: 'login', profile: 'work', exitCode: 0, problem: null,
      });
    });
    expect(notify).toHaveBeenCalledWith('work is signed in — sessions can run as it.');
  });

  it('each profile shows its login state, and logging in names the profile', async () => {
    show(<SettingsView notify={() => {}} section="agents" />);

    // The tool's own home and `personal` are both logged in; `work` is the one that is not.
    expect(await screen.findAllByText('logged in')).toHaveLength(2);
    expect(screen.getByText('not logged in')).toBeTruthy();

    // Two profiles, two buttons — the second one is `work`, which is the logged-out one.
    const logins = screen.getAllByRole('button', { name: /^Log in/ });
    await userEvent.click(logins[1]!);

    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'HARNESS_ACTION', {
      payload: { harness: 'claude-code', action: 'login', profile: 'work' },
    });
  });

  /**
   * 🔴 The account a person actually has — the tool's own configuration home — led nowhere: a
   * machine with no named profile read "No accounts" while its owner was logged in. It leads the
   * list now, with the login state the tool reports for its own home, and it is what sessions use
   * until a default is named. Daoris never logs into it: that is the tool's own business, and the
   * row says so instead of offering a button.
   */
  it('the tool’s own home leads the accounts, with its login state, and takes no login from here', async () => {
    show(<SettingsView notify={() => {}} section="agents" />);

    // Two tools, two own rows; claude-code's is first, in the roster's order.
    const own = (await screen.findAllByText("this machine's own"))[0]!.closest('li')!;
    expect(within(own).getByText('logged in')).toBeTruthy();
    expect(within(own).queryByRole('button', { name: /^Log in/ })).toBeNull();
    // `personal` is the machine's default, so the own home is not what sessions use — yet.
    expect(within(own).queryByText('sessions use this')).toBeNull();
    // Choosing it again names no profile: the default is CLEARED, not pointed somewhere.
    await userEvent.click(within(own).getByRole('button', { name: 'Make default' }));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'HARNESS_ACTION', {
      payload: { harness: 'claude-code', action: 'profile-default' },
    });
  });

  it('the tool’s own home is what sessions use when nothing is named', async () => {
    invoke.mockImplementation(async (_module: string, type: string) => (type === 'HARNESSES'
      ? { ...ROSTER, harnesses: [{ ...ROSTER.harnesses[0], machineDefault: null, profiles: [] }] }
      : WIRING));
    show(<SettingsView notify={() => {}} section="agents" />);

    const own = (await screen.findAllByText("this machine's own"))[0]!.closest('li')!;
    expect(within(own).getByText('sessions use this')).toBeTruthy();
    expect(screen.queryByText(/^No accounts/)).toBeNull();
  });

  /**
   * 🔴 An account is made by SIGNING IN (D66 §3). One press, no name typed first; the sign-in shows where
   * the new account will be, and the end names who signed in.
   */
  it('an account is made by signing in, and the end names who signed in', async () => {
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'HARNESS_ACTION') return { harness: 'claude-code', action: 'login-new', started: true };
      return type === 'HARNESSES' ? ROSTER : WIRING;
    });
    const notify = vi.fn();
    show(<SettingsView notify={notify} section="agents" />);

    // No name box anywhere: the account is named by who signs in.
    await screen.findByText('claude 9.9.9');
    expect(screen.queryByPlaceholderText(/a name/)).toBeNull();

    await userEvent.click(screen.getAllByRole('button', { name: 'Sign in to another account' })[0]!);
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'HARNESS_ACTION', {
      payload: { harness: 'claude-code', action: 'login-new' },
    });
    expect(await screen.findByText('Signing in to another Claude Code account')).toBeTruthy();

    await act(async () => {
      eventHandlers.get('DAORIS.HARNESS_ENDED')!({
        harness: 'claude-code', action: 'login-new', profile: 'account-1', exitCode: 0, problem: null,
        account: 'someone@example.invalid', kept: true,
      });
    });

    expect(notify).toHaveBeenCalledWith('Signed in as someone@example.invalid — sessions can run as it.');
    await waitFor(() => expect(screen.queryByText('Signing in to another Claude Code account')).toBeNull());
  });

  /** A sign-in that did not finish keeps nothing, and says so rather than going quiet. */
  it('a sign-in that kept nothing says so', async () => {
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'HARNESS_ACTION') return { harness: 'claude-code', action: 'login-new', started: true };
      return type === 'HARNESSES' ? ROSTER : WIRING;
    });
    const notify = vi.fn();
    show(<SettingsView notify={notify} section="agents" />);

    await userEvent.click((await screen.findAllByRole('button', { name: 'Sign in to another account' }))[0]!);
    await act(async () => {
      eventHandlers.get('DAORIS.HARNESS_ENDED')!({
        harness: 'claude-code', action: 'login-new', profile: 'account-1', exitCode: 0, problem: null,
        account: null, kept: false,
      });
    });

    expect(notify).toHaveBeenCalledWith('Nobody was signed in, so nothing was kept.', 'error');
  });

  /** A person knows an account by who is signed in there, not by `account-2`. */
  it('lists each account by who is signed in, where the tool says', async () => {
    invoke.mockImplementation(async (_module: string, type: string) => (type === 'HARNESSES'
      ? {
        ...ROSTER,
        harnesses: [{
          ...ROSTER.harnesses[0],
          ownAccount: 'owner@example.invalid',
          profiles: [
            { name: 'account-1', home: 'C:/somewhere/.daoris/harnesses/claude-code/account-1', login: 'in', account: 'someone@example.invalid' },
          ],
        }],
      }
      : WIRING));
    show(<SettingsView notify={() => {}} section="agents" />);

    const row = (await screen.findByText('someone@example.invalid')).closest('li')!;
    // The directory's name is still there, inside its path, for a terminal.
    expect(within(row).getByText(/account-1$/)).toBeTruthy();
    expect(screen.getByText('owner@example.invalid')).toBeTruthy();
    // Two different people: neither name repeats, so neither says which it is.
    expect(screen.queryByText("this machine's own")).toBeNull();
  });

  /**
   * Seen on the installed window (POLISH3): the tool's own home and an account made in Daoris, both
   * signed in as the same person, were two rows with one bold name, which read as one fact stated
   * twice. The name that repeats says which it is.
   */
  it('says which is the tool\'s own home when an account there is also one made here', async () => {
    invoke.mockImplementation(async (_module: string, type: string) => (type === 'HARNESSES'
      ? {
        ...ROSTER,
        harnesses: [{
          ...ROSTER.harnesses[0],
          ownAccount: 'owner@example.invalid',
          profiles: [
            { name: 'account-1', home: 'C:/somewhere/.daoris/harnesses/claude-code/account-1', login: 'in', account: 'owner@example.invalid' },
          ],
        }],
      }
      : WIRING));
    show(<SettingsView notify={() => {}} section="agents" />);

    const [own, made] = (await screen.findAllByText('owner@example.invalid')).map((name) => name.closest('li')!);
    expect(within(own!).getByText("this machine's own")).toBeTruthy();
    expect(within(made!).queryByText("this machine's own")).toBeNull();
  });

  /**
   * 🔴 Remove REMOVES (D66 §3) — Forget had kept a signed-in account on disk and on the list. It
   * deletes the sign-in, so the first press only asks, and says what the second will do.
   */
  it('Remove asks once, then deletes the account', async () => {
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'HARNESS_ACTION') return { harness: 'claude-code', action: 'profile-remove', exitCode: 0 };
      // The one line the host streams for it — which a console would render, were one shown.
      if (type === 'TAIL_SESSION') {
        return {
          session: 'claude-code:profile-remove', sequence: 1, live: false, dropped: 0,
          lines: [{ sequence: 1, text: 'removed … — the account and its sign-in are gone from this machine.' }],
        };
      }
      return type === 'HARNESSES' ? ROSTER : WIRING;
    });
    show(<SettingsView notify={() => {}} section="agents" />);

    const work = (await screen.findByText('work')).closest('li')!;
    await userEvent.click(within(work).getByRole('button', { name: 'Remove' }));
    expect(invoke).not.toHaveBeenCalledWith('DAORIS.DRIVER', 'HARNESS_ACTION', expect.anything());
    expect(within(work).getByText(/deletes the account from this machine, sign-in included/)).toBeTruthy();

    await userEvent.click(within(work).getByRole('button', { name: 'Remove it' }));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'HARNESS_ACTION', {
      payload: { harness: 'claude-code', action: 'profile-remove', profile: 'work' },
    });
    // An account edit is not a door's work: nothing streams under "Ways in" for it (seen on the
    // window, where the removal's one line sat under the direct door as a live console).
    await new Promise((settle) => setTimeout(settle, 50));
    expect(screen.queryByText(/its sign-in are gone/)).toBeNull();
  });

  it('Remove can be taken back before it deletes anything', async () => {
    show(<SettingsView notify={() => {}} section="agents" />);

    const work = (await screen.findByText('work')).closest('li')!;
    await userEvent.click(within(work).getByRole('button', { name: 'Remove' }));
    await userEvent.click(within(work).getByRole('button', { name: 'never mind' }));

    expect(within(work).queryByRole('button', { name: 'Remove it' })).toBeNull();
    expect(invoke).not.toHaveBeenCalledWith('DAORIS.DRIVER', 'HARNESS_ACTION', expect.anything());
  });

  /** A tool with no sign-in of its own offers neither sign-in button — the pin's rule. */
  it('a tool that cannot sign in offers no sign-in at all', async () => {
    invoke.mockImplementation(async (_module: string, type: string) => (type === 'HARNESSES'
      ? { ...ROSTER, harnesses: [{ ...ROSTER.harnesses[0], signsIn: false }] }
      : WIRING));
    show(<SettingsView notify={() => {}} section="agents" />);

    await screen.findByText('claude 9.9.9');
    expect(screen.queryByRole('button', { name: 'Sign in to another account' })).toBeNull();
    expect(screen.queryByRole('button', { name: /^Log in/ })).toBeNull();
  });

  /**
   * 🔴 USE1a: Update on a pinned door answered only a refusal. The roster now says which Update each
   * door has, and a door with none offers no button — Sign in's rule, and the pin's.
   */
  it('offers no update on a door the roster gives none', async () => {
    show(<SettingsView notify={() => {}} section="agents" />);

    await screen.findByText('claude 9.9.9');
    expect(screen.queryByRole('button', { name: 'update' })).toBeNull();
  });

  it('offers update on a pinned door, and says it moves the pin to the newest release', async () => {
    invoke.mockImplementation(async (_module: string, type: string) => (type === 'HARNESSES'
      ? {
        ...ROSTER,
        harnesses: [{
          ...ROSTER.harnesses[0], pinned: '0.79.0', updates: 'pin',
          managed: 'C:/somewhere/.daoris/toolchain/claude-code/0.79.0/bin/claude.exe',
        }, ROSTER.harnesses[1]],
      }
      : WIRING));
    show(<SettingsView notify={() => {}} section="agents" />);

    const update = await screen.findByRole('button', { name: 'update' });
    await userEvent.hover(update);
    expect(await screen.findByRole('tooltip')).toHaveTextContent(/newest release/);
    await userEvent.click(update);
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'HARNESS_ACTION', {
      payload: { harness: 'claude-code', action: 'update' },
    });
  });

  it('offers update on an unpinned door with its own updater, and says whose updater runs', async () => {
    invoke.mockImplementation(async (_module: string, type: string) => (type === 'HARNESSES'
      ? { ...ROSTER, harnesses: [{ ...ROSTER.harnesses[0], updates: 'tool' }, ROSTER.harnesses[1]] }
      : WIRING));
    show(<SettingsView notify={() => {}} section="agents" />);

    await userEvent.hover(await screen.findByRole('button', { name: 'update' }));
    expect(await screen.findByRole('tooltip')).toHaveTextContent(/own updater/);
  });

  /** After "Add", the next step and what it does were nowhere: the logged-out row says both. */
  it('a logged-out account says what Log in will do', async () => {
    show(<SettingsView notify={() => {}} section="agents" />);

    const work = (await screen.findByText('work')).closest('li')!;
    expect(within(work).getByText(/runs the tool's own sign-in/)).toBeTruthy();
    const personal = screen.getByText('personal').closest('li')!;
    expect(within(personal).queryByText(/runs the tool's own sign-in/)).toBeNull();
  });

  /**
   * 🔴 A work account for the work circle (D49 §4) could be set from a terminal and not from here.
   * The row says which circles use it, and a circle is chosen from the ones this machine has.
   */
  it('an account can be made one workspace’s default, and the row says which circles use it', async () => {
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => {
      const url = String(input);
      if (url.startsWith('/api/registry')) {
        return Response.json([
          { ...REGISTRY[0], workspace: 'orbit' },
          { ...REGISTRY[0], repository: 'game', workspace: 'lab' },
        ]);
      }
      return respond(url);
    }));
    show(<SettingsView notify={() => {}} section="agents" />);

    const work = (await screen.findByText('work')).closest('li')!;
    expect(within(work).getByText('sessions in orbit use this')).toBeTruthy();

    // Opened from the keyboard: in this suite a pointer click leaves the Radix trigger closed, where
    // the App suite's does not, and Enter is a door the person has too.
    const trigger = await screen.findByRole('combobox', { name: 'use personal for a workspace' });
    trigger.focus();
    await userEvent.keyboard('{Enter}');
    await userEvent.click(await screen.findByRole('option', { name: 'lab' }));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'HARNESS_ACTION', {
      payload: { harness: 'claude-code', action: 'profile-default', profile: 'personal', workspace: 'lab' },
    });
  });

  /**
   * A sign-in is the tool's to keep (D49 §4): there is no field for a token or a password, and the
   * surface says where a sign-in lives. An API key is the one exception (D67 §1), and it has its own
   * test below: one field, behind a press, on an agent that takes a key.
   */
  it('offers nowhere to put a sign-in, and says where one lives instead', async () => {
    const { container } = show(<SettingsView notify={() => {}} section="agents" />);
    await screen.findByText('claude 9.9.9');

    const card = screen.getByText('Agent tools').closest('section, div')!;
    expect(within(card as HTMLElement).queryByLabelText(/token|password|credential|API key/i)).toBeNull();
    expect(container.textContent).toContain('the tool stores itself');
  });

  /**
   * 🔴 An account that is an API key (AGT3, D67 §1). One password field,
   * behind a press, only on an agent that takes a key; saving sends the key once and the page is
   * told back only its last four characters.
   */
  it('takes an API key once, behind a press, and never shows it back', async () => {
    const key = 'sk-ant-api03-page-test-wxyz';
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'HARNESS_ACTION') {
        return { harness: 'claude-code', action: 'key-add', exitCode: 0, profile: 'account-1', key: '…wxyz' };
      }
      return type === 'HARNESSES'
        ? { ...ROSTER, harnesses: [{ ...ROSTER.harnesses[0], takesKey: true }, ROSTER.harnesses[1]] }
        : WIRING;
    });
    const notify = vi.fn();
    const { container } = show(<SettingsView notify={notify} section="agents" />);

    // Only the agent that takes a key offers it; codex, in this roster, does not.
    await screen.findByText('claude 9.9.9');
    expect(screen.getAllByRole('button', { name: 'Add an API key' })).toHaveLength(1);
    await userEvent.click(screen.getByRole('button', { name: 'Add an API key' }));
    const field = screen.getByLabelText('API key for Claude Code');
    expect(field.getAttribute('type')).toBe('password');

    fireEvent.change(field, { target: { value: key } });
    await userEvent.click(screen.getByRole('button', { name: 'Save the key' }));

    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'HARNESS_ACTION', {
      payload: { harness: 'claude-code', action: 'key-add', key },
    });
    await waitFor(() => expect(notify).toHaveBeenCalledWith('Added account-1, the API key …wxyz.'));
    // The field is gone, and the key with it.
    expect(screen.queryByLabelText('API key for Claude Code')).toBeNull();
    expect(container.innerHTML).not.toContain(key);
  });

  /**
   * 🔴 Cancel is no. Its button sat untyped inside the key's form, so it was a SUBMIT: pressing it
   * closed the field and saved the key typed into it (2026-09-25, found beside the composer's twin).
   */
  it('saves nothing when the person cancels a typed key', async () => {
    invoke.mockImplementation(async (_module: string, type: string) => (type === 'HARNESSES'
      ? { ...ROSTER, harnesses: [{ ...ROSTER.harnesses[0], takesKey: true }, ROSTER.harnesses[1]] }
      : WIRING));
    show(<SettingsView notify={() => {}} section="agents" />);

    await userEvent.click(await screen.findByRole('button', { name: 'Add an API key' }));
    fireEvent.change(screen.getByLabelText('API key for Claude Code'), { target: { value: 'sk-ant-api03-no' } });
    await userEvent.click(screen.getByRole('button', { name: 'never mind' }));

    expect(screen.queryByLabelText('API key for Claude Code')).toBeNull();
    expect(invoke).not.toHaveBeenCalledWith('DAORIS.DRIVER', 'HARNESS_ACTION', expect.anything());
  });

  /**
   * AGT6 (D98): an account's own model and effort, read from the tool's own file under it and changed
   * there — the screen's half of `daoris agent settings`. The choices are the tool's, from the roster;
   * the change goes over the bridge as the keys the person changed, and nothing else.
   */
  it("sets an account's own model over the bridge, from the choices the tool offers", async () => {
    const CHOICES = { models: ['default', 'sonnet', 'opus'], efforts: ['low', 'medium', 'high', 'xhigh'] };
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'SET_AGENT_SETTINGS') return { harness: 'claude-code', profile: 'work', model: 'sonnet', effort: 'high', perModel: [], problem: null };
      return type === 'HARNESSES'
        ? {
          ...ROSTER,
          harnesses: [{
            ...ROSTER.harnesses[0],
            settingsChoices: CHOICES,
            profiles: [
              { name: 'work', home: 'C:/somewhere/.daoris/harnesses/claude-code/work', login: 'in',
                settings: { model: 'opus', effort: 'high', perModel: [], problem: null } },
            ],
          }, ROSTER.harnesses[1]],
        }
        : WIRING;
    });
    const notify = vi.fn();
    show(<SettingsView notify={notify} section="agents" />);

    const row = (await screen.findByText('model opus · effort high')).closest('li')!;
    await userEvent.click(within(row).getByRole('button', { name: 'Model & effort' }));
    const form = within(row).getByRole('group', { name: 'work: model and effort' });
    within(form).getByRole('combobox', { name: 'model' }).focus();
    await userEvent.keyboard('{Enter}');
    await userEvent.click(await screen.findByRole('option', { name: 'sonnet' }));
    await userEvent.click(within(form).getByRole('button', { name: 'Save' }));

    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SET_AGENT_SETTINGS', {
      payload: { harness: 'claude-code', profile: 'work', model: 'sonnet' },
    });
    await waitFor(() => expect(notify).toHaveBeenCalledWith('work: saved — the next session reads it.'));
    expect(serviceCalls()).toEqual([]);
  });

  /** A tool whose settings Daoris does not know is offered none, said in one line (AGT6). */
  it("offers no settings for a tool whose own Daoris does not know, and says so once", async () => {
    invoke.mockImplementation(async (_module: string, type: string) => (type === 'HARNESSES'
      ? {
        ...ROSTER,
        harnesses: [ROSTER.harnesses[0], { ...ROSTER.harnesses[1], product: 'Codex', settingsChoices: null,
          profiles: [{ name: 'work', home: 'C:/somewhere/.daoris/harnesses/codex/work', login: 'in', settings: null }] }],
      }
      : WIRING));
    show(<SettingsView notify={() => {}} section="agents" />);

    expect(await screen.findByText("Daoris does not know Codex's own settings, so it offers none — set its model with the tool itself.")).toBeTruthy();
    expect(screen.queryByRole('button', { name: 'Model & effort' })).toBeNull();
  });

  /** A key account reads as its handle and offers no sign-in: it is signed in by its key. */
  it('lists a key account by its handle, with no sign-in to offer', async () => {
    invoke.mockImplementation(async (_module: string, type: string) => (type === 'HARNESSES'
      ? {
        ...ROSTER,
        harnesses: [{
          ...ROSTER.harnesses[0],
          takesKey: true,
          profiles: [
            { name: 'account-1', home: 'C:/somewhere/.daoris/harnesses/claude-code/account-1', login: 'in', key: '…wxyz' },
          ],
        }],
      }
      : WIRING));
    show(<SettingsView notify={() => {}} section="agents" />);

    const row = (await screen.findByText('API key …wxyz')).closest('li')!;
    expect(within(row).queryByRole('button', { name: /^Log in/ })).toBeNull();
    // 🔴 Never "logged in": the tool says so for any key, a wrong one included (measured, AGT3).
    expect(within(row).getByText('unchecked')).toBeTruthy();
    expect(within(row).queryByText('logged in')).toBeNull();
    expect(within(row).getByRole('button', { name: 'Remove' })).toBeTruthy();
  });

  /** A door a plugin declared says so beside its name (D64), and the build's own say nothing. */
  it('a declared door names the plugin it came from', async () => {
    invoke.mockImplementation(async (_module: string, type: string) => (type === 'HARNESSES'
      ? {
        ...ROSTER,
        harnesses: [
          ...ROSTER.harnesses,
          {
            harness: 'acme-agent', present: true, version: '(not asked)', problem: null, wire: 'acp',
            machineDefault: null, pinned: null, managed: null, pinnable: false, profiles: [], plugin: 'acme.gate',
          },
        ],
      }
      : WIRING));
    show(<SettingsView notify={() => {}} section="agents" />);

    expect(await screen.findByText('declared by plugin acme.gate')).toBeTruthy();
    expect(screen.getAllByText(/declared by plugin/)).toHaveLength(1);
  });

  /** A shell older than this surface answers something else; the rest of the page must stand. */
  it('an answer that is not a roster draws no tools, and takes the page down with it nowhere', async () => {
    invoke.mockImplementation(async () => WIRING); // no `harnesses` anywhere in it

    show(<SettingsView notify={() => {}} section="agents" />);

    expect(await screen.findByRole('navigation', { name: 'Settings domains' })).toBeTruthy();
    expect(screen.queryByText('Agent tools')).toBeNull();
  });

  /**
   * What each account has carried (TOOL3/D57 §4). Absent entirely on a machine that has measured
   * nothing — a row of zeroes would claim sessions used nothing, when what is true is that nothing
   * was measured.
   */
  it('says nothing about usage on a machine that has measured none', async () => {
    show(<SettingsView notify={() => {}} section="agents" />);

    await screen.findByText('claude 9.9.9');
    expect(screen.queryByText('What each account has carried')).toBeNull();
  });

  /**
   * UX5 U72, seen on the window: the Agents menu's *Usage* opened this domain at its top, and the
   * usage sat a screen below it. Opened for a part, the page brings that part into view once it is
   * drawn, and says it has, so a later visit opens at the top again.
   */
  it('opens at the part a menu item names, once that part is drawn', async () => {
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'HARNESSES') return ROSTER;
      if (type === 'USAGE') return { sessions: [], accounts: [{ harness: 'claude-code', profile: null, sessions: 1, used: 9000 }] };
      return WIRING;
    });
    const scrolled = vi.fn();
    const original = Element.prototype.scrollIntoView;
    Element.prototype.scrollIntoView = function scroll(this: Element) { scrolled(this.id); };
    const anchored = vi.fn();
    try {
      show(<SettingsView notify={() => {}} section="agents" anchor="usage" onAnchored={anchored} />);

      await screen.findByText('What each account has carried');
      await waitFor(() => expect(scrolled).toHaveBeenCalledWith('settings-usage'));
      expect(anchored).toHaveBeenCalled();
    } finally {
      Element.prototype.scrollIntoView = original;
    }
  });

  it('totals what each account carried, and names the one with no profile', async () => {
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'HARNESSES') return ROSTER;
      if (type === 'USAGE') {
        return {
          sessions: [],
          accounts: [
            { harness: 'claude-code', profile: 'work', sessions: 2, used: 60000 },
            { harness: 'claude-code', profile: null, sessions: 1, used: 9000 },
          ],
        };
      }
      return WIRING;
    });

    show(<SettingsView notify={() => {}} section="agents" />);

    expect(await screen.findByText('What each account has carried')).toBeTruthy();
    // The unit is named beside the figure: a bare number in a column says nothing, and "context"
    // is the honest word — calling them tokens would be a claim Daoris cannot make.
    expect(screen.getByText('60,000 context')).toBeTruthy();
    // A session on the harness's own configuration home is still somebody's usage, and each
    // account is named as the list above names it (UX5 U53): it said "its own home", and a named
    // account by its directory in the accent.
    const usage = screen.getByText('What each account has carried').parentElement!;
    expect(within(usage).getByText("this machine's own")).toBeTruthy();
    expect(within(usage).getByText('work').getAttribute('class')).not.toContain('accent');
    // Its note keeps a reading measure once the column follows the window (UX5 U59: 142 a line).
    expect(within(usage).getByText(/Measured, not billed/)).toHaveClass('max-w-prose');
    // 🔴 No price is claimed anywhere — Daoris does not know what a token costs (D24).
    expect(screen.queryByText(/[$£€]/)).toBeNull();
  });

  /**
   * The managed toolchain (TOOL2/D57), desktop half. **Absent means PATH**, and the surface says so
   * rather than leaving a blank — "Daoris manages this" and "the machine happens to have one" are
   * different facts about the same working session.
   */
  it('says a harness runs from PATH until something is pinned', async () => {
    show(<SettingsView notify={() => {}} section="agents" />);

    // Said on the door itself, where the pin control is — the roster's body no longer restates it.
    expect(await screen.findAllByText(/from PATH/)).not.toHaveLength(0);
    expect(screen.queryByText(/Daoris runs/)).toBeNull();
  });

  /**
   * UX5 U56: a way in that is not there does not run from PATH yet. It read *runs from PATH* under
   * *codex-acp is not on this machine's PATH*.
   */
  it('says a missing way in will run from PATH once it is there', async () => {
    invoke.mockImplementation(async (_module: string, type: string) => (type === 'HARNESSES'
      ? { ...ROSTER, harnesses: ROSTER.harnesses.map((door) => (door.harness === 'codex' ? { ...door, pinnable: true } : door)) }
      : WIRING));
    show(<SettingsView notify={() => {}} section="agents" />);

    expect(await screen.findByText('runs from PATH once it is there')).toBeTruthy();
    // The installed way in still runs from PATH, and says so plainly.
    expect(screen.getByText('runs from PATH')).toBeTruthy();
  });

  it('pinning installs that version and pins to it, in one action', async () => {
    show(<SettingsView notify={() => {}} section="agents" />);

    // The form is behind a press now: five always-open version boxes were the widest thing on
    // the surface and almost nobody types in one.
    await userEvent.click((await screen.findAllByRole('button', { name: 'Pin a version' }))[0]!);
    const version = await screen.findByLabelText('version of claude-code to pin');
    await userEvent.type(version, '1.2.3');
    await userEvent.click(screen.getAllByRole('button', { name: 'pin it' })[0]!);

    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'HARNESS_ACTION', {
      payload: { harness: 'claude-code', action: 'pin', profile: undefined, version: '1.2.3' },
    });
  });

  it('offers nothing to press until a version is typed', async () => {
    show(<SettingsView notify={() => {}} section="agents" />);

    await userEvent.click((await screen.findAllByRole('button', { name: 'Pin a version' }))[0]!);
    const [pin] = await screen.findAllByRole('button', { name: 'pin it' });
    expect(pin).toBeDisabled();
  });

  /**
   * 🔴 A harness that declares no package has no version for Daoris to fetch, so the control is
   * **absent** rather than present and refusing — half a control is worse than none. Found by
   * looking at the real window, where the stub adapter was offering a button whose only possible
   * outcome was a refusal.
   */
  it('offers no pin at all for a harness that cannot be pinned', async () => {
    show(<SettingsView notify={() => {}} section="agents" />);

    await screen.findByText('claude 9.9.9');
    // Only the pinnable tool offers the disclosure at all, though the roster carries two.
    expect(screen.getAllByRole('button', { name: 'Pin a version' })).toHaveLength(1);
  });

  /**
   * 🔴 A pin naming a version nobody installed **refuses every spawn**, so the surface must say that
   * rather than show the pin as though it were in force. The two fields are answered separately by
   * the host precisely so this state is renderable.
   */
  it('a pin with nothing installed at it reads as missing, not as in force', async () => {
    invoke.mockImplementation(async (_module: string, type: string) => (type === 'HARNESSES'
      ? {
        ...ROSTER,
        harnesses: [{ ...ROSTER.harnesses[0], pinned: '9.9.9', managed: null }],
      }
      : WIRING));

    show(<SettingsView notify={() => {}} section="agents" />);

    expect(await screen.findByText(/pinned 9\.9\.9 — not installed/)).toBeTruthy();
    // And the way back is offered, because a refusing pin is exactly when somebody wants it.
    expect(screen.getByRole('button', { name: 'use PATH again' })).toBeTruthy();
  });

  it('a pin that is in force names the binary sessions actually run', async () => {
    invoke.mockImplementation(async (_module: string, type: string) => (type === 'HARNESSES'
      ? {
        ...ROSTER,
        harnesses: [{
          ...ROSTER.harnesses[0],
          pinned: '1.2.3',
          managed: 'C:/somewhere/.daoris/toolchain/claude-code/1.2.3/node_modules/.bin/claude',
        }],
      }
      : WIRING));

    show(<SettingsView notify={() => {}} section="agents" />);

    expect(await screen.findByText('Daoris runs 1.2.3')).toBeTruthy();
    expect(screen.getByText(/toolchain[\\/]claude-code[\\/]1\.2\.3/)).toBeTruthy();
  });

  // The per-session PICKER moved with the start form it belongs to (`work/WorkFrame.test.tsx`).
});
