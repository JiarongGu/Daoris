import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { act, fireEvent, render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import i18n from '../locales/i18n';
import { Flow, type FlowEdge, type FlowNode } from './Flow';

// FLOWCHART1a1 (D186 points 3 and 8; the flowchart design §3.2, §3.3, §3.7 and §7 traps 4-7, 9, 14, 15, 21): one list in
// reading order whatever the placement, each node named by its title and state and described by every edge in and out;
// one Tab stop and its keys; *Show as list* remembered; the drawing hidden from a reader. jsdom lays nothing out, so the
// pane's width is stood in for here, and the edges' geometry is the measured-box check's, in a rendered browser.

const NODES: FlowNode[] = [
  { id: 'Open', title: 'Open', reached: true },
  { id: 'Taken', title: 'Taken', current: true, state: { word: 'here now', tone: 'taken' } },
  { id: 'Done', title: 'Done', reached: false },
  { id: 'Declined', title: 'Declined', reached: false },
];

const EDGES: FlowEdge[] = [
  { id: 'take', from: 'Open', to: 'Taken', says: 'when an agent takes it' },
  { id: 'doneOpen', from: 'Open', to: 'Done', says: 'when it is marked done first' },
  { id: 'declineOpen', from: 'Open', to: 'Declined', says: 'when it is declined first' },
  { id: 'done', from: 'Taken', to: 'Done', says: 'when its work is done' },
  { id: 'decline', from: 'Taken', to: 'Declined', says: 'when it is declined' },
];

const LIST_KEY = 'daoris.flow.list';

/** The pane's width as a rendered browser would measure it, and a resize the page's observer hears. */
let paneWidth: number | undefined;
const observers = new Set<ResizeObserverCallback>();
const resizeTo = (width: number) => act(() => {
  paneWidth = width;
  for (const callback of observers) callback([{ contentRect: { width } } as ResizeObserverEntry], {} as ResizeObserver);
});

beforeEach(() => {
  paneWidth = undefined;
  observers.clear();
  localStorage.removeItem(LIST_KEY);
  vi.spyOn(HTMLElement.prototype, 'getBoundingClientRect').mockImplementation(function (this: HTMLElement) {
    const width = this.hasAttribute('data-flow-pane') ? paneWidth ?? 0 : 0;
    return { width, height: 0, top: 0, left: 0, right: width, bottom: 0, x: 0, y: 0, toJSON: () => ({}) } as DOMRect;
  });
  vi.stubGlobal('ResizeObserver', class {
    private readonly callback: ResizeObserverCallback;
    constructor(callback: ResizeObserverCallback) { this.callback = callback; observers.add(callback); }
    observe() {}
    unobserve() {}
    disconnect() { observers.delete(this.callback); }
  });
});

afterEach(async () => {
  vi.restoreAllMocks();
  vi.unstubAllGlobals();
  await i18n.changeLanguage('en');
});

const draw = (props: Partial<Parameters<typeof Flow>[0]> = {}) =>
  render(<Flow label="How #q1 moves" nodes={NODES} edges={EDGES} entries={['Open']} {...props} />);

const list = () => screen.getByRole('list', { name: 'How #q1 moves' });
const items = () => within(list()).getAllByRole('listitem');
const node = (name: string) => within(list()).getByRole('listitem', { name: new RegExp(`^${name}\\b`) });
const placement = (container: HTMLElement) => container.querySelector('[data-flow-pane]')!.getAttribute('data-placement');

describe('one list in reading order (§3.7, trap 5)', () => {
  it('is the rail where nothing measured the pane, named for the flow, its nodes in rank order, and no drawing', () => {
    const { container } = draw();
    expect(placement(container)).toBe('rail');
    expect(items().map((item) => item.getAttribute('data-flow-node'))).toEqual(['Open', 'Taken', 'Done', 'Declined']);
    expect(container.querySelector('svg[data-flow-ink]')).toBeNull();
  });

  it('names each node by its title and its state\'s word, and marks the current one', () => {
    draw();
    expect(node('Taken')).toHaveAccessibleName('Taken here now');
    expect(node('Taken')).toHaveAttribute('aria-current', 'step');
    expect(node('Open')).not.toHaveAttribute('aria-current');
  });

  it('tells a reader the keys once, beside the list\'s name', () => {
    draw();
    expect(list()).toHaveAccessibleDescription('Arrow keys move between steps, Enter opens one, Tab reaches its controls.');
  });

  it('keeps the same items in the same order at every placement, and hides the drawing from a reader', () => {
    paneWidth = 1200;
    const { container } = draw();
    expect(placement(container)).toBe('across');
    expect(items().map((item) => item.getAttribute('data-flow-node'))).toEqual(['Open', 'Taken', 'Done', 'Declined']);
    const ink = container.querySelector('svg[data-flow-ink]')!;
    expect(ink).toHaveAttribute('aria-hidden', 'true');
    // Nothing is drawn smaller to fit (trap 15): no viewBox over the drawing, and no scale anywhere.
    expect(ink).not.toHaveAttribute('viewBox');
    expect(container.innerHTML).not.toMatch(/scale\(/);
  });
});

describe('a reader follows every edge (trap 6)', () => {
  it('describes each node by its state and every edge in and out, in words', () => {
    draw();
    expect(node('Taken')).toHaveAccessibleDescription(
      'here now. From Open: when an agent takes it. Next: Done, when its work is done; Declined, when it is declined.');
    expect(node('Done')).toHaveAccessibleDescription(
      'Not reached. From Open: when it is marked done first. From Taken: when its work is done.');
  });

  it('says a return as a way back, in the foot and the description', () => {
    draw({
      nodes: [{ id: 'working', title: 'Working' }, { id: 'waiting', title: 'Waiting on you' }],
      edges: [
        { id: 'ask', from: 'working', to: 'waiting', says: 'when it asks you' },
        { id: 'answer', from: 'waiting', to: 'working', says: 'when you answer' },
      ],
      entries: ['working'],
    });
    const waiting = node('Waiting on you');
    expect(waiting).toHaveAccessibleDescription('From Working: when it asks you. Back to Working, when you answer.');
    expect(within(waiting).getByRole('button', { name: /back to Working, when you answer/ })).toBeInTheDocument();
  });

  it('says them in 中文, the sentences joined the catalogue\'s way', async () => {
    await i18n.changeLanguage('zh');
    draw();
    expect(list()).toHaveAccessibleDescription('方向键在步骤之间移动，Enter 打开一步，Tab 进入它的控件。');
    expect(node('Taken')).toHaveAccessibleDescription(
      'here now。来自 Open：when an agent takes it。下一步：Done，when its work is done；Declined，when it is declined。');
    expect(node('Done')).toHaveAccessibleDescription(
      '尚未到达。来自 Open：when it is marked done first。来自 Taken：when its work is done。');
  });
});

describe('a node (§3.3, trap 9)', () => {
  it('says each edge out in its foot, a press that moves the focus to its target', async () => {
    draw();
    const open = node('Open');
    const feet = within(open).getAllByRole('button').map((button) => button.textContent);
    expect(feet).toEqual(['→Taken, when an agent takes it', '→Done, when it is marked done first', '→Declined, when it is declined first']);
    await userEvent.click(within(open).getByRole('button', { name: /Declined, when it is declined first/ }));
    expect(node('Declined')).toHaveFocus();
  });

  it('draws a node not reached with a dashed border and its words at a token ink, never faint by opacity', () => {
    const { container } = draw();
    expect(node('Done').className).toMatch(/border-dashed/);
    expect(node('Taken').className).not.toMatch(/border-dashed/);
    expect(container.innerHTML).not.toMatch(/opacity-/);
  });

  it('shows the caller\'s own lines inside the node', () => {
    draw({ nodes: NODES.map((each) => (each.id === 'Taken' ? { ...each, lines: <p>held by laptop</p> } : each)) });
    expect(within(node('Taken')).getByText('held by laptop')).toBeInTheDocument();
  });
});

describe('the keys (§3.7, trap 7)', () => {
  it('is one Tab stop, the current node, and moves with ↓ ↑ Home End', async () => {
    const user = userEvent.setup();
    draw();
    expect(items().filter((item) => item.tabIndex === 0).map((item) => item.getAttribute('data-flow-node'))).toEqual(['Taken']);
    await user.tab();
    expect(node('Taken')).toHaveFocus();
    await user.keyboard('{ArrowDown}');
    expect(node('Done')).toHaveFocus();
    await user.keyboard('{ArrowUp}{ArrowUp}');
    expect(node('Open')).toHaveFocus();
    await user.keyboard('{End}');
    expect(node('Declined')).toHaveFocus();
    await user.keyboard('{Home}');
    expect(node('Open')).toHaveFocus();
    // The roving stop follows the focus.
    expect(items().filter((item) => item.tabIndex === 0).map((item) => item.getAttribute('data-flow-node'))).toEqual(['Open']);
  });

  it('starts at the first node where none is current', async () => {
    const user = userEvent.setup();
    draw({ nodes: NODES.map(({ current: _current, ...each }) => each) });
    await user.tab();
    expect(node('Open')).toHaveFocus();
  });

  it('follows the first edge with →, and goes back along the edge it came by with ←, else its first edge in', async () => {
    const user = userEvent.setup();
    draw();
    node('Open').focus();
    await user.keyboard('{ArrowRight}');
    expect(node('Taken')).toHaveFocus();
    await user.keyboard('{ArrowRight}');
    expect(node('Done')).toHaveFocus();
    await user.keyboard('{ArrowLeft}');
    expect(node('Taken')).toHaveFocus();
    // Declined was reached by no →: ← takes its first edge in, from Open.
    node('Declined').focus();
    await user.keyboard('{ArrowLeft}');
    expect(node('Open')).toHaveFocus();
  });

  it('opens a node\'s door with Enter', async () => {
    const user = userEvent.setup();
    const open = vi.fn();
    draw({ nodes: NODES.map((each) => (each.id === 'Taken' ? { ...each, open } : each)) });
    node('Taken').focus();
    await user.keyboard('{Enter}');
    expect(open).toHaveBeenCalledOnce();
  });

  it('acts only on a key aimed at the node itself, and gives the focus back to it with Escape', async () => {
    const user = userEvent.setup();
    draw();
    const foot = within(node('Open')).getAllByRole('button')[0]!;
    foot.focus();
    await user.keyboard('{ArrowDown}');
    expect(foot).toHaveFocus();
    await user.keyboard('{Escape}');
    expect(node('Open')).toHaveFocus();
  });

  it('leaves a key an input method is composing with to the input method', () => {
    draw();
    node('Open').focus();
    fireEvent.keyDown(node('Open'), { key: 'ArrowDown', isComposing: true });
    expect(node('Open')).toHaveFocus();
  });
});

describe('the placement by the pane\'s width (§3.2, trap 4)', () => {
  it.each([[300, 'rail'], [400, 'down'], [544, 'down'], [1200, 'across']])('places a quest\'s life at %i px: %s', (width, expected) => {
    paneWidth = width;
    const { container } = draw();
    expect(placement(container)).toBe(expected);
  });

  it('keeps the focus on its node by id when a resize changes the placement', async () => {
    paneWidth = 1200;
    const { container } = draw();
    node('Done').focus();
    resizeTo(300);
    expect(placement(container)).toBe('rail');
    expect(node('Done')).toHaveFocus();
    resizeTo(544);
    expect(placement(container)).toBe('down');
    expect(node('Done')).toHaveFocus();
  });

  it('keeps the focus on its node by id when the graph grows and is laid out anew', () => {
    paneWidth = 1200;
    const { rerender } = draw();
    node('Done').focus();
    rerender(
      <Flow
        label="How #q1 moves"
        nodes={[...NODES, { id: 'Gone', title: 'Gone' }]}
        edges={[...EDGES, { id: 'gone', from: 'Done', to: 'Gone', says: 'when it is cleared' }]}
        entries={['Open']}
      />,
    );
    expect(node('Done')).toHaveFocus();
  });
});

describe('Show as list (§3.2, trap 21)', () => {
  it('is offered where the drawing fits, reads the rail when pressed, and is remembered on this machine for every flow', async () => {
    paneWidth = 1200;
    const { container, unmount } = draw();
    expect(placement(container)).toBe('across');
    await userEvent.click(screen.getByRole('button', { name: 'Show as list' }));
    expect(placement(container)).toBe('rail');
    expect(localStorage.getItem(LIST_KEY)).toBe('list');
    unmount();

    const again = draw({ label: 'Another flow' });
    expect(placement(again.container)).toBe('rail');
    await userEvent.click(screen.getByRole('button', { name: 'Show as flowchart' }));
    expect(placement(again.container)).toBe('across');
    expect(localStorage.getItem(LIST_KEY)).toBeNull();
  });

  it('is not offered where the rail is all the pane holds, since the list is already what is shown', () => {
    paneWidth = 300;
    draw();
    expect(screen.queryByRole('button', { name: 'Show as list' })).toBeNull();
  });
});

describe('a node outside the record\'s own, and a rail the caller draws (FLOWCHART1b)', () => {
  const outside = (): Partial<Parameters<typeof Flow>[0]> => ({
    nodes: [{ id: 'asked', title: 'asked by db', faint: true }, { id: 'part', title: 'Part 1', current: true }],
    edges: [{ id: 'asked>part', from: 'asked', to: 'part', says: 'asked by db' }],
    entries: ['asked'],
  });

  it('draws a faint node dashed with its words at a token ink, and does not say it was not reached', () => {
    const { container } = draw(outside());
    expect(node('asked by db').className).toMatch(/border-dashed/);
    expect(node('asked by db').className).not.toMatch(/opacity-/);
    expect(container.innerHTML).not.toMatch(/opacity-/);
    expect(node('asked by db')).toHaveAccessibleDescription('Next: Part 1, asked by db.');
    expect(node('Part 1')).toHaveAccessibleDescription('From asked by db: asked by db.');
  });

  it('draws the caller\'s own rail where only the rail fits, in the nodes\' place, and the drawing where it fits', () => {
    const rail = <ol aria-label="the strip"><li>the strip's own</li></ol>;
    // Nothing measured the pane: the rail.
    const { container, rerender } = draw({ ...outside(), rail });
    expect(placement(container)).toBe('rail');
    expect(screen.getByRole('list', { name: 'the strip' })).toBeInTheDocument();
    expect(screen.queryByRole('list', { name: 'How #q1 moves' })).toBeNull();

    // Where the drawing fits, the nodes are the list and the strip is not drawn twice.
    paneWidth = 1200;
    rerender(<Flow {...(outside() as Parameters<typeof Flow>[0])} label="How #q1 moves" rail={rail} />);
    resizeTo(1200);
    expect(placement(container)).toBe('across');
    expect(screen.queryByRole('list', { name: 'the strip' })).toBeNull();
    expect(screen.getByRole('list', { name: 'How #q1 moves' })).toBeInTheDocument();
  });

  it('draws the caller\'s rail when the person chose the list, and the drawing again on Show as flowchart (trap 21)', async () => {
    paneWidth = 1200;
    const rail = <ol aria-label="the strip"><li>the strip's own</li></ol>;
    const { container } = draw({ ...outside(), rail });
    await userEvent.click(screen.getByRole('button', { name: 'Show as list' }));
    expect(placement(container)).toBe('rail');
    expect(screen.getByRole('list', { name: 'the strip' })).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Show as flowchart' }));
    expect(placement(container)).toBe('across');
    expect(screen.queryByRole('list', { name: 'the strip' })).toBeNull();
  });

  it('says a conditional edge in the foot and the description like any other (its dashes are the measured check\'s)', () => {
    draw({
      nodes: [{ id: 'part', title: 'Part 1' }, { id: 'landing', title: 'Accept would land these', reached: false }],
      edges: [{ id: 'land', from: 'part', to: 'landing', says: 'its work would land', style: 'conditional' }],
      entries: ['part'],
    });
    expect(within(node('Part 1')).getByRole('button', { name: /Accept would land these, its work would land/ })).toBeInTheDocument();
    expect(node('Accept would land these')).toHaveAccessibleDescription('Not reached. From Part 1: its work would land.');
  });
});

describe('a stack of like nodes (FLOWCHART1c; the flowchart design §3.6 step 1)', () => {
  /** A due entry's tries, each a node like the others of its code, then the landing they ended in. */
  const tried = (codes: string[]): Partial<Parameters<typeof Flow>[0]> => {
    const ids = ['due', ...codes.map((_, index) => `t${index}`), 'landed'];
    return {
      label: 'How it landed',
      nodes: [
        { id: 'due', title: 'Due to land' },
        ...codes.map((code, index) => ({
          id: `t${index}`, title: `${code}, try ${index + 1}`,
          stack: { key: code, word: code === 'held' ? 'held for your yes' : code, at: `10:0${index + 1}` },
        })),
        { id: 'landed', title: 'Landed' },
      ],
      edges: ids.slice(1).map((to, index) => ({
        id: `e${index}`, from: ids[index]!, to,
        says: index === 0 ? 'tried' : to === 'landed' ? 'it landed' : 'tried again',
      })),
      entries: ['due'],
    };
  };
  const stackList = () => screen.getByRole('list', { name: 'How it landed' });
  const stackItem = (name: string) => within(stackList()).getByRole('listitem', { name: new RegExp(`^${name}`) });
  const titles = () => within(stackList()).getAllByRole('listitem').map((item) => item.getAttribute('data-flow-node'));

  it('folds three like nodes that follow each other into one, saying its count, its first and last time and its shared word', () => {
    draw(tried(['held', 'held', 'held']));
    expect(titles()).toEqual(['due', 'stack:t0', 'landed']);
    const fold = stackItem('held for your yes ×3');
    expect(fold).toHaveTextContent('first 10:01, last 10:03');
    expect(within(fold).getByRole('button', { name: 'Show all 3' })).toHaveAttribute('aria-expanded', 'false');
  });

  it('keeps every edge across its boundary with its cause, and says the edges between its members (a fold keeps its boundary)', () => {
    draw(tried(['held', 'held', 'held']));
    expect(stackItem('held for your yes ×3')).toHaveAccessibleDescription(
      'Holds 3 like this: held for your yes. From Due to land: tried. Next: Landed, it landed. Between them: tried again ×2.');
    expect(stackItem('Due to land')).toHaveAccessibleDescription('Next: held for your yes ×3, tried.');
    expect(stackItem('Landed')).toHaveAccessibleDescription('From held for your yes ×3: it landed.');
  });

  it('keeps two like nodes apart, so a single retry stays visible', () => {
    draw(tried(['held', 'held']));
    expect(titles()).toEqual(['due', 't0', 't1', 'landed']);
    expect(screen.queryByRole('button', { name: /Show all/ })).toBeNull();
  });

  it('opens with its press, every member drawn whole, and folds again from its first member, never remembered', async () => {
    const user = userEvent.setup();
    const { unmount } = draw(tried(['held', 'held', 'held']));
    await user.click(screen.getByRole('button', { name: 'Show all 3' }));
    expect(titles()).toEqual(['due', 't0', 't1', 't2', 'landed']);
    const again = within(stackItem('held, try 1')).getByRole('button', { name: 'Fold the 3 again' });
    expect(again).toHaveAttribute('aria-expanded', 'true');
    await user.click(again);
    expect(titles()).toEqual(['due', 'stack:t0', 'landed']);

    await user.click(screen.getByRole('button', { name: 'Show all 3' }));
    unmount();
    draw(tried(['held', 'held', 'held']));
    expect(titles()).toEqual(['due', 'stack:t0', 'landed']);
  });

  it('opens with Enter on the fold, the focus on its first member, and folds again with Enter there, the focus back on the fold', async () => {
    const user = userEvent.setup();
    draw(tried(['held', 'held', 'held']));
    stackItem('held for your yes ×3').focus();
    await user.keyboard('{Enter}');
    expect(stackItem('held, try 1')).toHaveFocus();
    expect(stackItem('held, try 1')).toHaveAccessibleDescription(/The first of 3 like it: Enter folds them again\./);
    await user.keyboard('{Enter}');
    expect(stackItem('held for your yes ×3')).toHaveFocus();
  });

  it('says it in 中文', async () => {
    await i18n.changeLanguage('zh');
    draw(tried(['held', 'held', 'held']));
    expect(within(stackItem('held for your yes ×3')).getByRole('button', { name: '展开全部 3 个' })).toBeInTheDocument();
    expect(stackItem('held for your yes ×3')).toHaveAccessibleDescription(/^共 3 个同类：held for your yes。/);
  });
});
