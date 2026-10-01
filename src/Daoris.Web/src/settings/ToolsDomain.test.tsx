import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

// Settings → Tools in SHELL mode (TOOLS7, D121 §4.1): every call goes over the bridge to DAORIS.DRIVER, and nothing
// over the service, since a tool's file is this machine's (D47 §4).

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

import { act } from 'react';
import { SettingsView } from '../SettingsView';
import { DRIVER_STATE, respond, serviceCalls, show } from '../test/shellHarness';
import { TOOLS } from './toolsFixtures';

type Payload = Record<string, unknown> | undefined;

/** What the shell answers each route, unless a test says otherwise. */
const answers: Record<string, (payload: Payload) => unknown> = {
  TOOLS_LIST: () => TOOLS,
  TOOLS_USE: (payload) => ({ tool: payload?.tool, action: payload?.action, started: payload?.action === 'managed', version: payload?.version }),
  TOOLS_DOWNLOAD: (payload) => ({ tool: payload?.tool, action: 'download', started: true, version: payload?.version }),
  TOOLS_STOP: (payload) => ({ tool: payload?.tool, stopped: true }),
  TOOLS_DELETE: (payload) => ({ tool: payload?.tool, version: payload?.version }),
  TOOLS_LOCATION: (payload) => ({ address: payload?.address, added: true, removed: true }),
  TOOLS_LOOK: () => ({ looks: [{ address: 'https://mirror.example/resources.json', outcome: 'fetched', sentence: 'fetched', added: ['node 22.12.0'], dropped: [] }] }),
  TOOLS_GIT: () => ({
    now: { file: 'C:/somewhere/Git/cmd/git.exe' },
    then: { file: 'C:/somewhere/data/tools/git/2.51.0/package/cmd/git.exe' },
    keys: [{ key: 'core.autocrlf', now: 'true' }],
    same: false,
  }),
  TOOLS_PICK: () => ({ can: true, file: 'C:/tools/gh.exe' }),
  TAIL_SESSION: (payload) => ({ session: payload?.id, lines: [{ sequence: 1, text: '  downloading https://github.com/cli/cli/…' }], sequence: 1, live: true, dropped: 0 }),
};

const calls = (type: string) => invoke.mock.calls.filter(([module, route]) => module === 'DAORIS.DRIVER' && route === type);

/** A card by the name its header carries; it throws until the card is drawn, so `waitFor` waits for it. */
const card = (name: string) => {
  const found = screen.getAllByRole('article').find((article) => article.querySelector('header span')?.textContent === name);
  if (!found) throw new Error(`no card named ${name} yet`);
  return found;
};

