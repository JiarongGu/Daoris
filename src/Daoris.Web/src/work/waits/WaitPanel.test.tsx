import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { act, fireEvent, render, screen, within } from '@testing-library/react';
import { useState } from 'react';
import i18n from '../../locales/i18n';
import { BackToBottom, ToTheTop } from '../conversation/SessionConversation';
import { WaitPanel, WAIT_PART } from './WaitPanel';
import { keptWaitPanel, useWaitPanelHeight, WAIT_PANEL, WAIT_PANEL_KEY, waitPanelMax } from './waitPanelHeight';

/**
 * WAITPANEL1: what waits on the person at the foot of a session's conversation was too tall and could not be resized (the owner's
 * report, 2026-10-23). Its top edge sizes it now, the height kept per viewer, between a floor and half the window; and the
 * conversation's *To the top* and *Back to bottom* lie above that edge, never under the panel.
 */

/** The panel as the frame holds it: its height kept by the hook, a part that waits, and the conversation's presses. */
function Held({ part = true, onTop = () => {} }: { part?: boolean; onTop?: () => void }) {
  const panel = useWaitPanelHeight();
  return (
    <WaitPanel
      height={panel.height}
      onResize={panel.resize}
      onReset={panel.reset}
      controls={<><ToTheTop onPress={onTop} /><BackToBottom onPress={() => {}} /></>}
    >
      {part && <section {...{ [WAIT_PART]: '' }} aria-label="Waiting on you">a go-ahead</section>}
      <form aria-label="the box"><textarea aria-label="Message" /></form>
    </WaitPanel>
  );
}

const handle = () => screen.getByRole('separator', { name: 'Height of what waits on you' });
const now = () => Number(handle().getAttribute('aria-valuenow'));

/** A drag of the top edge by `by` px upward (a negative `by` drags it down). */
const drag = (by: number) => {
  fireEvent.pointerDown(handle(), { clientY: 500, button: 0 });
  fireEvent.pointerMove(window, { clientY: 500 - by });
  fireEvent.pointerUp(window);
};

beforeEach(() => {
  window.localStorage.clear();
  // A window of 800 px: the waits take at most 400.
  vi.spyOn(window, 'innerHeight', 'get').mockReturnValue(800);
});

afterEach(async () => {
  vi.restoreAllMocks();
  await i18n.changeLanguage('en');
});

