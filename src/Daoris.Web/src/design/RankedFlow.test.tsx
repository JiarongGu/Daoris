import { afterEach, describe, expect, it, vi } from 'vitest';
import { act, cleanup, fireEvent, render, screen } from '@testing-library/react';
import * as Tooltip from '@radix-ui/react-tooltip';
import i18n from '../locales/i18n';
import type { CanvasOptions } from './canvas/Canvas';
import { type FlowCanvasProps, type FlowEdge, type FlowNode, FlowCanvas } from './FlowCanvas';

// CANVAS1a (D224; the canvas design §2.6, §3.1, §6, trap 12): the ranked flow in the canvas's viewport. A caller opts in; from 36rem of the
// pane a drawing wider than its pane is drawn top to bottom and pans rather than turning to the rail, below it the placement is today's,
// and a rem is the root's as measured, never 16 px under the window's zoom. jsdom lays nothing out, so the pane's width is stood in for.

const NODES: FlowNode[] = ['a', 'b', 'c', 'd', 'e'].map((id) => ({ id, title: id.toUpperCase() }));
// A rank of three below a rank of one: 3 × 10 + 2 × 1.5 = 33rem top to bottom, more than three ranks' 36rem left to right.
const EDGES: FlowEdge[] = [
  { id: 'ab', from: 'a', to: 'b', says: 'then' }, { id: 'ac', from: 'a', to: 'c', says: 'then' },
  { id: 'ad', from: 'a', to: 'd', says: 'then' }, { id: 'be', from: 'b', to: 'e', says: 'then' },
];
const VIEWPORT: CanvasOptions = { mode: 'edit', zoom: { min: 0.5, max: 2 }, fit: 'Show the whole flow', moves: 'a flow: drag to move' };

afterEach(async () => {
  vi.restoreAllMocks();
  vi.unstubAllGlobals();
  document.documentElement.style.removeProperty('font-size');
  await i18n.changeLanguage('en');
});

/** The flow drawn in a pane `width` px wide. */
function draw(width: number, props: Partial<FlowCanvasProps> = {}) {
  cleanup();
  vi.restoreAllMocks();
  vi.spyOn(HTMLElement.prototype, 'getBoundingClientRect').mockImplementation(function (this: HTMLElement) {
    const wide = this.hasAttribute('data-flow-pane') ? width : 0;
    return { width: wide, height: 0, top: 0, left: 0, right: wide, bottom: 0, x: 0, y: 0, toJSON: () => ({}) } as DOMRect;
  });
  vi.stubGlobal('ResizeObserver', class { observe() {} unobserve() {} disconnect() {} });
  return render(
    <Tooltip.Provider>
      <FlowCanvas
        label="How it moves"
        nodes={NODES}
        edges={EDGES}
        entries={['a']}
        renderNode={({ node, stop }) => <div role="listitem" data-flow-node={node.id} tabIndex={stop ? 0 : -1}>{node.title}</div>}
        {...props}
      />
    </Tooltip.Provider>,
  );
}

const placement = () => document.querySelector('[data-flow-pane]')!.getAttribute('data-placement');

describe('a flow not given a viewport', () => {
  it('is placed as it always was, with no region', () => {
    draw(800);
    expect(placement()).toBe('across');
    expect(screen.queryByRole('region')).toBeNull();
    draw(400);
    expect(screen.queryByRole('region')).toBeNull();
  });
});

describe('a flow given a viewport', () => {
  it('stands in a named region from 36rem, its world at 100%, the list offered in the toolbar beside the size', () => {
    draw(800, { viewport: VIEWPORT });
    const region = screen.getByRole('region', { name: 'How it moves' });
    expect(region).toHaveAttribute('aria-roledescription', 'a flow: drag to move');
    expect(region).toHaveAttribute('data-placement', 'across');
    expect(region.querySelector('[data-canvas-world]')).toHaveAttribute('data-canvas-scale', '1');
    expect(screen.getByRole('button', { name: 'Show as list' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Sizing: 100%' })).toBeInTheDocument();
  });

  it('is drawn top to bottom and pans where the pane cannot hold it, rather than turning to the rail', () => {
    // 576 px is 36rem, and the rank of three needs 33rem: drawn. The same flow needing more, at the floor, is drawn and pans.
    draw(576, { viewport: VIEWPORT, track: { least: 14 } });
    expect(screen.getByRole('region')).toHaveAttribute('data-placement', 'down');
  });

  it('keeps today\'s placement below the floor: drawn where it fits, else the rail', () => {
    draw(560, { viewport: VIEWPORT });
    expect(screen.queryByRole('region')).toBeNull();
    expect(placement()).toBe('down');
    draw(500, { viewport: VIEWPORT });
    expect(placement()).toBe('rail');
  });

  it('walks the focus without scrolling anything, since the canvas reveals the node (trap 2)', () => {
    draw(800, { viewport: VIEWPORT });
    const focus = vi.spyOn(HTMLElement.prototype, 'focus');
    const first = document.querySelector<HTMLElement>('[data-flow-node="a"]')!;
    act(() => first.focus());
    focus.mockClear();
    fireEvent.keyDown(first, { key: 'ArrowDown' });
    expect(focus).toHaveBeenCalledWith({ preventScroll: true });
  });
});

describe('a rem', () => {
  it('is the root\'s as measured, so the window\'s zoom places a flow by what it needs (trap 12)', () => {
    // 33rem at 16 px is 528 px and fits 560; at the window's 200% a rem is 32 px, and 33rem is 1056 px.
    draw(560);
    expect(placement()).toBe('down');
    document.documentElement.style.fontSize = '32px';
    draw(560);
    expect(placement()).toBe('rail');
  });
});
