import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { fireEvent, render } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import i18n from '../locales/i18n';
import { type FlowCanvasProps, type FlowEdge, type FlowNode, FlowCanvas } from './FlowCanvas';

// FLOWCHART2a (D223): the canvas draws the layout, the ink and the keys, and a caller draws the nodes and the chips. A decision's
// route names its port; a data wire is read by no layout and is drawn only while one of its ends is selected. jsdom lays nothing out,
// so the pane's width is stood in for, and the geometry is the measured-box check's.

const NODES: FlowNode[] = [{ id: 'find', title: 'Find' }, { id: 'record', title: 'Record' }, { id: 'none', title: 'No further step' }, { id: 'end', title: 'End' }];
const EDGES: FlowEdge[] = [
  { id: 'a', from: 'find', to: 'record', style: 'when', port: 'release', says: 'when it comes to release', label: 'release' },
  { id: 'b', from: 'find', to: 'none', style: 'when', port: 'none', says: 'when it comes to none', label: 'none' },
  { id: 'c', from: 'record', to: 'end', says: 'automatically' },
  { id: 'd', from: 'find', to: 'end', style: 'data', says: 'with what it made', label: 'release' },
];

beforeEach(() => {
  vi.spyOn(HTMLElement.prototype, 'getBoundingClientRect').mockImplementation(function (this: HTMLElement) {
    const width = this.hasAttribute('data-flow-pane') ? 1200 : 0;
    return { width, height: 0, top: 0, left: 0, right: width, bottom: 0, x: 0, y: 0, toJSON: () => ({}) } as DOMRect;
  });
  vi.stubGlobal('ResizeObserver', class { observe() {} unobserve() {} disconnect() {} });
});

afterEach(async () => {
  vi.restoreAllMocks();
  vi.unstubAllGlobals();
  await i18n.changeLanguage('en');
});

const draw = (props: Partial<FlowCanvasProps> = {}) => render(
  <FlowCanvas
    label="How it moves"
    nodes={NODES}
    edges={EDGES}
    entries={['find']}
    sections={[{ title: 'Steps', from: 'find' }]}
    renderNode={({ node, feet }) => (
      <div role="listitem" data-flow-node={node.id} data-feet={feet.map((foot) => `${foot.to}:${foot.port ?? ''}`).join(' ')}>{node.title}</div>
    )}
    {...props}
  />,
);

describe('the nodes a caller draws', () => {
  it('are drawn in reading order, handed their feet with the port each leaves, and none for a data wire', () => {
    const { container } = draw();
    const nodes = [...container.querySelectorAll('[data-flow-node]')];
    expect(nodes.map((node) => node.getAttribute('data-flow-node'))).toEqual(['find', 'record', 'none', 'end']);
    expect(nodes[0]!.getAttribute('data-feet')).toBe('record:release none:none');
  });
});

describe('the chips a caller draws', () => {
  it('are the edge\'s label by default, and none where the caller says none', () => {
    expect([...draw().container.querySelectorAll('[data-flow-label]')].map((label) => label.textContent)).toEqual(['release', 'none']);
  });

  it('go through renderLabel, which is told the edge and its style', () => {
    const seen: string[] = [];
    const { container } = draw({
      renderLabel: ({ style, edge }) => { seen.push(`${style}:${edge?.id}`); return style === 'when' ? null : 'chip'; },
    });
    expect([...container.querySelectorAll('[data-flow-label]')].map((label) => label.textContent)).toEqual(['chip']);
    expect([...new Set(seen)]).toEqual(['when:a', 'when:b', 'next:c']);
  });
});

