import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { act, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import * as Tooltip from '@radix-ui/react-tooltip';

// Signing in, on the row (2026-09-23): what the tool says arrives as console lines, and the two
// things the person can do with it — paste the code, or stop — land on the driver module.

const { invoke, notifyReady, eventHandlers } = vi.hoisted(() => ({
  invoke: vi.fn(),
  notifyReady: vi.fn(() => Promise.resolve()),
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

import './i18n';
import { SignIn } from './SignIn';

const ID = 'claude-code:login';

function show() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={client}>
      <Tooltip.Provider>
        <SignIn id={ID} harness="claude-code" profile="work" />
      </Tooltip.Provider>
    </QueryClientProvider>,
  );
}

/** The tool speaks: lines arrive as the host's push, keyed by the action's own id, in sequence. */
let sequence = 1;
async function said(...texts: string[]) {
  await act(async () => {
    eventHandlers.get('DAORIS.SESSION_OUTPUT')!({
      session: ID,
      lines: texts.map((text) => ({ sequence: ++sequence, text })),
    });
  });
}

describe('signing in on the row', () => {
  beforeEach(() => {
    // The action has no backlog (nothing buffers a harness action): the tail is the command line
    // the host echoes, and everything after it arrives as a push.
    invoke.mockImplementation(async (_module: string, type: string) => (type === 'TAIL_SESSION'
      ? { session: ID, lines: [{ sequence: 1, text: '$ claude auth login' }], sequence: 1, live: true, dropped: 0 }
      : {}));
  });
  afterEach(() => {
    invoke.mockReset();
    sequence = 1;
  });

  /**
   * 🔴 Measured on the real binary with no console: a hyperlink, then `Paste code here if
   * prompted >` with no newline, then a wait on stdin. The steps follow what has actually been said.
   */
  it('offers the link when it appears, the code box when asked, and sends what is pasted', async () => {
    show();
    expect(await screen.findByText(/Waiting for the tool/)).toBeTruthy();
    // The tail is in before the tool speaks, so what it says joins one stream.
    await userEvent.click(screen.getByText('The tool\'s own output'));
    await screen.findByText(/claude auth login/);
    // Nothing to paste into until the tool asks — a box before that would be a guess.
    expect(screen.queryByLabelText('sign-in code')).toBeNull();

    // The other flow the binary takes (with a stdin): it opens the browser and waits for the
    // callback, printing no link and asking for no code. The step says so rather than waiting.
    await said('Opening browser to sign in…');
    expect(await screen.findByText(/opened your browser/)).toBeTruthy();

    await said("If the browser didn't open, visit: https://x.test/oauth?code=true");
    const link = await screen.findByRole('link', { name: /x\.test/ });
    expect(link.getAttribute('href')).toBe('https://x.test/oauth?code=true');
    expect(screen.queryByLabelText('sign-in code')).toBeNull();

    await said('Paste code here if prompted >');
    await userEvent.type(await screen.findByLabelText('sign-in code'), 'abc-123');
    await userEvent.click(screen.getByRole('button', { name: 'Continue' }));

    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'HARNESS_INPUT', {
      payload: { harness: 'claude-code', action: 'login', text: 'abc-123' },
    });
    // Sent once: the box goes, so a second paste cannot land on a tool that is finishing.
    expect(await screen.findByText(/Code sent/)).toBeTruthy();
    expect(screen.queryByLabelText('sign-in code')).toBeNull();
  });

  /** A login nobody is going to finish is stopped from here — not by closing the window. */
  it('can be stopped', async () => {
    show();
    await userEvent.click(await screen.findByRole('button', { name: /Cancel/ }));

    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'HARNESS_CANCEL', {
      payload: { harness: 'claude-code', action: 'login' },
    });
  });

  /**
   * Signing in to ANOTHER account (D66 §3): the same three steps, with no account to name yet — and
   * the stop reaches the sign-in that is actually running, under its own action.
   */
  it('signs in to another account under its own action, naming the tool rather than an account', async () => {
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    render(
      <QueryClientProvider client={client}>
        <Tooltip.Provider>
          <SignIn id="claude-code:login-new" harness="claude-code" action="login-new" />
        </Tooltip.Provider>
      </QueryClientProvider>,
    );

    expect(await screen.findByText('Signing in to another claude-code account')).toBeTruthy();
    expect(screen.getByText(/under who signed in/)).toBeTruthy();
    await userEvent.click(screen.getByRole('button', { name: /Cancel/ }));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'HARNESS_CANCEL', {
      payload: { harness: 'claude-code', action: 'login-new' },
    });
  });

  /** The tool's own words are one disclosure away, never the surface. */
  it('keeps the raw output behind a disclosure', async () => {
    show();
    await said('Opening browser to sign in…');
    expect(await screen.findByText('The tool\'s own output')).toBeTruthy();
    expect(screen.getByText('The tool\'s own output').closest('details')?.open).toBe(false);
  });
});
