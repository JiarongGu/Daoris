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
import { DOCK, frameLayout, LIST_BOUNDS, type ListChoice } from './work/layout';
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
    // The programs Daoris runs beside its agents follow the driver, and Agents is a place of its own (UX6e, D150 §5).
    const names = within(domains).getAllByRole('button').map((button) => button.textContent);
    expect(names.slice(names.indexOf('Driver'), names.indexOf('Driver') + 2)).toEqual(['Driver', 'Tools']);
    expect(names).not.toContain('Agents');
    // The setup guide leads (SETUP1a, D97).
    expect(within(domains).getAllByRole('button')[0]?.textContent).toBe('Setup');
    // Daoris's browser (CHR5, CHR7) and the machine log (LOG1c) are a machine's domains, last in the list.
    expect(within(domains).getAllByRole('button').slice(-2).map((button) => button.textContent)).toEqual(['Browser', 'Machine log']);
    expect(await screen.findByLabelText('Failures before a quest parks')).toBeTruthy();
    // The main area's header names the domain (FRAME1g), and a card alone in it does not say it again.
    const main = screen.getByRole('main');
    expect(within(main).getByRole('heading', { level: 1, name: 'Driver' })).toBeTruthy();
    expect(within(main).getAllByText('Driver')).toHaveLength(1);
    // Another domain's cards are not on the page at all.
    expect(screen.queryByRole('button', { name: 'Wire a workspace' })).toBeNull();
    expect(screen.queryByText('Theme')).toBeNull();

    await userEvent.click(within(domains).getByRole('button', { name: 'Plugins' }));
    expect(onSection).toHaveBeenCalledWith('plugins');
  });

  /** A shell says nothing of what it is not offered: every domain is its own. */
  it('carries no browser\'s note beneath its domains', async () => {
    invoke.mockImplementation(async (module: string) => (
      module === 'DAORIS.DRIVER' ? DRIVER_STATE : WIRING));
    show(<SettingsView notify={() => {}} section="driver" />);

    const domains = await screen.findByRole('navigation', { name: 'Settings domains' });
    await waitFor(() => expect(within(domains).getAllByRole('button')).toHaveLength(10));
    expect(screen.queryByText(/on the desktop, where the machine is/)).toBeNull();
  });
});

/**
 * FRAME1g, by D118 §3a's room rule and §8's reason for it: Settings' list is a strip only where the domain
 * would fall below its 400 px floor beside it, with the side bar as it stands. A fixed 1024 px threshold,
 * Sessions' under FRAME6, would have stripped its 176 px list where it fits.
 */
describe("Settings' list in a shell's frame", () => {
  const settings = (closed = false): ListChoice => ({ bounds: LIST_BOUNDS.settings, width: null, closed, over: false });
  const at = (viewport: number, dockClosed: boolean) =>
    frameLayout(viewport, viewport - 48, { list: settings(), dockShare: null, dockClosed, dockFull: false }).list!;

  it('stays open beside a 1024 px window\'s side bar, where Sessions\' 280 px rail is a strip', () => {
    expect(at(1024, false)).toMatchObject({ mode: 'open', width: 176 });
    const sessions = frameLayout(1024, 976, {
      list: { bounds: LIST_BOUNDS.sessions, width: null, closed: false, over: false }, dockShare: null, dockClosed: false, dockFull: false,
    }).list!;
    expect(sessions.mode).toBe('strip');
  });

  it('at 680 px stays beside the domain with the side bar closed, and gives way to a strip with it open', () => {
    expect(at(680, true)).toMatchObject({ mode: 'open', width: 176 });
    expect(at(680, false)).toMatchObject({ mode: 'strip', width: 56, auto: true });
    // The side bar counts at its floor whenever it is open, full below 768 px included (FRAME1b).
    expect(680 - 48 - 176 - DOCK.floor).toBeLessThan(400);
  });
});