describe('the waiting panel', () => {
  it('starts at its initial height, its top edge a keyboard-reachable handle named in both languages', async () => {
    render(<Held />);
    expect(now()).toBe(WAIT_PANEL.initial);
    expect(handle()).toHaveAttribute('aria-orientation', 'horizontal');
    expect(handle()).toHaveAttribute('tabindex', '0');
    expect(handle()).toHaveAttribute('aria-valuemin', String(WAIT_PANEL.min));
    expect(handle()).toHaveAttribute('aria-valuemax', '400');

    await act(async () => { await i18n.changeLanguage('zh'); });
    expect(screen.getByRole('separator', { name: '等你处理区域的高度' })).toBeInTheDocument();
    expect(screen.getByRole('group', { name: '在对话中移动' })).toBeInTheDocument();
  });

  it('follows a drag of its top edge, and keeps the height for this viewer when it is drawn again', () => {
    const { unmount } = render(<Held />);
    drag(100);
    expect(now()).toBe(340);
    // The drag ended: the pointer moving on moves nothing.
    fireEvent.pointerMove(window, { clientY: 100 });
    expect(now()).toBe(340);
    expect(window.localStorage.getItem(WAIT_PANEL_KEY)).toBe('340');

    unmount();
    render(<Held />);
    expect(now()).toBe(340);
  });

  it('stops at its floor and at half the window, by drag and by key, and a double-click forgets what was kept', () => {
    render(<Held />);
    drag(-1000);
    expect(now()).toBe(WAIT_PANEL.min);
    drag(1000);
    expect(now()).toBe(400);

    fireEvent.keyDown(handle(), { key: 'ArrowDown' });
    expect(now()).toBe(400 - WAIT_PANEL.step);
    fireEvent.keyDown(handle(), { key: 'ArrowUp' });
    expect(now()).toBe(400);
    fireEvent.keyDown(handle(), { key: 'End' });
    expect(now()).toBe(WAIT_PANEL.min);
    fireEvent.keyDown(handle(), { key: 'Home' });
    expect(now()).toBe(400);
    expect(window.localStorage.getItem(WAIT_PANEL_KEY)).toBe('400');

    fireEvent.doubleClick(handle());
    expect(now()).toBe(WAIT_PANEL.initial);
    expect(window.localStorage.getItem(WAIT_PANEL_KEY)).toBeNull();
  });

  it('holds a height kept in a taller window at half of this one, where no column is laid out to read', () => {
    window.localStorage.setItem(WAIT_PANEL_KEY, '700');
    render(<Held />);
    expect(now()).toBe(400);
    expect(waitPanelMax(100)).toBe(WAIT_PANEL.min);
  });

  it('is drawn at its initial height where the store is missing or refuses, and still sizes', () => {
    vi.spyOn(Storage.prototype, 'getItem').mockImplementation(() => { throw new Error('refused'); });
    vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => { throw new Error('refused'); });
    expect(keptWaitPanel()).toBe(WAIT_PANEL.initial);
    render(<Held />);
    expect(now()).toBe(WAIT_PANEL.initial);
    drag(60);
    expect(now()).toBe(300);
  });

  it('reads what is no height as none kept', () => {
    window.localStorage.setItem(WAIT_PANEL_KEY, 'tall');
    expect(keptWaitPanel()).toBe(WAIT_PANEL.initial);
    window.localStorage.setItem(WAIT_PANEL_KEY, '10');
    expect(keptWaitPanel()).toBe(WAIT_PANEL.initial);
  });

  it('draws no handle while nothing waits, and one once a part is drawn or stops standing aside', async () => {
    function Coming() {
      const [shown, setShown] = useState<'none' | 'aside' | 'up'>('none');
      return (
        <>
          <button type="button" onClick={() => setShown(shown === 'none' ? 'aside' : 'up')}>next</button>
          <WaitPanel height={240} onResize={() => {}}>
            {shown !== 'none' && <section {...{ [WAIT_PART]: '' }} hidden={shown === 'aside'}>the strip</section>}
          </WaitPanel>
        </>
      );
    }
    render(<Coming />);
    expect(screen.queryByRole('separator')).toBeNull();
    // A strip standing aside (WAITFOOT2a) takes none of the height.
    fireEvent.click(screen.getByRole('button', { name: 'next' }));
    await act(async () => {});
    expect(screen.queryByRole('separator')).toBeNull();
    fireEvent.click(screen.getByRole('button', { name: 'next' }));
    await act(async () => {});
    expect(screen.getByRole('separator')).toBeInTheDocument();
  });

  it('gives each part drawn its share of the height', async () => {
    const { container, rerender } = render(
      <WaitPanel height={300} onResize={() => {}}><section {...{ [WAIT_PART]: '' }}>one</section></WaitPanel>,
    );
    const panel = container.querySelector<HTMLElement>('[data-region="wait-panel"]')!;
    expect(panel.style.getPropertyValue('--wait-part')).toBe('300px');
    rerender(
      <WaitPanel height={300} onResize={() => {}}>
        <section {...{ [WAIT_PART]: '' }}>one</section>
        <section {...{ [WAIT_PART]: '' }}>two</section>
      </WaitPanel>,
    );
    await act(async () => {});
    expect(panel.style.getPropertyValue('--wait-part')).toBe('150px');
  });

  it('leaves the conversation its floor in the column, whatever the window: the waits take what is left of their shared room', () => {
    // The column as laid out: a main area of 300 px over a part of 100, so the two share 400 and the waits take at most 160.
    vi.spyOn(HTMLElement.prototype, 'getBoundingClientRect').mockImplementation(function (this: HTMLElement) {
      const height = this.getAttribute('data-region') === 'main' ? 300 : this.hasAttribute(WAIT_PART) ? 100 : 0;
      return { height, width: 0, top: 0, left: 0, right: 0, bottom: height, x: 0, y: 0, toJSON: () => ({}) } as DOMRect;
    });
    window.localStorage.setItem(WAIT_PANEL_KEY, '300');
    render(
      <div>
        <main data-region="main">the conversation</main>
        <Held />
      </div>,
    );
    expect(handle()).toHaveAttribute('aria-valuemax', String(400 - WAIT_PANEL.conversation));
    expect(now()).toBe(400 - WAIT_PANEL.conversation);
    // A drag starts from what the parts take as drawn (100 px), not from the height kept.
    fireEvent.keyDown(handle(), { key: 'ArrowUp' });
    expect(now()).toBe(100 + WAIT_PANEL.step);
    expect(waitPanelMax(800, 200)).toBe(WAIT_PANEL.min);
  });

  /**
   * The owner's second report: the conversation's scroll presses sat under the panel. They are the panel's own now, laid over
   * the conversation's foot above its top edge, in a layer over the handle and over the conversation's sticky lines (z-[9]).
   * jsdom lays nothing out, so this holds the arrangement; `measured/waitPanel.spec.ts` measures it in a real browser.
   */
  it('lays the conversation\'s presses above its top edge, over the handle and outside every part', () => {
    const onTop = vi.fn();
    const { container } = render(<Held onTop={onTop} />);
    const panel = container.querySelector('[data-region="wait-panel"]')!;
    const presses = screen.getByRole('group', { name: 'Move through the conversation' });

    expect(presses.parentElement).toBe(panel);
    expect(presses.closest(`[${WAIT_PART}]`)).toBeNull();
    expect(presses.className).toMatch(/\babsolute\b/);
    expect(presses.className).toMatch(/\bbottom-full\b/);
    const layer = (element: Element) => Number(/\bz-\[?(\d+)\]?/.exec(element.className)?.[1] ?? 0);
    expect(layer(presses)).toBeGreaterThan(layer(handle()));
    // Over the conversation's sticky lines: the find line and the record's pinned head are z-[9].
    expect(layer(presses)).toBeGreaterThan(9);
    // Drawn before the handle and the parts, so the edge is never between the person and a press.
    expect(panel.firstElementChild).toBe(presses);

    fireEvent.click(within(presses).getByRole('button', { name: 'To the top' }));
    expect(onTop).toHaveBeenCalledOnce();
    expect(within(presses).getByRole('button', { name: 'Back to bottom' })).toBeInTheDocument();
  });

  it('draws no row of presses where the conversation offers none', () => {
    render(<WaitPanel height={240} onResize={() => {}} />);
    expect(screen.queryByRole('group')).toBeNull();
  });
});
