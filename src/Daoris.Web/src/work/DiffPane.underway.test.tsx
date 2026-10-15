import { afterEach, describe, expect, it, vi } from 'vitest';
import { act, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import * as Tooltip from '@radix-ui/react-tooltip';

// STATEHUB1c (D169 point 7): a press under way is the window's and the driver's, never the one component's that pressed. The
// review's *Accept* reads *Landing…* while its session's landing runs, after a tab switch remounted it and where another window
// pressed it; so too its *Discard* and its hand-off.

const { invoke, eventHandlers } = vi.hoisted(() => ({
  invoke: vi.fn(),
  // The push channel's seam: handlers land here by "module.type", and a test fires them as the host.
  eventHandlers: new Map<string, (payload: unknown) => void>(),
}));

vi.mock('@shenora/react', () => ({
  isShenoraAvailable: () => true,
  getBridge: () => ({ isAvailable: true, invoke }),
  useShenora: () => ({ isAvailable: true, bridge: {} }),
  useShenoraEvent: (module: string, type: string, handler: (payload: unknown) => void) => {
    eventHandlers.set(`${module}.${type}`, handler);
  },
}));

import '../locales/i18n';
import { useStateHub } from '../bridge/stateHub';
import { DiffPane } from './DiffPane';

const FILES = [{ path: 'notes.txt', status: 'added', added: 1, removed: 0, patch: '@@ -0,0 +1 @@\n+notes' }];
const DIFF = { session: 's1a2b3c4', base: '0123456789abcdef', truncated: null, files: FILES, source: 'tree' };
const PLAN = { session: 's1a2b3c4', form: 'branch', target: 'feature/0fda18-fix-the-gap', source: 'workspace' };
const LANDED = { session: 's1a2b3c4', done: true, message: 'put its work on `feature/0fda18-fix-the-gap` — 1 commit(s).' };

/** The routes the review reads; a landing answers only when the test lets it. */
function answer() {
  let land: (value: unknown) => void = () => {};
  invoke.mockImplementation((_module: string, type: string) => {
    if (type === 'SESSION_DIFF') return Promise.resolve(DIFF);
    if (type === 'HANDOFF_PLAN') return Promise.resolve({ session: 's1a2b3c4', branch: null });
    if (type === 'LANDING') return Promise.resolve(PLAN);
    if (type === 'LAND_SESSION_TREE') return new Promise((resolve) => { land = resolve; });
    return Promise.resolve({});
  });
  return { land: (value: unknown) => land(value) };
}

function Hub() {
  useStateHub();
  return null;
}

/** The window's root with the hub, and the review shown or not, as a tab switch shows it. */
function root() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  const Window = ({ shown }: { shown: boolean }) => (
    <QueryClientProvider client={client}>
      <Tooltip.Provider>
        <Hub />
        {shown && <DiffPane session="s1a2b3c4" hasTree onSendBack={vi.fn()} />}
      </Tooltip.Provider>
    </QueryClientProvider>
  );
  const view = render(<Window shown />);
  return { show: (shown: boolean) => view.rerender(<Window shown={shown} />) };
}

const told = (underWay: unknown) => act(() => eventHandlers.get('DAORIS.UNDER_WAY')!(underWay));

describe('a press under way, in the review', () => {
  afterEach(() => {
    invoke.mockReset();
    eventHandlers.clear();
  });

  it('still says Landing… once a tab switch remounted it mid-press, and Accept again once the landing answered', async () => {
    const pressed = answer();
    const { show } = root();
    await userEvent.click(await screen.findByRole('button', { name: 'Accept' }));
    expect(await screen.findByRole('button', { name: 'Landing…' })).toBeDisabled();

    show(false);
    expect(screen.queryByRole('button', { name: 'Landing…' })).toBeNull();
    show(true);

    expect(await screen.findByRole('button', { name: 'Landing…' })).toBeDisabled();
    expect(screen.queryByRole('button', { name: 'Accept' })).toBeNull();

    await act(async () => pressed.land(LANDED));
    expect(await screen.findByRole('button', { name: 'Accept' })).toBeEnabled();
  });

  it('says Landing… while another window’s landing of its session is under way, and nothing for another session’s', async () => {
    answer();
    root();
    await screen.findByRole('button', { name: 'Accept' });

    told({ seq: 1, presses: [{ route: 'LAND_SESSION_TREE', session: 'feedface' }] });
    await act(() => new Promise((resolve) => { setTimeout(resolve, 0); }));
    expect(screen.getByRole('button', { name: 'Accept' })).toBeEnabled();

    told({ seq: 2, presses: [{ route: 'LAND_SESSION_TREE', session: 'feedface' }, { route: 'LAND_SESSION_TREE', session: 's1a2b3c4' }] });
    expect(await screen.findByRole('button', { name: 'Landing…' })).toBeDisabled();

    told({ seq: 3, presses: [] });
    expect(await screen.findByRole('button', { name: 'Accept' })).toBeEnabled();
    // Nothing was pressed here: the press was the other window's.
    expect(invoke.mock.calls.filter(([, type]) => type === 'LAND_SESSION_TREE')).toHaveLength(0);
  });

  it('holds its Discard while another window discards its tree', async () => {
    answer();
    root();
    const discard = await screen.findByRole('button', { name: 'Discard tree' });
    expect(discard).toBeEnabled();

    told({ seq: 1, presses: [{ route: 'DISCARD_SESSION_TREE', session: 's1a2b3c4' }] });

    await vi.waitFor(() => expect(screen.getByRole('button', { name: 'Discard tree' })).toBeDisabled());
  });
});
