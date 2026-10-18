import { useState } from 'react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { act, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import * as Tooltip from '@radix-ui/react-tooltip';

// The frame's geometry in the Work frame, one part of its suite (WEBSPEED1 cut the one file, which ran 181 s alone, into
// parts that run beside each other). The bridge is mocked as present, because the frame does not exist without one (D55);
// what the parts share is in `test/workFrameBridge.ts` and `test/workFrameKit.tsx`.
vi.mock('@shenora/react', async () => (await import('../test/workFrameBridge')).shenoraMock());

import { eventHandlers, invoke } from '../test/workFrameBridge';
import {
  CHAT, DRIVEN, DRIVER_STATE, Hub, PARKED, QUESTS, REGISTRY, going, show, userEvent,
} from '../test/workFrameKit';
import { WorkFrame } from './WorkFrame';

let SESSIONS: unknown[] = [DRIVEN];

function respond(url: string): Response {
  if (url.startsWith('/api/sessions')) return Response.json(SESSIONS);
  if (url.startsWith('/api/quests')) return Response.json(QUESTS);
  if (url.startsWith('/api/registry')) return Response.json(REGISTRY);
  throw new Error(`unstubbed request: ${url}`);
}

// 🔴 Every test starts from a viewer with nothing remembered. The frame keeps its layout per viewer,
// and a test that hid the console left it hidden for whichever console test ran next: four failed
// in a shuffled order (UX5, 2026-09-26), and passed in file order only by luck of the order.
afterEach(() => window.localStorage.clear());

/**
 * FRAME6: the frame's geometry (components plan §3a) — a rail and a dock a person can resize, close to
 * a strip and open again, with the session in the middle keeping its floor. The numbers themselves are
 * `layout.ts`'s, asserted there; this is the frame doing what they say, and remembering what the person
 * chose.
 */
