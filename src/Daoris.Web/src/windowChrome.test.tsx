import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { cleanup, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';

// The window's own chrome (SURF7): the page half, which is the half that CAN be tested. `MainForm`
// is WinForms and has no test project by construction — `npm run desktop -- shot` is its gate — so
// everything that could live here does, and what is left there is wiring with no branches in it.

const { invoke, commands, window_ } = vi.hoisted(() => {
  // Every gesture command the strip can send, logged in the order sent, so a test can say "restored,
  // then dragged" and "sent nothing" over the same list.
  const sent: string[] = [];
  const command = (name: string) => vi.fn(() => {
    sent.push(name);
    return Promise.resolve();
  });
  return {
    invoke: vi.fn(),
    commands: {
      sent,
      toggleMaximize: command('toggleMaximize'),
      startDrag: command('startDrag'),
      startResize: command('startResize'),
      showSystemMenu: command('showSystemMenu'),
    },
    window_: { maximized: false },
  };
});

vi.mock('@shenora/react', async () => {
  const actual = await vi.importActual<typeof import('@shenora/react')>('@shenora/react');
  class WindowCommands {
    toggleMaximize = commands.toggleMaximize;
    startDrag = commands.startDrag;
    startResize = commands.startResize;
    showSystemMenu = commands.showSystemMenu;
    setCaptionButtons = () => Promise.resolve();
    setTheme = () => Promise.resolve();
  }
  return {
    ...actual,
    WindowCommands,
    isShenoraAvailable: () => true,
    getBridge: () => ({ isAvailable: true, invoke, notifyReady: () => Promise.resolve() }),
    useShenora: () => ({ isAvailable: true, bridge: { notifyReady: () => Promise.resolve() } }),
    useWindowMaximized: () => window_.maximized,
  };
});

import './i18n';
import { CAPTION_ATTRIBUTE, CAPTION_SLOTS, captionRects, useWindowChrome } from './windowChrome';
import { AppStrip, DRAG_DISTANCE, STRIP_SPACE } from './work/frame';

/** A strip with three laid-out slots, as the real one has once the browser has measured it. */
function strip(sizes: Partial<Record<string, DOMRect>> = {}): HTMLElement {
  const host = document.createElement('header');
  CAPTION_SLOTS.forEach((kind, index) => {
    const slot = document.createElement('div');
    slot.setAttribute(CAPTION_ATTRIBUTE, kind);
    const rect = sizes[kind] ?? ({ x: 1000 + index * 44, y: 0, width: 44, height: 36 } as DOMRect);
    slot.getBoundingClientRect = () => rect;
    host.appendChild(slot);
  });
  return host;
}

describe('captionRects', () => {
  it('reports one rectangle per slot, in the order Windows puts them', () => {
    expect(captionRects(strip())).toEqual([
      { kind: 'minimize', x: 1000, y: 0, width: 44, height: 36 },
      { kind: 'maximize', x: 1044, y: 0, width: 44, height: 36 },
      { kind: 'close', x: 1088, y: 0, width: 44, height: 36 },
    ]);
  });

  /**
   * An unlaid-out or hidden slot measures 0x0. Reporting it would hand the OS a degenerate region,
   * and the host skips those — but a rectangle nobody sends cannot be mis-skipped later either.
   */
  it('drops a slot that has not been laid out rather than reporting a zero rectangle', () => {
    const rects = captionRects(strip({ maximize: { x: 0, y: 0, width: 0, height: 0 } as DOMRect }));
    expect(rects.map((rect) => rect.kind)).toEqual(['minimize', 'close']);
  });

  it('has nothing to say about a strip that is not there — a browser, before any window', () => {
    expect(captionRects(null)).toEqual([]);
    expect(captionRects(document.createElement('header'))).toEqual([]);
  });
});

describe('the app strip as a title bar', () => {
  beforeEach(() => invoke.mockResolvedValue({}));
  afterEach(() => invoke.mockReset());

  const show = (props: Partial<Parameters<typeof AppStrip>[0]> = {}) => {
    const handlers = {
      onDragStart: vi.fn(), onToggleMaximize: vi.fn(), onResizeTop: vi.fn(),
    };
    render(
      <Tooltip.Provider>
        <AppStrip
          menus={<button type="button">app menu</button>}
          captionRoom
          {...handlers}
          {...props}
        />
      </Tooltip.Provider>,
    );
    return handlers;
  };

  it('reserves the three slots and draws nothing in them — the window owns those pixels', () => {
    show();
    const slots = document.querySelectorAll(`[${CAPTION_ATTRIBUTE}]`);
    expect([...slots].map((slot) => slot.getAttribute(CAPTION_ATTRIBUTE)))
      .toEqual(['minimize', 'maximize', 'close']);
    // Empty by design: a glyph here would be a second, unclickable copy of what the window paints.
    expect([...slots].every((slot) => slot.textContent === '')).toBe(true);
  });

  it('reserves nothing in a browser, where there is no window to give the pixels to', () => {
    render(
      <Tooltip.Provider>
        <AppStrip />
      </Tooltip.Provider>,
    );
    expect(document.querySelectorAll(`[${CAPTION_ATTRIBUTE}]`)).toHaveLength(0);
  });

  /**
   * The whole strip is a drag handle EXCEPT its controls. Without the target check every press on a
   * menu would start an OS move loop, and the menu would stop being a button.
   */
  it('drags from the strip itself and never from a control on it', async () => {
    const { onDragStart } = show();

    await press(screen.getByRole('banner'), { x: DRAG_DISTANCE + 1, y: 0 });
    expect(onDragStart).toHaveBeenCalledTimes(1);

    await press(screen.getByRole('button', { name: 'app menu' }), { x: 40, y: 40 });
    expect(onDragStart).toHaveBeenCalledTimes(1);
  });

  /**
   * The strip's groups lay it out, and the space they leave empty is still the strip's: a press
   * there drags, as a press on the strip itself does.
   */
  it('drags from the space its groups leave empty, as from the strip itself', async () => {
    const { onDragStart } = show({ center: <button type="button">command center</button> });
    const spaces = [...document.querySelectorAll<HTMLElement>(`[${STRIP_SPACE}]`)];
    expect(spaces).toHaveLength(3);

    for (const space of spaces) await press(space, { x: 0, y: DRAG_DISTANCE + 1 });
    expect(onDragStart).toHaveBeenCalledTimes(3);
  });

  /**
   * 🔴 FRAME2: a press used to hand off to the OS move loop at once, so a still click on a maximized
   * window restored it, and the loop swallowed a double-click's second press. A caption's press only
   * notes where it went down; the drag begins once the pointer has travelled past the drag distance.
   */
  it('starts no drag on a still click, nor on a press that stays within the drag distance', async () => {
    const { onDragStart } = show();

    await press(screen.getByRole('banner'));
    await press(screen.getByRole('banner'), { x: DRAG_DISTANCE, y: -DRAG_DISTANCE });
    expect(onDragStart).not.toHaveBeenCalled();
  });

  it('forgets the press once the button is up: a later move with no button down drags nothing', async () => {
    const { onDragStart } = show();
    const user = userEvent.setup();

    await user.pointer([
      { keys: '[MouseLeft>]', target: screen.getByRole('banner'), coords: { x: 100, y: 10 } },
      { keys: '[/MouseLeft]' },
      { coords: { x: 300, y: 200 } },
    ]);
    expect(onDragStart).not.toHaveBeenCalled();
  });

  /**
   * 🔴 UX5 U15: the command center was laid OVER the strip, centred on its whole width at a fixed
   * 28rem, and at a narrow window (888px) it ran over the View menu. It sits in the strip's flow now,
   * between two groups that grow alike from nothing: centred on the strip while both fit beside it,
   * and giving way to them when they do not, as VS Code's does. The geometry is the window's to show
   * (`npm run desktop -- shot`); this holds the arrangement that produces it.
   */
  it('lays the command center between the menus and the scope, never over them', () => {
    show({ center: <button type="button">command center</button>, scope: <span>every circle</span> });
    const [start, center, end] = [...document.querySelectorAll<HTMLElement>(`[${STRIP_SPACE}]`)];

    expect([start, center, end].map((group) => group.getAttribute(STRIP_SPACE))).toEqual(['start', 'center', 'end']);
    expect(within(start).getByRole('button', { name: 'app menu' })).toBeInTheDocument();
    expect(within(center).getByRole('button', { name: 'command center' })).toBeInTheDocument();
    expect(within(end).getByText('every circle')).toBeInTheDocument();
    expect(end.querySelectorAll(`[${CAPTION_ATTRIBUTE}]`)).toHaveLength(3);

    // The two sides grow alike from nothing, which is what centres the middle on the strip; the
    // middle never grows and may shrink below its pill, which is what makes it the one that yields.
    for (const side of [start, end]) expect(side).toHaveClass('flex-1', 'basis-0');
    // 🔴 And neither the strip nor a group carries padding: the sides share the free space by their
    // content boxes, so 12px of padding put the middle 12px off centre on the window. The mark's
    // inset is the mark's own.
    for (const box of [screen.getByRole('banner'), start, center, end]) {
      expect(box.className).not.toMatch(/(^|\s)p[lrx]?-/);
    }
    expect(center).toHaveClass('min-w-0', 'basis-md');
    expect(center).not.toHaveClass('flex-1');

    // 🔴 One line, by rule. A Chinese label may break between any two characters, so once the sides
    // shared the strip, 中文's menus wrapped onto two lines each (道 / 衍) at 888px, on the window.
    expect(screen.getByRole('banner')).toHaveClass('whitespace-nowrap');

    // Nothing is laid over the strip but the resize sliver, which is 4px at its very top.
    const over = [...screen.getByRole('banner').querySelectorAll('.absolute')];
    expect(over).toEqual([document.querySelector('.cursor-ns-resize')]);
  });

  it('ignores a press that is not the primary button — a right-click is a menu, not a drag', async () => {
    const { onDragStart } = show();
    await userEvent.setup().pointer([
      { keys: '[MouseRight>]', target: screen.getByRole('banner'), coords: { x: 100, y: 10 } },
      { coords: { x: 160, y: 60 } },
      { keys: '[/MouseRight]' },
    ]);
    expect(onDragStart).not.toHaveBeenCalled();
  });

  it('maximizes on a double-click of the bar, as a title bar does', async () => {
    const { onToggleMaximize } = show();
    await userEvent.dblClick(screen.getByRole('banner'));
    expect(onToggleMaximize).toHaveBeenCalledTimes(1);
  });

  it('offers a top resize strip only where there is a window to resize', () => {
    const { container } = render(
      <Tooltip.Provider>
        <AppStrip />
      </Tooltip.Provider>,
    );
    expect(container.querySelector('.cursor-ns-resize')).toBeNull();

    show();
    expect(document.querySelector('.cursor-ns-resize')).toBeTruthy();
  });
});

/**
 * FRAME2: the strip wired to its window as the application wires it, over a mocked `WindowCommands`, so
 * each gesture is read as the commands the window would receive. A title bar's still click does
 * nothing, its drag moves the window (restoring a maximized one first), and its double-click toggles
 * maximize.
 */
describe('the strip wired to the window, gesture by gesture', () => {
  beforeEach(() => {
    invoke.mockResolvedValue({});
    commands.sent.length = 0;
  });
  afterEach(() => {
    invoke.mockReset();
    window_.maximized = false;
  });

  /** The strip as `App` binds it: every handler from `useWindowChrome`, the menus a control on it. */
  function Wired() {
    const chrome = useWindowChrome();
    return (
      <AppStrip
        menus={<button type="button">app menu</button>}
        captionRoom={chrome.present}
        stripRef={chrome.stripRef}
        onDragStart={chrome.onDragStart}
        onToggleMaximize={chrome.onToggleMaximize}
        onResizeTop={chrome.onResizeTop}
        onSystemMenu={chrome.onSystemMenu}
      />
    );
  }

  const wire = ({ maximized }: { maximized: boolean }) => {
    window_.maximized = maximized;
    render(
      <Tooltip.Provider>
        <Wired />
      </Tooltip.Provider>,
    );
    return screen.getByRole('banner');
  };

  it('sends nothing for a still click on a maximized window — it stays maximized', async () => {
    await press(wire({ maximized: true }));
    // Long enough for a fire-and-forget command to have landed, had one been sent.
    await new Promise((resolve) => setTimeout(resolve, 20));
    expect(commands.sent).toEqual([]);
  });

  it('restores a maximized window, then drags it, once the press travels past the drag distance', async () => {
    await press(wire({ maximized: true }), { x: 0, y: DRAG_DISTANCE + 6 });
    await waitFor(() => expect(commands.sent).toEqual(['toggleMaximize', 'startDrag']));
  });

  it('drags a restored window without touching its maximize', async () => {
    await press(wire({ maximized: false }), { x: DRAG_DISTANCE + 1, y: 0 });
    await waitFor(() => expect(commands.sent).toEqual(['startDrag']));
  });

  it('toggles maximize once on a double-click, and starts no drag', async () => {
    for (const maximized of [false, true]) {
      commands.sent.length = 0;
      await userEvent.dblClick(wire({ maximized }));
      await waitFor(() => expect(commands.sent).toEqual(['toggleMaximize']));
      cleanup();
    }
  });

  it('sends nothing for a press or a drag on a control inside the strip', async () => {
    wire({ maximized: true });
    const menu = screen.getByRole('button', { name: 'app menu' });

    await press(menu);
    await press(menu, { x: 60, y: 30 });
    await userEvent.dblClick(menu);
    await new Promise((resolve) => setTimeout(resolve, 20));
    expect(commands.sent).toEqual([]);
  });

  it('keeps the right-click system menu and the top resize sliver as they were', async () => {
    const strip = wire({ maximized: true });

    await userEvent.pointer({ keys: '[MouseRight]', target: strip });
    await userEvent.pointer({ keys: '[MouseLeft>]', target: strip.querySelector('.cursor-ns-resize')! });
    await waitFor(() => expect(commands.sent).toEqual(['showSystemMenu', 'startResize']));
  });
});

/**
 * A primary press on `target` at a fixed point, a move of `travel` with the button held, and the release,
 * as one pointer, so the move is seen with the button down.
 */
async function press(target: Element, travel: { x: number; y: number } = { x: 0, y: 0 }) {
  await userEvent.setup().pointer([
    { keys: '[MouseLeft>]', target, coords: { x: 100, y: 10 } },
    { coords: { x: 100 + travel.x, y: 10 + travel.y } },
    { keys: '[/MouseLeft]' },
  ]);
}
