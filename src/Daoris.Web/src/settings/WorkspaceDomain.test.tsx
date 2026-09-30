import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

// The Workspace domain in SHELL mode: the machine's wiring, which only a desktop may render. Moved from
// `shell.test.tsx` with MOD4: tests follow their code.

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
import { DRIVER_STATE, respond, serviceCalls, show, WIRING } from '../test/shellHarness';

/**
 * The machine's wiring (D48 §5, D50): which deployment serves each workspace here. Shell-only and
 * more strictly than the rest — the service has no route onto this at all, so every call must land on
 * the bridge and none on the API.
 */
describe("the workspace domain: the machine's wiring", () => {
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

  it('keeps the wiring form one press away, not open on every visit', async () => {
    invoke.mockImplementation(async (module: string) => (
      module === 'DAORIS.DRIVER' ? DRIVER_STATE : WIRING));
    show(<SettingsView notify={() => {}} section="workspace" />);

    await userEvent.click(await screen.findByRole('button', { name: 'Wire a workspace' }));
    expect(screen.getByLabelText('deployment')).toBeTruthy();
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
   * WSR6: bringing repositories up to date is asked for, never fetched on its own — looking reaches the network,
   * as the person. Opening the domain asks nothing; *Look for updates* asks the driver, and the press sends only
   * the rows the look listed.
   */
  it('looks for updates only when asked, then brings up to date only what it listed', async () => {
    const plan = {
      lines: [{ repository: 'engine', workspace: 'aurora', line: 'main', kind: 'fast-forward', commits: 1, moves: true }],
      rebases: [{ repository: 'engine', workspace: 'aurora', branch: 'daoris/s-step', landed: false, kind: 'replay', onto: 'main', commits: 1, replays: true }],
      deletes: [],
    };
    invoke.mockImplementation(async (module: string, type: string) => {
      if (module !== 'DAORIS.DRIVER') return WIRING;
      if (type === 'TREES_SYNC_PLAN') return plan;
      if (type === 'TREES_SYNC') return { lines: [], rebases: [], deletes: [], changed: 2 };
      if (type === 'SWEEP_PLAN') return { branches: [], landed: [] };
      return DRIVER_STATE;
    });
    const notify = vi.fn();
    show(<SettingsView notify={notify} section="workspace" />);
    await screen.findByText('aurora');

    const button = await screen.findByRole('button', { name: 'Look for updates' });
    expect(invoke).not.toHaveBeenCalledWith('DAORIS.DRIVER', 'TREES_SYNC_PLAN', expect.anything());
    await userEvent.click(button);
    await userEvent.click(await screen.findByRole('button', { name: 'Bring up to date: 2 changes' }));

    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'TREES_SYNC_PLAN', {});
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'TREES_SYNC', { payload: { only: ['engine:main', 'engine:daoris/s-step'] } });
    expect(notify).toHaveBeenCalledWith('2 of 2 done. What did not happen is still listed, with why.');
    expect(serviceCalls()).toEqual([]);
  });

  /**
   * WSR7 (D112): a look takes the repositories holding Daoris's branches, and the rest are listed apart. Ticking one
   * and asking looks at it too, and looking again keeps it.
   */
  it('lists apart the repositories holding nothing of Daoris\'s, and looks at one once it is ticked', async () => {
    const plan = { lines: [], rebases: [], deletes: [], looked: [], apart: [{ repository: 'game', workspace: 'aurora', holds: false }] };
    invoke.mockImplementation(async (module: string, type: string) => {
      if (module !== 'DAORIS.DRIVER') return WIRING;
      if (type === 'TREES_SYNC_SCOPE') {
        return { repositories: [{ repository: 'engine', workspace: 'aurora', holds: true }, { repository: 'game', workspace: 'aurora', holds: false }] };
      }
      if (type === 'TREES_SYNC_PLAN') return plan;
      if (type === 'SWEEP_PLAN') return { branches: [], landed: [] };
      return DRIVER_STATE;
    });
    show(<SettingsView notify={() => {}} section="workspace" />);

    expect(await screen.findByText(/A look fetches the 1 repository that holds a branch of Daoris's/)).toBeTruthy();
    await userEvent.click(screen.getByText("1 other repository with a checkout here holds no branch of Daoris's"));
    await userEvent.click(screen.getByRole('checkbox', { name: 'game' }));
    await userEvent.click(screen.getByRole('button', { name: 'Include and look (1)' }));

    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'TREES_SYNC_PLAN', expect.objectContaining({ payload: { also: ['game'] } }));
    invoke.mockClear();
    const section = within(screen.getByRole('region', { name: 'Bring up to date' }));
    await userEvent.click(await section.findByRole('button', { name: 'Look again' }));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'TREES_SYNC_PLAN', expect.objectContaining({ payload: { also: ['game'] } }));
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
