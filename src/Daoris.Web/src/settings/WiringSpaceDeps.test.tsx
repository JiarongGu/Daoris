import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { screen, within } from '@testing-library/react';

// Settings → This Daoris's Disk space card: the dependency store's row, `SYSTEM_SPACE`'s `deps` kind.

const { invoke, notifyReady } = vi.hoisted(() => ({ invoke: vi.fn(), notifyReady: vi.fn(() => Promise.resolve()) }));

vi.mock('@shenora/react', () => ({
  isShenoraAvailable: () => true,
  getBridge: () => ({ isAvailable: true, invoke, notifyReady }),
  useShenora: () => ({ isAvailable: true, bridge: { notifyReady } }),
  useShenoraEvent: () => undefined,
}));

import { SettingsView } from './SettingsView';
import i18n from '../locales/i18n';
import { dayOf, size } from '../locales/format';
import { DRIVER_STATE, respond, show, WIRING } from '../test/shellHarness';
import { SPACE, WIRED } from './wiring/wiringFixtures';

const NO_UPDATE = { state: 'none', staged: null, mode: null, driven: 0, turns: 0, problem: null, outcome: null, last: null };

const shell = async (module: string, type: string) => {
  if (module === 'DAORIS.UPDATE') return NO_UPDATE;
  if (module !== 'DAORIS.DRIVER') return WIRING;
  if (type === 'SYSTEM_WIRING') return WIRED;
  if (type === 'SYSTEM_SPACE') return SPACE;
  return DRIVER_STATE;
};

describe('Settings → This Daoris → Disk space: the dependency store', () => {
  beforeEach(() => {
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => respond(String(input))));
  });
  afterEach(async () => {
    vi.unstubAllGlobals();
    invoke.mockReset();
    await i18n.changeLanguage('en');
  });

  it('shows the store with its size, its days and size limit, and its next, a rule the person does not set', async () => {
    invoke.mockImplementation(shell);
    show(<SettingsView notify={() => {}} section="wiring" onGo={vi.fn()} />);

    const card = within(await screen.findByRole('region', { name: 'Disk space' }));

    expect(card.getByText('Dependency store')).toBeTruthy();
    expect(card.getByText('an entry goes 30 days after it was last placed, or sooner past 10 GB')).toBeTruthy();
    expect(card.getByText(`next: 2 entries, ${size(0.4 * 1024 ** 3)} on ${dayOf('2026-11-20T06:00:00.000+00:00')} · last: nothing removed yet`)).toBeTruthy();
    // Only the five rules the person sets carry a Change…, the store's not among them.
    expect(card.getAllByRole('button', { name: 'Change…' })).toHaveLength(5);
  });

  it('says the store in 中文', async () => {
    await i18n.changeLanguage('zh');
    invoke.mockImplementation(shell);
    show(<SettingsView notify={() => {}} section="wiring" onGo={vi.fn()} />);

    expect(await screen.findByText('依赖存储')).toBeTruthy();
    expect(screen.getByText('条目在最后一次放置 30 天后移除，超过 10 GB 时更早')).toBeTruthy();
  });
});
