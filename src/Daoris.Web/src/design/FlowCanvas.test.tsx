import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { render } from '@testing-library/react';
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