describe('a data wire', () => {
  it('is drawn for no flow that is not given a selection', () => {
    expect(draw().container.querySelector('[data-flow-label="d"]')).toBeNull();
  });

  it('is drawn while one of its ends is selected, and not while another step is', () => {
    expect(draw({ selected: 'find' }).container.querySelector('[data-flow-label="d"]')).not.toBeNull();
  });

  it('is not drawn while another step is selected, or none', () => {
    expect(draw({ selected: 'none' }).container.querySelector('[data-flow-label="d"]')).toBeNull();
    expect(draw({ selected: null }).container.querySelector('[data-flow-label="d"]')).toBeNull();
  });

  it('moves no node when it appears: the order and the ranks are the same selected or not', () => {
    const order = (props: Partial<FlowCanvasProps>) =>
      [...draw(props).container.querySelectorAll('[data-flow-node]')].map((node) => node.getAttribute('data-flow-node'));
    expect(order({ selected: 'find' })).toEqual(order({ selected: null }));
    expect(order({ selected: 'find' })).toEqual(order({}));
  });
});

// FLOWCHART2b (D223 §3, §8.8): what an editor adds to the canvas. A ＋ on a way stands on its path where the flow is drawn and under its
// source in the rail; pick mode walks only the steps that can take the way and never gives the focus to one that cannot.
const focusable = ({ node, stop, picking }: Parameters<FlowCanvasProps['renderNode']>[0]) => (
  <div role="listitem" data-flow-node={node.id} data-picking={picking === undefined ? undefined : String(picking)} tabIndex={stop ? 0 : -1}>{node.title}</div>
);

describe('a ＋ on a way', () => {
  const plus = (edge: FlowEdge) => (edge.style === undefined ? <button type="button" tabIndex={-1}>{`＋ ${edge.from}→${edge.to}`}</button> : null);

  it('stands on the path of each way the caller offers one, placed with the labels where the flow is drawn', () => {
    const { container } = draw({ renderInsert: plus });
    expect([...container.querySelectorAll('[data-flow-insert]')].map((each) => [each.getAttribute('data-flow-insert'), each.textContent]))
      .toEqual([['c', '＋ record→end']]);
  });

  it('stands under its source in the rail', () => {
    vi.spyOn(HTMLElement.prototype, 'getBoundingClientRect').mockImplementation(() => ({ width: 0, height: 0, top: 0, left: 0, right: 0, bottom: 0, x: 0, y: 0, toJSON: () => ({}) }) as DOMRect);
    const { container } = draw({ renderInsert: plus, renderNode: focusable });
    const record = container.querySelector('[data-flow-node="record"]')!;
    expect(container.querySelector('[data-flow-pane]')).toHaveAttribute('data-placement', 'rail');
    expect(record.parentElement!.querySelector('[data-flow-insert="c"]')).toHaveTextContent('＋ record→end');
  });
});

describe('pick mode', () => {
  const pick = (more: Partial<NonNullable<FlowCanvasProps['pick']>> = {}) => ({
    key: 'find:release', can: (id: string) => id === 'record' || id === 'end', onPick: vi.fn(), onCancel: vi.fn(), ...more,
  });

  it('gives the focus to the first step that can take the way, and walks only those, picking with Enter', async () => {
    const chosen = pick();
    const { container } = draw({ renderNode: focusable, pick: chosen });
    const node = (id: string) => container.querySelector<HTMLElement>(`[data-flow-node="${id}"]`)!;
    expect(document.activeElement).toBe(node('record'));
    expect(node('record')).toHaveAttribute('data-picking', 'true');
    expect(node('find')).toHaveAttribute('data-picking', 'false');
    const user = userEvent.setup();
    for (const key of ['{ArrowDown}', '{ArrowDown}', '{ArrowUp}', '{Home}', '{End}', '{ArrowUp}']) {
      await user.keyboard(key);
      expect(['record', 'end'], key).toContain(document.activeElement!.getAttribute('data-flow-node'));
    }
    await user.keyboard('{Enter}');
    expect(chosen.onPick).toHaveBeenCalledWith('record');
    await user.keyboard('{Escape}');
    expect(chosen.onCancel).toHaveBeenCalledOnce();
  });

  it('keeps a press on a step that cannot take the way from focusing it', () => {
    const { container } = draw({ renderNode: focusable, pick: pick() });
    const pressed = fireEvent.mouseDown(container.querySelector('[data-flow-node="find"]')!);
    expect(pressed).toBe(false);
    expect(fireEvent.mouseDown(container.querySelector('[data-flow-node="end"]')!)).toBe(true);
  });
});
