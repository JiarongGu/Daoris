import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';

// The window's own chrome (SURF7): the page half, which is the half that CAN be tested. `MainForm`
// is WinForms and has no test project by construction — `npm run desktop -- shot` is its gate — so
// everything that could live here does, and what is left there is wiring with no branches in it.

const { invoke } = vi.hoisted(() => ({ invoke: vi.fn() }));

vi.mock('@shenora/react', async () => {
  const actual = await vi.importActual<typeof import('@shenora/react')>('@shenora/react');
  return {
    ...actual,
    isShenoraAvailable: () => true,
    getBridge: () => ({ isAvailable: true, invoke, notifyReady: () => Promise.resolve() }),
    useShenora: () => ({ isAvailable: true, bridge: { notifyReady: () => Promise.resolve() } }),
  };
});

import './i18n';
import { CAPTION_ATTRIBUTE, CAPTION_SLOTS, captionRects } from './windowChrome';
import { AppStrip } from './work/frame';

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

    await userEvent.pointer({ keys: '[MouseLeft>]', target: screen.getByRole('banner') });
    expect(onDragStart).toHaveBeenCalledTimes(1);

    await userEvent.pointer({ keys: '[MouseLeft>]', target: screen.getByRole('button', { name: 'app menu' }) });
    expect(onDragStart).toHaveBeenCalledTimes(1);
  });

  it('ignores a press that is not the primary button — a right-click is a menu, not a drag', async () => {
    const { onDragStart } = show();
    await userEvent.pointer({ keys: '[MouseRight>]', target: screen.getByRole('banner') });
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
