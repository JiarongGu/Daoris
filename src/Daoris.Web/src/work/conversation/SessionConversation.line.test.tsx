import { afterEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { createRef } from 'react';
import '../../locales/i18n';

// SWITCHCTX1c-i (the switch-context design §2.7), the organism's half: an earlier part of a line is drawn by a mode of its own, read
// with its own record's liveness, owning neither the region's scroll nor any fact of the frame; and a part's first line, where it
// began by a move, holds its opening line folded under it. The bridge is mocked; the driver's answers are the test's.

const { invoke } = vi.hoisted(() => ({ invoke: vi.fn() }));

vi.mock('@shenora/react', () => ({
  isShenoraAvailable: () => true,
  getBridge: () => ({ isAvailable: true, invoke }),
  useShenora: () => ({ isAvailable: true }),
  useShenoraEvent: () => {},
}));

import { SessionConversation } from './SessionConversation';

const at = '2026-10-25T00:00:00Z';
const asked = { seq: 1, at, kind: 'user', origin: 'person', text: 'Fix the loader.' };
const said = (seq: number, text: string) => ({ seq, at, kind: 'message', id: `m${seq}`, text });
const running = { seq: 3, at, kind: 'tool', id: 'x1', title: 'npm test', status: 'in_progress' };
const used = { seq: 4, at, kind: 'usage', used: 1200, size: 200000 };
const note = (seq: number, code: string, values: Record<string, unknown> = {}) => ({
  seq, at, kind: 'note', text: code, parts: [{ code, values, text: code }],
});

function draw(events: unknown[], props: Partial<React.ComponentProps<typeof SessionConversation>> = {}) {
  invoke.mockImplementation(async (_module: string, type: string) => {
    if (type === 'SESSION_HISTORY') return { session: 's1', events, earlier: false, latest: events.length };
    if (type === 'HARNESSES') return { harnesses: [] };
    return {};
  });
  const scroller = createRef<HTMLDivElement>();
  render(
    <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
      <Tooltip.Provider>
        <div ref={scroller} data-testid="region">
          <SessionConversation session="s1" live={false} scroller={scroller} {...props} />
        </div>
      </Tooltip.Provider>
    </QueryClientProvider>,
  );
  return scroller;
}

afterEach(() => invoke.mockReset());

describe('an earlier part of a line', () => {
  const OPEN = [asked, said(2, 'Reading the loader.'), running];

  it('is read with its own record’s liveness: a live one is not cut, its call not stopped', async () => {
    draw(OPEN, { mode: 'earlier', live: true, chat: true });
    await screen.findByText('Reading the loader.');
    expect(screen.queryByText('The session ended here, before this turn did.')).toBeNull();
    expect(screen.getByText('working…')).toBeInTheDocument();
  });

  it('is cut where its record has ended inside the turn, as an ended session is', async () => {
    draw(OPEN, { mode: 'earlier', live: false, chat: true });
    expect(await screen.findByText('The session ended here, before this turn did.')).toBeInTheDocument();
    expect(screen.queryByText('working…')).toBeNull();
  });

  it('tells the frame nothing: no usage, no wait, no limit’s wait, no tail, no anchor', async () => {
    const host = {
      onUsage: vi.fn(), onWaiting: vi.fn(), onLimitWait: vi.fn(), onTail: vi.fn(), onAnchors: vi.fn(),
    };
    draw([asked, said(2, 'Reading the loader.'), used, running], { mode: 'earlier', live: true, chat: true, ...host });
    await screen.findByText('Reading the loader.');
    for (const [name, told] of Object.entries(host)) expect(told, name).not.toHaveBeenCalled();
  });

  it('neither opens the region at its own head nor follows its tail, nor draws a press for the way back', async () => {
    const scroller = createRef<HTMLDivElement>();
    invoke.mockImplementation(async (_module: string, type: string) => (type === 'SESSION_HISTORY'
      ? { session: 's1', events: OPEN, earlier: false, latest: 3 } : { harnesses: [] }));
    const region = document.createElement('div');
    Object.defineProperty(region, 'scrollHeight', { configurable: true, value: 4000 });
    Object.defineProperty(region, 'clientHeight', { configurable: true, value: 400 });
    region.scrollTop = 1500;
    const scrollTo = vi.fn();
    region.scrollTo = scrollTo as unknown as typeof region.scrollTo;
    (scroller as { current: HTMLElement | null }).current = region;
    render(
      <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
        <Tooltip.Provider>
          <SessionConversation session="s1" live={false} mode="earlier" chat scroller={scroller} />
        </Tooltip.Provider>
      </QueryClientProvider>,
    );
    await screen.findByText('Reading the loader.');
    // An ended part would set the region to its top; a live one would send it to the tail. This one touches neither.
    expect(region.scrollTop).toBe(1500);
    expect(scrollTo).not.toHaveBeenCalled();
    expect(screen.queryByRole('button', { name: 'Back to bottom' })).toBeNull();
  });

  it('withholds the acts a conversation offers: no way through it, no press to the session its words went to', async () => {
    const events = [asked, ...Array.from({ length: 24 }, (_, index) => said(index + 2, `Step ${index + 1}.`))];
    draw(events, { mode: 'earlier', live: false, chat: true, onSession: vi.fn(), onStartFrom: vi.fn(), onGoOnBrief: vi.fn() });
    await screen.findByText('Step 24.');
    expect(screen.queryByRole('button', { name: 'Last words' })).toBeNull();
    expect(screen.queryByRole('searchbox')).toBeNull();
  });
});

describe('the attended part, beside it', () => {
  it('still tells the frame its usage and owns the scroll', async () => {
    const onUsage = vi.fn();
    draw([asked, said(2, 'Reading the loader.'), used], { live: true, chat: true, onUsage });
    await screen.findByText('Reading the loader.');
    await waitFor(() => expect(onUsage).toHaveBeenCalledWith('s1', expect.objectContaining({ used: 1200 })));
  });
});

describe('a part’s first line', () => {
  const MOVED = note(2, 'moved.from', { session: 's0', agent: 'claude-code', account: 'work', why: 'limit', tokens: 1000, count: 40 });
  const OPENING = note(1, 'opening.opened', { account: 'work', why: 'kept' });

  it('holds the opening line folded under a move’s boundary, shown on a press', async () => {
    draw([OPENING, MOVED, said(3, 'Where were we.')], { chat: true });
    const boundary = (await screen.findByText(/Continues the part before it, s0/)).closest<HTMLElement>('div')!;
    // One line on this side, and the opening line not drawn as a third.
    expect(screen.queryByText(/Opened on/)).toBeNull();
    await userEvent.click(within(boundary).getByRole('button', { name: 'Show all' }));
    expect(within(boundary).getByRole('button', { name: 'Fold' })).toBeInTheDocument();
  });

  it('draws the began words as its boundary where it began by no move, once', async () => {
    draw([asked, said(2, 'A sibling’s request.')], { chat: true, began: 'asked by db' });
    expect(await screen.findAllByText('asked by db')).toHaveLength(1);
  });

  it('draws no began words beside a boundary a move wrote: one line, not two', async () => {
    draw([OPENING, MOVED, said(3, 'Where were we.')], { chat: true, began: 'moved on from part 1' });
    await screen.findByText(/Continues the part before it, s0/);
    expect(screen.queryByText('moved on from part 1')).toBeNull();
  });

  it('draws no began words while its first events may be on a page before', async () => {
    invoke.mockImplementation(async (_module: string, type: string) => (type === 'SESSION_HISTORY'
      ? { session: 's1', events: [said(30, 'Late words.')], earlier: true, latest: 30 } : { harnesses: [] }));
    const scroller = createRef<HTMLDivElement>();
    render(
      <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
        <Tooltip.Provider>
          <div ref={scroller}>
            <SessionConversation session="s1" live={false} chat scroller={scroller} began="asked by db" />
          </div>
        </Tooltip.Provider>
      </QueryClientProvider>,
    );
    await screen.findByText('Late words.');
    expect(screen.queryByText('asked by db')).toBeNull();
  });
});
