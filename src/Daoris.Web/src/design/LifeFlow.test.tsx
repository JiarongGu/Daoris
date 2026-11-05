import { afterEach, describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
import i18n from '../locales/i18n';
import { type FlowEdge, type FlowNode } from './FlowCanvas';
import { LifeFlow } from './LifeFlow';

// ONEWAY1aa: the life-flows draw through one component in the canvas's viewport, in each language; the layout's own tests are
// `FlowNodeBox.test.tsx` and `RankedFlow.test.tsx`. jsdom has no width, so the drawing below the viewport's floor, the list, is what is read.

const NODES: FlowNode[] = [
  { id: 'open', title: 'Open', reached: true },
  { id: 'done', title: 'Done', current: true, state: { word: 'here now', tone: 'taken' } },
];
const EDGES: FlowEdge[] = [{ id: 'finish', from: 'open', to: 'done', says: 'when it is finished' }];

afterEach(async () => { await i18n.changeLanguage('en'); });

describe('LifeFlow', () => {
  it('names each node by its title and keeps one Tab stop', () => {
    render(<LifeFlow label="How #q1 moves" nodes={NODES} edges={EDGES} entries={['open']} />);
    const nodes = document.querySelectorAll<HTMLElement>('[data-flow-node]');
    expect(nodes.length).toBe(2);
    expect([...nodes].filter((node) => node.tabIndex === 0).length).toBe(1);
    expect(screen.getAllByText('Done').length).toBeGreaterThan(0);
  });

  it('draws the same flow in Chinese', async () => {
    await i18n.changeLanguage('zh');
    render(<LifeFlow label="流程" nodes={[{ id: 'a', title: '开放' }, { id: 'b', title: '完成' }]} edges={[{ id: 'e', from: 'a', to: 'b', says: '完成时' }]} entries={['a']} />);
    expect(screen.getAllByText('完成').length).toBeGreaterThan(0);
  });
});
