import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

// Settings → Updates: the install's update (its rows are DriverDomain.test.tsx's *Updates* describe), then the versions
// this install carries, read from the shell's `SYSTEM_WIRING`.

const { invoke, notifyReady } = vi.hoisted(() => ({ invoke: vi.fn(), notifyReady: vi.fn(() => Promise.resolve()) }));

vi.mock('@shenora/react', () => ({
  isShenoraAvailable: () => true,
  getBridge: () => ({ isAvailable: true, invoke, notifyReady }),
  useShenora: () => ({ isAvailable: true, bridge: { notifyReady } }),
  useShenoraEvent: () => undefined,
}));

import { SettingsView } from './SettingsView';
import type { SystemWiring } from '../bridge/system';
import { DRIVER_STATE, respond, show, WIRING } from '../test/shellHarness';
import { DEVELOPMENT, WIRED } from './wiring/wiringFixtures';

const NO_UPDATE = { state: 'none', staged: null, mode: null, driven: 0, turns: 0, problem: null, outcome: null, last: null };

const shell = (wiring: SystemWiring) => async (module: string, type: string) => {
  if (module === 'DAORIS.UPDATE') return NO_UPDATE;
  if (module !== 'DAORIS.DRIVER') return WIRING;
  return type === 'SYSTEM_WIRING' ? wiring : DRIVER_STATE;
};

const open = (onGo = vi.fn()) => {
  show(<SettingsView notify={() => {}} section="updates" onGo={onGo} />);
  return onGo;
};

describe('Settings → Updates: the versions', () => {
  beforeEach(() => {
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => respond(String(input))));
  });
  afterEach(() => {
    vi.unstubAllGlobals();
    invoke.mockReset();
  });

  it('shows the versions under the update, the one not read in its own words with its door', async () => {
    invoke.mockImplementation(shell(WIRED));
    const onGo = open();
    const versions = within(await screen.findByRole('region', { name: 'Versions' }));

    expect(screen.getByRole('region', { name: 'Update' })).toBeTruthy();
    expect(versions.getByText('0.21.0 (abc1234)')).toBeTruthy();
    expect(versions.getByText('Shenora 0.21.1')).toBeTruthy();
    expect(versions.getByText('10.0.1')).toBeTruthy();
    expect(versions.getByText('Settings → Tools says it')).toBeTruthy();
    // The update is the card above, so the versions say no line of it and carry no door to it.
    expect(versions.queryByText('Nothing is staged beside this install.')).toBeNull();
    await userEvent.click(versions.getByRole('button', { name: 'Open Tools' }));
    expect(onGo).toHaveBeenLastCalledWith({ view: 'settings', section: 'tools' });
    expect(versions.getAllByRole('button')).toHaveLength(1);
  });

  it('says what a development run does not carry, and that it is not updated', async () => {
    invoke.mockImplementation(shell(DEVELOPMENT));
    open();
    const versions = within(await screen.findByRole('region', { name: 'Versions' }));

    expect(versions.getAllByText('A development run has none')).toHaveLength(2);
    expect(versions.getByText('A development run is not updated.')).toBeTruthy();
    expect(versions.getByText('Only the window knows')).toBeTruthy();
  });
});
