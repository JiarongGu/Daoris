import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { cleanup, render, screen, waitFor, within } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import * as Tooltip from '@radix-ui/react-tooltip';

// A parked intake in the Work frame, one part of its suite: its card's door to the ask, and the box under it,
// whose words go to the ask's channel and never to the session. The bridge is mocked as present; what the parts share is in
// `test/workFrameBridge.ts` and `test/workFrameKit.tsx`.
vi.mock('@shenora/react', async () => (await import('../test/workFrameBridge')).shenoraMock());

import { eventHandlers, invoke } from '../test/workFrameBridge';
import { DRIVER_STATE, PARKED_INTAKE, QUESTS, REGISTRY, show, userEvent } from '../test/workFrameKit';
import { WorkFrame } from './WorkFrame';

let SESSIONS: unknown[] = [PARKED_INTAKE];

function respond(url: string): Response {
  if (url.startsWith('/api/sessions')) return Response.json(SESSIONS);
  if (url.startsWith('/api/quests')) return Response.json(QUESTS);
  if (url.startsWith('/api/registry')) return Response.json(REGISTRY);
  throw new Error(`unstubbed request: ${url}`);
}

describe('a parked intake', () => {
  beforeEach(() => {
    SESSIONS = [PARKED_INTAKE];
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => respond(String(input))));
  });
  afterEach(() => {
    cleanup();
    vi.unstubAllGlobals();
    invoke.mockReset();
    eventHandlers.clear();
    window.localStorage.clear();
  });

  /**
   * A parked intake's publish and close are on its ask, which the application opens in Quests — so the
   * frame hands the door up. Its box takes the person's words and posts them on the ask's channel: a parked
   * intake has no process left to hear a message, so it is never sent one.
   */
  it('leads a parked intake to its ask, with a box that posts on the ask and none of the three moves', async () => {
    SESSIONS = [PARKED_INTAKE];
    const onAnswerAsk = vi.fn();
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    render(
      <QueryClientProvider client={client}>
        <Tooltip.Provider>
          <WorkFrame selected="i9n8t7k6" onSelect={vi.fn()} notify={() => {}} onAnswerAsk={onAnswerAsk} />
        </Tooltip.Provider>
      </QueryClientProvider>,
    );

    // The head's card, in the main area; the strip at the foot offers the same door.
    await userEvent.click(await within(screen.getByRole('main')).findByRole('button', { name: 'Answer ask #0fda18' }));
    expect(onAnswerAsk).toHaveBeenCalledWith('0fda18');
    await userEvent.click(within(screen.getByRole('region', { name: 'Waiting on you' })).getByRole('button', { name: 'Answer ask #0fda18' }));
    expect(onAnswerAsk).toHaveBeenCalledTimes(2);
    expect(screen.queryByRole('button', { name: 'Finish' })).toBeNull();
    expect(screen.queryByRole('button', { name: 'Send' })).toBeNull();
    expect(screen.getByLabelText('Message')).toHaveAttribute('placeholder', 'your answer — it is posted on the ask’s channel');
  });

  /** The words go to the ask's channel as the person's, and to no session: nothing is said to the intake over the driver. */
  it('posts a parked intake’s answer on its ask’s channel, and says nothing to the session', async () => {
    SESSIONS = [PARKED_INTAKE];
    invoke.mockImplementation(async (_module: string, type: string) => (type === 'CHANNEL_POST' ? { posted: true } : DRIVER_STATE));
    const notify = vi.fn();
    show('i9n8t7k6', notify);

    await userEvent.type(await screen.findByLabelText('Message'), 'the engine owns it');
    await userEvent.click(screen.getByRole('button', { name: 'Post on the ask' }));

    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'CHANNEL_POST', { payload: { ask: '0fda18', text: 'the engine owns it' } }));
    expect(invoke.mock.calls.map(([, type]) => type)).not.toContain('SESSION_INPUT');
    await waitFor(() => expect(notify).toHaveBeenCalledWith(expect.stringContaining('Posted on ask #0fda18')));
    expect(screen.getByLabelText('Message')).toHaveValue('');
  });

  /** A refusal is said in the channel's own words, and the words are back in the box. */
  it('says a refused post in the channel’s words and keeps the words in the box', async () => {
    SESSIONS = [PARKED_INTAKE];
    invoke.mockImplementation(async (_module: string, type: string) => (type === 'CHANNEL_POST' ? { posted: false, code: 'too-long' } : DRIVER_STATE));
    const notify = vi.fn();
    show('i9n8t7k6', notify);

    await userEvent.type(await screen.findByLabelText('Message'), 'a very long answer');
    await userEvent.click(screen.getByRole('button', { name: 'Post on the ask' }));

    await waitFor(() => expect(notify).toHaveBeenCalledWith(expect.any(String), 'error'));
    expect(screen.getByLabelText('Message')).toHaveValue('a very long answer');
  });
});
