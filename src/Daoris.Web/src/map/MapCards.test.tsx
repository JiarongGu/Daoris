import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { cleanup, render, screen } from '@testing-library/react';
import i18n from '../locales/i18n';
import type { NodeState } from '../design/canvas/SuppliedCanvas';
import { LayerCard, RingNode } from './MapCards';
import type { MapNode } from './topology';

// MAPUX1a (the map UX design §3): each tier of a repository says less about the same card. The canvas gives the tier; jsdom has no zoom,
// so a state is handed in.

const words = { parked: 'waiting on you', working: 'working' };
const node = (extra: Partial<MapNode> = {}): MapNode => ({ id: 'repo-03', owns: [], accepts: [], open: 3, working: false, parked: true, sessions: 1, ...extra });
const state = (extra: Partial<NodeState> = {}): NodeState => ({ tier: 'full', selected: false, focused: false, dim: false, scale: 1, ...extra });

beforeEach(() => i18n.changeLanguage('en'));
afterEach(cleanup);

describe('a repository in the layers, by tier', () => {
  it('at the full tier says its name, its count and the session in words, ringed', () => {
    const { container } = render(<LayerCard node={node()} state={state()} words={words} />);
    expect(screen.getByText('repo-03')).toBeInTheDocument();
    expect(screen.getByText('3')).toBeInTheDocument();
    expect(screen.getByText('waiting on you')).toBeInTheDocument();
    expect(container.querySelector('[data-session="parked"]')).not.toBeNull();
  });

  it('at the chip tier keeps its name, its count and its ring and drops the words', () => {
    const { container } = render(<LayerCard node={node()} state={state({ tier: 'chip', scale: 0.5 })} words={words} />);
    expect(screen.getByText('repo-03')).toBeInTheDocument();
    expect(screen.getByText('3')).toBeInTheDocument();
    expect(screen.queryByText('waiting on you')).toBeNull();
    expect(container.querySelector('[data-session="parked"]')).not.toBeNull();
  });

  it('at the dot tier says nothing inside and keeps the ring as an outline', () => {
    const { container } = render(<LayerCard node={node({ parked: false, working: true })} state={state({ tier: 'dot', scale: 0.2 })} words={words} />);
    expect(screen.queryByText('repo-03')).toBeNull();
    expect(screen.queryByText('3')).toBeNull();
    expect(container.querySelector('[data-session="working"]')).not.toBeNull();
  });

  it('keeps the name of the chosen and of the focused beside the card at the dot tier, at the type scale', () => {
    const { container, rerender } = render(<LayerCard node={node()} state={state({ tier: 'dot', scale: 0.2 })} words={words} />);
    expect(container.querySelector('[data-card-label]')).toBeNull();
    rerender(<LayerCard node={node()} state={state({ tier: 'dot', scale: 0.2, selected: true })} words={words} />);
    const chosen = container.querySelector<HTMLElement>('[data-card-label]')!;
    expect(chosen.textContent).toBe('repo-03');
    // 1 / 0.2: undoes the canvas's scale so the name is read at the type scale whatever the zoom.
    expect(chosen.style.transform).toContain('scale(5)');
    rerender(<LayerCard node={node()} state={state({ tier: 'chip', scale: 0.5, focused: true })} words={words} />);
    expect(container.querySelector('[data-card-label]')!.textContent).toBe('repo-03');
  });

  it('draws no beside-label at the full tier, where the card holds its name', () => {
    const { container } = render(<LayerCard node={node()} state={state({ selected: true })} words={words} />);
    expect(container.querySelector('[data-card-label]')).toBeNull();
  });

  it('steps back when it is not a neighbour of the choice', () => {
    const { container } = render(<LayerCard node={node()} state={state()} words={words} back />);
    expect(container.firstElementChild!.className).toContain('opacity-40');
  });

  it('says the same in Chinese: the words are the caller\'s, the id stays as written', async () => {
    await i18n.changeLanguage('zh');
    render(<LayerCard node={node()} state={state()} words={{ parked: '等你处理', working: '工作中' }} />);
    expect(screen.getByText('repo-03')).toBeInTheDocument();
    expect(screen.getByText('等你处理')).toBeInTheDocument();
  });
});

describe('a repository on the ring', () => {
  it('is drawn at the full tier with its name outside and its session in words', () => {
    render(<RingNode node={node()} x={0} y={0} state={state()} words={words} />);
    expect(screen.getByText('repo-03')).toBeInTheDocument();
    expect(screen.getByText('waiting on you')).toBeInTheDocument();
  });
});
