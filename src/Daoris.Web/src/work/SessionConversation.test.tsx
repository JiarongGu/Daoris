import { afterEach, describe, expect, it, vi } from 'vitest';
import { act, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { createRef } from 'react';
import '../i18n';

// SESS1 S9, the organism: a jump lands on an event, and where the page does not hold it, the pages
// before are loaded until it does. The bridge is mocked; the driver's answers are the test's.

const { invoke } = vi.hoisted(() => ({ invoke: vi.fn() }));

vi.mock('@shenora/react', () => ({
  isShenoraAvailable: () => true,
  getBridge: () => ({ isAvailable: true, invoke }),
  useShenora: () => ({ isAvailable: true }),
  useShenoraEvent: () => {},
}));

import { BackToBottom, SessionConversation, type TailState } from './SessionConversation';

const at = '2026-09-28T00:00:00Z';
const target = { seq: 1, at, kind: 'user', origin: 'target', text: 'Your target is the quest…' };
const said = (seq: number, id: string, text: string) => ({ seq, at, kind: 'message', id, text });
const call = (seq: number, id: string, over: Record<string, unknown>) => ({ seq, at, kind: 'tool', id, ...over });

// The newest page: well past the ask and past the failure, which the driver names by where it began.
const NEWEST = [said(300, 'm9', 'Clean. Now the full gates.'), call(301, 'g1', { title: 'npm run gates', status: 'completed' })];
const EARLIER = [
  target,
  said(2, 'm1', 'Starting.'),
  call(3, 'r1', { title: 'Read loader.rs', status: 'completed' }),
  call(4, 'x1', { title: 'npm test', status: 'in_progress' }),
  call(5, 'x1', { status: 'failed', output: '3 failing' }),
  said(6, 'm2', 'Fixing the loader.'),
];

afterEach(() => invoke.mockReset());

describe('SessionConversation', () => {
  it('jumps to the first failure through the pages before it, and shows the failed call', async () => {
    invoke.mockImplementation(async (_module: string, type: string, body?: { payload?: { before?: number } }) => {
      if (type === 'SESSION_HISTORY') {
        return body?.payload?.before
          ? { session: 's1', events: EARLIER, earlier: false, latest: 301 }
          : { session: 's1', events: NEWEST, earlier: true, latest: 301, opening: target, firstFailure: 4 };
      }
      if (type === 'HARNESSES') return { harnesses: [] };
      return {};
    });
    const scroller = createRef<HTMLDivElement>();
    render(
      <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
        <Tooltip.Provider>
          <div ref={scroller}>
            <SessionConversation session="s1" live={false} scroller={scroller} />
          </div>
        </Tooltip.Provider>
      </QueryClientProvider>,
    );

    await userEvent.click(await screen.findByRole('button', { name: 'First failure' }));

    await waitFor(() => expect(screen.getByText('npm test')).toBeInTheDocument());
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_HISTORY', { payload: { id: 's1', before: 300 } });
    expect(screen.getByText('npm test').closest('[data-block]')!.className).toContain('outline-accent');
  });
});

/**
 * ASKHIST1c: *Back to bottom* floated over the last message's words (`sticky` inside the transcript). A host that names where
 * it goes is told whether the reader is at the tail and how to go there, and draws the press in a strip of its own outside
 * the transcript, which takes its own room and covers nothing.
 */
describe('the way back to the tail', () => {
  const LIVE = [said(1, 'm1', 'Reading the loader.'), said(2, 'm2', 'Running the gates now.')];

  function draw(onTail?: (tail: TailState) => void) {
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'SESSION_HISTORY') return { session: 's1', events: LIVE, earlier: false, latest: 2 };
      if (type === 'HARNESSES') return { harnesses: [] };
      return {};
    });
    const scroller = createRef<HTMLDivElement>();
    render(
      <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
        <Tooltip.Provider>
          <div ref={scroller} data-testid="transcript">
            <SessionConversation session="s1" live scroller={scroller} onTail={onTail} />
          </div>
        </Tooltip.Provider>
      </QueryClientProvider>,
    );
    return scroller;
  }

  /** The reader scrolls up, as the page measures it: far from the bottom of a long transcript. */
  function scrollUp(element: HTMLElement) {
    Object.defineProperty(element, 'scrollHeight', { value: 2000, configurable: true });
    Object.defineProperty(element, 'clientHeight', { value: 400, configurable: true });
    element.scrollTop = 200;
    fireEvent.scroll(element);
  }

  it('tells its host the reader left the tail, and draws nothing over the words itself', async () => {
    const onTail = vi.fn();
    const scroller = draw(onTail);
    await screen.findByText('Running the gates now.');

    act(() => scrollUp(scroller.current!));
    await waitFor(() => expect(onTail).toHaveBeenLastCalledWith(expect.objectContaining({ atTail: false })));
    expect(within(screen.getByTestId('transcript')).queryByRole('button', { name: 'Back to bottom' })).toBeNull();

    // The host's press goes back to the tail, and the reader is at it again.
    act(() => (onTail.mock.lastCall![0] as TailState).toTail());
    await waitFor(() => expect(onTail).toHaveBeenLastCalledWith(expect.objectContaining({ atTail: true })));
    expect(scroller.current!.scrollTop).toBe(2000);
  });

  it('draws its own press where no host takes it, a 28 px target', async () => {
    const scroller = draw();
    await screen.findByText('Running the gates now.');
    act(() => scrollUp(scroller.current!));

    const press = await screen.findByRole('button', { name: 'Back to bottom' });
    expect(press).toHaveClass('h-7');
  });

  it('draws the host’s strip as a 28 px press that takes its own room', () => {
    const toTail = vi.fn();
    render(<BackToBottom onPress={toTail} />);
    const press = screen.getByRole('button', { name: 'Back to bottom' });
    expect(press).toHaveClass('h-7');
    expect(press.className).not.toMatch(/sticky|absolute|fixed/);
    fireEvent.click(press);
    expect(toTail).toHaveBeenCalledOnce();
  });
});
