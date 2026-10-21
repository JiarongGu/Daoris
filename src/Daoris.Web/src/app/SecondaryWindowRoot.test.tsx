import { afterEach, describe, expect, it, vi } from 'vitest';
import { act, render, screen } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import * as Tooltip from '@radix-ui/react-tooltip';

// NOTIFY2b (the notification design §3.3): a secondary window (a detached session, the monitor) holds no *Notifications*, so a
// terminal's press heard while it is the window in front is said there by a toast naming where it waits, with no door; and nothing
// while it is not in front.

const { invoke, notifyReady, handlers } = vi.hoisted(() => ({
  invoke: vi.fn(async () => ({})),
  notifyReady: vi.fn(() => Promise.resolve()),
  handlers: new Map<string, (payload: unknown) => void>(),
}));

vi.mock('@shenora/react', async () => {
  const actual = await vi.importActual<typeof import('@shenora/react')>('@shenora/react');
  return {
    ...actual,
    isShenoraAvailable: () => true,
    getBridge: () => ({ isAvailable: true, invoke, notifyReady }),
    useShenora: () => ({ isAvailable: true, bridge: { notifyReady } }),
    useShenoraEvent: (module: string, type: string, handler: (payload: unknown) => void) => {
      handlers.set(`${module}.${type}`, handler);
    },
  };
});
vi.mock('../work/console/MonitorWindow', () => ({ MonitorWindow: () => <p>the monitor</p> }));

import i18n from '../locales/i18n';
import { SecondaryWindowRoot } from './SecondaryWindowRoot';

const show = () => render(
  <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
    <Tooltip.Provider>
      <SecondaryWindowRoot window={{ kind: 'monitor' }} />
    </Tooltip.Provider>
  </QueryClientProvider>,
);
const confirming = () => act(() => {
  handlers.get('DAORIS.SESSION_ATTENTION')!({ kind: 'Confirming', id: 'c1', session: null, headline: 'waits' });
});

describe("a secondary window's word of a terminal's press", () => {
  afterEach(async () => {
    vi.restoreAllMocks();
    handlers.clear();
    await i18n.changeLanguage('en');
  });

  it('says where it waits while this window is in front, with no door', async () => {
    vi.spyOn(document, 'hasFocus').mockReturnValue(true);
    show();
    confirming();

    expect(await screen.findByText("A terminal's press waits in Daoris's main window")).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Open' })).toBeNull();
  });

  it('says nothing while this window is not in front', () => {
    vi.spyOn(document, 'hasFocus').mockReturnValue(false);
    show();
    confirming();

    expect(screen.queryByText("A terminal's press waits in Daoris's main window")).toBeNull();
  });
});
