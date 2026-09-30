import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

// Settings' frame in SHELL mode, where every domain is offered. `SettingsView.test.tsx` holds the browser;
// each domain's own tests are beside it in `settings/`. Moved from `shell.test.tsx` with MOD4.

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
import { DRIVER_STATE, respond, show, WIRING } from './test/shellHarness';

describe('the domain list, in a shell', () => {
  beforeEach(() => {
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => respond(String(input))));
    invoke.mockImplementation(async () => WIRING);
  });
  afterEach(() => {
    vi.unstubAllGlobals();
    invoke.mockReset();
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
    expect(within(domains).getAllByRole('button')[0]?.textContent).toBe('Setup');
    // Daoris's browser (CHR5, CHR7) and the machine log (LOG1c) are a machine's domains, last in the list.
    expect(within(domains).getAllByRole('button').slice(-2).map((button) => button.textContent)).toEqual(['Browser', 'Machine log']);
    expect(await screen.findByLabelText('Failures before a quest parks')).toBeTruthy();
    // A card alone in its domain does not say the domain's name again: the list already has.
    expect(screen.getAllByText('Driver')).toHaveLength(1);
    // Another domain's cards are not on the page at all.
    expect(screen.queryByRole('button', { name: 'Wire a workspace' })).toBeNull();
    expect(screen.queryByText('Theme')).toBeNull();

    await userEvent.click(within(domains).getByRole('button', { name: 'Plugins' }));
    expect(onSection).toHaveBeenCalledWith('plugins');
  });
});
