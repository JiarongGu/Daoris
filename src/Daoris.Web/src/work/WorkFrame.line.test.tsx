import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { useState } from 'react';
import { render, screen, waitFor, within } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import * as Tooltip from '@radix-ui/react-tooltip';

// SWITCHCTX1c-i (the switch-context design §2.7, §6): a session's page draws its line as one conversation, one part of the Work
// frame's suite. The bridge is mocked as present, because the frame does not exist without one (D55); what the parts share is in
// `test/workFrameBridge.ts` and `test/workFrameKit.tsx`. No test starts a window, a console or an agent: the records are the
// driver's answers, as the page reads them.
vi.mock('@shenora/react', async () => (await import('../test/workFrameBridge')).shenoraMock());

import i18n from '../locales/i18n';
import { eventHandlers, invoke } from '../test/workFrameBridge';
import { CHAT, DRIVER_STATE, Hub, QUESTS, REGISTRY, ROSTER, userEvent } from '../test/workFrameKit';
import { WorkFrame } from './WorkFrame';

let SESSIONS: unknown[] = [];

function respond(url: string): Response {
  if (url.startsWith('/api/sessions')) return Response.json(SESSIONS);
  if (url.startsWith('/api/quests')) return Response.json(QUESTS);
  if (url.startsWith('/api/registry')) return Response.json(REGISTRY);
  throw new Error(`unstubbed request: ${url}`);
}

const at = '2026-10-25T09:00:00Z';
const asked = (seq: number, text: string) => ({ seq, at, kind: 'user', origin: 'person', text });
const said = (seq: number, text: string) => ({ seq, at, kind: 'message', id: `m${seq}`, text });
const ended = (seq: number) => ({ seq, at, kind: 'turn', stopReason: 'end_turn' });
const note = (seq: number, code: string, values: Record<string, unknown> = {}) => ({
  seq, at, kind: 'note', text: code, parts: [{ code, values, text: code }],
});

/** A record of the line, a conversation of the repository, in the order the driver lists them. */
type Part = { id: string; state?: string; from?: string; fromWhy?: string; adapter?: string; kind?: string; began?: string; after?: string; shown?: string };

/** The line as the driver answers it: the records, its entry's parts oldest first, and each part's events. */
function answering(parts: Part[], events: Record<string, unknown[]>, entry: Part[] = parts) {
  SESSIONS = parts.map((part, index) => ({
    ...CHAT, id: part.id, adapter: part.adapter ?? 'claude-code', kind: part.kind ?? 'chat', state: part.state ?? 'completed',
    ...(part.from ? { from: part.from, fromWhy: part.fromWhy ?? 'press' } : {}),
    created: `2026-10-25T0${index}:00:00Z`, updated: `2026-10-25T0${index}:30:00Z`,
  }));
  invoke.mockImplementation(async (_module: string, type: string, body?: { payload?: { id?: string } }) => {
    if (type === 'HARNESSES') return ROSTER;
    if (type === 'SESSION_HISTORY') {
      const list = events[body?.payload?.id ?? ''] ?? [];
      return { session: body?.payload?.id, events: list, earlier: false, latest: list.length };
    }
    if (type === 'SESSION_GROUPS') {
      return {
        sessions: parts.map((part) => ({
          session: part.id, group: part.state === 'working' ? 'working' : 'ended', shown: part.shown ?? part.state ?? 'completed',
          archived: false, teammate: false,
        })),
        entries: [{
          entry: 'session:c0000001/engine', origin: 'session:c0000001', repository: 'engine', group: 'ended', leading: entry.at(-1)!.id,
          parts: entry.map((part) => ({
            session: part.id, quest: null, shown: part.state ?? 'completed', group: 'ended', began: part.began ?? (part.from ? 'moved' : 'opened'),
            after: part.after ?? part.from ?? null, askedBy: null,
          })),
        }],
      };
    }
    return DRIVER_STATE;
  });
}

/** The frame with its selection held as `App` holds it, so attending a part shows it. */
function show(selected: string) {
  const onSelect = vi.fn();
  function Held() {
    const [chosen, setChosen] = useState<string | null>(selected);
    return <WorkFrame selected={chosen} onSelect={(id) => { onSelect(id); setChosen(id); }} notify={() => {}} />;
  }
  render(
    <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
      <Hub />
      <Tooltip.Provider><Held /></Tooltip.Provider>
    </QueryClientProvider>,
  );
  return { onSelect };
}

const main = () => document.querySelector<HTMLElement>('main[data-region="main"]')!;
const partOf = (id: string) => main().querySelector<HTMLElement>(`[data-part="${id}"]:not([data-attended])`);
const drawn = () => [...main().querySelectorAll('[data-part]:not([data-attended])')].map((each) => each.getAttribute('data-part'));
const strip = () => within(screen.getByRole('navigation', { name: 'Parts' }));

