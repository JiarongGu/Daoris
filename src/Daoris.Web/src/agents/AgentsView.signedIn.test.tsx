import { useState } from 'react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { act, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

// A new account's sign-in said once, over the mocked bridge.
const { invoke, notifyReady, eventHandlers } = vi.hoisted(() => ({
  invoke: vi.fn(),
  notifyReady: vi.fn(() => Promise.resolve()),
  eventHandlers: new Map<string, (payload: unknown) => void>(),
}));

vi.mock('@shenora/react', async () => ({
  ...(await import('../test/sliceShell')).mockedShell((await import('./agentsFixtures')).sheeted(invoke), { notifyReady }),
  useShenoraEvent: (module: string, type: string, handler: (payload: unknown) => void) => {
    eventHandlers.set(`${module}.${type}`, handler);
  },
}));

import { respond, show, WIRING } from '../test/shellHarness';
import { useListPanes } from '../work/frame/listPanes';
import { ViewFrame } from '../work/frame/ViewFrame';
import { useAgentsView } from './AgentsView';

function Place({ notify }: { notify: (message: string, tone?: 'ok' | 'error') => void }) {
  const lists = useListPanes();
  const [chosen, setChosen] = useState<string | null>('claude-code');
  const [over, setOver] = useState(false);
  const layout = useAgentsView({ active: true, chosen, onChoose: setChosen, notify, part: null });
  return <ViewFrame layout={layout} lists={lists} over={over} onOver={setOver} />;
}

describe('a new account signed in', () => {
  const ROSTER = {
    settingsPath: 'C:/somewhere/.daoris/harnesses.json',
    adapter: 'claude-code',
    harnesses: [{
      door: 'claude-code', product: 'Claude Code', maker: 'Anthropic', present: true, version: 'claude 9.9.9', problem: null,
      pinned: null, managed: null, pinnable: true, ownLogin: 'in', ownRead: null,
      profiles: [{ name: 'work', home: 'C:/somewhere/.daoris/harnesses/claude-code/work', login: 'out' }],
    }],
  };

  beforeEach(() => {
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => respond(String(input))));
    invoke.mockImplementation(async (_module: string, type: string, options?: { payload?: Record<string, unknown> }) => {
      if (type === 'HARNESS_ACTION') return { door: 'claude-code', action: options?.payload?.action, started: true };
      return type === 'HARNESSES' ? ROSTER : WIRING;
    });
  });
  afterEach(() => {
    vi.unstubAllGlobals();
    invoke.mockReset();
  });

  async function signedIn(notify: (message: string, tone?: 'ok' | 'error') => void) {
    show(<Place notify={notify} />);
    await userEvent.click(await screen.findByRole('button', { name: 'Add an account…' }));
    await userEvent.click(screen.getByRole('button', { name: 'Add a new account' }));
    await act(async () => {
      eventHandlers.get('DAORIS.HARNESS_ENDED')!({
        door: 'claude-code', action: 'login-new', profile: 'acct-1a2b3c4d', exitCode: 0, problem: null,
        account: 'alice@example.org', kept: true,
      });
    });
    return screen.findByRole('region', { name: 'Add an account' });
  }

  it('says who signed in once, in the card, and toasts nothing', async () => {
    const notify = vi.fn();
    const asking = await signedIn(notify);

    expect(asking.textContent).toContain("Signed in as alice@example.org. It joined Daoris's accounts: every workspace's work may run on it.");
    expect(notify).not.toHaveBeenCalled();
  });

  it('Done on the name as offered renames nothing and toasts nothing', async () => {
    const notify = vi.fn();
    const asking = await signedIn(notify);

    await userEvent.click(within(asking).getByRole('button', { name: 'Done' }));

    await waitFor(() => expect(screen.queryByRole('region', { name: 'Add an account' })).toBeNull());
    expect(invoke).not.toHaveBeenCalledWith('DAORIS.DRIVER', 'HARNESS_ACTION', expect.objectContaining({
      payload: expect.objectContaining({ action: 'profile-rename' }),
    }));
    expect(notify).not.toHaveBeenCalled();
  });
});
