import { afterEach, describe, expect, it, vi } from 'vitest';
import { cleanup, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import * as Tooltip from '@radix-ui/react-tooltip';

// REVIEWENV1g (D154 point 8; the review environment design §3.3): *What needs you* lists a set-up waiting for the person's
// look, oldest first with the rest, with *Reviewed*, *Not yet…* and *Show it again* on its row, over a mocked shell and a
// stubbed service.

const { invoke } = vi.hoisted(() => ({ invoke: vi.fn() }));

vi.mock('@shenora/react', async () => {
  const actual = await vi.importActual<typeof import('@shenora/react')>('@shenora/react');
  return {
    ...actual,
    isShenoraAvailable: () => true,
    getBridge: () => ({ isAvailable: true, invoke, notifyReady: () => Promise.resolve() }),
    useShenora: () => ({ isAvailable: true, bridge: {} }),
    useShenoraEvent: () => {},
    useWindowMaximized: () => false,
  };
});

import i18n from '../i18n';
import { WorkspaceScopeProvider } from '../scope';
import { AttentionBand } from './AttentionBand';
import { STEP_SHOWN } from './reviewFixtures';

const posts = () => vi.mocked(fetch).mock.calls
  .filter(([, init]) => init?.method === 'POST')
  .map(([path, init]) => ({ path: String(path), body: JSON.parse(String(init!.body)) }));

function start(notify = vi.fn(), post?: () => Response) {
  vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    const url = String(input);
    if (init?.method === 'POST' && post) return post();
    if (init?.method === 'POST') return Response.json({ quest: STEP_SHOWN, message: 'Reviewed set-up step `#q2` in `local`.' });
    if (url.startsWith('/api/quests')) return Response.json([STEP_SHOWN]);
    return Response.json([]);
  }));
  invoke.mockImplementation(async () => ({}));
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  render(
    <QueryClientProvider client={client}>
      <Tooltip.Provider>
        <WorkspaceScopeProvider initial={null}>
          <AttentionBand notify={notify} doors={{ 'set-up': vi.fn() }} />
        </WorkspaceScopeProvider>
      </Tooltip.Provider>
    </QueryClientProvider>,
  );
  return notify;
}

describe('a set-up waiting in What needs you', () => {
  afterEach(async () => {
    cleanup();
    vi.unstubAllGlobals();
    invoke.mockReset();
    await i18n.changeLanguage('en');
  });

  it('says what it showed, and a held step is no departure', async () => {
    start();

    const row = await screen.findByRole('listitem', { name: STEP_SHOWN.title });
    expect(row).toHaveTextContent('shown for review');
    expect(row).toHaveTextContent('Shown in local: The report with the compare setting turned on');
    expect(screen.queryByText('awaits your yes')).toBeNull();
  });

  it('sends Reviewed on the set-up the row drew', async () => {
    const notify = start();
    const user = userEvent.setup();

    const row = await screen.findByRole('listitem', { name: STEP_SHOWN.title });
    await user.click(within(row).getByRole('button', { name: 'Reviewed' }));

    await waitFor(() => expect(notify).toHaveBeenCalledWith('Reviewed set-up step `#q2` in `local`.'));
    expect(posts()).toEqual([{ path: '/api/quests/q2/review', body: { verdict: 'reviewed', setUp: { machine: 'desk', sequence: 7 } } }]);
  });

  it("asks for a not yet's words, then sends them to the step and to its session", async () => {
    start();
    const user = userEvent.setup();

    const row = await screen.findByRole('listitem', { name: STEP_SHOWN.title });
    await user.click(within(row).getByRole('button', { name: 'Not yet…' }));
    const ask = within(row).getByRole('group', { name: 'Not yet…' });
    await user.type(within(ask).getByRole('textbox'), 'The total is off by one.');
    await user.click(within(ask).getByRole('button', { name: 'Send not yet' }));

    await waitFor(() => expect(invoke).toHaveBeenCalledWith(expect.any(String), 'SESSION_INPUT', { payload: { id: 's9', text: 'The total is off by one.' } }));
    expect(posts()[0]).toEqual({
      path: '/api/quests/q2/review',
      body: { verdict: 'not-yet', words: 'The total is off by one.', setUp: { machine: 'desk', sequence: 7 } },
    });
  });

  /** UXFIX2b3a: a refused not yet is said inside its ask, where it was pressed, and never toasted; the ask stays for a retry. */
  it('says a refused not yet inside its ask, and toasts nothing', async () => {
    const notify = start(vi.fn(), () => Response.json({ error: 'The set-up moved on.' }, { status: 409 }));
    const user = userEvent.setup();

    const row = await screen.findByRole('listitem', { name: STEP_SHOWN.title });
    await user.click(within(row).getByRole('button', { name: 'Not yet…' }));
    const ask = within(row).getByRole('group', { name: 'Not yet…' });
    await user.type(within(ask).getByRole('textbox'), 'The total is off by one.');
    await user.click(within(ask).getByRole('button', { name: 'Send not yet' }));

    expect(await within(ask).findByRole('alert')).toBeInTheDocument();
    expect(notify).not.toHaveBeenCalled();
    expect(within(ask).getByRole('button', { name: 'Send not yet' })).toBeEnabled();
  });

  it('shows it again through the shell', async () => {
    start();
    const user = userEvent.setup();

    const row = await screen.findByRole('listitem', { name: STEP_SHOWN.title });
    await user.click(within(row).getByRole('button', { name: 'Show it again' }));

    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SHOW_REVIEW_AGAIN', { payload: { quest: 'q2' } }));
  });

  it('says it in 中文', async () => {
    await i18n.changeLanguage('zh');
    start();

    const row = await screen.findByRole('listitem', { name: STEP_SHOWN.title });
    expect(row).toHaveTextContent('展示待审阅');
    expect(within(row).getByRole('button', { name: '已审阅' })).toBeInTheDocument();
    expect(within(row).getByRole('button', { name: '还不行…' })).toBeInTheDocument();
  });
});