/** Whether `first` is drawn before `second` in the page's order. */
const before = (first: Node, second: Node) => Boolean(first.compareDocumentPosition(second) & Node.DOCUMENT_POSITION_FOLLOWING);

/** A region jsdom lays nothing out for: it is taller than it shows, as a long conversation is. */
function tall() {
  vi.spyOn(HTMLElement.prototype, 'scrollHeight', 'get').mockImplementation(function (this: HTMLElement) {
    return this.matches('main[data-region="main"]') ? 6000 : 0;
  });
  vi.spyOn(HTMLElement.prototype, 'clientHeight', 'get').mockImplementation(function (this: HTMLElement) {
    return this.matches('main[data-region="main"]') ? 600 : 0;
  });
}

describe('a session’s page draws its line as one conversation', () => {
  beforeEach(() => {
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => respond(String(input))));
  });
  afterEach(async () => {
    vi.restoreAllMocks();
    vi.unstubAllGlobals();
    invoke.mockReset();
    eventHandlers.clear();
    window.localStorage.clear();
    await i18n.changeLanguage('en');
  });

  it('shows a line handed to another agent as one conversation across its parts, and never as a new session', async () => {
    answering(
      [{ id: 'c0000001', adapter: 'codex-acp' }, { id: 'c0000002', from: 'c0000001', fromWhy: 'press' }],
      {
        c0000001: [
          asked(1, 'Fix the loader.'), said(2, 'The first agent read the loader.'), ended(3),
          note(4, 'chat.handed-over', { agent: 'claude-code', session: 'c0000002' }),
        ],
        c0000002: [
          note(1, 'chat.taken-over', { agent: 'codex-acp', session: 'c0000001', why: 'press' }),
          said(2, 'The second agent goes on.'),
        ],
      },
    );
    show('c0000002');

    const earlier = await screen.findByText('The first agent read the loader.');
    const went = await screen.findByText('This conversation went on with claude-code, in conversation c0000002.');
    const came = await screen.findByText(/handed Daoris's record of it, not its own turns\.$/);
    const now = await screen.findByText('The second agent goes on.');
    expect(before(earlier, went) && before(went, came) && before(came, now)).toBe(true);
    expect(drawn()).toEqual(['c0000001']);
    expect(main().textContent).not.toMatch(/new session/i);
  });

  it('draws a curve move’s line with one line on each side, the new part’s opening line folded under its boundary', async () => {
    const figures = { window: 'weekly', used: 62, curve: 45 };
    answering(
      [{ id: 'c0000001' }, { id: 'c0000002', from: 'c0000001', fromWhy: 'curve' }],
      {
        c0000001: [asked(1, 'Fix the loader.'), said(2, 'Reading it.'), ended(3), note(4, 'chat.moved', { to: 'work', from: 'personal', ...figures })],
        c0000002: [
          note(1, 'opening.opened', { account: 'work', why: 'locked', agent: 'claude-code', over: 'personal' }),
          note(2, 'chat.moved-from', { from: 'personal', ...figures }),
          said(3, 'Going on.'),
        ],
      },
    );
    show('c0000002');

    const went = await screen.findByText(/at the end of its turn/);
    const came = await screen.findByText(/which had used 62% of its weekly limit/);
    expect(before(went, came)).toBe(true);
    // The opening line is under the boundary, a press away, and never a third line.
    expect(screen.queryByText(/Opened on/)).toBeNull();
    const boundary = came.closest<HTMLElement>('div')!;
    await userEvent.click(within(boundary).getByRole('button', { name: 'Show all' }));
    expect(within(boundary).getByText(/Opened on/)).toBeInTheDocument();
    expect(main().textContent).not.toMatch(/new session/i);
  });

  it('does not draw a part running alongside above the attended one', async () => {
    answering(
      [{ id: 'c0000001' }, { id: 'c0000002', from: 'c0000001' }, { id: 'c0000003', began: 'asked', after: undefined }, { id: 'c0000004', from: 'c0000002' }],
      {
        c0000001: [asked(1, 'Fix the loader.'), said(2, 'From the first part.')],
        c0000002: [said(1, 'From the second part.')],
        c0000003: [asked(1, 'A sibling’s request.'), said(2, 'From the part alongside.')],
        c0000004: [said(1, 'From the newest part.')],
      },
    );
    show('c0000004');

    await screen.findByText('From the newest part.');
    await screen.findByText('From the second part.');
    expect(drawn()).toEqual(['c0000001', 'c0000002']);
    expect(screen.queryByText('From the part alongside.')).toBeNull();
    // It stays a press away in the strip.
    await userEvent.click(screen.getByRole('button', { name: 'Show the parts' }));
    expect(strip().getAllByRole('button')).toHaveLength(4);
  });

  it('draws an earlier part that is still live as live: its last turn not cut, its calls not stopped', async () => {
    answering(
      [{ id: 'c0000001', state: 'working' }, { id: 'c0000002', from: 'c0000001' }],
      {
        c0000001: [asked(1, 'Fix the loader.'), said(2, 'Still reading.'), { seq: 3, at, kind: 'tool', id: 'x1', title: 'npm test', status: 'in_progress' }],
        c0000002: [said(1, 'The new part.')],
      },
    );
    show('c0000002');

    await screen.findByText('Still reading.');
    const part = within(partOf('c0000001')!);
    expect(part.queryByText('The session ended here, before this turn did.')).toBeNull();
    expect(part.getByText('working…')).toBeInTheDocument();
  });

  it('lets the attended part alone own the scroll and the frame’s facts', async () => {
    tall();
    const set = vi.spyOn(Element.prototype, 'scrollTop', 'set');
    answering(
      [{ id: 'c0000001' }, { id: 'c0000002', from: 'c0000001', state: 'working' }],
      {
        c0000001: [asked(1, 'Fix the loader.'), said(2, 'The long first part.'), { seq: 3, at, kind: 'usage', used: 990_000, size: 1_000_000 }],
        c0000002: [said(1, 'The live part.'), { seq: 2, at, kind: 'usage', used: 34_000, size: 1_000_000 }],
      },
    );
    show('c0000002');

    await screen.findByText('The live part.');
    await screen.findByText('The long first part.');
    // The ring under the composer reads the attended part’s one usage, never the earlier part’s 99%.
    expect(await screen.findByRole('meter', { name: 'context' })).toHaveTextContent('3%');
    // A live line follows the attended part’s tail; no earlier part has opened the region at its own head.
    await waitFor(() => expect(main().scrollTop).toBe(6000));
    expect(set.mock.calls.map(([top]) => top)).not.toContain(0);
    // One tail: the press the frame draws for the way back is the attended part’s, and is not offered at the tail.
    expect(screen.queryByRole('button', { name: 'Back to bottom' })).toBeNull();
  });

  it('opens an ended line at the attended record’s head, the nearest earlier part beneath it', async () => {
    tall();
    answering(
      [{ id: 'c0000001' }, { id: 'c0000002', from: 'c0000001' }],
      { c0000001: [asked(1, 'Fix the loader.'), said(2, 'The earlier part.')], c0000002: [said(1, 'The attended part.')] },
    );
    show('c0000002');

    await screen.findByText('The attended part.');
    await screen.findByText('The earlier part.');
    expect(main().scrollTop).toBe(0);
    // The page’s order: the attended record’s head first, then the earlier part, then the attended part.
    expect(before(screen.getByRole('navigation', { name: 'Parts' }), partOf('c0000001')!)).toBe(true);
    expect(before(partOf('c0000001')!, screen.getByText('The attended part.'))).toBe(true);
    // The way to the bottom stands beside it, since the region is taller than it shows.
    expect(await screen.findByRole('button', { name: 'Back to bottom' })).toBeInTheDocument();
  });

  it('hides the parts after an earlier one when it is pressed, and draws them again when the newest is', async () => {
    answering(
      [{ id: 'c0000001' }, { id: 'c0000002', from: 'c0000001' }, { id: 'c0000003', from: 'c0000002' }],
      {
        c0000001: [asked(1, 'Fix the loader.'), said(2, 'First part words.')],
        c0000002: [said(1, 'Second part words.')],
        c0000003: [said(1, 'Third part words.')],
      },
    );
    show('c0000003');
    await screen.findByText('Third part words.');
    await screen.findByText('Second part words.');
    expect(drawn()).toEqual(['c0000001', 'c0000002']);

    await userEvent.click(screen.getByRole('button', { name: 'Show the parts' }));
    await userEvent.click(strip().getAllByRole('button')[1]!);
    // Part 2 attended: its own ancestry above it, and the third part no longer drawn.
    await waitFor(() => expect(drawn()).toEqual(['c0000001']));
    expect(screen.queryByText('Third part words.')).toBeNull();
    expect(screen.getByText('Second part words.')).toBeInTheDocument();

    await userEvent.click(strip().getAllByRole('button')[2]!);
    await waitFor(() => expect(drawn()).toEqual(['c0000001', 'c0000002']));
    expect(await screen.findByText('Third part words.')).toBeInTheDocument();
  });

  it('draws a line of five earlier parts as three, the press saying how many more, in English and in Chinese', async () => {
    const ids = ['c0000001', 'c0000002', 'c0000003', 'c0000004', 'c0000005', 'c0000006'];
    const parts = ids.map((id, index) => ({ id, ...(index > 0 ? { from: ids[index - 1] } : {}) }));
    const events = Object.fromEntries(ids.map((id, index) => [id, [said(1, `Words of part ${index + 1}.`)]]));
    answering(parts, events);
    show('c0000006');

    await screen.findByText('Words of part 6.');
    await screen.findByText('Words of part 5.');
    await waitFor(() => expect(drawn()).toEqual(['c0000003', 'c0000004', 'c0000005']));
    expect(screen.queryByText('Words of part 2.')).toBeNull();
    await userEvent.click(screen.getByRole('button', { name: 'Show 2 earlier parts' }));
    await screen.findByText('Words of part 1.');
    expect(drawn()).toEqual(ids.slice(0, 5));
    expect(screen.queryByRole('button', { name: /earlier parts?$/ })).toBeNull();
  });

  it('presses the same line in Chinese: 显示更早的 2 段', async () => {
    await i18n.changeLanguage('zh');
    const ids = ['c0000001', 'c0000002', 'c0000003', 'c0000004', 'c0000005', 'c0000006'];
    answering(
      ids.map((id, index) => ({ id, ...(index > 0 ? { from: ids[index - 1] } : {}) })),
      Object.fromEntries(ids.map((id, index) => [id, [said(1, `Words of part ${index + 1}.`)]])),
    );
    show('c0000006');
    await screen.findByText('Words of part 5.');
    expect(await screen.findByRole('button', { name: '显示更早的 2 段' })).toBeInTheDocument();
  });

  it('draws a driven line’s moves written before the rewording whole, in the new words, with their tokens and count', async () => {
    const written = { session: 'c0000001', agent: 'claude-code', account: 'work', why: 'limit', tokens: 182000, count: 4096 };
    answering(
      [{ id: 'c0000001', kind: 'driven' }, { id: 'c0000002', kind: 'driven', from: 'c0000001', fromWhy: 'limit' }],
      {
        c0000001: [said(1, 'Cut off by the limit.'), note(2, 'moved.on', { ...written, session: 'c0000002' })],
        c0000002: [note(1, 'moved.from', written), said(2, 'Going on from the brief.')],
      },
    );
    show('c0000002');

    expect(await screen.findByText(/Went on as a new part of this session, c0000002, with claude-code as .*because its account reached its limit: its context stood at 182,000 tokens, and the new part was handed Daoris's brief of it, 4,096 characters\./))
      .toBeInTheDocument();
    expect(await screen.findByText(/Continues the part before it, c0000001, which ran on claude-code as .*182,000 tokens, and it was handed Daoris's brief of it, 4,096 characters\./))
      .toBeInTheDocument();
    expect(main().textContent).not.toMatch(/new session/i);
  });

  it('bounds a sibling’s part by its began words, once, where it began by no move', async () => {
    answering(
      [{ id: 'c0000001' }, { id: 'c0000002', from: 'c0000001', fromWhy: 'retry', began: 'retried', after: 'c0000001' }],
      { c0000001: [asked(1, 'Fix the loader.'), said(2, 'The failed try.')], c0000002: [said(1, 'The retry.')] },
    );
    show('c0000002');

    await screen.findByText('The retry.');
    await screen.findByText('The failed try.');
    expect(await screen.findAllByText('retried after part 1')).toHaveLength(1);
    expect(main().textContent).not.toMatch(/new session/i);
  });

  it('attends a part pressed in the strip and scrolls to it', async () => {
    const scrolled = vi.spyOn(Element.prototype, 'scrollIntoView').mockImplementation(() => {});
    answering(
      [{ id: 'c0000001' }, { id: 'c0000002', from: 'c0000001' }, { id: 'c0000003', from: 'c0000002' }],
      {
        c0000001: [asked(1, 'Fix the loader.'), said(2, 'First part words.')],
        c0000002: [said(1, 'Second part words.')],
        c0000003: [said(1, 'Third part words.')],
      },
    );
    const { onSelect } = show('c0000003');
    await screen.findByText('Second part words.');

    await userEvent.click(screen.getByRole('button', { name: 'Show the parts' }));
    await userEvent.click(strip().getAllByRole('button')[1]!);
    expect(onSelect).toHaveBeenLastCalledWith('c0000002');
    // The part pressed is the one the page scrolls to: the box its conversation stands in.
    await waitFor(() => expect(scrolled).toHaveBeenCalled());
    const target = scrolled.mock.contexts.at(-1) as HTMLElement;
    expect(target.getAttribute('data-part')).toBe('c0000002');
    expect(within(target).getByText('Second part words.')).toBeInTheDocument();
  });
});