describe('the tools domain', () => {
  beforeEach(() => {
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => respond(String(input))));
    invoke.mockImplementation(async (module: string, type: string, options?: { payload?: Payload }) => {
      if (module !== 'DAORIS.DRIVER') return undefined;
      return answers[type]?.(options?.payload) ?? DRIVER_STATE;
    });
  });
  afterEach(() => {
    vi.unstubAllGlobals();
    invoke.mockReset();
    eventHandlers.clear();
  });

  it('is a domain of a desktop: every tool in its order, read from the machine and then asked its version, nothing over the service', async () => {
    show(<SettingsView notify={() => {}} section="tools" />);

    expect(await within(screen.getByRole('navigation', { name: 'Settings domains' })).findByRole('button', { name: 'Tools' }))
      .toHaveAttribute('aria-current', 'page');
    await screen.findByText('Node.js', { selector: 'span' });
    expect(screen.getAllByRole('article').map((article) => article.querySelector('header span')?.textContent))
      .toEqual(['Git', 'Node.js', 'PowerShell', 'GitHub CLI', 'Azure CLI', 'Resource locations']);
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'TOOLS_LIST', {});
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'TOOLS_LIST', { payload: { ask: true } });
    expect(serviceCalls()).toEqual([]);
  });

  /** A managed use downloads first and is followed (§3.6): answered once started, its end said when the shell says it. */
  it('uses a managed version on the press, and says its end when the shell announces it', async () => {
    const notify = vi.fn();
    show(<SettingsView notify={notify} section="tools" />);
    const gh = await waitFor(() => card('GitHub CLI'));

    await userEvent.click(within(gh).getByRole('radio', { name: 'Managed' }));
    await userEvent.click(within(gh).getByRole('button', { name: 'Use this version' }));

    await waitFor(() => expect(invoke).toHaveBeenCalledWith(
      'DAORIS.DRIVER', 'TOOLS_USE', { payload: { tool: 'gh', action: 'managed', version: '2.63.0' } }));
    expect(notify).not.toHaveBeenCalled();
    act(() => eventHandlers.get('DAORIS.TOOLS_ENDED')!({
      tool: 'gh', name: 'GitHub CLI', action: 'use', version: '2.63.0', exitCode: 0, stopped: false,
    }));
    expect(notify).toHaveBeenCalledWith('GitHub CLI now runs managed 2.63.0.', 'ok');
  });

  /** §4.1: what a switch of git changes is said before it applies, asked of both gits, and the switch is the second press. */
  it('asks both gits what a switch changes before it applies, and switches on the second press', async () => {
    show(<SettingsView notify={() => {}} section="tools" />);
    const git = await waitFor(() => card('Git'));

    await userEvent.click(within(git).getByRole('radio', { name: 'Managed' }));
    await userEvent.click(within(git).getByRole('button', { name: 'Use this version' }));

    await waitFor(() => expect(invoke).toHaveBeenCalledWith(
      'DAORIS.DRIVER', 'TOOLS_GIT', { payload: { way: 'managed', version: '2.51.0' } }));
    const switching = await within(git).findByRole('group', { name: 'What this switch changes' });
    expect(await within(switching).findByText('is true now, and not set after')).toBeInTheDocument();
    expect(calls('TOOLS_USE')).toEqual([]);

    await userEvent.click(within(switching).getByRole('button', { name: 'Switch Git' }));
    await waitFor(() => expect(invoke).toHaveBeenCalledWith(
      'DAORIS.DRIVER', 'TOOLS_USE', { payload: { tool: 'git', action: 'managed', version: '2.51.0' } }));
  });

  /** A refusal is the catalogue's sentence, from its code and parameters, never a bare code. */
  it('names a file from the picker, and says a file that answers no version in the catalogue\'s words', async () => {
    const notify = vi.fn();
    answers.TOOLS_USE = () => {
      throw { code: 'TOOL_FILE_NO_VERSION', parameters: { tool: 'GitHub CLI', file: 'C:/tools/gh.exe', reason: 'it did not start' } };
    };
    try {
      show(<SettingsView notify={notify} section="tools" />);
      const gh = await waitFor(() => card('GitHub CLI'));

      await userEvent.click(within(gh).getByRole('radio', { name: 'Custom' }));
      await userEvent.click(within(gh).getByRole('button', { name: 'Browse…' }));
      await waitFor(() => expect(invoke).toHaveBeenCalledWith(
        'DAORIS.DRIVER', 'TOOLS_PICK', { payload: { title: 'Choose GitHub CLI' }, timeoutMs: Infinity }));
      expect(await within(gh).findByDisplayValue('C:/tools/gh.exe')).toBeInTheDocument();

      await userEvent.click(within(gh).getByRole('button', { name: 'Use this file' }));
      await waitFor(() => expect(notify).toHaveBeenCalledWith(
        'GitHub CLI was not set to C:/tools/gh.exe, since it did not answer its version: it did not start', 'error'));
    } finally {
      answers.TOOLS_USE = (payload) => ({ tool: payload?.tool, action: payload?.action, started: payload?.action === 'managed' });
    }
  });

  it('shows a running download\'s console and stops it on the press', async () => {
    const running = { ...TOOLS, tools: TOOLS.tools.map((tool) => (tool.tool === 'gh' ? { ...tool, running: { action: 'download', version: '2.63.0' } } : tool)) };
    answers.TOOLS_LIST = () => running;
    try {
      show(<SettingsView notify={() => {}} section="tools" />);
      const gh = await waitFor(() => card('GitHub CLI'));

      expect(await within(gh).findByText(/downloading https:\/\/github\.com\/cli\/cli/)).toBeInTheDocument();
      expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'TAIL_SESSION', { payload: { id: 'tools:gh' } });
      await userEvent.click(within(gh).getByRole('button', { name: 'Stop download' }));
      await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'TOOLS_STOP', { payload: { tool: 'gh' } }));
    } finally {
      answers.TOOLS_LIST = () => TOOLS;
    }
  });

  it('downloads a version on its press, and deletes one only on the second press', async () => {
    const notify = vi.fn();
    show(<SettingsView notify={notify} section="tools" />);
    const node = await waitFor(() => card('Node.js'));

    // A select opens from the keyboard in this document, as the asks' tests open theirs.
    const choose = async (option: string) => {
      within(node).getByRole('combobox', { name: 'The version of Node.js' }).focus();
      await userEvent.keyboard('{Enter}');
      await userEvent.click(await screen.findByRole('option', { name: option }));
    };

    await choose('22.12.0 · 31.2 MB');
    await userEvent.click(within(node).getByRole('button', { name: 'Download' }));
    await waitFor(() => expect(invoke).toHaveBeenCalledWith(
      'DAORIS.DRIVER', 'TOOLS_DOWNLOAD', { payload: { tool: 'node', version: '22.12.0' } }));

    await choose('20.18.0 · downloaded');
    await userEvent.click(within(node).getByRole('button', { name: 'Delete' }));
    expect(calls('TOOLS_DELETE')).toEqual([]);
    await userEvent.click(within(node).getByRole('button', { name: 'Confirm delete' }));
    await waitFor(() => expect(invoke).toHaveBeenCalledWith(
      'DAORIS.DRIVER', 'TOOLS_DELETE', { payload: { tool: 'node', version: '20.18.0' } }));
    await waitFor(() => expect(notify).toHaveBeenCalledWith('Node.js 20.18.0 is deleted.'));
  });

  /** §3.7: a look waits a look's bound for each location (`hostBounds`), and says what it fetched. */
  it('looks for updates waiting as long as the shell may look, and adds and removes a location', async () => {
    const notify = vi.fn();
    show(<SettingsView notify={notify} section="tools" />);
    const locations = await waitFor(() => card('Resource locations'));

    await userEvent.click(within(locations).getByRole('button', { name: 'Look for updates' }));
    // One location: a look's half minute, and the two minutes every long route adds for the host's own work.
    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'TOOLS_LOOK', { timeoutMs: 150_000 }));
    await waitFor(() => expect(notify).toHaveBeenCalledWith('Fetched 1 of 1 list.', 'ok'));
    expect(await within(locations).findByText('Added node 22.12.0')).toBeInTheDocument();

    await userEvent.click(within(locations).getByRole('button', { name: 'Add location…' }));
    await userEvent.type(within(locations).getByRole('textbox', { name: 'Address' }), 'https://team.example/resources.json');
    await userEvent.click(within(locations).getByRole('button', { name: 'Add location' }));
    await waitFor(() => expect(invoke).toHaveBeenCalledWith(
      'DAORIS.DRIVER', 'TOOLS_LOCATION', { payload: { action: 'add', address: 'https://team.example/resources.json' } }));

    await userEvent.click(within(locations).getByRole('button', { name: 'Remove' }));
    await waitFor(() => expect(invoke).toHaveBeenCalledWith(
      'DAORIS.DRIVER', 'TOOLS_LOCATION', { payload: { action: 'remove', address: 'https://mirror.example/resources.json' } }));
    await waitFor(() => expect(notify).toHaveBeenCalledWith('https://mirror.example/resources.json is removed. What was downloaded from it stays.'));
  });
});