describe('the frame\'s geometry (FRAME6)', () => {
  const KEPT = ['daoris.railWidth', 'daoris.railClosed', 'daoris.dockWidth', 'daoris.dockShare', 'daoris.dockClosed'];
  const widen = (width: number) => act(() => {
    Object.defineProperty(window, 'innerWidth', { configurable: true, value: width });
    window.dispatchEvent(new Event('resize'));
  });

  beforeEach(() => {
    SESSIONS = [DRIVEN, CHAT];
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => respond(String(input))));
    invoke.mockImplementation(async () => DRIVER_STATE);
    widen(1600);
    // A viewer who has opened the dock: it opens on demand (UX5 U7), and these are about it open.
    window.localStorage.setItem('daoris.dockClosed', '0');
  });
  afterEach(() => {
    vi.unstubAllGlobals();
    invoke.mockReset();
    eventHandlers.clear();
    for (const key of KEPT) window.localStorage.removeItem(key);
    widen(1024);
  });

  it('resizes the rail from the keyboard within its bounds, and remembers the width', async () => {
    show('s1a2b3c4');
    const edge = await screen.findByRole('separator', { name: 'session list width' });
    expect(edge).toHaveAttribute('aria-valuenow', '280');

    edge.focus();
    await userEvent.keyboard('{ArrowRight}');
    expect(screen.getByRole('separator', { name: 'session list width' })).toHaveAttribute('aria-valuenow', '304');
    expect(window.localStorage.getItem('daoris.railWidth')).toBe('304');
  });

  it('closes the rail to a strip that still reaches every session, and only the person opens it again', async () => {
    const { onSelect } = show('s1a2b3c4');
    await userEvent.click(await screen.findByRole('button', { name: 'Hide the session list' }));

    expect(screen.queryByRole('separator', { name: 'session list width' })).toBeNull();
    await userEvent.click(await screen.findByRole('button', { name: 'Chat · engine · working' }));
    expect(onSelect).toHaveBeenCalledWith('c0ffee11');

    // A wider window is not the person asking for it back.
    widen(2400);
    expect(screen.queryByRole('separator', { name: 'session list width' })).toBeNull();
    expect(window.localStorage.getItem('daoris.railClosed')).toBe('1');

    await userEvent.click(screen.getByRole('button', { name: 'Show the session list' }));
    expect(await screen.findByRole('separator', { name: 'session list width' })).toBeInTheDocument();
  });

  it('draws the rail as a strip in a narrow window, and gives it back when the window widens', async () => {
    widen(1000);
    show('s1a2b3c4');
    expect(await screen.findByRole('button', { name: 'Chat · engine · working' })).toBeInTheDocument();
    expect(screen.queryByRole('separator', { name: 'session list width' })).toBeNull();

    widen(1600);
    expect(await screen.findByRole('separator', { name: 'session list width' })).toBeInTheDocument();
  });

  /**
   * D118 §3a: the rail is a strip by room, not below a fixed 1024 px. At 900 px beside a closed side bar
   * the conversation has 540 px beside the rail, so the rail stays open; the side bar opened takes the room.
   */
  it('keeps the rail open at 900 px beside a closed side bar, and draws its strip beside an open one', async () => {
    widen(900);
    window.localStorage.setItem('daoris.dockClosed', '1');
    show('s1a2b3c4');
    expect(await screen.findByRole('separator', { name: 'session list width' })).toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: 'Open Timeline' }));
    await waitFor(() => expect(screen.queryByRole('separator', { name: 'session list width' })).toBeNull());
    expect(screen.getByRole('button', { name: 'Chat · engine · working' })).toBeInTheDocument();
  });

  /**
   * D118 §3a, amending FRAME6: a strip the window drew opens the rail OVER the conversation, so an ended
   * session and the rail's search are within reach at 900 px. It closes on a choice, on Escape and on a
   * press outside it, and is never remembered.
   */
  it('lays the rail over the conversation from a strip the window drew, and closes it on a choice, Escape or a press outside', async () => {
    widen(900);
    const { onSelect } = show('s1a2b3c4');
    const open = await screen.findByRole('button', { name: 'Show the session list' });

    await userEvent.click(open);
    let over = screen.getByRole('region', { name: 'Sessions' });
    expect(within(over).getByRole('searchbox', { name: 'search sessions' })).toBeInTheDocument();
    // The strip stays beside it, and the conversation keeps its room: nothing was pushed aside.
    expect(screen.getByRole('button', { name: 'Chat · engine · working' })).toBeInTheDocument();
    // A row is its button, named by what it holds; its menu is named for it.
    const chat = within(over).getAllByRole('button').find((button) => !button.hasAttribute('aria-label') && button.textContent?.includes('Chat'));
    await userEvent.click(chat!);
    expect(onSelect).toHaveBeenCalledWith('c0ffee11');
    expect(screen.queryByRole('region', { name: 'Sessions' })).toBeNull();

    await userEvent.click(screen.getByRole('button', { name: 'Show the session list' }));
    over = screen.getByRole('region', { name: 'Sessions' });
    await userEvent.keyboard('{Escape}');
    expect(screen.queryByRole('region', { name: 'Sessions' })).toBeNull();
    expect(screen.getByRole('button', { name: 'Show the session list' })).toHaveFocus();

    await userEvent.click(screen.getByRole('button', { name: 'Show the session list' }));
    fireEvent.pointerDown(await screen.findByRole('heading', { level: 1, name: 'Expose a streaming budget' }));
    expect(screen.queryByRole('region', { name: 'Sessions' })).toBeNull();
    expect(window.localStorage.getItem('daoris.railClosed')).toBeNull();
  });

  it('gives the rail back beside the conversation, not over it, when the room returns', async () => {
    widen(900);
    show('s1a2b3c4');
    await userEvent.click(await screen.findByRole('button', { name: 'Show the session list' }));
    expect(screen.getByRole('region', { name: 'Sessions' })).toBeInTheDocument();

    widen(1600);
    expect(await screen.findByRole('separator', { name: 'session list width' })).toBeInTheDocument();
    expect(screen.queryByRole('region', { name: 'Sessions' })).toBeNull();
    // Narrowed again, the window draws its strip: the laying over was not kept.
    widen(900);
    await waitFor(() => expect(screen.queryByRole('separator', { name: 'session list width' })).toBeNull());
    expect(screen.queryByRole('region', { name: 'Sessions' })).toBeNull();
  });

  /**
   * ENTRY1g (D161's ENTRY1b note): a go to a group of the rail lays its strip over the conversation, whoever drew the strip,
   * and the rail laid over brings the group into view there; the strip beside it lets nothing go. Laid over, it stays until
   * the person lets it go, and nothing is remembered.
   */
  it('lays the strip the window drew over the conversation for a go, and brings its group into view there', async () => {
    SESSIONS = [DRIVEN, PARKED];
    widen(900);
    const brought = going('you');

    const over = await screen.findByRole('region', { name: 'Sessions' });
    const heading = await within(over).findByRole('heading', { level: 3, name: 'Waiting on you (1)' });
    await waitFor(() => expect(brought).toHaveBeenCalledTimes(1));
    expect(heading).toHaveFocus();
    expect(screen.getByRole('region', { name: 'Sessions' })).toBeInTheDocument();

    await userEvent.keyboard('{Escape}');
    expect(screen.queryByRole('region', { name: 'Sessions' })).toBeNull();
    expect(window.localStorage.getItem('daoris.railClosed')).toBeNull();
  });

  it('lays the strip the person closed over the conversation for a go, and their closing stands once it goes', async () => {
    SESSIONS = [DRIVEN, PARKED];
    window.localStorage.setItem('daoris.railClosed', '1');
    const brought = going('you');

    const over = await screen.findByRole('region', { name: 'Sessions' });
    const heading = await within(over).findByRole('heading', { level: 3, name: 'Waiting on you (1)' });
    await waitFor(() => expect(brought).toHaveBeenCalledTimes(1));
    expect(heading).toHaveFocus();

    fireEvent.pointerDown(document.body);
    expect(screen.queryByRole('region', { name: 'Sessions' })).toBeNull();
    expect(screen.getByRole('button', { name: 'Show the session list' })).toBeInTheDocument();
    expect(window.localStorage.getItem('daoris.railClosed')).toBe('1');
  });

  /** FRAME6's keys are still read, so nothing a person closed or widened changes on the upgrade (D118 §3f). */
  it('reads the rail\'s old keys: its width and its closing', async () => {
    window.localStorage.setItem('daoris.railWidth', '330');
    const { unmount } = show(null);
    expect(await screen.findByRole('separator', { name: 'session list width' })).toHaveAttribute('aria-valuenow', '330');
    // The rail's rows are drawn, so what the list asked of the driver has been answered.
    await screen.findByText('Expose a streaming budget');
    unmount();

    window.localStorage.setItem('daoris.railClosed', '1');
    show(null);
    expect(await screen.findByRole('button', { name: 'Show the session list' })).toBeInTheDocument();
    expect(await screen.findByRole('button', { name: 'Chat · engine · working' })).toBeInTheDocument();
    expect(screen.queryByRole('separator', { name: 'session list width' })).toBeNull();
  });

  /** D118 §3e (audit A7): the rail moves by ↑ and ↓, where it was walked by Tab. */
  it('moves between the rail\'s rows by the arrows', async () => {
    show('s1a2b3c4');
    const rail = await screen.findByRole('navigation', { name: 'Sessions' });
    await within(rail).findAllByText('Expose a streaming budget');
    // Each row is its button, named by what it holds, and its menu, named for it.
    const rows = within(rail).getAllByRole('button').filter((button) => !button.hasAttribute('aria-label'));
    expect(rows).toHaveLength(2);
    const [first, second] = rows;
    first!.focus();
    await userEvent.keyboard('{ArrowDown}');
    expect(second).toHaveFocus();
    await userEvent.keyboard('{ArrowUp}');
    expect(first).toHaveFocus();
  });

  /**
   * D118 §1, §5: every view hands the frame a `ViewLayout`, its list pane where it has one and its main
   * area, and the frame draws the list for every view that hands one. Sessions' rail is one such list.
   */
  describe('a view with a list, and a view without one', () => {
    const ROWS = ['Expose a streaming budget', 'Read the budget from the level file'];
    /** A view's list as it hands it in, with its chosen item held for it, as the application holds it. */
    function Quests({ initial = null }: { initial?: string | null }) {
      const [chosen, setChosen] = useState<string | null>(initial);
      return (
        <WorkFrame
          selected={null}
          onSelect={vi.fn()}
          notify={() => {}}
          layout={{
            list: {
              view: 'quests',
              name: 'Quests',
              labels: { open: 'Show the quest list', close: 'Hide the quest list', resize: 'quest list width' },
              chosen,
              body: (
                <ul>
                  {ROWS.map((title) => (
                    <li key={title} data-list-row=""><button type="button" onClick={() => setChosen(title)}>{title}</button></li>
                  ))}
                </ul>
              ),
            },
            main: <main><h1>{chosen ?? 'Nothing chosen'}</h1></main>,
          }}
        />
      );
    }
    const frame = () => {
      const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
      return render(<QueryClientProvider client={client}><Tooltip.Provider><Quests /></Tooltip.Provider></QueryClientProvider>);
    };

    it('draws its list beside its main area, with the side bar and the panel, and no session rail', async () => {
      frame();
      expect(await screen.findByRole('separator', { name: 'quest list width' })).toHaveAttribute('aria-valuenow', '280');
      expect(screen.getByRole('heading', { level: 1, name: 'Nothing chosen' })).toBeInTheDocument();
      expect(screen.getByRole('complementary', { name: 'right side bar' })).toBeInTheDocument();
      expect(screen.getByRole('region', { name: 'the panel' })).toBeInTheDocument();
      expect(screen.queryByRole('separator', { name: 'session list width' })).toBeNull();

      await userEvent.click(screen.getByRole('button', { name: 'Read the budget from the level file' }));
      expect(screen.getByRole('heading', { level: 1, name: 'Read the budget from the level file' })).toBeInTheDocument();
    });

    it('closes and widens as its own, in its own keys, leaving Sessions\' rail as it was', async () => {
      frame();
      const edge = await screen.findByRole('separator', { name: 'quest list width' });
      edge.focus();
      await userEvent.keyboard('{ArrowRight}');
      expect(window.localStorage.getItem('daoris.list.quests.width')).toBe('304');

      await userEvent.click(screen.getByRole('button', { name: 'Hide the quest list' }));
      expect(screen.queryByRole('separator', { name: 'quest list width' })).toBeNull();
      expect(window.localStorage.getItem('daoris.list.quests.closed')).toBe('1');
      expect(window.localStorage.getItem('daoris.railClosed')).toBeNull();
      expect(window.localStorage.getItem('daoris.railWidth')).toBeNull();

      await userEvent.click(screen.getByRole('button', { name: 'Show the quest list' }));
      expect(await screen.findByRole('separator', { name: 'quest list width' })).toHaveAttribute('aria-valuenow', '304');
      window.localStorage.removeItem('daoris.list.quests.width');
    });

    it('lays its list over the main area from a strip the window drew, and a choice closes it', async () => {
      widen(900);
      frame();
      await userEvent.click(await screen.findByRole('button', { name: 'Show the quest list' }));
      const over = screen.getByRole('region', { name: 'Quests' });

      await userEvent.click(within(over).getByRole('button', { name: 'Expose a streaming budget' }));
      expect(screen.getByRole('heading', { level: 1, name: 'Expose a streaming budget' })).toBeInTheDocument();
      await waitFor(() => expect(screen.queryByRole('region', { name: 'Quests' })).toBeNull());
      expect(window.localStorage.getItem('daoris.list.quests.closed')).toBeNull();
    });

    it('draws no list pane for a view that hands none', async () => {
      // No live chat: its turns would be asked of the driver after this short test has let the bridge go.
      SESSIONS = [DRIVEN];
      const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
      render(
        <QueryClientProvider client={client}>
          <Tooltip.Provider>
            <WorkFrame selected={null} onSelect={vi.fn()} notify={() => {}} layout={{ main: <main><h1>Overview</h1></main> }} />
          </Tooltip.Provider>
        </QueryClientProvider>,
      );
      expect(await screen.findByRole('heading', { level: 1, name: 'Overview' })).toBeInTheDocument();
      expect(document.querySelector('[data-region="list"]')).toBeNull();
    });
  });

  /**
   * LAYOUT1: a dragged dock was remembered in pixels and stayed 389px while the window grew
   * from 1518 to 1923 (measured on the shell). It is remembered as its share of the window now.
   */
  it('remembers a dragged dock as its share of the window, so it grows when the window does', async () => {
    show('s1a2b3c4');
    const edge = await screen.findByRole('separator', { name: 'side bar width' });
    edge.focus();
    await userEvent.keyboard('{ArrowRight}');
    const dragged = Number(screen.getByRole('separator', { name: 'side bar width' }).getAttribute('aria-valuenow'));
    expect(Number(window.localStorage.getItem('daoris.dockShare'))).toBeCloseTo(dragged / 1600, 5);

    widen(2400);
    expect(Number(screen.getByRole('separator', { name: 'side bar width' }).getAttribute('aria-valuenow')))
      .toBe(Math.round(2400 * (dragged / 1600)));
  });

  it('reads a width kept in pixels once, as its share of the window, and forgets the pixels', async () => {
    window.localStorage.setItem('daoris.dockWidth', '400');
    show('s1a2b3c4');

    expect(await screen.findByRole('separator', { name: 'side bar width' })).toHaveAttribute('aria-valuenow', '400');
    expect(window.localStorage.getItem('daoris.dockShare')).toBe(String(400 / 1600));
    expect(window.localStorage.getItem('daoris.dockWidth')).toBeNull();

    widen(2000);
    expect(screen.getByRole('separator', { name: 'side bar width' })).toHaveAttribute('aria-valuenow', '500');
  });

  /**
   * UX5 U7: the dock was open by default at 45%, where the reference's opens on demand, and on a
   * 1400px window the conversation started at 442px, near its floor. A viewer who never chose sees
   * the conversation with the dock closed to its strip; opening it is remembered like closing it.
   */
  it('opens the dock on demand: closed to its strip until the person opens it, and then remembered', async () => {
    window.localStorage.removeItem('daoris.dockClosed');
    show('s1a2b3c4');
    await screen.findByRole('button', { name: 'Open Review' });
    expect(screen.queryByRole('tablist', { name: 'right side bar' })).toBeNull();

    await userEvent.click(screen.getByRole('button', { name: 'Open Timeline' }));
    expect(await screen.findByRole('tab', { name: 'Timeline' })).toHaveAttribute('aria-selected', 'true');
    expect(window.localStorage.getItem('daoris.dockClosed')).toBe('0');
  });

  it('closes the dock to a strip, and opens it again only on the person\'s press, on the tab they chose', async () => {
    show('s1a2b3c4');
    await userEvent.click(await screen.findByRole('button', { name: 'Close the side bar' }));
    expect(screen.queryByRole('tablist', { name: 'right side bar' })).toBeNull();

    widen(2400);
    expect(screen.queryByRole('tablist', { name: 'right side bar' })).toBeNull();
    expect(window.localStorage.getItem('daoris.dockClosed')).toBe('1');

    await userEvent.click(screen.getByRole('button', { name: 'Open Review' }));
    expect(await screen.findByRole('tab', { name: 'Review' })).toHaveAttribute('aria-selected', 'true');
  });

  it('keeps each session\'s own tab in the dock', async () => {
    const { rerender, client, onSelect } = show('s1a2b3c4');
    const attend = (id: string) => rerender(
      <QueryClientProvider client={client}>
        <Hub />
        <Tooltip.Provider><WorkFrame selected={id} onSelect={onSelect} notify={() => {}} /></Tooltip.Provider>
      </QueryClientProvider>,
    );

    await userEvent.click(await screen.findByRole('tab', { name: 'Review' }));
    attend('c0ffee11');
    expect(await screen.findByRole('tab', { name: 'Timeline' })).toHaveAttribute('aria-selected', 'true');
    attend('s1a2b3c4');
    expect(await screen.findByRole('tab', { name: 'Review' })).toHaveAttribute('aria-selected', 'true');
  });

  it('fills the frame when asked, drawing the same surface rather than a new one', async () => {
    show('s1a2b3c4');
    const surface = await screen.findByRole('tabpanel');

    await userEvent.click(screen.getByRole('button', { name: 'Fill the frame' }));
    expect(screen.getByRole('tabpanel')).toBe(surface);
    await userEvent.click(screen.getByRole('button', { name: 'Back beside the session' }));
    expect(screen.getByRole('tabpanel')).toBe(surface);
  });

  /**
   * The column used to carry the timeline on a window under 1024px, because the dock was hidden
   * there. FRAME6 keeps the dock at every width, so the column's copy drew the timeline twice.
   */
  it('draws the timeline once on a narrow window, in the dock', async () => {
    widen(900);
    show('s1a2b3c4');
    await screen.findByRole('tab', { name: 'Timeline' });
    expect(await screen.findAllByText('reached working')).toHaveLength(1);
  });

  it('asks to be closed rather than squeezing the session, when it cannot fit beside it', async () => {
    // D118: the list gives way to its strip first, so it is past that, at 800 px, that the side bar is cramped.
    widen(800);
    show('s1a2b3c4');
    expect(await screen.findByText(/too narrow to sit beside the session/)).toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: 'Close the side bar' }));
    expect(screen.queryByText(/too narrow to sit beside the session/)).toBeNull();
  });
});
