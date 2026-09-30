import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { screen } from '@testing-library/react';
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
   * With the environment pair set no loader reads the file, so the surface must say which source
   * decided — otherwise it reports its own last edit as though it were the machine's wiring.
   */
  it('says when the environment, not the file, is the answer', async () => {
    invoke.mockImplementation(async () => ({ ...WIRING, fromEnvironment: true }));
    show(<SettingsView notify={() => {}} section="workspace" />);

    expect(await screen.findByText(/environment names this machine's remote/i)).toBeTruthy();
  });
});
