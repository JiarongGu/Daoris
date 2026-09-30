import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

// The Plugins domain in SHELL mode. Moved from `shell.test.tsx` with MOD4: tests follow their code.

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

import { SettingsView } from '../SettingsView';
import { respond, serviceCalls, show, WIRING } from '../test/shellHarness';

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

  /** PLUG10 (P9): running is a state, not an outcome, so its pill never wears done's green. */
  it('the running pill wears the in-progress hue, never done\'s', async () => {
    show(<SettingsView notify={() => {}} section="plugins" />);

    const pill = await screen.findByText('running');
    expect(pill.className).toMatch(/\bborder-st-taken\b/);
    expect(pill.className).not.toMatch(/st-done/);
  });

  it('the switch lands on the bridge as the action a terminal has', async () => {
    show(<SettingsView notify={() => {}} section="plugins" />);
    const row = (await screen.findByText('Acme gate')).closest('div')!.parentElement!.parentElement!;

    await userEvent.click(within(row as HTMLElement).getByRole('button', { name: 'Turn off' }));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'PLUGIN_ACTION', { payload: { id: 'acme.gate', action: 'disable' } });
  });

  /**
   * PLUG10 (P8): a removal takes the install folder, which only a fresh install brings back, so the first
   * press only asks, saying what the second will do and where what the plugin kept stays.
   */
  it('Remove asks once: the first press says what the second does, and only the second reaches the bridge', async () => {
    show(<SettingsView notify={() => {}} section="plugins" />);
    const row = (await screen.findByText('Acme gate')).closest('div')!.parentElement!.parentElement!;
    const removals = () => invoke.mock.calls.filter(([, type]) => type === 'PLUGIN_ACTION');

    await userEvent.click(within(row as HTMLElement).getByRole('button', { name: 'Remove…' }));
    const ask = screen.getByRole('group', { name: 'remove acme.gate' });
    expect(within(ask).getByText(/What it kept stays at/)).toHaveTextContent('C:/somewhere/data/plugins/.data/acme.gate');
    expect(removals()).toEqual([]);

    await userEvent.click(within(ask).getByRole('button', { name: 'Never mind' }));
    expect(screen.queryByRole('group', { name: 'remove acme.gate' })).toBeNull();
    expect(removals()).toEqual([]);

    await userEvent.click(within(row as HTMLElement).getByRole('button', { name: 'Remove…' }));
    await userEvent.click(within(screen.getByRole('group', { name: 'remove acme.gate' })).getByRole('button', { name: 'Remove plugin' }));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'PLUGIN_ACTION', { payload: { id: 'acme.gate', action: 'remove' } });
    expect(removals()).toHaveLength(1);
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
    const { default: i18n } = await import('../i18n');
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

      // PLUG10 (P8): the ask once, in the glossary's words — 移除 is remove's, 插件 plugin's.
      await userEvent.click(screen.getByRole('button', { name: '移除…' }));
      const ask = screen.getByRole('group', { name: '移除 acme.gate' });
      expect(within(ask).getByText(/它保存的内容留在/)).toBeTruthy();
      expect(within(ask).getByRole('button', { name: '确认移除插件' })).toBeTruthy();
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

    await userEvent.type(within(kit).getByRole('textbox', { name: 'Plugin ID' }), 'acme.new');
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
